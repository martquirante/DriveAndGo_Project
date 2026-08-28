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

    private static string? FormatImageUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();
        if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
        {
            return raw;
        }
        return "data:image/png;base64," + raw;
    }

    [HttpGet("admin")]
    public IActionResult GetAdminNotifications()
    {
        try
        {
            var list = new List<object>();
            var seenBookingRentalIds = new HashSet<int>();

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            // 1. Read persistent notifications from database joined with customer user details
            using (var notifCmd = new NpgsqlCommand(@"
                SELECT n.notif_id, n.user_id, n.title, n.body, n.type, n.is_read, n.sent_at,
                       u.full_name AS cust_name,
                       u.email AS cust_email,
                       u.phone AS cust_phone,
                       COALESCE(NULLIF(u.avatar_base64, ''), NULLIF(u.id_photo_url, '')) AS cust_avatar
                FROM notifications n
                LEFT JOIN users u ON n.user_id = u.user_id
                ORDER BY n.sent_at DESC, n.notif_id DESC
                LIMIT 40", connection))
            {
                using var reader = notifCmd.ExecuteReader();
                while (reader.Read())
                {
                    string notifTitle = reader["title"]?.ToString() ?? "System Notification";
                    string notifBody = reader["body"]?.ToString() ?? "";
                    string notifType = reader["type"]?.ToString() ?? "general";
                    string? custName = reader["cust_name"] == DBNull.Value ? null : reader["cust_name"]?.ToString();
                    string? custEmail = reader["cust_email"] == DBNull.Value ? null : reader["cust_email"]?.ToString();
                    string? custPhone = reader["cust_phone"] == DBNull.Value ? null : reader["cust_phone"]?.ToString();
                    string? custAvatar = reader["cust_avatar"] == DBNull.Value ? null : FormatImageUrl(reader["cust_avatar"]?.ToString());

                    list.Add(new
                    {
                        id = "db-" + reader["notif_id"],
                        title = notifTitle,
                        body = notifBody,
                        type = notifType,
                        unread = reader["is_read"] == DBNull.Value || !Convert.ToBoolean(reader["is_read"]),
                        time = reader["sent_at"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["sent_at"]),
                        customerName = custName,
                        customerEmail = custEmail,
                        customerPhone = custPhone,
                        customerAvatar = custAvatar,
                        eventType = notifType.Contains("booking") || notifType.Contains("rental") ? "booking" : notifType
                    });
                }
            }

            // 2. Add Live Alerts for Overdue Rentals (Highest Priority)
            using (var overdueCmd = new NpgsqlCommand(@"
                SELECT r.rental_id, COALESCE(r.rental_code, CONCAT('BK-', LPAD(r.rental_id::text, 6, '0'))) AS code,
                       r.customer_id,
                       COALESCE(u.full_name, 'Customer') AS cust_name,
                       u.email AS cust_email,
                       u.phone AS cust_phone,
                       COALESCE(NULLIF(u.avatar_base64, ''), NULLIF(u.id_photo_url, '')) AS cust_avatar,
                       CONCAT(v.brand, ' ', v.model) AS veh_name,
                       v.plate_no,
                       r.start_date,
                       r.end_date,
                       r.destination,
                       r.total_amount,
                       r.status
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
                    int rId = Convert.ToInt32(reader["rental_id"]);
                    seenBookingRentalIds.Add(rId);
                    var code = reader["code"]?.ToString() ?? "";
                    var cust = reader["cust_name"]?.ToString() ?? "Customer";
                    var veh = reader["veh_name"]?.ToString() ?? "Vehicle";
                    var plate = reader["plate_no"]?.ToString() ?? "";
                    var email = reader["cust_email"] == DBNull.Value ? "" : reader["cust_email"]?.ToString();
                    var phone = reader["cust_phone"] == DBNull.Value ? "" : reader["cust_phone"]?.ToString();
                    var avatar = reader["cust_avatar"] == DBNull.Value ? null : FormatImageUrl(reader["cust_avatar"]?.ToString());
                    var start = reader["start_date"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["start_date"]);
                    var end = Convert.ToDateTime(reader["end_date"]);
                    var dest = reader["destination"] == DBNull.Value ? "Rental Route" : reader["destination"]?.ToString();
                    var amount = reader["total_amount"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["total_amount"]);
                    var hoursLate = Math.Max(1, (int)(DateTime.UtcNow - end).TotalHours);

                    list.Add(new
                    {
                        id = "overdue-" + rId,
                        rentalId = rId,
                        rentalCode = code,
                        title = $"Vehicle Overdue: {plate}",
                        body = $"{veh} ({cust}) is {hoursLate}h past scheduled return. Contact: {phone}.",
                        type = "overdue",
                        unread = true,
                        time = end,
                        customerName = cust,
                        customerEmail = email,
                        customerPhone = phone,
                        customerAvatar = avatar,
                        vehicleName = veh,
                        vehiclePlate = plate,
                        bookingStatus = "overdue",
                        destination = dest,
                        startDate = start?.ToString("MMM dd, yyyy h:mm tt"),
                        endDate = end.ToString("MMM dd, yyyy h:mm tt"),
                        totalAmount = amount,
                        eventType = "overdue_alert"
                    });
                }
            }

            // 3. Add Live Alerts for Pending Bookings (New Booking Requests / Review)
            using (var pendingCmd = new NpgsqlCommand(@"
                SELECT r.rental_id, COALESCE(r.rental_code, CONCAT('BK-', LPAD(r.rental_id::text, 6, '0'))) AS code,
                       r.customer_id,
                       COALESCE(u.full_name, 'Customer') AS cust_name,
                       u.email AS cust_email,
                       u.phone AS cust_phone,
                       COALESCE(NULLIF(u.avatar_base64, ''), NULLIF(u.id_photo_url, '')) AS cust_avatar,
                       CONCAT(v.brand, ' ', v.model) AS veh_name,
                       v.plate_no,
                       r.start_date,
                       r.end_date,
                       r.destination,
                       r.total_amount,
                       r.status,
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
                    int rId = Convert.ToInt32(reader["rental_id"]);
                    seenBookingRentalIds.Add(rId);
                    var code = reader["code"]?.ToString() ?? "";
                    var cust = reader["cust_name"]?.ToString() ?? "Customer";
                    var veh = reader["veh_name"]?.ToString() ?? "Vehicle";
                    var plate = reader["plate_no"]?.ToString() ?? "";
                    var email = reader["cust_email"] == DBNull.Value ? "" : reader["cust_email"]?.ToString();
                    var phone = reader["cust_phone"] == DBNull.Value ? "" : reader["cust_phone"]?.ToString();
                    var avatar = reader["cust_avatar"] == DBNull.Value ? null : FormatImageUrl(reader["cust_avatar"]?.ToString());
                    var start = reader["start_date"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["start_date"]);
                    var end = reader["end_date"] == DBNull.Value ? DateTime.UtcNow.AddDays(1) : Convert.ToDateTime(reader["end_date"]);
                    var dest = reader["destination"] == DBNull.Value ? "Rental Route" : reader["destination"]?.ToString();
                    var amount = reader["total_amount"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["total_amount"]);
                    var created = reader["created_at"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["created_at"]);

                    bool isImminent = start <= DateTime.UtcNow.AddHours(24);
                    string title = isImminent 
                        ? $"Urgent Approval Required: {code}" 
                        : $"New Booking Request: {code}";
                    
                    string body = isImminent
                        ? $"{cust} scheduled {veh} ({plate}) for {start:MMM dd, h:mm tt}. Immediate booking approval required."
                        : $"{cust} submitted a booking request for {veh} ({plate}) to {dest}. Awaiting review.";

                    list.Add(new
                    {
                        id = "pending-" + rId,
                        rentalId = rId,
                        rentalCode = code,
                        title = title,
                        body = body,
                        type = "booking",
                        unread = true,
                        time = isImminent ? DateTime.UtcNow : created,
                        customerName = cust,
                        customerEmail = email,
                        customerPhone = phone,
                        customerAvatar = avatar,
                        vehicleName = veh,
                        vehiclePlate = plate,
                        bookingStatus = "pending",
                        destination = dest,
                        startDate = start.ToString("MMM dd, yyyy h:mm tt"),
                        endDate = end.ToString("MMM dd, yyyy h:mm tt"),
                        totalAmount = amount,
                        eventType = "booking_request"
                    });
                }
            }

            // 4. Add Live Alerts for Vehicle Returns & Finalized Inspections
            using (var returnedCmd = new NpgsqlCommand(@"
                SELECT r.rental_id, COALESCE(r.rental_code, CONCAT('BK-', LPAD(r.rental_id::text, 6, '0'))) AS code,
                       r.customer_id,
                       COALESCE(u.full_name, 'Customer') AS cust_name,
                       u.email AS cust_email,
                       u.phone AS cust_phone,
                       COALESCE(NULLIF(u.avatar_base64, ''), NULLIF(u.id_photo_url, '')) AS cust_avatar,
                       CONCAT(v.brand, ' ', v.model) AS veh_name,
                       v.plate_no,
                       r.start_date,
                       r.end_date,
                       r.destination,
                       r.total_amount,
                       r.status,
                       r.created_at
                FROM rentals r
                LEFT JOIN users u ON r.customer_id = u.user_id
                LEFT JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                WHERE LOWER(COALESCE(r.status, '')) IN ('completed', 'returned')
                ORDER BY r.rental_id DESC
                LIMIT 10", connection))
            {
                using var reader = returnedCmd.ExecuteReader();
                while (reader.Read())
                {
                    int rId = Convert.ToInt32(reader["rental_id"]);
                    if (seenBookingRentalIds.Contains(rId)) continue;
                    seenBookingRentalIds.Add(rId);

                    var code = reader["code"]?.ToString() ?? "";
                    var cust = reader["cust_name"]?.ToString() ?? "Customer";
                    var veh = reader["veh_name"]?.ToString() ?? "Vehicle";
                    var plate = reader["plate_no"]?.ToString() ?? "";
                    var email = reader["cust_email"] == DBNull.Value ? "" : reader["cust_email"]?.ToString();
                    var phone = reader["cust_phone"] == DBNull.Value ? "" : reader["cust_phone"]?.ToString();
                    var avatar = reader["cust_avatar"] == DBNull.Value ? null : FormatImageUrl(reader["cust_avatar"]?.ToString());
                    var start = reader["start_date"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["start_date"]);
                    var end = reader["end_date"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["end_date"]);
                    var dest = reader["destination"] == DBNull.Value ? "Completed Route" : reader["destination"]?.ToString();
                    var amount = reader["total_amount"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["total_amount"]);

                    list.Add(new
                    {
                        id = "returned-" + rId,
                        rentalId = rId,
                        rentalCode = code,
                        title = $"Vehicle Returned & Completed: {code}",
                        body = $"{cust} returned {veh} ({plate}). Inspection finalized and security deposit refunded.",
                        type = "booking",
                        unread = false,
                        time = end,
                        customerName = cust,
                        customerEmail = email,
                        customerPhone = phone,
                        customerAvatar = avatar,
                        vehicleName = veh,
                        vehiclePlate = plate,
                        bookingStatus = "completed",
                        destination = dest,
                        startDate = start.ToString("MMM dd, yyyy h:mm tt"),
                        endDate = end.ToString("MMM dd, yyyy h:mm tt"),
                        totalAmount = amount,
                        eventType = "vehicle_returned"
                    });
                }
            }

            // 5. Add Live Alerts for Approved & Ongoing Dispatched Rentals
            using (var approvedCmd = new NpgsqlCommand(@"
                SELECT r.rental_id, COALESCE(r.rental_code, CONCAT('BK-', LPAD(r.rental_id::text, 6, '0'))) AS code,
                       r.customer_id,
                       COALESCE(u.full_name, 'Customer') AS cust_name,
                       u.email AS cust_email,
                       u.phone AS cust_phone,
                       COALESCE(NULLIF(u.avatar_base64, ''), NULLIF(u.id_photo_url, '')) AS cust_avatar,
                       CONCAT(v.brand, ' ', v.model) AS veh_name,
                       v.plate_no,
                       r.start_date,
                       r.end_date,
                       r.destination,
                       r.total_amount,
                       r.status,
                       r.created_at
                FROM rentals r
                LEFT JOIN users u ON r.customer_id = u.user_id
                LEFT JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                WHERE LOWER(COALESCE(r.status, '')) IN ('approved', 'active', 'in-use', 'ongoing')
                ORDER BY r.rental_id DESC
                LIMIT 8", connection))
            {
                using var reader = approvedCmd.ExecuteReader();
                while (reader.Read())
                {
                    int rId = Convert.ToInt32(reader["rental_id"]);
                    if (seenBookingRentalIds.Contains(rId)) continue;
                    seenBookingRentalIds.Add(rId);

                    var code = reader["code"]?.ToString() ?? "";
                    var cust = reader["cust_name"]?.ToString() ?? "Customer";
                    var veh = reader["veh_name"]?.ToString() ?? "Vehicle";
                    var plate = reader["plate_no"]?.ToString() ?? "";
                    var email = reader["cust_email"] == DBNull.Value ? "" : reader["cust_email"]?.ToString();
                    var phone = reader["cust_phone"] == DBNull.Value ? "" : reader["cust_phone"]?.ToString();
                    var avatar = reader["cust_avatar"] == DBNull.Value ? null : FormatImageUrl(reader["cust_avatar"]?.ToString());
                    var start = reader["start_date"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["start_date"]);
                    var end = reader["end_date"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["end_date"]);
                    var dest = reader["destination"] == DBNull.Value ? "Rental Route" : reader["destination"]?.ToString();
                    var amount = reader["total_amount"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["total_amount"]);
                    var st = reader["status"]?.ToString() ?? "approved";
                    var isOngoing = st.Equals("active", StringComparison.OrdinalIgnoreCase) || st.Equals("in-use", StringComparison.OrdinalIgnoreCase) || st.Equals("ongoing", StringComparison.OrdinalIgnoreCase);

                    list.Add(new
                    {
                        id = "approved-" + rId,
                        rentalId = rId,
                        rentalCode = code,
                        title = isOngoing ? $"Rental Dispatched & In-Use: {code}" : $"Booking Approved: {code}",
                        body = isOngoing 
                            ? $"{cust} is actively driving {veh} ({plate}) to {dest}. Scheduled drop-off: {end:MMM dd, h:mm tt}."
                            : $"{veh} ({plate}) reserved for {cust}. Rental agreement signed and approved.",
                        type = "booking",
                        unread = false,
                        time = start,
                        customerName = cust,
                        customerEmail = email,
                        customerPhone = phone,
                        customerAvatar = avatar,
                        vehicleName = veh,
                        vehiclePlate = plate,
                        bookingStatus = st,
                        destination = dest,
                        startDate = start.ToString("MMM dd, yyyy h:mm tt"),
                        endDate = end.ToString("MMM dd, yyyy h:mm tt"),
                        totalAmount = amount,
                        eventType = isOngoing ? "rental_ongoing" : "booking_approved"
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
