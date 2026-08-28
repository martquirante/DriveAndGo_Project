using DriveAndGo_API.Models;
using DriveAndGo_API.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Globalization;

namespace DriveAndGo_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TransactionsController : ControllerBase
{
    private readonly string _connectionString;
    private readonly IConfiguration _configuration;
    private readonly NotificationWriter _notificationWriter;
    private readonly IEmailService _emailService;

    public TransactionsController(
        IConfiguration configuration, 
        NotificationWriter notificationWriter, 
        IEmailService emailService)
    {
        _configuration = configuration;
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        _notificationWriter = notificationWriter;
        _emailService = emailService;
    }

    [HttpGet]
    public IActionResult GetTransactions()
    {
        try
        {
            return Ok(ReadTransactions());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpGet("rental/{rentalId:int}")]
    public IActionResult GetByRental(int rentalId)
    {
        try
        {
            return Ok(ReadTransactions(rentalId));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPost]
    public IActionResult AddTransaction([FromBody] Transaction transaction)
    {
        if (transaction.RentalId <= 0 || transaction.Amount <= 0)
        {
            return BadRequest(new { Message = "RentalId and amount are required." });
        }

        var normalizedMethod = NormalizeMethod(transaction.Method);
        var validMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cash", "gcash", "maya", "bank" };
        if (!validMethods.Contains(normalizedMethod))
        {
            return BadRequest(new { Message = "Valid methods: cash, gcash, maya, bank" });
        }

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var pendingCommand = new NpgsqlCommand(
                @"SELECT COUNT(*) FROM transactions
                  WHERE rental_id = @rental_id
                    AND LOWER(COALESCE(status, '')) = 'pending'",
                connection);
            pendingCommand.Parameters.AddWithValue("@rental_id", transaction.RentalId);

            if (Convert.ToInt32(pendingCommand.ExecuteScalar()) > 0)
            {
                return Conflict(new { Message = "A pending payment already exists for this rental." });
            }

            using var rentalCommand = new NpgsqlCommand(
                @"SELECT rental_id, customer_id, payment_status
                  FROM rentals
                  WHERE rental_id = @rental_id
                  LIMIT 1",
                connection);
            rentalCommand.Parameters.AddWithValue("@rental_id", transaction.RentalId);

            using var rentalReader = rentalCommand.ExecuteReader();
            if (!rentalReader.Read())
            {
                return NotFound(new { Message = "Rental not found." });
            }

            var customerId = Convert.ToInt32(rentalReader["customer_id"], CultureInfo.InvariantCulture);
            var paymentStatus = rentalReader["payment_status"]?.ToString() ?? string.Empty;
            rentalReader.Close();

            var normalizedType = NormalizeTransactionType(transaction.Type);
            var normalizedProof = NormalizeProofUrl(transaction.ProofUrl);
            var existingTransaction = FindExistingTransaction(connection, transaction.RentalId, transaction.Amount, normalizedType, normalizedMethod, normalizedProof);

            if (existingTransaction.HasValue)
            {
                var alreadyFinalized = existingTransaction.Value.Status is "confirmed" or "paid" or "verified";
                return Ok(new
                {
                    Message = alreadyFinalized ? "Payment already exists for this rental." : "Payment was already submitted earlier.",
                    TransactionId = existingTransaction.Value.TransactionId,
                    DuplicatePrevented = true
                });
            }

            if (normalizedType == "payment" && string.Equals(paymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { Message = "This rental is already marked as paid." });
            }

            // PostgreSQL: use RETURNING to get new ID in one round-trip
            using var insertCommand = new NpgsqlCommand(
                @"INSERT INTO transactions
                    (rental_id, amount, type, method, proof_url, status, paid_at)
                  VALUES
                    (@rental_id, @amount, @type, @method, @proof_url, 'pending', NOW())
                  RETURNING transaction_id",
                connection);
            insertCommand.Parameters.AddWithValue("@rental_id", transaction.RentalId);
            insertCommand.Parameters.AddWithValue("@amount", transaction.Amount);
            insertCommand.Parameters.AddWithValue("@type", normalizedType);
            insertCommand.Parameters.AddWithValue("@method", normalizedMethod);
            insertCommand.Parameters.AddWithValue("@proof_url", normalizedProof);

            var transactionId = Convert.ToInt32(insertCommand.ExecuteScalar(), CultureInfo.InvariantCulture);

            _notificationWriter.Create(
                connection,
                customerId,
                "Payment submitted",
                "Your payment proof was submitted and is waiting for admin review.",
                "payment");

            return Ok(new
            {
                Message = "Payment submitted successfully.",
                TransactionId = transactionId
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPatch("{id:int}/confirm")]
    public IActionResult ConfirmPayment(int id)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(
                @"SELECT t.status, t.rental_id, r.customer_id
                  FROM transactions t
                  JOIN rentals r ON r.rental_id = t.rental_id
                  WHERE t.transaction_id = @id
                  LIMIT 1",
                connection);
            command.Parameters.AddWithValue("@id", id);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return NotFound(new { Message = "Transaction not found." });
            }

            var status = reader["status"]?.ToString() ?? string.Empty;
            var rentalId = Convert.ToInt32(reader["rental_id"], CultureInfo.InvariantCulture);
            var customerId = Convert.ToInt32(reader["customer_id"], CultureInfo.InvariantCulture);
            reader.Close();

            if (!IsConfirmableStatus(status))
            {
                return BadRequest(new { Message = $"Payment cannot be confirmed because it is already '{status}'." });
            }

            using var updateTransactionCommand = new NpgsqlCommand(
                "UPDATE transactions SET status = 'confirmed' WHERE transaction_id = @id",
                connection);
            updateTransactionCommand.Parameters.AddWithValue("@id", id);
            updateTransactionCommand.ExecuteNonQuery();

            using var updateRentalCommand = new NpgsqlCommand(
                "UPDATE rentals SET payment_status = 'paid' WHERE rental_id = @rental_id",
                connection);
            updateRentalCommand.Parameters.AddWithValue("@rental_id", rentalId);
            updateRentalCommand.ExecuteNonQuery();

            _notificationWriter.Create(
                connection,
                customerId,
                "Payment confirmed",
                "Your payment has been confirmed. Your rental record is now marked as paid.",
                "payment");

            return Ok(new { Message = "Payment confirmed successfully.", TransactionId = id, RentalId = rentalId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPatch("{id:int}/reject")]
    public IActionResult RejectPayment(int id)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(
                @"SELECT t.status, r.customer_id
                  FROM transactions t
                  JOIN rentals r ON r.rental_id = t.rental_id
                  WHERE t.transaction_id = @id
                  LIMIT 1",
                connection);
            command.Parameters.AddWithValue("@id", id);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return NotFound(new { Message = "Transaction not found." });
            }

            var status = reader["status"]?.ToString() ?? string.Empty;
            var customerId = Convert.ToInt32(reader["customer_id"], CultureInfo.InvariantCulture);
            reader.Close();

            if (!IsConfirmableStatus(status))
            {
                return BadRequest(new { Message = $"Payment cannot be rejected because it is already '{status}'." });
            }

            using var updateCommand = new NpgsqlCommand(
                "UPDATE transactions SET status = 'rejected' WHERE transaction_id = @id",
                connection);
            updateCommand.Parameters.AddWithValue("@id", id);
            updateCommand.ExecuteNonQuery();

            _notificationWriter.Create(
                connection,
                customerId,
                "Payment rejected",
                "Your submitted payment was rejected. Please upload a new proof of payment.",
                "payment");

            return Ok(new { Message = "Payment rejected successfully.", TransactionId = id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpGet("summary")]
    public IActionResult GetSummary([FromQuery] string period = "monthly")
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            // PostgreSQL equivalents:
            //   DATE(col)                    → DATE_TRUNC('day', col)::date
            //   YEAR(col)                    → EXTRACT(YEAR FROM col)::int
            //   DATE_FORMAT(col, '%Y-%m')    → TO_CHAR(col, 'YYYY-MM')
            var groupBy = period == "daily"
                ? "DATE_TRUNC('day', paid_at)::date"
                : period == "yearly"
                    ? "EXTRACT(YEAR FROM paid_at)::int"
                    : "TO_CHAR(paid_at, 'YYYY-MM')";

            var label = period == "daily"
                ? "DATE_TRUNC('day', paid_at)::date"
                : period == "yearly"
                    ? "EXTRACT(YEAR FROM paid_at)::int"
                    : "TO_CHAR(paid_at, 'YYYY-MM')";

            using var command = new NpgsqlCommand(
                $@"SELECT {label} AS period, COUNT(*) AS total_transactions, SUM(amount) AS total_amount
                   FROM transactions
                   WHERE LOWER(COALESCE(status, '')) IN ('confirmed', 'paid', 'verified')
                   GROUP BY {groupBy}
                   ORDER BY {groupBy} DESC
                   LIMIT 12",
                connection);

            var summary = new List<object>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                summary.Add(new
                {
                    Period = reader["period"]?.ToString() ?? string.Empty,
                    TotalTransactions = Convert.ToInt32(reader["total_transactions"], CultureInfo.InvariantCulture),
                    TotalAmount = Convert.ToDecimal(reader["total_amount"], CultureInfo.InvariantCulture)
                });
            }

            return Ok(summary);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpGet("kpi")]
    public IActionResult GetKpi()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(@"
                SELECT 
                    COUNT(*) AS total_tx,
                    COALESCE(SUM(CASE WHEN LOWER(COALESCE(status, '')) IN ('confirmed', 'paid', 'verified') THEN amount ELSE 0 END), 0) AS total_revenue,
                    COUNT(CASE WHEN LOWER(COALESCE(status, '')) IN ('pending', 'unpaid', '') THEN 1 END) AS pending_count,
                    COUNT(CASE WHEN LOWER(COALESCE(status, '')) IN ('confirmed', 'paid', 'verified') THEN 1 END) AS confirmed_count,
                    COUNT(CASE WHEN LOWER(COALESCE(status, '')) IN ('rejected') THEN 1 END) AS rejected_count,
                    COALESCE(SUM(CASE WHEN LOWER(COALESCE(status, '')) IN ('confirmed', 'paid', 'verified') 
                                        AND paid_at >= DATE_TRUNC('month', CURRENT_DATE) THEN amount ELSE 0 END), 0) AS current_month_revenue,
                    COALESCE(SUM(CASE WHEN LOWER(COALESCE(status, '')) IN ('confirmed', 'paid', 'verified') 
                                        AND paid_at >= DATE_TRUNC('month', CURRENT_DATE - INTERVAL '1 month') 
                                        AND paid_at < DATE_TRUNC('month', CURRENT_DATE) THEN amount ELSE 0 END), 0) AS prev_month_revenue,
                    COUNT(CASE WHEN paid_at >= DATE_TRUNC('month', CURRENT_DATE) THEN 1 END) AS current_month_tx,
                    COUNT(CASE WHEN paid_at >= DATE_TRUNC('month', CURRENT_DATE - INTERVAL '1 month') 
                                AND paid_at < DATE_TRUNC('month', CURRENT_DATE) THEN 1 END) AS prev_month_tx
                FROM transactions
                WHERE LOWER(COALESCE(status, '')) <> 'duplicate'", connection);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var totalTx = Convert.ToInt32(reader["total_tx"], CultureInfo.InvariantCulture);
                var totalRevenue = Convert.ToDecimal(reader["total_revenue"], CultureInfo.InvariantCulture);
                var pendingCount = Convert.ToInt32(reader["pending_count"], CultureInfo.InvariantCulture);
                var confirmedCount = Convert.ToInt32(reader["confirmed_count"], CultureInfo.InvariantCulture);
                var currentMonthRevenue = Convert.ToDecimal(reader["current_month_revenue"], CultureInfo.InvariantCulture);
                var prevMonthRevenue = Convert.ToDecimal(reader["prev_month_revenue"], CultureInfo.InvariantCulture);
                var currentMonthTx = Convert.ToInt32(reader["current_month_tx"], CultureInfo.InvariantCulture);
                var prevMonthTx = Convert.ToInt32(reader["prev_month_tx"], CultureInfo.InvariantCulture);

                double revenueGrowth = prevMonthRevenue > 0
                    ? (double)((currentMonthRevenue - prevMonthRevenue) / prevMonthRevenue) * 100.0
                    : (currentMonthRevenue > 0 ? 100.0 : 0.0);

                double txGrowth = prevMonthTx > 0
                    ? (double)(currentMonthTx - prevMonthTx) / prevMonthTx * 100.0
                    : (currentMonthTx > 0 ? 100.0 : 0.0);

                double successRate = totalTx > 0
                    ? (double)confirmedCount / totalTx * 100.0
                    : 100.0;

                return Ok(new
                {
                    TotalRevenue = totalRevenue,
                    TotalTransactions = totalTx,
                    PendingCount = pendingCount,
                    ConfirmedCount = confirmedCount,
                    SuccessRate = Math.Round(successRate, 1),
                    RevenueGrowth = Math.Round(revenueGrowth, 1),
                    TransactionGrowth = Math.Round(txGrowth, 1)
                });
            }

            return Ok(new
            {
                TotalRevenue = 0,
                TotalTransactions = 0,
                PendingCount = 0,
                ConfirmedCount = 0,
                SuccessRate = 100.0,
                RevenueGrowth = 0.0,
                TransactionGrowth = 0.0
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }



    [HttpPost("{id:int}/email-receipt")]
    public async Task<IActionResult> EmailReceipt(int id, [FromBody] EmailReceiptRequest? req)
    {
        try
        {
            var data = await FetchTransactionReceiptDataAsync(id, null);
            if (data == null)
            {
                return NotFound(new { Message = $"Transaction #{id} not found." });
            }

            string recipientEmail = req?.RecipientEmail?.Trim() ?? data.CustomerEmail;
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                return BadRequest(new { Message = "No recipient email provided or found on customer record." });
            }

            data.CustomerEmail = recipientEmail;
            data.PersonalMessage = req?.PersonalMessage;

            string subject = $"Official Payment Receipt [{data.ReceiptNumber}] - Drive&Go Vehicle Rentals";
            string html = BuildTransactionReceiptEmailHtml(data);

            bool sent = await _emailService.SendEmailAsync(recipientEmail, subject, html);
            if (sent)
            {
                return Ok(new { Success = true, Message = $"Official receipt sent successfully to {recipientEmail}." });
            }
            else
            {
                return StatusCode(500, new { Success = false, Message = "Failed to dispatch email. Please check server SMTP credentials." });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error sending receipt: " + ex.Message });
        }
    }

    [HttpGet("verify/{code}")]
    public async Task<IActionResult> VerifyReceipt(string code)
    {
        try
        {
            var txId = ExtractTransactionIdFromCode(code);
            var data = await FetchTransactionReceiptDataAsync(txId, null);
            return Content(GetReceiptVerificationHtml(data, code), "text/html");
        }
        catch
        {
            return Content(GetReceiptVerificationHtml(null, code), "text/html");
        }
    }

    private static int ExtractTransactionIdFromCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return 0;
        var digits = System.Text.RegularExpressions.Regex.Replace(code, @"[^\d]", "");
        return int.TryParse(digits, out var id) ? id : 0;
    }

    private static readonly Dictionary<string, (string Label, string Domain, string Category)> PaymentProviderMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // E-Wallets
        ["gcash"] = ("GCash", "gcash.com", "E-Wallet"),
        ["g-cash"] = ("GCash", "gcash.com", "E-Wallet"),
        ["maya"] = ("Maya", "maya.ph", "E-Wallet"),
        ["paymaya"] = ("Maya", "maya.ph", "E-Wallet"),
        ["shopeepay"] = ("ShopeePay", "shopee.ph", "E-Wallet"),
        ["grabpay"] = ("GrabPay", "grab.com", "E-Wallet"),
        ["coins"] = ("Coins.ph", "coins.ph", "E-Wallet"),
        ["coins.ph"] = ("Coins.ph", "coins.ph", "E-Wallet"),
        ["palawanpay"] = ("PalawanPay", "palawanpay.com", "E-Wallet"),

        // Digital Neobanks
        ["gotyme"] = ("GoTyme Bank", "gotyme.com.ph", "Digital Bank"),
        ["maribank"] = ("MariBank", "maribank.ph", "Digital Bank"),
        ["seabank"] = ("SeaBank", "seabank.com.ph", "Digital Bank"),
        ["cimb"] = ("CIMB Bank", "cimbbank.com.ph", "Digital Bank"),
        ["tonik"] = ("Tonik Bank", "tonikbank.com", "Digital Bank"),
        ["komo"] = ("Komo", "komo.ph", "Digital Bank"),

        // Traditional Banks
        ["bdo"] = ("BDO Unibank", "bdo.com.ph", "Bank"),
        ["bpi"] = ("BPI", "bpi.com.ph", "Bank"),
        ["unionbank"] = ("UnionBank", "unionbankph.com", "Bank"),
        ["metrobank"] = ("Metrobank", "metrobank.com.ph", "Bank"),
        ["rcbc"] = ("RCBC", "rcbc.com", "Bank"),
        ["securitybank"] = ("Security Bank", "securitybank.com", "Bank"),
        ["landbank"] = ("Landbank", "landbank.com", "Bank"),
        ["pnb"] = ("PNB", "pnb.com.ph", "Bank"),
        ["chinabank"] = ("Chinabank", "chinabank.ph", "Bank"),
        ["eastwest"] = ("EastWest Bank", "eastwestbanker.com", "Bank"),
        ["psbank"] = ("PSBank", "psbank.com.ph", "Bank"),
        ["aub"] = ("AUB", "aub.com.ph", "Bank"),

        // Cards & Cash
        ["visa"] = ("Visa", "visa.com", "Card"),
        ["mastercard"] = ("Mastercard", "mastercard.com", "Card"),
        ["card"] = ("Card", "visa.com", "Card")
    };

    [HttpGet("payment-methods")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public IActionResult GetPaymentMethods()
    {
        string serverBase = DriveAndGo_API.Helpers.NetworkHelper.GetServerBaseUrl(_configuration);
        var list = PaymentProviderMap
            .GroupBy(x => x.Value.Domain)
            .Select(g => g.First())
            .Select(kvp => new
            {
                Id = kvp.Key,
                Name = kvp.Value.Label,
                Category = kvp.Value.Category,
                Domain = kvp.Value.Domain,
                LogoUrl = $"{serverBase}/api/transactions/provider-logo/{kvp.Key}"
            })
            .ToList();

        list.Add(new
        {
            Id = "cash",
            Name = "Cash",
            Category = "Cash",
            Domain = "",
            LogoUrl = $"{serverBase}/api/transactions/provider-logo/cash"
        });

        return Ok(list);
    }

    [HttpGet("provider-logo/{provider}")]
    [ResponseCache(Duration = 604800, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetProviderLogo(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider)) return NotFound();

        string key = provider.Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");

        // 0. Generic Bank Transfer / InstaPay / PESONet vector landmark icon
        if (key == "bank" || key == "banktransfer" || key == "instapay" || key == "pesonet")
        {
            string bankSvg = @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' width='96' height='96' fill='none' stroke='#A855F7' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'>
              <rect width='24' height='24' rx='6' fill='#581C87' fill-opacity='0.25' stroke='none'/>
              <line x1='3' y1='21' x2='21' y2='21' stroke='#C084FC'/>
              <line x1='3' y1='10' x2='21' y2='10' stroke='#C084FC'/>
              <polygon points='12 3 2 10 22 10' fill='#A855F7' stroke='#C084FC'/>
              <line x1='6' y1='14' x2='6' y2='18' stroke='#E9D5FF'/>
              <line x1='10' y1='14' x2='10' y2='18' stroke='#E9D5FF'/>
              <line x1='14' y1='14' x2='14' y2='18' stroke='#E9D5FF'/>
              <line x1='18' y1='14' x2='18' y2='18' stroke='#E9D5FF'/>
            </svg>";
            Response.Headers["Cache-Control"] = "public, max-age=604800";
            return File(System.Text.Encoding.UTF8.GetBytes(bankSvg), "image/svg+xml");
        }

        // 1. Check local payment icons in wwwroot/payments/
        try
        {
            string wwwPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "payments", $"{key}.png");
            if (System.IO.File.Exists(wwwPath))
            {
                var bytes = await System.IO.File.ReadAllBytesAsync(wwwPath);
                return File(bytes, "image/png");
            }
        }
        catch { }

        // 2. Resolve domain
        string domain = "bdo.com.ph";
        if (PaymentProviderMap.TryGetValue(key, out var meta))
        {
            domain = meta.Domain;
        }
        else
        {
            domain = $"{key}.com.ph";
        }

        // 3. Fetch from Unavatar or Google Favicon CDN
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var unavatarUrl = $"https://unavatar.io/{domain}?fallback=https://www.google.com/s2/favicons?domain={domain}&sz=128";
            var resp = await httpClient.GetAsync(unavatarUrl);
            if (resp.IsSuccessStatusCode)
            {
                var bytes = await resp.Content.ReadAsByteArrayAsync();
                var contentType = resp.Content.Headers.ContentType?.MediaType ?? "image/png";
                Response.Headers["Cache-Control"] = "public, max-age=604800";
                return File(bytes, contentType);
            }
        }
        catch { }

        // 4. Secondary Fallback to Google Favicon 128px
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var gUrl = $"https://www.google.com/s2/favicons?domain={domain}&sz=128";
            var resp = await httpClient.GetAsync(gUrl);
            if (resp.IsSuccessStatusCode)
            {
                var bytes = await resp.Content.ReadAsByteArrayAsync();
                Response.Headers["Cache-Control"] = "public, max-age=604800";
                return File(bytes, "image/png");
            }
        }
        catch { }

