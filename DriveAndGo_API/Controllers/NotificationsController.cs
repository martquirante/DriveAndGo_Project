using DriveAndGo_API.Contracts;
using DriveAndGo_API.Models;
using DriveAndGo_API.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

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

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(
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
            using var connection = new NpgsqlConnection(_connectionString);
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

    [HttpGet("admin")]
    public IActionResult GetAdminNotifications()
    {
        try
        {
            var list = new List<object>();

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            // 1. Read persistent notifications from database
            using (var notifCmd = new NpgsqlCommand(@"
                SELECT notif_id, title, body, type, is_read, sent_at
                FROM notifications
                ORDER BY sent_at DESC, notif_id DESC
                LIMIT 40", connection))
            {
                using var reader = notifCmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new
                    {
                        id = "db-" + reader["notif_id"],
                        title = reader["title"]?.ToString() ?? "System Notification",
                        body = reader["body"]?.ToString() ?? "",
                        type = reader["type"]?.ToString() ?? "general",
                        unread = reader["is_read"] == DBNull.Value || !Convert.ToBoolean(reader["is_read"]),
                        time = reader["sent_at"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["sent_at"])
                    });
                }
            }

            // 2. Add Live Alerts for Overdue Rentals
            using (var overdueCmd = new NpgsqlCommand(@"
                SELECT r.rental_id, COALESCE(r.rental_code, CONCAT('RN-', LPAD(r.rental_id::text, 6, '0'))) AS code,
                       COALESCE(u.full_name, 'Customer') AS cust_name,
                       CONCAT(v.brand, ' ', v.model) AS veh_name,
                       v.plate_no,
                       r.end_date
                FROM rentals r
                LEFT JOIN users u ON r.customer_id = u.user_id
                LEFT JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                WHERE LOWER(COALESCE(r.status, '')) IN ('active', 'in-use', 'ongoing', 'overdue')
                  AND r.end_date < NOW()
                ORDER BY r.end_date ASC
                LIMIT 10", connection))
            {
                using var reader = overdueCmd.ExecuteReader();
                while (reader.Read())
                {
                    var code = reader["code"]?.ToString() ?? "";
                    var cust = reader["cust_name"]?.ToString() ?? "";
                    var veh = reader["veh_name"]?.ToString() ?? "";
                    var plate = reader["plate_no"]?.ToString() ?? "";
                    var end = Convert.ToDateTime(reader["end_date"]);
                    var hoursLate = Math.Max(1, (int)(DateTime.UtcNow - end).TotalHours);

                    list.Add(new
                    {
                        id = "overdue-" + reader["rental_id"],
                        title = $"⚠️ Vehicle Overdue: {plate}",
                        body = $"{veh} ({cust}) is {hoursLate} hour(s) past scheduled drop-off.",
                        type = "overdue",
                        unread = true,
                        time = end
                    });
                }
            }

            // 3. Add Live Alerts for Pending Bookings (highlighting imminent / past schedule)
            using (var pendingCmd = new NpgsqlCommand(@"
                SELECT r.rental_id, COALESCE(r.rental_code, CONCAT('RN-', LPAD(r.rental_id::text, 6, '0'))) AS code,
                       COALESCE(u.full_name, 'Customer') AS cust_name,
                       CONCAT(v.brand, ' ', v.model) AS veh_name,
                       r.start_date,
                       r.created_at
                FROM rentals r
                LEFT JOIN users u ON r.customer_id = u.user_id
                LEFT JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                WHERE LOWER(COALESCE(r.status, '')) = 'pending'
                ORDER BY r.start_date ASC, r.created_at DESC
                LIMIT 15", connection))
            {
                using var reader = pendingCmd.ExecuteReader();
                while (reader.Read())
                {
                    var code = reader["code"]?.ToString() ?? "";
                    var cust = reader["cust_name"]?.ToString() ?? "";
                    var veh = reader["veh_name"]?.ToString() ?? "";
                    var start = reader["start_date"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["start_date"]);
                    var created = reader["created_at"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["created_at"]);

                    bool isImminentOrPast = start <= DateTime.UtcNow.AddHours(24);
                    string title = isImminentOrPast 
                        ? $"⏳ Urgent Conforme Required: {code}" 
                        : $"New Booking Request: {code}";
                    
                    string body = isImminentOrPast
                        ? $"Trip scheduled for {start:MMM dd, h:mm tt} ({cust} - {veh}). Conforme review required."
                        : $"{cust} requested booking for {veh}. Awaiting Conforme approval.";

                    list.Add(new
                    {
                        id = "pending-" + reader["rental_id"],
                        title = title,
                        body = body,
                        type = "booking",
                        unread = true,
                        time = isImminentOrPast ? DateTime.UtcNow : created
                    });
                }
            }


            // Order combined list by time descending and take top 50
            var ordered = list.OrderByDescending(x => {
                var prop = x.GetType().GetProperty("time");
                return prop != null ? (DateTime)prop.GetValue(x)! : DateTime.MinValue;
            }).Take(50).ToList();

            return Ok(ordered);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpGet]

    public IActionResult GetNotifications([FromQuery] int? userId)
    {
        try
        {
            var notifications = new List<AppNotification>();

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            string sql = @"SELECT notif_id, user_id, title, body, type, is_read, sent_at
                           FROM notifications";
            
            if (userId.HasValue && userId.Value > 0)
            {
                sql += " WHERE user_id = @user_id";
            }
            sql += " ORDER BY sent_at DESC, notif_id DESC";

            using var command = new NpgsqlCommand(sql, connection);
            if (userId.HasValue && userId.Value > 0)
            {
                command.Parameters.AddWithValue("@user_id", userId.Value);
            }

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

    [HttpPatch("read-all")]
    public IActionResult MarkAllAsRead([FromQuery] int? userId)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            string sql = "UPDATE notifications SET is_read = true";
            if (userId.HasValue && userId.Value > 0)
            {
                sql += " WHERE user_id = @user_id";
            }

            using var command = new NpgsqlCommand(sql, connection);
            if (userId.HasValue && userId.Value > 0)
            {
                command.Parameters.AddWithValue("@user_id", userId.Value);
            }

            int affected = command.ExecuteNonQuery();
            return Ok(new { Message = "All notifications marked as read.", AffectedRows = affected });
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
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(
                "UPDATE notifications SET is_read = true WHERE notif_id = @id",
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
