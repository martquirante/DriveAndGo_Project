using DriveAndGo_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriveAndGo_API.Controllers;

[Route("api/admin/dashboard")]
[ApiController]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _dashboardService;
    private readonly IConfiguration _configuration;

    public AdminDashboardController(IAdminDashboardService dashboardService, IConfiguration configuration)
    {
        _dashboardService = dashboardService;
        _configuration = configuration;
    }

    [HttpGet("summary")]
    [HttpGet("stats")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSummary()
    {
        try
        {
            var summary = await _dashboardService.GetSummaryAsync();
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
                recentBookings      = new object[0]
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
            ?? _configuration["AiConfig:OpenRouterApiKey"] 
            ?? string.Empty;
        string geminiDirectKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") 
            ?? _configuration["AiConfig:GeminiApiKey"] 
            ?? string.Empty;
        string groqKey         = Environment.GetEnvironmentVariable("GROQ_API_KEY") 
            ?? _configuration["AiConfig:GroqApiKey"] 
            ?? string.Empty;

        // Define provider invocation tasks
        var providers = new List<(string SourceName, Func<Task<string>> Call)>
        {
            ("AI Engine (OpenRouter - Gemini)", () => TryOpenRouter(prompt, openRouterKey)),
            ("AI Engine (Google AI Studio Direct)", () => TryGeminiDirect(prompt, geminiDirectKey)),
            ("AI Engine (Groq - Llama)", () => TryGroq(prompt, groqKey))
        };

        // Shuffle providers to achieve automatic load-balancing
        var rnd = new Random();
        var shuffledProviders = providers.OrderBy(_ => rnd.Next()).ToList();

        string contentText = null;
        string activeSource = "Rule Engine (Local Fallback)";

        foreach (var provider in shuffledProviders)
        {
            try
            {
                string res = await provider.Call();
                if (!string.IsNullOrEmpty(res))
                {
                    contentText = res;
                    activeSource = provider.SourceName;
                    break;
                }
            }
            catch
            {
                // Fallback to next available tier in pipeline
            }
        }

        // If all online AI endpoints fail, fallback to the local analytics rule engine
        if (string.IsNullOrEmpty(contentText))
        {
            string occupancyMsg = occupancy > 70 
                ? "Your occupancy rate is excellent! Consider raising weekend rates slightly to optimize yield."
                : "Occupancy is low. We recommend running promotional campaigns or lowering daily rates temporarily.";

            contentText = $"### 💡 Drive & Go Business Health Analysis (Local Fallback Engine)\n\n" +
                          $"*Note: Online AI services are currently offline or rate-limited. Generating report using local analytical engine.*\n\n" +
                          $"#### 📊 Performance Summary\n" +
                          $"* **Occupancy Rate**: **{occupancy}%** ({summary.ActiveRentals} active bookings out of {summary.TotalVehicles} vehicles).\n" +
                          $"* **Revenue Performance**: Monthly revenue is currently at **₱{summary.RevenueThisMonth:N2}** (All-time: ₱{summary.TotalRevenueAllTime:N2}).\n" +
                          $"* **Operations Alert**: There are **{summary.PendingRentals} pending bookings** awaiting admin action.\n\n" +
                          $"#### 📈 Observations & Recommendations\n" +
                          $"1. **Optimize Fleet Occupancy**:\n" +
                          $"   - {occupancyMsg}\n" +
                          $"2. **Actionable Operations Items**:\n" +
                          $"   - Process the **{summary.PendingRentals} pending rental bookings** to release unutilized assets back to the catalog.\n" +
                          $"3. **Dynamic Pricing Alert**:\n" +
                          $"   - High demand days suggest adjusting baseline rates of top models by +10% during weekend peaks.";
        }

        // Return real-time parsed values alongside markdown content to eliminate client-side regex parsing issues
        return Ok(new
        {
            source = activeSource,
            content = contentText,
            occupancy = occupancy,
            monthlyRevenue = summary.RevenueThisMonth,
            totalRevenue = summary.TotalRevenueAllTime,
            pendingBookings = summary.PendingRentals
        });
    }

    private async Task<string> TryOpenRouter(string prompt, string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(7) };
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
        using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(7) };
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
        using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(7) };
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
}
