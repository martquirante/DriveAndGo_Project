using DriveAndGo_API.Contracts;
using DriveAndGo_API.Models;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace DriveAndGo_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly string _connectionString;

    public UsersController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    [HttpGet]
    public IActionResult GetUsers()
    {
        try
        {
            var users = new List<User>();

            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var command = new MySqlCommand(
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
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var command = new MySqlCommand(
                @"SELECT
                    u.user_id,
                    u.full_name,
                    u.email,
                    u.phone,
                    u.role,
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
                Role = reader["role"]?.ToString() ?? string.Empty
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
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var duplicateCommand = new MySqlCommand(
                @"SELECT COUNT(*) FROM users
                  WHERE email = @email AND user_id <> @id",
                connection);
            duplicateCommand.Parameters.AddWithValue("@email", request.Email.Trim());
            duplicateCommand.Parameters.AddWithValue("@id", id);

            if (Convert.ToInt32(duplicateCommand.ExecuteScalar()) > 0)
            {
                return Conflict(new { Message = "Email is already in use by another account." });
            }

            using var updateCommand = new MySqlCommand(
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
}
