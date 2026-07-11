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
}

public class ExpenseRequest
{
    public int? VehicleId { get; set; }
    public decimal Amount { get; set; }
    public string Category { get; set; } = "other";
    public string? ReceiptUrl { get; set; }
}
