using DriveAndGo_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace DriveAndGo_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class VehiclesController : ControllerBase
{
    private readonly string _connectionString;
    private readonly Microsoft.AspNetCore.SignalR.IHubContext<DriveAndGo_API.Hubs.AdminHub> _hubContext;
    private readonly DriveAndGo_API.Services.AuditService _auditService;

    public VehiclesController(
        IConfiguration configuration,
        Microsoft.AspNetCore.SignalR.IHubContext<DriveAndGo_API.Hubs.AdminHub> hubContext,
        DriveAndGo_API.Services.AuditService auditService)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        _hubContext = hubContext;
        _auditService = auditService;
    }

    private string GetAdminName()
    {
        if (Request.Headers.TryGetValue("X-Admin-Name", out var headerName) && !string.IsNullOrWhiteSpace(headerName))
        {
            return headerName.ToString();
        }
        string claimName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "";
        if (!string.IsNullOrWhiteSpace(claimName)) return claimName;
        return "Admin";
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

    [HttpGet("fleet")]
    public IActionResult GetFleetVehicles()
    {
        try
        {
            var fleet = new List<DriveAndGo_API.Models.VehicleFleetDto>();
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(
                @"SELECT vehicle_id, brand, model, plate_no, type, rate_per_day, status, photo_url, latitude, longitude 
                  FROM vehicles 
                  ORDER BY brand ASC, model ASC", connection);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                fleet.Add(new DriveAndGo_API.Models.VehicleFleetDto
                {
                    VehicleId = Convert.ToInt32(reader["vehicle_id"]),
                    Brand = reader["brand"]?.ToString() ?? string.Empty,
                    Model = reader["model"]?.ToString() ?? string.Empty,
                    PlateNo = reader["plate_no"]?.ToString() ?? string.Empty,
                    Type = reader["type"]?.ToString() ?? string.Empty,
                    RatePerDay = reader["rate_per_day"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["rate_per_day"]),
                    Status = reader["status"]?.ToString() ?? "available",
                    PhotoUrl = reader["photo_url"]?.ToString() ?? string.Empty,
                    Latitude = reader["latitude"] == DBNull.Value ? null : Convert.ToDouble(reader["latitude"]),
                    Longitude = reader["longitude"] == DBNull.Value ? null : Convert.ToDouble(reader["longitude"])
                });
            }
            return Ok(fleet);
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
            using var connection = new NpgsqlConnection(_connectionString);
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
    public async Task<IActionResult> AddVehicle([FromBody] Vehicle vehicle)
    {
        if (string.IsNullOrWhiteSpace(vehicle.Brand) ||
            string.IsNullOrWhiteSpace(vehicle.Model) ||
            string.IsNullOrWhiteSpace(vehicle.PlateNo))
        {
            return BadRequest(new { Message = "Brand, model, and plate number are required." });
        }

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var duplicateCommand = new NpgsqlCommand(
                "SELECT COUNT(*) FROM vehicles WHERE plate_no = @plate_no",
                connection);
            duplicateCommand.Parameters.AddWithValue("@plate_no", vehicle.PlateNo.Trim());

            if (Convert.ToInt32(duplicateCommand.ExecuteScalar()) > 0)
            {
                return Conflict(new { Message = "Plate number already exists." });
            }

            using var insertCommand = new NpgsqlCommand(
                @"INSERT INTO vehicles
                    (plate_no, brand, model, type, cc, status, rate_per_day, rate_with_driver, photo_url, description,
                     seat_capacity, transmission, model_3d_url, created_at, latitude, longitude, current_speed, last_update, in_garage)
                  VALUES
                    (@plate_no, @brand, @model, @type, @cc, @status, @rate_per_day, @rate_with_driver, @photo_url, @description,
                     @seat_capacity, @transmission, @model_3d_url, @created_at, @latitude, @longitude, @current_speed, @last_update, @in_garage)
                  RETURNING vehicle_id",
                connection);

            insertCommand.Parameters.AddWithValue("@plate_no", vehicle.PlateNo.Trim());
            insertCommand.Parameters.AddWithValue("@brand", vehicle.Brand.Trim());
            insertCommand.Parameters.AddWithValue("@model", vehicle.Model.Trim());
            insertCommand.Parameters.AddWithValue("@type", string.IsNullOrWhiteSpace(vehicle.Type) ? "Car" : vehicle.Type.Trim());
            insertCommand.Parameters.AddWithValue("@cc", vehicle.CC.HasValue ? vehicle.CC.Value : DBNull.Value);
            insertCommand.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(vehicle.Status) ? "available" : vehicle.Status.Trim().ToLowerInvariant());
            insertCommand.Parameters.AddWithValue("@rate_per_day", vehicle.RatePerDay);
            insertCommand.Parameters.AddWithValue("@rate_with_driver", vehicle.RateWithDriver);
            insertCommand.Parameters.AddWithValue("@photo_url", string.IsNullOrWhiteSpace(vehicle.PhotoUrl) ? "" : vehicle.PhotoUrl.Trim());
            insertCommand.Parameters.AddWithValue("@description", string.IsNullOrWhiteSpace(vehicle.Description) ? "" : vehicle.Description.Trim());
            insertCommand.Parameters.AddWithValue("@seat_capacity", vehicle.SeatCapacity <= 0 ? 1 : vehicle.SeatCapacity);
            insertCommand.Parameters.AddWithValue("@transmission", string.IsNullOrWhiteSpace(vehicle.Transmission) ? "Automatic" : vehicle.Transmission.Trim());
            insertCommand.Parameters.AddWithValue("@model_3d_url", string.IsNullOrWhiteSpace(vehicle.Model3dUrl) ? "" : vehicle.Model3dUrl.Trim());
            insertCommand.Parameters.AddWithValue("@created_at", vehicle.CreatedAt == DateTime.MinValue ? DateTime.UtcNow : vehicle.CreatedAt);
            insertCommand.Parameters.AddWithValue("@latitude", vehicle.Latitude.HasValue ? vehicle.Latitude.Value : DBNull.Value);
            insertCommand.Parameters.AddWithValue("@longitude", vehicle.Longitude.HasValue ? vehicle.Longitude.Value : DBNull.Value);
            insertCommand.Parameters.AddWithValue("@current_speed", vehicle.CurrentSpeed.HasValue ? vehicle.CurrentSpeed.Value : DBNull.Value);
            insertCommand.Parameters.AddWithValue("@last_update", vehicle.LastUpdate.HasValue ? vehicle.LastUpdate.Value : DBNull.Value);
            insertCommand.Parameters.AddWithValue("@in_garage", vehicle.InGarage);

            var vehicleId = Convert.ToInt32(insertCommand.ExecuteScalar());

            // System-wide audit trail logging
            string adminName = GetAdminName();
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            _ = _auditService.LogActionAsync(
                adminUserId: 0,
                adminName: adminName,
                actionType: "VEHICLE_ADDED",
                targetUserId: 0,
                ipAddress: clientIp,
                oldValues: new { vehicleId = vehicleId },
                newValues: new { description = $"{adminName} added vehicle {vehicle.Brand} {vehicle.Model} ({vehicle.PlateNo})", plateNo = vehicle.PlateNo }
            );

            await _hubContext.Clients.All.SendAsync("ReceiveVehicleUpdate");
            await _hubContext.Clients.All.SendAsync("ReceiveDashboardUpdate");

            return Ok(new { Message = "Vehicle added successfully.", VehicleId = vehicleId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateVehicle(int id, [FromBody] Vehicle vehicle)
    {
        if (string.IsNullOrWhiteSpace(vehicle.Brand) ||
            string.IsNullOrWhiteSpace(vehicle.Model) ||
            string.IsNullOrWhiteSpace(vehicle.PlateNo))
        {
            return BadRequest(new { Message = "Brand, model, and plate number are required." });
        }

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var duplicateCommand = new NpgsqlCommand(
                @"SELECT COUNT(*) FROM vehicles
                  WHERE plate_no = @plate_no AND vehicle_id <> @id",
                connection);
            duplicateCommand.Parameters.AddWithValue("@plate_no", vehicle.PlateNo.Trim());
            duplicateCommand.Parameters.AddWithValue("@id", id);

            if (Convert.ToInt32(duplicateCommand.ExecuteScalar()) > 0)
            {
                return Conflict(new { Message = "Plate number is already used by another vehicle." });
            }

            using var updateCommand = new NpgsqlCommand(
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
            updateCommand.Parameters.AddWithValue("@photo_url", string.IsNullOrWhiteSpace(vehicle.PhotoUrl) ? "" : vehicle.PhotoUrl.Trim());
            updateCommand.Parameters.AddWithValue("@description", string.IsNullOrWhiteSpace(vehicle.Description) ? "" : vehicle.Description.Trim());
            updateCommand.Parameters.AddWithValue("@seat_capacity", vehicle.SeatCapacity <= 0 ? 1 : vehicle.SeatCapacity);
            updateCommand.Parameters.AddWithValue("@transmission", string.IsNullOrWhiteSpace(vehicle.Transmission) ? "Automatic" : vehicle.Transmission.Trim());
            updateCommand.Parameters.AddWithValue("@model_3d_url", string.IsNullOrWhiteSpace(vehicle.Model3dUrl) ? "" : vehicle.Model3dUrl.Trim());
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

            // System-wide audit trail logging
            string adminName = GetAdminName();
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            _ = _auditService.LogActionAsync(
                adminUserId: 0,
                adminName: adminName,
                actionType: "VEHICLE_UPDATED",
                targetUserId: 0,
                ipAddress: clientIp,
                oldValues: new { vehicleId = id },
                newValues: new { description = $"{adminName} updated vehicle {vehicle.Brand} {vehicle.Model} ({vehicle.PlateNo})", status = vehicle.Status }
            );

            await _hubContext.Clients.All.SendAsync("ReceiveVehicleUpdate");
            await _hubContext.Clients.All.SendAsync("ReceiveDashboardUpdate");

            return Ok(new { Message = "Vehicle updated successfully.", VehicleId = id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteVehicle(int id)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var rentalCommand = new NpgsqlCommand(
                @"SELECT COUNT(*) FROM rentals
                  WHERE vehicle_id = @id
                    AND LOWER(COALESCE(status, '')) IN ('pending', 'approved', 'active', 'in-use')",
                connection);
            rentalCommand.Parameters.AddWithValue("@id", id);

            if (Convert.ToInt32(rentalCommand.ExecuteScalar()) > 0)
            {
                return Conflict(new { Message = "Cannot delete a vehicle with active or pending rentals." });
            }

            using var deleteCommand = new NpgsqlCommand(
                "DELETE FROM vehicles WHERE vehicle_id = @id",
                connection);
            deleteCommand.Parameters.AddWithValue("@id", id);

            if (deleteCommand.ExecuteNonQuery() == 0)
            {
                return NotFound(new { Message = "Vehicle not found." });
            }

            // System-wide audit trail logging
            string adminNameDel = GetAdminName();
            string clientIpDel = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            _ = _auditService.LogActionAsync(
                adminUserId: 0,
                adminName: adminNameDel,
                actionType: "VEHICLE_DELETED",
                targetUserId: 0,
                ipAddress: clientIpDel,
                oldValues: new { vehicleId = id },
                newValues: new { description = $"{adminNameDel} deleted vehicle ID #{id}" }
            );

            await _hubContext.Clients.All.SendAsync("ReceiveVehicleUpdate");
            await _hubContext.Clients.All.SendAsync("ReceiveDashboardUpdate");

            return Ok(new { Message = "Vehicle deleted successfully.", VehicleId = id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
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
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(
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

            await _hubContext.Clients.All.SendAsync("ReceiveVehicleUpdate");
            await _hubContext.Clients.All.SendAsync("ReceiveDashboardUpdate");

            return Ok(new { Message = "Vehicle status updated successfully.", VehicleId = id, Status = request.Status.Trim().ToLowerInvariant() });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    // GET /api/vehicles/suggest-rate — Dynamic Pricing Suggestion Engine
    [HttpGet("suggest-rate")]
    public IActionResult SuggestRate([FromQuery] decimal? baseRate, [FromQuery] int? vehicleId)
    {
        try
        {
            decimal rate = baseRate ?? 2000.00m;
            int ageYears = 0;
            string brandModel = "Vehicle";

            if (vehicleId.HasValue)
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                using var cmd = new NpgsqlCommand(
                    "SELECT rate_per_day, brand, model, created_at FROM vehicles WHERE vehicle_id = @id", conn);
                cmd.Parameters.AddWithValue("@id", vehicleId.Value);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    rate = reader["rate_per_day"] == DBNull.Value ? rate : Convert.ToDecimal(reader["rate_per_day"]);
                    brandModel = $"{reader["brand"]} {reader["model"]}";
                    if (reader["created_at"] != DBNull.Value)
                    {
                        var created = Convert.ToDateTime(reader["created_at"]);
                        ageYears = DateTime.Now.Year - created.Year;
                    }
                }
            }

            // Calculations based on actual inflation index and demand triggers
            var now = DateTime.Now;
            
            // 1. Seasonality Multiplier (e.g., Summer in PH April-May or Christmas in Dec)
            decimal seasonalityMarkup = 0;
            string seasonalityReason = "Normal Season";
            if (now.Month == 12 || now.Month == 4 || now.Month == 5)
            {
                seasonalityMarkup = rate * 0.15m; // +15%
                seasonalityReason = "Peak Season (Summer / Holiday)";
            }

            // 2. Weekend Demand Markup (Fri-Sun)
            decimal weekendMarkup = 0;
            string weekendReason = "Weekday Baseline";
            if (now.DayOfWeek == DayOfWeek.Friday || now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday)
            {
                weekendMarkup = rate * 0.10m; // +10%
                weekendReason = "High Weekend Demand";
            }

            // 3. Economy Inflation Adjustment Index (PH CPI index baseline)
            decimal inflationRate = 0.042m; // 4.2% inflation baseline
            decimal economyMarkup = rate * inflationRate;

            // 4. Age Depreciation Discount (reduces rate slightly for older models)
            decimal depreciationDiscount = Math.Min(rate * 0.10m, rate * (ageYears * 0.02m)); // max 10%

            decimal suggested = rate + seasonalityMarkup + weekendMarkup + economyMarkup - depreciationDiscount;
            
            // Round to nearest 50 pesos for convenience
            suggested = Math.Round(suggested / 50.0m) * 50.0m;

            return Ok(new {
                vehicleId,
                brandModel,
                baseRate = rate,
                suggestedRate = suggested,
                breakdown = new {
                    seasonalityMarkup = Math.Round(seasonalityMarkup, 2),
                    seasonalityReason,
                    weekendMarkup = Math.Round(weekendMarkup, 2),
                    weekendReason,
                    inflationMarkup = Math.Round(economyMarkup, 2),
                    inflationPercentage = "4.2%",
                    depreciationDiscount = Math.Round(depreciationDiscount, 2)
                },
                message = $"Suggested rental price: ₱{suggested:N2} (based on {seasonalityReason}, {weekendReason}, and inflation adjustment)."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Suggestion Engine Error: " + ex.Message });
        }
    }

    private List<VehicleDto> ReadVehicles(string? whereClause = null)
    {
        var vehicles = new List<VehicleDto>();

        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = CreateVehicleQuery(connection, whereClause);
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            vehicles.Add(MapVehicle(reader));
        }

        return vehicles;
    }

    private static NpgsqlCommand CreateVehicleQuery(NpgsqlConnection connection, string? whereClause)
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
                COALESCE(in_garage, true) AS in_garage
              FROM vehicles ";

        if (!string.IsNullOrWhiteSpace(whereClause))
        {
            sql += whereClause + " ";
        }

        sql += "ORDER BY brand ASC, model ASC";
        return new NpgsqlCommand(sql, connection);
    }

    private static VehicleDto MapVehicle(NpgsqlDataReader reader)
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
