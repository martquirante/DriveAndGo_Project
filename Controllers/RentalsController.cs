using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using DriveAndGo_API.Models;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RentalsController : ControllerBase
    {
        private readonly string _connectionString;

        public RentalsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // Ito 'yung pang-kuha ng listahan (GET)
        [HttpGet]
        public IActionResult GetRentals()
        {
            List<Rental> rentals = new List<Rental>();

            using (MySqlConnection conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                MySqlCommand cmd = new MySqlCommand("SELECT rental_id, customer_id, vehicle_id, driver_id, start_date, end_date, destination, status, total_amount FROM rentals", conn);

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rentals.Add(new Rental
                        {
                            RentalId = Convert.ToInt32(reader["rental_id"]),
                            CustomerId = Convert.ToInt32(reader["customer_id"]),
                            VehicleId = Convert.ToInt32(reader["vehicle_id"]),
                            DriverId = reader["driver_id"] != DBNull.Value ? Convert.ToInt32(reader["driver_id"]) : null,
                            StartDate = Convert.ToDateTime(reader["start_date"]),
                            EndDate = Convert.ToDateTime(reader["end_date"]),
                            Destination = reader["destination"].ToString(),
                            Status = reader["status"].ToString(),
                            TotalAmount = Convert.ToDecimal(reader["total_amount"])
                        });
                    }
                }
            }

            return Ok(rentals);
        }

        // Ito 'yung bago natin para makapag-save (POST)
        [HttpPost]
        public IActionResult AddRental([FromBody] Rental rental)
        {
            using (MySqlConnection conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string query = "INSERT INTO rentals (customer_id, vehicle_id, driver_id, start_date, end_date, destination, status, total_amount, payment_method, payment_status) VALUES (@customer_id, @vehicle_id, @driver_id, @start_date, @end_date, @destination, @status, @total_amount, 'gcash', 'unpaid')";

                MySqlCommand cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@customer_id", rental.CustomerId);
                cmd.Parameters.AddWithValue("@vehicle_id", rental.VehicleId);

                if (rental.DriverId.HasValue)
                    cmd.Parameters.AddWithValue("@driver_id", rental.DriverId.Value);
                else
                    cmd.Parameters.AddWithValue("@driver_id", DBNull.Value);

                cmd.Parameters.AddWithValue("@start_date", rental.StartDate);
                cmd.Parameters.AddWithValue("@end_date", rental.EndDate);
                cmd.Parameters.AddWithValue("@destination", rental.Destination);
                cmd.Parameters.AddWithValue("@status", "pending");
                cmd.Parameters.AddWithValue("@total_amount", rental.TotalAmount);

                cmd.ExecuteNonQuery();
            }

            return Ok(new { message = "Booking successful! Hintayin ang approval ni Admin." });
        }
    }
}