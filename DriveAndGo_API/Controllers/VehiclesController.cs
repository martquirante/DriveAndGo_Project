using DriveAndGo_API.Models;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace DriveAndGo_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class VehiclesController : ControllerBase
{
    private readonly string _connectionString;

    public VehiclesController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    [HttpGet]
    public IActionResult GetVehicles()
    {
        try
        {
            return Ok(ReadVehicles());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    public IActionResult GetVehicleById(int id)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var command = CreateVehicleQuery(connection, "WHERE vehicle_id = @id LIMIT 1");
            command.Parameters.AddWithValue("@id", id);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return NotFound(new { Message = "Vehicle not found." });
            }

            return Ok(MapVehicle(reader));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpGet("available")]
    public IActionResult GetAvailableVehicles()
    {
        try
        {
            return Ok(ReadVehicles("WHERE LOWER(status) = 'available'"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPost]
    public IActionResult AddVehicle([FromBody] Vehicle vehicle)
    {
        if (string.IsNullOrWhiteSpace(vehicle.Brand) ||
            string.IsNullOrWhiteSpace(vehicle.Model) ||
            string.IsNullOrWhiteSpace(vehicle.PlateNo))
        {
            return BadRequest(new { Message = "Brand, model, and plate number are required." });
        }

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var duplicateCommand = new MySqlCommand(
                "SELECT COUNT(*) FROM vehicles WHERE plate_no = @plate_no",
                connection);
            duplicateCommand.Parameters.AddWithValue("@plate_no", vehicle.PlateNo.Trim());

            if (Convert.ToInt32(duplicateCommand.ExecuteScalar()) > 0)
            {
                return Conflict(new { Message = "Plate number already exists." });
            }

            using var insertCommand = new MySqlCommand(
                @"INSERT INTO vehicles
                    (plate_no, brand, model, type, cc, status, rate_per_day, rate_with_driver, photo_url, description,
                     seat_capacity, transmission, model_3d_url, created_at, latitude, longitude, current_speed, last_update, in_garage)
                  VALUES
                    (@plate_no, @brand, @model, @type, @cc, @status, @rate_per_day, @rate_with_driver, @photo_url, @description,
                     @seat_capacity, @transmission, @model_3d_url, @created_at, @latitude, @longitude, @current_speed, @last_update, @in_garage)",
                connection);

            insertCommand.Parameters.AddWithValue("@plate_no", vehicle.PlateNo.Trim());
            insertCommand.Parameters.AddWithValue("@brand", vehicle.Brand.Trim());
            insertCommand.Parameters.AddWithValue("@model", vehicle.Model.Trim());
            insertCommand.Parameters.AddWithValue("@type", string.IsNullOrWhiteSpace(vehicle.Type) ? "Car" : vehicle.Type.Trim());
            insertCommand.Parameters.AddWithValue("@cc", vehicle.CC.HasValue ? vehicle.CC.Value : DBNull.Value);
            insertCommand.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(vehicle.Status) ? "available" : vehicle.Status.Trim().ToLowerInvariant());
            insertCommand.Parameters.AddWithValue("@rate_per_day", vehicle.RatePerDay);
            insertCommand.Parameters.AddWithValue("@rate_with_driver", vehicle.RateWithDriver);
            insertCommand.Parameters.AddWithValue("@photo_url", string.IsNullOrWhiteSpace(vehicle.PhotoUrl) ? DBNull.Value : vehicle.PhotoUrl.Trim());
            insertCommand.Parameters.AddWithValue("@description", string.IsNullOrWhiteSpace(vehicle.Description) ? DBNull.Value : vehicle.Description.Trim());
            insertCommand.Parameters.AddWithValue("@seat_capacity", vehicle.SeatCapacity <= 0 ? 1 : vehicle.SeatCapacity);
            insertCommand.Parameters.AddWithValue("@transmission", string.IsNullOrWhiteSpace(vehicle.Transmission) ? "Automatic" : vehicle.Transmission.Trim());
            insertCommand.Parameters.AddWithValue("@model_3d_url", string.IsNullOrWhiteSpace(vehicle.Model3dUrl) ? DBNull.Value : vehicle.Model3dUrl.Trim());
            insertCommand.Parameters.AddWithValue("@created_at", vehicle.CreatedAt == DateTime.MinValue ? DateTime.UtcNow : vehicle.CreatedAt);
            insertCommand.Parameters.AddWithValue("@latitude", vehicle.Latitude.HasValue ? vehicle.Latitude.Value : DBNull.Value);
            insertCommand.Parameters.AddWithValue("@longitude", vehicle.Longitude.HasValue ? vehicle.Longitude.Value : DBNull.Value);
            insertCommand.Parameters.AddWithValue("@current_speed", vehicle.CurrentSpeed.HasValue ? vehicle.CurrentSpeed.Value : DBNull.Value);
            insertCommand.Parameters.AddWithValue("@last_update", vehicle.LastUpdate.HasValue ? vehicle.LastUpdate.Value : DBNull.Value);
            insertCommand.Parameters.AddWithValue("@in_garage", vehicle.InGarage);

            insertCommand.ExecuteNonQuery();
            var vehicleId = Convert.ToInt32(new MySqlCommand("SELECT LAST_INSERT_ID()", connection).ExecuteScalar());

            return Ok(new { Message = "Vehicle added successfully.", VehicleId = vehicleId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public IActionResult UpdateVehicle(int id, [FromBody] Vehicle vehicle)
    {
        if (string.IsNullOrWhiteSpace(vehicle.Brand) ||
            string.IsNullOrWhiteSpace(vehicle.Model) ||
            string.IsNullOrWhiteSpace(vehicle.PlateNo))
        {
            return BadRequest(new { Message = "Brand, model, and plate number are required." });
        }

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var duplicateCommand = new MySqlCommand(
                @"SELECT COUNT(*) FROM vehicles
                  WHERE plate_no = @plate_no AND vehicle_id <> @id",
                connection);
            duplicateCommand.Parameters.AddWithValue("@plate_no", vehicle.PlateNo.Trim());
            duplicateCommand.Parameters.AddWithValue("@id", id);

            if (Convert.ToInt32(duplicateCommand.ExecuteScalar()) > 0)
            {
                return Conflict(new { Message = "Plate number is already used by another vehicle." });
            }

            using var updateCommand = new MySqlCommand(
                @"UPDATE vehicles
                  SET plate_no = @plate_no,
                      brand = @brand,
                      model = @model,
                      type = @type,
                      cc = @cc,
                      status = @status,
                      rate_per_day = @rate_per_day,
                      rate_with_driver = @rate_with_driver,
                      photo_url = @photo_url,
                      description = @description,
                      seat_capacity = @seat_capacity,
                      transmission = @transmission,
                      model_3d_url = @model_3d_url,
                      latitude = @latitude,
                      longitude = @longitude,
                      current_speed = @current_speed,
                      last_update = @last_update,
                      in_garage = @in_garage
                  WHERE vehicle_id = @id",
                connection);

            updateCommand.Parameters.AddWithValue("@plate_no", vehicle.PlateNo.Trim());
            updateCommand.Parameters.AddWithValue("@brand", vehicle.Brand.Trim());
            updateCommand.Parameters.AddWithValue("@model", vehicle.Model.Trim());
            updateCommand.Parameters.AddWithValue("@type", string.IsNullOrWhiteSpace(vehicle.Type) ? "Car" : vehicle.Type.Trim());
            updateCommand.Parameters.AddWithValue("@cc", vehicle.CC.HasValue ? vehicle.CC.Value : DBNull.Value);
            updateCommand.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(vehicle.Status) ? "available" : vehicle.Status.Trim().ToLowerInvariant());
            updateCommand.Parameters.AddWithValue("@rate_per_day", vehicle.RatePerDay);
            updateCommand.Parameters.AddWithValue("@rate_with_driver", vehicle.RateWithDriver);
            updateCommand.Parameters.AddWithValue("@photo_url", string.IsNullOrWhiteSpace(vehicle.PhotoUrl) ? DBNull.Value : vehicle.PhotoUrl.Trim());
            updateCommand.Parameters.AddWithValue("@description", string.IsNullOrWhiteSpace(vehicle.Description) ? DBNull.Value : vehicle.Description.Trim());
            updateCommand.Parameters.AddWithValue("@seat_capacity", vehicle.SeatCapacity <= 0 ? 1 : vehicle.SeatCapacity);
            updateCommand.Parameters.AddWithValue("@transmission", string.IsNullOrWhiteSpace(vehicle.Transmission) ? "Automatic" : vehicle.Transmission.Trim());
            updateCommand.Parameters.AddWithValue("@model_3d_url", string.IsNullOrWhiteSpace(vehicle.Model3dUrl) ? DBNull.Value : vehicle.Model3dUrl.Trim());
            updateCommand.Parameters.AddWithValue("@latitude", vehicle.Latitude.HasValue ? vehicle.Latitude.Value : DBNull.Value);
            updateCommand.Parameters.AddWithValue("@longitude", vehicle.Longitude.HasValue ? vehicle.Longitude.Value : DBNull.Value);
            updateCommand.Parameters.AddWithValue("@current_speed", vehicle.CurrentSpeed.HasValue ? vehicle.CurrentSpeed.Value : DBNull.Value);
            updateCommand.Parameters.AddWithValue("@last_update", vehicle.LastUpdate.HasValue ? vehicle.LastUpdate.Value : DBNull.Value);
            updateCommand.Parameters.AddWithValue("@in_garage", vehicle.InGarage);
            updateCommand.Parameters.AddWithValue("@id", id);

            if (updateCommand.ExecuteNonQuery() == 0)
            {
                return NotFound(new { Message = "Vehicle not found." });
            }

            return Ok(new { Message = "Vehicle updated successfully.", VehicleId = id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public IActionResult DeleteVehicle(int id)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var rentalCommand = new MySqlCommand(
                @"SELECT COUNT(*) FROM rentals
                  WHERE vehicle_id = @id
                    AND LOWER(COALESCE(status, '')) IN ('pending', 'approved', 'active', 'in-use')",
                connection);
            rentalCommand.Parameters.AddWithValue("@id", id);

            if (Convert.ToInt32(rentalCommand.ExecuteScalar()) > 0)
            {
                return Conflict(new { Message = "Cannot delete a vehicle with active or pending rentals." });
            }

            using var deleteCommand = new MySqlCommand(
                "DELETE FROM vehicles WHERE vehicle_id = @id",
                connection);
            deleteCommand.Parameters.AddWithValue("@id", id);

            if (deleteCommand.ExecuteNonQuery() == 0)
            {
                return NotFound(new { Message = "Vehicle not found." });
            }

            return Ok(new { Message = "Vehicle deleted successfully.", VehicleId = id });
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
            "rented",
            "maintenance",
            "retired",
            "in-use",
            "active"
        };

        if (request == null || string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new { Message = "Status is required." });
        }

        if (!validStatuses.Contains(request.Status))
        {
            return BadRequest(new { Message = "Valid statuses: available, rented, maintenance, retired, in-use, active" });
        }

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var command = new MySqlCommand(
                @"UPDATE vehicles
                  SET status = @status
                  WHERE vehicle_id = @id",
                connection);
            command.Parameters.AddWithValue("@status", request.Status.Trim().ToLowerInvariant());
            command.Parameters.AddWithValue("@id", id);

            if (command.ExecuteNonQuery() == 0)
            {
                return NotFound(new { Message = "Vehicle not found." });
            }

            return Ok(new { Message = "Vehicle status updated successfully.", VehicleId = id, Status = request.Status.Trim().ToLowerInvariant() });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    private List<VehicleDto> ReadVehicles(string? whereClause = null)
    {
        var vehicles = new List<VehicleDto>();

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        using var command = CreateVehicleQuery(connection, whereClause);
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            vehicles.Add(MapVehicle(reader));
        }

        return vehicles;
    }

    private static MySqlCommand CreateVehicleQuery(MySqlConnection connection, string? whereClause)
    {
        var sql =
            @"SELECT
                vehicle_id,
                plate_no,
                brand,
                model,
                type,
                cc,
                status,
                rate_per_day,
                rate_with_driver,
                COALESCE(photo_url, '') AS photo_url,
                COALESCE(description, '') AS description,
                COALESCE(seat_capacity, 1) AS seat_capacity,
                COALESCE(transmission, 'Automatic') AS transmission,
                COALESCE(model_3d_url, '') AS model_3d_url,
                created_at,
                latitude,
                longitude,
                current_speed,
                last_update,
                COALESCE(in_garage, 1) AS in_garage
              FROM vehicles ";

        if (!string.IsNullOrWhiteSpace(whereClause))
        {
            sql += whereClause + " ";
        }

        sql += "ORDER BY brand ASC, model ASC";
        return new MySqlCommand(sql, connection);
    }

    private static VehicleDto MapVehicle(MySqlDataReader reader)
    {
        return new VehicleDto
        {
            VehicleId = Convert.ToInt32(reader["vehicle_id"]),
            PlateNo = reader["plate_no"]?.ToString() ?? string.Empty,
            Brand = reader["brand"]?.ToString() ?? string.Empty,
            Model = reader["model"]?.ToString() ?? string.Empty,
            Type = reader["type"]?.ToString() ?? string.Empty,
            Cc = reader["cc"] == DBNull.Value ? null : Convert.ToInt32(reader["cc"]),
            Status = reader["status"]?.ToString() ?? "available",
            RatePerDay = reader["rate_per_day"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["rate_per_day"]),
            RateWithDriver = reader["rate_with_driver"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["rate_with_driver"]),
            PhotoUrl = reader["photo_url"]?.ToString() ?? string.Empty,
            Description = reader["description"]?.ToString() ?? string.Empty,
            SeatCapacity = reader["seat_capacity"] == DBNull.Value ? 1 : Convert.ToInt32(reader["seat_capacity"]),
            Transmission = reader["transmission"]?.ToString() ?? "Automatic",
            Model3DUrl = reader["model_3d_url"]?.ToString() ?? string.Empty,
            CreatedAt = reader["created_at"] == DBNull.Value ? null : Convert.ToDateTime(reader["created_at"]),
            Latitude = reader["latitude"] == DBNull.Value ? null : Convert.ToDouble(reader["latitude"]),
            Longitude = reader["longitude"] == DBNull.Value ? null : Convert.ToDouble(reader["longitude"]),
            CurrentSpeed = reader["current_speed"] == DBNull.Value ? null : Convert.ToInt32(reader["current_speed"]),
            LastUpdate = reader["last_update"] == DBNull.Value ? null : Convert.ToDateTime(reader["last_update"]),
            InGarage = reader["in_garage"] != DBNull.Value && Convert.ToBoolean(reader["in_garage"])
        };
    }
}
