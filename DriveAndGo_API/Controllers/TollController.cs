using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace DriveAndGo_API.Controllers;

[Route("api/rentals/{rentalId:int}/toll")]
[ApiController]
public class TollController : ControllerBase
{
    private readonly NpgsqlDataSource _ds;
    public TollController(NpgsqlDataSource ds) => _ds = ds;

    // POST /api/rentals/{rentalId}/toll
    [HttpPost]
    public async Task<IActionResult> LogToll(int rentalId, [FromBody] TollRequest req)
    {
        try {
            await using var conn = await _ds.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO toll_logs (rental_id, toll_amount, location) VALUES (@rid, @amt, @loc) RETURNING toll_log_id", conn);
            cmd.Parameters.AddWithValue("@rid", rentalId);
            cmd.Parameters.AddWithValue("@amt", req.Amount);
            cmd.Parameters.AddWithValue("@loc", (object?)req.Location ?? DBNull.Value);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Ok(new { message = "Toll logged.", tollLogId = id, rentalId, amount = req.Amount });
        } catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // GET /api/rentals/{rentalId}/toll
    [HttpGet]
    public async Task<IActionResult> GetTolls(int rentalId)
    {
        try {
            var list = new List<object>();
            await using var conn = await _ds.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT toll_log_id, toll_amount, location, logged_at FROM toll_logs WHERE rental_id = @rid ORDER BY logged_at DESC", conn);
            cmd.Parameters.AddWithValue("@rid", rentalId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) {
                list.Add(new {
                    tollLogId = reader.GetInt32(0),
                    amount    = reader.GetDecimal(1),
                    location  = reader.IsDBNull(2) ? null : reader[2].ToString(),
                    timestamp = reader.GetDateTime(3).ToString("yyyy-MM-dd HH:mm")
                });
            }
            return Ok(list);
        } catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }
}

public class TollRequest
{
    public decimal Amount   { get; set; }
    public string? Location { get; set; }
}
