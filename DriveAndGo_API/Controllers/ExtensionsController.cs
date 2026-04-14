using DriveAndGo_API.Models;
using DriveAndGo_API.Services;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace DriveAndGo_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ExtensionsController : ControllerBase
{
    private readonly string _connectionString;
    private readonly NotificationWriter _notificationWriter;

    public ExtensionsController(IConfiguration configuration, NotificationWriter notificationWriter)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        _notificationWriter = notificationWriter;
    }

    [HttpGet]
    public IActionResult GetExtensions()
    {
        try
        {
            return Ok(ReadExtensions());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpGet("rental/{rentalId:int}")]
    public IActionResult GetByRental(int rentalId)
    {
        try
        {
            return Ok(ReadExtensions(rentalId));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPost]
    public IActionResult RequestExtension([FromBody] Extension extension)
    {
        if (extension.RentalId <= 0 || extension.AddedDays <= 0)
        {
            return BadRequest(new { Message = "RentalId and addedDays are required." });
        }

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var rentalCommand = new MySqlCommand(
                @"SELECT
                    r.customer_id,
                    r.status,
                    r.vehicle_id
                  FROM rentals r
                  WHERE r.rental_id = @rental_id
                  LIMIT 1",
                connection);
            rentalCommand.Parameters.AddWithValue("@rental_id", extension.RentalId);

            using var rentalReader = rentalCommand.ExecuteReader();
            if (!rentalReader.Read())
            {
                return NotFound(new { Message = "Rental not found." });
            }

            var customerId = Convert.ToInt32(rentalReader["customer_id"]);
            var rentalStatus = rentalReader["status"]?.ToString() ?? string.Empty;
            var vehicleId = Convert.ToInt32(rentalReader["vehicle_id"]);
            rentalReader.Close();

            var allowedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "approved",
                "active",
                "in-use"
            };

            if (!allowedStatuses.Contains(rentalStatus))
            {
                return BadRequest(new { Message = "Only approved or active rentals can request an extension." });
            }

            using var vehicleCommand = new MySqlCommand(
                "SELECT rate_per_day FROM vehicles WHERE vehicle_id = @vehicle_id",
                connection);
            vehicleCommand.Parameters.AddWithValue("@vehicle_id", vehicleId);
            var dailyRate = Convert.ToDecimal(vehicleCommand.ExecuteScalar());
            var addedFee = dailyRate * extension.AddedDays;

            using var insertCommand = new MySqlCommand(
                @"INSERT INTO extensions
                    (rental_id, added_days, added_fee, status, requested_at)
                  VALUES
                    (@rental_id, @added_days, @added_fee, 'pending', NOW())",
                connection);
            insertCommand.Parameters.AddWithValue("@rental_id", extension.RentalId);
            insertCommand.Parameters.AddWithValue("@added_days", extension.AddedDays);
            insertCommand.Parameters.AddWithValue("@added_fee", addedFee);
            insertCommand.ExecuteNonQuery();

            var extensionId = Convert.ToInt32(new MySqlCommand("SELECT LAST_INSERT_ID()", connection).ExecuteScalar());

            _notificationWriter.Create(
                connection,
                customerId,
                "Extension request submitted",
                $"Your request to extend the rental by {extension.AddedDays} day(s) is now pending review.",
                "extension");

            return Ok(new
            {
                Message = "Extension request submitted successfully.",
                ExtensionId = extensionId,
                AddedDays = extension.AddedDays,
                AddedFee = addedFee
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPatch("{id:int}/approve")]
    public IActionResult ApproveExtension(int id)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var command = new MySqlCommand(
                @"SELECT
                    e.status,
                    e.rental_id,
                    e.added_days,
                    e.added_fee,
                    r.customer_id
                  FROM extensions e
                  JOIN rentals r ON r.rental_id = e.rental_id
                  WHERE e.extension_id = @id
                  LIMIT 1",
                connection);
            command.Parameters.AddWithValue("@id", id);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return NotFound(new { Message = "Extension request not found." });
            }

            var status = reader["status"]?.ToString() ?? string.Empty;
            var rentalId = Convert.ToInt32(reader["rental_id"]);
            var addedDays = Convert.ToInt32(reader["added_days"]);
            var addedFee = Convert.ToDecimal(reader["added_fee"]);
            var customerId = Convert.ToInt32(reader["customer_id"]);
            reader.Close();

            if (!string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { Message = $"Extension cannot be approved because it is already '{status}'." });
            }

            using var updateExtensionCommand = new MySqlCommand(
                "UPDATE extensions SET status = 'approved' WHERE extension_id = @id",
                connection);
            updateExtensionCommand.Parameters.AddWithValue("@id", id);
            updateExtensionCommand.ExecuteNonQuery();

            using var updateRentalCommand = new MySqlCommand(
                @"UPDATE rentals
                  SET end_date = DATE_ADD(end_date, INTERVAL @days DAY),
                      total_amount = total_amount + @fee
                  WHERE rental_id = @rental_id",
                connection);
            updateRentalCommand.Parameters.AddWithValue("@days", addedDays);
            updateRentalCommand.Parameters.AddWithValue("@fee", addedFee);
            updateRentalCommand.Parameters.AddWithValue("@rental_id", rentalId);
            updateRentalCommand.ExecuteNonQuery();

            _notificationWriter.Create(
                connection,
                customerId,
                "Extension approved",
                $"Your extension request was approved for {addedDays} additional day(s).",
                "extension");

            return Ok(new { Message = "Extension approved successfully.", ExtensionId = id, RentalId = rentalId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPatch("{id:int}/reject")]
    public IActionResult RejectExtension(int id)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var command = new MySqlCommand(
                @"SELECT e.status, r.customer_id
                  FROM extensions e
                  JOIN rentals r ON r.rental_id = e.rental_id
                  WHERE e.extension_id = @id
                  LIMIT 1",
                connection);
            command.Parameters.AddWithValue("@id", id);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return NotFound(new { Message = "Extension request not found." });
            }

            var status = reader["status"]?.ToString() ?? string.Empty;
            var customerId = Convert.ToInt32(reader["customer_id"]);
            reader.Close();

            if (!string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { Message = $"Extension cannot be rejected because it is already '{status}'." });
            }

            using var updateCommand = new MySqlCommand(
                "UPDATE extensions SET status = 'rejected' WHERE extension_id = @id",
                connection);
            updateCommand.Parameters.AddWithValue("@id", id);
            updateCommand.ExecuteNonQuery();

            _notificationWriter.Create(
                connection,
                customerId,
                "Extension rejected",
                "Your extension request was rejected. Please contact Drive & Go for details.",
                "extension");

            return Ok(new { Message = "Extension rejected successfully.", ExtensionId = id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    private List<Extension> ReadExtensions(int? rentalId = null)
    {
        var extensions = new List<Extension>();

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        var sql =
            @"SELECT
                e.extension_id,
                e.rental_id,
                e.added_days,
                e.added_fee,
                e.status,
                e.requested_at,
                u.full_name AS customer_name,
                CONCAT(v.brand, ' ', v.model) AS vehicle_name
              FROM extensions e
              JOIN rentals r ON e.rental_id = r.rental_id
              JOIN users u ON r.customer_id = u.user_id
              JOIN vehicles v ON r.vehicle_id = v.vehicle_id ";

        if (rentalId.HasValue)
        {
            sql += "WHERE e.rental_id = @rental_id ";
        }

        sql += "ORDER BY e.requested_at DESC";

        using var command = new MySqlCommand(sql, connection);
        if (rentalId.HasValue)
        {
            command.Parameters.AddWithValue("@rental_id", rentalId.Value);
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            extensions.Add(new Extension
            {
                ExtensionId = Convert.ToInt32(reader["extension_id"]),
                RentalId = Convert.ToInt32(reader["rental_id"]),
                AddedDays = Convert.ToInt32(reader["added_days"]),
                AddedFee = Convert.ToDecimal(reader["added_fee"]),
                Status = reader["status"]?.ToString(),
                RequestedAt = reader["requested_at"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["requested_at"]),
                CustomerName = reader["customer_name"]?.ToString(),
                VehicleName = reader["vehicle_name"]?.ToString()
            });
        }

        return extensions;
    }
}
