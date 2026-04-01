using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using DriveAndGo_API.Models;

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

        // ══ POST — Mobile App (Driver/Customer) nag-sesend ng live location ══
        // Ise-send ito ng app every 10-30 seconds kapag on-going ang byahe
        [HttpPost("update")]
        public IActionResult UpdateLocation([FromBody] LocationLog log)
        {
            if (log.RentalId == 0 || log.VehicleId == 0 || log.Latitude == 0 || log.Longitude == 0)
                return BadRequest(new { message = "RentalId, VehicleId, Latitude, and Longitude are required." });

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // Check kung active (approved) pa ba ang rental bago i-save ang location
                var checkCmd = new MySqlCommand(
                    "SELECT status FROM rentals WHERE rental_id = @rental_id", conn);
                checkCmd.Parameters.AddWithValue("@rental_id", log.RentalId);
                var status = checkCmd.ExecuteScalar()?.ToString();

                if (status != "approved")
                    return BadRequest(new { message = "This rental is not active. Location not saved." });

                // I-save ang location sa database
                var insertCmd = new MySqlCommand(@"
                    INSERT INTO location_logs 
                        (rental_id, vehicle_id, latitude, longitude, speed_kmh, logged_at) 
                    VALUES 
                        (@rental_id, @vehicle_id, @latitude, @longitude, @speed, NOW())", conn);

                insertCmd.Parameters.AddWithValue("@rental_id", log.RentalId);
                insertCmd.Parameters.AddWithValue("@vehicle_id", log.VehicleId);
                insertCmd.Parameters.AddWithValue("@latitude", log.Latitude);
                insertCmd.Parameters.AddWithValue("@longitude", log.Longitude);
                insertCmd.Parameters.AddWithValue("@speed", log.SpeedKmH ?? 0);

                insertCmd.ExecuteNonQuery();

                return Ok(new { message = "Location updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ GET LATEST LOCATIONS OF ALL ACTIVE VEHICLES — Para sa Admin Map Dashboard ══
        [HttpGet("active-vehicles")]
        public IActionResult GetActiveVehicleLocations()
        {
            try
            {
                List<LocationLog> locations = new List<LocationLog>();

                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // Kukunin natin ang pinaka-latest na location ng bawat active na byahe
                var cmd = new MySqlCommand(@"
                    SELECT l1.log_id, l1.rental_id, l1.vehicle_id, l1.latitude, l1.longitude, l1.speed_kmh, l1.logged_at,
                           CONCAT(v.brand, ' ', v.model) AS vehicle_name, v.plate_number,
                           IFNULL(u.full_name, 'No Driver (Self-Drive)') AS driver_name
                    FROM location_logs l1
                    JOIN (
                        SELECT rental_id, MAX(logged_at) AS latest_time
                        FROM location_logs
                        GROUP BY rental_id
                    ) l2 ON l1.rental_id = l2.rental_id AND l1.logged_at = l2.latest_time
                    JOIN rentals r ON l1.rental_id = r.rental_id
                    JOIN vehicles v ON l1.vehicle_id = v.vehicle_id
                    LEFT JOIN users u ON r.driver_id = u.user_id
                    WHERE r.status = 'approved'", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    locations.Add(new LocationLog
                    {
                        LogId = Convert.ToInt32(reader["log_id"]),
                        RentalId = Convert.ToInt32(reader["rental_id"]),
                        VehicleId = Convert.ToInt32(reader["vehicle_id"]),
                        Latitude = Convert.ToDecimal(reader["latitude"]),
                        Longitude = Convert.ToDecimal(reader["longitude"]),
                        SpeedKmH = reader["speed_kmh"] != DBNull.Value ? Convert.ToDecimal(reader["speed_kmh"]) : 0,
                        LoggedAt = Convert.ToDateTime(reader["logged_at"]),
                        VehicleName = reader["vehicle_name"].ToString(),
                        PlateNumber = reader["plate_number"].ToString(),
                        DriverName = reader["driver_name"].ToString()
                    });
                }

                return Ok(locations);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ GET LOCATION HISTORY — Para iguhit yung ruta (blue line) sa mapa ══
        [HttpGet("history/{rentalId}")]
        public IActionResult GetLocationHistory(int rentalId)
        {
            try
            {
                List<object> path = new List<object>();

                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // Kukunin lahat ng logs ng isang specific na byahe para maging 'polyline' o drawing sa mapa
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
                        lat = Convert.ToDecimal(reader["latitude"]),
                        lng = Convert.ToDecimal(reader["longitude"]),
                        speed = reader["speed_kmh"] != DBNull.Value ? Convert.ToDecimal(reader["speed_kmh"]) : 0,
                        time = Convert.ToDateTime(reader["logged_at"])
                    });
                }

                if (path.Count == 0)
                    return NotFound(new { message = "No location history recorded for this rental." });

                return Ok(path);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }
    }
}