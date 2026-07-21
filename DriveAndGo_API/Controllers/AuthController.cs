using BCryptNet = BCrypt.Net.BCrypt;
using DriveAndGo_API.Contracts;
using DriveAndGo_API.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

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
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var existsCommand = new NpgsqlCommand(
                "SELECT COUNT(*) FROM users WHERE email = @email",
                connection);
            existsCommand.Parameters.AddWithValue("@email", request.Email.Trim());

            if (Convert.ToInt32(existsCommand.ExecuteScalar()) > 0)
            {
                return Conflict(new { Message = "Email is already registered." });
            }

            var hashedPassword = BCryptNet.HashPassword(request.Password);

            // PostgreSQL: use RETURNING to get the new ID in one round-trip
            using var insertCommand = new NpgsqlCommand(
                @"INSERT INTO users
                    (full_name, email, password_hash, phone, role, created_at)
                  VALUES
                    (@full_name, @email, @password_hash, @phone, @role, NOW())
                  RETURNING user_id",
                connection);

            insertCommand.Parameters.AddWithValue("@full_name", request.FullName.Trim());
            insertCommand.Parameters.AddWithValue("@email", request.Email.Trim());
            insertCommand.Parameters.AddWithValue("@password_hash", hashedPassword);
            insertCommand.Parameters.AddWithValue("@phone", string.IsNullOrWhiteSpace(request.Phone) ? string.Empty : request.Phone.Trim());
            insertCommand.Parameters.AddWithValue("@role", normalizedRole);

            var userId = Convert.ToInt32(insertCommand.ExecuteScalar());

            return Ok(new AuthResponse
            {
                Message  = "Registration successful.",
                UserId   = userId,
                FullName = request.FullName.Trim(),
                Email    = request.Email.Trim(),
                Phone    = string.IsNullOrWhiteSpace(request.Phone) ? string.Empty : request.Phone.Trim(),
                Role     = normalizedRole
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
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(
                @"SELECT
                    u.user_id,
                    u.full_name,
                    u.email,
                    u.password_hash,
                    u.phone,
                    u.role,
                    u.failed_login_attempts,
                    u.lockout_enabled,
                    u.lockout_end,
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
            int userId = Convert.ToInt32(reader["user_id"]);
            int failedAttempts = reader["failed_login_attempts"] != DBNull.Value ? Convert.ToInt32(reader["failed_login_attempts"]) : 0;
            bool lockoutEnabled = reader["lockout_enabled"] != DBNull.Value && Convert.ToBoolean(reader["lockout_enabled"]);
            var lockoutEnd = reader["lockout_end"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(reader["lockout_end"]) : null;

            if (lockoutEnabled && lockoutEnd.HasValue && lockoutEnd.Value > DateTime.UtcNow)
            {
                return StatusCode(423, new { Message = $"Account locked due to consecutive failed login attempts. Try again after {lockoutEnd.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}." });
            }

            bool isValid = false;
            try
            {
                isValid = BCryptNet.Verify(request.Password, storedHash);
            }
            catch
            {
                isValid = string.Equals(request.Password, storedHash, StringComparison.Ordinal);
            }

            if (!isValid && !string.Equals(request.Password, storedHash, StringComparison.Ordinal))
            {
                reader.Close();
                failedAttempts++;
                if (failedAttempts >= 5)
                {
                    DateTime lockoutEndVal = DateTime.UtcNow.AddMinutes(15);
                    using (var lockCmd = new NpgsqlCommand("UPDATE users SET failed_login_attempts = @attempts, lockout_enabled = true, lockout_end = @end WHERE user_id = @uid", connection))
                    {
                        lockCmd.Parameters.AddWithValue("@attempts", failedAttempts);
                        lockCmd.Parameters.AddWithValue("@end", lockoutEndVal);
                        lockCmd.Parameters.AddWithValue("@uid", userId);
                        lockCmd.ExecuteNonQuery();
                    }
                    return StatusCode(423, new { Message = "Account locked for 15 minutes due to 5 consecutive failed login attempts." });
                }
                else
                {
                    using (var failCmd = new NpgsqlCommand("UPDATE users SET failed_login_attempts = @attempts WHERE user_id = @uid", connection))
                    {
                        failCmd.Parameters.AddWithValue("@attempts", failedAttempts);
                        failCmd.Parameters.AddWithValue("@uid", userId);
                        failCmd.ExecuteNonQuery();
                    }
                    return Unauthorized(new { Message = $"Invalid email or password. Attempt {failedAttempts} of 5." });
                }
            }

            // Reset failed login attempts on successful login
            reader.Close();
            if (failedAttempts > 0 || lockoutEnabled)
            {
                using (var resetCmd = new NpgsqlCommand("UPDATE users SET failed_login_attempts = 0, lockout_enabled = false, lockout_end = NULL WHERE user_id = @uid", connection))
                {
                    resetCmd.Parameters.AddWithValue("@uid", userId);
                    resetCmd.ExecuteNonQuery();
                }
            }

            // Re-open reader to fetch the results or query again (simpler to query again or read variables)
            string fullName = "";
            string email = "";
            string phone = "";
            string role = "";
            int? driverId = null;

            using (var finalCmd = new NpgsqlCommand(
                @"SELECT u.full_name, u.email, u.phone, u.role, d.driver_id 
                  FROM users u LEFT JOIN drivers d ON d.user_id = u.user_id 
                  WHERE u.user_id = @uid", connection))
            {
                finalCmd.Parameters.AddWithValue("@uid", userId);
                using var finalReader = finalCmd.ExecuteReader();
                if (finalReader.Read())
                {
                    fullName = finalReader["full_name"]?.ToString() ?? string.Empty;
                    email = finalReader["email"]?.ToString() ?? string.Empty;
                    phone = finalReader["phone"]?.ToString() ?? string.Empty;
                    role = finalReader["role"]?.ToString() ?? "customer";
                    driverId = finalReader["driver_id"] == DBNull.Value ? null : (int?)Convert.ToInt32(finalReader["driver_id"]);
                }
            }

            return Ok(new AuthResponse
            {
                Message  = "Login successful.",
                UserId   = userId,
                DriverId = driverId,
                FullName = fullName,
                Email    = email,
                Phone    = phone,
                Role     = role
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
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(
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
