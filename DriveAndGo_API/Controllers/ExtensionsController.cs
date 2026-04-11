using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using DriveAndGo_API.Models;
using System;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExtensionsController : ControllerBase
    {
        private readonly string _connectionString;

        public ExtensionsController(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")!;
        }

        // ══ GET ALL EXTENSIONS — para sa admin ══
        [HttpGet]
        public IActionResult GetExtensions()
        {
            try
            {
                List<Extension> extensions = new List<Extension>();

                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT e.extension_id, e.rental_id,
                           e.added_days, e.added_fee,
                           e.status, e.requested_at,
                           u.full_name AS customer_name,
                           CONCAT(v.brand, ' ', v.model) AS vehicle_name
                    FROM extensions e
                    JOIN rentals r  ON e.rental_id   = r.rental_id
                    JOIN users u    ON r.customer_id  = u.user_id
                    JOIN vehicles v ON r.vehicle_id   = v.vehicle_id
                    ORDER BY e.requested_at DESC", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    extensions.Add(new Extension
                    {
                        ExtensionId = Convert.ToInt32(reader["extension_id"]),
                        RentalId = Convert.ToInt32(reader["rental_id"]),
                        AddedDays = Convert.ToInt32(reader["added_days"]),
                        AddedFee = Convert.ToDecimal(reader["added_fee"]),
                        Status = reader["status"].ToString(),
                        RequestedAt = Convert.ToDateTime(reader["requested_at"]),
                        CustomerName = reader["customer_name"].ToString(),
                        VehicleName = reader["vehicle_name"].ToString()
                    });
                }

                return Ok(extensions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ GET BY RENTAL ID ══
        [HttpGet("rental/{rentalId}")]
        public IActionResult GetByRental(int rentalId)
        {
            try
            {
                List<Extension> extensions = new List<Extension>();

                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT e.extension_id, e.rental_id,
                           e.added_days, e.added_fee,
                           e.status, e.requested_at,
                           u.full_name AS customer_name,
                           CONCAT(v.brand, ' ', v.model) AS vehicle_name
                    FROM extensions e
                    JOIN rentals r  ON e.rental_id   = r.rental_id
                    JOIN users u    ON r.customer_id  = u.user_id
                    JOIN vehicles v ON r.vehicle_id   = v.vehicle_id
                    WHERE e.rental_id = @rental_id
                    ORDER BY e.requested_at DESC", conn);
                cmd.Parameters.AddWithValue("@rental_id", rentalId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    extensions.Add(new Extension
                    {
                        ExtensionId = Convert.ToInt32(reader["extension_id"]),
                        RentalId = Convert.ToInt32(reader["rental_id"]),
                        AddedDays = Convert.ToInt32(reader["added_days"]),
                        AddedFee = Convert.ToDecimal(reader["added_fee"]),
                        Status = reader["status"].ToString(),
                        RequestedAt = Convert.ToDateTime(reader["requested_at"]),
                        CustomerName = reader["customer_name"].ToString(),
                        VehicleName = reader["vehicle_name"].ToString()
                    });
                }

                return Ok(extensions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ POST — customer nag-request ng extension ══
        [HttpPost]
        public IActionResult RequestExtension([FromBody] Extension extension)
        {
            if (extension.RentalId == 0 || extension.AddedDays <= 0)
                return BadRequest(new
                {
                    message =
                    "RentalId and AddedDays are required."
                });

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // Check kung approved ang rental
                var rentalCmd = new MySqlCommand(@"
                    SELECT status, vehicle_id,
                           end_date, total_amount
                    FROM rentals
                    WHERE rental_id = @rental_id", conn);
                rentalCmd.Parameters.AddWithValue("@rental_id",
                    extension.RentalId);

                using var reader = rentalCmd.ExecuteReader();
                if (!reader.Read())
                    return NotFound(new { message = "Rental not found." });

                string rentalStatus = reader["status"].ToString()!;
                int vehicleId = Convert.ToInt32(reader["vehicle_id"]);
                decimal totalAmount = Convert.ToDecimal(reader["total_amount"]);
                reader.Close();

                if (!string.Equals(rentalStatus, "approved", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new
                    {
                        message =
                        "Only approved rentals can be extended."
                    });

                // Kunin ang daily rate ng sasakyan para
                // ma-compute ang added fee
                var vehicleCmd = new MySqlCommand(@"
                    SELECT rate_per_day FROM vehicles
                    WHERE vehicle_id = @vehicle_id", conn);
                vehicleCmd.Parameters.AddWithValue("@vehicle_id", vehicleId);
                decimal dailyRate = Convert.ToDecimal(
                    vehicleCmd.ExecuteScalar());

                // Compute ang added fee
                decimal addedFee = dailyRate * extension.AddedDays;

                // I-save ang extension request
                var insertCmd = new MySqlCommand(@"
                    INSERT INTO extensions
                        (rental_id, added_days,
                         added_fee, status, requested_at)
                    VALUES
                        (@rental_id, @added_days,
                         @added_fee, 'pending', NOW())", conn);

                insertCmd.Parameters.AddWithValue("@rental_id",
                    extension.RentalId);
                insertCmd.Parameters.AddWithValue("@added_days",
                    extension.AddedDays);
                insertCmd.Parameters.AddWithValue("@added_fee", addedFee);

                insertCmd.ExecuteNonQuery();

                var idCmd = new MySqlCommand(
                    "SELECT LAST_INSERT_ID()", conn);
                int newId = Convert.ToInt32(idCmd.ExecuteScalar());

                return Ok(new
                {
                    message = "Extension request submitted. Waiting for admin approval.",
                    extension_id = newId,
                    added_days = extension.AddedDays,
                    added_fee = addedFee
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ PATCH APPROVE — admin nag-approve ng extension ══
        [HttpPatch("{id}/approve")]
        public IActionResult ApproveExtension(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // Kunin ang extension details
                var checkCmd = new MySqlCommand(@"
                    SELECT e.status, e.rental_id, e.added_days, e.added_fee
                    FROM extensions e
                    WHERE e.extension_id = @id", conn);
                checkCmd.Parameters.AddWithValue("@id", id);

                using var reader = checkCmd.ExecuteReader();
                if (!reader.Read())
                    return NotFound(new { message = "Extension not found." });

                string currentStatus = reader["status"].ToString()!;
                int rentalId = Convert.ToInt32(reader["rental_id"]);
                int addedDays = Convert.ToInt32(reader["added_days"]);
                decimal addedFee = Convert.ToDecimal(reader["added_fee"]);
                reader.Close();

                if (!string.Equals(currentStatus, "pending", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new
                    {
                        message =
                        $"Cannot approve — status is already '{currentStatus}'."
                    });

                // I-approve ang extension
                var approveCmd = new MySqlCommand(@"
                    UPDATE extensions
                    SET status = 'approved'
                    WHERE extension_id = @id", conn);
                approveCmd.Parameters.AddWithValue("@id", id);
                approveCmd.ExecuteNonQuery();

                // I-update ang end_date at total_amount ng rental
                var rentalCmd = new MySqlCommand(@"
                    UPDATE rentals
                    SET end_date     = DATE_ADD(end_date,
                                       INTERVAL @days DAY),
                        total_amount = total_amount + @fee
                    WHERE rental_id  = @rental_id", conn);
                rentalCmd.Parameters.AddWithValue("@days", addedDays);
                rentalCmd.Parameters.AddWithValue("@fee", addedFee);
                rentalCmd.Parameters.AddWithValue("@rental_id", rentalId);
                rentalCmd.ExecuteNonQuery();

                return Ok(new
                {
                    message = $"Extension approved! Rental extended by {addedDays} day(s).",
                    extension_id = id,
                    rental_id = rentalId,
                    added_days = addedDays,
                    added_fee = addedFee
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ PATCH REJECT — admin nag-reject ng extension ══
        [HttpPatch("{id}/reject")]
        public IActionResult RejectExtension(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var checkCmd = new MySqlCommand(@"
                    SELECT status FROM extensions
                    WHERE extension_id = @id", conn);
                checkCmd.Parameters.AddWithValue("@id", id);
                var status = checkCmd.ExecuteScalar()?.ToString();

                if (status == null)
                    return NotFound(new { message = "Extension not found." });

                if (!string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new
                    {
                        message =
                        $"Cannot reject — status is already '{status}'."
                    });

                var rejectCmd = new MySqlCommand(@"
                    UPDATE extensions
                    SET status = 'rejected'
                    WHERE extension_id = @id", conn);
                rejectCmd.Parameters.AddWithValue("@id", id);
                rejectCmd.ExecuteNonQuery();

                return Ok(new
                {
                    message = "Extension request rejected.",
                    extension_id = id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }
    }
}
