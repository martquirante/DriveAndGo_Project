using DriveAndGo_API.Models;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Globalization;

namespace DriveAndGo_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DriversController : ControllerBase
{
    private readonly string _connectionString;

    public DriversController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    [HttpGet]
    public IActionResult GetDrivers()
    {
        try
        {
            return Ok(ReadDrivers());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpGet("available")]
    public IActionResult GetAvailableDrivers()
    {
        try
        {
            return Ok(ReadDrivers("WHERE LOWER(COALESCE(d.status, '')) = 'available'"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    public IActionResult GetDriverById(int id)
    {
        try
        {
            var driver = ReadDrivers("WHERE d.driver_id = @id", id).FirstOrDefault();
            return driver == null
                ? NotFound(new { Message = "Driver not found." })
                : Ok(driver);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpGet("user/{userId:int}")]
    public IActionResult GetDriverByUserId(int userId)
    {
        try
        {
            var driver = ReadDrivers("WHERE d.user_id = @id", userId).FirstOrDefault();
            return driver == null
                ? NotFound(new { Message = "Driver profile not found." })
                : Ok(driver);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpGet("assignments/user/{userId:int}")]
    public IActionResult GetAssignmentsByUserId(int userId)
    {
        try
        {
            var rentals = new List<Rental>();

            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var command = new MySqlCommand(
                @"SELECT
                    r.rental_id,
                    r.customer_id,
                    r.vehicle_id,
                    r.driver_id,
                    r.start_date,
                    r.end_date,
                    r.destination,
                    r.status,
                    r.total_amount,
                    r.payment_method,
                    r.payment_status,
                    r.created_at,
                    customer.full_name AS customer_name,
                    customer.phone AS customer_phone,
                    customer.email AS customer_email,
                    CONCAT(v.brand, ' ', v.model) AS vehicle_name,
                    v.plate_no AS vehicle_plate_no,
                    driver_user.full_name AS driver_name,
                    driver_user.phone AS driver_phone
                  FROM drivers d
                  JOIN rentals r ON r.driver_id = d.driver_id
                  JOIN users customer ON r.customer_id = customer.user_id
                  JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                  JOIN users driver_user ON d.user_id = driver_user.user_id
                  WHERE d.user_id = @user_id
                  ORDER BY CASE
                        WHEN LOWER(COALESCE(r.status, '')) IN ('active', 'approved', 'in-use') THEN 0
                        WHEN LOWER(COALESCE(r.status, '')) = 'pending' THEN 1
                        ELSE 2
                    END,
                    r.start_date ASC,
                    r.created_at DESC",
                connection);
            command.Parameters.AddWithValue("@user_id", userId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rentals.Add(new Rental
                {
                    RentalId = Convert.ToInt32(reader["rental_id"], CultureInfo.InvariantCulture),
                    CustomerId = Convert.ToInt32(reader["customer_id"], CultureInfo.InvariantCulture),
                    VehicleId = Convert.ToInt32(reader["vehicle_id"], CultureInfo.InvariantCulture),
                    DriverId = reader["driver_id"] == DBNull.Value ? null : Convert.ToInt32(reader["driver_id"], CultureInfo.InvariantCulture),
                    StartDate = Convert.ToDateTime(reader["start_date"], CultureInfo.InvariantCulture),
                    EndDate = reader["end_date"] == DBNull.Value ? null : Convert.ToDateTime(reader["end_date"], CultureInfo.InvariantCulture),
                    Destination = reader["destination"] == DBNull.Value ? null : reader["destination"].ToString(),
                    Status = reader["status"]?.ToString() ?? "pending",
                    TotalAmount = reader["total_amount"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["total_amount"], CultureInfo.InvariantCulture),
                    PaymentMethod = reader["payment_method"]?.ToString() ?? "cash",
                    PaymentStatus = reader["payment_status"]?.ToString() ?? "unpaid",
                    CreatedAt = reader["created_at"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["created_at"], CultureInfo.InvariantCulture),
                    CustomerName = reader["customer_name"] == DBNull.Value ? null : reader["customer_name"].ToString(),
                    CustomerPhone = reader["customer_phone"] == DBNull.Value ? null : reader["customer_phone"].ToString(),
                    CustomerEmail = reader["customer_email"] == DBNull.Value ? null : reader["customer_email"].ToString(),
                    VehicleName = reader["vehicle_name"] == DBNull.Value ? null : reader["vehicle_name"].ToString(),
                    VehiclePlateNo = reader["vehicle_plate_no"] == DBNull.Value ? null : reader["vehicle_plate_no"].ToString(),
                    DriverName = reader["driver_name"] == DBNull.Value ? null : reader["driver_name"].ToString(),
                    DriverPhone = reader["driver_phone"] == DBNull.Value ? null : reader["driver_phone"].ToString()
                });
            }

            return Ok(rentals);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPost]
    public IActionResult AddDriver([FromBody] Driver driver)
    {
        if (driver.UserId <= 0 || string.IsNullOrWhiteSpace(driver.LicenseNo))
        {
            return BadRequest(new { Message = "UserId and license number are required." });
        }

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var existingDriverCommand = new MySqlCommand(
                "SELECT COUNT(*) FROM drivers WHERE user_id = @user_id",
                connection);
            existingDriverCommand.Parameters.AddWithValue("@user_id", driver.UserId);

            if (Convert.ToInt32(existingDriverCommand.ExecuteScalar()) > 0)
            {
                return Conflict(new { Message = "Driver profile already exists for this user." });
            }

            using var existingUserCommand = new MySqlCommand(
                "SELECT COUNT(*) FROM users WHERE user_id = @user_id",
                connection);
            existingUserCommand.Parameters.AddWithValue("@user_id", driver.UserId);

            if (Convert.ToInt32(existingUserCommand.ExecuteScalar()) == 0)
            {
                return NotFound(new { Message = "User account not found." });
            }

            using var insertCommand = new MySqlCommand(
                @"INSERT INTO drivers
                    (user_id, license_no, status, rating_avg, total_trips)
                  VALUES
                    (@user_id, @license_no, 'available', 0.0, 0)",
                connection);
            insertCommand.Parameters.AddWithValue("@user_id", driver.UserId);
            insertCommand.Parameters.AddWithValue("@license_no", driver.LicenseNo.Trim());
            insertCommand.ExecuteNonQuery();

            var driverId = Convert.ToInt32(new MySqlCommand("SELECT LAST_INSERT_ID()", connection).ExecuteScalar());

            using var updateRoleCommand = new MySqlCommand(
                "UPDATE users SET role = 'driver' WHERE user_id = @user_id",
                connection);
            updateRoleCommand.Parameters.AddWithValue("@user_id", driver.UserId);
            updateRoleCommand.ExecuteNonQuery();

            return Ok(new { Message = "Driver added successfully.", DriverId = driverId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPatch("{id:int}/status")]
    public IActionResult UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        var validStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "available",
            "on-trip",
            "off-duty",
            "inactive"
        };

        if (request == null || string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new { Message = "Status is required." });
        }

        if (!validStatuses.Contains(request.Status))
        {
            return BadRequest(new { Message = "Valid driver statuses: available, on-trip, off-duty, inactive" });
        }

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var updateCommand = new MySqlCommand(
                "UPDATE drivers SET status = @status WHERE driver_id = @id",
                connection);
            updateCommand.Parameters.AddWithValue("@status", request.Status.Trim().ToLowerInvariant());
            updateCommand.Parameters.AddWithValue("@id", id);

            if (updateCommand.ExecuteNonQuery() == 0)
            {
                return NotFound(new { Message = "Driver not found." });
            }

            return Ok(new { Message = "Driver status updated successfully.", DriverId = id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public IActionResult UpdateDriver(int id, [FromBody] Driver driver)
    {
        if (string.IsNullOrWhiteSpace(driver.LicenseNo))
        {
            return BadRequest(new { Message = "License number is required." });
        }

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var updateCommand = new MySqlCommand(
                @"UPDATE drivers
                  SET license_no = @license_no
                  WHERE driver_id = @id",
                connection);
            updateCommand.Parameters.AddWithValue("@license_no", driver.LicenseNo.Trim());
            updateCommand.Parameters.AddWithValue("@id", id);

            if (updateCommand.ExecuteNonQuery() == 0)
            {
                return NotFound(new { Message = "Driver not found." });
            }

            return Ok(new { Message = "Driver updated successfully.", DriverId = id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public IActionResult DeleteDriver(int id)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var activeRentalsCommand = new MySqlCommand(
                @"SELECT COUNT(*) FROM rentals
                  WHERE driver_id = @driver_id
                    AND LOWER(COALESCE(status, '')) IN ('approved', 'active', 'in-use')",
                connection);
            activeRentalsCommand.Parameters.AddWithValue("@driver_id", id);

            if (Convert.ToInt32(activeRentalsCommand.ExecuteScalar()) > 0)
            {
                return Conflict(new { Message = "Driver cannot be deleted while assigned to active rentals." });
            }

            using var deleteCommand = new MySqlCommand(
                "DELETE FROM drivers WHERE driver_id = @id",
                connection);
            deleteCommand.Parameters.AddWithValue("@id", id);

            if (deleteCommand.ExecuteNonQuery() == 0)
            {
                return NotFound(new { Message = "Driver not found." });
            }

            return Ok(new { Message = "Driver deleted successfully.", DriverId = id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    private List<Driver> ReadDrivers(string? whereClause = null, int? id = null)
    {
        var drivers = new List<Driver>();

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        var sql =
            @"SELECT
                d.driver_id,
                d.user_id,
                d.license_no,
                d.status,
                d.rating_avg,
                d.total_trips,
                u.full_name,
                u.email,
                u.phone
              FROM drivers d
              JOIN users u ON d.user_id = u.user_id ";

        if (!string.IsNullOrWhiteSpace(whereClause))
        {
            sql += whereClause + " ";
        }

        sql += "ORDER BY u.full_name ASC";

        using var command = new MySqlCommand(sql, connection);
        if (id.HasValue)
        {
            command.Parameters.AddWithValue("@id", id.Value);
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            drivers.Add(new Driver
            {
                DriverId = Convert.ToInt32(reader["driver_id"], CultureInfo.InvariantCulture),
                UserId = Convert.ToInt32(reader["user_id"], CultureInfo.InvariantCulture),
                LicenseNo = reader["license_no"]?.ToString() ?? string.Empty,
                Status = reader["status"]?.ToString() ?? "available",
                RatingAvg = reader["rating_avg"] == DBNull.Value ? null : Convert.ToDecimal(reader["rating_avg"], CultureInfo.InvariantCulture),
                TotalTrips = reader["total_trips"] == DBNull.Value ? 0 : Convert.ToInt32(reader["total_trips"], CultureInfo.InvariantCulture),
                FullName = reader["full_name"]?.ToString(),
                Email = reader["email"]?.ToString(),
                Phone = reader["phone"]?.ToString()
            });
        }

        return drivers;
    }
}
