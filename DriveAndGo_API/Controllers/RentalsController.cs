using DriveAndGo_API.Models;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Globalization;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RentalsController : ControllerBase
    {
        private readonly string _connectionString;

        public RentalsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        [HttpGet]
        public IActionResult GetRentals()
        {
            try
            {
                List<Rental> rentals = new List<Rental>();

                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT r.rental_id, r.customer_id, r.vehicle_id,
                           r.driver_id, r.start_date, r.end_date,
                           r.destination, r.status, r.total_amount,
                           r.payment_method, r.payment_status,
                           u.full_name AS customer_name,
                           CONCAT(v.brand, ' ', v.model) AS vehicle_name
                    FROM rentals r
                    JOIN users u ON r.customer_id = u.user_id
                    JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                    ORDER BY r.created_at DESC", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    rentals.Add(MapRental(reader));
                }

                return Ok(rentals);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        [HttpGet("{id}")]
        public IActionResult GetRentalById(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT r.*, u.full_name AS customer_name,
                           CONCAT(v.brand, ' ', v.model) AS vehicle_name
                    FROM rentals r
                    JOIN users u ON r.customer_id = u.user_id
                    JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                    WHERE r.rental_id = @id
                    LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return NotFound(new { message = "Rental not found." });

                return Ok(MapRental(reader));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        [HttpGet("customer/{customerId}")]
        public IActionResult GetRentalsByCustomer(int customerId)
        {
            try
            {
                List<Rental> rentals = new List<Rental>();

                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT r.*, u.full_name AS customer_name,
                           CONCAT(v.brand, ' ', v.model) AS vehicle_name
                    FROM rentals r
                    JOIN users u ON r.customer_id = u.user_id
                    JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                    WHERE r.customer_id = @customer_id
                    ORDER BY r.created_at DESC", conn);
                cmd.Parameters.AddWithValue("@customer_id", customerId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    rentals.Add(MapRental(reader));
                }

                return Ok(rentals);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult AddRental([FromBody] Rental rental)
        {
            if (rental.CustomerId == 0 || rental.VehicleId == 0)
            {
                return BadRequest(new
                {
                    message = "Customer ID at Vehicle ID ay required."
                });
            }

            if (!rental.EndDate.HasValue || rental.StartDate >= rental.EndDate.Value)
            {
                return BadRequest(new
                {
                    message = "End date dapat later kaysa start date."
                });
            }

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var checkCmd = new MySqlCommand(@"
                    SELECT LOWER(COALESCE(status, ''))
                    FROM vehicles
                    WHERE vehicle_id = @vehicle_id", conn);
                checkCmd.Parameters.AddWithValue("@vehicle_id", rental.VehicleId);
                var vehicleStatus = checkCmd.ExecuteScalar()?.ToString();

                if (string.IsNullOrWhiteSpace(vehicleStatus))
                    return NotFound(new { message = "Vehicle not found." });

                if (!string.Equals(vehicleStatus, "available", StringComparison.OrdinalIgnoreCase))
                {
                    return Conflict(new
                    {
                        message = "Sorry, hindi na available ang sasakyang ito."
                    });
                }

                var insertCmd = new MySqlCommand(@"
                    INSERT INTO rentals
                        (customer_id, vehicle_id, driver_id,
                         start_date, end_date, destination,
                         status, total_amount,
                         payment_method, payment_status, created_at)
                    VALUES
                        (@customer_id, @vehicle_id, @driver_id,
                         @start_date, @end_date, @destination,
                         'pending', @total_amount,
                         @payment_method, 'unpaid', NOW())", conn);

                insertCmd.Parameters.AddWithValue("@customer_id", rental.CustomerId);
                insertCmd.Parameters.AddWithValue("@vehicle_id", rental.VehicleId);
                insertCmd.Parameters.AddWithValue("@driver_id", rental.DriverId.HasValue ? rental.DriverId.Value : DBNull.Value);
                insertCmd.Parameters.AddWithValue("@start_date", rental.StartDate);
                insertCmd.Parameters.AddWithValue("@end_date", rental.EndDate.Value);
                insertCmd.Parameters.AddWithValue("@destination", string.IsNullOrWhiteSpace(rental.Destination) ? DBNull.Value : rental.Destination);
                insertCmd.Parameters.AddWithValue("@total_amount", rental.TotalAmount);
                insertCmd.Parameters.AddWithValue("@payment_method", NormalizeLower(rental.PaymentMethod, "cash"));

                insertCmd.ExecuteNonQuery();

                var idCmd = new MySqlCommand("SELECT LAST_INSERT_ID()", conn);
                int newId = Convert.ToInt32(idCmd.ExecuteScalar(), CultureInfo.InvariantCulture);

                return Ok(new
                {
                    message = "Booking successful! Hintayin ang approval ni Admin.",
                    rental_id = newId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        [HttpPatch("{id}/approve")]
        public IActionResult ApproveRental(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();
                using var tx = conn.BeginTransaction();

                var checkCmd = new MySqlCommand(@"
                    SELECT LOWER(COALESCE(r.status, '')) AS rental_status,
                           r.vehicle_id,
                           LOWER(COALESCE(v.status, '')) AS vehicle_status
                    FROM rentals r
                    JOIN vehicles v ON v.vehicle_id = r.vehicle_id
                    WHERE r.rental_id = @id
                    LIMIT 1", conn, tx);
                checkCmd.Parameters.AddWithValue("@id", id);

                using var reader = checkCmd.ExecuteReader();
                if (!reader.Read())
                    return NotFound(new { message = "Rental not found." });

                string currentStatus = reader["rental_status"]?.ToString() ?? "";
                string vehicleStatus = reader["vehicle_status"]?.ToString() ?? "";
                int vehicleId = Convert.ToInt32(reader["vehicle_id"], CultureInfo.InvariantCulture);
                reader.Close();

                if (!string.Equals(currentStatus, "pending", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        message = $"Hindi ma-approve - status is already '{currentStatus}'."
                    });
                }

                if (!string.Equals(vehicleStatus, "available", StringComparison.OrdinalIgnoreCase))
                {
                    return Conflict(new
                    {
                        message = $"Hindi ma-approve - vehicle is already '{vehicleStatus}'."
                    });
                }

                var approveCmd = new MySqlCommand(@"
                    UPDATE rentals
                    SET status = 'approved'
                    WHERE rental_id = @id", conn, tx);
                approveCmd.Parameters.AddWithValue("@id", id);
                approveCmd.ExecuteNonQuery();

                var vehicleCmd = new MySqlCommand(@"
                    UPDATE vehicles
                    SET status = 'Rented'
                    WHERE vehicle_id = @vehicle_id", conn, tx);
                vehicleCmd.Parameters.AddWithValue("@vehicle_id", vehicleId);
                vehicleCmd.ExecuteNonQuery();

                tx.Commit();

                return Ok(new
                {
                    message = "Rental approved! Vehicle status updated to Rented.",
                    rental_id = id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        [HttpPatch("{id}/reject")]
        public IActionResult RejectRental(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var checkCmd = new MySqlCommand(
                    "SELECT LOWER(COALESCE(status, '')) FROM rentals WHERE rental_id = @id", conn);
                checkCmd.Parameters.AddWithValue("@id", id);
                var status = checkCmd.ExecuteScalar()?.ToString();

                if (string.IsNullOrWhiteSpace(status))
                    return NotFound(new { message = "Rental not found." });

                if (!string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        message = $"Hindi ma-reject - status is already '{status}'."
                    });
                }

                var rejectCmd = new MySqlCommand(@"
                    UPDATE rentals
                    SET status = 'rejected'
                    WHERE rental_id = @id", conn);
                rejectCmd.Parameters.AddWithValue("@id", id);
                rejectCmd.ExecuteNonQuery();

                return Ok(new
                {
                    message = "Rental rejected.",
                    rental_id = id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        [HttpPatch("{id}/complete")]
        public IActionResult CompleteRental(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();
                using var tx = conn.BeginTransaction();

                var checkCmd = new MySqlCommand(@"
                    SELECT LOWER(COALESCE(status, '')) AS rental_status, vehicle_id
                    FROM rentals
                    WHERE rental_id = @id
                    LIMIT 1", conn, tx);
                checkCmd.Parameters.AddWithValue("@id", id);

                using var reader = checkCmd.ExecuteReader();
                if (!reader.Read())
                    return NotFound(new { message = "Rental not found." });

                string currentStatus = reader["rental_status"]?.ToString() ?? "";
                int vehicleId = Convert.ToInt32(reader["vehicle_id"], CultureInfo.InvariantCulture);
                reader.Close();

                if (!string.Equals(currentStatus, "approved", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(currentStatus, "in-use", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        message = "Approved rentals lang ang pwedeng i-complete."
                    });
                }

                var completeCmd = new MySqlCommand(@"
                    UPDATE rentals
                    SET status = 'completed'
                    WHERE rental_id = @id", conn, tx);
                completeCmd.Parameters.AddWithValue("@id", id);
                completeCmd.ExecuteNonQuery();

                var vehicleCmd = new MySqlCommand(@"
                    UPDATE vehicles
                    SET status = 'Available'
                    WHERE vehicle_id = @vehicle_id", conn, tx);
                vehicleCmd.Parameters.AddWithValue("@vehicle_id", vehicleId);
                vehicleCmd.ExecuteNonQuery();

                tx.Commit();

                return Ok(new
                {
                    message = "Rental completed! Sasakyan ay Available na ulit.",
                    rental_id = id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        private static Rental MapRental(MySqlDataReader reader)
        {
            return new Rental
            {
                RentalId = Convert.ToInt32(reader["rental_id"], CultureInfo.InvariantCulture),
                CustomerId = Convert.ToInt32(reader["customer_id"], CultureInfo.InvariantCulture),
                VehicleId = Convert.ToInt32(reader["vehicle_id"], CultureInfo.InvariantCulture),
                DriverId = reader["driver_id"] != DBNull.Value
                    ? Convert.ToInt32(reader["driver_id"], CultureInfo.InvariantCulture)
                    : null,
                StartDate = Convert.ToDateTime(reader["start_date"], CultureInfo.InvariantCulture),
                EndDate = reader["end_date"] != DBNull.Value
                    ? Convert.ToDateTime(reader["end_date"], CultureInfo.InvariantCulture)
                    : null,
                Destination = reader["destination"] == DBNull.Value ? null : reader["destination"].ToString(),
                Status = reader["status"]?.ToString() ?? "pending",
                TotalAmount = reader["total_amount"] == DBNull.Value
                    ? 0
                    : Convert.ToDecimal(reader["total_amount"], CultureInfo.InvariantCulture),
                PaymentMethod = reader["payment_method"]?.ToString() ?? "cash",
                PaymentStatus = reader["payment_status"]?.ToString() ?? "unpaid",
                CustomerName = reader["customer_name"] == DBNull.Value ? null : reader["customer_name"].ToString(),
                VehicleName = reader["vehicle_name"] == DBNull.Value ? null : reader["vehicle_name"].ToString()
            };
        }

        private static string NormalizeLower(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim().ToLowerInvariant();
        }
    }
}
