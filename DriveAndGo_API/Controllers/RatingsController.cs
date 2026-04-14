using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using DriveAndGo_API.Models;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RatingsController : ControllerBase
    {
        private readonly string _connectionString;

        public RatingsController(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")!;
        }

        // ══ GET ALL RATINGS ══
        [HttpGet]
        public IActionResult GetRatings()
        {
            try
            {
                List<Rating> ratings = new List<Rating>();

                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT r.rating_id, r.rental_id, r.customer_id,
                           r.driver_id, r.vehicle_id,
                           r.driver_score, r.vehicle_score,
                           r.comment, r.rated_at,
                           u.full_name AS customer_name,
                           CONCAT(v.brand, ' ', v.model) AS vehicle_name
                    FROM ratings r
                    JOIN users u    ON r.customer_id = u.user_id
                    JOIN vehicles v ON r.vehicle_id  = v.vehicle_id
                    ORDER BY r.rated_at DESC", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    ratings.Add(new Rating
                    {
                        RatingId = Convert.ToInt32(reader["rating_id"]),
                        RentalId = Convert.ToInt32(reader["rental_id"]),
                        CustomerId = Convert.ToInt32(reader["customer_id"]),
                        DriverId = reader["driver_id"] != DBNull.Value
                                       ? Convert.ToInt32(reader["driver_id"])
                                       : null,
                        VehicleId = Convert.ToInt32(reader["vehicle_id"]),
                        DriverScore = reader["driver_score"] != DBNull.Value
                                       ? Convert.ToInt32(reader["driver_score"])
                                       : null,
                        VehicleScore = Convert.ToInt32(reader["vehicle_score"]),
                        Comment = reader["comment"].ToString(),
                        RatedAt = Convert.ToDateTime(reader["rated_at"]),
                        CustomerName = reader["customer_name"].ToString(),
                        VehicleName = reader["vehicle_name"].ToString()
                    });
                }

                return Ok(ratings);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ GET RATINGS BY DRIVER — para sa driver leaderboard ══
        [HttpGet("driver/{driverId}")]
        public IActionResult GetByDriver(int driverId)
        {
            try
            {
                List<Rating> ratings = new List<Rating>();

                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT r.rating_id, r.rental_id, r.customer_id,
                           r.driver_id, r.vehicle_id,
                           r.driver_score, r.vehicle_score,
                           r.comment, r.rated_at,
                           u.full_name AS customer_name,
                           CONCAT(v.brand, ' ', v.model) AS vehicle_name
                    FROM ratings r
                    JOIN users u    ON r.customer_id = u.user_id
                    JOIN vehicles v ON r.vehicle_id  = v.vehicle_id
                    WHERE r.driver_id = @driver_id
                    ORDER BY r.rated_at DESC", conn);
                cmd.Parameters.AddWithValue("@driver_id", driverId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    ratings.Add(new Rating
                    {
                        RatingId = Convert.ToInt32(reader["rating_id"]),
                        RentalId = Convert.ToInt32(reader["rental_id"]),
                        CustomerId = Convert.ToInt32(reader["customer_id"]),
                        DriverId = reader["driver_id"] != DBNull.Value
                                       ? Convert.ToInt32(reader["driver_id"])
                                       : null,
                        VehicleId = Convert.ToInt32(reader["vehicle_id"]),
                        DriverScore = reader["driver_score"] != DBNull.Value
                                       ? Convert.ToInt32(reader["driver_score"])
                                       : null,
                        VehicleScore = Convert.ToInt32(reader["vehicle_score"]),
                        Comment = reader["comment"].ToString(),
                        RatedAt = Convert.ToDateTime(reader["rated_at"]),
                        CustomerName = reader["customer_name"].ToString(),
                        VehicleName = reader["vehicle_name"].ToString()
                    });
                }

                return Ok(ratings);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ GET TOP DRIVERS — para sa leaderboard sa admin ══
        [HttpGet("top-drivers")]
        public IActionResult GetTopDrivers()
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT d.driver_id, u.full_name,
                           ROUND(AVG(r.driver_score), 2) AS avg_rating,
                           COUNT(r.rating_id) AS total_reviews,
                           d.total_trips
                    FROM ratings r
                    JOIN drivers d ON r.driver_id = d.driver_id
                    JOIN users u   ON d.user_id   = u.user_id
                    WHERE r.driver_score IS NOT NULL
                    GROUP BY d.driver_id, u.full_name, d.total_trips
                    ORDER BY avg_rating DESC
                    LIMIT 10", conn);

                var results = new List<object>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(new
                    {
                        driver_id = Convert.ToInt32(reader["driver_id"]),
                        full_name = reader["full_name"].ToString(),
                        avg_rating = Convert.ToDecimal(reader["avg_rating"]),
                        total_reviews = Convert.ToInt32(reader["total_reviews"]),
                        total_trips = Convert.ToInt32(reader["total_trips"])
                    });
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ POST — customer nag-submit ng rating ══
        [HttpPost]
        public IActionResult AddRating([FromBody] Rating rating)
        {
            // Validate required fields
            if (rating.RentalId == 0 || rating.CustomerId == 0 ||
                rating.VehicleId == 0)
                return BadRequest(new
                {
                    message =
                    "RentalId, CustomerId, and VehicleId are required."
                });

            // Validate scores — dapat 1 to 5 lang
            if (rating.VehicleScore < 1 || rating.VehicleScore > 5)
                return BadRequest(new
                {
                    message =
                    "Vehicle score must be between 1 and 5."
                });

            if (rating.DriverScore.HasValue &&
                (rating.DriverScore < 1 || rating.DriverScore > 5))
                return BadRequest(new
                {
                    message =
                    "Driver score must be between 1 and 5."
                });

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // Check kung naka-complete na ang rental
                var rentalCmd = new MySqlCommand(@"
                    SELECT status FROM rentals
                    WHERE rental_id = @rental_id", conn);
                rentalCmd.Parameters.AddWithValue("@rental_id",
                    rating.RentalId);
                var status = rentalCmd.ExecuteScalar()?.ToString();

                if (status == null)
                    return NotFound(new { message = "Rental not found." });

                if (status != "completed")
                    return BadRequest(new
                    {
                        message =
                        "Rating can only be submitted for completed rentals."
                    });

                // Check kung naka-rate na ang rental na ito
                var checkCmd = new MySqlCommand(@"
                    SELECT COUNT(*) FROM ratings
                    WHERE rental_id  = @rental_id
                    AND   customer_id = @customer_id", conn);
                checkCmd.Parameters.AddWithValue("@rental_id",
                    rating.RentalId);
                checkCmd.Parameters.AddWithValue("@customer_id",
                    rating.CustomerId);
                var alreadyRated = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (alreadyRated > 0)
                    return Conflict(new
                    {
                        message =
                        "You have already rated this rental."
                    });

                // I-save ang rating
                var insertCmd = new MySqlCommand(@"
                    INSERT INTO ratings
                        (rental_id, customer_id, driver_id,
                         vehicle_id, driver_score, vehicle_score,
                         comment, rated_at)
                    VALUES
                        (@rental_id, @customer_id, @driver_id,
                         @vehicle_id, @driver_score, @vehicle_score,
                         @comment, NOW())", conn);

                insertCmd.Parameters.AddWithValue("@rental_id",
                    rating.RentalId);
                insertCmd.Parameters.AddWithValue("@customer_id",
                    rating.CustomerId);
                insertCmd.Parameters.AddWithValue("@driver_id",
                    rating.DriverId.HasValue
                    ? (object)rating.DriverId.Value
                    : DBNull.Value);
                insertCmd.Parameters.AddWithValue("@vehicle_id",
                    rating.VehicleId);
                insertCmd.Parameters.AddWithValue("@driver_score",
                    rating.DriverScore.HasValue
                    ? (object)rating.DriverScore.Value
                    : DBNull.Value);
                insertCmd.Parameters.AddWithValue("@vehicle_score",
                    rating.VehicleScore);
                insertCmd.Parameters.AddWithValue("@comment",
                    rating.Comment ?? "");

                insertCmd.ExecuteNonQuery();

                // I-update ang average rating ng driver
                if (rating.DriverId.HasValue)
                {
                    var updateDriverCmd = new MySqlCommand(@"
                        UPDATE drivers
                        SET rating_avg = (
                            SELECT ROUND(AVG(driver_score), 2)
                            FROM ratings
                            WHERE driver_id = @driver_id
                            AND driver_score IS NOT NULL
                        )
                        WHERE driver_id = @driver_id", conn);
                    updateDriverCmd.Parameters.AddWithValue("@driver_id", rating.DriverId.Value);
                    updateDriverCmd.ExecuteNonQuery();
                }

                return Ok(new { message = "Rating submitted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }
    }
}
