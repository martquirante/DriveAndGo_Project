using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RouteAdvisoryController : ControllerBase
    {
        private readonly NpgsqlDataSource _ds;

        public RouteAdvisoryController(NpgsqlDataSource ds)
        {
            _ds = ds;
        }

        // GET /api/routeadvisory/{rentalId} -> changed to accommodate route
        [HttpGet("{rentalId}")]
        public async Task<IActionResult> GetRouteAdvisory(int rentalId)
        {
            try
            {
                string destination = "";
                await using var conn = await _ds.OpenConnectionAsync();

                await using (var cmd = new NpgsqlCommand("SELECT destination FROM rentals WHERE rental_id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", rentalId);
                    var val = await cmd.ExecuteScalarAsync();
                    destination = val?.ToString() ?? "";
                }

                if (string.IsNullOrEmpty(destination))
                {
                    destination = "Baguio"; // default route advisory simulation
                }

                // Simulate environmental forecasting parsing (e.g. Baguio, Manila, Cebu warnings)
                bool isHighRisk = false;
                string weatherCondition = "Clear Sky";
                string warningMessage = "";

                if (destination.ToLower().Contains("baguio") || destination.ToLower().Contains("mountain"))
                {
                    isHighRisk = true;
                    weatherCondition = "Heavy Rain & Low Visibility";
                    warningMessage = "[⚠️ WEATHER RISK: Heavy Rain in Destination Route] Landslide warning active on Kennon Road.";
                }
                else if (destination.ToLower().Contains("typhoon") || destination.ToLower().Contains("storm"))
                {
                    isHighRisk = true;
                    weatherCondition = "Severe Storm";
                    warningMessage = "[⚠️ WEATHER RISK: Severe Typhoon / Active Storm warning along route.]";
                }

                return Ok(new
                {
                    rentalId = rentalId,
                    destination = destination,
                    weatherCondition = weatherCondition,
                    isHighRisk = isHighRisk,
                    warningMessage = warningMessage,
                    recommendedAction = isHighRisk ? "Postpone non-essential travel or use alternative expressways." : "Proceed with standard precautions."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Error fetching route advisory: " + ex.Message });
            }
        }
    }
}
