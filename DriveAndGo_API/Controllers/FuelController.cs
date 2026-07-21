using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;
using System.Text.Json;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FuelController : ControllerBase
    {
        private readonly string _connectionString;

        public FuelController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        [HttpGet]
        public IActionResult GetFuelLogs()
        {
            try
            {
                var list = new List<object>();
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();

                using var cmd = new NpgsqlCommand(@"
                    SELECT f.*, CONCAT(v.brand, ' ', v.model) AS vehicle_name, v.plate_no, r.customer_id
                    FROM fuel_logs f
                    JOIN vehicles v ON f.vehicle_id = v.vehicle_id
                    LEFT JOIN rentals r ON f.rental_id = r.rental_id
                    ORDER BY f.logged_date DESC", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new
                    {
                        fuelLogId = Convert.ToInt32(reader["fuel_log_id"]),
                        vehicleId = Convert.ToInt32(reader["vehicle_id"]),
                        vehicleName = reader["vehicle_name"]?.ToString(),
                        plateNo = reader["plate_no"]?.ToString(),
                        rentalId = reader["rental_id"] != DBNull.Value ? (int?)Convert.ToInt32(reader["rental_id"]) : null,
                        fuelQtyLiters = Convert.ToDecimal(reader["fuel_qty_liters"]),
                        cost = Convert.ToDecimal(reader["cost"]),
                        currentOdometer = Convert.ToDecimal(reader["current_odometer"]),
                        loggedDate = Convert.ToDateTime(reader["logged_date"])
                    });
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "DB Error: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult CreateFuelLog([FromBody] dynamic payload)
        {
            try
            {
                var element = (JsonElement)payload;
                int vehicleId = element.GetProperty("vehicleId").GetInt32();
                int? rentalId = element.TryGetProperty("rentalId", out var r) && r.ValueKind != JsonValueKind.Null ? (int?)r.GetInt32() : null;
                decimal fuelQty = element.GetProperty("fuelQtyLiters").GetDecimal();
                decimal cost = element.GetProperty("cost").GetDecimal();
                decimal odometer = element.GetProperty("currentOdometer").GetDecimal();
                DateTime loggedDate = element.TryGetProperty("loggedDate", out var ld) && ld.ValueKind != JsonValueKind.Null ? ld.GetDateTime() : DateTime.UtcNow;

                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();

                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO fuel_logs (vehicle_id, rental_id, fuel_qty_liters, cost, current_odometer, logged_date)
                    VALUES (@vehicle_id, @rental_id, @fuel_qty, @cost, @odometer, @logged_date)
                    RETURNING fuel_log_id;", conn);

                cmd.Parameters.AddWithValue("@vehicle_id", vehicleId);
                cmd.Parameters.AddWithValue("@rental_id", (object?)rentalId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fuel_qty", fuelQty);
                cmd.Parameters.AddWithValue("@cost", cost);
                cmd.Parameters.AddWithValue("@odometer", odometer);
                cmd.Parameters.AddWithValue("@logged_date", loggedDate);

                int id = Convert.ToInt32(cmd.ExecuteScalar());
                return Ok(new { Message = "Fuel log saved successfully.", FuelLogId = id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "DB Error: " + ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteFuelLog(int id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();

                using var cmd = new NpgsqlCommand("DELETE FROM fuel_logs WHERE fuel_log_id = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);

                int affected = cmd.ExecuteNonQuery();
                if (affected == 0) return NotFound(new { Message = "Fuel log not found." });

                return Ok(new { Message = "Fuel log deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "DB Error: " + ex.Message });
            }
        }
    }
}
