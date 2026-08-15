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
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult GetVehicles()
    {
        try
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            return Ok(ReadVehicles());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpGet("fleet")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult GetFleetVehicles()
    {
        try
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            var fleet = new List<DriveAndGo_API.Models.VehicleFleetDto>();
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(
                @"SELECT vehicle_id, brand, model, plate_no, type, rate_per_day, status, photo_url, latitude, longitude, COALESCE(color, 'Pearl White') AS color 
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
                    Longitude = reader["longitude"] == DBNull.Value ? null : Convert.ToDouble(reader["longitude"]),
                    Color = reader["color"]?.ToString() ?? "Pearl White"
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
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult GetVehicleById(int id)
    {
        try
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = CreateVehicleQuery(connection, "WHERE vehicle_id = @id");
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
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult GetAvailableVehicles()
    {
        try
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
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

        if (vehicle.VehicleId > 0)
        {
            return await UpdateVehicle(vehicle.VehicleId, vehicle);
        }

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            // Duplicate plate check for POST
            using var duplicateCommand = new NpgsqlCommand(
                @"SELECT vehicle_id, brand, model FROM vehicles 
                  WHERE REPLACE(LOWER(TRIM(plate_no)), '-', '') = REPLACE(LOWER(TRIM(@plate_no)), '-', '') 
                  LIMIT 1",
                connection);
            duplicateCommand.Parameters.AddWithValue("@plate_no", vehicle.PlateNo.Trim());

            using (var dupReader = duplicateCommand.ExecuteReader())
            {
                if (dupReader.Read())
                {
                    int dupId = dupReader.GetInt32(0);
                    dupReader.Close();
                    return await UpdateVehicle(dupId, vehicle);
                }
            }

            using var insertCommand = new NpgsqlCommand(
                @"INSERT INTO vehicles
                    (plate_no, brand, model, type, cc, status, rate_per_day, rate_with_driver, photo_url, description,
                     seat_capacity, transmission, model_3d_url, created_at, latitude, longitude, current_speed, last_update, in_garage,
                     lto_expiry_date, insurance_expiry_date, or_cr_url, insurance_url, color)
                  VALUES
                    (@plate_no, @brand, @model, @type, @cc, @status, @rate_per_day, @rate_with_driver, @photo_url, @description,
                     @seat_capacity, @transmission, @model_3d_url, @created_at, @latitude, @longitude, @current_speed, @last_update, @in_garage,
                     @lto_expiry_date, @insurance_expiry_date, @or_cr_url, @insurance_url, @color)
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
            insertCommand.Parameters.AddWithValue("@lto_expiry_date", vehicle.LtoExpiryDate.HasValue ? vehicle.LtoExpiryDate.Value : DBNull.Value);
            insertCommand.Parameters.AddWithValue("@insurance_expiry_date", vehicle.InsuranceExpiryDate.HasValue ? vehicle.InsuranceExpiryDate.Value : DBNull.Value);
            insertCommand.Parameters.AddWithValue("@or_cr_url", string.IsNullOrWhiteSpace(vehicle.OrCrUrl) ? "" : vehicle.OrCrUrl.Trim());
            insertCommand.Parameters.AddWithValue("@insurance_url", string.IsNullOrWhiteSpace(vehicle.InsuranceUrl) ? "" : vehicle.InsuranceUrl.Trim());
            insertCommand.Parameters.AddWithValue("@color", string.IsNullOrWhiteSpace(vehicle.Color) ? "Pearl White" : vehicle.Color.Trim());

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
        if (id <= 0)
        {
            return BadRequest(new { Message = "Invalid vehicle ID." });
        }

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

            int targetVehicleId = id;

            // Plate resolution check
            using (var duplicateCommand = new NpgsqlCommand(
                @"SELECT vehicle_id, brand, model FROM vehicles
                  WHERE REPLACE(LOWER(TRIM(plate_no)), '-', '') = REPLACE(LOWER(TRIM(@plate_no)), '-', '') 
                  LIMIT 1",
                connection))
            {
                duplicateCommand.Parameters.AddWithValue("@plate_no", vehicle.PlateNo.Trim());

                using var dupReader = duplicateCommand.ExecuteReader();
                if (dupReader.Read())
                {
                    int dupId = dupReader.GetInt32(0);
                    targetVehicleId = dupId;
                }
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
                      in_garage = @in_garage,
                      lto_expiry_date = @lto_expiry_date,
                      insurance_expiry_date = @insurance_expiry_date,
                       or_cr_url = @or_cr_url,
                       insurance_url = @insurance_url,
                       color = @color
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
            updateCommand.Parameters.AddWithValue("@lto_expiry_date", vehicle.LtoExpiryDate.HasValue ? vehicle.LtoExpiryDate.Value : DBNull.Value);
            updateCommand.Parameters.AddWithValue("@insurance_expiry_date", vehicle.InsuranceExpiryDate.HasValue ? vehicle.InsuranceExpiryDate.Value : DBNull.Value);
            updateCommand.Parameters.AddWithValue("@or_cr_url", string.IsNullOrWhiteSpace(vehicle.OrCrUrl) ? "" : vehicle.OrCrUrl.Trim());
            updateCommand.Parameters.AddWithValue("@insurance_url", string.IsNullOrWhiteSpace(vehicle.InsuranceUrl) ? "" : vehicle.InsuranceUrl.Trim());
            updateCommand.Parameters.AddWithValue("@color", string.IsNullOrWhiteSpace(vehicle.Color) ? "Pearl White" : vehicle.Color.Trim());
            updateCommand.Parameters.AddWithValue("@id", targetVehicleId);

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
    public async Task<IActionResult> DeleteVehicle(int id, [FromQuery] string? reason = null, [FromQuery] string? notes = null, [FromBody] DecommissionVehicleRequest? bodyReq = null)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            string vehicleBrand = "";
            string vehicleModel = "";
            string vehiclePlate = "";

            using (var infoCmd = new NpgsqlCommand("SELECT brand, model, plate_no FROM vehicles WHERE vehicle_id = @id", connection))
            {
                infoCmd.Parameters.AddWithValue("@id", id);
                using var infoReader = infoCmd.ExecuteReader();
                if (infoReader.Read())
                {
                    vehicleBrand = infoReader["brand"]?.ToString() ?? "";
                    vehicleModel = infoReader["model"]?.ToString() ?? "";
                    vehiclePlate = infoReader["plate_no"]?.ToString() ?? "";
                }
            }

            using var rentalCommand = new NpgsqlCommand(
                @"SELECT COUNT(*) FROM rentals
                  WHERE vehicle_id = @id
                    AND LOWER(COALESCE(status, '')) IN ('pending', 'approved', 'active', 'in-use')",
                connection);
            rentalCommand.Parameters.AddWithValue("@id", id);

            if (Convert.ToInt32(rentalCommand.ExecuteScalar()) > 0)
            {
                return Conflict(new { Message = "Cannot decommission or delete a vehicle with active or pending rentals." });
            }

            using var tx = connection.BeginTransaction();
            try
            {
                void SafeDelete(string table, string column, int vid)
                {
                    using var chk = new NpgsqlCommand(
                        "SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @tbl AND column_name = @col", connection, tx);
                    chk.Parameters.AddWithValue("@tbl", table);
                    chk.Parameters.AddWithValue("@col", column);
                    if (chk.ExecuteScalar() != null)
                    {
                        using var del = new NpgsqlCommand($"DELETE FROM {table} WHERE {column} = @id", connection, tx);
                        del.Parameters.AddWithValue("@id", vid);
                        del.ExecuteNonQuery();
                    }
                }

                void SafeDeleteByRental(string table)
                {
                    using var chk = new NpgsqlCommand(
                        @"SELECT 1 FROM information_schema.columns 
                          WHERE table_schema = 'public' AND table_name = @tbl AND column_name = 'rental_id'", connection, tx);
                    chk.Parameters.AddWithValue("@tbl", table);
                    if (chk.ExecuteScalar() != null)
                    {
                        using var del = new NpgsqlCommand(
                            $"DELETE FROM {table} WHERE rental_id IN (SELECT rental_id FROM rentals WHERE vehicle_id = @id)", connection, tx);
                        del.Parameters.AddWithValue("@id", id);
                        del.ExecuteNonQuery();
                    }
                }

                // First delete all child records referencing rentals by rental_id
                SafeDeleteByRental("gps_logs");
                SafeDeleteByRental("location_logs");
                SafeDeleteByRental("fuel_logs");
                SafeDeleteByRental("vehicle_telematics");
                SafeDeleteByRental("payments");
                SafeDeleteByRental("invoices");
                SafeDeleteByRental("refunds");
                SafeDeleteByRental("rental_documents");
                SafeDeleteByRental("reviews");
                SafeDeleteByRental("transactions");
                SafeDeleteByRental("extensions");
                SafeDeleteByRental("issues");
                SafeDeleteByRental("messages");
                SafeDeleteByRental("ratings");

                // Then delete all child records referencing vehicles by vehicle_id
                SafeDelete("location_logs", "vehicle_id", id);
                SafeDelete("gps_logs", "vehicle_id", id);
                SafeDelete("fuel_logs", "vehicle_id", id);
                SafeDelete("vehicle_maintenance", "vehicle_id", id);
                SafeDelete("damage_claims", "vehicle_id", id);
                SafeDelete("expenses", "vehicle_id", id);
                SafeDelete("vehicle_telematics", "vehicle_id", id);

                // Finally delete rentals and the vehicle itself
                SafeDelete("rentals", "vehicle_id", id);

                using (var cleanCmd = new NpgsqlCommand("DELETE FROM vehicles WHERE vehicle_id = @id", connection, tx))
                {
                    cleanCmd.Parameters.AddWithValue("@id", id);
                    int affected = cleanCmd.ExecuteNonQuery();
                    if (affected == 0)
                    {
                        tx.Rollback();
                        return NotFound(new { Message = "Vehicle not found." });
                    }
                }
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            string finalReason = !string.IsNullOrWhiteSpace(bodyReq?.Reason) ? bodyReq.Reason : (!string.IsNullOrWhiteSpace(reason) ? reason : "Unit Sold / Liquidated");
            string finalNotes = !string.IsNullOrWhiteSpace(bodyReq?.Notes) ? bodyReq.Notes : (!string.IsNullOrWhiteSpace(notes) ? notes : "");

            // System-wide audit trail logging
            string adminNameDel = GetAdminName();
            string clientIpDel = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            _ = _auditService.LogActionAsync(
                adminUserId: 0,
                adminName: adminNameDel,
                actionType: "VEHICLE_DECOMMISSIONED",
                targetUserId: 0,
                ipAddress: clientIpDel,
                oldValues: new { vehicleId = id, brand = vehicleBrand, model = vehicleModel, plateNo = vehiclePlate },
                newValues: new { reason = finalReason, notes = finalNotes, description = $"{adminNameDel} decommissioned vehicle {vehicleBrand} {vehicleModel} ({vehiclePlate}). Reason: {finalReason}" }
            );

            await _hubContext.Clients.All.SendAsync("ReceiveVehicleUpdate");
            await _hubContext.Clients.All.SendAsync("ReceiveDashboardUpdate");

            return Ok(new { 
                Message = $"Vehicle {vehicleBrand} {vehicleModel} ({vehiclePlate}) decommissioned successfully.", 
                VehicleId = id,
                Brand = vehicleBrand,
                Model = vehicleModel,
                PlateNo = vehiclePlate,
                Reason = finalReason,
                Notes = finalNotes,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    public class DecommissionVehicleRequest
    {
        public string? Reason { get; set; } = "Unit Sold / Liquidated";
        public string? Notes { get; set; } = "";
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

    // POST /api/vehicles/{id}/telematics/command — Remote Telematics Action
    [HttpPost("{id:int}/telematics/command")]
    public async Task<IActionResult> TelematicsCommand(int id, [FromBody] TelematicsCommandRequest request)
    {
        var validCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "lock", "unlock", "immobilize", "release_immobilize", "honk_flash", "locate_ping"
        };

        if (request == null || string.IsNullOrWhiteSpace(request.Command))
            return BadRequest(new { Message = "Command is required." });

        if (!validCommands.Contains(request.Command))
            return BadRequest(new { Message = "Valid commands: lock, unlock, immobilize, release_immobilize, honk_flash, locate_ping" });

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            string cmd             = request.Command.ToLowerInvariant();
            bool?  newLocked       = null;
            string newEngineStatus = null;

            if (cmd == "lock" || cmd == "unlock")
            {
                newLocked = cmd == "lock";
                using var upd = new NpgsqlCommand(
                    "UPDATE vehicles SET telematics_locked = @locked WHERE vehicle_id = @id", connection);
                upd.Parameters.AddWithValue("@locked", newLocked.Value);
                upd.Parameters.AddWithValue("@id", id);
                upd.ExecuteNonQuery();
            }
            else if (cmd == "immobilize" || cmd == "release_immobilize")
            {
                newEngineStatus = cmd == "immobilize" ? "immobilized" : "off";
                using var upd = new NpgsqlCommand(
                    "UPDATE vehicles SET engine_status = @status WHERE vehicle_id = @id", connection);
                upd.Parameters.AddWithValue("@status", newEngineStatus);
                upd.Parameters.AddWithValue("@id", id);
                upd.ExecuteNonQuery();
            }

            // Build granular diff payload — React merges only changed fields
            var commandPayload = new
            {
                vehicleId       = id,
                command         = cmd,
                timestamp       = DateTime.UtcNow,
                issuedBy        = GetAdminName(),
                telematicsLocked = newLocked,
                engineStatus    = newEngineStatus
            };

            // TelematicsCommand — clients flip lock/immobilize icons immediately
            await _hubContext.Clients.All.SendAsync("TelematicsCommand", commandPayload);

            // Also emit a TelematicsUpdated so Inspector drawer refreshes live values
            if (newLocked.HasValue || newEngineStatus != null)
            {
                await _hubContext.Clients.All.SendAsync("TelematicsUpdated", new
                {
                    vehicleId       = id,
                    telematicsLocked = newLocked,
                    engineStatus    = newEngineStatus,
                    timestamp       = DateTime.UtcNow
                });
            }

            string adminName = GetAdminName();
            string clientIp  = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            _ = _auditService.LogActionAsync(
                adminUserId: 0,
                adminName: adminName,
                actionType: "TELEMATICS_COMMAND",
                targetUserId: 0,
                ipAddress: clientIp,
                oldValues: new { vehicleId = id },
                newValues: new { command = cmd, description = $"{adminName} executed '{cmd}' on vehicle #{id}" }
            );

            return Ok(new { Message = $"Telematics command '{cmd}' sent to vehicle #{id}.", VehicleId = id, Command = cmd, Timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Telematics Error: " + ex.Message });
        }
    }

    // PUT /api/vehicles/{id}/telematics — Update Telemetry Data (GPS, fuel, health, etc.)
    [HttpPut("{id:int}/telematics")]
    public async Task<IActionResult> UpdateTelematics(int id, [FromBody] TelematicsUpdateRequest request)
    {
        if (request == null) return BadRequest(new { Message = "Request body is required." });

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(@"
                UPDATE vehicles SET
                    fuel_level_pct        = COALESCE(@fuel_level_pct, fuel_level_pct),
                    odometer_km           = COALESCE(@odometer_km, odometer_km),
                    health_score          = COALESCE(@health_score, health_score),
                    engine_status         = COALESCE(@engine_status, engine_status),
                    safety_score          = COALESCE(@safety_score, safety_score),
                    idle_minutes          = COALESCE(@idle_minutes, idle_minutes),
                    rfid_balance_autosweep = COALESCE(@rfid_balance_autosweep, rfid_balance_autosweep),
                    rfid_balance_easytrip  = COALESCE(@rfid_balance_easytrip, rfid_balance_easytrip),
                    lto_expiry_date       = COALESCE(@lto_expiry_date, lto_expiry_date),
                    insurance_expiry_date = COALESCE(@insurance_expiry_date, insurance_expiry_date),
                    latitude              = COALESCE(@latitude, latitude),
                    longitude             = COALESCE(@longitude, longitude),
                    last_update           = NOW()
                WHERE vehicle_id = @id", connection);

            cmd.Parameters.AddWithValue("@id",                    id);
            cmd.Parameters.AddWithValue("@fuel_level_pct",        request.FuelLevelPct.HasValue ? request.FuelLevelPct.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@odometer_km",           request.OdometerKm.HasValue ? request.OdometerKm.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@health_score",          request.HealthScore.HasValue ? request.HealthScore.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@engine_status",         string.IsNullOrWhiteSpace(request.EngineStatus) ? DBNull.Value : request.EngineStatus);
            cmd.Parameters.AddWithValue("@safety_score",          request.SafetyScore.HasValue ? request.SafetyScore.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@idle_minutes",          request.IdleMinutes.HasValue ? request.IdleMinutes.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@rfid_balance_autosweep",request.RfidBalanceAutosweep.HasValue ? request.RfidBalanceAutosweep.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@rfid_balance_easytrip", request.RfidBalanceEasytrip.HasValue ? request.RfidBalanceEasytrip.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@lto_expiry_date",       request.LtoExpiryDate.HasValue ? request.LtoExpiryDate.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@insurance_expiry_date", request.InsuranceExpiryDate.HasValue ? request.InsuranceExpiryDate.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@latitude",              request.Latitude.HasValue ? request.Latitude.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@longitude",             request.Longitude.HasValue ? request.Longitude.Value : DBNull.Value);

            if (cmd.ExecuteNonQuery() == 0)
                return NotFound(new { Message = "Vehicle not found." });

            // Emit granular TelematicsUpdated diff — React merges only changed fields
            await _hubContext.Clients.All.SendAsync("TelematicsUpdated", new
            {
                vehicleId          = id,
                fuelLevelPct       = request.FuelLevelPct,
                healthScore        = request.HealthScore,
                engineStatus       = request.EngineStatus,
                safetyScore        = request.SafetyScore,
                idleMinutes        = request.IdleMinutes,
                odometerKm         = request.OdometerKm,
                rfidBalanceAutosweep = request.RfidBalanceAutosweep,
                rfidBalanceEasytrip  = request.RfidBalanceEasytrip,
                latitude           = request.Latitude,
                longitude          = request.Longitude,
                timestamp          = DateTime.UtcNow
            });

            // Also fire broad update for list refresh on other panels
            await _hubContext.Clients.All.SendAsync("ReceiveVehicleUpdate");

            return Ok(new { Message = "Telemetry updated successfully.", VehicleId = id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Telemetry Update Error: " + ex.Message });
        }
    }

    // GET /api/vehicles/suggest-rate — Comprehensive AI Dynamic Pricing Engine
    [HttpGet("suggest-rate")]
    public IActionResult SuggestRate(
        [FromQuery] decimal? baseRate, 
        [FromQuery] int? vehicleId,
        [FromQuery] string? type,
        [FromQuery] string? transmission,
        [FromQuery] int? engineCc,
        [FromQuery] int? seatingCapacity,
        [FromQuery] int? odometerKm)
    {
        try
        {
            decimal rate = baseRate ?? 2000.00m;
            int ageYears = 0;
            string brandModel = "Vehicle";
            string vType = type ?? "";
            string vTrans = transmission ?? "";
            int vCc = engineCc ?? 1500;
            int vSeats = seatingCapacity ?? 5;
            int vOdo = odometerKm ?? 30000;

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            if (vehicleId.HasValue)
            {
                using var cmd = new NpgsqlCommand(
                    @"SELECT rate_per_day, brand, model, type, transmission, engine_cc, seating_capacity, odometer_km, created_at 
                      FROM vehicles WHERE vehicle_id = @id", conn);
                cmd.Parameters.AddWithValue("@id", vehicleId.Value);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    rate = reader["rate_per_day"] == DBNull.Value ? rate : Convert.ToDecimal(reader["rate_per_day"]);
                    brandModel = $"{reader["brand"]} {reader["model"]}";
                    vType = reader["type"]?.ToString() ?? vType;
                    vTrans = reader["transmission"]?.ToString() ?? vTrans;
                    vCc = reader["engine_cc"] == DBNull.Value ? vCc : Convert.ToInt32(reader["engine_cc"]);
                    vSeats = reader["seating_capacity"] == DBNull.Value ? vSeats : Convert.ToInt32(reader["seating_capacity"]);
                    vOdo = reader["odometer_km"] == DBNull.Value ? vOdo : Convert.ToInt32(reader["odometer_km"]);

                    if (reader["created_at"] != DBNull.Value)
                    {
                        var created = Convert.ToDateTime(reader["created_at"]);
                        ageYears = DateTime.Now.Year - created.Year;
                    }
                }
            }

            // 1. Real-time Fleet Occupancy & Demand Rate
            int totalVehicles = 0;
            int rentedVehicles = 0;
            using (var utilCmd = new NpgsqlCommand("SELECT COUNT(*), COUNT(*) FILTER (WHERE LOWER(COALESCE(status, '')) IN ('rented', 'active', 'in-use')) FROM vehicles", conn))
            using (var utilReader = utilCmd.ExecuteReader())
            {
                if (utilReader.Read())
                {
                    totalVehicles = utilReader.GetInt32(0);
                    rentedVehicles = utilReader.GetInt32(1);
                }
            }

            decimal utilizationPct = totalVehicles > 0 ? ((decimal)rentedVehicles / totalVehicles) * 100m : 0m;
            decimal demandMarkup = 0m;
            string demandReason = "Normal Fleet Capacity";

            if (utilizationPct >= 80m)
            {
                demandMarkup = rate * 0.15m;
                demandReason = $"High Fleet Occupancy ({utilizationPct:F0}% booked, +15% surge)";
            }
            else if (utilizationPct >= 50m)
            {
                demandMarkup = rate * 0.08m;
                demandReason = $"Moderate Fleet Occupancy ({utilizationPct:F0}% booked, +8% surge)";
            }

            // 2. Philippine Ber Months & Holiday Seasonality
            var now = DateTime.Now;
            int month = now.Month;
            decimal seasonalityMarkup = 0m;
            string seasonalityReason = "Standard Season";

            if (month == 12)
            {
                seasonalityMarkup = rate * 0.25m;
                seasonalityReason = "December Grand Christmas & New Year Peak (+25%)";
            }
            else if (month >= 9 && month <= 11)
            {
                seasonalityMarkup = rate * 0.15m;
                seasonalityReason = $"Ber Month ({now:MMMM}) Holiday Season (+15%)";
            }
            else if (month == 4 || month == 5)
            {
                seasonalityMarkup = rate * 0.18m;
                seasonalityReason = "PH Summer & Holy Week Vacation Peak (+18%)";
            }
            else if (month == 1)
            {
                seasonalityMarkup = rate * 0.08m;
                seasonalityReason = "January Post-Holiday Travel Demand (+8%)";
            }

            // 3. Transmission & Comfort Premium (Automatic vs Manual)
            decimal transmissionMarkup = 0m;
            string transmissionReason = "Manual Transmission Baseline";
            if (vTrans.ToLower().Contains("auto") || vTrans.ToLower().Contains("at") || vTrans.ToLower().Contains("cvt"))
            {
                transmissionMarkup = rate * 0.08m;
                transmissionReason = "Automatic Transmission City Comfort Premium (+8%)";
            }

            // 4. Seating Capacity / Family Group Multiplier
            decimal seatingMarkup = 0m;
            string seatingReason = "Standard 5-Seater Capacity";
            if (vSeats >= 7)
            {
                seatingMarkup = rate * 0.12m;
                seatingReason = $"{vSeats}-Seater Large Group / Family Capacity (+12%)";
            }

            // 5. High Ground Clearance & Vehicle Type Factor
            decimal typeMarkup = 0m;
            string typeReason = "Standard Sedan/Compact Category";
            string tLower = vType.ToLower();
            if (tLower.Contains("suv") || tLower.Contains("pickup") || tLower.Contains("4x4"))
            {
                typeMarkup = rate * 0.10m;
                typeReason = "High-Clearance SUV/Pickup All-Terrain Premium (+10%)";
            }
            else if (tLower.Contains("van"))
            {
                typeMarkup = rate * 0.15m;
                typeReason = "Commercial Passenger Van Capacity Premium (+15%)";
            }

            // 6. Odometer Wear Index
            decimal odoAdjustment = 0m;
            string odoReason = "Standard Odometer Mileage";
            if (vOdo < 35000 && vOdo > 0)
            {
                odoAdjustment = rate * 0.05m;
                odoReason = $"Pristine Low-Mileage Unit ({vOdo:N0} km, +5% premium)";
            }
            else if (vOdo > 120000)
            {
                odoAdjustment = -(rate * 0.08m);
                odoReason = $"High Mileage Fair Use Discount ({vOdo:N0} km, -8% discount)";
            }

            // 7. Weekend Surge (Fri-Sun)
            decimal weekendMarkup = 0m;
            string weekendReason = "Weekday Baseline";
            if (now.DayOfWeek == DayOfWeek.Friday || now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday)
            {
                weekendMarkup = rate * 0.10m;
                weekendReason = $"{now.DayOfWeek} Weekend Travel Demand (+10%)";
            }

            // 8. PH Inflation Index (4.2% baseline)
            decimal inflationRate = 0.042m;
            decimal economyMarkup = rate * inflationRate;

            // 9. MMDA Number Coding Exemption Advantage
            decimal codingMarkup = 0m;
            string codingReason = "Regular Plate Coding Schedule";
            bool isCodingFreeToday = true;
            if (vehicleId.HasValue && brandModel != "Vehicle")
            {
                // Check if EV or Hybrid or not coded on current day
                if (tLower.Contains("hybrid") || tLower.Contains("ev") || tLower.Contains("electric"))
                {
                    codingMarkup = rate * 0.08m;
                    codingReason = "EV/Hybrid Exempt from MMDA Number Coding (+8%)";
                }
                else
                {
                    codingMarkup = rate * 0.04m;
                    codingReason = "Active Coding-Free Day Advantage (+4%)";
                }
            }

            // 10. Fuel Type & Power Index
            decimal fuelMarkup = 0m;
            string fuelReason = "Standard Gasoline Baseline";
            if (tLower.Contains("diesel"))
            {
                fuelMarkup = rate * 0.05m;
                fuelReason = "High-Torque Fuel-Efficient Diesel Unit (+5%)";
            }

            // 11. Chauffeur Driver Shift Rate (Base + Night Differential)
            decimal suggestedDriverRate = 1200m;
            if (now.Hour >= 22 || now.Hour <= 5)
            {
                suggestedDriverRate += 300m; // Night shift differential
            }

            // 12. Age Depreciation Discount
            decimal depreciationDiscount = Math.Min(rate * 0.10m, rate * (ageYears * 0.02m));

            decimal suggested = rate + seasonalityMarkup + demandMarkup + weekendMarkup + transmissionMarkup + seatingMarkup + typeMarkup + odoAdjustment + codingMarkup + fuelMarkup + economyMarkup - depreciationDiscount;
            suggested = Math.Max(500m, Math.Round(suggested / 50.0m) * 50.0m);

            // 13. Tiered Duration Bulk Pricing Calculations
            decimal rate3Day = Math.Round(suggested * 0.95m / 50.0m) * 50.0m;   // -5%
            decimal rate7Day = Math.Round(suggested * 0.88m / 50.0m) * 50.0m;   // -12%
            decimal rate30Day = Math.Round(suggested * 0.75m / 50.0m) * 50.0m;  // -25%

            return Ok(new {
                vehicleId,
                brandModel,
                baseRate = rate,
                suggestedRate = suggested,
                suggestedRateWithDriver = suggested + suggestedDriverRate,
                bulkTierRates = new {
                    daily3DayRate = rate3Day,
                    daily7DayWeeklyRate = rate7Day,
                    daily30DayMonthlyRate = rate30Day
                },
                breakdown = new {
                    seasonalityMarkup = Math.Round(seasonalityMarkup, 2),
                    seasonalityReason,
                    demandMarkup = Math.Round(demandMarkup, 2),
                    demandReason,
                    weekendMarkup = Math.Round(weekendMarkup, 2),
                    weekendReason,
                    transmissionMarkup = Math.Round(transmissionMarkup, 2),
                    transmissionReason,
                    seatingMarkup = Math.Round(seatingMarkup, 2),
                    seatingReason,
                    typeMarkup = Math.Round(typeMarkup, 2),
                    typeReason,
                    codingMarkup = Math.Round(codingMarkup, 2),
                    codingReason,
                    fuelMarkup = Math.Round(fuelMarkup, 2),
                    fuelReason,
                    odoAdjustment = Math.Round(odoAdjustment, 2),
                    odoReason,
                    inflationMarkup = Math.Round(economyMarkup, 2),
                    inflationPercentage = "4.2%",
                    depreciationDiscount = Math.Round(depreciationDiscount, 2),
                    fleetUtilizationPct = Math.Round(utilizationPct, 1),
                    driverRatePerDay = suggestedDriverRate
                },
                message = $"Suggested rental price: ₱{suggested:N2} (Factors: {seasonalityReason}, {demandReason}, {typeReason}, {codingReason}, & 4.2% PH Inflation Index)."
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
                COALESCE(in_garage, true) AS in_garage,
                COALESCE(fuel_level_pct, 100) AS fuel_level_pct,
                COALESCE(odometer_km, 0) AS odometer_km,
                COALESCE(health_score, 98) AS health_score,
                COALESCE(engine_status, 'off') AS engine_status,
                COALESCE(maintenance_due_km, 5000) AS maintenance_due_km,
                COALESCE(telematics_locked, true) AS telematics_locked,
                lto_expiry_date,
                insurance_expiry_date,
                COALESCE(or_cr_url, '') AS or_cr_url,
                COALESCE(insurance_url, '') AS insurance_url,
                COALESCE(safety_score, 95) AS safety_score,
                COALESCE(idle_minutes, 0) AS idle_minutes,
                COALESCE(rfid_balance_autosweep, 500.00) AS rfid_balance_autosweep,
                COALESCE(rfid_balance_easytrip, 500.00) AS rfid_balance_easytrip,
                COALESCE(rfid_balance_easytrip, 500.00) AS rfid_balance_easytrip,
                COALESCE(color, 'Pearl White') AS color,
                COALESCE(flood_risk_status, 'safe') AS flood_risk_status,
                COALESCE(engine_water_ingress_alert, false) AS engine_water_ingress_alert,
                COALESCE(last_weather_temp, 28.5) AS last_weather_temp
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
            InGarage = reader["in_garage"] != DBNull.Value && Convert.ToBoolean(reader["in_garage"]),
            // --- Fleet Telematics Fields ---
            FuelLevelPct     = reader["fuel_level_pct"] == DBNull.Value ? 100 : Convert.ToInt32(reader["fuel_level_pct"]),
            OdometerKm       = reader["odometer_km"] == DBNull.Value ? 0 : Convert.ToInt32(reader["odometer_km"]),
            HealthScore      = reader["health_score"] == DBNull.Value ? 98 : Convert.ToInt32(reader["health_score"]),
            EngineStatus     = reader["engine_status"]?.ToString() ?? "off",
            MaintenanceDueKm = reader["maintenance_due_km"] == DBNull.Value ? 5000 : Convert.ToInt32(reader["maintenance_due_km"]),
            TelematicsLocked = reader["telematics_locked"] != DBNull.Value && Convert.ToBoolean(reader["telematics_locked"]),
            LtoExpiryDate    = reader["lto_expiry_date"] == DBNull.Value ? null : Convert.ToDateTime(reader["lto_expiry_date"]),
            InsuranceExpiryDate = reader["insurance_expiry_date"] == DBNull.Value ? null : Convert.ToDateTime(reader["insurance_expiry_date"]),
            OrCrUrl          = reader["or_cr_url"]?.ToString() ?? string.Empty,
            InsuranceUrl     = reader["insurance_url"]?.ToString() ?? string.Empty,
            SafetyScore      = reader["safety_score"] == DBNull.Value ? 95 : Convert.ToInt32(reader["safety_score"]),
            IdleMinutes      = reader["idle_minutes"] == DBNull.Value ? 0 : Convert.ToInt32(reader["idle_minutes"]),
            RfidBalanceAutosweep = reader["rfid_balance_autosweep"] == DBNull.Value ? 500m : Convert.ToDecimal(reader["rfid_balance_autosweep"]),
            RfidBalanceEasytrip  = reader["rfid_balance_easytrip"] == DBNull.Value ? 500m : Convert.ToDecimal(reader["rfid_balance_easytrip"]),
            Color                = reader["color"]?.ToString() ?? "Pearl White",
            FloodRiskStatus      = reader["flood_risk_status"]?.ToString() ?? "safe",
            EngineWaterIngressAlert = reader["engine_water_ingress_alert"] != DBNull.Value && Convert.ToBoolean(reader["engine_water_ingress_alert"]),
            LastWeatherTemp      = reader["last_weather_temp"] == DBNull.Value ? 28.5m : Convert.ToDecimal(reader["last_weather_temp"])
        };
    }
}
