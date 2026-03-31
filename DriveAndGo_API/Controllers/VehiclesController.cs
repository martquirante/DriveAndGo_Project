using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using DriveAndGo_API.Models;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VehiclesController : ControllerBase
    {
        private readonly string _connectionString;

        public VehiclesController(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")!;
        }

        // ══ GET ALL VEHICLES ══
        [HttpGet]
        public IActionResult GetVehicles()
        {
            try
            {
                List<Vehicle> vehicles = new List<Vehicle>();

                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // Kunin ang lahat ng sasakyan
                var cmd = new MySqlCommand(@"
                    SELECT vehicle_id, brand, model, plate_number,
                           type, daily_rate, status
                    FROM vehicles
                    ORDER BY brand ASC", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    vehicles.Add(new Vehicle
                    {
                        VehicleId = Convert.ToInt32(reader["vehicle_id"]),
                        Brand = reader["brand"].ToString(),
                        Model = reader["model"].ToString(),
                        PlateNumber = reader["plate_number"].ToString(),
                        Type = reader["type"].ToString(),
                        DailyRate = Convert.ToDecimal(reader["daily_rate"]),
                        Status = reader["status"].ToString()
                    });
                }

                return Ok(vehicles);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ GET BY ID ══
        [HttpGet("{id}")]
        public IActionResult GetVehicleById(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT vehicle_id, brand, model, plate_number,
                           type, daily_rate, status
                    FROM vehicles
                    WHERE vehicle_id = @id
                    LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return NotFound(new { message = "Vehicle not found." });

                var vehicle = new Vehicle
                {
                    VehicleId = Convert.ToInt32(reader["vehicle_id"]),
                    Brand = reader["brand"].ToString(),
                    Model = reader["model"].ToString(),
                    PlateNumber = reader["plate_number"].ToString(),
                    Type = reader["type"].ToString(),
                    DailyRate = Convert.ToDecimal(reader["daily_rate"]),
                    Status = reader["status"].ToString()
                };

                return Ok(vehicle);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ GET AVAILABLE VEHICLES ONLY — para sa mobile app ══
        [HttpGet("available")]
        public IActionResult GetAvailableVehicles()
        {
            try
            {
                List<Vehicle> vehicles = new List<Vehicle>();

                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT vehicle_id, brand, model, plate_number,
                           type, daily_rate, status
                    FROM vehicles
                    WHERE status = 'Available'
                    ORDER BY brand ASC", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    vehicles.Add(new Vehicle
                    {
                        VehicleId = Convert.ToInt32(reader["vehicle_id"]),
                        Brand = reader["brand"].ToString(),
                        Model = reader["model"].ToString(),
                        PlateNumber = reader["plate_number"].ToString(),
                        Type = reader["type"].ToString(),
                        DailyRate = Convert.ToDecimal(reader["daily_rate"]),
                        Status = reader["status"].ToString()
                    });
                }

                return Ok(vehicles);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ POST — add new vehicle ══
        [HttpPost]
        public IActionResult AddVehicle([FromBody] Vehicle newVehicle)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(newVehicle.Brand) ||
                string.IsNullOrWhiteSpace(newVehicle.Model) ||
                string.IsNullOrWhiteSpace(newVehicle.PlateNumber))
                return BadRequest(new
                {
                    message =
                    "Brand, Model, and Plate Number are required."
                });

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // Check kung existing na ang plate number
                var checkCmd = new MySqlCommand(@"
                    SELECT COUNT(*) FROM vehicles
                    WHERE plate_number = @plate", conn);
                checkCmd.Parameters.AddWithValue("@plate",
                    newVehicle.PlateNumber);
                var count = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (count > 0)
                    return Conflict(new
                    {
                        message =
                        "Plate number already exists."
                    });

                var insertCmd = new MySqlCommand(@"
                    INSERT INTO vehicles
                        (brand, model, plate_number,
                         type, daily_rate, status)
                    VALUES
                        (@brand, @model, @plate,
                         @type, @rate, 'Available')", conn);

                insertCmd.Parameters.AddWithValue("@brand", newVehicle.Brand);
                insertCmd.Parameters.AddWithValue("@model", newVehicle.Model);
                insertCmd.Parameters.AddWithValue("@plate",
                    newVehicle.PlateNumber);
                insertCmd.Parameters.AddWithValue("@type",
                    newVehicle.Type ?? "Car");
                insertCmd.Parameters.AddWithValue("@rate", newVehicle.DailyRate);

                insertCmd.ExecuteNonQuery();

                var idCmd = new MySqlCommand(
                    "SELECT LAST_INSERT_ID()", conn);
                int newId = Convert.ToInt32(idCmd.ExecuteScalar());

                return Ok(new
                {
                    message = "Vehicle added successfully.",
                    vehicle_id = newId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ PUT — update vehicle info ══
        [HttpPut("{id}")]
        public IActionResult UpdateVehicle(int id,
            [FromBody] Vehicle updated)
        {
            if (string.IsNullOrWhiteSpace(updated.Brand) ||
                string.IsNullOrWhiteSpace(updated.Model) ||
                string.IsNullOrWhiteSpace(updated.PlateNumber))
                return BadRequest(new
                {
                    message =
                    "Brand, Model, and Plate Number are required."
                });

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // Check kung existing ang vehicle
                var checkCmd = new MySqlCommand(@"
                    SELECT COUNT(*) FROM vehicles
                    WHERE vehicle_id = @id", conn);
                checkCmd.Parameters.AddWithValue("@id", id);
                var exists = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (exists == 0)
                    return NotFound(new { message = "Vehicle not found." });

                // Check kung ang bagong plate number ay hindi
                // ginagamit ng ibang sasakyan
                var plateCmd = new MySqlCommand(@"
                    SELECT COUNT(*) FROM vehicles
                    WHERE plate_number = @plate
                    AND vehicle_id != @id", conn);
                plateCmd.Parameters.AddWithValue("@plate",
                    updated.PlateNumber);
                plateCmd.Parameters.AddWithValue("@id", id);
                var plateCount = Convert.ToInt32(plateCmd.ExecuteScalar());

                if (plateCount > 0)
                    return Conflict(new
                    {
                        message =
                        "Plate number is already used by another vehicle."
                    });

                var updateCmd = new MySqlCommand(@"
                    UPDATE vehicles
                    SET brand        = @brand,
                        model        = @model,
                        plate_number = @plate,
                        type         = @type,
                        daily_rate   = @rate,
                        status       = @status
                    WHERE vehicle_id = @id", conn);

                updateCmd.Parameters.AddWithValue("@brand", updated.Brand);
                updateCmd.Parameters.AddWithValue("@model", updated.Model);
                updateCmd.Parameters.AddWithValue("@plate",
                    updated.PlateNumber);
                updateCmd.Parameters.AddWithValue("@type",
                    updated.Type ?? "Car");
                updateCmd.Parameters.AddWithValue("@rate", updated.DailyRate);
                updateCmd.Parameters.AddWithValue("@status",
                    updated.Status ?? "Available");
                updateCmd.Parameters.AddWithValue("@id", id);

                updateCmd.ExecuteNonQuery();

                return Ok(new
                {
                    message = "Vehicle updated successfully.",
                    vehicle_id = id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ DELETE — remove vehicle ══
        [HttpDelete("{id}")]
        public IActionResult DeleteVehicle(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // Check kung may active or pending rental ang sasakyan
                var rentalCmd = new MySqlCommand(@"
                    SELECT COUNT(*) FROM rentals
                    WHERE vehicle_id = @id
                    AND status IN ('pending', 'approved')", conn);
                rentalCmd.Parameters.AddWithValue("@id", id);
                var activeRentals = Convert.ToInt32(
                    rentalCmd.ExecuteScalar());

                if (activeRentals > 0)
                    return Conflict(new
                    {
                        message =
                        "Cannot delete — vehicle has active or pending rentals."
                    });

                var deleteCmd = new MySqlCommand(@"
                    DELETE FROM vehicles
                    WHERE vehicle_id = @id", conn);
                deleteCmd.Parameters.AddWithValue("@id", id);
                int affected = deleteCmd.ExecuteNonQuery();

                if (affected == 0)
                    return NotFound(new { message = "Vehicle not found." });

                return Ok(new
                {
                    message = "Vehicle deleted successfully.",
                    vehicle_id = id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ PATCH STATUS — update vehicle status only ══
        [HttpPatch("{id}/status")]
        public IActionResult UpdateStatus(int id,
            [FromBody] UpdateStatusRequest request)
        {
            var validStatuses = new[] {
                "Available", "Rented", "Maintenance", "Retired" };

            if (!validStatuses.Contains(request.Status))
                return BadRequest(new
                {
                    message =
                    "Valid statuses: Available, Rented, Maintenance, Retired"
                });

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var checkCmd = new MySqlCommand(@"
                    SELECT COUNT(*) FROM vehicles
                    WHERE vehicle_id = @id", conn);
                checkCmd.Parameters.AddWithValue("@id", id);
                var exists = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (exists == 0)
                    return NotFound(new { message = "Vehicle not found." });

                var updateCmd = new MySqlCommand(@"
                    UPDATE vehicles
                    SET status = @status
                    WHERE vehicle_id = @id", conn);
                updateCmd.Parameters.AddWithValue("@status", request.Status);
                updateCmd.Parameters.AddWithValue("@id", id);
                updateCmd.ExecuteNonQuery();

                return Ok(new
                {
                    message = $"Vehicle status updated to '{request.Status}'.",
                    vehicle_id = id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }
    }
}