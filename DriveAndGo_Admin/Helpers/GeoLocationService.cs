#nullable disable
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DriveAndGo_Admin.Helpers
{
    /// <summary>
    /// IP-based geolocation (ip-api.com) — free, no API key, sub-4s timeout.
    /// Also exposes raw lat/lon for downstream weather lookups.
    /// </summary>
    public static class GeoLocationService
    {
        private static string _cachedLocation = null;
        private static double? _cachedLat     = null;
        private static double? _cachedLon     = null;

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        // ── Public result record ──────────────────────────────────────────────
        public class GeoResult
        {
            public string LocationLabel { get; init; }  // e.g. "Quezon City, Philippines"
            public double Lat           { get; init; }
            public double Lon           { get; init; }
        }

        // ── Single cached async call ──────────────────────────────────────────
        public static async Task<GeoResult> GetGeoAsync()
        {
            // Return cached result if already fetched this session
            if (_cachedLat.HasValue && _cachedLon.HasValue && !string.IsNullOrEmpty(_cachedLocation))
            {
                return new GeoResult
                {
                    LocationLabel = _cachedLocation,
                    Lat           = _cachedLat.Value,
                    Lon           = _cachedLon.Value
                };
            }

            try
            {
                // ip-api.com: free, no key, returns city + coords
                string json = await _httpClient.GetStringAsync(
                    "http://ip-api.com/json/?fields=status,city,country,lat,lon");

                using var doc  = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("status", out var status) && status.GetString() == "success")
                {
                    string city    = root.TryGetProperty("city",    out var c)   ? c.GetString()   : "";
                    string country = root.TryGetProperty("country", out var cnt) ? cnt.GetString() : "";
                    double lat     = root.TryGetProperty("lat",     out var la)  ? la.GetDouble()  : 14.5995;
                    double lon     = root.TryGetProperty("lon",     out var lo)  ? lo.GetDouble()  : 120.9842;

                    string label = !string.IsNullOrWhiteSpace(city)
                        ? $"{city}, {country}"
                        : country;

                    _cachedLocation = string.IsNullOrWhiteSpace(label) ? "Unknown" : label;
                    _cachedLat      = lat;
                    _cachedLon      = lon;

                    return new GeoResult
                    {
                        LocationLabel = _cachedLocation,
                        Lat           = lat,
                        Lon           = lon
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GeoLocationService] {ex.Message}");
            }

            // Fallback: Manila
            _cachedLocation = "Manila, Philippines";
            _cachedLat      = 14.5995;
            _cachedLon      = 120.9842;

            return new GeoResult
            {
                LocationLabel = _cachedLocation,
                Lat           = _cachedLat.Value,
                Lon           = _cachedLon.Value
            };
        }

        /// <summary>
        /// Legacy compat: returns just the city+country label string.
        /// </summary>
        public static async Task<string> GetDeviceLocationAsync()
            => (await GetGeoAsync()).LocationLabel;
    }
}
