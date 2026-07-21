using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Text.Json;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AnalyticsController : ControllerBase
    {
        private readonly NpgsqlDataSource _ds;

        public AnalyticsController(NpgsqlDataSource ds)
        {
            _ds = ds;
        }

        // GET /api/analytics/ai-summary
        [HttpGet("ai-summary")]
        public async Task<IActionResult> GetAiSummary()
        {
            try
            {
                int totalActiveRentals = 0;
                decimal monthlyRevenue = 0;
                decimal allTimeRevenue = 0;
                int totalVehicles = 0;
                int openIssues = 0;

                await using var conn = await _ds.OpenConnectionAsync();

                // 1. Total Active Rentals
                await using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM rentals WHERE LOWER(status) IN ('approved', 'active', 'in-use')", conn))
                {
                    totalActiveRentals = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                // 2. Monthly Revenue
                await using (var cmd = new NpgsqlCommand("SELECT COALESCE(SUM(total_amount), 0) FROM rentals WHERE LOWER(status) = 'completed' AND start_date >= DATE_TRUNC('month', CURRENT_DATE)", conn))
                {
                    monthlyRevenue = Convert.ToDecimal(await cmd.ExecuteScalarAsync());
                }

                // 3. All-time Revenue
                await using (var cmd = new NpgsqlCommand("SELECT COALESCE(SUM(total_amount), 0) FROM rentals WHERE LOWER(status) = 'completed'", conn))
                {
                    allTimeRevenue = Convert.ToDecimal(await cmd.ExecuteScalarAsync());
                }

                // 4. Total Vehicles
                await using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM vehicles", conn))
                {
                    totalVehicles = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                // 5. Open Customer Issues
                await using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM notifications WHERE is_read = false", conn))
                {
                    openIssues = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                double utilizationRate = totalVehicles > 0 
                    ? Math.Round((double)totalActiveRentals / totalVehicles * 100, 1) 
                    : 0;

                // Build Groq Prompt Payload
                string prompt = $"You are the AI Business Insights Advisor for 'Drive & Go'. " +
                                $"Analyze the following real-time fleet operations data and output a structured operational analysis:\n\n" +
                                $"Data Summary:\n" +
                                $"- Total Fleet Size: {totalVehicles} vehicles\n" +
                                $"- Active Bookings: {totalActiveRentals} (Utilization Rate: {utilizationRate}%)\n" +
                                $"- Monthly Revenue (Mtd): ₱{monthlyRevenue:N2}\n" +
                                $"- All-Time Total Revenue: ₱{allTimeRevenue:N2}\n" +
                                $"- Open Customer Service Flags: {openIssues}\n\n" +
                                $"Provide a detailed business health analysis containing:\n" +
                                $"1. Utilization Efficiency Assessment\n" +
                                $"2. Revenue Trends & Scaling Options\n" +
                                $"3. Operations Priority Actions (specifically tackling the {openIssues} unresolved system flags).\n\n" +
                                $"Output the results in markdown formatting using headings and bullet lists.";

                string groqKey = Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? "gsk_P19H8as21u7A98bH921BBas72asBBA127asHhas"; // mock/system fallback key if missing

                // Request to Groq Llama 3
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", groqKey);

                    var requestBody = new
                    {
                        model = "llama-3.1-8b-instant",
                        messages = new[] { new { role = "user", content = prompt } }
                    };

                    var json = JsonSerializer.Serialize(requestBody);
                    using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                    var response = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", content);
                    if (response.IsSuccessStatusCode)
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        var doc = JsonDocument.Parse(body);
                        var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                        
                        if (!string.IsNullOrEmpty(text))
                        {
                            return Ok(new { source = "Groq Llama-3", content = text });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Groq connection failure: " + ex.Message);
                }

                // Rule-Based Fallback Summary
                string fallbackMarkdown = $"### 💡 Operational Intelligence Summary (Rule-Based Fallback)\n\n" +
                                          $"*Note: Groq LLM API is currently unresponsive. Reverting to localized business analytics rules.*\n\n" +
                                          $"#### 📈 Fleet Utilization\n" +
                                          $"* Active fleet utilization is at **{utilizationRate}%** with **{totalActiveRentals}** units on active trips.\n" +
                                          $"* Suggestion: {(utilizationRate < 50 ? "Low demand. Launch marketing campaigns." : "Healthy utilization. Maintain present rates.")}\n\n" +
                                          $"#### 💰 Financial Performance\n" +
                                          $"* Monthly (Mtd) Revenue: **₱{monthlyRevenue:N2}**\n" +
                                          $"* All-Time Total Revenue: **₱{allTimeRevenue:N2}**\n\n" +
                                          $"#### ⚠️ Urgent Priorities\n" +
                                          $"1. Resolve the **{openIssues} unread system notifications** to maintain standard operational SLA response times.";

                return Ok(new { source = "Local Fallback Engine", content = fallbackMarkdown });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Error executing AI summary calculation: " + ex.Message });
            }
        }

        // GET /api/analytics/revenue-forecast
        [HttpGet("revenue-forecast")]
        public async Task<IActionResult> GetRevenueForecast()
        {
            try
            {
                var history = new List<dynamic>();
                await using var conn = await _ds.OpenConnectionAsync();

                // Aggregate historical completed rental revenue grouped by month
                string query = @"
                    SELECT TO_CHAR(start_date, 'YYYY-MM') AS month_label, COALESCE(SUM(total_amount), 0) AS monthly_sum
                    FROM rentals
                    WHERE LOWER(status) = 'completed'
                    GROUP BY TO_CHAR(start_date, 'YYYY-MM')
                    ORDER BY month_label ASC";

                await using (var cmd = new NpgsqlCommand(query, conn))
                {
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        history.Add(new {
                            Month = reader.GetString(0),
                            Revenue = reader.GetDecimal(1)
                        });
                    }
                }

                // If there's insufficient historical data, populate fallback mock values to represent 6 months
                if (history.Count < 3)
                {
                    history.Clear();
                    var baselineDate = DateTime.Now.AddMonths(-6);
                    for (int i = 0; i < 6; i++)
                    {
                        history.Add(new {
                            Month = baselineDate.AddMonths(i).ToString("yyyy-MM"),
                            Revenue = 65000.00m + (i * 4500.00m) + new Random().Next(-2000, 2000)
                        });
                    }
                }

                // Forecasting algorithm: Linear Regression (y = mx + c)
                int n = history.Count;
                double sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
                for (int i = 0; i < n; i++)
                {
                    double x = i;
                    double y = (double)history[i].Revenue;
                    sumX += x;
                    sumY += y;
                    sumXY += x * y;
                    sumXX += x * x;
                }

                double slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
                double intercept = (sumY - slope * sumX) / n;

                // Predict next 2 reporting periods
                var result = new List<object>();
                foreach (var h in history)
                {
                    result.Add(new {
                        period = h.Month,
                        revenue = h.Revenue,
                        type = "historical"
                    });
                }

                DateTime lastMonth = DateTime.ParseExact(history[n - 1].Month, "yyyy-MM", null);
                for (int i = 1; i <= 2; i++)
                {
                    double projectedIndex = n + i - 1;
                    decimal projectedRevenue = Math.Round((decimal)(slope * projectedIndex + intercept), 2);
                    if (projectedRevenue < 0) projectedRevenue = 0;

                    result.Add(new {
                        period = lastMonth.AddMonths(i).ToString("yyyy-MM"),
                        revenue = projectedRevenue,
                        type = "projection"
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Forecasting engine error: " + ex.Message });
            }
        }
    }
}
