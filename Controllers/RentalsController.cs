using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using DriveAndGo_API.Models;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RentalsController : ControllerBase
    {
        private readonly string _connectionString;

        public RentalsController(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")!;
        }

        // ══ GET ALL — para sa Admin dashboard ══
        [HttpGet]
        public IActionResult GetRentals()
        {
            try
            {
                List<Rental> rentals = new List<Rental>();

                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // JOIN na para makuha din ang customer name at vehicle info
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
                    rentals.Add(new Rental
                    {
                        RentalId = Convert.ToInt32(reader["rental_id"]),
                        CustomerId = Convert.ToInt32(reader["customer_id"]),
                        VehicleId = Convert.ToInt32(reader["vehicle_id"]),
                        DriverId = reader["driver_id"] != DBNull.Value
                                        ? Convert.ToInt32(reader["driver_id"])
                                        : null,
                        StartDate = Convert.ToDateTime(reader["start_date"]),
                        EndDate = Convert.ToDateTime(reader["end_date"]),
                        Destination = reader["destination"].ToString(),
                        Status = reader["status"].ToString(),
                        TotalAmount = Convert.ToDecimal(reader["total_amount"]),
                        PaymentMethod = reader["payment_method"].ToString(),
                        PaymentStatus = reader["payment_status"].ToString(),
                        CustomerName = reader["customer_name"].ToString(),
                        VehicleName = reader["vehicle_name"].ToString()
                    });
                }

                return Ok(rentals);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ GET BY ID — para sa single rental details ══
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

                var rental = new Rental
                {
                    RentalId = Convert.ToInt32(reader["rental_id"]),
                    CustomerId = Convert.ToInt32(reader["customer_id"]),
                    VehicleId = Convert.ToInt32(reader["vehicle_id"]),
                    DriverId = reader["driver_id"] != DBNull.Value
                                    ? Convert.ToInt32(reader["driver_id"])
                                    : null,
                    StartDate = Convert.ToDateTime(reader["start_date"]),
                    EndDate = Convert.ToDateTime(reader["end_date"]),
                    Destination = reader["destination"].ToString(),
                    Status = reader["status"].ToString(),
                    TotalAmount = Convert.ToDecimal(reader["total_amount"]),
                    PaymentMethod = reader["payment_method"].ToString(),
                    PaymentStatus = reader["payment_status"].ToString(),
                    CustomerName = reader["customer_name"].ToString(),
                    VehicleName = reader["vehicle_name"].ToString()
                };

                return Ok(rental);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ GET BY CUSTOMER — para sa mobile app (my bookings) ══
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
                    rentals.Add(new Rental
                    {
                        RentalId = Convert.ToInt32(reader["rental_id"]),
                        CustomerId = Convert.ToInt32(reader["customer_id"]),
                        VehicleId = Convert.ToInt32(reader["vehicle_id"]),
                        DriverId = reader["driver_id"] != DBNull.Value
                                        ? Convert.ToInt32(reader["driver_id"])
                                        : null,
                        StartDate = Convert.ToDateTime(reader["start_date"]),
                        EndDate = Convert.ToDateTime(reader["end_date"]),
                        Destination = reader["destination"].ToString(),
                        Status = reader["status"].ToString(),
                        TotalAmount = Convert.ToDecimal(reader["total_amount"]),
                        PaymentMethod = reader["payment_method"].ToString(),
                        PaymentStatus = reader["payment_status"].ToString(),
                        CustomerName = reader["customer_name"].ToString(),
                        VehicleName = reader["vehicle_name"].ToString()
                    });
                }

                return Ok(rentals);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ POST — bagong booking mula sa mobile app ══
        [HttpPost]
        public IActionResult AddRental([FromBody] Rental rental)
        {
            // Validation
            if (rental.CustomerId == 0 || rental.VehicleId == 0)
                return BadRequest(new
                {
                    message =
                    "Customer ID at Vehicle ID ay required."
                });

            if (rental.StartDate >= rental.EndDate)
                return BadRequest(new
                {
                    message =
                    "End date dapat later kaysa start date."
                });

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // Check kung available pa ang sasakyan
                var checkCmd = new MySqlCommand(@"
                    SELECT status FROM vehicles
                    WHERE vehicle_id = @vehicle_id", conn);
                checkCmd.Parameters.AddWithValue("@vehicle_id", rental.VehicleId);
                var vehicleStatus = checkCmd.ExecuteScalar()?.ToString();

                if (vehicleStatus != "Available")
                    return Conflict(new
                    {
                        message =
                        "Sorry, hindi na available ang sasakyang ito."
                    });

                // I-save ang rental
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

                insertCmd.Parameters.AddWithValue("@customer_id",
                    rental.CustomerId);
                insertCmd.Parameters.AddWithValue("@vehicle_id",
                    rental.VehicleId);
                insertCmd.Parameters.AddWithValue("@driver_id",
                    rental.DriverId.HasValue
                    ? (object)rental.DriverId.Value
                    : DBNull.Value);
                insertCmd.Parameters.AddWithValue("@start_date",
                    rental.StartDate);
                insertCmd.Parameters.AddWithValue("@end_date",
                    rental.EndDate);
                insertCmd.Parameters.AddWithValue("@destination",
                    rental.Destination ?? "");
                insertCmd.Parameters.AddWithValue("@total_amount",
                    rental.TotalAmount);
                insertCmd.Parameters.AddWithValue("@payment_method",
                    rental.PaymentMethod ?? "cash");

                insertCmd.ExecuteNonQuery();

                // Kunin ang bagong rental_id
                var idCmd = new MySqlCommand("SELECT LAST_INSERT_ID()", conn);
                int newId = Convert.ToInt32(idCmd.ExecuteScalar());

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

        // ══ PATCH APPROVE — admin nag-approve ng booking ══
        [HttpPatch("{id}/approve")]
        public IActionResult ApproveRental(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // I-check muna kung existing at pending pa
                var checkCmd = new MySqlCommand(@"
                    SELECT status, vehicle_id FROM rentals
                    WHERE rental_id = @id", conn);
                checkCmd.Parameters.AddWithValue("@id", id);

                using var reader = checkCmd.ExecuteReader();
                if (!reader.Read())
                    return NotFound(new { message = "Rental not found." });

                string currentStatus = reader["status"].ToString()!;
                int vehicleId = Convert.ToInt32(reader["vehicle_id"]);
                reader.Close();

                if (currentStatus != "pending")
                    return BadRequest(new
                    {
                        message =
                        $"Hindi ma-approve — status is already '{currentStatus}'."
                    });

                // I-approve ang rental
                var approveCmd = new MySqlCommand(@"
                    UPDATE rentals
                    SET status = 'approved'
                    WHERE rental_id = @id", conn);
                approveCmd.Parameters.AddWithValue("@id", id);
                approveCmd.ExecuteNonQuery();

                // I-update ang vehicle status → Rented
                var vehicleCmd = new MySqlCommand(@"
                    UPDATE vehicles
                    SET status = 'Rented'
                    WHERE vehicle_id = @vehicle_id", conn);
                vehicleCmd.Parameters.AddWithValue("@vehicle_id", vehicleId);
                vehicleCmd.ExecuteNonQuery();

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

        // ══ PATCH REJECT — admin nag-reject ng booking ══
        [HttpPatch("{id}/reject")]
        public IActionResult RejectRental(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var checkCmd = new MySqlCommand(
                    "SELECT status FROM rentals WHERE rental_id = @id", conn);
                checkCmd.Parameters.AddWithValue("@id", id);
                var status = checkCmd.ExecuteScalar()?.ToString();

                if (status == null)
                    return NotFound(new { message = "Rental not found." });

                if (status != "pending")
                    return BadRequest(new
                    {
                        message =
                        $"Hindi ma-reject — status is already '{status}'."
                    });

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

        // ══ PATCH COMPLETE — driver/admin nag-mark ng returned ══
        [HttpPatch("{id}/complete")]
        public IActionResult CompleteRental(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var checkCmd = new MySqlCommand(@"
                    SELECT status, vehicle_id FROM rentals
                    WHERE rental_id = @id", conn);
                checkCmd.Parameters.AddWithValue("@id", id);

                using var reader = checkCmd.ExecuteReader();
                if (!reader.Read())
                    return NotFound(new { message = "Rental not found." });

                string currentStatus = reader["status"].ToString()!;
                int vehicleId = Convert.ToInt32(reader["vehicle_id"]);
                reader.Close();

                if (currentStatus != "approved")
                    return BadRequest(new
                    {
                        message =
                        "Approved rentals lang ang pwedeng i-complete."
                    });

                // I-complete ang rental
                var completeCmd = new MySqlCommand(@"
                    UPDATE rentals
                    SET status = 'completed'
                    WHERE rental_id = @id", conn);
                completeCmd.Parameters.AddWithValue("@id", id);
                completeCmd.ExecuteNonQuery();

                // I-free ulit ang sasakyan
                var vehicleCmd = new MySqlCommand(@"
                    UPDATE vehicles
                    SET status = 'Available'
                    WHERE vehicle_id = @vehicle_id", conn);
                vehicleCmd.Parameters.AddWithValue("@vehicle_id", vehicleId);
                vehicleCmd.ExecuteNonQuery();

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
    }
}