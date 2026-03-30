using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using DriveAndGo_API.Models;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DriversController : ControllerBase
    {
        private readonly string _connectionString;

        public DriversController(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")!;
        }

        // ══ GET ALL DRIVERS ══
        [HttpGet]
        public IActionResult GetDrivers()
        {
            try
            {
                List<Driver> drivers = new List<Driver>();

                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // JOIN sa users para makuha ang name, email, phone
                var cmd = new MySqlCommand(@"
                    SELECT d.driver_id, d.user_id, d.license_no,
                           d.status, d.rating_avg, d.total_trips,
                           u.full_name, u.email, u.phone
                    FROM drivers d
                    JOIN users u ON d.user_id = u.user_id
                    ORDER BY u.full_name ASC", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    drivers.Add(new Driver
                    {
                        DriverId = Convert.ToInt32(reader["driver_id"]),
                        UserId = Convert.ToInt32(reader["user_id"]),
                        LicenseNo = reader["license_no"].ToString(),
                        Status = reader["status"].ToString(),
                        RatingAvg = Convert.ToDecimal(reader["rating_avg"]),
                        TotalTrips = Convert.ToInt32(reader["total_trips"]),
                        FullName = reader["full_name"].ToString(),
                        Email = reader["email"].ToString(),
                        Phone = reader["phone"].ToString()
                    });
                }

                return Ok(drivers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ GET AVAILABLE DRIVERS ONLY — para sa mobile app ══
        [HttpGet("available")]
        public IActionResult GetAvailableDrivers()
        {
            try
            {
                List<Driver> drivers = new List<Driver>();

                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT d.driver_id, d.user_id, d.license_no,
                           d.status, d.rating_avg, d.total_trips,
                           u.full_name, u.email, u.phone
                    FROM drivers d
                    JOIN users u ON d.user_id = u.user_id
                    WHERE d.status = 'available'
                    ORDER BY d.rating_avg DESC", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    drivers.Add(new Driver
                    {
                        DriverId = Convert.ToInt32(reader["driver_id"]),
                        UserId = Convert.ToInt32(reader["user_id"]),
                        LicenseNo = reader["license_no"].ToString(),
                        Status = reader["status"].ToString(),
                        RatingAvg = Convert.ToDecimal(reader["rating_avg"]),
                        TotalTrips = Convert.ToInt32(reader["total_trips"]),
                        FullName = reader["full_name"].ToString(),
                        Email = reader["email"].ToString(),
                        Phone = reader["phone"].ToString()
                    });
                }

                return Ok(drivers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ GET BY ID ══
        [HttpGet("{id}")]
        public IActionResult GetDriverById(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT d.driver_id, d.user_id, d.license_no,
                           d.status, d.rating_avg, d.total_trips,
                           u.full_name, u.email, u.phone
                    FROM drivers d
                    JOIN users u ON d.user_id = u.user_id
                    WHERE d.driver_id = @id
                    LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return NotFound(new { message = "Driver not found." });

                var driver = new Driver
                {
                    DriverId = Convert.ToInt32(reader["driver_id"]),
                    UserId = Convert.ToInt32(reader["user_id"]),
                    LicenseNo = reader["license_no"].ToString(),
                    Status = reader["status"].ToString(),
                    RatingAvg = Convert.ToDecimal(reader["rating_avg"]),
                    TotalTrips = Convert.ToInt32(reader["total_trips"]),
                    FullName = reader["full_name"].ToString(),
                    Email = reader["email"].ToString(),
                    Phone = reader["phone"].ToString()
                };

                return Ok(driver);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ POST — mag-add ng bagong driver ══
        [HttpPost]
        public IActionResult AddDriver([FromBody] Driver driver)
        {
            if (driver.UserId == 0 ||
                string.IsNullOrWhiteSpace(driver.LicenseNo))
                return BadRequest(new
                {
                    message =
                    "UserId at LicenseNo ay required."
                });

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // Check kung existing na ang user_id bilang driver
                var checkCmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM drivers WHERE user_id = @user_id",
                    conn);
                checkCmd.Parameters.AddWithValue("@user_id", driver.UserId);
                var count = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (count > 0)
                    return Conflict(new
                    {
                        message =
                        "Driver already exists para sa user na ito."
                    });

                // Check kung existing ang user
                var userCmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM users WHERE user_id = @user_id",
                    conn);
                userCmd.Parameters.AddWithValue("@user_id", driver.UserId);
                var userCount = Convert.ToInt32(userCmd.ExecuteScalar());

                if (userCount == 0)
                    return NotFound(new
                    {
                        message =
                        "User not found. Mag-register muna ang driver."
                    });

                // I-save ang driver record
                var insertCmd = new MySqlCommand(@"
                    INSERT INTO drivers
                        (user_id, license_no, status,
                         rating_avg, total_trips)
                    VALUES
                        (@user_id, @license_no, 'available', 0.0, 0)",
                    conn);

                insertCmd.Parameters.AddWithValue("@user_id",
                    driver.UserId);
                insertCmd.Parameters.AddWithValue("@license_no",
                    driver.LicenseNo);

                insertCmd.ExecuteNonQuery();

                // I-update ang role ng user sa 'driver'
                var roleCmd = new MySqlCommand(@"
                    UPDATE users SET role = 'driver'
                    WHERE user_id = @user_id", conn);
                roleCmd.Parameters.AddWithValue("@user_id", driver.UserId);
                roleCmd.ExecuteNonQuery();

                var idCmd = new MySqlCommand(
                    "SELECT LAST_INSERT_ID()", conn);
                int newId = Convert.ToInt32(idCmd.ExecuteScalar());

                return Ok(new
                {
                    message = "Driver added successfully!",
                    driver_id = newId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ PATCH STATUS — admin nag-update ng driver status ══
        [HttpPatch("{id}/status")]
        public IActionResult UpdateStatus(int id,
            [FromBody] UpdateStatusRequest request)
        {
            var validStatuses = new[] {
                "available", "on-trip", "off-duty" };

            if (!validStatuses.Contains(request.Status))
                return BadRequest(new
                {
                    message =
                    "Valid status: available, on-trip, off-duty"
                });

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var checkCmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM drivers WHERE driver_id = @id",
                    conn);
                checkCmd.Parameters.AddWithValue("@id", id);
                var exists = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (exists == 0)
                    return NotFound(new { message = "Driver not found." });

                var updateCmd = new MySqlCommand(@"
                    UPDATE drivers SET status = @status
                    WHERE driver_id = @id", conn);
                updateCmd.Parameters.AddWithValue("@status", request.Status);
                updateCmd.Parameters.AddWithValue("@id", id);
                updateCmd.ExecuteNonQuery();

                return Ok(new
                {
                    message = $"Driver status updated to '{request.Status}'.",
                    driver_id = id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ PUT — i-update ang driver info ══
        [HttpPut("{id}")]
        public IActionResult UpdateDriver(int id,
            [FromBody] Driver driver)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var checkCmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM drivers WHERE driver_id = @id",
                    conn);
                checkCmd.Parameters.AddWithValue("@id", id);
                var exists = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (exists == 0)
                    return NotFound(new { message = "Driver not found." });

                var updateCmd = new MySqlCommand(@"
                    UPDATE drivers
                    SET license_no = @license_no
                    WHERE driver_id = @id", conn);
                updateCmd.Parameters.AddWithValue("@license_no",
                    driver.LicenseNo);
                updateCmd.Parameters.AddWithValue("@id", id);
                updateCmd.ExecuteNonQuery();

                return Ok(new
                {
                    message = "Driver updated successfully.",
                    driver_id = id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ DELETE — mag-remove ng driver ══
        [HttpDelete("{id}")]
        public IActionResult DeleteDriver(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // Check kung may active rental ang driver
                var rentalCmd = new MySqlCommand(@"
                    SELECT COUNT(*) FROM rentals
                    WHERE driver_id = (
                        SELECT user_id FROM drivers
                        WHERE driver_id = @id)
                    AND status = 'approved'", conn);
                rentalCmd.Parameters.AddWithValue("@id", id);
                var activeRentals = Convert.ToInt32(
                    rentalCmd.ExecuteScalar());

                if (activeRentals > 0)
                    return Conflict(new
                    {
                        message =
                        "Hindi ma-delete — may active rental pa ang driver."
                    });

                var deleteCmd = new MySqlCommand(
                    "DELETE FROM drivers WHERE driver_id = @id", conn);
                deleteCmd.Parameters.AddWithValue("@id", id);
                int affected = deleteCmd.ExecuteNonQuery();

                if (affected == 0)
                    return NotFound(new { message = "Driver not found." });

                return Ok(new
                {
                    message = "Driver deleted successfully.",
                    driver_id = id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }
    }
}