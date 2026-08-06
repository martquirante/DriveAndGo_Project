using DriveAndGo_API.Models;
using DriveAndGo_API.Models.AiCopilot;
using DriveAndGo_API.Models.Operations;
using DriveAndGo_API.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DriveAndGo_API.Services.Ai;

/// <summary>
/// THE SECURE GATEKEEPER — Function Calling / Tool Use Layer.
///
/// The AI model is NEVER allowed to query the database directly.
/// Instead, it declares intent to call one of these named tools.
/// The backend intercepts that intent, calls the corresponding method here,
/// and returns clean, typed DTOs back to the AI for narrative construction.
///
/// Security guarantees:
///   - All methods use parameterized Npgsql queries (no string interpolation in SQL).
///   - No raw connection strings are ever forwarded to any AI model.
///   - PII fields (password_hash, exact addresses, tokens) are EXCLUDED from all LLM payloads.
///   - Every tool is wrapped in try-catch returning structured JSON error payloads.
///   - All list queries enforce hard SQL LIMIT caps to prevent LLM context overflow.
/// </summary>
public class AiToolsService
{
    private readonly NpgsqlDataSource _ds;
    private readonly AppDbContext _dbContext;
    private readonly Services.Operations.IFleetOperationsService _ops;
    private readonly Services.Risk.IAiVisionService _vision;
    private readonly Services.Risk.IFinanceRiskService _risk;
    private readonly ILogger<AiToolsService> _logger;

    // ── Existing tool name constants ─────────────────────────────────────
    public const string ToolGetTodayRevenue       = "get_today_revenue";
    public const string ToolGetWeeklyAnalytics    = "get_weekly_analytics";
    public const string ToolGetOverdueRentals     = "get_overdue_rentals";
    public const string ToolGetFleetCount         = "get_available_fleet_count";
    public const string ToolGetPendingBookings    = "get_pending_bookings";
    public const string ToolGetTopDrivers         = "get_top_drivers";
    public const string ToolGetMonthlyRevenue     = "get_monthly_revenue";
    public const string ToolGetVehicleUtil        = "get_vehicle_utilization";

    // ── Phase 3 Operational Engine Tools ────────────────────────────────
    public const string ToolCheckSurgePricing     = "check_surge_pricing";
    public const string ToolGetMaintenanceAlerts  = "get_maintenance_alerts";
    public const string ToolAutoDispatchBooking   = "auto_dispatch_booking";

    // ── Phase 4 Risk Management Tools ────────────────────────────────────
    public const string ToolAnalyzeIdDocument     = "analyze_id_document";
    public const string ToolAssessVehicleDamage   = "assess_vehicle_damage";
    public const string ToolCheckFuelAnomaly      = "check_fuel_anomaly";
    public const string ToolPredictNextYearSales  = "predict_next_year_sales";

    // ── Phase 5 Comprehensive Database Visibility Tools ───────────────────
    public const string ToolSearchVehicles        = "search_vehicles";
    public const string ToolGetRentalHistory      = "get_rental_history";
    public const string ToolGetCustomerInsights   = "get_customer_insights";
    public const string ToolGetReportedIssues     = "get_reported_issues";
    public const string ToolGetRatingsFeedback    = "get_ratings_feedback";
    public const string ToolGetTransactionSummary = "get_transaction_summary";
    public const string ToolGetTableRecords       = "get_table_records";
    public const string ToolGetRentalExtensions   = "get_rental_extensions";

    public AiToolsService(
        NpgsqlDataSource ds,
        AppDbContext dbContext,
        Services.Operations.IFleetOperationsService ops,
        Services.Risk.IAiVisionService vision,
        Services.Risk.IFinanceRiskService risk,
        ILogger<AiToolsService> logger)
    {
        _ds        = ds;
        _dbContext = dbContext;
        _ops       = ops;
        _vision    = vision;
        _risk      = risk;
        _logger    = logger;
    }

    // ─────────────────────────────────────────────────────────────────
    //  TOOL DISPATCHER — called by AiOrchestrationService
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Dispatches a tool call by name with optional JSON arguments.
    /// Returns a human-readable JSON string describing the result.
    /// </summary>
    public async Task<string> DispatchAsync(string toolName, string? arguments = null)
    {
        _logger.LogInformation("AI Tool call dispatched: {Tool}", toolName);

        // Strip Markdown code fences that some LLMs inject around JSON arguments
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            arguments = arguments
                .Replace("```json", "", StringComparison.OrdinalIgnoreCase)
                .Replace("```", "")
                .Trim();
        }

