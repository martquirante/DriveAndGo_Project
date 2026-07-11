using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace DriveAndGo_Admin.Helpers
{
    /// <summary>
    /// Phase 2: Anti-Tamper Server Clock Synchronization.
    /// Maintains an offset between the PostgreSQL server UTC clock and
    /// the local Windows machine clock. Even if the user changes the
    /// Windows system time, ServerNow will continue to return the
    /// correct server-calibrated time.
    /// </summary>
    public static class ServerClock
    {
        private static TimeSpan _serverTimeOffset = TimeSpan.Zero;
        private static bool _syncInProgress = false;

        /// <summary>
        /// Returns current time calibrated to the PostgreSQL server clock.
        /// Use instead of DateTime.Now for all business-critical timestamps.
        /// </summary>
        public static DateTime ServerNow => DateTime.UtcNow.Add(_serverTimeOffset).ToLocalTime();

        /// <summary>
        /// True if local clock differs from server by more than 5 minutes
        /// (indicates a possible tampering attempt).
        /// </summary>
        public static bool IsClockTampered => Math.Abs(_serverTimeOffset.TotalMinutes) > 5;

        /// <summary>
        /// Calls GET /api/timesync and recalculates the server time offset.
        /// Call once on login and then every 5 minutes via the clock timer.
        /// </summary>
        public static async Task SyncServerTimeAsync()
        {
            if (_syncInProgress) return;
            _syncInProgress = true;

            try
            {
                var localBefore = DateTime.UtcNow;
                var result = await ApiService.GetAsync("timesync");
                var localAfter = DateTime.UtcNow;

                if (!result.Success) return;

                var doc = JsonDocument.Parse(result.Body);
                if (!doc.RootElement.TryGetProperty("serverUtcTime", out var prop)) return;

                var serverUtc = DateTime.Parse(
                    prop.GetString()!, null,
                    System.Globalization.DateTimeStyles.RoundtripKind);

                // Compensate for network latency by using the midpoint of the round-trip
                var localMid = localBefore + (localAfter - localBefore) / 2;
                _serverTimeOffset = serverUtc - localMid;
            }
            catch
            {
                // Silently retain previous offset on network failure
            }
            finally
            {
                _syncInProgress = false;
            }
        }
    }
}
