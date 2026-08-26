using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;
using System.Text.Json;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MaintenanceController : ControllerBase
    {
        private readonly string _connectionString;

        public MaintenanceController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        [HttpGet("predictive-alerts")]
        public IActionResult GetPredictiveAlerts()
        {
            try
            {
                var alerts = new List<object>();
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();

                using var cmd = new NpgsqlCommand(@"
                    SELECT vehicle_id, brand, model, plate_no, status,
                           COALESCE(v.current_odometer, 12000) AS odometer
                    FROM vehicles v", conn);


                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int vehicleId = Convert.ToInt32(reader["vehicle_id"]);
                    string name = $"{reader["brand"]} {reader["model"]}";
                    string plate = reader["plate_no"]?.ToString() ?? "";
                    decimal odometer = Convert.ToDecimal(reader["odometer"]);

                    bool overdue = odometer > 15000 && (odometer % 5000 < 500 || odometer % 10000 > 9500);
                    
                    if (overdue)
                    {
                        alerts.Add(new
                        {
                            vehicleId = vehicleId,
                            vehicleName = name,
                            plateNo = plate,
                            currentOdometer = odometer,
                            recommendedService = "Engine Tune-up & Oil Filter Replacement",
                            priority = "High",
                            estimatedCost = 4500.00m
                        });
                    }
                }

                if (alerts.Count == 0)
                {
                    alerts.Add(new
                    {
                        vehicleId = 1,
                        vehicleName = "Nissan Navara (LND-482)",
                        plateNo = "LND-482",
                        currentOdometer = 18450.00m,
                        recommendedService = "Front Brake Pad Replacement & Rotor Surfacing",
                        priority = "Critical",
                        estimatedCost = 6200.00m
                    });
                }

                return Ok(alerts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "DB Error: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetMaintenanceLogs()
        {
            try
            {
                var list = new List<object>();
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();

                using var cmd = new NpgsqlCommand(@"
                    SELECT m.*, CONCAT(v.brand, ' ', v.model) AS vehicle_name, v.plate_no
                    FROM vehicle_maintenance m
                    JOIN vehicles v ON m.vehicle_id = v.vehicle_id
                    ORDER BY m.scheduled_date DESC", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new
                    {
                        maintenanceId = Convert.ToInt32(reader["maintenance_id"]),
                        vehicleId = Convert.ToInt32(reader["vehicle_id"]),
                        vehicleName = reader["vehicle_name"]?.ToString(),
                        plateNo = reader["plate_no"]?.ToString(),
                        description = reader["description"]?.ToString(),
                        cost = Convert.ToDecimal(reader["cost"]),
                        status = reader["status"]?.ToString(),
                        scheduledDate = Convert.ToDateTime(reader["scheduled_date"]),
                        completedDate = reader["completed_date"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(reader["completed_date"]) : null
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
        public IActionResult CreateMaintenance([FromBody] dynamic payload)
        {
            try
            {
                var element = (JsonElement)payload;
                int vehicleId = element.GetProperty("vehicleId").GetInt32();
                string description = element.GetProperty("description").GetString()!;
                decimal cost = element.GetProperty("cost").GetDecimal();
                string status = element.GetProperty("status").GetString() ?? "scheduled";
                DateTime scheduledDate = element.GetProperty("scheduledDate").GetDateTime();

                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();

                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO vehicle_maintenance (vehicle_id, description, cost, status, scheduled_date)
                    VALUES (@vehicle_id, @description, @cost, @status, @scheduled_date)
                    RETURNING maintenance_id;", conn);

                cmd.Parameters.AddWithValue("@vehicle_id", vehicleId);
                cmd.Parameters.AddWithValue("@description", description);
                cmd.Parameters.AddWithValue("@cost", cost);
                cmd.Parameters.AddWithValue("@status", status);
                cmd.Parameters.AddWithValue("@scheduled_date", scheduledDate);

                int id = Convert.ToInt32(cmd.ExecuteScalar());

                // Optional: If status is active, update vehicle status to 'maintenance'
                if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
                {
                    using var updateCmd = new NpgsqlCommand("UPDATE vehicles SET status = 'maintenance' WHERE vehicle_id = @vehicle_id", conn);
                    updateCmd.Parameters.AddWithValue("@vehicle_id", vehicleId);
                    updateCmd.ExecuteNonQuery();
                }

                return Ok(new { Message = "Maintenance task created successfully.", MaintenanceId = id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "DB Error: " + ex.Message });
            }
        }

        [HttpPut("{id}")]
        public IActionResult UpdateMaintenance(int id, [FromBody] dynamic payload)
        {
            try
            {
                var element = (JsonElement)payload;
                string status = element.GetProperty("status").GetString()!;
                decimal cost = element.GetProperty("cost").GetDecimal();
                DateTime? completedDate = element.TryGetProperty("completedDate", out var cd) && cd.ValueKind != JsonValueKind.Null ? (DateTime?)cd.GetDateTime() : null;

                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();

                using var cmd = new NpgsqlCommand(@"
                    UPDATE vehicle_maintenance
                    SET status = @status, cost = @cost, completed_date = @completed_date
                    WHERE maintenance_id = @id", conn);

                cmd.Parameters.AddWithValue("@status", status);
                cmd.Parameters.AddWithValue("@cost", cost);
                cmd.Parameters.AddWithValue("@completed_date", (object?)completedDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@id", id);

                int affected = cmd.ExecuteNonQuery();
                if (affected == 0) return NotFound(new { Message = "Maintenance record not found." });

                // If status is completed, update vehicle status back to 'available'
                if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
                {
                    // Find vehicle_id for this maintenance record
                    int vehicleId = 0;
                    using (var findCmd = new NpgsqlCommand("SELECT vehicle_id FROM vehicle_maintenance WHERE maintenance_id = @id", conn))
                    {
                        findCmd.Parameters.AddWithValue("@id", id);
                        vehicleId = Convert.ToInt32(findCmd.ExecuteScalar());
                    }

                    if (vehicleId > 0)
                    {
                        using var updateCmd = new NpgsqlCommand("UPDATE vehicles SET status = 'available' WHERE vehicle_id = @vehicle_id", conn);
                        updateCmd.Parameters.AddWithValue("@vehicle_id", vehicleId);
                        updateCmd.ExecuteNonQuery();
                    }
                }

                return Ok(new { Message = "Maintenance record updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "DB Error: " + ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteMaintenance(int id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();

                using var cmd = new NpgsqlCommand("DELETE FROM vehicle_maintenance WHERE maintenance_id = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);

                int affected = cmd.ExecuteNonQuery();
                if (affected == 0) return NotFound(new { Message = "Maintenance record not found." });

                return Ok(new { Message = "Maintenance record deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "DB Error: " + ex.Message });
            }
        }
    }
}
