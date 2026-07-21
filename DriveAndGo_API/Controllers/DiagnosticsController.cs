using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Diagnostics;
using System.Threading;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiagnosticsController : ControllerBase
    {
        private readonly NpgsqlDataSource _ds;

        public DiagnosticsController(NpgsqlDataSource ds)
        {
            _ds = ds;
        }

        // GET /api/diagnostics/telemetry
        [HttpGet("telemetry")]
        public async Task<IActionResult> GetTelemetry()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                bool dbHealthy = false;

                // Test database connectivity and record response latency
                try
                {
                    await using var conn = await _ds.OpenConnectionAsync();
                    await using var cmd = new NpgsqlCommand("SELECT 1", conn);
                    await cmd.ExecuteScalarAsync();
                    dbHealthy = true;
                }
                catch
                {
                    dbHealthy = false;
                }
                stopwatch.Stop();
                long dbLatencyMs = stopwatch.ElapsedMilliseconds;

                // Retrieve live thread counts & memory usage parameters
                int activeThreads = Process.GetCurrentProcess().Threads.Count;
                long allocatedMemoryBytes = GC.GetTotalMemory(forceFullCollection: false);
                double allocatedMemoryMb = Math.Round((double)allocatedMemoryBytes / 1024 / 1024, 2);

                // Build telemetry metrics model
                var metrics = new
                {
                    status = dbHealthy ? "Healthy" : "Degraded",
                    dbLatencyMs,
                    activeThreads,
                    allocatedMemoryMb,
                    timestamp = DateTime.UtcNow,
                    systemLoad = new {
                        cpuLoadPercentage = new Random().Next(12, 45), // mock load
                        activeSignalRConnections = new Random().Next(3, 12),
                        openDatabasePools = new Random().Next(1, 4)
                    }
                };

                return Ok(metrics);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to calculate telemetry metrics: " + ex.Message });
            }
        }
    }
}
