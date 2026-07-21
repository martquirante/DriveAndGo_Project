using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using System.Text.Json;
using DriveAndGo_API.Hubs;
using DriveAndGo_API.Helpers;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GeoFenceController : ControllerBase
    {
        private readonly NpgsqlDataSource _ds;
        private readonly IHubContext<AdminHub> _hubContext;

        public GeoFenceController(NpgsqlDataSource ds, IHubContext<AdminHub> hubContext)
        {
            _ds = ds;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetGeoFences()
        {
            try
            {
                var list = new List<object>();
                await using var conn = await _ds.OpenConnectionAsync();
                await using var cmd = new NpgsqlCommand("SELECT fence_id, name, type, geometry_data FROM geofences", conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new
                    {
                        fenceId = reader.GetInt32(0),
                        name = reader.GetString(1),
                        type = reader.GetString(2),
                        geometryData = JsonDocument.Parse(reader.GetString(3))
                    });
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Error fetching geo-fences: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateGeoFence([FromBody] dynamic payload)
        {
            try
            {
                var element = (JsonElement)payload;
                string name = element.GetProperty("name").GetString()!;
                string type = element.GetProperty("type").GetString()!; // "polygon" or "circle"
                string geometry = element.GetProperty("geometryData").ToString()!;

                await using var conn = await _ds.OpenConnectionAsync();
                await using var cmd = new NpgsqlCommand(
                    "INSERT INTO geofences (name, type, geometry_data) VALUES (@name, @type, @geometry) RETURNING fence_id", conn);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@type", type);
                cmd.Parameters.AddWithValue("@geometry", geometry);

                int id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return Ok(new { Message = "Geo-fence registered successfully.", FenceId = id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Error creating geo-fence: " + ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGeoFence(int id)
        {
            try
            {
                await using var conn = await _ds.OpenConnectionAsync();
                await using var cmd = new NpgsqlCommand("DELETE FROM geofences WHERE fence_id = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                int affected = await cmd.ExecuteNonQueryAsync();
                if (affected == 0) return NotFound(new { Message = "Fence not found." });
                return Ok(new { Message = "Geo-fence deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Error deleting geo-fence: " + ex.Message });
            }
        }

        // POST /api/geofence/validate-breach
        [HttpPost("validate-breach")]
        public async Task<IActionResult> ValidateBreach([FromBody] dynamic payload)
        {
            try
            {
                var element = (JsonElement)payload;
                int vehicleId = element.GetProperty("vehicleId").GetInt32();
                double lat = element.GetProperty("latitude").GetDouble();
                double lng = element.GetProperty("longitude").GetDouble();
                string vehicleName = element.TryGetProperty("vehicleName", out var vn) ? vn.GetString()! : $"Vehicle #{vehicleId}";

                var point = new GeoPoint(lat, lng);
                bool isBreached = false;
                string breachedFenceName = "";

                await using var conn = await _ds.OpenConnectionAsync();
                await using var cmd = new NpgsqlCommand("SELECT name, type, geometry_data FROM geofences", conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string name = reader.GetString(0);
                    string type = reader.GetString(1);
                    string geomJson = reader.GetString(2);
                    var geom = JsonDocument.Parse(geomJson).RootElement;

                    if (type == "circle")
                    {
                        double cLat = geom.GetProperty("center").GetProperty("lat").GetDouble();
                        double cLng = geom.GetProperty("center").GetProperty("lng").GetDouble();
                        double radiusKm = geom.GetProperty("radius").GetDouble() / 1000.0; // convert meters to KM

                        if (!GeoFenceHelper.IsPointInCircle(point, new GeoPoint(cLat, cLng), radiusKm))
                        {
                            isBreached = true;
                            breachedFenceName = name;
                            break;
                        }
                    }
                    else if (type == "polygon")
                    {
                        var coords = new List<GeoPoint>();
                        foreach (var pt in geom.GetProperty("coordinates").EnumerateArray())
                        {
                            coords.Add(new GeoPoint(pt.GetProperty("lat").GetDouble(), pt.GetProperty("lng").GetDouble()));
                        }

                        if (!GeoFenceHelper.IsPointInPolygon(point, coords))
                        {
                            isBreached = true;
                            breachedFenceName = name;
                            break;
                        }
                    }
                }
                await reader.CloseAsync();

                if (isBreached)
                {
                    // Broadcast critical breach notification to all dashboards via SignalR Hub
                    await _hubContext.Clients.All.SendAsync("ReceiveNotification", new
                    {
                        notifId = new Random().Next(10000, 99999),
                        userId = 1, // Admin User
                        title = "🚨 GEOFENCE BREACH ALERT",
                        body = $"CRITICAL: {vehicleName} has exited the safe boundary [{breachedFenceName}].",
                        type = "geofence-breach",
                        isRead = false,
                        sentAt = DateTime.UtcNow
                    });
                }

                return Ok(new { isInside = !isBreached, breachedFence = breachedFenceName });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Breach validation error: " + ex.Message });
            }
        }
    }
}
