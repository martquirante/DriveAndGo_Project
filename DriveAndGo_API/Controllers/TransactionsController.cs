using DriveAndGo_API.Models;
using DriveAndGo_API.Services;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Globalization;

namespace DriveAndGo_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TransactionsController : ControllerBase
{
    private readonly string _connectionString;
    private readonly NotificationWriter _notificationWriter;

    public TransactionsController(IConfiguration configuration, NotificationWriter notificationWriter)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        _notificationWriter = notificationWriter;
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
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var pendingCommand = new MySqlCommand(
                @"SELECT COUNT(*) FROM transactions
                  WHERE rental_id = @rental_id
                    AND LOWER(COALESCE(status, '')) = 'pending'",
                connection);
            pendingCommand.Parameters.AddWithValue("@rental_id", transaction.RentalId);

            if (Convert.ToInt32(pendingCommand.ExecuteScalar()) > 0)
            {
                return Conflict(new { Message = "A pending payment already exists for this rental." });
            }

            using var rentalCommand = new MySqlCommand(
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

            using var insertCommand = new MySqlCommand(
                @"INSERT INTO transactions
                    (rental_id, amount, type, method, proof_url, status, paid_at)
                  VALUES
                    (@rental_id, @amount, @type, @method, @proof_url, 'pending', NOW())",
                connection);
            insertCommand.Parameters.AddWithValue("@rental_id", transaction.RentalId);
            insertCommand.Parameters.AddWithValue("@amount", transaction.Amount);
            insertCommand.Parameters.AddWithValue("@type", normalizedType);
            insertCommand.Parameters.AddWithValue("@method", normalizedMethod);
            insertCommand.Parameters.AddWithValue("@proof_url", normalizedProof);
            insertCommand.ExecuteNonQuery();

            var transactionId = Convert.ToInt32(new MySqlCommand("SELECT LAST_INSERT_ID()", connection).ExecuteScalar(), CultureInfo.InvariantCulture);

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
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var command = new MySqlCommand(
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

            using var updateTransactionCommand = new MySqlCommand(
                "UPDATE transactions SET status = 'confirmed' WHERE transaction_id = @id",
                connection);
            updateTransactionCommand.Parameters.AddWithValue("@id", id);
            updateTransactionCommand.ExecuteNonQuery();

            using var updateRentalCommand = new MySqlCommand(
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
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var command = new MySqlCommand(
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

            using var updateCommand = new MySqlCommand(
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
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            var groupBy = period == "daily"
                ? "DATE(paid_at)"
                : period == "yearly"
                    ? "YEAR(paid_at)"
                    : "DATE_FORMAT(paid_at, '%Y-%m')";

            var label = period == "daily"
                ? "DATE(paid_at)"
                : period == "yearly"
                    ? "YEAR(paid_at)"
                    : "DATE_FORMAT(paid_at, '%Y-%m')";

            using var command = new MySqlCommand(
                $@"SELECT {label} AS period, COUNT(*) AS total_transactions, SUM(amount) AS total_amount
                   FROM transactions
                   WHERE LOWER(COALESCE(status, '')) IN ('confirmed', 'paid', 'verified')
                   GROUP BY {groupBy}
                   ORDER BY period DESC
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

    private List<Transaction> ReadTransactions(int? rentalId = null)
    {
        var transactions = new List<Transaction>();

        using var connection = new MySqlConnection(_connectionString);
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
                CONCAT(v.brand, ' ', v.model) AS vehicle_name
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

        using var command = new MySqlCommand(sql, connection);
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
                RentalId = Convert.ToInt32(reader["rental_id"], CultureInfo.InvariantCulture),
                Amount = Convert.ToDecimal(reader["amount"], CultureInfo.InvariantCulture),
                Type = reader["type"]?.ToString(),
                Method = reader["method"]?.ToString(),
                ProofUrl = reader["proof_url"] == DBNull.Value ? null : reader["proof_url"].ToString(),
                Status = reader["status"]?.ToString(),
                PaidAt = reader["paid_at"] == DBNull.Value ? null : Convert.ToDateTime(reader["paid_at"], CultureInfo.InvariantCulture),
                CustomerName = reader["customer_name"]?.ToString(),
                VehicleName = reader["vehicle_name"]?.ToString()
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
        MySqlConnection connection,
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

        using var command = new MySqlCommand(sql, connection);
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
            "paid" => 0,
            "confirmed" => 1,
            "verified" => 2,
            "pending" => 3,
            "" => 4,
            _ => 5
        };
    }
}
