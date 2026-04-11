using DriveAndGo_API.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DriveAndGo_API.Services
{
    public sealed class MySqlFirebaseBridgeService : BackgroundService
    {
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        private readonly string _mysqlConn;
        private readonly FirebaseBridgeOptions _options;
        private readonly ILogger<MySqlFirebaseBridgeService> _logger;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
        private readonly JsonSerializerOptions _readJsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        private readonly Dictionary<string, string> _lastNodePayloads = new(StringComparer.OrdinalIgnoreCase);

        private readonly record struct FirebaseLocationEntry(string Key, JsonElement Payload);

        public MySqlFirebaseBridgeService(IConfiguration config, ILogger<MySqlFirebaseBridgeService> logger)
        {
            _logger = logger;
            _mysqlConn = config.GetConnectionString("DefaultConnection")
                      ?? "Server=127.0.0.1;Port=3306;Database=vehicle_rental_db;Uid=root;Pwd=;";

            _options = config.GetSection("FirebaseBridge").Get<FirebaseBridgeOptions>()
                       ?? new FirebaseBridgeOptions();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (string.IsNullOrWhiteSpace(_options.DatabaseUrl))
            {
                _logger.LogWarning("MySqlFirebaseBridgeService disabled: FirebaseBridge:DatabaseUrl is missing.");
                return;
            }

            _logger.LogInformation("MySqlFirebaseBridgeService started. MySQL is the primary source of truth.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await BridgeOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MySqlFirebaseBridgeService cycle failed.");
                }

                await Task.Delay(TimeSpan.FromSeconds(Math.Max(2, _options.MirrorPollingSeconds)), stoppingToken);
            }
        }

        private async Task BridgeOnceAsync(CancellationToken ct)
        {
            await ImportFirebaseRentalsAsync(ct);
            await ImportFirebaseLocationLogsAsync(ct);
            await ImportFirebaseMessagesAsync(ct);
            await ImportFirebaseNotificationsAsync(ct);

            await MirrorUsersAsync(ct);
            await MirrorDriversAsync(ct);
            await MirrorVehiclesAsync(ct);
            await MirrorRentalsAsync(ct);
            await MirrorTransactionsAsync(ct);
            await MirrorRatingsAsync(ct);
            await MirrorMessagesAsync(ct);
            await MirrorNotificationsAsync(ct);
            await MirrorIssuesAsync(ct);
            await MirrorExtensionsAsync(ct);
            await MirrorVehicleLocationsAsync(ct);
        }

        private async Task ImportFirebaseRentalsAsync(CancellationToken ct)
        {
            using var doc = await GetFirebaseJsonAsync(_options.RentalsNode, ct);
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Object)
                return;

            int imported = 0;
            int skipped = 0;

            using var conn = await OpenAsync(ct);
            var existingIds = await LoadExistingIdsAsync(conn, "SELECT rental_id FROM rentals", "rental_id", ct);

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                var rental = TryDeserialize<Rental>(property.Value);
                if (rental == null)
                {
                    skipped++;
                    continue;
                }

                if (rental.RentalId <= 0 && int.TryParse(property.Name, out int parsedId))
                    rental.RentalId = parsedId;

                if (rental.RentalId <= 0 || rental.CustomerId <= 0 || rental.VehicleId <= 0)
                {
                    skipped++;
                    continue;
                }

                if (existingIds.Contains(rental.RentalId))
                    continue;

                await InsertImportedRentalAsync(conn, rental, ct);
                existingIds.Add(rental.RentalId);
                imported++;
            }

            if (imported > 0 || skipped > 0)
            {
                _logger.LogInformation(
                    "Firebase rentals import complete. Imported: {Imported}, Skipped: {Skipped}",
                    imported, skipped);
            }
        }

        private async Task ImportFirebaseLocationLogsAsync(CancellationToken ct)
        {
            using var doc = await GetFirebaseJsonAsync(_options.LocationLogsNode, ct);
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Object)
                return;

            using var conn = await OpenAsync(ct);
            var rentalVehicleMap = await LoadRentalVehicleMapAsync(conn, ct);

            int imported = 0;
            int skipped = 0;

            foreach (var rentalNode in doc.RootElement.EnumerateObject())
            {
                if (!int.TryParse(rentalNode.Name, out int rentalId))
                    continue;

                foreach (var entry in EnumerateLocationEntries(rentalNode))
                {
                    if (!TryReadDecimal(entry.Payload, out decimal latitude, "Latitude", "latitude", "lat") ||
                        !TryReadDecimal(entry.Payload, out decimal longitude, "Longitude", "longitude", "lng"))
                    {
                        skipped++;
                        continue;
                    }

                    int vehicleId = rentalVehicleMap.TryGetValue(rentalId, out int mappedVehicleId)
                        ? mappedVehicleId
                        : TryReadInt(entry.Payload, "VehicleId", "vehicleId");

                    if (vehicleId <= 0)
                    {
                        skipped++;
                        continue;
                    }

                    decimal? speed = TryReadNullableDecimal(entry.Payload, "SpeedKmh", "speedKmh", "speed");
                    DateTime loggedAt = TryReadDateTime(entry.Payload, "LoggedAt", "loggedAt")
                        ?? DateTime.UtcNow;

                    int logId = CreateStablePositiveInt($"{rentalId}:{entry.Key}");

                    await InsertImportedLocationLogAsync(conn, logId, rentalId, vehicleId, latitude, longitude, speed, loggedAt, ct);
                    await UpdateVehiclePositionAsync(conn, vehicleId, latitude, longitude, speed, loggedAt, ct);
                    imported++;
                }
            }

            if (imported > 0 || skipped > 0)
            {
                _logger.LogInformation(
                    "Firebase location import complete. Imported: {Imported}, Skipped: {Skipped}",
                    imported, skipped);
            }
        }

        private async Task ImportFirebaseMessagesAsync(CancellationToken ct)
        {
            using var doc = await GetFirebaseJsonAsync(_options.MessagesNode, ct);
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Object)
                return;

            using var conn = await OpenAsync(ct);
            var participants = await LoadRentalParticipantsAsync(conn, ct);

            int imported = 0;
            int skipped = 0;

            foreach (var rentalNode in doc.RootElement.EnumerateObject())
            {
                if (!int.TryParse(rentalNode.Name, out int rentalId))
                    continue;

                foreach (var entry in EnumerateMessageEntries(rentalNode))
                {
                    int senderId = TryReadInt(entry.Payload, "SenderId", "senderId");
                    string? content = TryReadString(entry.Payload, "MessageText", "messageText", "Content", "content");
                    string? mediaUrl = TryReadString(entry.Payload, "AttachmentUrl", "attachmentUrl", "MediaUrl", "mediaUrl");
                    int messageId = TryReadInt(entry.Payload, "MessageId", "messageId");
                    bool isRead = TryReadBool(entry.Payload, "IsRead", "isRead");
                    DateTime sentAt = TryReadDateTime(entry.Payload, "SentAt", "sentAt")
                        ?? DateTime.UtcNow;

                    if (senderId <= 0 || string.IsNullOrWhiteSpace(content))
                    {
                        skipped++;
                        continue;
                    }

                    if (messageId <= 0)
                        messageId = CreateStablePositiveInt($"{rentalId}:{entry.Key}");

                    int? receiverId = null;
                    if (participants.TryGetValue(rentalId, out var participant))
                    {
                        if (participant.CustomerId > 0 && senderId == participant.CustomerId && participant.DriverId.HasValue)
                            receiverId = participant.DriverId.Value;
                        else if (participant.DriverId.HasValue && senderId == participant.DriverId.Value && participant.CustomerId > 0)
                            receiverId = participant.CustomerId;
                    }

                    await UpsertImportedMessageAsync(
                        conn,
                        messageId,
                        rentalId,
                        senderId,
                        receiverId,
                        content,
                        mediaUrl,
                        isRead,
                        sentAt,
                        ct);
                    imported++;
                }
            }

            if (imported > 0 || skipped > 0)
            {
                _logger.LogInformation(
                    "Firebase messages import complete. Imported: {Imported}, Skipped: {Skipped}",
                    imported, skipped);
            }
        }

        private async Task ImportFirebaseNotificationsAsync(CancellationToken ct)
        {
            using var doc = await GetFirebaseJsonAsync(_options.NotificationsNode, ct);
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Object)
                return;

            using var conn = await OpenAsync(ct);

            int imported = 0;
            int skipped = 0;

            foreach (var userNode in doc.RootElement.EnumerateObject())
            {
                if (!int.TryParse(userNode.Name, out int userId))
                    continue;

                foreach (var entry in EnumerateNotificationEntries(userNode))
                {
                    string? title = TryReadString(entry.Payload, "Title", "title");
                    string? body = TryReadString(entry.Payload, "Body", "body");
                    string? type = TryReadString(entry.Payload, "Type", "type");
                    bool isRead = TryReadBool(entry.Payload, "IsRead", "isRead");
                    DateTime sentAt = TryReadDateTime(entry.Payload, "SentAt", "sentAt")
                        ?? DateTime.UtcNow;
                    int notifId = TryReadInt(entry.Payload, "NotifId", "notifId", "NotificationId", "notificationId");

                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
                    {
                        skipped++;
                        continue;
                    }

                    if (notifId <= 0)
                        notifId = CreateStablePositiveInt($"{userId}:{entry.Key}");

                    await UpsertImportedNotificationAsync(
                        conn,
                        notifId,
                        userId,
                        title,
                        body,
                        type,
                        isRead,
                        sentAt,
                        ct);
                    imported++;
                }
            }

            if (imported > 0 || skipped > 0)
            {
                _logger.LogInformation(
                    "Firebase notifications import complete. Imported: {Imported}, Skipped: {Skipped}",
                    imported, skipped);
            }
        }

        private async Task MirrorUsersAsync(CancellationToken ct)
        {
            using var conn = await OpenAsync(ct);
            var users = await LoadUsersAsync(conn, ct);
            await PutNodeSnapshotAsync(_options.UsersNode, ToNodeMap(users, x => x.UserId), ct);
        }

        private async Task MirrorDriversAsync(CancellationToken ct)
        {
            using var conn = await OpenAsync(ct);
            var drivers = await LoadDriversAsync(conn, ct);
            await PutNodeSnapshotAsync(_options.DriversNode, ToNodeMap(drivers, x => x.DriverId), ct);
        }

        private async Task MirrorVehiclesAsync(CancellationToken ct)
        {
            using var conn = await OpenAsync(ct);
            var vehicles = await LoadVehiclesAsync(conn, ct);
            await PutNodeSnapshotAsync(_options.VehiclesNode, ToNodeMap(vehicles, x => x.VehicleId), ct);
        }

        private async Task MirrorRentalsAsync(CancellationToken ct)
        {
            using var conn = await OpenAsync(ct);
            var rentals = await LoadRentalsAsync(conn, ct);
            await PutNodeSnapshotAsync(_options.RentalsNode, ToNodeMap(rentals, x => x.RentalId), ct);
        }

        private async Task MirrorTransactionsAsync(CancellationToken ct)
        {
            using var conn = await OpenAsync(ct);
            var transactions = await LoadTransactionsAsync(conn, ct);
            await PutNodeSnapshotAsync(_options.TransactionsNode, ToNodeMap(transactions, x => x.TransactionId), ct);
        }

        private async Task MirrorRatingsAsync(CancellationToken ct)
        {
            using var conn = await OpenAsync(ct);
            var ratings = await LoadRatingsAsync(conn, ct);
            await PutNodeSnapshotAsync(_options.RatingsNode, ToNodeMap(ratings, x => x.RatingId), ct);
        }

        private async Task MirrorMessagesAsync(CancellationToken ct)
        {
            using var conn = await OpenAsync(ct);
            var messages = await LoadMessagesAsync(conn, ct);

            var payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var message in messages)
            {
                if (message.RentalId <= 0 || message.MessageId <= 0)
                    continue;

                string rentalKey = message.RentalId.ToString(CultureInfo.InvariantCulture);
                if (!payload.TryGetValue(rentalKey, out object? bucketObj) || bucketObj is not Dictionary<string, object> bucket)
                {
                    bucket = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    payload[rentalKey] = bucket;
                }

                bucket[message.MessageId.ToString(CultureInfo.InvariantCulture)] = new
                {
                    MessageId = message.MessageId,
                    RentalId = message.RentalId,
                    SenderId = message.SenderId,
                    ReceiverId = message.ReceiverId,
                    MessageText = message.MessageText,
                    Content = message.MessageText,
                    AttachmentUrl = message.MediaUrl,
                    MediaUrl = message.MediaUrl,
                    IsRead = message.IsRead,
                    SentAt = message.SentAt
                };
            }

            await PutNodeSnapshotAsync(_options.MessagesNode, payload, ct);
        }

        private async Task MirrorNotificationsAsync(CancellationToken ct)
        {
            using var conn = await OpenAsync(ct);
            var notifications = await LoadNotificationsAsync(conn, ct);

            var payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var notification in notifications)
            {
                if (notification.UserId <= 0 || notification.NotifId <= 0)
                    continue;

                string userKey = notification.UserId.ToString(CultureInfo.InvariantCulture);
                if (!payload.TryGetValue(userKey, out object? bucketObj) || bucketObj is not Dictionary<string, object> bucket)
                {
                    bucket = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    payload[userKey] = bucket;
                }

                bucket[notification.NotifId.ToString(CultureInfo.InvariantCulture)] = notification;
            }

            await PutNodeSnapshotAsync(_options.NotificationsNode, payload, ct);
        }

        private async Task MirrorIssuesAsync(CancellationToken ct)
        {
            using var conn = await OpenAsync(ct);
            var issues = await LoadIssuesAsync(conn, ct);
            await PutNodeSnapshotAsync(_options.IssuesNode, ToNodeMap(issues, x => x.IssueId), ct);
        }

        private async Task MirrorExtensionsAsync(CancellationToken ct)
        {
            using var conn = await OpenAsync(ct);
            var extensions = await LoadExtensionsAsync(conn, ct);
            await PutNodeSnapshotAsync(_options.ExtensionsNode, ToNodeMap(extensions, x => x.ExtensionId), ct);
        }

        private async Task MirrorVehicleLocationsAsync(CancellationToken ct)
        {
            using var conn = await OpenAsync(ct);
            var locations = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            const string sql = @"
                SELECT
                    v.vehicle_id,
                    v.latitude,
                    v.longitude,
                    v.current_speed,
                    v.last_update,
                    (
                        SELECT r.rental_id
                        FROM rentals r
                        WHERE r.vehicle_id = v.vehicle_id
                          AND LOWER(r.status) IN ('pending','approved','in-use','rented')
                        ORDER BY r.created_at DESC, r.rental_id DESC
                        LIMIT 1
                    ) AS rental_id
                FROM vehicles v
                WHERE v.latitude IS NOT NULL
                  AND v.longitude IS NOT NULL";

            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                int vehicleId = Convert.ToInt32(reader["vehicle_id"]);
                decimal latitude = Convert.ToDecimal(reader["latitude"], CultureInfo.InvariantCulture);
                decimal longitude = Convert.ToDecimal(reader["longitude"], CultureInfo.InvariantCulture);
                decimal speed = reader["current_speed"] == DBNull.Value
                    ? 0
                    : Convert.ToDecimal(reader["current_speed"], CultureInfo.InvariantCulture);
                DateTime? loggedAt = reader["last_update"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(reader["last_update"], CultureInfo.InvariantCulture);
                int? rentalId = reader["rental_id"] == DBNull.Value
                    ? null
                    : Convert.ToInt32(reader["rental_id"]);

                locations[vehicleId.ToString(CultureInfo.InvariantCulture)] = new
                {
                    VehicleId = vehicleId,
                    RentalId = rentalId,
                    Latitude = latitude,
                    Longitude = longitude,
                    SpeedKmh = speed,
                    LoggedAt = loggedAt,
                    lat = latitude,
                    lng = longitude,
                    speed = speed,
                    loggedAt = loggedAt
                };
            }

            await PutNodeSnapshotAsync(_options.VehicleLocationsNode, locations, ct);
        }

        private async Task<HashSet<int>> LoadExistingIdsAsync(
            MySqlConnection conn,
            string sql,
            string columnName,
            CancellationToken ct)
        {
            var ids = new HashSet<int>();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
                ids.Add(Convert.ToInt32(reader[columnName]));

            return ids;
        }

        private async Task<Dictionary<int, int>> LoadRentalVehicleMapAsync(MySqlConnection conn, CancellationToken ct)
        {
            var map = new Dictionary<int, int>();
            using var cmd = new MySqlCommand("SELECT rental_id, vehicle_id FROM rentals", conn);
            using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
                map[Convert.ToInt32(reader["rental_id"])] = Convert.ToInt32(reader["vehicle_id"]);

            return map;
        }

        private async Task<Dictionary<int, (int CustomerId, int? DriverId)>> LoadRentalParticipantsAsync(MySqlConnection conn, CancellationToken ct)
        {
            var map = new Dictionary<int, (int CustomerId, int? DriverId)>();
            using var cmd = new MySqlCommand("SELECT rental_id, customer_id, driver_id FROM rentals", conn);
            using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                int rentalId = Convert.ToInt32(reader["rental_id"]);
                int customerId = reader["customer_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["customer_id"]);
                int? driverId = reader["driver_id"] == DBNull.Value ? null : Convert.ToInt32(reader["driver_id"]);
                map[rentalId] = (customerId, driverId);
            }

            return map;
        }

        private async Task InsertImportedRentalAsync(MySqlConnection conn, Rental rental, CancellationToken ct)
        {
            const string sql = @"
                INSERT INTO rentals
                    (rental_id, customer_id, vehicle_id, driver_id,
                     start_date, end_date, destination, status,
                     total_amount, payment_method, payment_status,
                     qr_code, created_at)
                VALUES
                    (@id, @customer, @vehicle, @driver,
                     @start, @end, @destination, @status,
                     @amount, @paymentMethod, @paymentStatus,
                     @qr, @created)";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", rental.RentalId);
            cmd.Parameters.AddWithValue("@customer", rental.CustomerId);
            cmd.Parameters.AddWithValue("@vehicle", rental.VehicleId);
            cmd.Parameters.AddWithValue("@driver", rental.DriverId.HasValue ? rental.DriverId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@start", rental.StartDate == DateTime.MinValue ? DateTime.UtcNow : rental.StartDate);
            cmd.Parameters.AddWithValue("@end", rental.EndDate.HasValue ? rental.EndDate.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@destination", string.IsNullOrWhiteSpace(rental.Destination) ? DBNull.Value : rental.Destination);
            cmd.Parameters.AddWithValue("@status", NormalizeValue(rental.Status, "pending"));
            cmd.Parameters.AddWithValue("@amount", rental.TotalAmount);
            cmd.Parameters.AddWithValue("@paymentMethod", NormalizeValue(rental.PaymentMethod, "cash"));
            cmd.Parameters.AddWithValue("@paymentStatus", NormalizeValue(rental.PaymentStatus, "unpaid"));
            cmd.Parameters.AddWithValue("@qr", string.IsNullOrWhiteSpace(rental.QrCode) ? DBNull.Value : rental.QrCode);
            cmd.Parameters.AddWithValue("@created", rental.CreatedAt == DateTime.MinValue ? DateTime.UtcNow : rental.CreatedAt);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private async Task InsertImportedLocationLogAsync(
            MySqlConnection conn,
            int logId,
            int rentalId,
            int vehicleId,
            decimal latitude,
            decimal longitude,
            decimal? speed,
            DateTime loggedAt,
            CancellationToken ct)
        {
            const string sql = @"
                INSERT IGNORE INTO location_logs
                    (log_id, rental_id, vehicle_id, latitude, longitude, speed_kmh, logged_at)
                VALUES
                    (@id, @rental, @vehicle, @lat, @lng, @speed, @logged)";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", logId);
            cmd.Parameters.AddWithValue("@rental", rentalId);
            cmd.Parameters.AddWithValue("@vehicle", vehicleId);
            cmd.Parameters.AddWithValue("@lat", latitude);
            cmd.Parameters.AddWithValue("@lng", longitude);
            cmd.Parameters.AddWithValue("@speed", speed.HasValue ? speed.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@logged", loggedAt);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private async Task UpsertImportedMessageAsync(
            MySqlConnection conn,
            int messageId,
            int rentalId,
            int senderId,
            int? receiverId,
            string content,
            string? mediaUrl,
            bool isRead,
            DateTime sentAt,
            CancellationToken ct)
        {
            const string sql = @"
                INSERT INTO messages
                    (message_id, rental_id, sender_id, receiver_id, content, attachment_url, is_read, sent_at)
                VALUES
                    (@id, @rental, @sender, @receiver, @content, @attachment, @read, @sent)
                ON DUPLICATE KEY UPDATE
                    receiver_id = @receiver,
                    content = @content,
                    attachment_url = @attachment,
                    is_read = @read,
                    sent_at = @sent";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", messageId);
            cmd.Parameters.AddWithValue("@rental", rentalId);
            cmd.Parameters.AddWithValue("@sender", senderId);
            cmd.Parameters.AddWithValue("@receiver", receiverId.HasValue ? receiverId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@content", content);
            cmd.Parameters.AddWithValue("@attachment", string.IsNullOrWhiteSpace(mediaUrl) ? DBNull.Value : mediaUrl);
            cmd.Parameters.AddWithValue("@read", isRead);
            cmd.Parameters.AddWithValue("@sent", sentAt);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private async Task UpsertImportedNotificationAsync(
            MySqlConnection conn,
            int notifId,
            int userId,
            string title,
            string body,
            string? type,
            bool isRead,
            DateTime sentAt,
            CancellationToken ct)
        {
            const string sql = @"
                INSERT INTO notifications
                    (notif_id, user_id, title, body, type, is_read, sent_at)
                VALUES
                    (@id, @user, @title, @body, @type, @read, @sent)
                ON DUPLICATE KEY UPDATE
                    title = @title,
                    body = @body,
                    type = @type,
                    is_read = @read,
                    sent_at = @sent";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", notifId);
            cmd.Parameters.AddWithValue("@user", userId);
            cmd.Parameters.AddWithValue("@title", title);
            cmd.Parameters.AddWithValue("@body", body);
            cmd.Parameters.AddWithValue("@type", string.IsNullOrWhiteSpace(type) ? DBNull.Value : type);
            cmd.Parameters.AddWithValue("@read", isRead);
            cmd.Parameters.AddWithValue("@sent", sentAt);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private async Task UpdateVehiclePositionAsync(
            MySqlConnection conn,
            int vehicleId,
            decimal latitude,
            decimal longitude,
            decimal? speed,
            DateTime loggedAt,
            CancellationToken ct)
        {
            const string sql = @"
                UPDATE vehicles
                SET latitude = @lat,
                    longitude = @lng,
                    current_speed = @speed,
                    last_update = @logged
                WHERE vehicle_id = @vehicle";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@lat", latitude);
            cmd.Parameters.AddWithValue("@lng", longitude);
            cmd.Parameters.AddWithValue("@speed", speed.HasValue ? Convert.ToInt32(Math.Round(speed.Value)) : DBNull.Value);
            cmd.Parameters.AddWithValue("@logged", loggedAt);
            cmd.Parameters.AddWithValue("@vehicle", vehicleId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private async Task<List<User>> LoadUsersAsync(MySqlConnection conn, CancellationToken ct)
        {
            const string sql = @"
                SELECT user_id, full_name, email, password_hash, phone, role,
                       id_photo_url, firebase_uid, created_at
                FROM users
                ORDER BY user_id";

            var items = new List<User>();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                items.Add(new User
                {
                    UserId = Convert.ToInt32(reader["user_id"]),
                    FullName = reader["full_name"]?.ToString() ?? "",
                    Email = reader["email"]?.ToString() ?? "",
                    PasswordHash = reader["password_hash"]?.ToString() ?? "",
                    Phone = reader["phone"]?.ToString() ?? "",
                    Role = reader["role"]?.ToString() ?? "customer",
                    IdPhotoUrl = reader["id_photo_url"] == DBNull.Value ? null : reader["id_photo_url"]?.ToString(),
                    FirebaseUid = reader["firebase_uid"] == DBNull.Value ? null : reader["firebase_uid"]?.ToString(),
                    CreatedAt = reader["created_at"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["created_at"])
                });
            }

            return items;
        }

        private async Task<List<Driver>> LoadDriversAsync(MySqlConnection conn, CancellationToken ct)
        {
            const string sql = @"
                SELECT driver_id, user_id, license_no, license_photo_url,
                       status, rating_avg, total_trips
                FROM drivers
                ORDER BY driver_id";

            var items = new List<Driver>();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                items.Add(new Driver
                {
                    DriverId = Convert.ToInt32(reader["driver_id"]),
                    UserId = reader["user_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["user_id"]),
                    LicenseNo = reader["license_no"]?.ToString() ?? "",
                    LicensePhotoUrl = reader["license_photo_url"] == DBNull.Value ? null : reader["license_photo_url"]?.ToString(),
                    Status = reader["status"]?.ToString() ?? "inactive",
                    RatingAvg = reader["rating_avg"] == DBNull.Value ? null : Convert.ToDecimal(reader["rating_avg"]),
                    TotalTrips = reader["total_trips"] == DBNull.Value ? 0 : Convert.ToInt32(reader["total_trips"])
                });
            }

            return items;
        }

        private async Task<List<Vehicle>> LoadVehiclesAsync(MySqlConnection conn, CancellationToken ct)
        {
            const string sql = @"
                SELECT vehicle_id, plate_no, brand, model, type, cc,
                       status, rate_per_day, rate_with_driver, photo_url,
                       description, seat_capacity, transmission, created_at,
                       latitude, longitude, current_speed, last_update,
                       model_3d_url, in_garage
                FROM vehicles
                ORDER BY vehicle_id";

            var items = new List<Vehicle>();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                items.Add(new Vehicle
                {
                    VehicleId = Convert.ToInt32(reader["vehicle_id"]),
                    PlateNo = reader["plate_no"]?.ToString() ?? "",
                    Brand = reader["brand"]?.ToString() ?? "",
                    Model = reader["model"]?.ToString() ?? "",
                    Type = reader["type"]?.ToString() ?? "Car",
                    CC = reader["cc"] == DBNull.Value ? null : Convert.ToInt32(reader["cc"]),
                    Status = NormalizeValue(reader["status"]?.ToString(), "available"),
                    RatePerDay = reader["rate_per_day"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["rate_per_day"]),
                    RateWithDriver = reader["rate_with_driver"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["rate_with_driver"]),
                    PhotoUrl = reader["photo_url"] == DBNull.Value ? "" : reader["photo_url"]?.ToString() ?? "",
                    Description = reader["description"] == DBNull.Value ? "" : reader["description"]?.ToString() ?? "",
                    SeatCapacity = reader["seat_capacity"] == DBNull.Value ? 5 : Convert.ToInt32(reader["seat_capacity"]),
                    Transmission = reader["transmission"] == DBNull.Value ? "Automatic" : reader["transmission"]?.ToString() ?? "Automatic",
                    CreatedAt = reader["created_at"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["created_at"]),
                    Latitude = reader["latitude"] == DBNull.Value ? null : Convert.ToDouble(reader["latitude"]),
                    Longitude = reader["longitude"] == DBNull.Value ? null : Convert.ToDouble(reader["longitude"]),
                    CurrentSpeed = reader["current_speed"] == DBNull.Value ? null : Convert.ToInt32(reader["current_speed"]),
                    LastUpdate = reader["last_update"] == DBNull.Value ? null : Convert.ToDateTime(reader["last_update"]),
                    Model3dUrl = reader["model_3d_url"] == DBNull.Value ? "" : reader["model_3d_url"]?.ToString() ?? "",
                    InGarage = reader["in_garage"] != DBNull.Value && Convert.ToBoolean(reader["in_garage"])
                });
            }

            return items;
        }

        private async Task<List<Rental>> LoadRentalsAsync(MySqlConnection conn, CancellationToken ct)
        {
            const string sql = @"
                SELECT rental_id, customer_id, vehicle_id, driver_id,
                       start_date, end_date, destination, status,
                       total_amount, payment_method, payment_status,
                       qr_code, created_at
                FROM rentals
                ORDER BY rental_id";

            var items = new List<Rental>();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                items.Add(new Rental
                {
                    RentalId = Convert.ToInt32(reader["rental_id"]),
                    CustomerId = reader["customer_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["customer_id"]),
                    VehicleId = reader["vehicle_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["vehicle_id"]),
                    DriverId = reader["driver_id"] == DBNull.Value ? null : Convert.ToInt32(reader["driver_id"]),
                    StartDate = reader["start_date"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["start_date"]),
                    EndDate = reader["end_date"] == DBNull.Value ? null : Convert.ToDateTime(reader["end_date"]),
                    Destination = reader["destination"] == DBNull.Value ? null : reader["destination"]?.ToString(),
                    Status = NormalizeValue(reader["status"]?.ToString(), "pending"),
                    TotalAmount = reader["total_amount"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["total_amount"]),
                    PaymentMethod = NormalizeValue(reader["payment_method"]?.ToString(), "cash"),
                    PaymentStatus = NormalizeValue(reader["payment_status"]?.ToString(), "unpaid"),
                    QrCode = reader["qr_code"] == DBNull.Value ? null : reader["qr_code"]?.ToString(),
                    CreatedAt = reader["created_at"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["created_at"])
                });
            }

            return items;
        }

        private async Task<List<Transaction>> LoadTransactionsAsync(MySqlConnection conn, CancellationToken ct)
        {
            const string sql = @"
                SELECT transaction_id, rental_id, amount, type,
                       method, proof_url, status, paid_at
                FROM transactions
                ORDER BY transaction_id";

            var items = new List<Transaction>();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                items.Add(new Transaction
                {
                    TransactionId = Convert.ToInt32(reader["transaction_id"]),
                    RentalId = reader["rental_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["rental_id"]),
                    Amount = reader["amount"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["amount"]),
                    Type = reader["type"] == DBNull.Value ? null : NormalizeValue(reader["type"]?.ToString(), "payment"),
                    Method = reader["method"] == DBNull.Value ? null : NormalizeValue(reader["method"]?.ToString(), "cash"),
                    ProofUrl = reader["proof_url"] == DBNull.Value ? null : reader["proof_url"]?.ToString(),
                    Status = reader["status"] == DBNull.Value ? null : NormalizeValue(reader["status"]?.ToString(), "pending"),
                    PaidAt = reader["paid_at"] == DBNull.Value ? null : Convert.ToDateTime(reader["paid_at"])
                });
            }

            return items;
        }

        private async Task<List<Rating>> LoadRatingsAsync(MySqlConnection conn, CancellationToken ct)
        {
            const string sql = @"
                SELECT rating_id, rental_id, customer_id, driver_id,
                       vehicle_id, driver_score, vehicle_score,
                       comment, rated_at
                FROM ratings
                ORDER BY rating_id";

            var items = new List<Rating>();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                items.Add(new Rating
                {
                    RatingId = Convert.ToInt32(reader["rating_id"]),
                    RentalId = reader["rental_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["rental_id"]),
                    CustomerId = reader["customer_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["customer_id"]),
                    DriverId = reader["driver_id"] == DBNull.Value ? null : Convert.ToInt32(reader["driver_id"]),
                    VehicleId = reader["vehicle_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["vehicle_id"]),
                    DriverScore = reader["driver_score"] == DBNull.Value ? null : Convert.ToInt32(reader["driver_score"]),
                    VehicleScore = reader["vehicle_score"] == DBNull.Value ? 0 : Convert.ToInt32(reader["vehicle_score"]),
                    Comment = reader["comment"] == DBNull.Value ? null : reader["comment"]?.ToString(),
                    RatedAt = reader["rated_at"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["rated_at"])
                });
            }

            return items;
        }

        private async Task<List<Message>> LoadMessagesAsync(MySqlConnection conn, CancellationToken ct)
        {
            const string sql = @"
                SELECT message_id, rental_id, sender_id, receiver_id,
                       content, attachment_url, is_read, sent_at
                FROM messages
                ORDER BY message_id";

            var items = new List<Message>();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                items.Add(new Message
                {
                    MessageId = Convert.ToInt32(reader["message_id"]),
                    RentalId = reader["rental_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["rental_id"]),
                    SenderId = reader["sender_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["sender_id"]),
                    ReceiverId = reader["receiver_id"] == DBNull.Value ? null : Convert.ToInt32(reader["receiver_id"]),
                    MessageText = reader["content"] == DBNull.Value ? null : reader["content"]?.ToString(),
                    MediaUrl = reader["attachment_url"] == DBNull.Value ? null : reader["attachment_url"]?.ToString(),
                    IsRead = reader["is_read"] != DBNull.Value && Convert.ToBoolean(reader["is_read"]),
                    SentAt = reader["sent_at"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["sent_at"])
                });
            }

            return items;
        }

        private async Task<List<AppNotification>> LoadNotificationsAsync(MySqlConnection conn, CancellationToken ct)
        {
            const string sql = @"
                SELECT notif_id, user_id, title, body, type, is_read, sent_at
                FROM notifications
                ORDER BY notif_id";

            var items = new List<AppNotification>();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                items.Add(new AppNotification
                {
                    NotifId = Convert.ToInt32(reader["notif_id"]),
                    UserId = reader["user_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["user_id"]),
                    Title = reader["title"] == DBNull.Value ? "" : reader["title"]?.ToString() ?? "",
                    Body = reader["body"] == DBNull.Value ? "" : reader["body"]?.ToString() ?? "",
                    Type = reader["type"] == DBNull.Value ? null : reader["type"]?.ToString(),
                    IsRead = reader["is_read"] != DBNull.Value && Convert.ToBoolean(reader["is_read"]),
                    SentAt = reader["sent_at"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["sent_at"])
                });
            }

            return items;
        }

        private async Task<List<Issue>> LoadIssuesAsync(MySqlConnection conn, CancellationToken ct)
        {
            const string sql = @"
                SELECT issue_id, rental_id, reporter_id, issue_type,
                       description, image_url, status, reported_at
                FROM issues
                ORDER BY issue_id";

            var items = new List<Issue>();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                items.Add(new Issue
                {
                    IssueId = Convert.ToInt32(reader["issue_id"]),
                    RentalId = reader["rental_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["rental_id"]),
                    ReporterId = reader["reporter_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["reporter_id"]),
                    IssueType = reader["issue_type"] == DBNull.Value ? null : reader["issue_type"]?.ToString(),
                    Description = reader["description"] == DBNull.Value ? null : reader["description"]?.ToString(),
                    ImageUrl = reader["image_url"] == DBNull.Value ? null : reader["image_url"]?.ToString(),
                    Status = reader["status"] == DBNull.Value ? "pending" : reader["status"]?.ToString(),
                    ReportedAt = reader["reported_at"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["reported_at"])
                });
            }

            return items;
        }

        private async Task<List<Extension>> LoadExtensionsAsync(MySqlConnection conn, CancellationToken ct)
        {
            const string sql = @"
                SELECT extension_id, rental_id, added_days, added_fee, status, requested_at
                FROM extensions
                ORDER BY extension_id";

            var items = new List<Extension>();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                items.Add(new Extension
                {
                    ExtensionId = Convert.ToInt32(reader["extension_id"]),
                    RentalId = reader["rental_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["rental_id"]),
                    AddedDays = reader["added_days"] == DBNull.Value ? 0 : Convert.ToInt32(reader["added_days"]),
                    AddedFee = reader["added_fee"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["added_fee"]),
                    Status = reader["status"] == DBNull.Value ? "pending" : reader["status"]?.ToString(),
                    RequestedAt = reader["requested_at"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["requested_at"])
                });
            }

            return items;
        }

        private async Task PutNodeSnapshotAsync(string node, object payload, CancellationToken ct)
        {
            string json = JsonSerializer.Serialize(payload, _jsonOptions);
            if (_lastNodePayloads.TryGetValue(node, out string? lastJson) &&
                string.Equals(lastJson, json, StringComparison.Ordinal))
            {
                return;
            }

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await Http.PutAsync(BuildFirebaseUrl(node), content, ct);
            response.EnsureSuccessStatusCode();

            _lastNodePayloads[node] = json;
        }

        private async Task<JsonDocument?> GetFirebaseJsonAsync(string node, CancellationToken ct)
        {
            using var response = await Http.GetAsync(BuildFirebaseUrl(node), ct);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(json) ||
                string.Equals(json.Trim(), "null", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return JsonDocument.Parse(json);
        }

        private string BuildFirebaseUrl(string node)
        {
            string baseUrl = (_options.DatabaseUrl ?? string.Empty).TrimEnd('/');
            string path = string.IsNullOrWhiteSpace(node) ? string.Empty : "/" + node.Trim('/');
            string url = $"{baseUrl}{path}.json";

            if (!string.IsNullOrWhiteSpace(_options.Secret))
                url += "?auth=" + Uri.EscapeDataString(_options.Secret);

            return url;
        }

        private async Task<MySqlConnection> OpenAsync(CancellationToken ct)
        {
            var conn = new MySqlConnection(_mysqlConn);
            await conn.OpenAsync(ct);
            return conn;
        }

        private T? TryDeserialize<T>(JsonElement element) where T : class
        {
            if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return null;

            try
            {
                return JsonSerializer.Deserialize<T>(element.GetRawText(), _readJsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private Dictionary<string, T> ToNodeMap<T>(IEnumerable<T> items, Func<T, int> keySelector) where T : class
        {
            var map = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

            foreach (T item in items)
            {
                int key = keySelector(item);
                if (key <= 0)
                    continue;

                map[key.ToString(CultureInfo.InvariantCulture)] = item;
            }

            return map;
        }

        private static string NormalizeValue(string? value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            return value.Trim().ToLowerInvariant();
        }

        private IEnumerable<FirebaseLocationEntry> EnumerateLocationEntries(JsonProperty rentalNode)
        {
            if (rentalNode.Value.ValueKind != JsonValueKind.Object)
                yield break;

            if (LooksLikeLocationPayload(rentalNode.Value))
            {
                yield return new FirebaseLocationEntry(rentalNode.Name, rentalNode.Value.Clone());
                yield break;
            }

            foreach (var child in rentalNode.Value.EnumerateObject())
            {
                if (child.Value.ValueKind != JsonValueKind.Object)
                    continue;

                if (!LooksLikeLocationPayload(child.Value))
                    continue;

                yield return new FirebaseLocationEntry(child.Name, child.Value.Clone());
            }
        }

        private IEnumerable<FirebaseLocationEntry> EnumerateMessageEntries(JsonProperty rentalNode)
        {
            if (rentalNode.Value.ValueKind != JsonValueKind.Object)
                yield break;

            if (LooksLikeMessagePayload(rentalNode.Value))
            {
                yield return new FirebaseLocationEntry(rentalNode.Name, rentalNode.Value.Clone());
                yield break;
            }

            foreach (var child in rentalNode.Value.EnumerateObject())
            {
                if (child.Value.ValueKind != JsonValueKind.Object)
                    continue;

                if (!LooksLikeMessagePayload(child.Value))
                    continue;

                yield return new FirebaseLocationEntry(child.Name, child.Value.Clone());
            }
        }

        private IEnumerable<FirebaseLocationEntry> EnumerateNotificationEntries(JsonProperty userNode)
        {
            if (userNode.Value.ValueKind != JsonValueKind.Object)
                yield break;

            if (LooksLikeNotificationPayload(userNode.Value))
            {
                yield return new FirebaseLocationEntry(userNode.Name, userNode.Value.Clone());
                yield break;
            }

            foreach (var child in userNode.Value.EnumerateObject())
            {
                if (child.Value.ValueKind != JsonValueKind.Object)
                    continue;

                if (!LooksLikeNotificationPayload(child.Value))
                    continue;

                yield return new FirebaseLocationEntry(child.Name, child.Value.Clone());
            }
        }

        private static bool LooksLikeLocationPayload(JsonElement element) =>
            HasProperty(element, "Latitude") ||
            HasProperty(element, "latitude") ||
            HasProperty(element, "lat");

        private static bool LooksLikeMessagePayload(JsonElement element) =>
            HasProperty(element, "MessageText") ||
            HasProperty(element, "messageText") ||
            HasProperty(element, "Content") ||
            HasProperty(element, "content");

        private static bool LooksLikeNotificationPayload(JsonElement element) =>
            HasProperty(element, "Title") ||
            HasProperty(element, "title");

        private static bool HasProperty(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object)
                return false;

            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool TryReadDecimal(JsonElement element, out decimal value, params string[] propertyNames)
        {
            value = default;

            foreach (string propertyName in propertyNames)
            {
                if (!TryGetPropertyCaseInsensitive(element, propertyName, out JsonElement property))
                    continue;

                if (property.ValueKind == JsonValueKind.Number)
                {
                    if (property.TryGetDecimal(out value))
                        return true;

                    if (property.TryGetDouble(out double doubleValue))
                    {
                        value = Convert.ToDecimal(doubleValue, CultureInfo.InvariantCulture);
                        return true;
                    }
                }

                if (property.ValueKind == JsonValueKind.String &&
                    decimal.TryParse(
                        property.GetString(),
                        NumberStyles.Float | NumberStyles.AllowThousands,
                        CultureInfo.InvariantCulture,
                        out value))
                {
                    return true;
                }
            }

            return false;
        }

        private static decimal? TryReadNullableDecimal(JsonElement element, params string[] propertyNames) =>
            TryReadDecimal(element, out decimal value, propertyNames) ? value : null;

        private static int TryReadInt(JsonElement element, params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                if (!TryGetPropertyCaseInsensitive(element, propertyName, out JsonElement property))
                    continue;

                if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int numericValue))
                    return numericValue;

                if (property.ValueKind == JsonValueKind.String &&
                    int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int stringValue))
                {
                    return stringValue;
                }
            }

            return 0;
        }

        private static string? TryReadString(JsonElement element, params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                if (!TryGetPropertyCaseInsensitive(element, propertyName, out JsonElement property))
                    continue;

                if (property.ValueKind == JsonValueKind.String)
                    return property.GetString();

                if (property.ValueKind == JsonValueKind.Number || property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
                    return property.ToString();
            }

            return null;
        }

        private static bool TryReadBool(JsonElement element, params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                if (!TryGetPropertyCaseInsensitive(element, propertyName, out JsonElement property))
                    continue;

                if (property.ValueKind == JsonValueKind.True)
                    return true;

                if (property.ValueKind == JsonValueKind.False)
                    return false;

                if (property.ValueKind == JsonValueKind.String &&
                    bool.TryParse(property.GetString(), out bool boolValue))
                {
                    return boolValue;
                }

                if (property.ValueKind == JsonValueKind.Number &&
                    property.TryGetInt32(out int numericValue))
                {
                    return numericValue != 0;
                }
            }

            return false;
        }

        private static DateTime? TryReadDateTime(JsonElement element, params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                if (!TryGetPropertyCaseInsensitive(element, propertyName, out JsonElement property))
                    continue;

                if (property.ValueKind == JsonValueKind.String)
                {
                    string? raw = property.GetString();
                    if (string.IsNullOrWhiteSpace(raw))
                        continue;

                    if (DateTime.TryParse(
                        raw,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out DateTime utcValue))
                    {
                        return utcValue;
                    }

                    if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime localValue))
                        return localValue;
                }

                if (property.ValueKind == JsonValueKind.Number)
                {
                    if (property.TryGetInt64(out long unixValue))
                    {
                        try
                        {
                            return unixValue > 10_000_000_000
                                ? DateTimeOffset.FromUnixTimeMilliseconds(unixValue).UtcDateTime
                                : DateTimeOffset.FromUnixTimeSeconds(unixValue).UtcDateTime;
                        }
                        catch
                        {
                            return null;
                        }
                    }
                }
            }

            return null;
        }

        private static int CreateStablePositiveInt(string input)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            int value = BitConverter.ToInt32(hash, 0) & int.MaxValue;
            return value == 0 ? 1 : value;
        }

        private static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value;
                        return true;
                    }
                }
            }

            value = default;
            return false;
        }
    }
}
