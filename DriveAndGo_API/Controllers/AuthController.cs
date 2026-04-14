using BCryptNet = BCrypt.Net.BCrypt;
using DriveAndGo_API.Contracts;
using DriveAndGo_API.Models;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace DriveAndGo_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly string _connectionString;

    public AuthController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new { Message = "Complete the required registration fields." });
        }

        var normalizedRole = string.Equals(request.Role, "driver", StringComparison.OrdinalIgnoreCase)
            ? "driver"
            : "customer";

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var existsCommand = new MySqlCommand(
                "SELECT COUNT(*) FROM users WHERE email = @email",
                connection);
            existsCommand.Parameters.AddWithValue("@email", request.Email.Trim());

            if (Convert.ToInt32(existsCommand.ExecuteScalar()) > 0)
            {
                return Conflict(new { Message = "Email is already registered." });
            }

            var hashedPassword = BCryptNet.HashPassword(request.Password);

            using var insertCommand = new MySqlCommand(
                @"INSERT INTO users
                    (full_name, email, password_hash, phone, role, created_at)
                  VALUES
                    (@full_name, @email, @password_hash, @phone, @role, NOW())",
                connection);

            insertCommand.Parameters.AddWithValue("@full_name", request.FullName.Trim());
            insertCommand.Parameters.AddWithValue("@email", request.Email.Trim());
            insertCommand.Parameters.AddWithValue("@password_hash", hashedPassword);
            insertCommand.Parameters.AddWithValue("@phone", string.IsNullOrWhiteSpace(request.Phone) ? string.Empty : request.Phone.Trim());
            insertCommand.Parameters.AddWithValue("@role", normalizedRole);
            insertCommand.ExecuteNonQuery();

            var userId = Convert.ToInt32(new MySqlCommand("SELECT LAST_INSERT_ID()", connection).ExecuteScalar());

            return Ok(new AuthResponse
            {
                Message = "Registration successful.",
                UserId = userId,
                FullName = request.FullName.Trim(),
                Email = request.Email.Trim(),
                Phone = string.IsNullOrWhiteSpace(request.Phone) ? string.Empty : request.Phone.Trim(),
                Role = normalizedRole
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Registration failed: " + ex.Message });
        }
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { Message = "Email and password are required." });
        }

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var command = new MySqlCommand(
                @"SELECT
                    u.user_id,
                    u.full_name,
                    u.email,
                    u.password_hash,
                    u.phone,
                    u.role,
                    d.driver_id
                  FROM users u
                  LEFT JOIN drivers d ON d.user_id = u.user_id
                  WHERE u.email = @email
                  LIMIT 1",
                connection);

            command.Parameters.AddWithValue("@email", request.Email.Trim());

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return Unauthorized(new { Message = "Account not found." });
            }

            var storedHash = reader["password_hash"]?.ToString() ?? string.Empty;
            if (!BCryptNet.Verify(request.Password, storedHash))
            {
                return Unauthorized(new { Message = "Invalid email or password." });
            }

            return Ok(new AuthResponse
            {
                Message = "Login successful.",
                UserId = Convert.ToInt32(reader["user_id"]),
                DriverId = reader["driver_id"] == DBNull.Value ? null : Convert.ToInt32(reader["driver_id"]),
                FullName = reader["full_name"]?.ToString() ?? string.Empty,
                Email = reader["email"]?.ToString() ?? string.Empty,
                Phone = reader["phone"]?.ToString() ?? string.Empty,
                Role = reader["role"]?.ToString() ?? "customer"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Login failed: " + ex.Message });
        }
    }

    [HttpGet("check-email")]
    public IActionResult CheckEmail([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { Message = "Email is required." });
        }

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var command = new MySqlCommand(
                "SELECT COUNT(*) FROM users WHERE email = @email",
                connection);
            command.Parameters.AddWithValue("@email", email.Trim());

            return Ok(new { Exists = Convert.ToInt32(command.ExecuteScalar()) > 0 });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = ex.Message });
        }
    }
}
