using BCryptNet = BCrypt.Net.BCrypt;
using DriveAndGo_API.Contracts;
using DriveAndGo_API.Models;
using DriveAndGo_API.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace DriveAndGo_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly string _connectionString;
    private readonly IEmailService _emailService;
    private readonly AuditService _auditService;

    public AuthController(IConfiguration configuration, IEmailService emailService, AuditService auditService)
    {
        var connStr = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connStr) || connStr.Contains("YOUR_DB_PASSWORD"))
        {
            connStr = Environment.GetEnvironmentVariable("DEFAULT_CONNECTION")
                ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? connStr;
        }
        _connectionString = connStr!;
        _emailService = emailService;
        _auditService = auditService;
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
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("AuthPolicy")]
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
                    u.two_factor_enabled,
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
            bool twoFactorEnabled = reader["two_factor_enabled"] != DBNull.Value && Convert.ToBoolean(reader["two_factor_enabled"]);

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

            if (!isValid)
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

            // Reset failed login attempts on successful credentials match
            reader.Close();
            if (failedAttempts > 0 || lockoutEnabled)
            {
                using (var resetCmd = new NpgsqlCommand("UPDATE users SET failed_login_attempts = 0, lockout_enabled = false, lockout_end = NULL WHERE user_id = @uid", connection))
                {
                    resetCmd.Parameters.AddWithValue("@uid", userId);
                    resetCmd.ExecuteNonQuery();
                }
            }

            // ── 2FA ENFORCEMENT CHECK ──
            if (twoFactorEnabled)
            {
                // Invalidate any existing unused 2FA OTP codes for this email
                using (var invCmd = new NpgsqlCommand("UPDATE otp_codes SET is_used = true WHERE email = @email AND purpose = '2FA' AND is_used = false", connection))
                {
                    invCmd.Parameters.AddWithValue("@email", request.Email.Trim());
                    invCmd.ExecuteNonQuery();
                }

                string otpCode = Random.Shared.Next(100000, 999999).ToString();
                using (var insCmd = new NpgsqlCommand(@"
                    INSERT INTO otp_codes (email, otp_code, purpose, expires_at)
                    VALUES (@email, @code, '2FA', NOW() + INTERVAL '2 minutes')", connection))
                {
                    insCmd.Parameters.AddWithValue("@email", request.Email.Trim());
                    insCmd.Parameters.AddWithValue("@code", otpCode);
                    insCmd.ExecuteNonQuery();
                }

                _ = _emailService.SendOtpEmailAsync(request.Email.Trim(), otpCode, "2FA");

                return Ok(new
                {
                    Requires2FA = true,
                    Email = request.Email.Trim(),
                    Message = "2FA verification required. Verification code sent to your email."
                });
            }

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

            // Log USER_LOGIN in audit logs asynchronously
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            _ = _auditService.LogActionAsync(
                adminUserId: userId,
                actionType: "USER_LOGIN",
                targetUserId: userId,
                ipAddress: clientIp,
                oldValues: new { description = "Session Init" },
                newValues: new { description = "Logged into system", email = email, role = role }
            );

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

    [HttpPost("verify-2fa")]
    public IActionResult Verify2Fa([FromBody] Verify2FaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Otp))
        {
            return BadRequest(new { Message = "Email and OTP code are required." });
        }

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            int otpId = 0;
            using (var checkCmd = new NpgsqlCommand(@"
                SELECT otp_id FROM otp_codes 
                WHERE email = @email AND otp_code = @code AND purpose = '2FA' AND is_used = false AND expires_at > NOW() 
                ORDER BY otp_id DESC LIMIT 1", connection))
            {
                checkCmd.Parameters.AddWithValue("@email", request.Email.Trim());
                checkCmd.Parameters.AddWithValue("@code", request.Otp.Trim());

                var result = checkCmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                {
                    return BadRequest(new { Message = "Invalid or expired 2FA OTP code." });
                }
                otpId = Convert.ToInt32(result);
            }

            // Mark OTP as used
            using (var useCmd = new NpgsqlCommand("UPDATE otp_codes SET is_used = true WHERE otp_id = @id", connection))
            {
                useCmd.Parameters.AddWithValue("@id", otpId);
                useCmd.ExecuteNonQuery();
            }

            // Fetch user info for auth response
            string fullName = "";
            string email = "";
            string phone = "";
            string role = "";
            int userId = 0;
            int? driverId = null;

            using (var userCmd = new NpgsqlCommand(
                @"SELECT u.user_id, u.full_name, u.email, u.phone, u.role, d.driver_id 
                  FROM users u LEFT JOIN drivers d ON d.user_id = u.user_id 
                  WHERE u.email = @email LIMIT 1", connection))
            {
                userCmd.Parameters.AddWithValue("@email", request.Email.Trim());
                using var reader = userCmd.ExecuteReader();
                if (reader.Read())
                {
                    userId = Convert.ToInt32(reader["user_id"]);
                    fullName = reader["full_name"]?.ToString() ?? string.Empty;
                    email = reader["email"]?.ToString() ?? string.Empty;
                    phone = reader["phone"]?.ToString() ?? string.Empty;
                    role = reader["role"]?.ToString() ?? "customer";
                    driverId = reader["driver_id"] == DBNull.Value ? null : (int?)Convert.ToInt32(reader["driver_id"]);
                }
            }

            return Ok(new AuthResponse
            {
                Message  = "2FA Authentication successful.",
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
            return StatusCode(500, new { Message = "2FA Verification failed: " + ex.Message });
        }
    }

    [HttpPost("send-reset-otp")]
    public async Task<IActionResult> SendResetOtp([FromBody] SendResetOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { Message = "Email address is required." });
        }

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            // Verify user exists
            using (var userCheck = new NpgsqlCommand("SELECT COUNT(*) FROM users WHERE email = @email", connection))
            {
                userCheck.Parameters.AddWithValue("@email", request.Email.Trim());
                if (Convert.ToInt32(userCheck.ExecuteScalar()) == 0)
                {
                    return NotFound(new { Message = "No account found with this email address." });
                }
            }

            // Invalidate previous unused PASSWORD_RESET codes
            using (var invCmd = new NpgsqlCommand("UPDATE otp_codes SET is_used = true WHERE email = @email AND purpose = 'PASSWORD_RESET' AND is_used = false", connection))
            {
                invCmd.Parameters.AddWithValue("@email", request.Email.Trim());
                invCmd.ExecuteNonQuery();
            }

            string otpCode = Random.Shared.Next(100000, 999999).ToString();
            using (var insCmd = new NpgsqlCommand(@"
                INSERT INTO otp_codes (email, otp_code, purpose, expires_at)
                VALUES (@email, @code, 'PASSWORD_RESET', NOW() + INTERVAL '2 minutes')", connection))
            {
                insCmd.Parameters.AddWithValue("@email", request.Email.Trim());
                insCmd.Parameters.AddWithValue("@code", otpCode);
                insCmd.ExecuteNonQuery();
            }

            await _emailService.SendOtpEmailAsync(request.Email.Trim(), otpCode, "PASSWORD_RESET");

            return Ok(new { Success = true, Message = "Password reset OTP verification code sent to your email." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Failed to send reset OTP: " + ex.Message });
        }
    }

    [HttpPost("reset-password-with-otp")]
    public IActionResult ResetPasswordWithOtp([FromBody] ResetPasswordWithOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Otp) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { Message = "Email, OTP code, and new password are required." });
        }

        if (request.NewPassword.Trim().Length < 6)
        {
            return BadRequest(new { Message = "New password must be at least 6 characters long." });
        }

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            int otpId = 0;
            using (var checkCmd = new NpgsqlCommand(@"
                SELECT otp_id FROM otp_codes 
                WHERE email = @email AND otp_code = @code AND purpose = 'PASSWORD_RESET' AND is_used = false AND expires_at > NOW() 
                ORDER BY otp_id DESC LIMIT 1", connection))
            {
                checkCmd.Parameters.AddWithValue("@email", request.Email.Trim());
                checkCmd.Parameters.AddWithValue("@code", request.Otp.Trim());

                var result = checkCmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                {
                    return BadRequest(new { Message = "Invalid or expired password reset OTP code." });
                }
                otpId = Convert.ToInt32(result);
            }

            // Mark OTP as used
            using (var useCmd = new NpgsqlCommand("UPDATE otp_codes SET is_used = true WHERE otp_id = @id", connection))
            {
                useCmd.Parameters.AddWithValue("@id", otpId);
                useCmd.ExecuteNonQuery();
            }

            // Hash new password and update user's record
            string newHashedPassword = BCryptNet.HashPassword(request.NewPassword.Trim());
            using (var updateCmd = new NpgsqlCommand(@"
                UPDATE users 
                SET password_hash = @hash, failed_login_attempts = 0, lockout_enabled = false, lockout_end = NULL 
                WHERE email = @email", connection))
            {
                updateCmd.Parameters.AddWithValue("@hash", newHashedPassword);
                updateCmd.Parameters.AddWithValue("@email", request.Email.Trim());
                updateCmd.ExecuteNonQuery();
            }

            return Ok(new { Success = true, Message = "Password reset successful! You can now log in with your new password." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Failed to reset password: " + ex.Message });
        }
    }

    [HttpPost("verify-reset-otp")]
    public IActionResult VerifyResetOtp([FromBody] VerifyResetOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Otp))
        {
            return BadRequest(new { Message = "Email and OTP code are required." });
        }

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using (var checkCmd = new NpgsqlCommand(@"
                SELECT otp_id FROM otp_codes 
                WHERE email = @email AND otp_code = @code AND purpose = 'PASSWORD_RESET' AND is_used = false AND expires_at > NOW() 
                ORDER BY otp_id DESC LIMIT 1", connection))
            {
                checkCmd.Parameters.AddWithValue("@email", request.Email.Trim());
                checkCmd.Parameters.AddWithValue("@code", request.Otp.Trim());

                var result = checkCmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                {
                    return BadRequest(new { Message = "Invalid or expired password reset OTP code." });
                }
            }

            return Ok(new { Success = true, Message = "OTP code verified successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "OTP verification failed: " + ex.Message });
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

public class VerifyResetOtpRequest
{
    public string Email { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
}