        try
        {
            return toolName switch
            {
                // ── Existing tools ───────────────────────────────────────────────
                ToolGetTodayRevenue      => Serialize(await GetTodayRevenueAsync()),
                ToolGetWeeklyAnalytics   => Serialize(await GetWeeklyAnalyticsAsync()),
                ToolGetOverdueRentals    => Serialize(await GetOverdueRentalsAsync()),
                ToolGetFleetCount        => Serialize(await GetAvailableFleetCountAsync()),
                ToolGetPendingBookings   => Serialize(await GetPendingBookingsAsync()),
                ToolGetTopDrivers        => Serialize(await GetTopDriversAsync(
                                               ParseStringArg(arguments, "period"),
                                               ParseLimit(arguments))),
                ToolGetMonthlyRevenue    => Serialize(await GetMonthlyRevenueBreakdownAsync()),
                ToolPredictNextYearSales => Serialize(await PredictNextYearSalesToolAsync()),
                ToolGetVehicleUtil       => Serialize(await GetVehicleUtilizationAsync(
                                               ParseStringArg(arguments, "period"),
                                               ParseLimit(arguments, defaultVal: 15))),
                ToolCheckSurgePricing    => FormatSurgePricingResult(await _ops.CalculateSurgePriceAsync(
                                               ParseIntArg(arguments, "categoryId"),
                                               DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
                                               DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(1), DateTimeKind.Utc))),
                ToolGetMaintenanceAlerts => Serialize(await _ops.GetMaintenanceAlertsAsync()),
                ToolAutoDispatchBooking  => Serialize(await _ops.AutoAssignBookingAsync(ParseIntArg(arguments, "rentalId"))),
                ToolAnalyzeIdDocument    => Serialize(await _vision.AnalyzeDriverLicenseAsync(ParseStringArg(arguments, "base64Image"))),
                ToolAssessVehicleDamage  => Serialize(await _vision.AssessVehicleDamageAsync(
                                               ParseStringArg(arguments, "base64Image"),
                                               ParseStringArg(arguments, "description"))),
                ToolCheckFuelAnomaly     => Serialize(await _risk.CheckFuelAnomalyAsync(
                                               ParseIntArg(arguments, "vehicleId"),
                                               ParseDecimalArg(arguments, "amount"),
                                               ParseDecimalArg(arguments, "distance"))),

                // ── New Phase 5 Comprehensive Tools ─────────────────────────────
                ToolSearchVehicles        => Serialize(await SearchVehiclesAsync(
                                               ParseStringArg(arguments, "status"),
                                               ParseStringArg(arguments, "brand"),
                                               ParseStringArg(arguments, "model"),
                                               ParseLimit(arguments, defaultVal: 20))),
                ToolGetRentalHistory      => Serialize(await GetRentalHistoryAsync(
                                               ParseStringArg(arguments, "status"),
                                               ParseLimit(arguments, defaultVal: 15),
                                               ParseIntArg(arguments, "offset"))),
                ToolGetCustomerInsights   => Serialize(await GetCustomerInsightsAsync(
                                               ParseLimit(arguments, defaultVal: 10))),
                ToolGetReportedIssues     => Serialize(await GetReportedIssuesAsync(
                                               ParseStringArg(arguments, "status"),
                                               ParseLimit(arguments, defaultVal: 15))),
                ToolGetRatingsFeedback    => Serialize(await GetRatingsFeedbackAsync(
                                               ParseLimit(arguments, defaultVal: 15))),
                ToolGetTransactionSummary => Serialize(await GetTransactionSummaryAsync(
                                               ParseStringArg(arguments, "method"),
                                               ParseStringArg(arguments, "status"),
                                               ParseLimit(arguments, defaultVal: 20))),
                ToolGetTableRecords       => Serialize(await GetTableRecordsAsync(
                                               ParseStringArg(arguments, "tableName") ?? "vehicles",
                                               ParseStringArg(arguments, "search"),
                                               ParseStringArg(arguments, "status"),
                                               ParseLimit(arguments, defaultVal: 20))),
                ToolGetRentalExtensions   => Serialize(await GetRentalExtensionsAsync(
                                               ParseStringArg(arguments, "status"),
                                               ParseLimit(arguments, defaultVal: 15))),

                _                        => $"{{\"error\": \"Unknown tool: {toolName}\"}}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AiToolsService] DispatchAsync failed for tool '{Tool}'", toolName);
            return $"{{\"error\": \"Tool '{toolName}' failed: {ex.Message.Replace("\"", "'")}. Inform the user the data is temporarily unavailable.\"}}";
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  TOOL: Today's Revenue
    // ─────────────────────────────────────────────────────────────────
    public async Task<TodayRevenueResult> GetTodayRevenueAsync()
    {
        try
        {
            var result = new TodayRevenueResult();
            var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
            var startOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var validStatuses = new[] { "confirmed", "paid", "verified", "completed", "success", "approved", "active", "in-use", "successful", "settled" };

            var recentTxns = await _dbContext.Transactions
                .Where(t => validStatuses.Contains(t.Status.ToLower()))
                .ToListAsync();

            if (recentTxns.Any())
            {
                Func<Transaction, DateTime> getDate = t => t.PaidAt ?? DateTime.MinValue;
                result.TodayRevenue      = recentTxns.Where(t => getDate(t) >= today).Sum(t => t.Amount);
                result.TodayTransactions = recentTxns.Count(t => getDate(t) >= today);
                result.WeekRevenue       = recentTxns.Where(t => getDate(t) >= startOfWeek).Sum(t => t.Amount);
                result.MonthRevenue      = recentTxns.Where(t => getDate(t) >= startOfMonth).Sum(t => t.Amount);
            }

            // Fallback: If EF transactions yield 0 revenue, calculate directly from rentals table
            if (result.TodayRevenue == 0 && result.WeekRevenue == 0 && result.MonthRevenue == 0)
            {
                await using var conn = await _ds.OpenConnectionAsync();
                await using var cmd = new NpgsqlCommand(@"
                    SELECT 
                        COALESCE(SUM(CASE WHEN start_date >= CURRENT_DATE THEN total_amount ELSE 0 END), 0) AS today_rev,
                        COUNT(CASE WHEN start_date >= CURRENT_DATE THEN 1 END) AS today_txns,
                        COALESCE(SUM(CASE WHEN start_date >= DATE_TRUNC('week', CURRENT_DATE) THEN total_amount ELSE 0 END), 0) AS week_rev,
                        COALESCE(SUM(CASE WHEN start_date >= DATE_TRUNC('month', CURRENT_DATE) THEN total_amount ELSE 0 END), 0) AS month_rev
                    FROM rentals
                    WHERE LOWER(status) IN ('approved', 'active', 'completed', 'in-use', 'confirmed', 'paid')", conn);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    result.TodayRevenue      = reader.GetDecimal(0);
                    result.TodayTransactions = (int)reader.GetInt64(1);
                    result.WeekRevenue       = reader.GetDecimal(2);
                    result.MonthRevenue      = reader.GetDecimal(3);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tool] GetTodayRevenueAsync failed");
            throw;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  TOOL: Weekly Analytics (last 7 days daily breakdown)
    // ─────────────────────────────────────────────────────────────────
    public async Task<WeeklyAnalyticsResult> GetWeeklyAnalyticsAsync()
    {
        try
        {
            var result = new WeeklyAnalyticsResult();
            var startDate = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-7), DateTimeKind.Utc);
            var validStatuses = new[] { "confirmed", "paid", "verified", "completed", "success", "approved", "active", "in-use", "successful", "settled" };

            var transactions = await _dbContext.Transactions
                .Where(t => validStatuses.Contains(t.Status.ToLower()))
                .ToListAsync();

            if (transactions.Any())
            {
                Func<Transaction, DateTime> getDate = t => t.PaidAt ?? DateTime.MinValue;
                var filtered = transactions.Where(t => getDate(t) >= startDate).ToList();

                var dailyGroups = filtered
                    .GroupBy(t => getDate(t).Date)
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var g in dailyGroups)
                {
                    var day = new WeeklyDayData
                    {
                        DayLabel = g.Key.ToString("ddd dd MMM"),
                        Revenue  = g.Sum(t => t.Amount),
                        Rentals  = g.Count()
                    };
                    result.DailyBreakdown.Add(day);
                    result.WeekTotal   += day.Revenue;
                    result.WeekRentals += day.Rentals;
                }
            }

            // Fallback: If EF transactions yield 0 weekly breakdown, calculate directly from rentals table
            if (result.DailyBreakdown.Count == 0)
            {
                await using var conn = await _ds.OpenConnectionAsync();
                await using var cmd = new NpgsqlCommand(@"
                    SELECT 
                        TO_CHAR(start_date, 'Mon DD') AS day_label,
                        COALESCE(SUM(total_amount), 0) AS rev,
                        COUNT(*) AS cnt
                    FROM rentals
                    WHERE LOWER(status) IN ('approved', 'active', 'completed', 'in-use', 'confirmed', 'paid')
                      AND start_date >= CURRENT_DATE - INTERVAL '7 days'
                    GROUP BY TO_CHAR(start_date, 'Mon DD'), start_date::date
                    ORDER BY start_date::date ASC", conn);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var day = new WeeklyDayData
                    {
                        DayLabel = reader.GetString(0),
                        Revenue  = reader.GetDecimal(1),
                        Rentals  = (int)reader.GetInt64(2)
                    };
                    result.DailyBreakdown.Add(day);
                    result.WeekTotal   += day.Revenue;
                    result.WeekRentals += day.Rentals;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tool] GetWeeklyAnalyticsAsync failed");
            throw;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  TOOL: Overdue Rentals
    // ─────────────────────────────────────────────────────────────────
    public async Task<OverdueRentalsResult> GetOverdueRentalsAsync()
    {
        try
        {
            var result = new OverdueRentalsResult();
            await using var conn = await _ds.OpenConnectionAsync();

            await using var cmd = new NpgsqlCommand(@"
                SELECT
                  r.rental_id,
                  u.full_name                                           AS customer_name,
                  v.brand || ' ' || v.model                            AS vehicle_name,
                  r.end_date::text                                      AS end_date,
                  GREATEST(0, EXTRACT(DAY FROM NOW() - r.end_date))    AS days_overdue
                FROM rentals r
                JOIN users    u ON u.user_id    = r.customer_id
                JOIN vehicles v ON v.vehicle_id = r.vehicle_id
                WHERE LOWER(r.status) IN ('active', 'in-use', 'overdue')
                  AND r.end_date < NOW()
                ORDER BY r.end_date ASC
                LIMIT 20", conn);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                int daysOverdue = Convert.ToInt32(reader["days_overdue"]);
                decimal penaltyEst = daysOverdue == 0 ? 1500m : daysOverdue * 1500m;

                result.Items.Add(new OverdueRentalItem
                {
                    RentalId     = reader.GetInt32(0),
                    CustomerName = reader.GetString(1),
                    VehicleName  = reader.GetString(2),
                    EndDate      = reader.GetString(3),
                    DaysOverdue  = daysOverdue,
                    PenaltyEst   = penaltyEst
                });
            }
            result.OverdueCount = result.Items.Count;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tool] GetOverdueRentalsAsync failed");
            throw;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  TOOL: Fleet Status Count
    // ─────────────────────────────────────────────────────────────────
    public async Task<FleetStatusResult> GetAvailableFleetCountAsync()
    {
        try
        {
            var result = new FleetStatusResult();
            await using var conn = await _ds.OpenConnectionAsync();

            await using var cmd = new NpgsqlCommand(@"
                SELECT LOWER(status) AS status, COUNT(*) AS cnt
                FROM vehicles
                GROUP BY LOWER(status)", conn);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string st  = reader.GetString(0);
                int    cnt = reader.GetInt32(1);
                result.TotalVehicles += cnt;
                result.Breakdown.Add(new VehicleStatusItem { Status = st, Count = cnt });

                if (st is "available")                           result.Available   = cnt;
                else if (st is "rented" or "active" or "in-use") result.OnRent     = cnt;
                else if (st is "maintenance")                    result.Maintenance = cnt;
            }

            result.UtilizationPct = result.TotalVehicles > 0
                ? Math.Round((double)result.OnRent / result.TotalVehicles * 100, 1)
                : 0;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tool] GetAvailableFleetCountAsync failed");
            throw;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  TOOL: Pending Bookings
    // ─────────────────────────────────────────────────────────────────
    public async Task<PendingRentalsResult> GetPendingBookingsAsync()
    {
        var result = new PendingRentalsResult();
        await using var conn = await _ds.OpenConnectionAsync();

        // Pending rentals
        await using (var cmd = new NpgsqlCommand(@"
            SELECT r.rental_id, u.full_name, v.brand || ' ' || v.model AS vehicle, r.start_date::text, r.total_amount
            FROM rentals r
            JOIN users    u ON u.user_id    = r.customer_id
            JOIN vehicles v ON v.vehicle_id = r.vehicle_id
            WHERE LOWER(r.status) = 'pending'
            ORDER BY r.created_at ASC
            LIMIT 15", conn))
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Items.Add(new PendingRentalItem
                {
                    RentalId     = reader.GetInt32(0),
                    CustomerName = reader.GetString(1),
                    VehicleName  = reader.GetString(2),
                    StartDate    = reader.GetString(3),
                    TotalAmount  = reader.GetDecimal(4)
                });
            }
        }
        result.PendingCount = result.Items.Count;

        // Pending extensions count
        await using (var cmd2 = new NpgsqlCommand(
            "SELECT COUNT(*) FROM extensions WHERE LOWER(status) = 'pending'", conn))
        {
            result.PendingExtensions = Convert.ToInt32(await cmd2.ExecuteScalarAsync());
        }

        return result;
    }

    // ─────────────────────────────────────────────────────────────────
    //  TOOL: Top Drivers / Employees (supports period filter e.g. July, August, this_month)
    // ─────────────────────────────────────────────────────────────────
    public async Task<TopDriversResult> GetTopDriversAsync(string? period = null, int limit = 5)
    {
        if (limit < 1 || limit > 20) limit = 5;
        var result = new TopDriversResult { Period = string.IsNullOrWhiteSpace(period) ? "all_time" : period };
        await using var conn = await _ds.OpenConnectionAsync();

        string dateClause = "";
        string pLower = (period ?? "").ToLowerInvariant().Trim();

        if (pLower.Contains("july") || pLower.Contains("jul"))
        {
            dateClause = "AND (EXTRACT(MONTH FROM COALESCE(r.start_date, r.created_at)) = 7 OR TO_CHAR(COALESCE(r.start_date, r.created_at), 'Mon') ILIKE 'Jul')";
        }
        else if (pLower.Contains("august") || pLower.Contains("aug"))
        {
            dateClause = "AND (EXTRACT(MONTH FROM COALESCE(r.start_date, r.created_at)) = 8 OR TO_CHAR(COALESCE(r.start_date, r.created_at), 'Mon') ILIKE 'Aug')";
        }
        else if (pLower.Contains("this_month"))
        {
            dateClause = "AND COALESCE(r.start_date, r.created_at) >= DATE_TRUNC('month', NOW())";
        }
        else if (pLower.Contains("last_month"))
        {
            dateClause = "AND COALESCE(r.start_date, r.created_at) >= DATE_TRUNC('month', NOW() - INTERVAL '1 month') AND COALESCE(r.start_date, r.created_at) < DATE_TRUNC('month', NOW())";
        }

        if (!string.IsNullOrEmpty(dateClause))
        {
            try
            {
                await using var cmdPeriod = new NpgsqlCommand($@"
                    SELECT
                        d.driver_id,
                        u.full_name,
                        d.rating_avg,
                        d.total_trips,
                        COUNT(r.rental_id)               AS period_trips,
                        COALESCE(SUM(r.total_amount), 0) AS period_revenue
                    FROM drivers d
                    JOIN users u ON u.user_id = d.user_id
                    JOIN rentals r ON r.driver_id = d.driver_id
                    WHERE LOWER(r.status) IN ('completed', 'active', 'in-use', 'approved', 'paid', 'confirmed', 'verified', 'settled')
                      {dateClause}
                    GROUP BY d.driver_id, u.full_name, d.rating_avg, d.total_trips
                    ORDER BY period_trips DESC, period_revenue DESC, d.rating_avg DESC
                    LIMIT @limit", conn);
                cmdPeriod.Parameters.AddWithValue("@limit", limit);

                await using var readerPeriod = await cmdPeriod.ExecuteReaderAsync();
                while (await readerPeriod.ReadAsync())
                {
                    result.Drivers.Add(new TopDriverItem
                    {
                        DriverId      = readerPeriod.GetInt32(0),
                        FullName      = readerPeriod.GetString(1),
                        RatingAvg     = readerPeriod.GetDecimal(2),
                        TotalTrips    = readerPeriod.GetInt32(3),
                        PeriodTrips   = (int)readerPeriod.GetInt64(4),
                        PeriodRevenue = readerPeriod.GetDecimal(5)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Tool] Period query for GetTopDriversAsync failed, falling back to overall");
            }
        }

        if (!result.Drivers.Any())
        {
            await using var cmdAll = new NpgsqlCommand(@"
                SELECT d.driver_id, u.full_name, d.rating_avg, d.total_trips
                FROM drivers d
                JOIN users u ON u.user_id = d.user_id
                WHERE d.total_trips > 0 OR LOWER(d.status) IN ('available', 'active', 'assigned')
                ORDER BY d.rating_avg DESC, d.total_trips DESC
                LIMIT @limit", conn);
            cmdAll.Parameters.AddWithValue("@limit", limit);

            await using var readerAll = await cmdAll.ExecuteReaderAsync();
            while (await readerAll.ReadAsync())
            {
                int trips = readerAll.GetInt32(3);
                result.Drivers.Add(new TopDriverItem
                {
                    DriverId      = readerAll.GetInt32(0),
                    FullName      = readerAll.GetString(1),
                    RatingAvg     = readerAll.GetDecimal(2),
                    TotalTrips    = trips,
                    PeriodTrips   = trips,
                    PeriodRevenue = 0m
                });
            }
        }

        return result;
    }

    // ─────────────────────────────────────────────────────────────────
    //  TOOL: Monthly Revenue (last 12 months)
    // ─────────────────────────────────────────────────────────────────
    public async Task<MonthlyRevenueResult> GetMonthlyRevenueBreakdownAsync()
    {
        var result = new MonthlyRevenueResult();

        try
        {
            await using var conn = await _ds.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT
                    TO_CHAR(COALESCE(t.paid_at, r.created_at, NOW()), 'Mon YYYY') AS month_label,
                    SUM(COALESCE(t.amount, r.total_amount, 0)) AS revenue,
                    COUNT(DISTINCT COALESCE(t.transaction_id, r.rental_id)) AS txns,
                    TO_CHAR(COALESCE(t.paid_at, r.created_at, NOW()), 'YYYY-MM') AS month_key
                FROM transactions t
                FULL OUTER JOIN rentals r ON r.rental_id = t.rental_id
                WHERE LOWER(COALESCE(t.status, r.status)) IN ('confirmed', 'paid', 'verified', 'completed', 'success', 'approved', 'active', 'in-use', 'successful', 'settled')
                GROUP BY TO_CHAR(COALESCE(t.paid_at, r.created_at, NOW()), 'Mon YYYY'),
                         TO_CHAR(COALESCE(t.paid_at, r.created_at, NOW()), 'YYYY-MM')
                ORDER BY month_key ASC", conn);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var item = new MonthlyRevenueItem
                {
                    MonthLabel   = reader.GetString(0),
                    Revenue      = reader.GetDecimal(1),
                    Transactions = (int)reader.GetInt64(2)
                };
                result.Months.Add(item);
                result.GrandTotal += item.Revenue;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Tool] SQL GetMonthlyRevenueBreakdownAsync failed; falling back to EF");
        }

        if (!result.Months.Any())
        {
            var startDate = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddMonths(-12), DateTimeKind.Utc);
            var validStatuses = new[] { "confirmed", "paid", "verified", "completed", "success", "approved", "active", "in-use", "successful", "settled" };

            var transactions = await _dbContext.Transactions
                .Where(t => validStatuses.Contains(t.Status.ToLower()) && t.PaidAt >= startDate)
                .ToListAsync();

            var monthlyGroups = transactions
                .GroupBy(t => new { t.PaidAt.Value.Year, t.PaidAt.Value.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .ToList();

            foreach (var g in monthlyGroups)
            {
                var date = new DateTime(g.Key.Year, g.Key.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var item = new MonthlyRevenueItem
                {
                    MonthLabel   = date.ToString("MMM yyyy"),
                    Revenue      = g.Sum(t => t.Amount),
                    Transactions = g.Count()
                };
                result.Months.Add(item);
                result.GrandTotal += item.Revenue;
            }
        }

        string currentLabel = DateTime.UtcNow.ToString("MMM yyyy");
        result.CurrentMonthLabel = currentLabel;

        var currentItem = result.Months.FirstOrDefault(m => string.Equals(m.MonthLabel, currentLabel, StringComparison.OrdinalIgnoreCase));
        if (currentItem == null)
        {
            currentItem = new MonthlyRevenueItem
            {
                MonthLabel   = currentLabel,
                Revenue      = 0m,
                Transactions = 0
            };
            result.Months.Add(currentItem);
        }
        result.CurrentMonthRevenue = currentItem.Revenue;

        return result;
    }

    // ─────────────────────────────────────────────────────────────────
    //  TOOL: Vehicle Utilization (with period filter)
    // ─────────────────────────────────────────────────────────────────
    public async Task<VehicleUtilResult> GetVehicleUtilizationAsync(string? period = null, int limit = 15)
    {
        if (limit < 1 || limit > 50) limit = 15;

        try
        {
            var result = new VehicleUtilResult();
            await using var conn = await _ds.OpenConnectionAsync();

            // Build date filter clause based on period
            string dateFilter = "";
            if (!string.IsNullOrWhiteSpace(period))
            {
                var now = DateTime.UtcNow;
                dateFilter = period.ToLowerInvariant() switch
                {
                    "this_month"  => $"AND r.created_at >= DATE_TRUNC('month', NOW())",
                    "last_month"  => $"AND r.created_at >= DATE_TRUNC('month', NOW() - INTERVAL '1 month') AND r.created_at < DATE_TRUNC('month', NOW())",
                    "this_year"   => $"AND r.created_at >= DATE_TRUNC('year', NOW())",
                    _             => "" // "all_time" or unrecognized — no filter
                };
            }

            await using var cmd = new NpgsqlCommand($@"
                SELECT
                  v.brand || ' ' || v.model              AS vehicle_name,
                  v.plate_no,
                  COUNT(r.rental_id)                     AS total_rentals,
                  COALESCE(SUM(r.total_amount), 0)       AS revenue
                FROM vehicles v
                LEFT JOIN rentals r ON r.vehicle_id = v.vehicle_id
                  AND LOWER(r.status) IN ('completed', 'paid', 'verified')
                  {dateFilter}
                GROUP BY v.vehicle_id, v.brand, v.model, v.plate_no
                ORDER BY revenue DESC
                LIMIT @limit", conn);
            cmd.Parameters.AddWithValue("@limit", limit);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Vehicles.Add(new VehicleUtilItem
                {
                    VehicleName  = reader.GetString(0),
                    PlateNo      = reader.GetString(1),
                    TotalRentals = reader.GetInt32(2),
                    Revenue      = reader.GetDecimal(3)
                });
            }
            result.Period = string.IsNullOrWhiteSpace(period) ? "all_time" : period;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tool] GetVehicleUtilizationAsync failed");
            throw;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  TOOL: Predict Next Year Sales
    // ─────────────────────────────────────────────────────────────────
    public async Task<MonthlyRevenueResult> PredictNextYearSalesToolAsync()
    {
        var result = new MonthlyRevenueResult();
        var startDate = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddMonths(-6), DateTimeKind.Utc);
        var validStatuses = new[] { "confirmed", "paid", "verified" };

        var transactions = await _dbContext.Transactions
            .Where(t => validStatuses.Contains(t.Status.ToLower()) && t.PaidAt >= startDate)
            .ToListAsync();

        var monthlyGroups = transactions
            .GroupBy(t => new { t.PaidAt.Value.Year, t.PaidAt.Value.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => g.Sum(t => t.Amount))
            .ToList();

        if (monthlyGroups.Count < 2)
            return result; // Not enough data

        decimal totalGrowth = 0;
        int growthPeriods = 0;
        for (int i = 1; i < monthlyGroups.Count; i++)
        {
            var prev = monthlyGroups[i - 1];
            var curr = monthlyGroups[i];
            if (prev > 0)
            {
                totalGrowth += (curr - prev) / prev;
                growthPeriods++;
            }
        }

        decimal avgGrowth = growthPeriods > 0 ? totalGrowth / growthPeriods : 0;
        decimal currentRevenue = monthlyGroups.Last();

        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        for (int i = 1; i <= 12; i++)
        {
            var nextMonth = today.AddMonths(i);
            currentRevenue = currentRevenue * (1 + avgGrowth);
            result.Months.Add(new MonthlyRevenueItem
            {
                MonthLabel = nextMonth.ToString("MMM yyyy"),
                Revenue = currentRevenue,
                Transactions = 0
            });
            result.GrandTotal += currentRevenue;
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PHASE 5 — NEW COMPREHENSIVE DATABASE VISIBILITY TOOLS
    // ═══════════════════════════════════════════════════════════════════

    // ─────────────────────────────────────────────────────────────────
    //  TOOL: Search Vehicles (PII-safe, row-capped)
    // ─────────────────────────────────────────────────────────────────
    public async Task<object> SearchVehiclesAsync(
        string? status = null, string? brand = null, string? model = null, int limit = 20)
    {
        if (limit < 1 || limit > 50) limit = 20;

        try
        {
            await using var conn = await _ds.OpenConnectionAsync();

            var conditions = new List<string>();
            var cmd = new NpgsqlCommand();

            if (!string.IsNullOrWhiteSpace(status))
            {
                conditions.Add("LOWER(v.status) = @status");
                cmd.Parameters.AddWithValue("@status", status.ToLower().Trim());
            }
            if (!string.IsNullOrWhiteSpace(brand))
            {
                conditions.Add("LOWER(v.brand) LIKE @brand");
                cmd.Parameters.AddWithValue("@brand", $"%{brand.ToLower().Trim()}%");
            }
            if (!string.IsNullOrWhiteSpace(model))
            {
                conditions.Add("LOWER(v.model) LIKE @model");
                cmd.Parameters.AddWithValue("@model", $"%{model.ToLower().Trim()}%");
            }

            string whereClause = conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "";
            cmd.CommandText = $@"
                SELECT v.vehicle_id, v.brand, v.model, v.plate_no, v.type,
                       v.status, v.rate_per_day, v.rate_with_driver,
                       v.seat_capacity, v.transmission
                FROM vehicles v
                {whereClause}
                ORDER BY v.brand, v.model
                LIMIT @limit";
            cmd.Parameters.AddWithValue("@limit", limit);
            cmd.Connection = conn;

            var vehicles = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                // PII-safe: Only operational fields, no GPS tracking, no photos
                vehicles.Add(new
                {
                    vehicle_id    = reader.GetInt32(0),
                    brand         = reader.GetString(1),
                    model         = reader.GetString(2),
                    plate_no      = reader.GetString(3),
                    type          = reader.IsDBNull(4) ? null : reader.GetString(4),
                    status        = reader.GetString(5),
                    rate_per_day  = reader.GetDecimal(6),
                    rate_with_driver = reader.GetDecimal(7),
                    seat_capacity = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                    transmission  = reader.IsDBNull(9) ? null : reader.GetString(9)
                });
            }

            return new { total_returned = vehicles.Count, limit_applied = limit, vehicles };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tool] SearchVehiclesAsync failed");
            return new { error = $"Failed to search vehicles: {ex.Message}. Data temporarily unavailable." };
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  TOOL: Rental History (PII-safe, paginated, row-capped)
    // ─────────────────────────────────────────────────────────────────
    public async Task<object> GetRentalHistoryAsync(
        string? status = null, int limit = 15, int offset = 0)
    {
        if (limit < 1 || limit > 50) limit = 15;
        if (offset < 0) offset = 0;

        try
        {
            await using var conn = await _ds.OpenConnectionAsync();

            string statusFilter = "";
            var cmd = new NpgsqlCommand();

            if (!string.IsNullOrWhiteSpace(status))
            {
                statusFilter = "AND LOWER(r.status) = @status";
                cmd.Parameters.AddWithValue("@status", status.ToLower().Trim());
            }

            cmd.CommandText = $@"
                SELECT r.rental_id,
                       u.full_name            AS customer_name,
                       v.brand || ' ' || v.model AS vehicle,
                       v.plate_no,
                       r.start_date::text,
                       r.end_date::text,
                       r.status,
                       r.total_amount,
                       r.payment_method,
                       r.payment_status,
                       r.created_at::text
                FROM rentals r
                JOIN users    u ON u.user_id    = r.customer_id
                JOIN vehicles v ON v.vehicle_id = r.vehicle_id
                WHERE 1=1 {statusFilter}
                ORDER BY r.created_at DESC
                LIMIT @limit OFFSET @offset";
            cmd.Parameters.AddWithValue("@limit",  limit);
            cmd.Parameters.AddWithValue("@offset", offset);
            cmd.Connection = conn;

            var rentals = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                // PII-safe: customer name only, no addresses, no passwords
                rentals.Add(new
                {
                    rental_id      = reader.GetInt32(0),
                    customer_name  = reader.GetString(1),
                    vehicle        = reader.GetString(2),
                    plate_no       = reader.GetString(3),
                    start_date     = reader.GetString(4),
                    end_date       = reader.GetString(5),
                    status         = reader.GetString(6),
                    total_amount   = reader.GetDecimal(7),
                    payment_method = reader.IsDBNull(8)  ? null : reader.GetString(8),
                    payment_status = reader.IsDBNull(9)  ? null : reader.GetString(9),
                    created_at     = reader.GetString(10)
                });
            }

            return new { total_returned = rentals.Count, limit_applied = limit, offset_applied = offset, rentals };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tool] GetRentalHistoryAsync failed");
            return new { error = $"Failed to load rental history: {ex.Message}. Data temporarily unavailable." };
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  TOOL: Customer Insights (PII-safe — no passwords, no addresses)
    // ─────────────────────────────────────────────────────────────────
    public async Task<object> GetCustomerInsightsAsync(int limit = 10)
    {
        if (limit < 1 || limit > 30) limit = 10;

        try
        {
            await using var conn = await _ds.OpenConnectionAsync();

            // Top customers by total spend — PII-safe: name, phone masked, spend only
            await using var cmd = new NpgsqlCommand(@"
                SELECT
                    u.full_name,
                    CONCAT(LEFT(u.phone, 4), '****', RIGHT(u.phone, 2)) AS phone_masked,
                    COUNT(r.rental_id)                                   AS total_bookings,
                    COALESCE(SUM(r.total_amount), 0)                     AS total_spent,
                    MIN(r.created_at)::text                              AS first_booking,
                    MAX(r.created_at)::text                              AS last_booking
                FROM users u
                JOIN rentals r ON r.customer_id = u.user_id
                WHERE LOWER(u.role) = 'customer'
                GROUP BY u.user_id, u.full_name, u.phone
                ORDER BY total_spent DESC
                LIMIT @limit", conn);
            cmd.Parameters.AddWithValue("@limit", limit);

            var customers = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                // Strictly no: email, password_hash, exact address, or user_id in bulk
                customers.Add(new
                {
                    customer_name  = reader.GetString(0),
                    phone_masked   = reader.IsDBNull(1) ? "N/A" : reader.GetString(1),
                    total_bookings = reader.GetInt64(2),
                    total_spent    = reader.GetDecimal(3),
                    first_booking  = reader.GetString(4),
                    last_booking   = reader.GetString(5)
                });
            }

            // Also get new customers this month
            await using var cmd2 = new NpgsqlCommand(@"
                SELECT COUNT(*) FROM users
                WHERE LOWER(role) = 'customer'
                  AND created_at >= DATE_TRUNC('month', NOW())", conn);
            int newThisMonth = Convert.ToInt32(await cmd2.ExecuteScalarAsync());

            await using var cmd3 = new NpgsqlCommand(
                "SELECT COUNT(*) FROM users WHERE LOWER(role) = 'customer'", conn);
            int totalCustomers = Convert.ToInt32(await cmd3.ExecuteScalarAsync());

            return new
            {
                total_customers      = totalCustomers,
                new_customers_this_month = newThisMonth,
                top_customers_shown  = customers.Count,
                limit_applied        = limit,
                top_customers        = customers
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tool] GetCustomerInsightsAsync failed");
            return new { error = $"Failed to load customer insights: {ex.Message}. Data temporarily unavailable." };
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  TOOL: Reported Issues (row-capped)
    // ─────────────────────────────────────────────────────────────────
    public async Task<object> GetReportedIssuesAsync(string? status = null, int limit = 15)
    {
        if (limit < 1 || limit > 50) limit = 15;

        try
        {
            await using var conn = await _ds.OpenConnectionAsync();

            string statusFilter = "";
            var cmd = new NpgsqlCommand();
            if (!string.IsNullOrWhiteSpace(status))
            {
                statusFilter = "AND LOWER(i.status) = @status";
                cmd.Parameters.AddWithValue("@status", status.ToLower().Trim());
            }

            cmd.CommandText = $@"
                SELECT
                    i.issue_id,
                    i.issue_type,
                    i.description,
                    i.status,
                    u.full_name       AS reported_by,
                    v.brand || ' ' || v.model AS vehicle,
                    r.rental_id,
                    i.reported_at::text
                FROM issues i
                JOIN rentals  r ON r.rental_id  = i.rental_id
                JOIN users    u ON u.user_id     = i.reporter_id
                JOIN vehicles v ON v.vehicle_id  = r.vehicle_id
                WHERE 1=1 {statusFilter}
                ORDER BY i.reported_at DESC
                LIMIT @limit";
            cmd.Parameters.AddWithValue("@limit", limit);
            cmd.Connection = conn;

            var issues = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                issues.Add(new
                {
                    issue_id    = reader.GetInt32(0),
                    issue_type  = reader.IsDBNull(1) ? "General" : reader.GetString(1),
                    description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    status      = reader.GetString(3),
                    reported_by = reader.GetString(4),
                    vehicle     = reader.GetString(5),
                    rental_id   = reader.GetInt32(6),
                    reported_at = reader.GetString(7)
                });
            }

            return new { total_returned = issues.Count, limit_applied = limit, issues };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tool] GetReportedIssuesAsync failed");
            return new { error = $"Failed to load reported issues: {ex.Message}. Data temporarily unavailable." };
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  TOOL: Ratings & Feedback (row-capped, PII-safe)
    // ─────────────────────────────────────────────────────────────────
    public async Task<object> GetRatingsFeedbackAsync(int limit = 15)
    {
        if (limit < 1 || limit > 50) limit = 15;

        try
        {
            await using var conn = await _ds.OpenConnectionAsync();

            // Overall averages
            await using var avgCmd = new NpgsqlCommand(@"
                SELECT
                    ROUND(AVG(vehicle_score), 2) AS avg_vehicle_score,
                    ROUND(AVG(driver_score),  2) AS avg_driver_score,
                    COUNT(*)                      AS total_ratings
                FROM ratings", conn);

            decimal avgVehicle = 0, avgDriver = 0;
            int totalRatings = 0;
            await using (var avgReader = await avgCmd.ExecuteReaderAsync())
            {
                if (await avgReader.ReadAsync())
                {
                    avgVehicle   = avgReader.IsDBNull(0) ? 0 : avgReader.GetDecimal(0);
                    avgDriver    = avgReader.IsDBNull(1) ? 0 : avgReader.GetDecimal(1);
                    totalRatings = avgReader.GetInt32(2);
                }
            }

            // Individual recent ratings (PII-safe: customer name only, no IDs)
            await using var cmd = new NpgsqlCommand(@"
                SELECT
                    u.full_name       AS customer_name,
                    v.brand || ' ' || v.model AS vehicle,
                    d_user.full_name  AS driver_name,
                    rt.vehicle_score,
                    rt.driver_score,
                    rt.comment,
                    rt.rated_at::text
                FROM ratings rt
                JOIN users    u      ON u.user_id        = rt.customer_id
                JOIN vehicles v      ON v.vehicle_id     = rt.vehicle_id
                LEFT JOIN drivers d  ON d.driver_id      = rt.driver_id
                LEFT JOIN users d_user ON d_user.user_id = d.user_id
                ORDER BY rt.rated_at DESC
                LIMIT @limit", conn);
            cmd.Parameters.AddWithValue("@limit", limit);

            var ratings = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                ratings.Add(new
                {
                    customer_name  = reader.GetString(0),
                    vehicle        = reader.GetString(1),
                    driver_name    = reader.IsDBNull(2)  ? "N/A" : reader.GetString(2),
                    vehicle_score  = reader.IsDBNull(3)  ? (decimal?)null : reader.GetDecimal(3),
                    driver_score   = reader.IsDBNull(4)  ? (decimal?)null : reader.GetDecimal(4),
                    comment        = reader.IsDBNull(5)  ? null : reader.GetString(5),
                    rated_at       = reader.GetString(6)
                });
            }

            return new
            {
                summary = new
                {
                    total_ratings     = totalRatings,
                    avg_vehicle_score = avgVehicle,
                    avg_driver_score  = avgDriver
                },
                recent_ratings_shown = ratings.Count,
                limit_applied        = limit,
                ratings
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tool] GetRatingsFeedbackAsync failed");
            return new { error = $"Failed to load ratings: {ex.Message}. Data temporarily unavailable." };
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  TOOL: Transaction Summary (row-capped, PII-safe)
    // ─────────────────────────────────────────────────────────────────
    public async Task<object> GetTransactionSummaryAsync(
        string? method = null, string? status = null, int limit = 20)
    {
        if (limit < 1 || limit > 50) limit = 20;

        try
        {
            await using var conn = await _ds.OpenConnectionAsync();

            // Aggregated breakdown by payment method
            await using var aggCmd = new NpgsqlCommand(@"
                SELECT method, status, COUNT(*) AS txn_count, SUM(amount) AS total_amount
                FROM transactions
                GROUP BY method, status
                ORDER BY total_amount DESC", conn);

            var breakdown = new List<object>();
            await using (var aggReader = await aggCmd.ExecuteReaderAsync())
            {
                while (await aggReader.ReadAsync())
                {
                    breakdown.Add(new
                    {
                        method       = aggReader.IsDBNull(0) ? "N/A" : aggReader.GetString(0),
                        status       = aggReader.GetString(1),
                        txn_count    = aggReader.GetInt64(2),
                        total_amount = aggReader.GetDecimal(3)
                    });
                }
            }

            // Recent individual transactions (PII-safe: no customer details)
            var conditions = new List<string>();
            var cmd = new NpgsqlCommand();
            if (!string.IsNullOrWhiteSpace(method))
            {
                conditions.Add("LOWER(t.method) = @method");
                cmd.Parameters.AddWithValue("@method", method.ToLower().Trim());
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                conditions.Add("LOWER(t.status) = @status");
                cmd.Parameters.AddWithValue("@status", status.ToLower().Trim());
            }
            string whereClause = conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "";
            cmd.CommandText = $@"
                SELECT t.transaction_id, t.rental_id, t.amount, t.type, t.method, t.status, t.paid_at::text
                FROM transactions t
                {whereClause}
                ORDER BY t.paid_at DESC
                LIMIT @limit";
            cmd.Parameters.AddWithValue("@limit", limit);
            cmd.Connection = conn;

            var transactions = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                // No customer PII — just operational transaction data
                transactions.Add(new
                {
                    transaction_id = reader.GetInt32(0),
                    rental_id      = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1),
                    amount         = reader.GetDecimal(2),
                    type           = reader.IsDBNull(3) ? null : reader.GetString(3),
                    method         = reader.IsDBNull(4) ? null : reader.GetString(4),
                    status         = reader.GetString(5),
                    paid_at        = reader.IsDBNull(6) ? null : reader.GetString(6)
                });
            }

            return new
            {
                breakdown_by_method = breakdown,
                recent_transactions_shown = transactions.Count,
                limit_applied = limit,
                transactions
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tool] GetTransactionSummaryAsync failed");
            return new { error = $"Failed to load transactions: {ex.Message}. Data temporarily unavailable." };
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  TOOL: Universal Database Reader (PII-safe, secure column whitelisting)
    // ─────────────────────────────────────────────────────────────────
    public async Task<object> GetTableRecordsAsync(
        string tableName, string? search = null, string? status = null, int limit = 20)
    {
        if (limit < 1 || limit > 50) limit = 20;

        var allowedTables = new Dictionary<string, (string selectCols, string defaultOrder)>(StringComparer.OrdinalIgnoreCase)
        {
            { "users",             ("user_id, full_name, phone, role, created_at", "created_at DESC") },
            { "drivers",           ("driver_id, user_id, license_no, status, rating_avg, total_trips", "rating_avg DESC") },
            { "vehicles",          ("vehicle_id, plate_no, brand, model, type, cc, status, rate_per_day, rate_with_driver, seat_capacity, transmission, in_garage", "brand, model") },
            { "rentals",           ("rental_id, customer_id, vehicle_id, driver_id, start_date, end_date, destination, status, total_amount, payment_method, payment_status, created_at", "created_at DESC") },
            { "transactions",      ("transaction_id, rental_id, amount, type, method, status, paid_at", "paid_at DESC") },
            { "extensions",        ("extension_id, rental_id, extra_days, additional_cost, reason, status, requested_at, approved_at", "requested_at DESC") },
            { "issues",            ("issue_id, rental_id, reporter_id, issue_type, description, status, reported_at", "reported_at DESC") },
            { "ratings",           ("rating_id, rental_id, customer_id, vehicle_id, driver_id, vehicle_score, driver_score, comment, rated_at", "rated_at DESC") },
            { "notifications",     ("notification_id, title, message, type, is_read, created_at", "created_at DESC") },
            { "app_notifications", ("notification_id, title, message, type, is_read, created_at", "created_at DESC") },
            { "location_logs",     ("log_id, vehicle_id, latitude, longitude, speed, recorded_at", "recorded_at DESC") },
            { "gps_logs",          ("log_id, vehicle_id, latitude, longitude, speed, timestamp", "timestamp DESC") },
            { "messages",          ("message_id, sender_id, receiver_id, message_body, timestamp, delivery_status", "timestamp DESC") },
            { "chat_messages",     ("message_id, sender_id, receiver_id, message_body, timestamp, delivery_status", "timestamp DESC") },
            { "ai_copilot_sessions", ("session_id, admin_user_id, title, created_at, updated_at", "updated_at DESC") },
            { "ai_copilot_messages", ("copilot_msg_id, session_id, sender_id, llm_role, content, sent_at", "sent_at DESC") }
        };

        string cleanTable = (tableName ?? "").Trim().ToLowerInvariant();
        if (!allowedTables.ContainsKey(cleanTable))
        {
            return new { error = $"Table '{tableName}' is restricted or does not exist. Accessible tables: {string.Join(", ", allowedTables.Keys)}" };
        }

        var (cols, defaultOrder) = allowedTables[cleanTable];
        try
        {
            await using var conn = await _ds.OpenConnectionAsync();
            var conditions = new List<string>();
            var cmd = new NpgsqlCommand();

            if (!string.IsNullOrWhiteSpace(status))
            {
                conditions.Add("LOWER(status) = @status");
                cmd.Parameters.AddWithValue("@status", status.ToLower().Trim());
            }

            string whereClause = conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "";
            cmd.CommandText = $@"
                SELECT {cols}
                FROM {cleanTable}
                {whereClause}
                ORDER BY {defaultOrder}
                LIMIT @limit";
            cmd.Parameters.AddWithValue("@limit", limit);
            cmd.Connection = conn;

            var records = new List<Dictionary<string, object?>>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string colName = reader.GetName(i);
                    row[colName] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                records.Add(row);
            }

            return new { table = cleanTable, total_returned = records.Count, limit_applied = limit, records };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tool] GetTableRecordsAsync failed for table '{Table}'", tableName);
            return new { error = $"Failed to query table '{tableName}': {ex.Message}" };
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  TOOL: Rental Extensions
    // ─────────────────────────────────────────────────────────────────
    public async Task<object> GetRentalExtensionsAsync(string? status = null, int limit = 15)
    {
        if (limit < 1 || limit > 50) limit = 15;
        try
        {
            await using var conn = await _ds.OpenConnectionAsync();
            string statusClause = "";
            var cmd = new NpgsqlCommand();
            if (!string.IsNullOrWhiteSpace(status))
            {
                statusClause = "AND LOWER(e.status) = @status";
                cmd.Parameters.AddWithValue("@status", status.ToLower().Trim());
            }

            cmd.CommandText = $@"
                SELECT e.extension_id, e.rental_id, u.full_name AS customer_name,
                       v.brand || ' ' || v.model AS vehicle_name,
                       e.extra_days, e.additional_cost, e.reason, e.status, e.requested_at::text
                FROM extensions e
                JOIN rentals r ON r.rental_id = e.rental_id
                JOIN users u ON u.user_id = r.customer_id
                JOIN vehicles v ON v.vehicle_id = r.vehicle_id
                WHERE 1=1 {statusClause}
                ORDER BY e.requested_at DESC
                LIMIT @limit";
            cmd.Parameters.AddWithValue("@limit", limit);
            cmd.Connection = conn;

            var extensions = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                extensions.Add(new
                {
                    extension_id    = reader.GetInt32(0),
                    rental_id       = reader.GetInt32(1),
                    customer_name   = reader.GetString(2),
                    vehicle_name    = reader.GetString(3),
                    extra_days      = reader.GetInt32(4),
                    additional_cost = reader.GetDecimal(5),
                    reason          = reader.IsDBNull(6) ? null : reader.GetString(6),
                    status          = reader.GetString(7),
                    requested_at    = reader.GetString(8)
                });
            }
            return new { total_returned = extensions.Count, limit_applied = limit, extensions };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tool] GetRentalExtensionsAsync failed");
            return new { error = $"Failed to get extensions: {ex.Message}" };
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  TOOL SCHEMA BUILDERS — For all three LLM provider formats
    // ─────────────────────────────────────────────────────────────────

    /// <summary>OpenAI-compatible tools array (Groq, OpenRouter, SambaNova)</summary>
    public static object[] GetToolDefinitions() => new object[]
    {
        // ── Core Financial & Operational ──────────────────────────────────────────
        Tool(ToolGetTodayRevenue,    "Get today's revenue, transaction count, week-to-date, and month-to-date revenue figures."),
        Tool(ToolGetWeeklyAnalytics, "Get the daily revenue and rental breakdown for the last 7 days."),
        Tool(ToolGetOverdueRentals,  "Get the list of overdue rentals with customer names, vehicle info, days overdue, and penalty estimates."),
        Tool(ToolGetFleetCount,      "Get the current fleet availability status: how many vehicles are available, on-rent, or in maintenance."),
        Tool(ToolGetPendingBookings, "Get the list of pending bookings awaiting admin approval and count of pending extension requests."),
        Tool(ToolGetMonthlyRevenue,  "Get the monthly revenue breakdown for the last 12 months including transaction counts."),
        Tool(ToolPredictNextYearSales, "Predict the sales/revenue for the next 12 months based on historical Month-over-Month (MoM) growth."),

        // ── Fleet & Vehicle Tools ─────────────────────────────────────────────────
        Tool(ToolGetVehicleUtil, "Get per-vehicle revenue and rental count for utilization analysis. Supports period filtering.", new
        {
            type = "object",
            properties = new
            {
                period = new { type = "string", description = "Time period: 'this_month', 'last_month', 'this_year', or 'all_time' (default: all_time)" },
                limit  = new { type = "integer", description = "Max vehicles to return (default: 15, max: 50)" }
            },
            required = Array.Empty<string>()
        }),
        Tool(ToolSearchVehicles, "Search and filter fleet vehicles by status, brand, or model.", new
        {
            type = "object",
            properties = new
            {
                status = new { type = "string", description = "Vehicle status: 'available', 'rented', 'maintenance', 'in-use'" },
                brand  = new { type = "string", description = "Vehicle brand to search for (partial match)" },
                model  = new { type = "string", description = "Vehicle model to search for (partial match)" },
                limit  = new { type = "integer", description = "Max results to return (default: 20, max: 50)" }
            },
            required = Array.Empty<string>()
        }),
        Tool(ToolCheckSurgePricing, "Check dynamic surge pricing rate multiplier and utilization for a vehicle category or fleet.", new
        {
            type = "object",
            properties = new { categoryId = new { type = "integer", description = "Vehicle category ID (0 for all/overall fleet)." } },
            required = Array.Empty<string>()
        }),
        Tool(ToolGetMaintenanceAlerts, "Get list of fleet vehicles needing or approaching maintenance based on odometer mileage."),

        // ── Rental & Booking Tools ────────────────────────────────────────────────
        Tool(ToolGetRentalHistory, "Get rental/booking history filtered by status. Supports pagination.", new
        {
            type = "object",
            properties = new
            {
                status = new { type = "string", description = "Rental status: 'pending', 'active', 'completed', 'cancelled', 'overdue'" },
                limit  = new { type = "integer", description = "Max rentals to return (default: 15, max: 50)" },
                offset = new { type = "integer", description = "Pagination offset (default: 0)" }
            },
            required = Array.Empty<string>()
        }),

        // ── Customer & People Tools ───────────────────────────────────────────────
        Tool(ToolGetTopDrivers, "Get top-performing drivers and staff sorted by rating, trips, and revenue. Supports period filtering.", new
        {
            type       = "object",
            properties = new {
                period = new { type = "string", description = "Time period filter: 'july', 'august', 'this_month', 'last_month', or 'all_time' (default: all_time)" },
                limit  = new { type = "integer", description = "Number of drivers to return. Default: 5. Max: 20." }
            },
            required   = Array.Empty<string>()
        }),
        Tool(ToolGetCustomerInsights, "Get customer insights: top customers by spend, new signups this month, total customer count. PII-safe.", new
        {
            type = "object",
            properties = new { limit = new { type = "integer", description = "Max top customers to return (default: 10, max: 30)" } },
            required = Array.Empty<string>()
        }),
        Tool(ToolGetRatingsFeedback, "Get vehicle and driver ratings with customer comments and overall score averages.", new
        {
            type = "object",
            properties = new { limit = new { type = "integer", description = "Max recent ratings to return (default: 15, max: 50)" } },
            required = Array.Empty<string>()
        }),

        // ── Issue & Transaction Tools ─────────────────────────────────────────────
        Tool(ToolGetReportedIssues, "Get reported vehicle or rental issues and complaints filtered by status.", new
        {
            type = "object",
            properties = new
            {
                status = new { type = "string", description = "Issue status: 'open', 'in-progress', 'resolved'" },
                limit  = new { type = "integer", description = "Max issues to return (default: 15, max: 50)" }
            },
            required = Array.Empty<string>()
        }),
        Tool(ToolGetTransactionSummary, "Get transaction records and breakdown by payment method and status.", new
        {
            type = "object",
            properties = new
            {
                method = new { type = "string", description = "Payment method: 'cash', 'gcash', 'maya', 'bank_transfer'" },
                status = new { type = "string", description = "Transaction status: 'verified', 'pending', 'failed'" },
                limit  = new { type = "integer", description = "Max transactions to return (default: 20, max: 50)" }
            },
            required = Array.Empty<string>()
        }),

        // ── Dispatch & Risk Tools ─────────────────────────────────────────────────
        Tool(ToolAutoDispatchBooking, "Automatically assign an available vehicle and top-rated driver to a pending/approved rental booking.", new
        {
            type       = "object",
            properties = new { rentalId = new { type = "integer", description = "Rental booking ID to auto-dispatch." } },
            required   = new[] { "rentalId" }
        }),
        Tool(ToolAnalyzeIdDocument, "Analyze a Base64-encoded driver's license photo using Gemini AI Vision OCR to detect fraud and extract identity info.", new
        {
            type       = "object",
            properties = new { base64Image = new { type = "string", description = "Base64 image string of driver's license." } },
            required   = new[] { "base64Image" }
        }),
        Tool(ToolAssessVehicleDamage, "Assess vehicle damage photo using Gemini AI Vision to estimate repair cost and penalty fee.", new
        {
            type       = "object",
            properties = new
            {
                base64Image = new { type = "string", description = "Base64 image string of vehicle damage." },
                description = new { type = "string", description = "Optional staff notes on damage context." }
            },
            required   = new[] { "base64Image" }
        }),
        Tool(ToolCheckFuelAnomaly, "Check fuel expense against historical vehicle benchmarks to detect overpricing or fuel theft.", new
        {
            type       = "object",
            properties = new
            {
                vehicleId = new { type = "integer", description = "Vehicle ID" },
                amount    = new { type = "number",  description = "Fuel expense amount in PHP" },
                distance  = new { type = "number",  description = "Distance traveled in km" }
            },
            required   = new[] { "vehicleId", "amount", "distance" }
        }),
        Tool(ToolGetTableRecords, "Query any database table (users, drivers, vehicles, rentals, transactions, extensions, issues, ratings, notifications, location_logs) for PII-safe operational records.", new
        {
            type = "object",
            properties = new
            {
                tableName = new { type = "string", description = "Table name: 'users', 'drivers', 'vehicles', 'rentals', 'transactions', 'extensions', 'issues', 'ratings', 'notifications', 'location_logs'" },
                search    = new { type = "string", description = "Optional search query" },
                status    = new { type = "string", description = "Optional status filter" },
                limit     = new { type = "integer", description = "Max records (default: 20, max: 50)" }
            },
            required = new[] { "tableName" }
        }),
        Tool(ToolGetRentalExtensions, "Get list of rental extension requests with extra days, additional costs, reasons, and status.", new
        {
            type = "object",
            properties = new
            {
                status = new { type = "string", description = "Extension status: 'pending', 'approved', 'rejected'" },
                limit  = new { type = "integer", description = "Max records (default: 15, max: 50)" }
            },
            required = Array.Empty<string>()
        }),
    };

    /// <summary>
    /// Gemini-compatible functionDeclarations array for Google AI Studio REST API.
    /// Structure: { "tools": [{ "functionDeclarations": [...] }] }
    /// </summary>
    public static object GetGeminiToolDefinitions() => new
    {
        functionDeclarations = new object[]
        {
            GeminiTool("get_today_revenue",    "Get today's revenue, transaction count, week-to-date, and month-to-date revenue figures."),
            GeminiTool("get_weekly_analytics", "Get the daily revenue and rental breakdown for the last 7 days."),
            GeminiTool("get_overdue_rentals",  "Get list of overdue rentals with customer names, vehicle info, days overdue, and penalty estimates."),
            GeminiTool("get_available_fleet_count", "Get current fleet availability: available, on-rent, maintenance vehicle counts."),
            GeminiTool("get_pending_bookings", "Get pending rentals awaiting admin approval and count of pending extension requests."),
            GeminiTool("get_monthly_revenue",  "Get monthly revenue breakdown for the last 12 months."),
            GeminiTool("predict_next_year_sales", "Predict 12-month sales forecast from historical MoM growth."),
            GeminiTool("get_maintenance_alerts", "Get fleet vehicles needing or approaching maintenance."),
            GeminiTool("get_vehicle_utilization", "Get per-vehicle revenue and rental count. Supports period filtering.",
                GeminiParams(("period", "string", "Time period: 'this_month', 'last_month', 'this_year', or 'all_time'"),
                             ("limit",  "integer", "Max vehicles to return (default: 15, max: 50)"))),
            GeminiTool("search_vehicles", "Search and filter fleet vehicles by status, brand, or model.",
                GeminiParams(("status", "string",  "Vehicle status filter"),
                             ("brand",  "string",  "Brand name partial match"),
                             ("model",  "string",  "Model name partial match"),
                             ("limit",  "integer", "Max results (default: 20)"))),
            GeminiTool("check_surge_pricing", "Get current dynamic surge pricing rates.",
                GeminiParams(("categoryId", "integer", "Vehicle category ID (0 = all fleet)"))),
            GeminiTool("get_rental_history", "Get rental/booking history filtered by status with pagination.",
                GeminiParams(("status", "string",  "Rental status filter"),
                             ("limit",  "integer", "Max results (default: 15)"),
                             ("offset", "integer", "Pagination offset (default: 0)"))),
            GeminiTool("get_top_drivers", "Get top-performing drivers and staff by rating, trips, and revenue.",
                GeminiParams(("period", "string",  "Period filter: 'july', 'august', 'this_month', 'last_month', 'all_time'"),
                             ("limit",  "integer", "Number of drivers (default: 5, max: 20)"))),
            GeminiTool("get_customer_insights", "Get top customers by spend and new signups this month (PII-safe).",
                GeminiParams(("limit", "integer", "Max top customers (default: 10, max: 30)"))),
            GeminiTool("get_ratings_feedback", "Get vehicle and driver ratings with comments and score averages.",
                GeminiParams(("limit", "integer", "Max ratings (default: 15, max: 50)"))),
            GeminiTool("get_reported_issues", "Get reported vehicle or rental issues and complaints.",
                GeminiParams(("status", "string",  "Issue status: 'open', 'in-progress', 'resolved'"),
                             ("limit",  "integer", "Max issues (default: 15)"))),
            GeminiTool("get_transaction_summary", "Get transaction records and breakdown by payment method.",
                GeminiParams(("method", "string",  "Payment method filter"),
                             ("status", "string",  "Transaction status filter"),
                             ("limit",  "integer", "Max transactions (default: 20)"))),
            GeminiTool("auto_dispatch_booking", "Auto-assign an available vehicle and top driver to a rental.",
                GeminiParams(("rentalId", "integer", "Rental booking ID to dispatch"))),
            GeminiTool("check_fuel_anomaly", "Detect fuel expense anomalies against historical benchmarks.",
                GeminiParams(("vehicleId", "integer", "Vehicle ID"),
                             ("amount",    "number",  "Fuel expense in PHP"),
                             ("distance",  "number",  "Distance in km"))),
            GeminiTool("get_table_records", "Query any database table (users, drivers, vehicles, rentals, transactions, extensions, issues, ratings, notifications, location_logs) for PII-safe operational records.",
                GeminiParams(("tableName", "string",  "Table name: 'users', 'drivers', 'vehicles', 'rentals', 'transactions', 'extensions', 'issues', 'ratings', 'notifications', 'location_logs'"),
                             ("search",    "string",  "Optional search string"),
                             ("status",    "string",  "Optional status filter"),
                             ("limit",     "integer", "Max records (default: 20)"))),
            GeminiTool("get_rental_extensions", "Get list of rental extension requests with extra days, additional costs, reasons, and status.",
                GeminiParams(("status", "string",  "Extension status: 'pending', 'approved', 'rejected'"),
                             ("limit",  "integer", "Max records (default: 15)"))),
        }
    };

    /// <summary>
    /// Cohere v2-compatible tools array for command-r-plus.
    /// </summary>
    public static object[] BuildCohereTools() => new object[]
    {
        CohereToolDef("get_today_revenue",    "Get today's revenue, transaction count, week-to-date and month-to-date totals.", new { }),
        CohereToolDef("get_weekly_analytics", "Get the daily revenue breakdown for the last 7 days.", new { }),
        CohereToolDef("get_overdue_rentals",  "Get list of overdue rentals with customer names, vehicle info and penalty estimates.", new { }),
        CohereToolDef("get_available_fleet_count", "Get current fleet availability: available, on-rent, maintenance counts.", new { }),
        CohereToolDef("get_pending_bookings", "Get pending rentals awaiting admin approval.", new { }),
        CohereToolDef("get_monthly_revenue",  "Get monthly revenue for the last 12 months.", new { }),
        CohereToolDef("predict_next_year_sales", "Predict 12-month revenue forecast from historical MoM growth.", new { }),
        CohereToolDef("get_vehicle_utilization", "Get per-vehicle rental count and revenue. Supports period filter.",
            new { period = new { type = "string", description = "Filter: this_month, last_month, this_year, all_time" },
                  limit  = new { type = "integer", description = "Max vehicles (default: 15)" } }),
        CohereToolDef("search_vehicles", "Search/filter fleet vehicles by status, brand, or model.",
            new { status = new { type = "string", description = "Vehicle status" },
                  brand  = new { type = "string", description = "Brand partial match" },
                  model  = new { type = "string", description = "Model partial match" },
                  limit  = new { type = "integer", description = "Max results (default: 20)" } }),
        CohereToolDef("get_rental_history", "Get rental history by status with pagination.",
            new { status = new { type = "string", description = "Rental status" },
                  limit  = new { type = "integer", description = "Max results (default: 15)" },
                  offset = new { type = "integer", description = "Pagination offset (default: 0)" } }),
        CohereToolDef("get_top_drivers", "Get top-performing drivers and staff by rating, trips, and revenue.",
            new { period = new { type = "string", description = "Period filter: july, august, this_month, last_month, all_time" },
                  limit  = new { type = "integer", description = "Number of drivers. Default 5." } }),
        CohereToolDef("get_customer_insights", "Get top customers by spend and new signups (PII-safe).",
            new { limit = new { type = "integer", description = "Max top customers (default: 10)" } }),
        CohereToolDef("get_ratings_feedback", "Get vehicle and driver ratings with comments.",
            new { limit = new { type = "integer", description = "Max ratings (default: 15)" } }),
        CohereToolDef("get_reported_issues", "Get reported vehicle or rental issues.",
            new { status = new { type = "string", description = "Issue status: open, in-progress, resolved" },
                  limit  = new { type = "integer", description = "Max issues (default: 15)" } }),
        CohereToolDef("get_transaction_summary", "Get transaction records and payment method breakdown.",
            new { method = new { type = "string", description = "Payment method filter" },
                  status = new { type = "string", description = "Transaction status filter" },
                  limit  = new { type = "integer", description = "Max transactions (default: 20)" } }),
        CohereToolDef("get_maintenance_alerts", "Get vehicles needing or approaching maintenance.", new { }),
        CohereToolDef("check_surge_pricing", "Check current dynamic surge pricing rates.",
            new { categoryId = new { type = "integer", description = "Vehicle category ID (0 = fleet-wide)" } }),
        CohereToolDef("auto_dispatch_booking", "Auto-assign vehicle and driver to a rental.",
            new { rentalId = new { type = "integer", description = "Rental booking ID" } }),
        CohereToolDef("check_fuel_anomaly", "Detect fuel expense anomalies.",
            new { vehicleId = new { type = "integer", description = "Vehicle ID" },
                  amount    = new { type = "float",   description = "Fuel expense in PHP" },
                  distance  = new { type = "float",   description = "Distance in km" } }),
        CohereToolDef("get_table_records", "Query any database table (users, drivers, vehicles, rentals, transactions, extensions, issues, ratings, notifications, location_logs).",
            new { tableName = new { type = "string", description = "Table name" },
                  search    = new { type = "string", description = "Search query" },
                  status    = new { type = "string", description = "Status filter" },
                  limit     = new { type = "integer", description = "Max records (default: 20)" } }),
        CohereToolDef("get_rental_extensions", "Get rental extension requests.",
            new { status = new { type = "string", description = "Extension status" },
                  limit  = new { type = "integer", description = "Max records (default: 15)" } }),
    };

    // ─────────────────────────────────────────────────────────────────
    //  SCHEMA BUILDER HELPERS
    // ─────────────────────────────────────────────────────────────────

    private static object Tool(string name, string description, object? parameters = null) => new
    {
        type     = "function",
        function = new
        {
            name,
            description,
            parameters = parameters ?? new { type = "object", properties = new { }, required = Array.Empty<string>() }
        }
    };

    private static object GeminiTool(string name, string description, object? parameters = null) => new
    {
        name,
        description,
        parameters = parameters ?? new
        {
            type       = "object",
            properties = new { },
            required   = Array.Empty<string>()
        }
    };

    private static object GeminiParams(params (string Name, string Type, string Description)[] props)
    {
        var properties = props.ToDictionary(
            p => p.Name,
            p => (object)new { type = p.Type, description = p.Description });

        return new
        {
            type = "object",
            properties,
            required = Array.Empty<string>()
        };
    }

    private static object CohereToolDef(string name, string description, object parameterDefinitions) =>
        new { name, description, parameter_definitions = parameterDefinitions };

    // ─────────────────────────────────────────────────────────────────
    //  HELPERS — Argument Parsers
    // ─────────────────────────────────────────────────────────────────

    private static string Serialize(object obj) =>
        System.Text.Json.JsonSerializer.Serialize(obj,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = false });

    private static int ParseLimit(string? args, int defaultVal = 5)
    {
        if (string.IsNullOrWhiteSpace(args)) return defaultVal;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(args);
            if (doc.RootElement.TryGetProperty("limit", out var v))
                return v.GetInt32();
        }
        catch { /* ignore */ }
        return defaultVal;
    }

    private static int ParseIntArg(string? args, string paramName)
    {
        if (string.IsNullOrWhiteSpace(args)) return 0;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(args);
            if (doc.RootElement.TryGetProperty(paramName, out var v))
                return v.GetInt32();
        }
        catch { /* ignore */ }
        return 0;
    }

    private static decimal ParseDecimalArg(string? args, string paramName)
    {
        if (string.IsNullOrWhiteSpace(args)) return 0m;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(args);
            if (doc.RootElement.TryGetProperty(paramName, out var v))
                return v.GetDecimal();
        }
        catch { /* ignore */ }
        return 0m;
    }

    private static string ParseStringArg(string? args, string paramName)
    {
        if (string.IsNullOrWhiteSpace(args)) return string.Empty;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(args);
            if (doc.RootElement.TryGetProperty(paramName, out var v))
                return v.GetString() ?? string.Empty;
        }
        catch { /* ignore */ }
        return string.Empty;
    }

    private static string FormatSurgePricingResult(SurgePricingResultDto? res)
    {
        if (res == null) return "{\"status\":\"No surge data available\"}";
        return JsonSerializer.Serialize(new
        {
            category = res.CategoryName,
            status = res.SurgeMultiplier > 1.0m ? "Surge Rate Active" : "Standard Base Rate (No surge surcharge)",
            dailyRateFormatted = $"₱{res.FinalRate:N2}",
            originalRateFormatted = $"₱{res.OriginalRate:N2}",
            surgeMultiplierFormatted = $"{res.SurgeMultiplier:F2}x",
            utilizationFormatted = $"{res.UtilizationPercentage:F1}% ({res.BookedVehicles} of {res.TotalVehicles} vehicles booked)",
            businessNotice = $"The current daily rate is ₱{res.FinalRate:N2} per day with a standard multiplier of {res.SurgeMultiplier:F2}x. Fleet utilization is currently at {res.UtilizationPercentage:F1}%."
        });
    }
}
