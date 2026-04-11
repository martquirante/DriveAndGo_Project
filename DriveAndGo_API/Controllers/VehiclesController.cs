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
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        // ══ GET ALL VEHICLES ══
        [HttpGet]
        public IActionResult GetVehicles()
        {
            try
            {
                var vehicles = new List<object>();

                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT
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
                        COALESCE(seat_capacity, 5) AS seat_capacity,
                        COALESCE(transmission, 'Automatic') AS transmission,
                        created_at,
                        latitude,
                        longitude,
                        current_speed,
                        last_update,
                        COALESCE(model_3d_url, '') AS model_3d_url,
                        COALESCE(in_garage, 1) AS in_garage
                    FROM vehicles
                    ORDER BY brand ASC, model ASC", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    vehicles.Add(new
                    {
                        vehicle_id = Convert.ToInt32(reader["vehicle_id"]),
                        plate_no = reader["plate_no"]?.ToString() ?? "",
                        brand = reader["brand"]?.ToString() ?? "",
                        model = reader["model"]?.ToString() ?? "",
                        type = reader["type"]?.ToString() ?? "",
                        cc = reader["cc"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["cc"]),
                        status = reader["status"]?.ToString() ?? "available",
                        rate_per_day = reader["rate_per_day"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["rate_per_day"]),
                        rate_with_driver = reader["rate_with_driver"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["rate_with_driver"]),
                        photo_url = reader["photo_url"]?.ToString() ?? "",
                        description = reader["description"]?.ToString() ?? "",
                        seat_capacity = reader["seat_capacity"] == DBNull.Value ? 5 : Convert.ToInt32(reader["seat_capacity"]),
                        transmission = reader["transmission"]?.ToString() ?? "Automatic",
                        created_at = reader["created_at"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["created_at"]),
                        latitude = reader["latitude"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["latitude"]),
                        longitude = reader["longitude"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["longitude"]),
                        current_speed = reader["current_speed"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["current_speed"]),
                        last_update = reader["last_update"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["last_update"]),
                        model_3d_url = reader["model_3d_url"]?.ToString() ?? "",
                        in_garage = reader["in_garage"] != DBNull.Value && Convert.ToBoolean(reader["in_garage"])
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
                    SELECT
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
                        COALESCE(seat_capacity, 5) AS seat_capacity,
                        COALESCE(transmission, 'Automatic') AS transmission,
                        created_at,
                        latitude,
                        longitude,
                        current_speed,
                        last_update,
                        COALESCE(model_3d_url, '') AS model_3d_url,
                        COALESCE(in_garage, 1) AS in_garage
                    FROM vehicles
                    WHERE vehicle_id = @id
                    LIMIT 1", conn);

                cmd.Parameters.AddWithValue("@id", id);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return NotFound(new { message = "Vehicle not found." });

                var vehicle = new
                {
                    vehicle_id = Convert.ToInt32(reader["vehicle_id"]),
                    plate_no = reader["plate_no"]?.ToString() ?? "",
                    brand = reader["brand"]?.ToString() ?? "",
                    model = reader["model"]?.ToString() ?? "",
                    type = reader["type"]?.ToString() ?? "",
                    cc = reader["cc"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["cc"]),
                    status = reader["status"]?.ToString() ?? "available",
                    rate_per_day = reader["rate_per_day"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["rate_per_day"]),
                    rate_with_driver = reader["rate_with_driver"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["rate_with_driver"]),
                    photo_url = reader["photo_url"]?.ToString() ?? "",
                    description = reader["description"]?.ToString() ?? "",
                    seat_capacity = reader["seat_capacity"] == DBNull.Value ? 5 : Convert.ToInt32(reader["seat_capacity"]),
                    transmission = reader["transmission"]?.ToString() ?? "Automatic",
                    created_at = reader["created_at"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["created_at"]),
                    latitude = reader["latitude"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["latitude"]),
                    longitude = reader["longitude"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["longitude"]),
                    current_speed = reader["current_speed"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["current_speed"]),
                    last_update = reader["last_update"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["last_update"]),
                    model_3d_url = reader["model_3d_url"]?.ToString() ?? "",
                    in_garage = reader["in_garage"] != DBNull.Value && Convert.ToBoolean(reader["in_garage"])
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
                var vehicles = new List<object>();

                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT
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
                        COALESCE(seat_capacity, 5) AS seat_capacity,
                        COALESCE(transmission, 'Automatic') AS transmission,
                        created_at,
                        latitude,
                        longitude,
                        current_speed,
                        last_update,
                        COALESCE(model_3d_url, '') AS model_3d_url,
                        COALESCE(in_garage, 1) AS in_garage
                    FROM vehicles
                    WHERE LOWER(status) = 'available'
                    ORDER BY brand ASC, model ASC", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    vehicles.Add(new
                    {
                        vehicle_id = Convert.ToInt32(reader["vehicle_id"]),
                        plate_no = reader["plate_no"]?.ToString() ?? "",
                        brand = reader["brand"]?.ToString() ?? "",
                        model = reader["model"]?.ToString() ?? "",
                        type = reader["type"]?.ToString() ?? "",
                        cc = reader["cc"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["cc"]),
                        status = reader["status"]?.ToString() ?? "available",
                        rate_per_day = reader["rate_per_day"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["rate_per_day"]),
                        rate_with_driver = reader["rate_with_driver"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["rate_with_driver"]),
                        photo_url = reader["photo_url"]?.ToString() ?? "",
                        description = reader["description"]?.ToString() ?? "",
                        seat_capacity = reader["seat_capacity"] == DBNull.Value ? 5 : Convert.ToInt32(reader["seat_capacity"]),
                        transmission = reader["transmission"]?.ToString() ?? "Automatic",
                        created_at = reader["created_at"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["created_at"]),
                        latitude = reader["latitude"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["latitude"]),
                        longitude = reader["longitude"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["longitude"]),
                        current_speed = reader["current_speed"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["current_speed"]),
                        last_update = reader["last_update"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["last_update"]),
                        model_3d_url = reader["model_3d_url"]?.ToString() ?? "",
                        in_garage = reader["in_garage"] != DBNull.Value && Convert.ToBoolean(reader["in_garage"])
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
            if (string.IsNullOrWhiteSpace(newVehicle.Brand) ||
                string.IsNullOrWhiteSpace(newVehicle.Model) ||
                string.IsNullOrWhiteSpace(newVehicle.PlateNo))
            {
                return BadRequest(new
                {
                    message = "Brand, Model, and Plate No are required."
                });
            }

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var checkCmd = new MySqlCommand(@"
                    SELECT COUNT(*) FROM vehicles
                    WHERE plate_no = @plate", conn);
                checkCmd.Parameters.AddWithValue("@plate", newVehicle.PlateNo);
                var count = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (count > 0)
                {
                    return Conflict(new { message = "Plate no already exists." });
                }

                var insertCmd = new MySqlCommand(@"
                    INSERT INTO vehicles
                    (
                        plate_no,
                        brand,
                        model,
                        type,
                        cc,
                        status,
                        rate_per_day,
                        rate_with_driver,
                        photo_url,
                        description,
                        seat_capacity,
                        transmission,
                        model_3d_url,
                        created_at,
                        latitude,
                        longitude,
                        current_speed,
                        last_update,
                        in_garage
                    )
                    VALUES
                    (
                        @plate,
                        @brand,
                        @model,
                        @type,
                        @cc,
                        @status,
                        @rate_per_day,
                        @rate_with_driver,
                        @photo_url,
                        @description,
                        @seat_capacity,
                        @transmission,
                        @model_3d_url,
                        @created_at,
                        @latitude,
                        @longitude,
                        @current_speed,
                        @last_update,
                        @in_garage
                    )", conn);

                insertCmd.Parameters.AddWithValue("@plate", newVehicle.PlateNo);
                insertCmd.Parameters.AddWithValue("@brand", newVehicle.Brand);
                insertCmd.Parameters.AddWithValue("@model", newVehicle.Model);
                insertCmd.Parameters.AddWithValue("@type", string.IsNullOrWhiteSpace(newVehicle.Type) ? "Car" : newVehicle.Type);
                insertCmd.Parameters.AddWithValue("@cc", newVehicle.CC.HasValue ? newVehicle.CC.Value : DBNull.Value);
                insertCmd.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(newVehicle.Status) ? "available" : newVehicle.Status);
                insertCmd.Parameters.AddWithValue("@rate_per_day", newVehicle.RatePerDay);
                insertCmd.Parameters.AddWithValue("@rate_with_driver", newVehicle.RateWithDriver);
                insertCmd.Parameters.AddWithValue("@photo_url", string.IsNullOrWhiteSpace(newVehicle.PhotoUrl) ? DBNull.Value : newVehicle.PhotoUrl);
                insertCmd.Parameters.AddWithValue("@description", string.IsNullOrWhiteSpace(newVehicle.Description) ? DBNull.Value : newVehicle.Description);
                insertCmd.Parameters.AddWithValue("@seat_capacity", newVehicle.SeatCapacity <= 0 ? 5 : newVehicle.SeatCapacity);
                insertCmd.Parameters.AddWithValue("@transmission", string.IsNullOrWhiteSpace(newVehicle.Transmission) ? "Automatic" : newVehicle.Transmission);
                insertCmd.Parameters.AddWithValue("@model_3d_url", string.IsNullOrWhiteSpace(newVehicle.Model3dUrl) ? DBNull.Value : newVehicle.Model3dUrl);
                insertCmd.Parameters.AddWithValue("@created_at", newVehicle.CreatedAt == DateTime.MinValue ? DateTime.Now : newVehicle.CreatedAt);
                insertCmd.Parameters.AddWithValue("@latitude", newVehicle.Latitude.HasValue ? newVehicle.Latitude.Value : DBNull.Value);
                insertCmd.Parameters.AddWithValue("@longitude", newVehicle.Longitude.HasValue ? newVehicle.Longitude.Value : DBNull.Value);
                insertCmd.Parameters.AddWithValue("@current_speed", newVehicle.CurrentSpeed.HasValue ? newVehicle.CurrentSpeed.Value : DBNull.Value);
                insertCmd.Parameters.AddWithValue("@last_update", newVehicle.LastUpdate.HasValue ? newVehicle.LastUpdate.Value : DBNull.Value);
                insertCmd.Parameters.AddWithValue("@in_garage", newVehicle.InGarage);

                insertCmd.ExecuteNonQuery();

                var idCmd = new MySqlCommand("SELECT LAST_INSERT_ID()", conn);
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
        public IActionResult UpdateVehicle(int id, [FromBody] Vehicle updated)
        {
            if (string.IsNullOrWhiteSpace(updated.Brand) ||
                string.IsNullOrWhiteSpace(updated.Model) ||
                string.IsNullOrWhiteSpace(updated.PlateNo))
            {
                return BadRequest(new
                {
                    message = "Brand, Model, and Plate No are required."
                });
            }

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

                var plateCmd = new MySqlCommand(@"
                    SELECT COUNT(*) FROM vehicles
                    WHERE plate_no = @plate
                    AND vehicle_id != @id", conn);
                plateCmd.Parameters.AddWithValue("@plate", updated.PlateNo);
                plateCmd.Parameters.AddWithValue("@id", id);
                var plateCount = Convert.ToInt32(plateCmd.ExecuteScalar());

                if (plateCount > 0)
                {
                    return Conflict(new
                    {
                        message = "Plate no is already used by another vehicle."
                    });
                }

                var updateCmd = new MySqlCommand(@"
                    UPDATE vehicles
                    SET
                        plate_no = @plate,
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
                    WHERE vehicle_id = @id", conn);

                updateCmd.Parameters.AddWithValue("@plate", updated.PlateNo);
                updateCmd.Parameters.AddWithValue("@brand", updated.Brand);
                updateCmd.Parameters.AddWithValue("@model", updated.Model);
                updateCmd.Parameters.AddWithValue("@type", string.IsNullOrWhiteSpace(updated.Type) ? "Car" : updated.Type);
                updateCmd.Parameters.AddWithValue("@cc", updated.CC.HasValue ? updated.CC.Value : DBNull.Value);
                updateCmd.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(updated.Status) ? "available" : updated.Status);
                updateCmd.Parameters.AddWithValue("@rate_per_day", updated.RatePerDay);
                updateCmd.Parameters.AddWithValue("@rate_with_driver", updated.RateWithDriver);
                updateCmd.Parameters.AddWithValue("@photo_url", string.IsNullOrWhiteSpace(updated.PhotoUrl) ? DBNull.Value : updated.PhotoUrl);
                updateCmd.Parameters.AddWithValue("@description", string.IsNullOrWhiteSpace(updated.Description) ? DBNull.Value : updated.Description);
                updateCmd.Parameters.AddWithValue("@seat_capacity", updated.SeatCapacity <= 0 ? 5 : updated.SeatCapacity);
                updateCmd.Parameters.AddWithValue("@transmission", string.IsNullOrWhiteSpace(updated.Transmission) ? "Automatic" : updated.Transmission);
                updateCmd.Parameters.AddWithValue("@model_3d_url", string.IsNullOrWhiteSpace(updated.Model3dUrl) ? DBNull.Value : updated.Model3dUrl);
                updateCmd.Parameters.AddWithValue("@latitude", updated.Latitude.HasValue ? updated.Latitude.Value : DBNull.Value);
                updateCmd.Parameters.AddWithValue("@longitude", updated.Longitude.HasValue ? updated.Longitude.Value : DBNull.Value);
                updateCmd.Parameters.AddWithValue("@current_speed", updated.CurrentSpeed.HasValue ? updated.CurrentSpeed.Value : DBNull.Value);
                updateCmd.Parameters.AddWithValue("@last_update", updated.LastUpdate.HasValue ? updated.LastUpdate.Value : DBNull.Value);
                updateCmd.Parameters.AddWithValue("@in_garage", updated.InGarage);
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

                var rentalCmd = new MySqlCommand(@"
                    SELECT COUNT(*) FROM rentals
                    WHERE vehicle_id = @id
                    AND status IN ('pending', 'approved')", conn);
                rentalCmd.Parameters.AddWithValue("@id", id);
                var activeRentals = Convert.ToInt32(rentalCmd.ExecuteScalar());

                if (activeRentals > 0)
                {
                    return Conflict(new
                    {
                        message = "Cannot delete — vehicle has active or pending rentals."
                    });
                }

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
        public IActionResult UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
        {
            var validStatuses = new[] { "available", "rented", "maintenance", "retired", "in-use" };

            if (request == null || string.IsNullOrWhiteSpace(request.Status))
            {
                return BadRequest(new { message = "Status is required." });
            }

            if (!validStatuses.Contains(request.Status.ToLower()))
            {
                return BadRequest(new
                {
                    message = "Valid statuses: available, rented, maintenance, retired, in-use"
                });
            }

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
                updateCmd.Parameters.AddWithValue("@status", request.Status.ToLower());
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