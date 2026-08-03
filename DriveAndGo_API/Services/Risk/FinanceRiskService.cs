using DriveAndGo_API.Models.Risk;
using Npgsql;

namespace DriveAndGo_API.Services.Risk;

/// <summary>
/// Finance & Risk Management Service implementation:
/// Fuel Overpricing / Theft Anomaly Detection & Split Payment Reminders.
/// </summary>
public class FinanceRiskService : IFinanceRiskService
{
    private readonly NpgsqlDataSource _ds;
    private readonly ILogger<FinanceRiskService> _logger;

    public FinanceRiskService(NpgsqlDataSource ds, ILogger<FinanceRiskService> logger)
    {
        _ds     = ds;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  1. FUEL OVERPRICING / THEFT ANOMALY DETECTION ENGINE
    // ═══════════════════════════════════════════════════════════════════
    public async Task<FuelAnomalyDto> CheckFuelAnomalyAsync(
        int vehicleId, decimal fuelAmount, decimal distanceTraveled)
    {
        if (distanceTraveled <= 0) distanceTraveled = 1m; // Avoid divide by zero

        decimal costPerKm = Math.Round(fuelAmount / distanceTraveled, 2);
        decimal historicalAvgCostPerKm = 8.50m; // Default benchmark ₱8.50/km in PH

        await using var conn = await _ds.OpenConnectionAsync();

        // Query historical average cost/km from fuel_logs table for this vehicle if data exists
        await using (var cmd = new NpgsqlCommand(@"
            SELECT COALESCE(AVG(cost / NULLIF(current_odometer, 0)), 8.50)
            FROM fuel_logs
            WHERE vehicle_id = @vid AND cost > 0 AND current_odometer > 0", conn))
        {
            cmd.Parameters.AddWithValue("@vid", vehicleId);
            object? val = await cmd.ExecuteScalarAsync();
            if (val != null && val != DBNull.Value && Convert.ToDecimal(val) > 0)
            {
                historicalAvgCostPerKm = Math.Round(Convert.ToDecimal(val), 2);
            }
        }

        double discrepancyPct = Math.Round((double)((costPerKm - historicalAvgCostPerKm) / historicalAvgCostPerKm) * 100, 1);

        bool isAnomaly;
        string riskLevel;
        string riskReason;

        if (discrepancyPct > 50.0)
        {
            isAnomaly  = true;
            riskLevel  = "High Risk";
            riskReason = $"CRITICAL FUEL DISCREPANCY: Current cost of ₱{costPerKm:F2}/km is {discrepancyPct:F1}% higher than vehicle historical baseline (₱{historicalAvgCostPerKm:F2}/km). High risk of overpricing, receipt falsification, or fuel theft.";
        }
        else if (discrepancyPct > 25.0)
        {
            isAnomaly  = true;
            riskLevel  = "Moderate Risk";
            riskReason = $"ELEVATED FUEL EXPENSE: Current cost of ₱{costPerKm:F2}/km is {discrepancyPct:F1}% above historical average (₱{historicalAvgCostPerKm:F2}/km). Requires admin review.";
        }
        else
        {
            isAnomaly  = false;
            riskLevel  = "Normal";
            riskReason = $"Fuel expense of ₱{costPerKm:F2}/km aligns with historical vehicle efficiency (₱{historicalAvgCostPerKm:F2}/km).";
        }

        _logger.LogInformation(
            "Fuel Anomaly check vehicle #{Vid}: Cost/km=₱{Cost}, Avg=₱{Avg}, Discrepancy={Disc}%, Risk={Risk}",
            vehicleId, costPerKm, historicalAvgCostPerKm, discrepancyPct, riskLevel);

        return new FuelAnomalyDto
        {
            VehicleId               = vehicleId,
            CurrentFuelCost         = fuelAmount,
            DistanceTraveled        = distanceTraveled,
            CostPerKm               = costPerKm,
            HistoricalAvgCostPerKm = historicalAvgCostPerKm,
            DiscrepancyPercentage   = discrepancyPct,
            IsAnomaly               = isAnomaly,
            RiskLevel               = riskLevel,
            RiskReason              = riskReason
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    //  2. SPLIT PAYMENT REMINDER ENGINE
    // ═══════════════════════════════════════════════════════════════════
    public async Task<SplitPayReminderDto> GenerateSplitPayRemindersAsync(int rentalId)
    {
        await using var conn = await _ds.OpenConnectionAsync();

        string customerName = "Customer";
        decimal totalAmount = 0m;

        await using (var cmd = new NpgsqlCommand(@"
            SELECT r.total_amount, u.full_name
            FROM rentals r
            JOIN users u ON u.user_id = r.customer_id
            WHERE r.rental_id = @rid", conn))
        {
            cmd.Parameters.AddWithValue("@rid", rentalId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                totalAmount  = reader.GetDecimal(0);
                customerName = reader.GetString(1);
            }
        }

        // Calculate unpaid share (assume 50% split balance if partial payment)
        decimal unpaidAmount = Math.Round(totalAmount * 0.50m, 2);
        string paymentLink   = $"https://pay.driveandgo.ph/split/{rentalId}?token=dng_sp_{rentalId}";

        string sms = $"[DriveAndGo] Hi {customerName}, your split payment share for Rental #{rentalId} is pending: ₱{unpaidAmount:N2}. Pay securely via GCash/Maya: {paymentLink}";

        string email = $"""
            Dear {customerName},

            This is a friendly reminder regarding your group split payment for DriveAndGo Rental #{rentalId}.

            • Total Rental Amount : ₱{totalAmount:N2}
            • Your Remaining Share: ₱{unpaidAmount:N2}
            • Secure Payment Link : {paymentLink}

            Please complete payment within 24 hours to keep your vehicle reservation active.

            Best regards,
            DriveAndGo Financial Operations Team
            """;

        _logger.LogInformation("Split pay reminder generated for Rental #{Rid}", rentalId);

        return new SplitPayReminderDto
        {
            RentalId            = rentalId,
            CustomerName        = customerName,
            TotalAmount         = totalAmount,
            UnpaidAmount        = unpaidAmount,
            PendingMembersCount = 2,
            PaymentLink         = paymentLink,
            DraftedSmsText      = sms,
            DraftedEmailText    = email
        };
    }
}
