using DriveAndGo_API.Models;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly string _connectionString;

        public AuthController(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")!;
        }

        // ══ REGISTER ══
        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                string.IsNullOrWhiteSpace(request.FullName))
                return BadRequest(new
                {
                    message = "Kumpletuhin ang lahat ng fields."
                });

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // Check kung naka-register na ang email
                var checkCmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM users WHERE email = @email", conn);
                checkCmd.Parameters.AddWithValue("@email", request.Email);
                var count = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (count > 0)
                    return Conflict(new
                    {
                        message = "Email already registered. Mag-login na lang!"
                    });

                // Hash ang password gamit ang BCrypt.Net-Next
                string hashedPassword = BCrypt.Net.BCrypt
                    .HashPassword(request.Password);

                // I-save sa database
                var insertCmd = new MySqlCommand(@"
                    INSERT INTO users
                        (full_name, email, password_hash,
                         phone, role, created_at)
                    VALUES
                        (@full_name, @email, @password_hash,
                         @phone, @role, NOW())", conn);

                insertCmd.Parameters.AddWithValue("@full_name", request.FullName);
                insertCmd.Parameters.AddWithValue("@email", request.Email);
                insertCmd.Parameters.AddWithValue("@password_hash", hashedPassword);
                insertCmd.Parameters.AddWithValue("@phone", request.Phone ?? "");
                insertCmd.Parameters.AddWithValue("@role", request.Role ?? "customer");

                insertCmd.ExecuteNonQuery();

                // Kunin ang bagong user_id
                var idCmd = new MySqlCommand("SELECT LAST_INSERT_ID()", conn);
                int newUserId = Convert.ToInt32(idCmd.ExecuteScalar());

                return Ok(new
                {
                    message = "Registration successful! Welcome sa DriveAndGo!",
                    user_id = newUserId,
                    full_name = request.FullName,
                    email = request.Email,
                    role = request.Role ?? "customer"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Registration failed: " + ex.Message
                });
            }
        }

        // ══ LOGIN ══
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new
                {
                    message = "Email at password ay required."
                });

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT user_id, full_name, email,
                           password_hash, phone, role
                    FROM users
                    WHERE email = @email
                    LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@email", request.Email);

                using var reader = cmd.ExecuteReader();

                if (!reader.Read())
                    return Unauthorized(new
                    {
                        message = "Email not found. Mag-register muna!"
                    });

                string storedHash = reader["password_hash"].ToString()!;
                bool isPasswordCorrect = BCrypt.Net.BCrypt
                    .Verify(request.Password, storedHash);

                if (!isPasswordCorrect)
                    return Unauthorized(new
                    {
                        message = "Mali ang password. Subukan ulit!"
                    });

                return Ok(new
                {
                    message = "Login successful! Welcome back!",
                    user_id = Convert.ToInt32(reader["user_id"]),
                    full_name = reader["full_name"].ToString(),
                    email = reader["email"].ToString(),
                    phone = reader["phone"].ToString(),
                    role = reader["role"].ToString()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Login failed: " + ex.Message
                });
            }
        }

        // ══ CHECK EMAIL — real-time validation sa mobile app ══
        [HttpGet("check-email")]
        public IActionResult CheckEmail([FromQuery] string email)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM users WHERE email = @email", conn);
                cmd.Parameters.AddWithValue("@email", email);
                var count = Convert.ToInt32(cmd.ExecuteScalar());

                return Ok(new { exists = count > 0 });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}