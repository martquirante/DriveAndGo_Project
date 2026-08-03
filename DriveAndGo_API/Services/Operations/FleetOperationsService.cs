using DriveAndGo_API.Models.Operations;
using Npgsql;

namespace DriveAndGo_API.Services.Operations;

/// <summary>
/// Fleet Operations Service implementation.
/// Implements:
///   1. Dynamic Surge Pricing Engine
///   2. Predictive Maintenance Engine
///   3. AI Auto-Dispatcher Engine
/// </summary>
public class FleetOperationsService : IFleetOperationsService
{
    private readonly NpgsqlDataSource _ds;
    private readonly ILogger<FleetOperationsService> _logger;

    public FleetOperationsService(NpgsqlDataSource ds, ILogger<FleetOperationsService> logger)
    {
        _ds     = ds;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  1. DYNAMIC SURGE PRICING ENGINE
    // ═══════════════════════════════════════════════════════════════════
    public async Task<SurgePricingResultDto> CalculateSurgePriceAsync(
        int vehicleCategoryId, DateTime startDate, DateTime endDate)
    {
        var result = new SurgePricingResultDto
        {
            VehicleCategoryId = vehicleCategoryId,
            CategoryName      = "Fleet Category " + (vehicleCategoryId > 0 ? vehicleCategoryId.ToString() : "All")
        };

        await using var conn = await _ds.OpenConnectionAsync();

        // 1. Get total vehicles count and base rate for category (or overall)
        await using (var cmd = new NpgsqlCommand(@"
            SELECT COUNT(*), COALESCE(AVG(rate_per_day), 2500)
            FROM vehicles
            WHERE LOWER(status) != 'retired'", conn))
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                result.TotalVehicles = reader.GetInt32(0);
                result.OriginalRate  = Math.Round(reader.GetDecimal(1), 2);
            }
        }

        if (result.TotalVehicles == 0)
        {
            result.FinalRate = result.OriginalRate;
            return result;
        }

        // 2. Count active/pending bookings overlapping the date range
        await using (var cmd2 = new NpgsqlCommand(@"
            SELECT COUNT(DISTINCT vehicle_id)
            FROM rentals
            WHERE LOWER(status) IN ('pending', 'approved', 'active', 'in-use', 'assigned')
              AND start_date <= @endDate AND end_date >= @startDate", conn))
        {
            cmd2.Parameters.AddWithValue("@startDate", startDate.Date);
            cmd2.Parameters.AddWithValue("@endDate",   endDate.Date);

            result.BookedVehicles = Convert.ToInt32(await cmd2.ExecuteScalarAsync());
        }

        // 3. Calculate utilization percentage
        result.UtilizationPercentage = Math.Round((double)result.BookedVehicles / result.TotalVehicles * 100, 1);

        // 4. Determine surge multiplier based on strict rules:
        //    >= 80% → 1.20x (20% surge)
        //    >= 60% → 1.10x (10% surge)
        //    < 60%  → 1.00x (Normal)
        if (result.UtilizationPercentage >= 80.0)
        {
            result.SurgeMultiplier = 1.20m;
            result.SurgeReason     = $"High Demand ({result.UtilizationPercentage:F1}% fleet utilization) — 20% surge active.";
        }
        else if (result.UtilizationPercentage >= 60.0)
        {
            result.SurgeMultiplier = 1.10m;
            result.SurgeReason     = $"Moderate Demand ({result.UtilizationPercentage:F1}% fleet utilization) — 10% surge active.";
        }
        else
        {
            result.SurgeMultiplier = 1.00m;
            result.SurgeReason     = $"Normal Demand ({result.UtilizationPercentage:F1}% fleet utilization) — Base rate applies.";
        }

        result.FinalRate = Math.Round(result.OriginalRate * result.SurgeMultiplier, 2);

        _logger.LogInformation(
            "Surge Price calculated for Cat {CatId}: Util={Util}%, Multiplier={Mult}, Final={Final}",
            vehicleCategoryId, result.UtilizationPercentage, result.SurgeMultiplier, result.FinalRate);

        return result;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  2. PREDICTIVE MAINTENANCE ENGINE
    // ═══════════════════════════════════════════════════════════════════
    public async Task<List<VehicleMaintenanceAlertDto>> GetMaintenanceAlertsAsync()
    {
        var alerts = new List<VehicleMaintenanceAlertDto>();
        await using var conn = await _ds.OpenConnectionAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT
                vehicle_id,
                brand || ' ' || model AS brand_model,
                plate_no,
                COALESCE(current_odometer, 0)         AS current_odo,
                COALESCE(last_maintenance_odometer, 0) AS last_maint_odo
            FROM vehicles
            WHERE LOWER(status) != 'retired'
            ORDER BY (COALESCE(current_odometer, 0) - COALESCE(last_maintenance_odometer, 0)) DESC", conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var currentOdo   = reader.GetDecimal(3);
            var lastMaintOdo = reader.GetDecimal(4);
            var kmSince      = currentOdo - lastMaintOdo;

            string riskLevel;
            string recommendation;

            if (kmSince >= 5000m)
            {
                riskLevel      = "High Risk";
                recommendation = $"CRITICAL: Overdue by {(kmSince - 5000m):N0} km. Perform immediate oil change & 5,000 km safety inspection.";
            }
            else if (kmSince >= 4000m)
            {
                riskLevel      = "Approaching";
                recommendation = $"ATTENTION: Within {(5000m - kmSince):N0} km of threshold. Schedule routine service soon.";
            }
            else
            {
                riskLevel      = "Normal";
                recommendation = "Optimal vehicle health. Next service due in " + (5000m - kmSince).ToString("N0") + " km.";
            }

            alerts.Add(new VehicleMaintenanceAlertDto
            {
                VehicleId               = reader.GetInt32(0),
                BrandModel              = reader.GetString(1),
                PlateNo                 = reader.GetString(2),
                CurrentOdometer         = currentOdo,
                LastMaintenanceOdometer = lastMaintOdo,
                KmSinceMaintenance      = kmSince,
                RiskLevel               = riskLevel,
                RecommendedAction       = recommendation
            });
        }

        _logger.LogInformation("Predictive maintenance analysis completed: {Count} vehicles checked.", alerts.Count);
        return alerts;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  3. AI AUTO-DISPATCHER ENGINE
    // ═══════════════════════════════════════════════════════════════════
    public async Task<AutoDispatchResultDto> AutoAssignBookingAsync(int rentalId)
    {
        await using var conn = await _ds.OpenConnectionAsync();

        // 1. Load rental details
        int customerId;
        int currentVehicleId;
        string customerName = "Customer";

        await using (var cmd = new NpgsqlCommand(@"
            SELECT r.customer_id, r.vehicle_id, u.full_name, LOWER(r.status)
            FROM rentals r
            JOIN users u ON u.user_id = r.customer_id
            WHERE r.rental_id = @rid", conn))
        {
            cmd.Parameters.AddWithValue("@rid", rentalId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return new AutoDispatchResultDto
                {
                    RentalId = rentalId,
                    Success  = false,
                    Message  = $"Rental #{rentalId} not found."
                };
            }

            customerId       = reader.GetInt32(0);
            currentVehicleId = reader.GetInt32(1);
            customerName     = reader.GetString(2);
            string st        = reader.GetString(3);

            if (st is "completed" or "cancelled")
            {
                return new AutoDispatchResultDto
                {
                    RentalId = rentalId,
                    Success  = false,
                    Message  = $"Cannot dispatch Rental #{rentalId} because its status is already '{st}'."
                };
            }
        }

        // 2. Select optimal available vehicle (use assigned vehicle if available, else pick first available)
        int assignedVehicleId = 0;
        string assignedVehicleName = string.Empty;

        await using (var cmdV = new NpgsqlCommand(@"
            SELECT vehicle_id, brand || ' ' || model || ' (' || plate_no || ')'
            FROM vehicles
            WHERE LOWER(status) IN ('available', 'active')
            ORDER BY CASE WHEN vehicle_id = @vid THEN 0 ELSE 1 END, vehicle_id ASC
            LIMIT 1", conn))
        {
            cmdV.Parameters.AddWithValue("@vid", currentVehicleId);
            await using var readerV = await cmdV.ExecuteReaderAsync();
            if (await readerV.ReadAsync())
            {
                assignedVehicleId   = readerV.GetInt32(0);
                assignedVehicleName = readerV.GetString(1);
            }
            else
            {
                return new AutoDispatchResultDto
                {
                    RentalId = rentalId,
                    Success  = false,
                    Message  = $"Auto-dispatch failed: No available vehicles in fleet."
                };
            }
        }

        // 3. Find top available driver
        int? assignedDriverId = null;
        string? assignedDriverName = null;

        await using (var cmdD = new NpgsqlCommand(@"
            SELECT d.driver_id, d.user_id, u.full_name
            FROM drivers d
            JOIN users u ON u.user_id = d.user_id
            WHERE LOWER(d.status) IN ('active', 'available')
            ORDER BY d.rating_avg DESC, d.total_trips DESC
            LIMIT 1", conn))
        {
            await using var readerD = await cmdD.ExecuteReaderAsync();
            if (await readerD.ReadAsync())
            {
                assignedDriverId   = readerD.GetInt32(1); // user_id for rentals.driver_id FK
                assignedDriverName = readerD.GetString(2);
            }
        }

        // 4. Update Rental status to 'assigned' and update vehicle & driver IDs
        await using (var cmdUpd = new NpgsqlCommand(@"
            UPDATE rentals
            SET vehicle_id = @vid,
                driver_id  = @did,
                status     = 'assigned'
            WHERE rental_id = @rid", conn))
        {
            cmdUpd.Parameters.AddWithValue("@vid", assignedVehicleId);
            cmdUpd.Parameters.AddWithValue("@did", (object?)assignedDriverId ?? DBNull.Value);
            cmdUpd.Parameters.AddWithValue("@rid", rentalId);
            await cmdUpd.ExecuteNonQueryAsync();
        }

        // 5. Update vehicle status to 'rented'
        await using (var cmdVupd = new NpgsqlCommand(
            "UPDATE vehicles SET status = 'rented' WHERE vehicle_id = @vid", conn))
        {
            cmdVupd.Parameters.AddWithValue("@vid", assignedVehicleId);
            await cmdVupd.ExecuteNonQueryAsync();
        }

        // 6. Update driver status to 'on-trip' if assigned
        if (assignedDriverId.HasValue)
        {
            await using var cmdDupd = new NpgsqlCommand(
                "UPDATE drivers SET status = 'on-trip' WHERE user_id = @uid", conn);
            cmdDupd.Parameters.AddWithValue("@uid", assignedDriverId.Value);
            await cmdDupd.ExecuteNonQueryAsync();
        }

        _logger.LogInformation(
            "Auto-Dispatch Success for Rental #{Rid}: Vehicle {Vname}, Driver {Dname}",
            rentalId, assignedVehicleName, assignedDriverName ?? "Self-Drive");

        return new AutoDispatchResultDto
        {
            RentalId     = rentalId,
            VehicleId    = assignedVehicleId,
            VehicleName  = assignedVehicleName,
            DriverId     = assignedDriverId,
            DriverName   = assignedDriverName ?? "Self-Drive (No driver requested)",
            CustomerName = customerName,
            Success      = true,
            Message      = $"Rental #{rentalId} successfully dispatched! Assigned {assignedVehicleName} and Driver {assignedDriverName ?? "Self-Drive"}."
        };
    }
}
