using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Text.Json;

namespace DriveAndGo_API.Controllers
{
    [Route("api/fleet")]
    [ApiController]
    public class OfflineSyncController : ControllerBase
    {
        private readonly NpgsqlDataSource _ds;

        public OfflineSyncController(NpgsqlDataSource ds)
        {
            _ds = ds;
        }

        public class OfflineLogItem
        {
            public string IdempotencyKey { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty; // "location", "expense"
            public JsonElement Payload { get; set; }
            public DateTime Timestamp { get; set; }
        }

        // POST /api/fleet/sync-offline-logs
        [HttpPost("sync-offline-logs")]
        public async Task<IActionResult> SyncOfflineLogs([FromBody] List<OfflineLogItem> logs)
        {
            if (logs == null || logs.Count == 0)
            {
                return BadRequest(new { Message = "Empty sync batch data array." });
            }

            try
            {
                int processedCount = 0;
                int duplicateCount = 0;

                await using var conn = await _ds.OpenConnectionAsync();

                foreach (var log in logs)
                {
                    // 1. Try to record the idempotency key. If key exists, it means the request was already processed
                    bool isDuplicate = false;
                    try
                    {
                        await using (var cmd = new NpgsqlCommand("INSERT INTO idempotent_keys (key_value) VALUES (@key)", conn))
                        {
                            cmd.Parameters.AddWithValue("@key", log.IdempotencyKey);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                    catch (PostgresException ex) when (ex.SqlState == "23505") // Unique violation state
                    {
                        isDuplicate = true;
                        duplicateCount++;
                    }

                    if (isDuplicate)
                    {
                        continue; // Skip this log as it's already recorded in DB
                    }

                    // 2. Process payload based on sync type
                    if (log.Type == "location")
                    {
                        int rentalId = log.Payload.GetProperty("rentalId").GetInt32();
                        int vehicleId = log.Payload.GetProperty("vehicleId").GetInt32();
                        decimal lat = log.Payload.GetProperty("latitude").GetDecimal();
                        decimal lng = log.Payload.GetProperty("longitude").GetDecimal();
                        decimal speed = log.Payload.TryGetProperty("speedKmH", out var sp) ? sp.GetDecimal() : 0m;

                        await using (var cmd = new NpgsqlCommand(@"
                            INSERT INTO location_logs (rental_id, vehicle_id, latitude, longitude, speed_kmh, logged_at)
                            VALUES (@rid, @vid, @lat, @lng, @speed, @time)", conn))
                        {
                            cmd.Parameters.AddWithValue("@rid", rentalId);
                            cmd.Parameters.AddWithValue("@vid", vehicleId);
                            cmd.Parameters.AddWithValue("@lat", lat);
                            cmd.Parameters.AddWithValue("@lng", lng);
                            cmd.Parameters.AddWithValue("@speed", speed);
                            cmd.Parameters.AddWithValue("@time", log.Timestamp);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                    else if (log.Type == "expense")
                    {
                        int vehicleId = log.Payload.GetProperty("vehicleId").GetInt32();
                        decimal amount = log.Payload.GetProperty("amount").GetDecimal();
                        string category = log.Payload.GetProperty("category").GetString()!;

                        await using (var cmd = new NpgsqlCommand(@"
                            INSERT INTO expenses (vehicle_id, amount, category, created_at)
                            VALUES (@vid, @amt, @cat, @time)", conn))
                        {
                            cmd.Parameters.AddWithValue("@vid", vehicleId);
                            cmd.Parameters.AddWithValue("@amt", amount);
                            cmd.Parameters.AddWithValue("@cat", category);
                            cmd.Parameters.AddWithValue("@time", log.Timestamp);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    processedCount++;
                }

                return Ok(new
                {
                    success = true,
                    processed = processedCount,
                    duplicatesSkipped = duplicateCount,
                    message = $"Batch sync processed. {processedCount} logged. {duplicateCount} duplicates skipped."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Error executing batch logs synchronization: " + ex.Message });
            }
        }
    }
}
