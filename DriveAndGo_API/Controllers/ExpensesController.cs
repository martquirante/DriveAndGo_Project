using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace DriveAndGo_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ExpensesController : ControllerBase
{
    private readonly NpgsqlDataSource _ds;
    public ExpensesController(NpgsqlDataSource ds) => _ds = ds;

    // GET /api/expenses?vehicleId={id}
    [HttpGet]
    public async Task<IActionResult> GetExpenses([FromQuery] int? vehicleId)
    {
        try {
            var list = new List<object>();
            await using var conn = await _ds.OpenConnectionAsync();
            var sql = vehicleId.HasValue
                ? "SELECT expense_id, vehicle_id, amount, category, receipt_url, created_at FROM expenses WHERE vehicle_id = @vid ORDER BY created_at DESC"
                : "SELECT expense_id, vehicle_id, amount, category, receipt_url, created_at FROM expenses ORDER BY created_at DESC";
            await using var cmd = new NpgsqlCommand(sql, conn);
            if (vehicleId.HasValue) cmd.Parameters.AddWithValue("@vid", vehicleId.Value);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) {
                list.Add(new {
                    expenseId  = reader.GetInt32(0),
                    vehicleId  = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1),
                    amount     = reader.GetDecimal(2),
                    category   = reader[3].ToString(),
                    receiptUrl = reader.IsDBNull(4) ? null : reader[4].ToString(),
                    createdAt  = reader.GetDateTime(5).ToString("yyyy-MM-dd HH:mm")
                });
            }
            return Ok(list);
        } catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // POST /api/expenses (manual entry, no OCR)
    [HttpPost]
    public async Task<IActionResult> AddExpense([FromBody] ExpenseRequest req)
    {
        try {
            await using var conn = await _ds.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO expenses (vehicle_id, amount, category, receipt_url) VALUES (@vid, @amt, @cat, @url) RETURNING expense_id", conn);
            cmd.Parameters.AddWithValue("@vid", (object?)req.VehicleId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@amt", req.Amount);
            cmd.Parameters.AddWithValue("@cat", req.Category);
            cmd.Parameters.AddWithValue("@url", (object?)req.ReceiptUrl ?? DBNull.Value);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Ok(new { message = "Expense recorded.", expenseId = id });
        } catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // POST /api/expenses/ocr  (simulated OCR - parses amount from text)
    [HttpPost("ocr")]
    public async Task<IActionResult> OcrExpense([FromForm] int vehicleId, IFormFile? receiptImage)
    {
        try {
            // Simulate OCR: In production, use Tesseract.NET or Google Vision API
            // For now, we parse a filename pattern or return a smart mock
            string detectedText = "PETRON FUEL STATION - TOTAL AMOUNT PHP 1500.00";
            decimal amount = 1500.00m;
            string category = "fuel";

            if (receiptImage != null) {
                // Try to extract amount from filename as a demo
                var name = receiptImage.FileName.ToLower();
                if (name.Contains("toll")) { category = "toll"; detectedText = "TOLL GATE - AMOUNT PHP 350.00"; amount = 350m; }
                else if (name.Contains("repair") || name.Contains("maint")) { category = "maintenance"; detectedText = "AUTO REPAIR - TOTAL PHP 3500.00"; amount = 3500m; }
                else if (name.Contains("park")) { category = "parking"; detectedText = "PARKING FEE - PHP 120.00"; amount = 120m; }
            }

            // Save to expenses table
            await using var conn = await _ds.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO expenses (vehicle_id, amount, category, receipt_url) VALUES (@vid, @amt, @cat, NULL) RETURNING expense_id", conn);
            cmd.Parameters.AddWithValue("@vid", vehicleId);
            cmd.Parameters.AddWithValue("@amt", amount);
            cmd.Parameters.AddWithValue("@cat", category);
            var expenseId = (int)(await cmd.ExecuteScalarAsync())!;

            return Ok(new {
                message      = "OCR parsing completed.",
                expenseId,
                amount,
                category,
                detectedText
            });
        } catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // POST /api/expenses/scan-receipt
    [HttpPost("scan-receipt")]
    public async Task<IActionResult> ScanReceipt([FromForm] IFormFile? receiptFile, [FromForm] int? vehicleId)
    {
        try
        {
            if (receiptFile == null)
            {
                return BadRequest(new { Message = "No receipt image file uploaded." });
            }

            // Simulated AI OCR Engine parsing binary stream
            string fileName = receiptFile.FileName.ToLower();
            string merchantName = "Shell Select Petron Station";
            DateTime transactionDate = DateTime.Now.AddDays(-1);
            decimal totalAmount = 1850.00m;
            string category = "Fuel";

            // Set dynamic metadata based on file naming simulation
            if (fileName.Contains("toll") || fileName.Contains("highway"))
            {
                merchantName = "NLEX Tollways Corp";
                category = "Tolls";
                totalAmount = 450.00m;
                if (fileName.Contains("high") || fileName.Contains("fraud"))
                {
                    totalAmount = 2500.00m; // exceeds ₱1500 Toll gate threshold
                }
            }
            else if (fileName.Contains("repair") || fileName.Contains("wash") || fileName.Contains("maint"))
            {
                merchantName = "Auto Clean Premium Car Wash";
                category = "Maintenance";
                totalAmount = 1200.00m;
                if (fileName.Contains("exceed") || fileName.Contains("expensive") || fileName.Contains("wash"))
                {
                    totalAmount = 2800.00m; // exceeds ₱2000 Car Wash threshold
                }
            }
            else if (fileName.Contains("fuel") || fileName.Contains("gas"))
            {
                merchantName = "Caltex Petrol Hub";
                category = "Fuel";
                totalAmount = 3500.00m;
                if (fileName.Contains("high") || fileName.Contains("fraud"))
                {
                    totalAmount = 9500.00m; // exceeds ₱8000 Fuel threshold
                }
            }

            // Programmatic auditing threshold check
            string status = "Approved";
            if (category == "Tolls" && totalAmount > 1500.00m)
            {
                status = "Requires Manual Review / Flagged";
            }
            else if (category == "Maintenance" && totalAmount > 2000.00m)
            {
                status = "Requires Manual Review / Flagged";
            }
            else if (category == "Fuel" && totalAmount > 8000.00m)
            {
                status = "Requires Manual Review / Flagged";
            }

            // Insert into the database expenses table
            await using var conn = await _ds.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO expenses (vehicle_id, amount, category, receipt_url, status) VALUES (@vid, @amt, @cat, NULL, @status) RETURNING expense_id", conn);
            cmd.Parameters.AddWithValue("@vid", vehicleId.HasValue ? (object)vehicleId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@amt", totalAmount);
            cmd.Parameters.AddWithValue("@cat", category.ToLower());
            cmd.Parameters.AddWithValue("@status", status);
            
            var expenseId = (int)(await cmd.ExecuteScalarAsync())!;

            return Ok(new
            {
                success = true,
                message = status == "Approved" ? "Receipt scanned successfully." : "Receipt flagged for audit review.",
                expenseId = expenseId,
                parsedData = new
                {
                    merchantName,
                    transactionDate = transactionDate.ToString("yyyy-MM-dd"),
                    totalAmount,
                    category,
                    status
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "OCR parsing failed: " + ex.Message });
        }
    }
}

public class ExpenseRequest
{
    public int? VehicleId { get; set; }
    public decimal Amount { get; set; }
    public string Category { get; set; } = "other";
    public string? ReceiptUrl { get; set; }
}
