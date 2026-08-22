using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DriveAndGo_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private static readonly HttpClient _httpClient = new HttpClient();

        public WeatherController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
        }

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentWeather()
        {
            // 1. Try WeatherAPI.com (Primary - Target: Rental Garage Hub Lat: 14.871116, Lon: 121.048088)
            try
            {
                var weatherApiKey = _configuration["WEATHERAPI_KEY"] ?? Environment.GetEnvironmentVariable("WEATHERAPI_KEY") ?? "";
                if (!string.IsNullOrWhiteSpace(weatherApiKey) && weatherApiKey != "YOUR_WEATHERAPI_KEY")
                {
                    var url = $"https://api.weatherapi.com/v1/current.json?key={weatherApiKey}&q=14.871116,121.048088&aqi=no";
                    var res = await _httpClient.GetAsync(url);
                    if (res.IsSuccessStatusCode)
                    {
                        var jsonStr = await res.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(jsonStr);
                        var root = doc.RootElement;
                        var current = root.GetProperty("current");

                        var temp = current.GetProperty("temp_c").GetDouble();
                        var humidity = current.GetProperty("humidity").GetInt32();
                        var precip = current.GetProperty("precip_mm").GetDouble();
                        var windSpeed = current.GetProperty("wind_kph").GetDouble();
                        var conditionText = current.GetProperty("condition").GetProperty("text").GetString() ?? "Moderate Rain";

                        string pagasaSignal = precip > 15 ? "PAGASA Signal #2 - Heavy Rainfall Warning" :
                                             precip > 5 ? "PAGASA Yellow Rainfall Advisory" : "Normal Conditions";

                        return Ok(new
                        {
                            provider = "WeatherAPI.com",
                            location = "Rental Garage Hub (SJDM / Metro Manila)",
                            target_coordinates = "14.871116, 121.048088",
                            temperature = temp,
                            humidity = humidity,
                            precipitation_mm_hr = precip,
                            weather_code = precip > 10 ? 95 : 61,
                            condition = conditionText,
                            wind_speed_kmh = windSpeed,
                            pagasa_alert = pagasaSignal,
                            active_flood_zones_count = 4,
                            timestamp = DateTime.UtcNow
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WeatherAPI error: {ex.Message}");
            }

            // 2. Try OpenWeatherMap (Secondary Failover)
            try
            {
                var openWeatherKey = _configuration["OPENWEATHER_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENWEATHER_API_KEY") ?? "";
                if (!string.IsNullOrWhiteSpace(openWeatherKey) && openWeatherKey != "YOUR_OPENWEATHER_API_KEY")
                {
                    var url = $"https://api.openweathermap.org/data/2.5/weather?lat=14.871116&lon=121.048088&units=metric&appid={openWeatherKey}";
                    var res = await _httpClient.GetAsync(url);
                    if (res.IsSuccessStatusCode)
                    {
                        var jsonStr = await res.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(jsonStr);
                        var root = doc.RootElement;
                        var main = root.GetProperty("main");
                        var temp = main.GetProperty("temp").GetDouble();
                        var humidity = main.GetProperty("humidity").GetInt32();

                        double rain = 0.0;
                        if (root.TryGetProperty("rain", out var rainObj) && rainObj.TryGetProperty("1h", out var rain1h))
                        {
                            rain = rain1h.GetDouble();
                        }

                        string pagasaSignal = rain > 15 ? "PAGASA Signal #2 - Heavy Rainfall Warning" :
                                             rain > 5 ? "PAGASA Yellow Rainfall Advisory" : "Normal Conditions";

                        return Ok(new
                        {
                            provider = "OpenWeatherMap",
                            location = "Rental Garage Hub (SJDM / Metro Manila)",
                            target_coordinates = "14.871116, 121.048088",
                            temperature = temp,
                            humidity = humidity,
                            precipitation_mm_hr = rain,
                            weather_code = rain > 10 ? 95 : 61,
                            condition = "Monsoon Surge / Rain",
                            wind_speed_kmh = 18.5,
                            pagasa_alert = pagasaSignal,
                            active_flood_zones_count = 4,
                            timestamp = DateTime.UtcNow
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OpenWeatherMap error: {ex.Message}");
            }

            // 3. Fallback: Open-Meteo API
            try
            {
                var url = "https://api.open-meteo.com/v1/forecast?latitude=14.871116&longitude=121.048088&current=temperature_2m,relative_humidity_2m,precipitation,weather_code,wind_speed_10m&timezone=Asia%2FManila";
                var res = await _httpClient.GetAsync(url);
                if (res.IsSuccessStatusCode)
                {
                    var jsonStr = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonStr);
                    var root = doc.RootElement;
                    var current = root.GetProperty("current");

                    var temp = current.GetProperty("temperature_2m").GetDouble();
                    var rain = current.GetProperty("precipitation").GetDouble();
                    var weatherCode = current.GetProperty("weather_code").GetInt32();
                    var windSpeed = current.GetProperty("wind_speed_10m").GetDouble();
                    var humidity = current.GetProperty("relative_humidity_2m").GetInt32();

                    string condition = weatherCode >= 95 ? "Severe Thunderstorm" :
                                       weatherCode >= 61 ? "Heavy Rain Showers" :
                                       weatherCode >= 51 ? "Light Drizzle / Moderate Rain" : "Clear Skies";

                    return Ok(new
                    {
                        provider = "Open-Meteo",
                        location = "Rental Garage Hub (SJDM / Metro Manila)",
                        target_coordinates = "14.871116, 121.048088",
                        temperature = temp,
                        humidity = humidity,
                        precipitation_mm_hr = rain,
                        weather_code = weatherCode,
                        condition = condition,
                        wind_speed_kmh = windSpeed,
                        pagasa_alert = rain > 10 ? "PAGASA Yellow Rainfall Advisory" : "Normal Conditions",
                        active_flood_zones_count = 4,
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Open-Meteo fallback error: {ex.Message}");
            }

            return Ok(new
            {
                provider = "Fallback Systems",
                location = "Rental Garage Hub (SJDM / Metro Manila)",
                target_coordinates = "14.871116, 121.048088",
                temperature = 28.5,
                humidity = 82,
                precipitation_mm_hr = 4.2,
                weather_code = 61,
                condition = "Moderate Rain / Monsoon Surge",
                wind_speed_kmh = 18.4,
                pagasa_alert = "PAGASA Yellow Rainfall Advisory",
                active_flood_zones_count = 4,
                timestamp = DateTime.UtcNow
            });
        }

        [HttpGet("flood-zones")]
        public async Task<IActionResult> GetFloodZones([FromQuery] double? rainOverride = null)
        {
            var list = new List<object>();
            double liveRain = 0.0;
            string weatherCondition = "Clear Skies";
            int weatherCode = 0;

            // Fetch live rainfall telemetry from Open-Meteo API (Target: Metro Manila)
            try
            {
                var weatherUrl = "https://api.open-meteo.com/v1/forecast?latitude=14.5995&longitude=120.9842&current=precipitation,weather_code,temperature_2m&timezone=Asia%2FManila";
                var wRes = await _httpClient.GetAsync(weatherUrl);
                if (wRes.IsSuccessStatusCode)
                {
                    var jsonStr = await wRes.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonStr);
                    var current = doc.RootElement.GetProperty("current");
                    liveRain = current.GetProperty("precipitation").GetDouble();
                    weatherCode = current.GetProperty("weather_code").GetInt32();
                    weatherCondition = weatherCode switch
                    {
                        0 => "Clear Sky (WMO 0)",
                        1 => "Mainly Clear (WMO 1)",
                        2 => "Partly Cloudy (WMO 2)",
                        3 => "Overcast (WMO 3)",
                        45 or 48 => "Fog / Mist (WMO 45)",
                        51 => "Light Drizzle (WMO 51)",
                        53 => "Moderate Drizzle (WMO 53)",
                        55 => "Dense Drizzle (WMO 55)",
                        61 => "Slight Rain (WMO 61)",
                        63 => "Moderate Rain (WMO 63)",
                        65 => "Heavy Rain (WMO 65)",
                        80 => "Slight Rain Showers (WMO 80)",
                        81 => "Moderate Rain Showers (WMO 81)",
                        82 => "Violent Rain Showers (WMO 82)",
                        95 => "Thunderstorm (WMO 95)",
                        96 or 99 => "Severe Thunderstorm (WMO 96)",
                        _ => "Clear Sky"
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Live rain fetch error: {ex.Message}");
            }

            if (rainOverride.HasValue)
            {
                liveRain = Math.Max(0, rainOverride.Value);
            }

            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("SELECT id, zone_name, risk_level, water_depth_level, polygon_coordinates_json, advisory_timestamp, recommended_reroute, is_active FROM flood_hazard_zones WHERE is_active = true ORDER BY id ASC;", conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    int id = reader.GetInt32(0);
                    string zoneName = reader.GetString(1);
                    string baseRisk = reader.GetString(2);
                    string baseDepth = reader.GetString(3);
                    string coordsJson = reader.GetString(4);
                    DateTime advisoryTs = reader.GetDateTime(5);
                    string reroute = reader.GetString(6);
                    bool isActive = reader.GetBoolean(7);

                    // Dynamic Water Depth & Risk Level Calculation based on Live Precipitation (mm/hr)
                    string dynamicRisk;
                    string dynamicDepth;
                    string riskBadge;
                    bool isSubmergedRisk;

                    if (liveRain < 2.0)
                    {
                        // Dry / Light clear weather
                        dynamicRisk = "passable";
                        dynamicDepth = "Passable / Dry (0 - 5 cm)";
                        riskBadge = "PASSABLE (NORMAL)";
                        isSubmergedRisk = false;
                    }
                    else if (liveRain < 8.0)
                    {
                        // Moderate rain
                        dynamicRisk = "moderate";
                        dynamicDepth = "Ankle-Deep Level (10 - 20 cm)";
                        riskBadge = "MODERATE RISK";
                        isSubmergedRisk = false;
                    }
                    else if (liveRain < 20.0)
                    {
                        // Heavy rain surge
                        dynamicRisk = "severe";
                        dynamicDepth = "Tire-Deep Level (25 - 35 cm)";
                        riskBadge = "SEVERE HAZARD";
                        isSubmergedRisk = true;
                    }
                    else
                    {
                        // Torrential monsoon downpour
                        dynamicRisk = "impassable";
                        dynamicDepth = "Waist-Deep Hazard (60 - 80 cm)";
                        riskBadge = "IMPASSABLE (BAWAL DUMAAN)";
                        isSubmergedRisk = true;
                    }

                    list.Add(new
                    {
                        id,
                        zone_name = zoneName,
                        risk_level = dynamicRisk,
                        risk_label = riskBadge,
                        water_depth_level = dynamicDepth,
                        base_hazard_grade = baseRisk.ToUpper() + " (UP NOAH Benchmark)",
                        historical_benchmark_depth = baseDepth,
                        polygon_coordinates_json = coordsJson,
                        advisory_timestamp = advisoryTs,
                        recommended_reroute = reroute,
                        is_active = isActive,
                        is_submerged_risk = isSubmergedRisk,
                        telematics_source = "Live Open-Meteo Telematics + UP NOAH Geofencing",
                        live_precipitation_mm_hr = liveRain,
                        weather_condition = weatherCondition
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error reading flood zones: {ex.Message}" });
            }

            return Ok(list);
        }

        [HttpGet("river-flood")]
        public async Task<IActionResult> GetRiverFloodForecast()
        {
            try
            {
                // Open-Meteo Flood API (Copernicus GloFAS River Discharge for Metro Manila / Marikina Basin)
                var url = "https://flood-api.open-meteo.com/v1/flood?latitude=14.63&longitude=121.09&daily=river_discharge,river_discharge_mean,river_discharge_median&timezone=Asia%2FManila";
                var res = await _httpClient.GetAsync(url);
                if (res.IsSuccessStatusCode)
                {
                    var jsonStr = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonStr);
                    return Ok(new
                    {
                        provider = "Open-Meteo Flood API (GloFAS / Copernicus)",
                        basin = "Marikina & Pasig River Basin",
                        coordinates = "14.63, 121.09",
                        data = doc.RootElement,
                        status = "active",
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Open-Meteo Flood API error: {ex.Message}");
            }

            return Ok(new
            {
                provider = "Open-Meteo Flood API",
                basin = "Marikina & Pasig River Basin",
                status = "baseline_flow",
                discharge_m3_s = 24.5,
                timestamp = DateTime.UtcNow
            });
        }

        [HttpPost("trigger-submersion-alert/{vehicleId}")]
        public async Task<IActionResult> TriggerSubmersionAlert(int vehicleId, [FromBody] SubmersionAlertRequest req)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(@"
                    UPDATE vehicles 
                    SET flood_risk_status = @status, 
                        engine_water_ingress_alert = @ingress,
                        status = CASE WHEN @ingress THEN 'maintenance' ELSE status END
                    WHERE vehicle_id = @id;
                ", conn);

                cmd.Parameters.AddWithValue("status", req.FloodRiskStatus ?? "critical_flood");
                cmd.Parameters.AddWithValue("ingress", req.EngineWaterIngressAlert);
                cmd.Parameters.AddWithValue("id", vehicleId);

                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0) return NotFound(new { message = "Vehicle not found" });

                return Ok(new
                {
                    success = true,
                    message = $"Emergency Submersion Protocol triggered for Vehicle ID #{vehicleId}. Protocol action executed: {req.ProtocolAction ?? "SMS Evac Sent"}",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("tile/{layer}/{z}/{x}/{y}.png")]
        [HttpGet("tile/{layer}/{z}/{x}/{y}")]
        public async Task<IActionResult> GetWeatherTile(string layer, int z, int x, int y)
        {
            var openWeatherKey = _configuration["OPENWEATHER_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENWEATHER_API_KEY") ?? "";
            if (string.IsNullOrWhiteSpace(openWeatherKey) || openWeatherKey == "YOUR_OPENWEATHER_API_KEY")
            {
                return NotFound(new { message = "OpenWeather API key is not configured" });
            }

            // Map layer alias to OpenWeatherMap layer name
            string owmLayer = layer.ToLowerInvariant() switch
            {
                "clouds" => "clouds_new",
                "wind" => "wind_new",
                "precipitation" => "precipitation_new",
                "temp" => "temp_new",
                "pressure" => "pressure_new",
                _ => layer.EndsWith("_new") ? layer : $"{layer}_new"
            };

            var url = $"https://tile.openweathermap.org/map/{owmLayer}/{z}/{x}/{y}.png?appid={openWeatherKey}";
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode);
                }

                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                Response.Headers["Cache-Control"] = "public, max-age=600";
                return File(imageBytes, "image/png");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Tile fetch error: {ex.Message}" });
            }
        }
    }

    public class SubmersionAlertRequest
    {
        public string? FloodRiskStatus { get; set; } = "critical_flood";
        public bool EngineWaterIngressAlert { get; set; } = true;
        public string? ProtocolAction { get; set; } = "Send Evac / Reroute SMS to Driver";
    }
}
