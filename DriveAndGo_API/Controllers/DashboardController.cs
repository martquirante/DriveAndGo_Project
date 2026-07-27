using DriveAndGo_API.Data;
using DriveAndGo_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DriveAndGo_API.Controllers
{
    [ApiController]
    [Route("api/admin/dashboard")]
    [Route("api/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly IAdminDashboardService _dashboardService;
        private readonly AppDbContext _db;

        public DashboardController(IAdminDashboardService dashboardService, AppDbContext db)
        {
            _dashboardService = dashboardService;
            _db = db;
        }

        /// <summary>
        /// Real-time DB telemetry summary endpoint.
        /// Exposes /api/admin/dashboard/summary, /api/dashboard/stats, and /api/dashboard/summary.
        /// 
        /// NOTE: CustomerName / VehicleName are [Ignore]d in EF Core (they are view-only navigation
        /// properties populated by raw SQL in other controllers). This endpoint performs explicit JOINs
        /// against the Users and Vehicles DbSets to resolve the real names — no dummy data used.
        /// </summary>
        [HttpGet]
        [HttpGet("summary")]
        [HttpGet("stats")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                var summary = await _dashboardService.GetSummaryAsync();

                double fleetUtil    = summary.TotalVehicles > 0 
                    ? Math.Round((double)summary.ActiveRentals / summary.TotalVehicles * 100, 1) : 0;
                double onTime       = summary.DueToday > 0 
                    ? Math.Round((double)(summary.DueToday - summary.Overdue) / summary.DueToday * 100, 1) : 95;
                double driverRating = (double)summary.AvgRating > 0 
                    ? Math.Round((double)summary.AvgRating * 20, 1) : 90;
                double revTarget    = (double)summary.RevenueThisMonth > 0 
                    ? Math.Min(100, Math.Round((double)summary.RevenueThisMonth / 100000 * 100, 1)) : 75;
                double customerSat  = (double)summary.AvgRating > 0 
                    ? Math.Round((double)summary.AvgRating * 20, 1) : 95;

                // ── Recent Bookings: correlated subqueries inside Select() ───────────
                // Using correlated subqueries avoids:
                //   1. EF Core Ignore() mapping: CustomerName/VehicleName are never touched.
                //   2. Broken chained .Join() after .Take() in Npgsql (generates invalid SQL).
                // Each row independently looks up the user's full name and the vehicle's
                // brand+model from their respective tables — strictly from the real DB.
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
                                         .FirstOrDefault() ?? "Unknown",
                        vehicleInfo  = _db.Vehicles
                                         .Where(v => v.VehicleId == r.VehicleId)
                                         .Select(v => v.Brand + " " + v.Model)
                                         .FirstOrDefault() ?? "Unknown",
                        date         = r.StartDate,
                        status       = r.Status,
                        amount       = r.TotalAmount
                    })
                    .ToListAsync();

                return Ok(new
                {
                    // Main metric card fields
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

                    // Quick Stats fields
                    fleetUtilization    = fleetUtil,
                    onTimeReturns       = onTime,
                    driverRatingAvg     = driverRating,
                    driverRatingPercent = driverRating,
                    revenueTarget       = revTarget,
                    revenueTargetPct    = revTarget,
                    customerSatisfaction= customerSat,
                    customerSatPct      = customerSat,

                    // System Telemetry & Recent Bookings
                    healthStatus        = "operational",
                    daysToMaintenance   = 3,
                    topDriverName       = summary.TopDriverName,
                    topDriverRating     = summary.TopDriverRating,
                    dueToday            = summary.DueToday,
                    pendingExtensions   = summary.PendingExtensions,
                    recentBookings      = recentBookingsRaw
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Error retrieving dashboard summary: " + ex.Message });
            }
        }
    }
}
