using DriveAndGo_API.Models;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Globalization;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LocationsController : ControllerBase
    {
        private readonly string _connectionString;

        public LocationsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        [HttpPost("update")]
        public IActionResult UpdateLocation([FromBody] LocationLog log)
        {
            if (log.RentalId == 0 || log.VehicleId == 0 || log.Latitude == 0 || log.Longitude == 0)
                return BadRequest(new { Message = "RentalId, VehicleId, Latitude, and Longitude are required." });

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();
                using var tx = conn.BeginTransaction();

                var checkCmd = new MySqlCommand(@"
                    SELECT LOWER(COALESCE(status, '')) AS rental_status, vehicle_id
                    FROM rentals
                    WHERE rental_id = @rental_id
                    LIMIT 1", conn, tx);
                checkCmd.Parameters.AddWithValue("@rental_id", log.RentalId);

                using var reader = checkCmd.ExecuteReader();
                if (!reader.Read())
                    return NotFound(new { Message = "Rental not found." });

                string status = reader["rental_status"]?.ToString() ?? "";
                int expectedVehicleId = Convert.ToInt32(reader["vehicle_id"], CultureInfo.InvariantCulture);
                reader.Close();

                if (!string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(status, "in-use", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { Message = "This rental is not active. Location not saved." });
                }

                if (expectedVehicleId != log.VehicleId)
                {
                    return Conflict(new { Message = "VehicleId does not match the rental record." });
                }

                DateTime loggedAt = log.LoggedAt == DateTime.MinValue ? DateTime.UtcNow : log.LoggedAt;
                decimal speed = log.SpeedKmH ?? 0;

                var insertCmd = new MySqlCommand(@"
                    INSERT INTO location_logs
                        (rental_id, vehicle_id, latitude, longitude, speed_kmh, logged_at)
                    VALUES
                        (@rental_id, @vehicle_id, @latitude, @longitude, @speed, @logged_at)", conn, tx);

                insertCmd.Parameters.AddWithValue("@rental_id", log.RentalId);
                insertCmd.Parameters.AddWithValue("@vehicle_id", log.VehicleId);
                insertCmd.Parameters.AddWithValue("@latitude", log.Latitude);
                insertCmd.Parameters.AddWithValue("@longitude", log.Longitude);
                insertCmd.Parameters.AddWithValue("@speed", speed);
                insertCmd.Parameters.AddWithValue("@logged_at", loggedAt);
                insertCmd.ExecuteNonQuery();

                var vehicleCmd = new MySqlCommand(@"
                    UPDATE vehicles
                    SET latitude = @latitude,
                        longitude = @longitude,
                        current_speed = @speed,
                        last_update = @logged_at
                    WHERE vehicle_id = @vehicle_id", conn, tx);
                vehicleCmd.Parameters.AddWithValue("@latitude", log.Latitude);
                vehicleCmd.Parameters.AddWithValue("@longitude", log.Longitude);
                vehicleCmd.Parameters.AddWithValue("@speed", Convert.ToInt32(Math.Round(speed)));
                vehicleCmd.Parameters.AddWithValue("@logged_at", loggedAt);
                vehicleCmd.Parameters.AddWithValue("@vehicle_id", log.VehicleId);
                vehicleCmd.ExecuteNonQuery();

                tx.Commit();

                return Ok(new { Message = "Location updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "DB Error: " + ex.Message });
            }
        }

        [HttpGet("active-vehicles")]
        public IActionResult GetActiveVehicleLocations()
        {
            try
            {
                List<LocationLog> locations = new List<LocationLog>();

                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT l1.log_id, l1.rental_id, l1.vehicle_id, l1.latitude, l1.longitude, l1.speed_kmh, l1.logged_at,
                           CONCAT(v.brand, ' ', v.model) AS vehicle_name, v.plate_no AS plate_number,
                           IFNULL(u.full_name, 'No Driver (Self-Drive)') AS driver_name
                    FROM location_logs l1
                    JOIN (
                        SELECT rental_id, MAX(logged_at) AS latest_time
                        FROM location_logs
                        GROUP BY rental_id
                    ) l2 ON l1.rental_id = l2.rental_id AND l1.logged_at = l2.latest_time
                    JOIN rentals r ON l1.rental_id = r.rental_id
                    JOIN vehicles v ON l1.vehicle_id = v.vehicle_id
                    LEFT JOIN drivers d ON r.driver_id = d.driver_id
                    LEFT JOIN users u ON d.user_id = u.user_id
                    WHERE LOWER(COALESCE(r.status, '')) IN ('approved', 'in-use', 'active')", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    locations.Add(new LocationLog
                    {
                        LogId = Convert.ToInt32(reader["log_id"], CultureInfo.InvariantCulture),
                        RentalId = Convert.ToInt32(reader["rental_id"], CultureInfo.InvariantCulture),
                        VehicleId = Convert.ToInt32(reader["vehicle_id"], CultureInfo.InvariantCulture),
                        Latitude = Convert.ToDecimal(reader["latitude"], CultureInfo.InvariantCulture),
                        Longitude = Convert.ToDecimal(reader["longitude"], CultureInfo.InvariantCulture),
                        SpeedKmH = reader["speed_kmh"] != DBNull.Value
                            ? Convert.ToDecimal(reader["speed_kmh"], CultureInfo.InvariantCulture)
                            : 0,
                        LoggedAt = Convert.ToDateTime(reader["logged_at"], CultureInfo.InvariantCulture),
                        VehicleName = reader["vehicle_name"]?.ToString(),
                        PlateNumber = reader["plate_number"]?.ToString(),
                        DriverName = reader["driver_name"]?.ToString()
                    });
                }

                return Ok(locations);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "DB Error: " + ex.Message });
            }
        }

        [HttpGet("history/{rentalId}")]
        public IActionResult GetLocationHistory(int rentalId)
        {
            try
            {
                List<object> path = new List<object>();

                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT latitude, longitude, speed_kmh, logged_at
                    FROM location_logs
                    WHERE rental_id = @rental_id
                    ORDER BY logged_at ASC", conn);
                cmd.Parameters.AddWithValue("@rental_id", rentalId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    path.Add(new
                    {
                        lat = Convert.ToDecimal(reader["latitude"], CultureInfo.InvariantCulture),
                        lng = Convert.ToDecimal(reader["longitude"], CultureInfo.InvariantCulture),
                        speed = reader["speed_kmh"] != DBNull.Value
                            ? Convert.ToDecimal(reader["speed_kmh"], CultureInfo.InvariantCulture)
                            : 0,
                        time = Convert.ToDateTime(reader["logged_at"], CultureInfo.InvariantCulture)
                    });
                }

                if (path.Count == 0)
                    return NotFound(new { Message = "No location history recorded for this rental." });

                return Ok(path);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "DB Error: " + ex.Message });
            }
        }
    }
}
