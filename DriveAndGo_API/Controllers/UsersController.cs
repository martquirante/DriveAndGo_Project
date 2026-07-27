using DriveAndGo_API.Contracts;
using DriveAndGo_API.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace DriveAndGo_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly string _connectionString;

    public UsersController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
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
                    u.avatar_base64,
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

    [HttpPut("{id:int}/security")]
    public IActionResult UpdateSecurity(int id, [FromBody] SecuritySettingsRequest request)
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

            return Ok(new { Message = "Security settings updated in database." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }
}
