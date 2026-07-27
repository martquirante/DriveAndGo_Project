#nullable disable
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DriveAndGo_Admin.Helpers
{
    public static class GeoLocationService
    {
        private static string _cachedLocation = null;
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };

        public static async Task<string> GetDeviceLocationAsync()
        {
            if (!string.IsNullOrEmpty(_cachedLocation))
                return _cachedLocation;

            try
            {
                // Free, fast public IP geolocation API endpoint
                var response = await _httpClient.GetStringAsync("http://ip-api.com/json/?fields=city,regionName,country,status");
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                if (root.TryGetProperty("status", out var status) && status.GetString() == "success")
                {
                    string city = root.TryGetProperty("city", out var c) ? c.GetString() : "";
                    string country = root.TryGetProperty("country", out var cnt) ? cnt.GetString() : "";

                    string loc = !string.IsNullOrWhiteSpace(city) ? $"{city}, {country}" : country;
                    if (!string.IsNullOrWhiteSpace(loc))
                    {
                        _cachedLocation = loc;
                        return loc;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[GeoLocationService] Error fetching IP location: " + ex.Message);
            }

            _cachedLocation = "Manila, Philippines";
            return _cachedLocation;
        }
    }
}
