using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using DriveAndGo_API.Hubs;
using DriveAndGo_API.Services;
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace DriveAndGo_API.Controllers
{
    [ApiController]
    [Route("api/road-closures")]
    public class RoadClosuresController : ControllerBase
    {
        private readonly ITrafficIncidentAggregatorService _trafficService;
        private readonly string _connectionString;
        private readonly IHubContext<AdminHub> _hubContext;

        public RoadClosuresController(ITrafficIncidentAggregatorService trafficService, IConfiguration config, IHubContext<AdminHub> hubContext)
        {
            _trafficService = trafficService;
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetRoadClosures([FromQuery] string? category = null)
        {
            var closures = await _trafficService.GetActiveClosuresAsync(category);
            return Ok(new
            {
                success = true,
                count = closures.Count,
                data = closures,
                timestamp = DateTime.UtcNow
            });
        }

        [HttpPost("sync-live")]
        public async Task<IActionResult> SyncLiveIncidents()
        {
            int count = await _trafficService.SyncAllSourcesAsync();
            return Ok(new
            {
                success = true,
                message = $"Synchronized {count} real-time traffic incidents and closures across TomTom, HERE, and AI News Feeds.",
                synced_count = count,
                timestamp = DateTime.UtcNow
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateClosure([FromBody] CreateRoadClosureDto req)
        {
            if (string.IsNullOrWhiteSpace(req.RoadName))
            {
                return BadRequest(new { message = "Road name is required." });
            }

            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO road_closures (road_name, category, severity, latitude, longitude, radius_meters, reroute_advice, provider, source_headline, source_url, is_active)
                    VALUES (@name, @cat, @sev, @lat, @lng, @rad, @reroute, @prov, @hl, @url, true)
                    RETURNING closure_id;", conn);

                cmd.Parameters.AddWithValue("name", req.RoadName);
                cmd.Parameters.AddWithValue("cat", req.Category ?? "roadworks");
                cmd.Parameters.AddWithValue("sev", req.Severity ?? "closed");
                cmd.Parameters.AddWithValue("lat", req.Latitude);
                cmd.Parameters.AddWithValue("lng", req.Longitude);
                cmd.Parameters.AddWithValue("rad", req.RadiusMeters <= 0 ? 150 : req.RadiusMeters);
                cmd.Parameters.AddWithValue("reroute", req.RerouteAdvice ?? "");
                cmd.Parameters.AddWithValue("prov", req.Provider ?? "Admin Dispatch");
                cmd.Parameters.AddWithValue("hl", req.SourceHeadline ?? "");
                cmd.Parameters.AddWithValue("url", req.SourceUrl ?? "");

                int newId = (int)(await cmd.ExecuteScalarAsync() ?? 0);

                try { await _hubContext.Clients.All.SendAsync("ReceiveRoadClosuresUpdate"); } catch {}

                return Ok(new
                {
                    success = true,
                    closure_id = newId,
                    message = "Road closure broadcasted successfully to all fleet map instances and mobile app drivers."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error creating road closure: " + ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateClosure(int id, [FromBody] UpdateRoadClosureDto req)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(@"
                    UPDATE road_closures
                    SET road_name = COALESCE(@name, road_name),
                        category = COALESCE(@cat, category),
                        severity = COALESCE(@sev, severity),
                        reroute_advice = COALESCE(@reroute, reroute_advice),
                        is_active = COALESCE(@active, is_active)
                    WHERE closure_id = @id;", conn);

                cmd.Parameters.AddWithValue("name", (object?)req.RoadName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("cat", (object?)req.Category ?? DBNull.Value);
                cmd.Parameters.AddWithValue("sev", (object?)req.Severity ?? DBNull.Value);
                cmd.Parameters.AddWithValue("reroute", (object?)req.RerouteAdvice ?? DBNull.Value);
                cmd.Parameters.AddWithValue("active", (object?)req.IsActive ?? DBNull.Value);
                cmd.Parameters.AddWithValue("id", id);

                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0) return NotFound(new { message = "Closure not found." });

                try { await _hubContext.Clients.All.SendAsync("ReceiveRoadClosuresUpdate"); } catch {}

                return Ok(new { success = true, message = "Road closure updated." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating closure: " + ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteClosure(int id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand("DELETE FROM road_closures WHERE closure_id = @id;", conn);
                cmd.Parameters.AddWithValue("id", id);
                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0) return NotFound(new { message = "Closure not found." });

                try { await _hubContext.Clients.All.SendAsync("ReceiveRoadClosuresUpdate"); } catch {}

                return Ok(new { success = true, message = "Road closure deleted." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error deleting closure: " + ex.Message });
            }
        }
    }

    public class CreateRoadClosureDto
    {
        public string RoadName { get; set; } = string.Empty;
        public string? Category { get; set; } = "roadworks";
        public string? Severity { get; set; } = "closed";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int RadiusMeters { get; set; } = 150;
        public string? RerouteAdvice { get; set; }
        public string? Provider { get; set; } = "Admin Dispatch";
        public string? SourceHeadline { get; set; }
        public string? SourceUrl { get; set; }
    }

    public class UpdateRoadClosureDto
    {
        public string? RoadName { get; set; }
        public string? Category { get; set; }
        public string? Severity { get; set; }
        public string? RerouteAdvice { get; set; }
        public bool? IsActive { get; set; }
    }
}
