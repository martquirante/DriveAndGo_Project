using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using DriveAndGo_API.Hubs;

namespace DriveAndGo_API.Services
{
    public interface IDbFailoverEngine
    {
        string GetActiveConnectionString();
        bool IsFailoverActive { get; }
        string ActiveProviderName { get; }
        Task TriggerFailoverAsync(string reason);
        Task TriggerFailbackAsync();
    }

    public class DbFailoverEngine : BackgroundService, IDbFailoverEngine
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DbFailoverEngine> _logger;
        private readonly IHubContext<AdminHub> _hubContext;

        private readonly string _azurePrimaryConn;
        private readonly string _supabaseBackupConn;
        private readonly string _localDbConn;
        private readonly bool _useLocalDb;

        private volatile bool _isFailoverActive = false;
        private int _consecutiveFailures = 0;
        private int _consecutiveSuccesses = 0;

        public bool IsFailoverActive => _isFailoverActive;
        public string ActiveProviderName => _useLocalDb 
            ? "Local Docker PostgreSQL" 
            : (_isFailoverActive ? "Supabase Backup (Hot Standby)" : "Azure Primary (Flexible Server)");

        public DbFailoverEngine(
            IConfiguration configuration,
            ILogger<DbFailoverEngine> logger,
            IHubContext<AdminHub> hubContext)
        {
            _configuration = configuration;
            _logger = logger;
            _hubContext = hubContext;

            _useLocalDb = string.Equals(Environment.GetEnvironmentVariable("USE_LOCAL_DB"), "true", StringComparison.OrdinalIgnoreCase);

            _localDbConn = Environment.GetEnvironmentVariable("LOCAL_DB_CONNECTION")
                ?? "Host=localhost;Port=5432;Database=driveandgo_test_db;Username=postgres;Password=postgres_local_password;";

            _azurePrimaryConn = Environment.GetEnvironmentVariable("AZURE_POSTGRES_CONNECTION_STRING")
                ?? Environment.GetEnvironmentVariable("DEFAULT_CONNECTION")
                ?? _configuration.GetConnectionString("DefaultConnection")
                ?? _localDbConn;

            _supabaseBackupConn = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING")
                ?? _localDbConn;

            bool explicitBackup = string.Equals(Environment.GetEnvironmentVariable("USE_BACKUP_DB"), "true", StringComparison.OrdinalIgnoreCase);
            if (explicitBackup)
            {
                _isFailoverActive = true;
            }
        }

        public string GetActiveConnectionString()
        {
            if (_useLocalDb)
            {
                return _localDbConn;
            }

            return _isFailoverActive ? _supabaseBackupConn : _azurePrimaryConn;
        }

        public async Task TriggerFailoverAsync(string reason)
        {
            if (_isFailoverActive) return;

            _isFailoverActive = true;
            _logger.LogWarning("🚨 AUTOMATIC DUAL-CLOUD FAILOVER ACTIVATED: {Reason}. Switched active database to Supabase Backup.", reason);

            try
            {
                await _hubContext.Clients.All.SendAsync("FailoverStatusChanged", new
                {
                    status = "FAILOVER_ACTIVATED",
                    activeProvider = "Supabase Backup",
                    reason = reason,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast failover notification via SignalR.");
            }
        }

        public async Task TriggerFailbackAsync()
        {
            if (!_isFailoverActive) return;

            _isFailoverActive = false;
            _logger.LogInformation("🟢 AUTOMATIC DUAL-CLOUD FAILBACK RESTORED: Azure Primary Database is verified healthy. Switched active database back to Azure Primary.");

            try
            {
                await _hubContext.Clients.All.SendAsync("FailoverStatusChanged", new
                {
                    status = "FAILBACK_RESTORED",
                    activeProvider = "Azure Primary",
                    reason = "Azure Primary Database connection successfully re-established.",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast failback notification via SignalR.");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // If local testing mode, failover monitor remains quiet
            if (_useLocalDb)
            {
                _logger.LogInformation("DbFailoverEngine: Running in Local Dev mode ({LocalDb}). Dual-cloud monitoring idle.", _localDbConn);
                return;
            }

            // In production, probe Azure Primary health every 15 seconds
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

                    bool isAzureReachable = await TestDatabaseConnectionAsync(_azurePrimaryConn, stoppingToken);

                    if (!isAzureReachable)
                    {
                        _consecutiveFailures++;
                        _consecutiveSuccesses = 0;

                        if (_consecutiveFailures >= 2 && !_isFailoverActive)
                        {
                            await TriggerFailoverAsync($"Azure Primary database unreachable for {_consecutiveFailures * 15} seconds.");
                        }
                    }
                    else
                    {
                        _consecutiveSuccesses++;
                        _consecutiveFailures = 0;

                        // Require 2 consecutive successful pings before failing back to prevent flapping
                        if (_consecutiveSuccesses >= 2 && _isFailoverActive)
                        {
                            await TriggerFailbackAsync();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in DbFailoverEngine health probe cycle.");
                }
            }
        }

        private static async Task<bool> TestDatabaseConnectionAsync(string connectionString, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("YOUR_DB_PASSWORD"))
            {
                return false;
            }

            try
            {
                // Timeout probe within 4 seconds
                var csb = new NpgsqlConnectionStringBuilder(connectionString)
                {
                    Timeout = 4,
                    CommandTimeout = 4
                };

                await using var conn = new NpgsqlConnection(csb.ConnectionString);
                await conn.OpenAsync(ct);
                await using var cmd = new NpgsqlCommand("SELECT 1;", conn);
                await cmd.ExecuteScalarAsync(ct);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
