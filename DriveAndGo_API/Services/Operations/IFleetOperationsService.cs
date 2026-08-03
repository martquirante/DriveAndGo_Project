using DriveAndGo_API.Models.Operations;

namespace DriveAndGo_API.Services.Operations;

/// <summary>
/// Interface for Fleet Operations Service — core operational engines:
/// Dynamic Surge Pricing, Predictive Maintenance, and AI Auto-Dispatcher.
/// </summary>
public interface IFleetOperationsService
{
    /// <summary>
    /// Computes dynamic surge pricing multiplier based on category utilization.
    /// Returns 1.20x (20% surge) if utilization >= 80%, 1.10x if >= 60%, else 1.00x.
    /// </summary>
    Task<SurgePricingResultDto> CalculateSurgePriceAsync(int vehicleCategoryId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Analyzes fleet odometer logs and identifies vehicles requiring or approaching maintenance.
    /// </summary>
    Task<List<VehicleMaintenanceAlertDto>> GetMaintenanceAlertsAsync();

    /// <summary>
    /// Automatically assigns an optimal available vehicle and top-rated driver to a pending rental.
    /// </summary>
    Task<AutoDispatchResultDto> AutoAssignBookingAsync(int rentalId);
}