        // 5. Dynamic Fail-Safe: Return high-contrast vector SVG badge so image never breaks
        string init = provider.Trim().Length >= 2 ? provider.Trim()[..2].ToUpperInvariant() : provider.Trim().ToUpperInvariant();
        string pSvg = $@"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100' width='100' height='100'>
          <rect width='100' height='100' rx='24' fill='#0F172A'/>
          <text x='50%' y='55%' dominant-baseline='middle' text-anchor='middle' font-family='Segoe UI, Arial, sans-serif' font-weight='900' font-size='38' fill='#FF6B00'>{init}</text>
        </svg>";
        var pBytes = System.Text.Encoding.UTF8.GetBytes(pSvg);
        Response.Headers["Cache-Control"] = "public, max-age=86400";
        return File(pBytes, "image/svg+xml");
    }

    private async Task<TransactionReceiptPdfData?> FetchTransactionReceiptDataAsync(int id, string? adminName)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = new NpgsqlCommand(@"
            SELECT 
                t.transaction_id, t.rental_id, t.amount, t.type, t.method, t.status, t.paid_at, t.proof_url,
                u.full_name AS customer_name, u.email AS customer_email, u.phone AS customer_phone,
                CONCAT(v.brand, ' ', v.model) AS vehicle_name, v.plate_no, v.color AS vehicle_color,
                r.start_date, r.end_date, r.total_amount
            FROM transactions t
            JOIN rentals r ON t.rental_id = r.rental_id
            JOIN users u ON r.customer_id = u.user_id
            JOIN vehicles v ON r.vehicle_id = v.vehicle_id
            WHERE t.transaction_id = @id", connection);

        cmd.Parameters.AddWithValue("@id", id);
        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        var txId = Convert.ToInt32(reader["transaction_id"], CultureInfo.InvariantCulture);
        var rId = Convert.ToInt32(reader["rental_id"], CultureInfo.InvariantCulture);
        var rentalCode = $"DG-{rId:D5}";
        var customerName = reader["customer_name"]?.ToString() ?? "Valued Customer";
        var customerEmail = reader["customer_email"]?.ToString() ?? "";
        var customerPhone = reader["customer_phone"]?.ToString() ?? "+63 900 000 0000";
        var vehicleName = reader["vehicle_name"]?.ToString() ?? "Drive&Go Rental Unit";
        var plateNo = reader["plate_no"]?.ToString() ?? "N/A";
        var vehicleColor = reader["vehicle_color"]?.ToString() ?? "";
        var amount = Convert.ToDecimal(reader["amount"], CultureInfo.InvariantCulture);
        var method = (reader["method"]?.ToString() ?? "Cash").ToUpperInvariant();
        var status = (reader["status"]?.ToString() ?? "Confirmed").ToUpperInvariant();
        var paidAt = reader["paid_at"] != DBNull.Value ? Convert.ToDateTime(reader["paid_at"]) : DateTime.Now;

        DateTime? sDate = reader["start_date"] != DBNull.Value ? Convert.ToDateTime(reader["start_date"]) : null;
        DateTime? eDate = reader["end_date"] != DBNull.Value ? Convert.ToDateTime(reader["end_date"]) : null;
        int durationDays = sDate.HasValue && eDate.HasValue ? Math.Max(1, (int)Math.Ceiling((eDate.Value - sDate.Value).TotalDays)) : 1;

        decimal dailyRate = durationDays > 0 ? (amount > 0 ? amount / durationDays : 3000m) : amount;
        decimal deposit = 0m;
        decimal discount = 0m;

        string receiptNo = $"TX-{txId:D6}";

        return new TransactionReceiptPdfData
        {
            TransactionId = txId,
            ReceiptNumber = receiptNo,
            RentalCode = rentalCode,
            RentalId = rId,
            CustomerName = customerName,
            CustomerEmail = customerEmail,
            CustomerPhone = customerPhone,
            VehicleName = vehicleName,
            PlateNo = plateNo,
            VehicleColor = vehicleColor,
            PickupDate = sDate?.ToString("MMM dd, yyyy hh:mm tt") ?? "",
            DropoffDate = eDate?.ToString("MMM dd, yyyy hh:mm tt") ?? "",
            DurationDays = durationDays,
            DailyRate = dailyRate,
            RentalSubtotal = dailyRate * durationDays,
            SecurityDeposit = deposit,
            DiscountAmount = discount,
            TotalAmount = amount,
            AmountInWords = ConvertNumberToWords(amount),
            PaymentMethod = method,
            Status = status,
            TransactionDate = paidAt.ToString("MMM dd, yyyy hh:mm tt"),
            AdminName = !string.IsNullOrWhiteSpace(adminName) ? adminName : "Raymart Quirante",
            ProofUrl = reader["proof_url"] == DBNull.Value ? null : reader["proof_url"].ToString(),
            VerificationUrl = $"{DriveAndGo_API.Helpers.NetworkHelper.GetServerBaseUrl(_configuration)}/api/transactions/verify/{receiptNo}"
        };
    }

    private static string ConvertNumberToWords(decimal num)
    {
        if (num <= 0) return "Zero Pesos Only";
        string[] a = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
        string[] b = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

        string InWords(long n)
        {
            if (n < 20) return a[n];
            long digit = n % 10;
            return b[n / 10] + (digit > 0 ? " " + a[digit] : "");
        }

        string ConvertInt(long n)
        {
            if (n < 100) return InWords(n);
            if (n < 1000) return a[n / 100] + " Hundred" + (n % 100 > 0 ? " " + ConvertInt(n % 100) : "");
            if (n < 1000000) return ConvertInt(n / 1000) + " Thousand" + (n % 1000 > 0 ? " " + ConvertInt(n % 1000) : "");
            return ConvertInt(n / 1000000) + " Million" + (n % 1000000 > 0 ? " " + ConvertInt(n % 1000000) : "");
        }

        long whole = (long)Math.Floor(num);
        long cents = (long)Math.Round((num - whole) * 100);
        string result = ConvertInt(whole) + " Pesos";
        if (cents > 0) result += " and " + ConvertInt(cents) + " Centavos";
        return result + " Only";
    }

    private static string BuildTransactionReceiptEmailHtml(TransactionReceiptPdfData d)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<style>
  body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #f4f5fa; margin: 0; padding: 24px; color: #1e293b; }}
  .card {{ max-width: 580px; margin: 0 auto; background: #ffffff; border-radius: 16px; border: 1px solid #e2e8f0; overflow: hidden; box-shadow: 0 10px 30px rgba(0,0,0,0.06); }}
  .header {{ background: linear-gradient(135deg, #090D16 0%, #1e293b 100%); color: #ffffff; padding: 28px 24px; text-align: center; border-bottom: 3px solid #ff6b00; }}
  .header h1 {{ margin: 0; font-size: 22px; letter-spacing: 0.5px; color: #ff6b00; font-weight: 800; }}
  .header p {{ margin: 4px 0 0 0; font-size: 12px; color: #94a3b8; }}
  .badge {{ display: inline-block; padding: 4px 14px; border-radius: 999px; font-size: 11px; font-weight: 800; background: #dcfce7; color: #15803d; margin-top: 12px; }}
  .content {{ padding: 24px 28px; }}
  .grid {{ width: 100%; border-collapse: collapse; margin-bottom: 18px; }}
  .grid td {{ padding: 9px 0; border-bottom: 1px solid #f1f5f9; font-size: 13.5px; }}
  .label {{ color: #64748b; font-weight: 500; width: 42%; }}
  .value {{ color: #0f172a; font-weight: 600; text-align: right; }}
  .total-box {{ background: #fff7ed; border: 1px solid #fed7aa; border-radius: 12px; padding: 18px; text-align: center; margin: 18px 0; }}
  .total-label {{ font-size: 11px; color: #c2410c; font-weight: 800; text-transform: uppercase; letter-spacing: 1px; }}
  .total-amt {{ font-size: 26px; color: #ea580c; font-weight: 800; margin-top: 4px; }}
  .words {{ font-size: 11px; color: #9a3412; margin-top: 4px; font-weight: 600; }}
  .footer {{ background: #f8fafc; padding: 20px; text-align: center; font-size: 11.5px; color: #94a3b8; border-top: 1px solid #e2e8f0; }}
</style>
</head>
<body>
  <div class='card'>
    <div class='header'>
      <img src='https://raw.githubusercontent.com/martquirante/DriveAndGo_Project/main/DriveAndGo_Admin/WebAssets/logo.png' alt='Drive&Go' style='height:48px;width:auto;object-fit:contain;margin:0 auto 10px auto;display:block;' />
      <h1>DRIVE&GO VEHICLE RENTALS</h1>
      <p>San Jose del Monte, Bulacan · Official Electronic Payment Receipt</p>
      <div class='badge'>{d.Status.ToUpper()}</div>
    </div>
    <div class='content'>
      {(string.IsNullOrWhiteSpace(d.PersonalMessage) ? "" : $"<div style='background:#f8fafc;border-left:3px solid #ff6b00;padding:10px 14px;border-radius:0 8px 8px 0;margin-bottom:16px;font-size:13px;color:#334155;'><strong>Note from Admin:</strong> {d.PersonalMessage}</div>")}
      <table class='grid'>
        <tr><td class='label'>Receipt Number</td><td class='value' style='font-family:monospace;color:#ff6b00;'>{d.ReceiptNumber}</td></tr>
        <tr><td class='label'>Rental Reference</td><td class='value' style='font-family:monospace;'>{d.RentalCode}</td></tr>
        <tr><td class='label'>Customer Name</td><td class='value'>{d.CustomerName}</td></tr>
        <tr><td class='label'>Vehicle Unit</td><td class='value'>{d.VehicleName} ({d.PlateNo})</td></tr>
        <tr><td class='label'>Payment Channel</td><td class='value'>{d.PaymentMethod}</td></tr>
        <tr><td class='label'>Transaction Date</td><td class='value'>{d.TransactionDate}</td></tr>
      </table>
      <div class='total-box'>
        <div class='total-label'>Total Amount Paid</div>
        <div class='total-amt'>PHP {d.TotalAmount:N2}</div>
        <div class='words'>{d.AmountInWords}</div>
      </div>

      <!-- Official Verification QR Code -->
      <div style='background:#f8fafc; border:1px solid #e2e8f0; border-radius:12px; padding:16px; text-align:center; margin:20px 0;'>
        <div style='font-size:11px; font-weight:800; color:#475569; text-transform:uppercase; letter-spacing:0.8px; margin-bottom:10px;'>
          Official Digital Verification Seal
        </div>
        <img src='https://api.qrserver.com/v1/create-qr-code/?size=140x140&margin=4&data={Uri.EscapeDataString(string.IsNullOrWhiteSpace(d.VerificationUrl) ? $"https://driveandgo.com/verify/{d.ReceiptNumber}" : d.VerificationUrl)}' alt='Verification QR Code' style='width:130px; height:130px; border-radius:8px; border:1px solid #cbd5e1; display:inline-block;' />
        <div style='font-size:11.5px; color:#64748b; font-weight:600; margin-top:8px;'>
          Scan with mobile camera to verify receipt authenticity
        </div>
        <div style='font-size:10.5px; color:#94a3b8; font-family:monospace; margin-top:4px;'>
          Receipt Ref: {d.ReceiptNumber}
        </div>
      </div>

      <p style='font-size: 12.5px; color: #64748b; line-height: 1.5; text-align: center; margin: 12px 0;'>
        Please find your official electronic payment receipt. Your transaction is verified and permanently recorded in the Drive&Go master ledger.
      </p>
    </div>
    <div class='footer'>
      Drive&Go Vehicle Rental System • Automated Notification<br>
      CSJDM | Norzagaray, Bulacan, Philippines • Hotline: +63 935 966 7178
    </div>
  </div>
</body>
</html>";
    }

    public class EmailReceiptRequest
    {
        public string? RecipientEmail { get; set; }
        public string? PersonalMessage { get; set; }
    }

    private List<Transaction> ReadTransactions(int? rentalId = null)
    {
        var transactions = new List<Transaction>();

        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        var sql =
            @"SELECT
                t.transaction_id,
                t.rental_id,
                t.amount,
                t.type,
                t.method,
                t.proof_url,
                t.status,
                t.paid_at,
                u.full_name AS customer_name,
                u.email AS customer_email,
                u.phone AS customer_phone,
                CONCAT(v.brand, ' ', v.model) AS vehicle_name,
                v.plate_no
              FROM transactions t
              JOIN rentals r ON t.rental_id = r.rental_id
              JOIN users u ON r.customer_id = u.user_id
              JOIN vehicles v ON r.vehicle_id = v.vehicle_id
              WHERE LOWER(COALESCE(t.status, '')) <> 'duplicate' ";

        if (rentalId.HasValue)
        {
            sql += "AND t.rental_id = @rental_id ";
        }

        sql += "ORDER BY t.paid_at DESC";

        using var command = new NpgsqlCommand(sql, connection);
        if (rentalId.HasValue)
        {
            command.Parameters.AddWithValue("@rental_id", rentalId.Value);
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            transactions.Add(new Transaction
            {
                TransactionId = Convert.ToInt32(reader["transaction_id"], CultureInfo.InvariantCulture),
                RentalId      = Convert.ToInt32(reader["rental_id"], CultureInfo.InvariantCulture),
                Amount        = Convert.ToDecimal(reader["amount"], CultureInfo.InvariantCulture),
                Type          = reader["type"]?.ToString(),
                Method        = reader["method"]?.ToString(),
                ProofUrl      = reader["proof_url"] == DBNull.Value ? null : reader["proof_url"].ToString(),
                Status        = reader["status"]?.ToString(),
                PaidAt        = reader["paid_at"] == DBNull.Value ? null : Convert.ToDateTime(reader["paid_at"], CultureInfo.InvariantCulture),
                CustomerName  = reader["customer_name"]?.ToString(),
                CustomerEmail = reader["customer_email"] == DBNull.Value ? null : reader["customer_email"].ToString(),
                CustomerPhone = reader["customer_phone"] == DBNull.Value ? null : reader["customer_phone"].ToString(),
                VehicleName   = reader["vehicle_name"]?.ToString(),
                PlateNo       = reader["plate_no"] == DBNull.Value ? null : reader["plate_no"].ToString()
            });
        }

        return DeduplicateTransactions(transactions);
    }

    private static string NormalizeTransactionType(string? type)
    {
        var normalized = type?.Trim().ToLowerInvariant() ?? "payment";
        return normalized switch
        {
            "" => "payment",
            "rental" => "payment",
            _ => normalized
        };
    }

    private static string NormalizeMethod(string? method)
    {
        return string.IsNullOrWhiteSpace(method)
            ? "cash"
            : method.Trim().ToLowerInvariant();
    }

    private static string NormalizeProofUrl(string? proofUrl)
    {
        return string.IsNullOrWhiteSpace(proofUrl)
            ? string.Empty
            : proofUrl.Trim();
    }

    private static bool IsConfirmableStatus(string? status)
    {
        var normalized = status?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized is "pending" or "verified" or "";
    }

    private static (int TransactionId, string Status)? FindExistingTransaction(
        NpgsqlConnection connection,
        int rentalId,
        decimal amount,
        string type,
        string method,
        string proofUrl)
    {
        const string sql =
            @"SELECT
                transaction_id,
                LOWER(COALESCE(status, '')) AS normalized_status
              FROM transactions
              WHERE rental_id = @rental_id
                AND ABS(COALESCE(amount, 0) - @amount) < 0.01
                AND (CASE WHEN LOWER(COALESCE(type, '')) IN ('', 'rental') THEN 'payment' ELSE LOWER(COALESCE(type, '')) END) = @type
                AND LOWER(COALESCE(method, '')) = @method
                AND COALESCE(NULLIF(TRIM(COALESCE(proof_url, '')), ''), '') = @proof_url
                AND LOWER(COALESCE(status, '')) NOT IN ('rejected', 'refunded', 'duplicate')
              ORDER BY CASE LOWER(COALESCE(status, ''))
                    WHEN 'paid' THEN 0
                    WHEN 'confirmed' THEN 1
                    WHEN 'verified' THEN 2
                    WHEN 'pending' THEN 3
                    WHEN '' THEN 4
                    ELSE 5
                END,
                transaction_id DESC
              LIMIT 1";

        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@rental_id", rentalId);
        command.Parameters.AddWithValue("@amount", amount);
        command.Parameters.AddWithValue("@type", type);
        command.Parameters.AddWithValue("@method", method);
        command.Parameters.AddWithValue("@proof_url", proofUrl);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return (
            Convert.ToInt32(reader["transaction_id"], CultureInfo.InvariantCulture),
            reader["normalized_status"]?.ToString() ?? string.Empty);
    }

    private static List<Transaction> DeduplicateTransactions(IEnumerable<Transaction> transactions)
    {
        return transactions
            .GroupBy(BuildTransactionKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(tx => GetStatusRank(NormalizeStatus(tx.Status)))
                .ThenBy(tx => string.IsNullOrWhiteSpace(tx.Type) ? 1 : 0)
                .ThenByDescending(tx => tx.PaidAt ?? DateTime.MinValue)
                .ThenByDescending(tx => tx.TransactionId)
                .First())
            .OrderByDescending(tx => tx.PaidAt ?? DateTime.MinValue)
            .ThenByDescending(tx => tx.TransactionId)
            .ToList();
    }

    private static string BuildTransactionKey(Transaction tx)
    {
        return string.Join(
            "|",
            tx.RentalId.ToString(CultureInfo.InvariantCulture),
            NormalizeTransactionType(tx.Type),
            tx.Amount.ToString("0.##", CultureInfo.InvariantCulture),
            NormalizeMethod(tx.Method),
            NormalizeProofUrl(tx.ProofUrl));
    }

    private static string NormalizeStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            ? string.Empty
            : status.Trim().ToLowerInvariant();
    }

    private static int GetStatusRank(string status)
    {
        return status switch
        {
            "paid"      => 0,
            "confirmed" => 1,
            "verified"  => 2,
            "pending"   => 3,
            ""          => 4,
            _           => 5
        };
    }

    private static string GetReceiptVerificationHtml(TransactionReceiptPdfData? d, string code)
    {
        bool found = d != null;
        string logoUrl = "https://raw.githubusercontent.com/martquirante/DriveAndGo_Project/main/DriveAndGo_Admin/WebAssets/logo.png";

        if (!found)
        {
            return $@"<!DOCTYPE html>
<html lang='en'>
<head>
  <meta charset='utf-8'/>
  <meta name='viewport' content='width=device-width, initial-scale=1.0'/>
  <title>Invalid Receipt - Drive&Go Verification</title>
  <script src='https://cdn.tailwindcss.com'></script>
</head>
<body class='bg-[#090D16] text-white min-h-screen flex items-center justify-center p-4 font-sans'>
  <div class='max-w-md w-full bg-[#0E1626] border border-red-500/30 rounded-3xl p-6 text-center shadow-2xl'>
    <div class='w-16 h-16 rounded-2xl bg-red-500/15 text-red-400 mx-auto flex items-center justify-center mb-4 border border-red-500/30'>
      <svg class='w-8 h-8' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z'/></svg>
    </div>
    <h1 class='text-xl font-bold text-red-400 mb-2'>Receipt Not Recognized</h1>
    <p class='text-xs text-slate-400 mb-4'>The scanned code <span class='font-mono font-bold text-white'>{code}</span> could not be verified in the Drive&Go National Ledger.</p>
    <div class='p-3.5 rounded-xl bg-slate-900/60 border border-slate-800 text-[11px] text-slate-400'>
      If you believe this is an error, please contact Drive&Go Support hotline: <strong class='text-[#FF6B00]'>+63 935 966 7178</strong>
    </div>
  </div>
</body>
</html>";
        }

        string txCode = d!.ReceiptNumber;
        string rentalCode = d.RentalCode;
        string custName = d.CustomerName;
        string custEmail = string.IsNullOrWhiteSpace(d.CustomerEmail) ? "—" : d.CustomerEmail;
        string vehicle = d.VehicleName;
        string plate = string.IsNullOrWhiteSpace(d.PlateNo) ? "N/A" : d.PlateNo;
        string amt = d.TotalAmount.ToString("N2");
        string method = d.PaymentMethod.ToUpperInvariant();
        string status = d.Status.ToUpperInvariant();
        string dateStr = d.TransactionDate;
        string admin = d.AdminName;
        string words = d.AmountInWords;

        return $@"<!DOCTYPE html>
<html lang='en' class='dark'>
<head>
  <meta charset='utf-8'/>
  <meta name='viewport' content='width=device-width, initial-scale=1.0'/>
  <title>Verified Receipt - {txCode}</title>
  <script src='https://cdn.tailwindcss.com'></script>
  <script>
    tailwind.config = {{
      darkMode: 'class',
      theme: {{
        extend: {{
          colors: {{
            brand: {{ DEFAULT: '#FF6B00', hover: '#E85F00' }}
          }}
        }}
      }}
    }};
  </script>
  <style>
    @keyframes pulse-ring {{
      0% {{ transform: scale(0.95); opacity: 0.8; }}
      50% {{ transform: scale(1.05); opacity: 0.3; }}
      100% {{ transform: scale(0.95); opacity: 0.8; }}
    }}
    .pulse-ring {{ animation: pulse-ring 2.5s infinite ease-in-out; }}
  </style>
</head>
<body class='bg-slate-100 dark:bg-[#070B14] text-slate-900 dark:text-slate-100 min-h-screen flex items-center justify-center p-3 sm:p-6 font-sans transition-colors duration-200'>
  
  <div class='max-w-md w-full bg-white dark:bg-[#0E1626] border border-slate-200 dark:border-slate-800 rounded-3xl overflow-hidden shadow-2xl transition-colors duration-200'>
    
    <!-- Top Header with Brand & Theme Toggle -->
    <div class='px-5 py-3.5 bg-slate-50 dark:bg-[#0B111E] border-b border-slate-200 dark:border-slate-800/80 flex items-center justify-between transition-colors'>
      <div class='flex items-center gap-2.5'>
        <img src='{logoUrl}' alt='Drive&Go' class='w-9 h-9 rounded-xl object-contain bg-white p-0.5 border border-slate-200/80 shadow-xs shrink-0' onerror=""this.style.display='none';"" />
        <div>
          <div class='text-xs font-black tracking-tight text-slate-900 dark:text-white flex items-center gap-0.5'>
            <span>DRIVE</span><span class='text-[#FF6B00]'>&</span><span>GO</span>
          </div>
          <div class='text-[9px] font-bold text-slate-500 dark:text-slate-400 uppercase tracking-widest leading-none mt-0.5'>Official Verification</div>
        </div>
      </div>
      
      <!-- Theme Switcher Button -->
      <div class='flex items-center gap-2'>
        <button id='themeToggle' onclick='toggleTheme()' type='button' class='flex items-center gap-1 px-2.5 py-1.5 rounded-xl bg-white dark:bg-slate-800/90 text-slate-700 dark:text-slate-200 border border-slate-200 dark:border-slate-700 hover:border-slate-300 dark:hover:border-slate-600 transition-all shadow-xs text-[11px] font-bold' title='Toggle Dark / Light Theme'>
          <!-- Sun Icon (shown in dark mode) -->
          <svg id='iconSun' class='w-3.5 h-3.5 text-amber-500 hidden' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z'/></svg>
          <!-- Moon Icon (shown in light mode) -->
          <svg id='iconMoon' class='w-3.5 h-3.5 text-slate-600 dark:text-slate-300' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z'/></svg>
          <span id='themeText' class='text-[10px] uppercase font-bold tracking-wider'>Mode</span>
        </button>
      </div>
    </div>

    <!-- Verification Badge Banner -->
    <div class='p-6 bg-gradient-to-b from-emerald-500/10 via-emerald-900/10 dark:via-emerald-950/20 to-transparent border-b border-emerald-500/20 text-center relative'>
      <div class='w-16 h-16 rounded-2xl bg-emerald-500/15 dark:bg-emerald-500/20 text-emerald-600 dark:text-emerald-400 mx-auto flex items-center justify-center mb-3 border border-emerald-500/30 dark:border-emerald-500/40 relative shadow-sm'>
        <div class='absolute inset-0 rounded-2xl border border-emerald-400/40 pulse-ring pointer-events-none'></div>
        <svg class='w-8 h-8' fill='none' stroke='currentColor' viewBox='0 0 24 24'>
          <path stroke-linecap='round' stroke-linejoin='round' stroke-width='2.5' d='M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z'/>
        </svg>
      </div>

      <div class='inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-[10px] font-black uppercase tracking-wider bg-emerald-500/15 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400 border border-emerald-500/30'>
        <svg class='w-3 h-3' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='3' d='M5 13l4 4L19 7'/></svg>
        <span>Verified Proof of Payment</span>
      </div>

      <h1 class='text-2xl font-black text-slate-900 dark:text-white mt-2 font-mono tracking-tight'>{txCode}</h1>
      <p class='text-xs text-slate-500 dark:text-slate-400 mt-0.5'>{dateStr}</p>
    </div>

    <!-- Details Body -->
    <div class='p-5 sm:p-6 space-y-3.5 text-xs'>
      
      <!-- Billed To & Rental Grid -->
      <div class='grid grid-cols-2 gap-2.5'>
        <div class='p-3 rounded-2xl bg-slate-50 dark:bg-slate-900/60 border border-slate-200 dark:border-slate-800/80 transition-colors'>
          <div class='text-[9.5px] font-bold text-slate-500 dark:text-slate-400 uppercase tracking-wider mb-1'>Customer</div>
          <div class='font-bold text-slate-900 dark:text-white truncate'>{custName}</div>
          <div class='text-[10.5px] text-slate-500 dark:text-slate-400 truncate mt-0.5'>{custEmail}</div>
        </div>
        <div class='p-3 rounded-2xl bg-slate-50 dark:bg-slate-900/60 border border-slate-200 dark:border-slate-800/80 transition-colors'>
          <div class='text-[9.5px] font-bold text-slate-500 dark:text-slate-400 uppercase tracking-wider mb-1'>Rental & Unit</div>
          <div class='font-mono font-bold text-[#FF6B00]'>{rentalCode}</div>
          <div class='text-[10.5px] text-slate-900 dark:text-white truncate mt-0.5'>{vehicle} ({plate})</div>
        </div>
      </div>

      <!-- Settlement Status Row -->
      <div class='p-3 rounded-2xl bg-slate-50 dark:bg-slate-900/60 border border-slate-200 dark:border-slate-800/80 flex items-center justify-between transition-colors'>
        <div>
          <div class='text-[9.5px] font-bold text-slate-500 dark:text-slate-400 uppercase tracking-wider'>Payment Channel</div>
          <div class='text-xs font-bold text-slate-900 dark:text-white mt-0.5'>{method}</div>
        </div>
        <span class='px-3 py-1 rounded-full text-[10px] font-black uppercase tracking-wider bg-emerald-500/15 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400 border border-emerald-500/30'>
          {status}
        </span>
      </div>

      <!-- Financial Total Box -->
      <div class='p-4 rounded-2xl bg-amber-500/10 dark:bg-[#FF6B00]/10 border border-amber-500/30 dark:border-[#FF6B00]/30 text-center transition-colors'>
        <div class='text-[10px] font-bold text-[#FF6B00] uppercase tracking-wider'>Total Settlement Paid</div>
        <div class='text-3xl font-black text-[#FF6B00] tracking-tight mt-1'>₱{amt}</div>
        <div class='text-[10.5px] text-amber-800 dark:text-orange-200/80 font-medium italic mt-1'>{words}</div>
      </div>

      <!-- Verification Authentication Stamp -->
      <div class='p-3.5 rounded-2xl bg-slate-50 dark:bg-slate-900/40 border border-slate-200 dark:border-slate-800 text-[10.5px] text-slate-600 dark:text-slate-400 space-y-1.5 transition-colors'>
        <div class='flex justify-between'>
          <span>Authorized Cashier:</span>
          <strong class='text-slate-900 dark:text-white'>{admin}</strong>
        </div>
        <div class='flex justify-between'>
          <span>Tamper-Proof Seal:</span>
          <span class='font-mono text-emerald-600 dark:text-emerald-400 font-bold'>VALIDATED · {txCode}</span>
        </div>
        <div class='flex justify-between'>
          <span>Authority:</span>
          <span class='text-slate-700 dark:text-slate-300'>Drive&Go Vehicle Rental System</span>
        </div>
      </div>

    </div>

    <!-- Footer -->
    <div class='px-6 py-4 bg-slate-50 dark:bg-[#090D16] border-t border-slate-200 dark:border-slate-800 text-center text-[10px] text-slate-500 transition-colors'>
      Official Proof of Payment • CSJDM, Bulacan, Philippines<br>
      Hotline: +63 935 966 7178 • support@driveandgo.com
    </div>

  </div>

  <script>
    function applyTheme(isDark) {{
      const html = document.documentElement;
      const sun = document.getElementById('iconSun');
      const moon = document.getElementById('iconMoon');
      const text = document.getElementById('themeText');
      if (isDark) {{
        html.classList.add('dark');
        if (sun) sun.classList.remove('hidden');
        if (moon) moon.classList.add('hidden');
        if (text) text.textContent = 'Dark';
      }} else {{
        html.classList.remove('dark');
        if (sun) sun.classList.add('hidden');
        if (moon) moon.classList.remove('hidden');
        if (text) text.textContent = 'Light';
      }}
      try {{ localStorage.setItem('dg_verify_theme', isDark ? 'dark' : 'light'); }} catch(e){{}}
    }}

    function toggleTheme() {{
      const isDark = document.documentElement.classList.contains('dark');
      applyTheme(!isDark);
    }}

    (function() {{
      let saved = null;
      try {{ saved = localStorage.getItem('dg_verify_theme'); }} catch(e){{}}
      if (saved) {{
        applyTheme(saved === 'dark');
      }} else {{
        const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
        applyTheme(prefersDark);
      }}
    }})();
  </script>

</body>
</html>";
    }
}
