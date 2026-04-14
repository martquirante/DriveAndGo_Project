using DriveAndGo_API.Models;
using DriveAndGo_API.Services;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Globalization;

namespace DriveAndGo_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RentalsController : ControllerBase
{
    private readonly string _connectionString;
    private readonly NotificationWriter _notificationWriter;

    public RentalsController(IConfiguration configuration, NotificationWriter notificationWriter)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        _notificationWriter = notificationWriter;
    }

    [HttpGet]
    public IActionResult GetRentals()
    {
        try
        {
            return Ok(ReadRentals(orderBy: "ORDER BY r.created_at DESC"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    public IActionResult GetRentalById(int id)
    {
        try
        {
            var rentals = ReadRentals("WHERE r.rental_id = @id", id);
            var rental = rentals.FirstOrDefault();

            return rental == null
                ? NotFound(new { Message = "Rental not found." })
                : Ok(rental);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpGet("customer/{customerId:int}")]
    public IActionResult GetRentalsByCustomer(int customerId)
    {
        try
        {
            return Ok(ReadRentals("WHERE r.customer_id = @id", customerId, "ORDER BY r.created_at DESC"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPost]
    public IActionResult AddRental([FromBody] Rental rental)
    {
        if (rental.CustomerId <= 0 || rental.VehicleId <= 0)
        {
            return BadRequest(new { Message = "CustomerId and VehicleId are required." });
        }

        if (!rental.EndDate.HasValue || rental.StartDate.Date >= rental.EndDate.Value.Date)
        {
            return BadRequest(new { Message = "End date must be later than the start date." });
        }

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            using var duplicateCommand = new MySqlCommand(
                @"SELECT COUNT(*) FROM rentals
                  WHERE customer_id = @customer_id
                    AND vehicle_id = @vehicle_id
                    AND LOWER(COALESCE(status, '')) = 'pending'",
                connection,
                transaction);
            duplicateCommand.Parameters.AddWithValue("@customer_id", rental.CustomerId);
            duplicateCommand.Parameters.AddWithValue("@vehicle_id", rental.VehicleId);

            if (Convert.ToInt32(duplicateCommand.ExecuteScalar()) > 0)
            {
                return Conflict(new { Message = "You already have a pending booking for this vehicle." });
            }

            using var vehicleCommand = new MySqlCommand(
                @"SELECT LOWER(COALESCE(status, '')) FROM vehicles
                  WHERE vehicle_id = @vehicle_id
                  LIMIT 1",
                connection,
                transaction);
            vehicleCommand.Parameters.AddWithValue("@vehicle_id", rental.VehicleId);
            var vehicleStatus = vehicleCommand.ExecuteScalar()?.ToString();

            if (string.IsNullOrWhiteSpace(vehicleStatus))
            {
                return NotFound(new { Message = "Vehicle not found." });
            }

            if (!string.Equals(vehicleStatus, "available", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { Message = "Vehicle is no longer available." });
            }

            int? driverUserId = null;
            if (rental.DriverId.HasValue)
            {
                using var driverCheckCommand = new MySqlCommand(
                    @"SELECT driver_id, user_id, LOWER(COALESCE(status, '')) AS status
                      FROM drivers
                      WHERE driver_id = @driver_id
                      LIMIT 1",
                    connection,
                    transaction);
                driverCheckCommand.Parameters.AddWithValue("@driver_id", rental.DriverId.Value);

                using var driverReader = driverCheckCommand.ExecuteReader();
                if (!driverReader.Read())
                {
                    return NotFound(new { Message = "Selected driver was not found." });
                }

                var driverStatus = driverReader["status"]?.ToString() ?? string.Empty;
                driverUserId = Convert.ToInt32(driverReader["user_id"], CultureInfo.InvariantCulture);
                driverReader.Close();

                if (!string.Equals(driverStatus, "available", StringComparison.OrdinalIgnoreCase))
                {
                    return Conflict(new { Message = "Selected driver is not available right now." });
                }
            }

            using var insertCommand = new MySqlCommand(
                @"INSERT INTO rentals
                    (customer_id, vehicle_id, driver_id, start_date, end_date, destination, status, total_amount, payment_method, payment_status, created_at)
                  VALUES
                    (@customer_id, @vehicle_id, @driver_id, @start_date, @end_date, @destination, 'pending', @total_amount, @payment_method, 'unpaid', NOW())",
                connection,
                transaction);

            insertCommand.Parameters.AddWithValue("@customer_id", rental.CustomerId);
            insertCommand.Parameters.AddWithValue("@vehicle_id", rental.VehicleId);
            insertCommand.Parameters.AddWithValue("@driver_id", rental.DriverId.HasValue ? rental.DriverId.Value : DBNull.Value);
            insertCommand.Parameters.AddWithValue("@start_date", rental.StartDate);
            insertCommand.Parameters.AddWithValue("@end_date", rental.EndDate.Value);
            insertCommand.Parameters.AddWithValue("@destination", string.IsNullOrWhiteSpace(rental.Destination) ? DBNull.Value : rental.Destination.Trim());
            insertCommand.Parameters.AddWithValue("@total_amount", rental.TotalAmount);
            insertCommand.Parameters.AddWithValue("@payment_method", NormalizeLower(rental.PaymentMethod, "cash"));
            insertCommand.ExecuteNonQuery();

            var rentalId = Convert.ToInt32(new MySqlCommand("SELECT LAST_INSERT_ID()", connection, transaction).ExecuteScalar(), CultureInfo.InvariantCulture);

            _notificationWriter.Create(
                connection,
                rental.CustomerId,
                "Booking request submitted",
                "Your booking request has been sent to Drive & Go for review.",
                "booking",
                transaction);

            if (driverUserId.HasValue)
            {
                _notificationWriter.Create(
                    connection,
                    driverUserId.Value,
                    "Trip request assigned",
                    "A customer requested a booking that includes your driver service.",
                    "driver-assignment",
                    transaction);
            }

            transaction.Commit();

            return Ok(new
            {
                Message = "Booking request submitted successfully.",
                RentalId = rentalId
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPatch("{id:int}/approve")]
    public IActionResult ApproveRental(int id)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            using var command = new MySqlCommand(
                @"SELECT
                    r.customer_id,
                    r.driver_id,
                    r.vehicle_id,
                    LOWER(COALESCE(r.status, '')) AS rental_status,
                    LOWER(COALESCE(v.status, '')) AS vehicle_status,
                    d.user_id AS driver_user_id,
                    LOWER(COALESCE(d.status, '')) AS driver_status
                  FROM rentals r
                  JOIN vehicles v ON v.vehicle_id = r.vehicle_id
                  LEFT JOIN drivers d ON d.driver_id = r.driver_id
                  WHERE r.rental_id = @id
                  LIMIT 1",
                connection,
                transaction);
            command.Parameters.AddWithValue("@id", id);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return NotFound(new { Message = "Rental not found." });
            }

            var customerId = Convert.ToInt32(reader["customer_id"], CultureInfo.InvariantCulture);
            var vehicleId = Convert.ToInt32(reader["vehicle_id"], CultureInfo.InvariantCulture);
            var driverId = reader["driver_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["driver_id"], CultureInfo.InvariantCulture);
            var driverUserId = reader["driver_user_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["driver_user_id"], CultureInfo.InvariantCulture);
            var rentalStatus = reader["rental_status"]?.ToString() ?? string.Empty;
            var vehicleStatus = reader["vehicle_status"]?.ToString() ?? string.Empty;
            var driverStatus = reader["driver_status"]?.ToString() ?? string.Empty;
            reader.Close();

            if (!string.Equals(rentalStatus, "pending", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { Message = $"Rental cannot be approved because it is already '{rentalStatus}'." });
            }

            if (!string.Equals(vehicleStatus, "available", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { Message = $"Vehicle cannot be approved because it is already '{vehicleStatus}'." });
            }

            if (driverId.HasValue && !string.Equals(driverStatus, "available", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { Message = "Assigned driver is no longer available." });
            }

            ExecuteStatusUpdate(connection, transaction, "UPDATE rentals SET status = 'approved' WHERE rental_id = @id", id);
            ExecuteStatusUpdate(connection, transaction, "UPDATE vehicles SET status = 'rented' WHERE vehicle_id = @id", vehicleId);

            if (driverId.HasValue)
            {
                ExecuteStatusUpdate(connection, transaction, "UPDATE drivers SET status = 'on-trip' WHERE driver_id = @id", driverId.Value);
            }

            _notificationWriter.Create(
                connection,
                customerId,
                "Booking approved",
                "Your booking was approved. Please prepare for your rental schedule.",
                "booking",
                transaction);

            if (driverUserId.HasValue)
            {
                _notificationWriter.Create(
                    connection,
                    driverUserId.Value,
                    "Rental approved",
                    "A trip assigned to you has been approved and is ready for dispatch.",
                    "driver-assignment",
                    transaction);
            }

            transaction.Commit();
            return Ok(new { Message = "Rental approved successfully.", RentalId = id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPatch("{id:int}/reject")]
    public IActionResult RejectRental(int id)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var rental = GetRentalStatusSnapshot(connection, transaction, id);
            if (rental == null)
            {
                return NotFound(new { Message = "Rental not found." });
            }

            if (!string.Equals(rental.Status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { Message = $"Rental cannot be rejected because it is already '{rental.Status}'." });
            }

            ExecuteStatusUpdate(connection, transaction, "UPDATE rentals SET status = 'rejected' WHERE rental_id = @id", id);
            _notificationWriter.Create(
                connection,
                rental.CustomerId,
                "Booking request rejected",
                "Your booking request was not approved. Please contact Drive & Go for assistance.",
                "booking",
                transaction);

            transaction.Commit();
            return Ok(new { Message = "Rental rejected successfully.", RentalId = id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPatch("{id:int}/cancel")]
    public IActionResult CancelRental(int id)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var rental = GetRentalStatusSnapshot(connection, transaction, id);
            if (rental == null)
            {
                return NotFound(new { Message = "Rental not found." });
            }

            if (!string.Equals(rental.Status, "pending", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(rental.Status, "approved", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { Message = $"Rental cannot be cancelled because it is already '{rental.Status}'." });
            }

            ExecuteStatusUpdate(connection, transaction, "UPDATE rentals SET status = 'cancelled' WHERE rental_id = @id", id);

            if (string.Equals(rental.Status, "approved", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteStatusUpdate(connection, transaction, "UPDATE vehicles SET status = 'available' WHERE vehicle_id = @id", rental.VehicleId);

                if (rental.DriverId.HasValue)
                {
                    ExecuteStatusUpdate(connection, transaction, "UPDATE drivers SET status = 'available' WHERE driver_id = @id", rental.DriverId.Value);
                }
            }

            _notificationWriter.Create(
                connection,
                rental.CustomerId,
                "Booking cancelled",
                "Your booking has been cancelled.",
                "booking",
                transaction);

            if (rental.DriverUserId.HasValue)
            {
                _notificationWriter.Create(
                    connection,
                    rental.DriverUserId.Value,
                    "Trip cancelled",
                    "A previously assigned trip has been cancelled.",
                    "driver-assignment",
                    transaction);
            }

            transaction.Commit();
            return Ok(new { Message = "Rental cancelled successfully.", RentalId = id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPatch("{id:int}/complete")]
    public IActionResult CompleteRental(int id)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var rental = GetRentalStatusSnapshot(connection, transaction, id);
            if (rental == null)
            {
                return NotFound(new { Message = "Rental not found." });
            }

            if (!string.Equals(rental.Status, "approved", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(rental.Status, "active", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(rental.Status, "in-use", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { Message = "Only approved or active rentals can be completed." });
            }

            ExecuteStatusUpdate(connection, transaction, "UPDATE rentals SET status = 'completed' WHERE rental_id = @id", id);
            ExecuteStatusUpdate(connection, transaction, "UPDATE vehicles SET status = 'available' WHERE vehicle_id = @id", rental.VehicleId);

            if (rental.DriverId.HasValue)
            {
                ExecuteStatusUpdate(connection, transaction, "UPDATE drivers SET status = 'available' WHERE driver_id = @id", rental.DriverId.Value);
            }

            _notificationWriter.Create(
                connection,
                rental.CustomerId,
                "Rental completed",
                "Your rental has been marked as completed. Thank you for using Drive & Go.",
                "booking",
                transaction);

            if (rental.DriverUserId.HasValue)
            {
                _notificationWriter.Create(
                    connection,
                    rental.DriverUserId.Value,
                    "Trip completed",
                    "Your assigned trip has been completed.",
                    "driver-assignment",
                    transaction);
            }

            transaction.Commit();
            return Ok(new { Message = "Rental completed successfully.", RentalId = id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    private List<Rental> ReadRentals(string? whereClause = null, int? id = null, string? orderBy = null)
    {
        var rentals = new List<Rental>();

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        using var command = new MySqlCommand(BuildRentalQuery(whereClause, orderBy), connection);
        if (id.HasValue)
        {
            command.Parameters.AddWithValue("@id", id.Value);
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rentals.Add(MapRental(reader));
        }

        return rentals;
    }

    private static string BuildRentalQuery(string? whereClause, string? orderBy)
    {
        var sql =
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
              FROM rentals r
              JOIN users customer ON r.customer_id = customer.user_id
              JOIN vehicles v ON r.vehicle_id = v.vehicle_id
              LEFT JOIN drivers d ON r.driver_id = d.driver_id
              LEFT JOIN users driver_user ON d.user_id = driver_user.user_id ";

        if (!string.IsNullOrWhiteSpace(whereClause))
        {
            sql += whereClause + " ";
        }

        sql += string.IsNullOrWhiteSpace(orderBy)
            ? "ORDER BY r.created_at DESC"
            : orderBy;

        return sql;
    }

    private static Rental MapRental(MySqlDataReader reader)
    {
        return new Rental
        {
            RentalId = Convert.ToInt32(reader["rental_id"], CultureInfo.InvariantCulture),
            CustomerId = Convert.ToInt32(reader["customer_id"], CultureInfo.InvariantCulture),
            VehicleId = Convert.ToInt32(reader["vehicle_id"], CultureInfo.InvariantCulture),
            DriverId = reader["driver_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["driver_id"], CultureInfo.InvariantCulture),
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
        };
    }

    private static void ExecuteStatusUpdate(MySqlConnection connection, MySqlTransaction transaction, string sql, int id)
    {
        using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }

    private static string NormalizeLower(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim().ToLowerInvariant();
    }

    private static RentalStatusSnapshot? GetRentalStatusSnapshot(MySqlConnection connection, MySqlTransaction transaction, int rentalId)
    {
        using var command = new MySqlCommand(
            @"SELECT
                r.customer_id,
                r.driver_id,
                r.vehicle_id,
                LOWER(COALESCE(r.status, '')) AS rental_status,
                d.user_id AS driver_user_id
              FROM rentals r
              LEFT JOIN drivers d ON d.driver_id = r.driver_id
              WHERE r.rental_id = @id
              LIMIT 1",
            connection,
            transaction);
        command.Parameters.AddWithValue("@id", rentalId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new RentalStatusSnapshot
        {
            CustomerId = Convert.ToInt32(reader["customer_id"], CultureInfo.InvariantCulture),
            DriverId = reader["driver_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["driver_id"], CultureInfo.InvariantCulture),
            DriverUserId = reader["driver_user_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["driver_user_id"], CultureInfo.InvariantCulture),
            VehicleId = Convert.ToInt32(reader["vehicle_id"], CultureInfo.InvariantCulture),
            Status = reader["rental_status"]?.ToString() ?? string.Empty
        };
    }

    private sealed class RentalStatusSnapshot
    {
        public int CustomerId { get; set; }
        public int VehicleId { get; set; }
        public int? DriverId { get; set; }
        public int? DriverUserId { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
