using DriveAndGo_API.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace DriveAndGo_API.Services;

// ─────────────────────────────────────────────────────────────
//  Interface
// ─────────────────────────────────────────────────────────────
public interface IAdminDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync();
    Task<IEnumerable<RevenuePeriodDto>> GetRevenueAsync(string period);
    Task<IEnumerable<TopDriverDto>> GetTopDriversAsync(int limit = 5);
}

// ─────────────────────────────────────────────────────────────
//  DTOs
// ─────────────────────────────────────────────────────────────
public record DashboardSummaryDto(
    int TotalUsers,
    int TotalVehicles,
    int ActiveRentals,
    int PendingRentals,
    decimal TotalRevenueAllTime,
    decimal RevenueThisMonth,
    int TotalReviews,
    decimal AvgRating,
    int DueToday,
    int Overdue,
    int PendingExtensions,
    int OpenIssues,
    string TopDriverName,
    decimal TopDriverRating
);

public record RevenuePeriodDto(
    string Period,
    int TotalTransactions,
    decimal TotalAmount
);

public record TopDriverDto(
    int DriverId,
    string FullName,
    decimal RatingAvg,
    int TotalTrips
);

// ─────────────────────────────────────────────────────────────
//  Implementation
// ─────────────────────────────────────────────────────────────
public class AdminDashboardService : IAdminDashboardService
{
    private readonly AppDbContext _db;

    public AdminDashboardService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        var totalUsers = await _db.Users.CountAsync();
        var totalVehicles = await _db.Vehicles.CountAsync();

        var activeRentals = await _db.Rentals
            .CountAsync(r => r.Status == "active" || r.Status == "approved" || r.Status == "in-use");

        var pendingRentals = await _db.Rentals
            .CountAsync(r => r.Status == "pending");

        var totalRevenue = await _db.Transactions
            .Where(t => t.Status == "confirmed" || t.Status == "paid" || t.Status == "verified")
            .SumAsync(t => (decimal?)t.Amount) ?? 0;

        var firstOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var revenueThisMonth = await _db.Transactions
            .Where(t => (t.Status == "confirmed" || t.Status == "paid" || t.Status == "verified")
                        && t.PaidAt >= firstOfMonth)
            .SumAsync(t => (decimal?)t.Amount) ?? 0;

        var totalReviews = await _db.Ratings.CountAsync();
        var avgRating = totalReviews > 0 ? await _db.Ratings.AverageAsync(r => (decimal?)r.VehicleScore) ?? 0m : 0m;

        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);
        var dueToday = await _db.Rentals
            .CountAsync(r => (r.Status == "active" || r.Status == "in-use") && r.EndDate >= todayStart && r.EndDate < todayEnd);

        var overdue = await _db.Rentals
            .CountAsync(r => r.Status == "overdue" || ((r.Status == "active" || r.Status == "in-use") && r.EndDate < DateTime.UtcNow));

        var pendingExtensions = await _db.Extensions.CountAsync(e => e.Status == "pending");
        var openIssues = await _db.Issues.CountAsync(i => i.Status == "open");

        var topDriver = await _db.Drivers
            .AsNoTracking()
            .Join(_db.Users, d => d.UserId, u => u.UserId, (d, u) => new { u.FullName, d.RatingAvg, d.TotalTrips })
            .Where(x => x.TotalTrips > 0)
            .OrderByDescending(x => x.RatingAvg)
            .ThenByDescending(x => x.TotalTrips)
            .FirstOrDefaultAsync();

        return new DashboardSummaryDto(
            totalUsers,
            totalVehicles,
            activeRentals,
            pendingRentals,
            totalRevenue,
            revenueThisMonth,
            totalReviews,
            Math.Round(avgRating, 1),
            dueToday,
            overdue,
            pendingExtensions,
            openIssues,
            topDriver?.FullName ?? "No driver ratings yet",
            topDriver?.RatingAvg ?? 0m
        );
    }

    public async Task<IEnumerable<RevenuePeriodDto>> GetRevenueAsync(string period)
    {
        var confirmedStatuses = new[] { "confirmed", "paid", "verified" };

        var query = _db.Transactions
            .Where(t => confirmedStatuses.Contains(t.Status!.ToLower()))
            .AsNoTracking();

        // Group by period in memory (EF Core Npgsql handles TO_CHAR poorly in groupby)
        var raw = await query
            .Select(t => new { t.PaidAt, t.Amount })
            .ToListAsync();

        var grouped = period.ToLowerInvariant() switch
        {
            "daily" => raw
                .GroupBy(t => t.PaidAt!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            "yearly" => raw
                .GroupBy(t => t.PaidAt!.Value.ToString("yyyy", CultureInfo.InvariantCulture)),
            _ => raw
                .GroupBy(t => t.PaidAt!.Value.ToString("yyyy-MM", CultureInfo.InvariantCulture)) // monthly default
        };

        return grouped
            .Select(g => new RevenuePeriodDto(g.Key, g.Count(), g.Sum(x => x.Amount)))
            .OrderByDescending(x => x.Period)
            .Take(12);
    }

    public async Task<IEnumerable<TopDriverDto>> GetTopDriversAsync(int limit = 5)
    {
        return await _db.Drivers
            .AsNoTracking()
            .Join(
                _db.Users,
                d => d.UserId,
                u => u.UserId,
                (d, u) => new TopDriverDto(d.DriverId, u.FullName, d.RatingAvg ?? 0m, d.TotalTrips))
            .OrderByDescending(d => d.RatingAvg)
            .ThenByDescending(d => d.TotalTrips)
            .Take(limit)
            .ToListAsync();
    }
}
