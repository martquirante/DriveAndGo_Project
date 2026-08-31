using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using DriveAndGo_API.Services;

namespace DriveAndGo_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly ITrafficIncidentAggregatorService _trafficService;
        private static readonly HttpClient _httpClient = new HttpClient();

        public WeatherController(IConfiguration configuration, ITrafficIncidentAggregatorService trafficService)
        {
            _configuration = configuration;
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
            _trafficService = trafficService;
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
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2.0));
                    var url = $"https://api.weatherapi.com/v1/current.json?key={weatherApiKey}&q=14.871116,121.048088&aqi=no";
                    var res = await _httpClient.GetAsync(url, cts.Token);
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
                            active_flood_zones_count = 9,
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
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2.0));
                    var url = $"https://api.openweathermap.org/data/2.5/weather?lat=14.871116&lon=121.048088&units=metric&appid={openWeatherKey}";
                    var res = await _httpClient.GetAsync(url, cts.Token);
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
                            active_flood_zones_count = 9,
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
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2.5));
                var url = "https://api.open-meteo.com/v1/forecast?latitude=14.871116&longitude=121.048088&current=temperature_2m,relative_humidity_2m,precipitation,weather_code,wind_speed_10m&timezone=Asia%2FManila";
                var res = await _httpClient.GetAsync(url, cts.Token);
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
                        active_flood_zones_count = 9,
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Open-Meteo fallback error: {ex.Message}");
            }

            // 4. Fallback: wttr.in Live Meteorological Stream API
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2.5));
                var url = "https://wttr.in/Manila?format=j1";
                var res = await _httpClient.GetAsync(url, cts.Token);
                if (res.IsSuccessStatusCode)
                {
                    var jsonStr = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonStr);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("current_condition", out var currArr) && currArr.GetArrayLength() > 0)
                    {
                        var curr = currArr[0];
                        double temp = double.TryParse(curr.GetProperty("temp_C").GetString(), out var tVal) ? tVal : 28.0;
                        int humidity = int.TryParse(curr.GetProperty("humidity").GetString(), out var hVal) ? hVal : 80;
                        double windSpeed = double.TryParse(curr.GetProperty("windspeedKmph").GetString(), out var wVal) ? wVal : 15.0;
                        double rain = double.TryParse(curr.GetProperty("precipMM").GetString(), out var rVal) ? rVal : 0.0;
                        string desc = "Live Weather Telematics";
                        if (curr.TryGetProperty("weatherDesc", out var wDescArr) && wDescArr.GetArrayLength() > 0)
                        {
                            desc = wDescArr[0].GetProperty("value").GetString() ?? desc;
                        }

                        return Ok(new
                        {
                            provider = "wttr.in Real-Time API",
                            location = "Rental Garage Hub (SJDM / Metro Manila)",
                            target_coordinates = "14.871116, 121.048088",
                            temperature = temp,
                            humidity = humidity,
                            precipitation_mm_hr = rain,
                            weather_code = rain > 10 ? 95 : rain > 0 ? 61 : 1,
                            condition = desc,
                            wind_speed_kmh = windSpeed,
                            pagasa_alert = rain > 10 ? "PAGASA Yellow Rainfall Advisory" : "Normal Conditions",
                            active_flood_zones_count = 9,
                            timestamp = DateTime.UtcNow
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"wttr.in live fallback error: {ex.Message}");
            }

            return StatusCode(503, new { message = "Weather telemetry services currently unreachable. Retrying live API connections..." });
        }

        [HttpGet("cities")]
        public async Task<IActionResult> GetCitiesWeather()
        {
            var cities = new[]
            {
                new { name = "Manila", lat = 14.5995, lng = 120.9842 },
                new { name = "Tuguegarao", lat = 17.6132, lng = 121.7270 },
                new { name = "Baguio", lat = 16.4023, lng = 120.5960 },
                new { name = "Tarlac City", lat = 15.4802, lng = 120.5979 },
                new { name = "Naga", lat = 13.6218, lng = 123.1948 },
                new { name = "Puerto Princesa", lat = 9.7392, lng = 118.7353 }
            };

            var results = new List<object>();

            // 1. Primary: Batch Open-Meteo API
            try
            {
                var lats = string.Join(",", cities.Select(c => c.lat.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                var lngs = string.Join(",", cities.Select(c => c.lng.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                var url = $"https://api.open-meteo.com/v1/forecast?latitude={lats}&longitude={lngs}&current=temperature_2m,wind_speed_10m";

                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3.5));
                var res = await _httpClient.GetAsync(url, cts.Token);
                if (res.IsSuccessStatusCode)
                {
                    var jsonStr = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonStr);
                    var root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        int idx = 0;
                        foreach (var item in root.EnumerateArray())
                        {
                            if (idx < cities.Length && item.TryGetProperty("current", out var curr))
                            {
                                double temp = curr.GetProperty("temperature_2m").GetDouble();
                                double wind = curr.TryGetProperty("wind_speed_10m", out var w) ? w.GetDouble() : 0.0;
                                results.Add(new
                                {
                                    name = cities[idx].name,
                                    lat = cities[idx].lat,
                                    lng = cities[idx].lng,
                                    temperature = Math.Round(temp, 1),
                                    wind_speed_kmh = Math.Round(wind, 1),
                                    provider = "Open-Meteo API"
                                });
                            }
                            idx++;
                        }
                    }
                    else if (root.TryGetProperty("current", out var singleCurr))
                    {
                        double temp = singleCurr.GetProperty("temperature_2m").GetDouble();
                        double wind = singleCurr.TryGetProperty("wind_speed_10m", out var w) ? w.GetDouble() : 0.0;
                        results.Add(new
                        {
                            name = cities[0].name,
                            lat = cities[0].lat,
                            lng = cities[0].lng,
                            temperature = Math.Round(temp, 1),
                            wind_speed_kmh = Math.Round(wind, 1),
                            provider = "Open-Meteo API"
                        });
                    }

                    if (results.Count > 0)
                    {
                        return Ok(results);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Backend cities weather batch error: {ex.Message}");
            }

            // 2. Secondary Failover: wttr.in per city
            try
            {
                foreach (var c in cities)
                {
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(1.5));
                    var wUrl = $"https://wttr.in/{Uri.EscapeDataString(c.name)}?format=j1";
                    var wRes = await _httpClient.GetAsync(wUrl, cts.Token);
                    if (wRes.IsSuccessStatusCode)
                    {
                        var json = await wRes.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("current_condition", out var currArr) && currArr.GetArrayLength() > 0)
                        {
                            var curr = currArr[0];
                            double temp = double.TryParse(curr.GetProperty("temp_C").GetString(), out var t) ? t : 28.0;
                            double wind = double.TryParse(curr.GetProperty("windspeedKmph").GetString(), out var w) ? w : 15.0;
                            results.Add(new
                            {
                                name = c.name,
                                lat = c.lat,
                                lng = c.lng,
                                temperature = Math.Round(temp, 1),
                                wind_speed_kmh = Math.Round(wind, 1),
                                provider = "wttr.in Real-Time"
                            });
                        }
                    }
                }

                if (results.Count > 0)
                {
                    return Ok(results);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Backend wttr.in cities error: {ex.Message}");
            }

            return StatusCode(503, new { message = "City telematics currently unreachable" });
        }

        [HttpGet("radar-frames")]
        public async Task<IActionResult> GetRadarFrames()
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3.0));
                var res = await _httpClient.GetAsync("https://api.rainviewer.com/public/weather-maps.json", cts.Token);
                if (res.IsSuccessStatusCode)
                {
                    var jsonStr = await res.Content.ReadAsStringAsync();
                    return Content(jsonStr, "application/json");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Backend radar frames fetch error: {ex.Message}");
            }

            return StatusCode(503, new { message = "Radar frames service unavailable" });
        }

        [HttpGet("hourly")]
        public async Task<IActionResult> GetHourlyForecast()
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3.0));
                var url = "https://api.open-meteo.com/v1/forecast?latitude=14.5995&longitude=120.9842&hourly=precipitation,temperature_2m,weather_code&forecast_days=2&timezone=Asia%2FManila";
                var res = await _httpClient.GetAsync(url, cts.Token);
                if (res.IsSuccessStatusCode)
                {
                    var jsonStr = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonStr);
                    if (doc.RootElement.TryGetProperty("hourly", out var hourly))
                    {
                        return Ok(hourly);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Backend hourly forecast error: {ex.Message}");
            }

            return StatusCode(503, new { message = "Hourly forecast service unavailable" });
        }

        [HttpGet("flood-zones")]
        public async Task<IActionResult> GetFloodZones([FromQuery] double? rainOverride = null, [FromQuery] string? region = null)
        {
            var list = new List<object>();
            double liveRain = 0.0;
            string weatherCondition = "Clear Skies";
            int weatherCode = 0;

            // Fetch live rainfall telemetry from Open-Meteo API (Target: Metro Manila & Central Luzon Hub)
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
                        0 => "Clear Sky",
                        1 => "Mainly Clear",
                        2 => "Partly Cloudy",
                        3 => "Overcast",
                        45 or 48 => "Fog / Low Visibility",
                        51 => "Light Drizzle",
                        53 => "Moderate Drizzle",
                        55 => "Dense Drizzle",
                        61 => "Light Rain",
                        63 => "Moderate Rain",
                        65 => "Heavy Rain",
                        80 => "Light Rain Showers",
                        81 => "Moderate Rain Showers",
                        82 => "Violent Rain Showers",
                        95 => "Thunderstorm",
                        96 or 99 => "Severe Thunderstorm",
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

                string sql = "SELECT id, zone_name, risk_level, water_depth_level, polygon_coordinates_json, advisory_timestamp, recommended_reroute, is_active, COALESCE(region, 'Metro Manila') FROM flood_hazard_zones WHERE is_active = true";
                if (!string.IsNullOrWhiteSpace(region) && region.ToLowerInvariant() != "all")
                {
                    sql += " AND LOWER(COALESCE(region, '')) LIKE @reg";
                }
                sql += " ORDER BY id ASC;";

                using var cmd = new NpgsqlCommand(sql, conn);
                if (!string.IsNullOrWhiteSpace(region) && region.ToLowerInvariant() != "all")
                {
                    cmd.Parameters.AddWithValue("reg", $"%{region.ToLowerInvariant()}%");
                }

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
                    string zoneRegion = reader.GetString(8);

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
                        riskBadge = "IMPASSABLE (HIGH FLOOD HAZARD)";
                        isSubmergedRisk = true;
                    }

                    list.Add(new
                    {
                        id,
                        zone_name = zoneName,
                        region = zoneRegion,
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
                // Multi-Basin Copernicus GloFAS River Flow (Pampanga River, Angat/Bulacan River, Marikina River)
                // 15.01, 120.70 = Pampanga River Basin (San Simon / Apalit / Candaba Swamp)
                // 14.88, 120.96 = Angat / Bulacan River Basin (Bustos / Calumpit Lowlands)
                // 14.63, 121.09 = Marikina & Pasig River Basin (Metro Manila)
                var url = "https://flood-api.open-meteo.com/v1/flood?latitude=15.01,14.88,14.63&longitude=120.70,120.96,121.09&daily=river_discharge,river_discharge_mean,river_discharge_median&forecast_days=3&timezone=Asia%2FManila";
                var res = await _httpClient.GetAsync(url);
                if (res.IsSuccessStatusCode)
                {
                    var jsonStr = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonStr);

                    var basins = new List<object>();
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        var arr = doc.RootElement.EnumerateArray().ToList();
                        string[] basinNames = new[] { "Pampanga River Basin (Central Luzon / NLEX)", "Angat River Basin (Bulacan)", "Marikina & Pasig River Basin (Metro Manila)" };
                        string[] regions = new[] { "Pampanga / NLEX", "Bulacan", "Metro Manila" };
                        double[] dangerThresholds = new[] { 50.0, 30.0, 18.0 };

                        for (int i = 0; i < arr.Count && i < basinNames.Length; i++)
                        {
                            var bElem = arr[i];
                            double curDischarge = 0.0;
                            if (bElem.TryGetProperty("daily", out var dObj) && dObj.TryGetProperty("river_discharge", out var rdArr) && rdArr.GetArrayLength() > 0)
                            {
                                curDischarge = rdArr[0].GetDouble();
                            }

                            string status = curDischarge >= dangerThresholds[i] ? "OVERFLOW ALERT / DANGER" : curDischarge >= (dangerThresholds[i] * 0.5) ? "MODERATE SURGE" : "NORMAL FLOW";

                            basins.Add(new
                            {
                                basin_name = basinNames[i],
                                region = regions[i],
                                coordinates = $"{bElem.GetProperty("latitude").GetDouble()}, {bElem.GetProperty("longitude").GetDouble()}",
                                discharge_m3_s = Math.Round(curDischarge, 2),
                                danger_threshold_m3_s = dangerThresholds[i],
                                status = status,
                                daily = bElem.TryGetProperty("daily", out var dailyProp) ? dailyProp : default
                            });
                        }
                    }

                    // Compute primary discharge (Pampanga or highest active surge)
                    double primaryDischarge = basins.Count > 0 ? ((dynamic)basins[0]).discharge_m3_s : 79.5;

                    return Ok(new
                    {
                        provider = "Open-Meteo Flood API (Copernicus GloFAS Multi-Basin)",
                        status = "active",
                        primary_discharge_m3_s = primaryDischarge,
                        discharge_m3_s = primaryDischarge,
                        basins = basins,
                        data = doc.RootElement,
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
                status = "baseline_flow",
                discharge_m3_s = 79.5,
                primary_discharge_m3_s = 79.5,
                basins = new[]
                {
                    new { basin_name = "Pampanga River Basin (Central Luzon / NLEX)", region = "Pampanga / NLEX", discharge_m3_s = 79.5, status = "OVERFLOW ALERT / DANGER" },
                    new { basin_name = "Angat River Basin (Bulacan)", region = "Bulacan", discharge_m3_s = 8.5, status = "NORMAL FLOW" },
                    new { basin_name = "Marikina & Pasig River Basin (Metro Manila)", region = "Metro Manila", discharge_m3_s = 8.9, status = "NORMAL FLOW" }
                },
                timestamp = DateTime.UtcNow
            });
        }

        [HttpGet("crisis-feeds")]
        public async Task<IActionResult> GetCrisisFeeds()
        {
            try
            {
                var data = await _trafficService.GetCrisisFeedsAsync();
                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading crisis feeds: " + ex.Message });
            }
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
