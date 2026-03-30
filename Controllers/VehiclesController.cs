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
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet]
        public IActionResult GetVehicles()
        {
            List<Vehicle> vehicles = new List<Vehicle>();

            using (MySqlConnection conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                MySqlCommand cmd = new MySqlCommand("SELECT * FROM vehicles", conn);

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
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
                }
            }

            return Ok(vehicles);
        }

        [HttpPost]
        public IActionResult AddVehicle([FromBody] Vehicle newVehicle)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(_connectionString))
                {
                    conn.Open();
                    // Status defaults to 'Available' when adding a new vehicle
                    string query = "INSERT INTO vehicles (brand, model, plate_number, type, daily_rate, status) VALUES (@brand, @model, @plate, @type, @rate, 'Available')";
                    MySqlCommand cmd = new MySqlCommand(query, conn);

                    cmd.Parameters.AddWithValue("@brand", newVehicle.Brand);
                    cmd.Parameters.AddWithValue("@model", newVehicle.Model);
                    cmd.Parameters.AddWithValue("@plate", newVehicle.PlateNumber);
                    cmd.Parameters.AddWithValue("@type", newVehicle.Type);
                    cmd.Parameters.AddWithValue("@rate", newVehicle.DailyRate);

                    cmd.ExecuteNonQuery();
                }
                return Ok(new { message = "Success! New vehicle added successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Error adding vehicle: " + ex.Message });
            }
        }
    }
}