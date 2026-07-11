using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace DriveAndGo_API.Controllers
{
    /// <summary>
    /// Provides a tamper-proof server-side timestamp for the Admin client
    /// to calculate its local clock offset and guard against time manipulation.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TimeSyncController : ControllerBase
    {
        private readonly string _connectionString;

        public TimeSyncController(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")!;
        }

        /// <summary>
        /// GET /api/timesync
        /// Returns the authoritative UTC time sourced directly from the
        /// PostgreSQL database server — independent of the host OS clock.
        /// </summary>
        [HttpGet]
        public IActionResult GetServerTime()
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();

                using var cmd = new NpgsqlCommand("SELECT NOW() AT TIME ZONE 'UTC'", conn);
                var dbTime = (DateTime)cmd.ExecuteScalar()!;

                return Ok(new
                {
                    serverUtcTime  = dbTime.ToString("o"),          // ISO 8601 round-trip format
                    unixTimestamp  = new DateTimeOffset(dbTime, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                    timeZoneId     = "UTC",
                    source         = "postgresql-server-clock"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Time sync failed: " + ex.Message });
            }
        }
    }
}
