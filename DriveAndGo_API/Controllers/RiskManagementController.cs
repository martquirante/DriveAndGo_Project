using DriveAndGo_API.Models.Risk;
using DriveAndGo_API.Services.Risk;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriveAndGo_API.Controllers;

/// <summary>
/// REST API Controller for Phase 4 Risk Management:
/// AI Vision (KYC ID Inspection, Vehicle Damage Assessment),
/// Fuel Anomaly Detection, and Split Payment Reminders.
/// </summary>
[Route("api/risk")]
[ApiController]
public class RiskManagementController : ControllerBase
{
    private readonly IAiVisionService _vision;
    private readonly IFinanceRiskService _finance;
    private readonly ILogger<RiskManagementController> _logger;

    public RiskManagementController(
        IAiVisionService vision,
        IFinanceRiskService finance,
        ILogger<RiskManagementController> logger)
    {
        _vision  = vision;
        _finance = finance;
        _logger  = logger;
    }

    // ─────────────────────────────────────────────────────────────────
    //  POST /api/risk/analyze-id — Driver License OCR & Fraud Check
    // ─────────────────────────────────────────────────────────────────
    [HttpPost("analyze-id")]
    [AllowAnonymous] // Swappable to [Authorize]
    public async Task<IActionResult> AnalyzeDriverLicense([FromBody] LicenseAnalysisRequestDto req)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(req.Base64Image))
                return BadRequest(new { Message = "Base64Image string is required." });

            var result = await _vision.AnalyzeDriverLicenseAsync(req.Base64Image);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze driver license.");
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  POST /api/risk/assess-damage — Visual Vehicle Damage Assessment
    // ─────────────────────────────────────────────────────────────────
    [HttpPost("assess-damage")]
    [AllowAnonymous]
    public async Task<IActionResult> AssessVehicleDamage([FromBody] DamageAssessmentRequestDto req)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(req.Base64Image))
                return BadRequest(new { Message = "Base64Image string is required." });

            var result = await _vision.AssessVehicleDamageAsync(req.Base64Image, req.Description);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assess vehicle damage.");
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  GET /api/risk/fuel-anomaly — Fuel Expense Anomaly Check
    // ─────────────────────────────────────────────────────────────────
    [HttpGet("fuel-anomaly")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckFuelAnomaly(
        [FromQuery] int vehicleId,
        [FromQuery] decimal amount,
        [FromQuery] decimal distance)
    {
        try
        {
            if (vehicleId <= 0 || amount <= 0 || distance <= 0)
                return BadRequest(new { Message = "vehicleId, amount, and distance must be greater than zero." });

            var result = await _finance.CheckFuelAnomalyAsync(vehicleId, amount, distance);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check fuel anomaly for vehicle #{VehicleId}", vehicleId);
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  POST /api/risk/rentals/{id}/split-pay-reminders
    // ─────────────────────────────────────────────────────────────────
    [HttpPost("rentals/{rentalId:int}/split-pay-reminders")]
    [AllowAnonymous]
    public async Task<IActionResult> GenerateSplitPayReminders(int rentalId)
    {
        try
        {
            if (rentalId <= 0)
                return BadRequest(new { Message = "Valid rentalId is required." });

            var result = await _finance.GenerateSplitPayRemindersAsync(rentalId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate split pay reminders for rental #{RentalId}", rentalId);
            return StatusCode(500, new { Message = ex.Message });
        }
    }
}
