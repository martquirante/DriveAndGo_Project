using DriveAndGo_API.Models.Operations;
using DriveAndGo_API.Services.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriveAndGo_API.Controllers;

/// <summary>
/// REST API Controller for Fleet & Driver Operational Engines:
/// Dynamic Surge Pricing, Predictive Maintenance Alerts, and AI Auto-Dispatcher.
/// </summary>
[Route("api/operations")]
[ApiController]
public class OperationsController : ControllerBase
{
    private readonly IFleetOperationsService _ops;
    private readonly ILogger<OperationsController> _logger;

    public OperationsController(IFleetOperationsService ops, ILogger<OperationsController> logger)
    {
        _ops    = ops;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────
    //  GET /api/operations/surge-pricing
    // ─────────────────────────────────────────────────────────────────
    [HttpGet("surge-pricing")]
    [AllowAnonymous] // Swappable to [Authorize]
    public async Task<IActionResult> GetSurgePricing(
        [FromQuery] int categoryId = 0,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null)
    {
        try
        {
            DateTime startDate = start ?? DateTime.Today;
            DateTime endDate   = end   ?? DateTime.Today.AddDays(1);

            var result = await _ops.CalculateSurgePriceAsync(categoryId, startDate, endDate);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute surge pricing.");
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  GET /api/operations/maintenance-alerts
    // ─────────────────────────────────────────────────────────────────
    [HttpGet("maintenance-alerts")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMaintenanceAlerts()
    {
        try
        {
            var alerts = await _ops.GetMaintenanceAlertsAsync();
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch maintenance alerts.");
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  POST /api/operations/rentals/{id}/auto-assign
    // ─────────────────────────────────────────────────────────────────
    [HttpPost("rentals/{rentalId:int}/auto-assign")]
    [AllowAnonymous]
    public async Task<IActionResult> AutoAssignBooking(int rentalId)
    {
        try
        {
            if (rentalId <= 0)
                return BadRequest(new { Message = "Valid rentalId is required." });

            var result = await _ops.AutoAssignBookingAsync(rentalId);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-dispatch rental #{RentalId}", rentalId);
            return StatusCode(500, new { Message = ex.Message });
        }
    }
}
