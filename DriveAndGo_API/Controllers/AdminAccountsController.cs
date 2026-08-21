using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using DriveAndGo_API.Hubs;
using BCryptNet = BCrypt.Net.BCrypt;

namespace DriveAndGo_API.Controllers
{
    [Route("api/admin/accounts")]
    [ApiController]
    public class AdminAccountsController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly IHubContext<AdminHub> _hubContext;
        private readonly DriveAndGo_API.Services.AuditService _auditService;

        public AdminAccountsController(IConfiguration configuration, IHubContext<AdminHub> hubContext, DriveAndGo_API.Services.AuditService auditService)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _hubContext = hubContext;
            _auditService = auditService;
        }

        // DTOs for client requests/responses
        public class AccountDto
        {
            public int UserId { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public string? IdPhotoUrl { get; set; }
            public string? AvatarBase64 { get; set; }
            public string? SignatureBase64 { get; set; }
            public string? SignatureUrl { get; set; }
            public DateTime CreatedAt { get; set; }

            // Driver specific fields (null if not a driver)
            public int? DriverId { get; set; }
            public string? LicenseNo { get; set; }
            public string? LicensePhotoUrl { get; set; }
            public string? DriverStatus { get; set; }
            public decimal? RatingAvg { get; set; }
            public int? TotalTrips { get; set; }
        }

        public class CreateAccountRequest
        {
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty; // "admin", "driver", "customer"
            public string? IdPhotoUrl { get; set; }
            public string? AvatarBase64 { get; set; }
            public string? SignatureBase64 { get; set; }

            // Driver fields:
            public string? LicenseNo { get; set; }
            public string? LicensePhotoUrl { get; set; }
            public string? DriverStatus { get; set; } = "available";
        }

        public class UpdateAccountRequest
        {
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string? Password { get; set; } // Only updated if not empty
            public string Phone { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public string? IdPhotoUrl { get; set; }
            public string? AvatarBase64 { get; set; }
            public string? SignatureBase64 { get; set; }

            // Driver fields:
            public string? LicenseNo { get; set; }
            public string? LicensePhotoUrl { get; set; }
            public string? DriverStatus { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> GetAccounts([FromQuery] string? role = null)
        {
            try
            {
                var accounts = new List<AccountDto>();
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Ensure columns exist in database
                using (var initCmd = new NpgsqlCommand(@"
                    ALTER TABLE users ADD COLUMN IF NOT EXISTS avatar_base64 TEXT;
                    ALTER TABLE users ADD COLUMN IF NOT EXISTS signature_base64 TEXT;
                    ALTER TABLE users ADD COLUMN IF NOT EXISTS signature_url TEXT;
                ", connection))
                {
                    try { await initCmd.ExecuteNonQueryAsync(); } catch { }
                }

                string query = @"
                    SELECT 
                        u.user_id, 
                        u.full_name, 
                        u.email, 
                        u.phone, 
                        u.role, 
                        COALESCE(u.avatar_base64, u.id_photo_url) AS id_photo_url,
                        u.avatar_base64,
                        u.signature_base64,
                        u.signature_url,
                        u.created_at,
                        d.driver_id,
                        d.license_no,
                        d.license_photo_url,
                        d.status AS driver_status,
                        d.rating_avg,
                        d.total_trips
                    FROM users u
                    LEFT JOIN drivers d ON d.user_id = u.user_id";

                if (!string.IsNullOrWhiteSpace(role) && !string.Equals(role, "all", StringComparison.OrdinalIgnoreCase))
                {
                    query += " WHERE LOWER(u.role) = LOWER(@role)";
                }

                query += " ORDER BY u.full_name ASC";

                using var command = new NpgsqlCommand(query, connection);
                if (!string.IsNullOrWhiteSpace(role) && !string.Equals(role, "all", StringComparison.OrdinalIgnoreCase))
                {
                    command.Parameters.AddWithValue("@role", role.Trim());
                }

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var acc = new AccountDto
                    {
                        UserId = Convert.ToInt32(reader["user_id"]),
                        FullName = reader["full_name"]?.ToString() ?? string.Empty,
                        Email = reader["email"]?.ToString() ?? string.Empty,
                        Phone = reader["phone"]?.ToString() ?? string.Empty,
                        Role = reader["role"]?.ToString() ?? string.Empty,
                        IdPhotoUrl = FormatImageUrl(reader["id_photo_url"]),
                        AvatarBase64 = FormatImageUrl(reader["avatar_base64"]),
                        SignatureBase64 = FormatImageUrl(reader["signature_base64"]),
                        SignatureUrl = FormatImageUrl(reader["signature_url"]),
                        CreatedAt = Convert.ToDateTime(reader["created_at"])
                    };

                    if (reader["driver_id"] != DBNull.Value)
                    {
                        acc.DriverId = Convert.ToInt32(reader["driver_id"]);
                        acc.LicenseNo = reader["license_no"]?.ToString();
                        acc.LicensePhotoUrl = FormatImageUrl(reader["license_photo_url"]);
                        acc.DriverStatus = reader["driver_status"]?.ToString();
                        acc.RatingAvg = reader["rating_avg"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["rating_avg"]);
                        acc.TotalTrips = reader["total_trips"] == DBNull.Value ? 0 : Convert.ToInt32(reader["total_trips"]);
                    }

                    accounts.Add(acc);
                }

                return Ok(accounts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to load accounts: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FullName) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                string.IsNullOrWhiteSpace(request.Role))
            {
                return BadRequest(new { Message = "Required fields: FullName, Email, Password, and Role." });
            }

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Check duplicate email
                using var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM users WHERE email = @email", connection);
                checkCmd.Parameters.AddWithValue("@email", request.Email.Trim());
                if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0)
                {
                    return Conflict(new { Message = "Email is already registered." });
                }

                var hashedPassword = BCryptNet.HashPassword(request.Password);
                var normalizedRole = request.Role.Trim().ToLower();

                using var transaction = await connection.BeginTransactionAsync();

                int userId;
                try
                {
                    using var insertUserCmd = new NpgsqlCommand(@"
                        INSERT INTO users (full_name, email, password_hash, phone, role, id_photo_url, avatar_base64, signature_base64, created_at)
                        VALUES (@full_name, @email, @password_hash, @phone, @role, @id_photo_url, @avatar_base64, @signature_base64, NOW())
                        RETURNING user_id", connection, transaction);

                    insertUserCmd.Parameters.AddWithValue("@full_name", request.FullName.Trim());
                    insertUserCmd.Parameters.AddWithValue("@email", request.Email.Trim());
                    insertUserCmd.Parameters.AddWithValue("@password_hash", hashedPassword);
                    insertUserCmd.Parameters.AddWithValue("@phone", string.IsNullOrWhiteSpace(request.Phone) ? string.Empty : request.Phone.Trim());
                    insertUserCmd.Parameters.AddWithValue("@role", normalizedRole);
                    insertUserCmd.Parameters.AddWithValue("@id_photo_url", (object?)request.IdPhotoUrl ?? DBNull.Value);
                    insertUserCmd.Parameters.AddWithValue("@avatar_base64", (object?)request.AvatarBase64 ?? (object?)request.IdPhotoUrl ?? DBNull.Value);
                    insertUserCmd.Parameters.AddWithValue("@signature_base64", (object?)request.SignatureBase64 ?? DBNull.Value);

                    userId = Convert.ToInt32(await insertUserCmd.ExecuteScalarAsync());

                    if (normalizedRole == "driver")
                    {
                        using var insertDriverCmd = new NpgsqlCommand(@"
                            INSERT INTO drivers (user_id, license_no, license_photo_url, status, rating_avg, total_trips)
                            VALUES (@user_id, @license_no, @license_photo_url, @status, 0, 0)", connection, transaction);

                        insertDriverCmd.Parameters.AddWithValue("@user_id", userId);
                        insertDriverCmd.Parameters.AddWithValue("@license_no", request.LicenseNo?.Trim() ?? string.Empty);
                        insertDriverCmd.Parameters.AddWithValue("@license_photo_url", (object?)request.LicensePhotoUrl ?? DBNull.Value);
                        insertDriverCmd.Parameters.AddWithValue("@status", request.DriverStatus?.Trim() ?? "available");

                        await insertDriverCmd.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }

                // Notify SignalR clients
                await _hubContext.Clients.All.SendAsync("ReceiveAccountsUpdate");

                string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                var newValues = new { request.FullName, request.Email, request.Phone, request.Role };
                _ = Task.Run(async () => {
                    await _auditService.LogActionAsync(1, "Create", userId, ip, new { }, newValues);
                });

                return Ok(new { Message = "Account created successfully.", UserId = userId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to create account: " + ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateAccount(int id, [FromBody] UpdateAccountRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FullName) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Role))
            {
                return BadRequest(new { Message = "Required fields: FullName, Email, and Role." });
            }

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Check duplicate email
                using var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM users WHERE email = @email AND user_id <> @id", connection);
                checkCmd.Parameters.AddWithValue("@email", request.Email.Trim());
                checkCmd.Parameters.AddWithValue("@id", id);
                if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0)
                {
                    return Conflict(new { Message = "Email is already in use by another user." });
                }

                // Query Old Values for audit log
                var oldValues = new Dictionary<string, object>();
                using (var selectCmd = new NpgsqlCommand("SELECT full_name, email, phone, role FROM users WHERE user_id = @id", connection))
                {
                    selectCmd.Parameters.AddWithValue("@id", id);
                    using var reader = await selectCmd.ExecuteReaderAsync();
                    if (reader.Read())
                    {
                        oldValues["FullName"] = reader["full_name"]?.ToString() ?? "";
                        oldValues["Email"] = reader["email"]?.ToString() ?? "";
                        oldValues["Phone"] = reader["phone"]?.ToString() ?? "";
                        oldValues["Role"] = reader["role"]?.ToString() ?? "";
                    }
                }

                using var transaction = await connection.BeginTransactionAsync();
                try
                {
                    // Update user
                    string updateUserSql = @"
                        UPDATE users
                        SET full_name = @full_name,
                            email = @email,
                            phone = @phone,
                            role = @role,
                            id_photo_url = @id_photo_url,
                            avatar_base64 = COALESCE(@avatar_base64, avatar_base64),
                            signature_base64 = @signature_base64";

                    if (!string.IsNullOrWhiteSpace(request.Password))
                    {
                        updateUserSql += ", password_hash = @password_hash";
                    }

                    updateUserSql += " WHERE user_id = @id";

                    using var updateUserCmd = new NpgsqlCommand(updateUserSql, connection, transaction);
                    updateUserCmd.Parameters.AddWithValue("@full_name", request.FullName.Trim());
                    updateUserCmd.Parameters.AddWithValue("@email", request.Email.Trim());
                    updateUserCmd.Parameters.AddWithValue("@phone", string.IsNullOrWhiteSpace(request.Phone) ? string.Empty : request.Phone.Trim());
                    updateUserCmd.Parameters.AddWithValue("@role", request.Role.Trim().ToLower());
                    updateUserCmd.Parameters.AddWithValue("@id_photo_url", (object?)request.IdPhotoUrl ?? DBNull.Value);
                    updateUserCmd.Parameters.AddWithValue("@avatar_base64", (object?)request.AvatarBase64 ?? (object?)request.IdPhotoUrl ?? DBNull.Value);
                    updateUserCmd.Parameters.AddWithValue("@signature_base64", string.IsNullOrWhiteSpace(request.SignatureBase64) ? DBNull.Value : (object)request.SignatureBase64.Trim());
                    updateUserCmd.Parameters.AddWithValue("@id", id);

                    if (!string.IsNullOrWhiteSpace(request.Password))
                    {
                        var hashedPassword = BCryptNet.HashPassword(request.Password);
                        updateUserCmd.Parameters.AddWithValue("@password_hash", hashedPassword);
                    }

                    int affected = await updateUserCmd.ExecuteNonQueryAsync();
                    if (affected == 0)
                    {
                        await transaction.RollbackAsync();
                        return NotFound(new { Message = "Account not found." });
                    }

                    var normalizedRole = request.Role.Trim().ToLower();

                    // Check if driver entry exists
                    using var checkDriverCmd = new NpgsqlCommand("SELECT COUNT(*) FROM drivers WHERE user_id = @user_id", connection, transaction);
                    checkDriverCmd.Parameters.AddWithValue("@user_id", id);
                    bool isDriverInDb = Convert.ToInt32(await checkDriverCmd.ExecuteScalarAsync()) > 0;

                    if (normalizedRole == "driver")
                    {
                        if (isDriverInDb)
                        {
                            // Update existing driver
                            using var updateDriverCmd = new NpgsqlCommand(@"
                                UPDATE drivers
                                SET license_no = @license_no,
                                    license_photo_url = @license_photo_url,
                                    status = @status
                                WHERE user_id = @user_id", connection, transaction);

                            updateDriverCmd.Parameters.AddWithValue("@license_no", request.LicenseNo?.Trim() ?? string.Empty);
                            updateDriverCmd.Parameters.AddWithValue("@license_photo_url", (object?)request.LicensePhotoUrl ?? DBNull.Value);
                            updateDriverCmd.Parameters.AddWithValue("@status", request.DriverStatus?.Trim() ?? "available");
                            updateDriverCmd.Parameters.AddWithValue("@user_id", id);

                            await updateDriverCmd.ExecuteNonQueryAsync();
                        }
                        else
                        {
                            // Convert to driver
                            using var insertDriverCmd = new NpgsqlCommand(@"
                                INSERT INTO drivers (user_id, license_no, license_photo_url, status, rating_avg, total_trips)
                                VALUES (@user_id, @license_no, @license_photo_url, @status, 0, 0)", connection, transaction);

                            insertDriverCmd.Parameters.AddWithValue("@user_id", id);
                            insertDriverCmd.Parameters.AddWithValue("@license_no", request.LicenseNo?.Trim() ?? string.Empty);
                            insertDriverCmd.Parameters.AddWithValue("@license_photo_url", (object?)request.LicensePhotoUrl ?? DBNull.Value);
                            insertDriverCmd.Parameters.AddWithValue("@status", request.DriverStatus?.Trim() ?? "available");

                            await insertDriverCmd.ExecuteNonQueryAsync();
                        }
                    }
                    else
                    {
                        // If role was changed from driver to something else, remove driver entry
                        if (isDriverInDb)
                        {
                            using var deleteDriverCmd = new NpgsqlCommand("DELETE FROM drivers WHERE user_id = @user_id", connection, transaction);
                            deleteDriverCmd.Parameters.AddWithValue("@user_id", id);
                            await deleteDriverCmd.ExecuteNonQueryAsync();
                        }
                    }

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }

                // Notify SignalR clients
                await _hubContext.Clients.All.SendAsync("ReceiveAccountsUpdate");

                string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                var newValues = new Dictionary<string, object>
                {
                    { "FullName", request.FullName.Trim() },
                    { "Email", request.Email.Trim() },
                    { "Phone", request.Phone.Trim() },
                    { "Role", request.Role.Trim().ToLower() }
                };
                if (!string.IsNullOrEmpty(request.Password))
                {
                    newValues["Password"] = "[REDACTED_RESET]";
                }

                _ = Task.Run(async () => {
                    await _auditService.LogActionAsync(1, "Update", id, ip, oldValues, newValues);
                });

                return Ok(new { Message = "Account updated successfully.", UserId = id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to update account: " + ex.Message });
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteAccount(int id)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Query Old Values for audit log
                var oldValues = new Dictionary<string, object>();
                using (var selectCmd = new NpgsqlCommand("SELECT full_name, email, phone, role FROM users WHERE user_id = @id", connection))
                {
                    selectCmd.Parameters.AddWithValue("@id", id);
                    using var reader = await selectCmd.ExecuteReaderAsync();
                    if (reader.Read())
                    {
                        oldValues["FullName"] = reader["full_name"]?.ToString() ?? "";
                        oldValues["Email"] = reader["email"]?.ToString() ?? "";
                        oldValues["Phone"] = reader["phone"]?.ToString() ?? "";
                        oldValues["Role"] = reader["role"]?.ToString() ?? "";
                    }
                }

                using var command = new NpgsqlCommand("DELETE FROM users WHERE user_id = @id", connection);
                command.Parameters.AddWithValue("@id", id);

                int affected = await command.ExecuteNonQueryAsync();
                if (affected == 0)
                {
                    return NotFound(new { Message = "Account not found." });
                }

                // Notify SignalR clients
                await _hubContext.Clients.All.SendAsync("ReceiveAccountsUpdate");

                string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                _ = Task.Run(async () => {
                    await _auditService.LogActionAsync(1, "Delete", id, ip, oldValues, new { });
                });

                return Ok(new { Message = "Account deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to delete account: " + ex.Message });
            }
        }

        private static string? FormatImageUrl(object? rawObj)
        {
            if (rawObj == null || rawObj == DBNull.Value) return null;
            string raw = rawObj.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                raw.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) ||
                raw.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
            {
                return raw;
            }
            return "data:image/png;base64," + raw;
        }
    }
}
