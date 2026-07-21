using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using System;
using System.IO;
using System.Threading.Tasks;
using DriveAndGo_API.Hubs;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClaimsController : ControllerBase
    {
        private readonly NpgsqlDataSource _ds;
        private readonly IHubContext<AdminHub> _hubContext;

        public ClaimsController(NpgsqlDataSource ds, IHubContext<AdminHub> hubContext)
        {
            _ds = ds;
            _hubContext = hubContext;
        }

        public class ClaimSubmission
        {
            public int RentalId { get; set; }
            public string DamageSeverity { get; set; } = "Low"; // "Low", "Medium", "Critical"
            public string Description { get; set; } = string.Empty;
            public string? PhotoUrl { get; set; }
        }

        // POST /api/claims/submit
        [HttpPost("submit")]
        public async Task<IActionResult> SubmitClaim([FromBody] ClaimSubmission submission)
        {
            if (submission.RentalId == 0 || string.IsNullOrEmpty(submission.Description))
            {
                return BadRequest(new { Message = "RentalId and Description are required." });
            }

            try
            {
                await using var conn = await _ds.OpenConnectionAsync();

                // 1. Fetch rental details and vehicle name/plate
                string vehicleName = "Unknown Vehicle";
                decimal baseRentalAmount = 0;
                int vehicleId = 0;

                string query = @"
                    SELECT r.total_amount, v.vehicle_id, CONCAT(v.brand, ' ', v.model, ' (', v.plate_no, ')')
                    FROM rentals r
                    JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                    WHERE r.rental_id = @id";

                await using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@id", submission.RentalId);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    if (reader.Read())
                    {
                        baseRentalAmount = reader.GetDecimal(0);
                        vehicleId = reader.GetInt32(1);
                        vehicleName = reader.GetString(2);
                    }
                }

                if (vehicleId == 0)
                {
                    return NotFound(new { Message = "Rental or associated vehicle not found." });
                }

                // 2. Compute liability cost based on severity level
                decimal liabilityCost = 5000.00m; // Default Low
                string severity = submission.DamageSeverity.Trim().ToLower();

                if (severity == "medium")
                {
                    liabilityCost = 15000.00m;
                }
                else if (severity == "critical")
                {
                    liabilityCost = 50000.00m;
                }

                // 3. Save damage claim to database
                int claimId = 0;
                string photoUrl = submission.PhotoUrl ?? "https://images.unsplash.com/photo-1597481499750-3e6b22637e12?auto=format&fit=crop&q=80&w=600"; // placeholder

                await using (var cmd = new NpgsqlCommand(@"
                    INSERT INTO damage_claims (rental_id, damage_severity, description, photo_url, liability_cost)
                    VALUES (@rid, @severity, @desc, @photo, @cost)
                    RETURNING claim_id", conn))
                {
                    cmd.Parameters.AddWithValue("@rid", submission.RentalId);
                    cmd.Parameters.AddWithValue("@severity", submission.DamageSeverity);
                    cmd.Parameters.AddWithValue("@desc", submission.Description);
                    cmd.Parameters.AddWithValue("@photo", photoUrl);
                    cmd.Parameters.AddWithValue("@cost", liabilityCost);

                    claimId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                // 4. Update the rental overall amount with the liability cost
                await using (var cmd = new NpgsqlCommand(@"
                    UPDATE rentals
                    SET total_amount = total_amount + @cost
                    WHERE rental_id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@cost", liabilityCost);
                    cmd.Parameters.AddWithValue("@id", submission.RentalId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // 5. Instantly broadcast filed claim to the AdminActionFeed via SignalR
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", new
                {
                    notifId = new Random().Next(100000, 999999),
                    userId = 1, // Admin User
                    title = "🚨 DAMAGE CLAIM FILED",
                    body = $"NEW CLAIM: Incident reported for {vehicleName}. Severity: {submission.DamageSeverity}. Estimated Liability: ₱{liabilityCost:N2}.",
                    type = "damage-claim",
                    isRead = false,
                    sentAt = DateTime.UtcNow
                });

                return Ok(new
                {
                    success = true,
                    message = "Damage claim submitted successfully.",
                    claimId,
                    computedLiability = liabilityCost,
                    totalAdjustedAmount = baseRentalAmount + liabilityCost
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to submit damage claim: " + ex.Message });
            }
        }
    }
}
