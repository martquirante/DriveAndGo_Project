using DriveAndGo_API.Models.Risk;

namespace DriveAndGo_API.Services.Risk;

/// <summary>
/// Contract for Finance & Risk Management Service:
/// Fuel Overpricing / Theft Anomaly Detection & Split Payment Reminders.
/// </summary>
public interface IFinanceRiskService
{
    /// <summary>
    /// Evaluates fuel expense against historical PHP/km benchmarks for a vehicle to detect overpricing or theft.
    /// </summary>
    Task<FuelAnomalyDto> CheckFuelAnomalyAsync(int vehicleId, decimal fuelAmount, decimal distanceTraveled);

    /// <summary>
    /// Generates structured payment links and draft SMS/Email reminders for pending split payments on a rental booking.
    /// </summary>
    Task<SplitPayReminderDto> GenerateSplitPayRemindersAsync(int rentalId);
}
