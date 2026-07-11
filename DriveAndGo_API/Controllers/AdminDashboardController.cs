using DriveAndGo_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriveAndGo_API.Controllers;

[Route("api/admin/dashboard")]
[ApiController]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _dashboardService;

    public AdminDashboardController(IAdminDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        try
        {
            var summary = await _dashboardService.GetSummaryAsync();
            return Ok(summary);
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
        string openRouterKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? string.Empty;
        string geminiDirectKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? string.Empty;
        string groqKey         = Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? string.Empty;

        // ─────────────────────────────────────────────────────────────
        //  TIER 1: OpenRouter (Gemini Flash)
        // ─────────────────────────────────────────────────────────────
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", openRouterKey);
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
                var doc = System.Text.Json.JsonDocument.Parse(body);
                var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    return Ok(new { source = "AI Engine (OpenRouter - Gemini)", content = text });
                }
            }
        }
        catch { }

        // ─────────────────────────────────────────────────────────────
        //  TIER 2: Google AI Studio Direct (Gemini)
        // ─────────────────────────────────────────────────────────────
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
            using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

            // Google Gemini generateContent endpoint
            var response = await client.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={geminiDirectKey}", content);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                var doc = System.Text.Json.JsonDocument.Parse(body);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                if (!string.IsNullOrEmpty(text))
                {
                    return Ok(new { source = "AI Engine (Google AI Studio Direct)", content = text });
                }
            }
        }
        catch { }

        // ─────────────────────────────────────────────────────────────
        //  TIER 3: Groq Cloud (Llama 3.1 8B Instant)
        // ─────────────────────────────────────────────────────────────
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", groqKey);

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
                var doc = System.Text.Json.JsonDocument.Parse(body);
                var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    return Ok(new { source = "AI Engine (Groq - Llama)", content = text });
                }
            }
        }
        catch { }

        // ─────────────────────────────────────────────────────────────
        //  TIER 4: Fallback Local Analytics Rule-Based Engine
        // ─────────────────────────────────────────────────────────────
        string occupancyMsg = occupancy > 70 
            ? "Your occupancy rate is excellent! Consider raising weekend rates slightly to optimize yield."
            : "Occupancy is low. We recommend running promotional campaigns or lowering daily rates temporarily.";

        string fallbackMarkdown = $"### 💡 Drive & Go Business Health Analysis (Local Fallback Engine)\n\n" +
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

        return Ok(new { source = "Rule Engine (Local Fallback)", content = fallbackMarkdown });
    }
}
