using Microsoft.AspNetCore.SignalR;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using DriveAndGo_API.Hubs;

namespace DriveAndGo_API.Services
{
    public class RentalComplianceWorker : BackgroundService
    {
        private readonly NpgsqlDataSource _ds;
        private readonly IHubContext<AdminHub> _hubContext;

        public RentalComplianceWorker(NpgsqlDataSource ds, IHubContext<AdminHub> hubContext)
        {
            _ds = ds;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await EvaluateOverdueRentalsAsync();
                    
                    // Check compliance status every 5 minutes
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Safe exit on application shutdown or task cancellation
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("RentalComplianceWorker error: " + ex.Message);
                    
                    // Brief delay before retry to avoid spamming on DB connectivity errors
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task EvaluateOverdueRentalsAsync()
        {
            await using var conn = await _ds.OpenConnectionAsync();

            // Fetch active rentals that have passed their return date
            var overdueRentals = new List<dynamic>();

            string query = @"
                SELECT r.rental_id, r.end_date, r.total_amount, r.penalty_fee, v.vehicle_id, v.brand, v.model, v.plate_no, r.start_date
                FROM rentals r
                JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                WHERE LOWER(r.status) IN ('approved', 'active', 'in-use') 
                  AND r.end_date < NOW()";

            await using (var cmd = new NpgsqlCommand(query, conn))
            {
                cmd.CommandTimeout = 5;
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var endDate = reader.GetDateTime(1);
                    var startDate = reader.IsDBNull(8) ? endDate.AddHours(-24) : reader.GetDateTime(8);
                    var totalAmt = reader.GetDecimal(2);
                    double durationHours = Math.Max((endDate - startDate).TotalHours, 1.0);
                    decimal hourlyRate = Math.Round(totalAmt / (decimal)durationHours, 2);
                    if (hourlyRate <= 0) hourlyRate = 50.00m;

                    overdueRentals.Add(new {
                        RentalId = reader.GetInt32(0),
                        EndDate = endDate,
                        TotalAmount = totalAmt,
                        PenaltyFee = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                        VehicleId = reader.GetInt32(4),
                        VehicleName = $"{reader.GetString(5)} {reader.GetString(6)} ({reader.GetString(7)})",
                        HourlyRate = hourlyRate
                    });
                }
            }

            foreach (var rental in overdueRentals)
            {
                // Calculate dynamic late hours penalty
                double lateHours = (DateTime.UtcNow - rental.EndDate).TotalHours;
                if (lateHours < 0) continue;

                // 1.5x the base hourly rate penalty multiplier rule
                decimal penaltyMultiplier = 1.5m;
                decimal newPenaltyFee = Math.Round((decimal)lateHours * rental.HourlyRate * penaltyMultiplier, 2);

                // Check if penalty has changed to prevent infinite DB overrides
                if (newPenaltyFee > rental.PenaltyFee)
                {
                    decimal penaltyIncrement = newPenaltyFee - rental.PenaltyFee;

                    await using (var updateCmd = new NpgsqlCommand(@"
                        UPDATE rentals 
                        SET status = 'OVERDUE', 
                            penalty_fee = @penalty, 
                            total_amount = total_amount + @increment
                        WHERE rental_id = @id", conn))
                    {
                        updateCmd.Parameters.AddWithValue("@penalty", newPenaltyFee);
                        updateCmd.Parameters.AddWithValue("@increment", penaltyIncrement);
                        updateCmd.Parameters.AddWithValue("@id", rental.RentalId);
                        await updateCmd.ExecuteNonQueryAsync();
                    }

                    // Fire real-time notification alert via SignalR to flush alert globally
                    await _hubContext.Clients.All.SendAsync("ReceiveNotification", new
                    {
                        notifId = new Random().Next(100000, 999999),
                        userId = 1, // Admin User
                        title = "⚠️ VEHICLE OVERDUE EXCEPTION",
                        body = $"{rental.VehicleName} is late by {lateHours:F1} hours. Status changed to OVERDUE. Penalty: ₱{newPenaltyFee:N2}.",
                        type = "rental-overdue",
                        isRead = false,
                        sentAt = DateTime.UtcNow
                    });
                }
            }
        }
    }
}
