using DriveAndGo_API.Contracts;
using DriveAndGo_API.Models;
using DriveAndGo_API.Services;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace DriveAndGo_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class NotificationsController : ControllerBase
{
    private readonly string _connectionString;
    private readonly NotificationWriter _notificationWriter;

    public NotificationsController(IConfiguration configuration, NotificationWriter notificationWriter)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        _notificationWriter = notificationWriter;
    }

    [HttpGet("user/{userId:int}")]
    public IActionResult GetByUser(int userId)
    {
        try
        {
            var notifications = new List<AppNotification>();

            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var command = new MySqlCommand(
                @"SELECT notif_id, user_id, title, body, type, is_read, sent_at
                  FROM notifications
                  WHERE user_id = @user_id
                  ORDER BY sent_at DESC, notif_id DESC",
                connection);
            command.Parameters.AddWithValue("@user_id", userId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                notifications.Add(new AppNotification
                {
                    NotifId = Convert.ToInt32(reader["notif_id"]),
                    UserId = Convert.ToInt32(reader["user_id"]),
                    Title = reader["title"]?.ToString() ?? string.Empty,
                    Body = reader["body"]?.ToString() ?? string.Empty,
                    Type = reader["type"] == DBNull.Value ? null : reader["type"].ToString(),
                    IsRead = reader["is_read"] != DBNull.Value && Convert.ToBoolean(reader["is_read"]),
                    SentAt = reader["sent_at"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["sent_at"])
                });
            }

            return Ok(notifications);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPost]
    public IActionResult Create([FromBody] CreateNotificationRequest request)
    {
        if (request.UserId <= 0 ||
            string.IsNullOrWhiteSpace(request.Title) ||
            string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest(new { Message = "UserId, title, and body are required." });
        }

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            _notificationWriter.Create(
                connection,
                request.UserId,
                request.Title.Trim(),
                request.Body.Trim(),
                string.IsNullOrWhiteSpace(request.Type) ? "general" : request.Type.Trim().ToLowerInvariant());

            return Ok(new { Message = "Notification created successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPatch("{id:int}/read")]
    public IActionResult MarkAsRead(int id)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var command = new MySqlCommand(
                "UPDATE notifications SET is_read = 1 WHERE notif_id = @id",
                connection);
            command.Parameters.AddWithValue("@id", id);

            if (command.ExecuteNonQuery() == 0)
            {
                return NotFound(new { Message = "Notification not found." });
            }

            return Ok(new { Message = "Notification marked as read.", NotificationId = id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }
}
