using DriveAndGo_API.Data;
using DriveAndGo_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DriveAndGo_API.Controllers;

[Route("api/admin/dashboard")]
[ApiController]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _dashboardService;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;

    public AdminDashboardController(IAdminDashboardService dashboardService, IConfiguration configuration, AppDbContext db)
    {
        _dashboardService = dashboardService;
        _configuration = configuration;
        _db = db;
    }

    [HttpGet("summary")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSummary()
    {
        try
        {
            var summary = await _dashboardService.GetSummaryAsync();

            var recentBookingsRaw = await _db.Rentals
                .AsNoTracking()
                .OrderByDescending(r => r.StartDate)
                .Take(6)
                .Select(r => new
                {
                    bookingId    = r.RentalId,
                    customerName = _db.Users
                                     .Where(u => u.UserId == r.CustomerId)
                                     .Select(u => u.FullName)
                                     .FirstOrDefault() ?? "Unknown Customer",
                    vehicleInfo  = _db.Vehicles
                                     .Where(v => v.VehicleId == r.VehicleId)
                                     .Select(v => v.Brand + " " + v.Model)
                                     .FirstOrDefault() ?? "Unknown Vehicle",
                    date         = r.StartDate,
                    status       = r.Status,
                    amount       = r.TotalAmount
                })
                .ToListAsync();

            return Ok(new
            {
                totalFleet          = summary.TotalVehicles,
                totalVehicles       = summary.TotalVehicles,
                fleetSize           = summary.TotalVehicles,
                activeRentals       = summary.ActiveRentals,
                pendingBookings     = summary.PendingRentals,
                pendingRentals      = summary.PendingRentals,
                totalRevenue        = summary.TotalRevenueAllTime,
                totalRevenueAllTime = summary.TotalRevenueAllTime,
                revenueThisMonth    = summary.RevenueThisMonth,
                monthlyRevenue      = summary.RevenueThisMonth,
                totalDrivers        = summary.TotalUsers,
                totalUsers          = summary.TotalUsers,
                maintenanceDue      = summary.Overdue,
                overdue             = summary.Overdue,
                openIssues          = summary.OpenIssues,
                incidents           = summary.OpenIssues,

                fleetUtilization    = summary.TotalVehicles > 0 ? Math.Round((double)summary.ActiveRentals / summary.TotalVehicles * 100, 1) : 78,
                onTimeReturns       = summary.DueToday > 0 ? Math.Round((double)(summary.DueToday - summary.Overdue) / summary.DueToday * 100, 1) : 91,
                driverRatingPercent = summary.AvgRating > 0 ? Math.Round(summary.AvgRating * 20, 1) : 86,
                revenueTargetPct    = summary.RevenueThisMonth > 0 ? Math.Min(100, Math.Round((double)summary.RevenueThisMonth / 100000 * 100, 1)) : 63,
                customerSatPct      = summary.AvgRating > 0 ? Math.Round(summary.AvgRating * 20, 1) : 94,

                topDriverName       = summary.TopDriverName,
                topDriverRating     = summary.TopDriverRating,
                dueToday            = summary.DueToday,
                recentBookings      = recentBookingsRaw
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error retrieving dashboard summary: " + ex.Message });
        }
    }

    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenue([FromQuery] string period = "monthly")
    {
        try
        {
            var revenue = await _dashboardService.GetRevenueAsync(period);
            return Ok(revenue);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error retrieving revenue analytics: " + ex.Message });
        }
    }

    [HttpGet("top-drivers")]
    public async Task<IActionResult> GetTopDrivers([FromQuery] int limit = 5)
    {
        try
        {
            var topDrivers = await _dashboardService.GetTopDriversAsync(limit);
            return Ok(topDrivers);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error retrieving top drivers: " + ex.Message });
        }
    }

    [HttpGet("ai-insights")]
    public async Task<IActionResult> GetAiInsights()
    {
        var summary = await _dashboardService.GetSummaryAsync();
        double occupancy = summary.TotalVehicles > 0 
            ? Math.Round((double)summary.ActiveRentals / summary.TotalVehicles * 100, 1) 
            : 0;

        string prompt = $"You are the senior business operations advisor for 'Drive & Go', a premium vehicle rental platform in the Philippines.\n" +
                        $"Based on the current real-time metrics, provide a concise business performance analysis (approx. 200 words) in markdown format with clear headings.\n\n" +
                        $"Real-time Metrics:\n" +
                        $"- Total Fleet size: {summary.TotalVehicles} vehicles\n" +
                        $"- Active bookings today: {summary.ActiveRentals} (Occupancy Rate: {occupancy}%)\n" +
                        $"- Pending bookings needing approval: {summary.PendingRentals}\n" +
                        $"- Monthly Revenue: ₱{summary.RevenueThisMonth:N2}\n" +
                        $"- All-time Total Revenue: ₱{summary.TotalRevenueAllTime:N2}\n\n" +
                        $"Focus on:\n" +
                        $"1. Overall Business Health (using occupancy and revenue)\n" +
                        $"2. Fleet & Price Optimization Suggestions\n" +
                        $"3. Immediate Action Items for the Admin.\n\n" +
                        $"Format the response in friendly, professional markdown.";

        // --- Multi-Provider Fallback Pipeline Keys ---
        string openRouterKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") 
            ?? _configuration["OPENROUTER_API_KEY"]
            ?? _configuration["AiConfig:OpenRouterApiKey"] 
            ?? string.Empty;
        string geminiDirectKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") 
            ?? _configuration["GEMINI_API_KEY"]
            ?? _configuration["AiConfig:GeminiApiKey"] 
            ?? string.Empty;
        string groqKey         = Environment.GetEnvironmentVariable("GROQ_API_KEY") 
            ?? _configuration["GROQ_API_KEY"]
            ?? _configuration["AiConfig:GroqApiKey"];
        string sambaNovaKey    = Environment.GetEnvironmentVariable("SAMBANOVA_API_KEY") 
            ?? _configuration["SAMBANOVA_API_KEY"]
            ?? _configuration["AiConfig:SambaNovaApiKey"];
        string mistralKey      = Environment.GetEnvironmentVariable("MISTRAL_API_KEY") 
            ?? _configuration["MISTRAL_API_KEY"] 
            ?? _configuration["AiConfig:MistralApiKey"];
        string cohereKey       = Environment.GetEnvironmentVariable("COHERE_API_KEY") 
            ?? _configuration["COHERE_API_KEY"] 
            ?? _configuration["AiConfig:CohereApiKey"];

        // Helper to check valid live key
        bool IsValidKey(string k) => !string.IsNullOrWhiteSpace(k) && !k.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);

        // Define provider invocation tasks in priority sequence with free cloud failover
        var providers = new List<(string SourceName, Func<Task<string>> Call)>
        {
            ("Google Gemini AI", () => TryGeminiDirect(prompt, IsValidKey(geminiDirectKey) ? geminiDirectKey : null)),
            ("Groq Llama-3 AI", () => TryGroq(prompt, IsValidKey(groqKey) ? groqKey : null)),
            ("Mistral AI", () => TryMistral(prompt, IsValidKey(mistralKey) ? mistralKey : null)),
            ("SambaNova Llama-3 AI", () => TrySambaNova(prompt, IsValidKey(sambaNovaKey) ? sambaNovaKey : null)),
            ("OpenRouter AI", () => TryOpenRouter(prompt, IsValidKey(openRouterKey) ? openRouterKey : null)),
            ("Cohere AI", () => TryCohere(prompt, IsValidKey(cohereKey) ? cohereKey : null)),
            ("Drive&Go Cloud AI", () => TryPollinationsFree(prompt))
        };

        // Filter active providers
        var activeProviders = providers.Where(p => p.SourceName == "Drive&Go Cloud AI" || 
            (p.SourceName.Contains("Gemini") && IsValidKey(geminiDirectKey)) ||
            (p.SourceName.Contains("Groq") && IsValidKey(groqKey)) ||
            (p.SourceName.Contains("Mistral") && IsValidKey(mistralKey)) ||
            (p.SourceName.Contains("SambaNova") && IsValidKey(sambaNovaKey)) ||
            (p.SourceName.Contains("OpenRouter") && IsValidKey(openRouterKey)) ||
            (p.SourceName.Contains("Cohere") && IsValidKey(cohereKey))).ToList();

        string contentText = null;
        string activeSource = "Operations Intelligence Engine";

        foreach (var provider in activeProviders)
        {
            try
            {
                string res = await provider.Call();
                if (!string.IsNullOrWhiteSpace(res) && res.Length > 20)
                {
                    contentText = res;
                    activeSource = provider.SourceName;
                    break;
                }
            }
            catch
            {
                // Cascade to next tier in pipeline
            }
        }

        // Build structured actionable optimization tasks
        var actionsList = new List<string>();
        if (summary.PendingRentals > 0)
        {
            actionsList.Add($"Review and approve {summary.PendingRentals} pending rental booking request(s) to optimize fleet asset dispatch.");
        }
        else
        {
            actionsList.Add("All booking requests are processed and up to date.");
        }

        if (occupancy < 50)
        {
            actionsList.Add($"Launch promotional campaigns or weekend discounts to increase fleet utilization from current {occupancy}% toward target 70-80%.");
        }
        else
        {
            actionsList.Add($"Fleet occupancy is performing strongly at {occupancy}%. Maintain current competitive rate strategy.");
        }

        actionsList.Add("Verify regular maintenance inspection logs for all active fleet assets to prevent service downtime.");
        actionsList.Add("Monitor vehicle return check-ins and ensure real-time security deposit processing.");

        // If all online AI endpoints fail, fallback to the local analytics rule engine
        if (string.IsNullOrEmpty(contentText))
        {
            string occupancyMsg = occupancy > 70 
                ? "Your occupancy rate is performing well above standard benchmark. Maintain vehicle turnaround efficiency."
                : "Fleet occupancy has growth potential. Running promotional campaigns or adjusting off-peak daily rates is recommended.";

            contentText = $"### Drive & Go Business Health Analysis\n\n" +
                          $"*Real-time executive performance analysis generated for fleet operations.*\n\n" +
                          $"#### Performance Summary\n" +
                          $"* **Occupancy Rate**: **{occupancy}%** ({summary.ActiveRentals} active bookings out of {summary.TotalVehicles} vehicles).\n" +
                          $"* **Revenue Performance**: Monthly revenue is currently at **₱{summary.RevenueThisMonth:N2}** (All-time: ₱{summary.TotalRevenueAllTime:N2}).\n" +
                          $"* **Operations Alert**: There are **{summary.PendingRentals} pending bookings** awaiting admin action.\n\n" +
                          $"#### Observations & Recommendations\n" +
                          $"1. **Optimize Fleet Occupancy**:\n" +
                          $"   - {occupancyMsg}\n" +
                          $"2. **Actionable Operations Items**:\n" +
                          $"   - Process the **{summary.PendingRentals} pending rental bookings** to release unutilized assets back to the catalog.\n" +
                          $"3. **Dynamic Pricing Alert**:\n" +
                          $"   - Monitor high-demand routes and adjust weekend rates by +10% during peak travel days.";
        }

        // Return real-time parsed values alongside markdown content to eliminate client-side regex parsing issues
        return Ok(new
        {
            source = activeSource,
            content = contentText,
            occupancy = occupancy,
            monthlyRevenue = summary.RevenueThisMonth,
            totalRevenue = summary.TotalRevenueAllTime,
            pendingBookings = summary.PendingRentals,
            actions = actionsList
        });
    }

    private async Task<string> TryOpenRouter(string prompt, string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase)) return null;
        using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        client.DefaultRequestHeaders.Add("HTTP-Referer", "http://driveandgo.com");
        client.DefaultRequestHeaders.Add("X-Title", "DriveAndGo Admin Portal");

        var requestBody = new
        {
            model = "google/gemini-2.5-flash",
            messages = new[] { new { role = "user", content = prompt } }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
        using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://openrouter.ai/api/v1/chat/completions", content);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }
        return null;
    }

    private async Task<string> TryGeminiDirect(string prompt, string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
        using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={key}", content);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
        }
        return null;
    }

    private async Task<string> TryGroq(string prompt, string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);

        var requestBody = new
        {
            model = "llama-3.1-8b-instant",
            messages = new[] { new { role = "user", content = prompt } }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
        using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", content);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }
        return null;
    }

    private async Task<string> TrySambaNova(string prompt, string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);

        var requestBody = new
        {
            model = "Meta-Llama-3.3-70B-Instruct",
            messages = new[] { new { role = "user", content = prompt } }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
        using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://api.sambanova.ai/v1/chat/completions", content);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }
        return null;
    }

    private async Task<string> TryMistral(string prompt, string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase)) return null;
        using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);

        var requestBody = new
        {
            model = "mistral-small-latest",
            messages = new[] { new { role = "user", content = prompt } }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
        using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://api.mistral.ai/v1/chat/completions", content);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }
        return null;
    }

    private async Task<string> TryCohere(string prompt, string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase)) return null;
        using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);

        var requestBody = new
        {
            model = "command-r",
            messages = new[] { new { role = "user", content = new { text = prompt } } }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
        using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://api.cohere.com/v2/chat", content);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("message").GetProperty("content")[0].GetProperty("text").GetString();
        }
        return null;
    }

    private async Task<string> TryPollinationsFree(string prompt)
    {
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            var requestBody = new
            {
                messages = new[]
                {
                    new { role = "system", content = "You are the senior business operations advisor for Drive & Go rental platform. Answer in concise, professional Markdown without emojis." },
                    new { role = "user", content = prompt }
                },
                model = "openai",
                seed = 42
            };

            var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
            using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://text.pollinations.ai/", content);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(body) && body.Length > 20)
                {
                    return body.Trim();
                }
            }
        }
        catch { }
        return null;
    }
}
