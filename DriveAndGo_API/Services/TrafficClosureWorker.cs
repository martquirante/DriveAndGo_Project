using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using DriveAndGo_API.Hubs;

namespace DriveAndGo_API.Services
{
    /// <summary>
    /// Background service that ensures road closures, constructions, and flood advisories
    /// remain synchronized in real-time. It runs on startup and every 15 minutes thereafter,
    /// pushing live updates to all open fleet maps via SignalR without manual refresh.
    /// </summary>
    public class TrafficClosureWorker : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<TrafficClosureWorker> _logger;

        public TrafficClosureWorker(IServiceProvider services, ILogger<TrafficClosureWorker> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TrafficClosureWorker background service started.");

            // Initial brief delay to allow API and DB initialization to complete cleanly
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _services.CreateScope())
                    {
                        var trafficService = scope.ServiceProvider.GetRequiredService<ITrafficIncidentAggregatorService>();
                        int count = await trafficService.SyncAllSourcesAsync();
                        _logger.LogInformation("TrafficClosureWorker synced {Count} live road closures/hazards.", count);
                    }

                    // Periodic refresh every 15 minutes
                    await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "TrafficClosureWorker error during periodic sync cycle.");
                    try
                    {
                        // Wait 30 seconds before retrying on failure
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }
    }
}
