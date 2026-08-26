using BCryptNet = BCrypt.Net.BCrypt;
using DriveAndGo_API.Contracts;
using DriveAndGo_API.Models;
using DriveAndGo_API.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace DriveAndGo_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly string _connectionString;
    private readonly IEmailService _emailService;
    private readonly AuditService _auditService;

    public UsersController(IConfiguration configuration, IEmailService emailService, AuditService auditService)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        _emailService = emailService;
        _auditService = auditService;
        EnsureColumnsExist();
    }

    private void EnsureColumnsExist()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(@"
                ALTER TABLE users ADD COLUMN IF NOT EXISTS avatar_base64 TEXT;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS two_factor_enabled BOOLEAN DEFAULT FALSE;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS login_alerts_enabled BOOLEAN DEFAULT TRUE;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS pin_required BOOLEAN DEFAULT FALSE;
            ", connection);
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    [HttpGet]
    public IActionResult GetUsers()
    {
        try
        {
            var users = new List<User>();

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(
                "SELECT user_id, full_name, email, phone, role FROM users ORDER BY full_name ASC",
                connection);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                users.Add(new User
                {
                    UserId = Convert.ToInt32(reader["user_id"]),
                    FullName = reader["full_name"]?.ToString() ?? string.Empty,
                    Email = reader["email"]?.ToString() ?? string.Empty,
                    Phone = reader["phone"]?.ToString() ?? string.Empty,
                    Role = reader["role"]?.ToString() ?? string.Empty
                });
            }

            return Ok(users);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    public IActionResult GetUserById(int id)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(
                @"SELECT
                    u.user_id,
                    u.full_name,
                    u.email,
                    u.phone,
                    u.role,
                    COALESCE(u.avatar_base64, u.id_photo_url) AS avatar_base64,
                    COALESCE(u.id_photo_url, u.avatar_base64) AS id_photo_url,
                    u.two_factor_enabled,
                    u.login_alerts_enabled,
                    u.pin_required,
                    d.driver_id
                  FROM users u
                  LEFT JOIN drivers d ON d.user_id = u.user_id
                  WHERE u.user_id = @id
                  LIMIT 1",
                connection);

            command.Parameters.AddWithValue("@id", id);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return NotFound(new { Message = "User not found." });
            }

            return Ok(new
            {
                UserId = Convert.ToInt32(reader["user_id"]),
                DriverId = reader["driver_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["driver_id"]),
                FullName = reader["full_name"]?.ToString() ?? string.Empty,
                Email = reader["email"]?.ToString() ?? string.Empty,
                Phone = reader["phone"]?.ToString() ?? string.Empty,
                Role = reader["role"]?.ToString() ?? string.Empty,
                AvatarBase64 = reader["avatar_base64"] == DBNull.Value ? string.Empty : reader["avatar_base64"]?.ToString(),
                IdPhotoUrl = reader["id_photo_url"] == DBNull.Value ? string.Empty : reader["id_photo_url"]?.ToString(),
                TwoFactorEnabled = reader["two_factor_enabled"] != DBNull.Value && Convert.ToBoolean(reader["two_factor_enabled"]),
                LoginAlertsEnabled = reader["login_alerts_enabled"] == DBNull.Value || Convert.ToBoolean(reader["login_alerts_enabled"]),
                PinRequired = reader["pin_required"] != DBNull.Value && Convert.ToBoolean(reader["pin_required"])
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public IActionResult UpdateUser(int id, [FromBody] UpdateUserProfileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { Message = "Full name and email are required." });
        }

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var duplicateCommand = new NpgsqlCommand(
                @"SELECT COUNT(*) FROM users
                  WHERE email = @email AND user_id <> @id",
                connection);
            duplicateCommand.Parameters.AddWithValue("@email", request.Email.Trim());
            duplicateCommand.Parameters.AddWithValue("@id", id);

            if (Convert.ToInt32(duplicateCommand.ExecuteScalar()) > 0)
            {
                return Conflict(new { Message = "Email is already in use by another account." });
            }

            using var updateCommand = new NpgsqlCommand(
                @"UPDATE users
                  SET full_name = @full_name,
                      email = @email,
                      phone = @phone
                  WHERE user_id = @id",
                connection);

            updateCommand.Parameters.AddWithValue("@full_name", request.FullName.Trim());
            updateCommand.Parameters.AddWithValue("@email", request.Email.Trim());
            updateCommand.Parameters.AddWithValue("@phone", string.IsNullOrWhiteSpace(request.Phone) ? string.Empty : request.Phone.Trim());
            updateCommand.Parameters.AddWithValue("@id", id);

            if (updateCommand.ExecuteNonQuery() == 0)
            {
                return NotFound(new { Message = "User not found." });
            }

            return Ok(new
            {
                Message = "Profile updated successfully.",
                UserId = id,
                FullName = request.FullName.Trim(),
                Email = request.Email.Trim(),
                Phone = string.IsNullOrWhiteSpace(request.Phone) ? string.Empty : request.Phone.Trim()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    public class UpdateAvatarRequest
    {
        public string? AvatarBase64 { get; set; }
    }

    [HttpPost("{id:int}/avatar")]
    public IActionResult UpdateAvatar(int id, [FromBody] UpdateAvatarRequest request)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(
                @"UPDATE users SET avatar_base64 = @avatar WHERE user_id = @id",
                connection);
            cmd.Parameters.AddWithValue("@avatar", request.AvatarBase64 ?? string.Empty);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();

            return Ok(new { Message = "Avatar updated in database." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    public class SecuritySettingsRequest
    {
        public bool TwoFactorEnabled { get; set; }
        public bool LoginAlertsEnabled { get; set; }
        public bool PinRequired { get; set; }
    }

    [HttpGet("{id:int}/security")]
    public IActionResult GetSecuritySettings(int id)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(
                "SELECT two_factor_enabled, login_alerts_enabled, pin_required FROM users WHERE user_id = @id",
                connection);
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return Ok(new
                {
                    TwoFactorEnabled = !reader.IsDBNull(0) && reader.GetBoolean(0),
                    LoginAlertsEnabled = reader.IsDBNull(1) || reader.GetBoolean(1),
                    PinRequired = !reader.IsDBNull(2) && reader.GetBoolean(2)
                });
            }
            return NotFound(new { Message = "User not found." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPut("{id:int}/security")]
    public async Task<IActionResult> UpdateSecurity(int id, [FromBody] SecuritySettingsRequest request)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(
                @"UPDATE users SET 
                    two_factor_enabled = @tf,
                    login_alerts_enabled = @la,
                    pin_required = @pr
                  WHERE user_id = @id",
                connection);
            cmd.Parameters.AddWithValue("@tf", request.TwoFactorEnabled);
            cmd.Parameters.AddWithValue("@la", request.LoginAlertsEnabled);
            cmd.Parameters.AddWithValue("@pr", request.PinRequired);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();

            // Log SECURITY_UPDATE to system_audit_logs table
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            string desc = $"Updated Security: 2FA={(request.TwoFactorEnabled ? "ON" : "OFF")}, Alerts={(request.LoginAlertsEnabled ? "ON" : "OFF")}, PIN={(request.PinRequired ? "ON" : "OFF")}";

            await _auditService.LogActionAsync(
                adminUserId: id,
                actionType: "SECURITY_UPDATE",
                targetUserId: id,
                ipAddress: clientIp,
                oldValues: new { description = "Security Settings Toggled" },
                newValues: new { description = desc, twoFactor = request.TwoFactorEnabled, alerts = request.LoginAlertsEnabled, pin = request.PinRequired }
            );

            return Ok(new { Message = "Security settings updated in database." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpGet("{id:int}/activity")]
    public IActionResult GetUserActivityLogs(int id)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var logs = new List<object>();

            using var cmd = new NpgsqlCommand(
                @"SELECT audit_id, action_type, metadata_json, timestamp, ip_address 
                  FROM system_audit_logs 
                  WHERE admin_user_id = @id OR target_user_id = @id OR admin_user_id IS NULL
                  ORDER BY timestamp DESC 
                  LIMIT 20",
                connection);
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int auditId = reader.GetInt32(0);
                string actionType = reader.GetString(1);
                string metadataJson = reader.IsDBNull(2) ? "{}" : reader.GetString(2);
                DateTime ts = reader.GetDateTime(3);
                string ip = reader.IsDBNull(4) ? "127.0.0.1" : reader.GetString(4);

                string description = actionType switch
                {
                    "USER_LOGIN" => "Logged into system",
                    "SECURITY_UPDATE" => "Updated security settings",
                    "PASSWORD_CHANGE" => "Changed account password",
                    _ => actionType.Replace("_", " ")
                };

                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(metadataJson);
                    if (doc.RootElement.TryGetProperty("newValues", out var nv) && nv.TryGetProperty("description", out var d))
                    {
                        string? dStr = d.GetString();
                        if (!string.IsNullOrWhiteSpace(dStr)) description = dStr;
                    }
                }
                catch { }

                logs.Add(new
                {
                    AuditId = auditId,
                    ActionType = actionType,
                    Description = description,
                    MetadataJson = metadataJson,
                    Timestamp = ts,
                    IpAddress = ip
                });
            }

            return Ok(logs);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPost("request-password-change-otp")]
    public async Task<IActionResult> RequestPasswordChangeOtp([FromBody] RequestPasswordChangeOtpRequest request)
    {
        if (request.UserId <= 0 || string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            return BadRequest(new { Message = "UserId and current password are required." });
        }

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            string email = "";
            string storedHash = "";

            using (var userCmd = new NpgsqlCommand("SELECT email, password_hash FROM users WHERE user_id = @id", connection))
            {
                userCmd.Parameters.AddWithValue("@id", request.UserId);
                using var reader = userCmd.ExecuteReader();
                if (!reader.Read())
                {
                    return NotFound(new { Message = "User account not found." });
                }
                email = reader["email"]?.ToString() ?? "";
                storedHash = reader["password_hash"]?.ToString() ?? "";
            }

            bool isValid = false;
            try
            {
                isValid = BCryptNet.Verify(request.CurrentPassword, storedHash);
            }
            catch
            {
                isValid = string.Equals(request.CurrentPassword, storedHash, StringComparison.Ordinal);
            }

            if (!isValid && !string.Equals(request.CurrentPassword, storedHash, StringComparison.Ordinal))
            {
                return BadRequest(new { Message = "Current password is incorrect." });
            }

            // Invalidate previous unused PASSWORD_CHANGE codes
            using (var invCmd = new NpgsqlCommand("UPDATE otp_codes SET is_used = true WHERE email = @email AND purpose = 'PASSWORD_CHANGE' AND is_used = false", connection))
            {
                invCmd.Parameters.AddWithValue("@email", email.Trim());
                invCmd.ExecuteNonQuery();
            }

            string otpCode = Random.Shared.Next(100000, 999999).ToString();
            using (var insCmd = new NpgsqlCommand(@"
                INSERT INTO otp_codes (email, otp_code, purpose, expires_at)
                VALUES (@email, @code, 'PASSWORD_CHANGE', NOW() + INTERVAL '2 minutes')", connection))
            {
                insCmd.Parameters.AddWithValue("@email", email.Trim());
                insCmd.Parameters.AddWithValue("@code", otpCode);
                insCmd.ExecuteNonQuery();
            }

            await _emailService.SendOtpEmailAsync(email.Trim(), otpCode, "PASSWORD_CHANGE");

            return Ok(new { Success = true, Email = email.Trim(), Message = "Password change verification OTP code sent to your email." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Failed to request password change OTP: " + ex.Message });
        }
    }

    [HttpPost("change-password-with-otp")]
    public IActionResult ChangePasswordWithOtp([FromBody] ChangePasswordWithOtpRequest request)
    {
        if (request.UserId <= 0 || string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword) || string.IsNullOrWhiteSpace(request.Otp))
        {
            return BadRequest(new { Message = "UserId, current password, new password, and OTP code are required." });
        }

        if (request.NewPassword.Trim().Length < 6)
        {
            return BadRequest(new { Message = "New password must be at least 6 characters long." });
        }

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            string email = "";
            string storedHash = "";

            using (var userCmd = new NpgsqlCommand("SELECT email, password_hash FROM users WHERE user_id = @id", connection))
            {
                userCmd.Parameters.AddWithValue("@id", request.UserId);
                using var reader = userCmd.ExecuteReader();
                if (!reader.Read())
                {
                    return NotFound(new { Message = "User account not found." });
                }
                email = reader["email"]?.ToString() ?? "";
                storedHash = reader["password_hash"]?.ToString() ?? "";
            }

            bool isValid = false;
            try
            {
                isValid = BCryptNet.Verify(request.CurrentPassword, storedHash);
            }
            catch
            {
                isValid = string.Equals(request.CurrentPassword, storedHash, StringComparison.Ordinal);
            }

            if (!isValid && !string.Equals(request.CurrentPassword, storedHash, StringComparison.Ordinal))
            {
                return BadRequest(new { Message = "Current password is incorrect." });
            }

            int otpId = 0;
            using (var checkCmd = new NpgsqlCommand(@"
                SELECT otp_id FROM otp_codes 
                WHERE email = @email AND otp_code = @code AND purpose = 'PASSWORD_CHANGE' AND is_used = false AND expires_at > NOW() 
                ORDER BY otp_id DESC LIMIT 1", connection))
            {
                checkCmd.Parameters.AddWithValue("@email", email.Trim());
                checkCmd.Parameters.AddWithValue("@code", request.Otp.Trim());

                var result = checkCmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                {
                    return BadRequest(new { Message = "Invalid or expired OTP code." });
                }
                otpId = Convert.ToInt32(result);
            }

            // Mark OTP as used
            using (var useCmd = new NpgsqlCommand("UPDATE otp_codes SET is_used = true WHERE otp_id = @id", connection))
            {
                useCmd.Parameters.AddWithValue("@id", otpId);
                useCmd.ExecuteNonQuery();
            }

            string newHash = BCryptNet.HashPassword(request.NewPassword.Trim());
            using (var updateCmd = new NpgsqlCommand("UPDATE users SET password_hash = @hash WHERE user_id = @id", connection))
            {
                updateCmd.Parameters.AddWithValue("@hash", newHash);
                updateCmd.Parameters.AddWithValue("@id", request.UserId);
                updateCmd.ExecuteNonQuery();
            }

            return Ok(new { Success = true, Message = "Password updated successfully!" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Failed to update password: " + ex.Message });
        }
    }

    public class CreateCustomerRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? AvatarBase64 { get; set; }
        public string? IdPhotoUrl { get; set; }
        public string? Address { get; set; }
        public string? DriverLicenseNo { get; set; }
        public string? InitialPassword { get; set; }
    }

    [HttpPost]
    public IActionResult CreateCustomer([FromBody] CreateCustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new { Message = "Customer full name is required." });
        }

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            string cleanPhone = string.IsNullOrWhiteSpace(request.Phone) ? string.Empty : request.Phone.Trim();
            string email = string.IsNullOrWhiteSpace(request.Email)
                ? $"walkin_{DateTime.UtcNow.Ticks}@driveandgo.com"
                : request.Email.Trim();

            // Check if phone or email already matches an existing customer
            using var checkCmd = new NpgsqlCommand(
                @"SELECT user_id, full_name, email, phone FROM users 
                  WHERE email = @email OR (phone = @phone AND phone <> '') 
                  LIMIT 1", connection);
            checkCmd.Parameters.AddWithValue("@email", email);
            checkCmd.Parameters.AddWithValue("@phone", cleanPhone);
            using var reader = checkCmd.ExecuteReader();
            if (reader.Read())
            {
                return Ok(new
                {
                    UserId = Convert.ToInt32(reader["user_id"]),
                    FullName = reader["full_name"]?.ToString() ?? request.FullName.Trim(),
                    Email = reader["email"]?.ToString() ?? email,
                    Phone = reader["phone"]?.ToString() ?? cleanPhone,
                    Role = "customer",
                    Message = "Customer record found."
                });
            }
            reader.Close();

            string rawPassword = string.IsNullOrWhiteSpace(request.InitialPassword) ? "DriveAndGoWalkIn2026!" : request.InitialPassword.Trim();
            string defaultHash = BCryptNet.HashPassword(rawPassword);

            using var insertCmd = new NpgsqlCommand(@"
                INSERT INTO users (full_name, email, password_hash, phone, role, avatar_base64, id_photo_url, created_at)
                VALUES (@full_name, @email, @password_hash, @phone, 'customer', @avatar_base64, @id_photo_url, NOW())
                RETURNING user_id", connection);

            insertCmd.Parameters.AddWithValue("@full_name", request.FullName.Trim());
            insertCmd.Parameters.AddWithValue("@email", email);
            insertCmd.Parameters.AddWithValue("@password_hash", defaultHash);
            insertCmd.Parameters.AddWithValue("@phone", cleanPhone);
            insertCmd.Parameters.AddWithValue("@avatar_base64", string.IsNullOrWhiteSpace(request.AvatarBase64) ? (object)DBNull.Value : request.AvatarBase64.Trim());
            insertCmd.Parameters.AddWithValue("@id_photo_url", string.IsNullOrWhiteSpace(request.IdPhotoUrl) ? (object)DBNull.Value : request.IdPhotoUrl.Trim());

            var newId = Convert.ToInt32(insertCmd.ExecuteScalar());

            return Ok(new
            {
                UserId = newId,
                FullName = request.FullName.Trim(),
                Email = email,
                Phone = cleanPhone,
                Role = "customer",
                Message = "Walk-in customer registered successfully."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }
}
