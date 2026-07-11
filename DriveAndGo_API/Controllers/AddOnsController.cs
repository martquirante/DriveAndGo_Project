using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Globalization;

namespace DriveAndGo_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AddOnsController : ControllerBase
{
    private readonly NpgsqlDataSource _ds;

    public AddOnsController(NpgsqlDataSource ds) => _ds = ds;

    // GET /api/addons
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var list = new List<object>();
            await using var conn = await _ds.OpenConnectionAsync();
            await using var cmd  = new NpgsqlCommand(
                "SELECT add_on_id, name, description, daily_rate, flat_rate, is_active FROM add_ons ORDER BY name", conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new {
                    addOnId     = reader.GetInt32(reader.GetOrdinal("add_on_id")),
                    name        = reader["name"].ToString(),
                    description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader["description"].ToString(),
                    dailyRate   = Convert.ToDecimal(reader["daily_rate"], CultureInfo.InvariantCulture),
                    flatRate    = Convert.ToDecimal(reader["flat_rate"],  CultureInfo.InvariantCulture),
                    isActive    = reader.GetBoolean(reader.GetOrdinal("is_active"))
                });
            }
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
    }

    // POST /api/addons
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AddOnRequest req)
    {
        try
        {
            await using var conn = await _ds.OpenConnectionAsync();
            await using var cmd  = new NpgsqlCommand(
                @"INSERT INTO add_ons (name, description, daily_rate, flat_rate, is_active)
                  VALUES (@name, @desc, @daily, @flat, true) RETURNING add_on_id", conn);
            cmd.Parameters.AddWithValue("@name",  req.Name);
            cmd.Parameters.AddWithValue("@desc",  (object?)req.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@daily", req.DailyRate);
            cmd.Parameters.AddWithValue("@flat",  req.FlatRate);
            var newId = (int)(await cmd.ExecuteScalarAsync())!;
            return Ok(new { Message = "Add-on created.", AddOnId = newId });
        }
        catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
    }

    // PUT /api/addons/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] AddOnRequest req)
    {
        try
        {
            await using var conn = await _ds.OpenConnectionAsync();
            await using var cmd  = new NpgsqlCommand(
                "UPDATE add_ons SET name=@name, description=@desc, daily_rate=@daily, flat_rate=@flat WHERE add_on_id=@id", conn);
            cmd.Parameters.AddWithValue("@name",  req.Name);
            cmd.Parameters.AddWithValue("@desc",  (object?)req.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@daily", req.DailyRate);
            cmd.Parameters.AddWithValue("@flat",  req.FlatRate);
            cmd.Parameters.AddWithValue("@id",    id);
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows == 0 ? NotFound() : Ok(new { Message = "Updated." });
        }
        catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
    }

    // DELETE /api/addons/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await using var conn = await _ds.OpenConnectionAsync();
            await using var cmd  = new NpgsqlCommand("DELETE FROM add_ons WHERE add_on_id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows == 0 ? NotFound() : Ok(new { Message = "Deleted." });
        }
        catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
    }
}

public class AddOnRequest
{
    public string  Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DailyRate   { get; set; }
    public decimal FlatRate    { get; set; }
}
