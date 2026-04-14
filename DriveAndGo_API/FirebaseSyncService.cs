using DriveAndGo_API.Models;
using Firebase.Database;
using Firebase.Database.Streaming;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DriveAndGo_API.Services
{
    public class FirebaseSyncService : BackgroundService
    {
        private const int MaxRetries = 3;

        private readonly string _mysqlConn;
        private readonly FirebaseClient _fb;
        private readonly ILogger<FirebaseSyncService> _logger;

        // 🔴 [NEW] ASYNC LOCKS PARA PUMILA ANG MGA SABAY-SABAY NA REQUESTS 🔴
        private static readonly SemaphoreSlim _rentalLock = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim _txLock = new SemaphoreSlim(1, 1);

        private static string Now => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        public FirebaseSyncService(IConfiguration config, ILogger<FirebaseSyncService> logger)
        {
            _logger = logger;
            _mysqlConn = config.GetConnectionString("DefaultConnection")
                      ?? "Server=127.0.0.1;Port=3306;Database=vehicle_rental_db;Uid=root;Pwd=;";

            var bridgeOptions = config.GetSection("FirebaseBridge").Get<DriveAndGo_API.Services.FirebaseBridgeOptions>()
                               ?? new DriveAndGo_API.Services.FirebaseBridgeOptions();
            string firebaseUrl = string.IsNullOrWhiteSpace(bridgeOptions.DatabaseUrl)
                ? "https://vechiclerentaldb-default-rtdb.asia-southeast1.firebasedatabase.app/"
                : bridgeOptions.DatabaseUrl;

            if (string.IsNullOrWhiteSpace(bridgeOptions.Secret))
            {
                _fb = new FirebaseClient(firebaseUrl);
            }
            else
            {
                _fb = new FirebaseClient(
                firebaseUrl,
                new FirebaseOptions
                {
                    AuthTokenAsyncFactory = () => Task.FromResult(bridgeOptions.Secret)
                });
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Log("STARTING", "FirebaseSyncService is now running.");

            ListenTo<User>("users", OnUser);
            ListenTo<Driver>("drivers", OnDriver);
            ListenTo<Vehicle>("vehicles", OnVehicle);

            await Task.Delay(3000, stoppingToken);
            ListenTo<Rental>("rentals", OnRental);

            await Task.Delay(2000, stoppingToken);
            ListenTo<Transaction>("transactions", OnTransaction);
            ListenTo<Message>("messages", OnMessage);
            ListenTo<AppNotification>("notifications", OnNotification);
            ListenTo<Rating>("ratings", OnRating);
            ListenTo<GpsLog>("gps_logs", OnGpsLog);
            ListenTo<LocationLog>("location_logs", OnLocationLog);
            ListenTo<Incident>("incidents", OnIncident);
            ListenTo<Issue>("issues", OnIssue);
            ListenTo<Extension>("extensions", OnExtension);

            Log("READY", "All listeners are active. Waiting for Firebase changes...");
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private void ListenTo<T>(string node, Func<T, Task> handler) where T : class
        {
            _fb.Child(node)
               .AsObservable<T>()
               .Subscribe(
                   async d =>
                   {
                       if (d.EventType != FirebaseEventType.InsertOrUpdate) return;
                       if (d.Object == null) return;

                       await handler(d.Object);
                   },
                   ex => _logger.LogError("[{Time}] [LISTENER] [{Node}] STATUS: STREAM ERROR | {Msg}", Now, node, ex.Message)
               );
        }

        private async Task<MySqlConnection> OpenAsync()
        {
            var conn = new MySqlConnection(_mysqlConn);
            await conn.OpenAsync();
            return conn;
        }

        private static object DbVal(object? val) => val ?? DBNull.Value;
        private static object DbStr(string? val) => string.IsNullOrWhiteSpace(val) ? DBNull.Value : val;
        private static object DbDate(DateTime dt) => dt == DateTime.MinValue ? DBNull.Value : (object)dt;
        private static object DbDate(DateTime? dt) => !dt.HasValue || dt.Value == DateTime.MinValue ? DBNull.Value : (object)dt.Value;
        private static object DbFk(int? id) => (!id.HasValue || id.Value <= 0) ? DBNull.Value : (object)id.Value;

        private void Log(string tag, string message) => _logger.LogInformation("[{Time}] [{Tag}] {Message}", Now, tag, message);
        private void LogSuccess(string table, string detail) => _logger.LogInformation("[{Time}] [SYNC] [{Table}] STATUS: SUCCESS | {Detail}", Now, table, detail);
        private void LogFailed(string table, string detail, string error) => _logger.LogError("[{Time}] [SYNC] [{Table}] STATUS: FAILED | {Detail} | Error: {Error}", Now, table, detail, error);


        // ─────────────────────────────────────────────────────────────────
        //  SYNC HANDLERS (Users, Drivers, Vehicles - No Changes)
        // ─────────────────────────────────────────────────────────────────
        private async Task OnUser(User u)
        {
            if (u.UserId <= 0) return;
            try
            {
                using var conn = await OpenAsync();
                var cmd = new MySqlCommand(@"
                    INSERT INTO users (user_id, full_name, email, password_hash, phone, role, id_photo_url, firebase_uid, created_at)
                    VALUES (@id, @name, @email, @hash, @phone, @role, @photo, @uid, @created)
                    ON DUPLICATE KEY UPDATE full_name = @name, phone = @phone, role = @role, id_photo_url = @photo, firebase_uid = @uid", conn);
                cmd.Parameters.AddWithValue("@id", u.UserId); cmd.Parameters.AddWithValue("@name", DbStr(u.FullName)); cmd.Parameters.AddWithValue("@email", DbStr(u.Email)); cmd.Parameters.AddWithValue("@hash", DbStr(u.PasswordHash)); cmd.Parameters.AddWithValue("@phone", DbStr(u.Phone)); cmd.Parameters.AddWithValue("@role", DbStr(u.Role)); cmd.Parameters.AddWithValue("@photo", DbStr(u.IdPhotoUrl)); cmd.Parameters.AddWithValue("@uid", DbStr(u.FirebaseUid)); cmd.Parameters.AddWithValue("@created", DbDate(u.CreatedAt));
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { LogFailed("users", $"UserId: {u.UserId}", ex.Message); }
        }

        private async Task OnDriver(Driver d)
        {
            if (d.DriverId <= 0) return;
            try
            {
                using var conn = await OpenAsync();
                var cmd = new MySqlCommand(@"
                    INSERT INTO drivers (driver_id, user_id, license_no, license_photo_url, status, rating_avg, total_trips)
                    VALUES (@id, @uid, @license, @photo, @status, @rating, @trips)
                    ON DUPLICATE KEY UPDATE license_no = @license, license_photo_url = @photo, status = @status, rating_avg = @rating, total_trips = @trips", conn);
                cmd.Parameters.AddWithValue("@id", d.DriverId); cmd.Parameters.AddWithValue("@uid", DbFk(d.UserId)); cmd.Parameters.AddWithValue("@license", DbStr(d.LicenseNo)); cmd.Parameters.AddWithValue("@photo", DbStr(d.LicensePhotoUrl)); cmd.Parameters.AddWithValue("@status", DbStr(d.Status)); cmd.Parameters.AddWithValue("@rating", DbVal(d.RatingAvg)); cmd.Parameters.AddWithValue("@trips", d.TotalTrips);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { LogFailed("drivers", $"DriverId: {d.DriverId}", ex.Message); }
        }

        private async Task OnVehicle(Vehicle v)
        {
            if (v.VehicleId <= 0) return;
            try
            {
                using var conn = await OpenAsync();
                var cmd = new MySqlCommand(@"
                    INSERT INTO vehicles (vehicle_id, plate_no, brand, model, type, cc, status, rate_per_day, rate_with_driver, photo_url, latitude, longitude, current_speed, last_update, model_3d_url, in_garage, created_at)
                    VALUES (@id, @plate, @brand, @model, @type, @cc, @status, @rate, @rateDriver, @photo, @lat, @lng, @speed, @lastUpdate, @model3d, @garage, @created)
                    ON DUPLICATE KEY UPDATE status = @status, rate_per_day = @rate, rate_with_driver = @rateDriver, photo_url = @photo, latitude = @lat, longitude = @lng, current_speed = @speed, last_update = @lastUpdate, model_3d_url = @model3d, in_garage = @garage", conn);
                cmd.Parameters.AddWithValue("@id", v.VehicleId); cmd.Parameters.AddWithValue("@plate", DbStr(v.PlateNo)); cmd.Parameters.AddWithValue("@brand", DbStr(v.Brand)); cmd.Parameters.AddWithValue("@model", DbStr(v.Model)); cmd.Parameters.AddWithValue("@type", DbStr(v.Type)); cmd.Parameters.AddWithValue("@cc", DbVal(v.CC)); cmd.Parameters.AddWithValue("@status", DbStr(v.Status)); cmd.Parameters.AddWithValue("@rate", v.RatePerDay); cmd.Parameters.AddWithValue("@rateDriver", v.RateWithDriver); cmd.Parameters.AddWithValue("@photo", DbStr(v.PhotoUrl)); cmd.Parameters.AddWithValue("@lat", DbVal(v.Latitude)); cmd.Parameters.AddWithValue("@lng", DbVal(v.Longitude)); cmd.Parameters.AddWithValue("@speed", DbVal(v.CurrentSpeed)); cmd.Parameters.AddWithValue("@lastUpdate", DbDate(v.LastUpdate)); cmd.Parameters.AddWithValue("@model3d", DbStr(v.Model3dUrl)); cmd.Parameters.AddWithValue("@garage", v.InGarage); cmd.Parameters.AddWithValue("@created", DbDate(v.CreatedAt));
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { LogFailed("vehicles", $"VehicleId: {v.VehicleId}", ex.Message); }
        }

        // ─────────────────────────────────────────────────────────────────
        //  RENTALS (WITH ASYNC LOCK)
        // ─────────────────────────────────────────────────────────────────
        private async Task OnRental(Rental r)
        {
            if (r.RentalId <= 0) return;

            // PUMILA DITO
            await _rentalLock.WaitAsync();
            try
            {
                using var conn = await OpenAsync();

                if (string.Equals(r.Status, "pending", StringComparison.OrdinalIgnoreCase))
                {
                    var dupCheck = new MySqlCommand(@"
                        SELECT COUNT(*) FROM rentals 
                        WHERE customer_id = @cust AND vehicle_id = @veh AND LOWER(status) = 'pending' AND rental_id <> @id", conn);
                    dupCheck.Parameters.AddWithValue("@cust", r.CustomerId);
                    dupCheck.Parameters.AddWithValue("@veh", r.VehicleId);
                    dupCheck.Parameters.AddWithValue("@id", r.RentalId);

                    if (Convert.ToInt32(await dupCheck.ExecuteScalarAsync()) > 0)
                    {
                        _logger.LogWarning("[{Time}] [SYNC] [rentals] BLOCKED RACE CONDITION: Duplicate pending booking from Mobile App.", Now);
                        return; // Wag i-insert
                    }
                }

                var cmd = new MySqlCommand(@"
                    INSERT INTO rentals (rental_id, customer_id, vehicle_id, driver_id, start_date, end_date, destination, status, total_amount, payment_method, payment_status, qr_code, created_at)
                    VALUES (@id, @cust, @veh, @driver, @start, @end, @dest, @status, @amount, @payMethod, @payStatus, @qr, @created)
                    ON DUPLICATE KEY UPDATE status = @status, end_date = @end, destination = @dest, total_amount = @amount, payment_status = @payStatus, qr_code = @qr", conn);
                cmd.Parameters.AddWithValue("@id", r.RentalId); cmd.Parameters.AddWithValue("@cust", DbFk(r.CustomerId)); cmd.Parameters.AddWithValue("@veh", DbFk(r.VehicleId)); cmd.Parameters.AddWithValue("@driver", DbFk(r.DriverId)); cmd.Parameters.AddWithValue("@start", DbDate(r.StartDate)); cmd.Parameters.AddWithValue("@end", DbDate(r.EndDate)); cmd.Parameters.AddWithValue("@dest", DbStr(r.Destination)); cmd.Parameters.AddWithValue("@status", DbStr(r.Status ?? "pending")); cmd.Parameters.AddWithValue("@amount", r.TotalAmount); cmd.Parameters.AddWithValue("@payMethod", DbStr(r.PaymentMethod)); cmd.Parameters.AddWithValue("@payStatus", DbStr(r.PaymentStatus)); cmd.Parameters.AddWithValue("@qr", DbStr(r.QrCode)); cmd.Parameters.AddWithValue("@created", DbDate(r.CreatedAt));
                await cmd.ExecuteNonQueryAsync();
                LogSuccess("rentals", $"RentalId: {r.RentalId} synced.");
            }
            catch (Exception ex) { LogFailed("rentals", $"RentalId: {r.RentalId}", ex.Message); }
            finally
            {
                // PALAYAIN ANG LOCK PARA SA SUSUNOD
                _rentalLock.Release();
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  TRANSACTIONS (WITH ASYNC LOCK)
        // ─────────────────────────────────────────────────────────────────
        private async Task OnTransaction(Transaction t)
        {
            if (t.TransactionId <= 0) return;

            // PUMILA DITO
            await _txLock.WaitAsync();
            try
            {
                using var conn = await OpenAsync();

                if (string.Equals(t.Status, "pending", StringComparison.OrdinalIgnoreCase))
                {
                    var dupCheck = new MySqlCommand(@"
                        SELECT COUNT(*) FROM transactions 
                        WHERE rental_id = @rental AND LOWER(status) = 'pending' AND transaction_id <> @id", conn);
                    dupCheck.Parameters.AddWithValue("@rental", t.RentalId);
                    dupCheck.Parameters.AddWithValue("@id", t.TransactionId);

                    if (Convert.ToInt32(await dupCheck.ExecuteScalarAsync()) > 0)
                    {
                        _logger.LogWarning("[{Time}] [SYNC] [transactions] BLOCKED RACE CONDITION: Duplicate pending payment from Mobile App.", Now);
                        return; // Wag i-insert
                    }
                }

                var cmd = new MySqlCommand(@"
                    INSERT INTO transactions (transaction_id, rental_id, amount, type, method, proof_url, status, paid_at)
                    VALUES (@id, @rental, @amount, @type, @method, @proof, @status, @paid)
                    ON DUPLICATE KEY UPDATE status = @status, proof_url = @proof", conn);
                cmd.Parameters.AddWithValue("@id", t.TransactionId); cmd.Parameters.AddWithValue("@rental", DbFk(t.RentalId)); cmd.Parameters.AddWithValue("@amount", t.Amount); cmd.Parameters.AddWithValue("@type", DbStr(t.Type)); cmd.Parameters.AddWithValue("@method", DbStr(t.Method)); cmd.Parameters.AddWithValue("@proof", DbStr(t.ProofUrl)); cmd.Parameters.AddWithValue("@status", DbStr(t.Status)); cmd.Parameters.AddWithValue("@paid", DbDate(t.PaidAt));
                await cmd.ExecuteNonQueryAsync();
                LogSuccess("transactions", $"TransactionId: {t.TransactionId} synced.");
            }
            catch (Exception ex) { LogFailed("transactions", $"TransactionId: {t.TransactionId}", ex.Message); }
            finally
            {
                // PALAYAIN ANG LOCK PARA SA SUSUNOD
                _txLock.Release();
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  OTHER SYNC HANDLERS (Messages, Notifications, etc. - No Changes)
        // ─────────────────────────────────────────────────────────────────

        private async Task OnMessage(Message m)
        {
            if (m.MessageId <= 0) return;
            try
            {
                using var conn = await OpenAsync();
                var cmd = new MySqlCommand(@"
                    INSERT INTO messages (message_id, rental_id, sender_id, message_text, media_url, sent_at)
                    VALUES (@id, @rental, @sender, @text, @media, @sent)
                    ON DUPLICATE KEY UPDATE message_text = @text, media_url = @media", conn);
                cmd.Parameters.AddWithValue("@id", m.MessageId); cmd.Parameters.AddWithValue("@rental", DbFk(m.RentalId)); cmd.Parameters.AddWithValue("@sender", DbFk(m.SenderId)); cmd.Parameters.AddWithValue("@text", DbStr(m.MessageText)); cmd.Parameters.AddWithValue("@media", DbStr(m.MediaUrl)); cmd.Parameters.AddWithValue("@sent", DbDate(m.SentAt));
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { LogFailed("messages", $"MessageId: {m.MessageId}", ex.Message); }
        }

        private async Task OnNotification(AppNotification n)
        {
            if (n.NotifId <= 0) return;
            try
            {
                using var conn = await OpenAsync();
                var cmd = new MySqlCommand(@"
                    INSERT INTO notifications (notif_id, user_id, title, body, type, is_read, sent_at)
                    VALUES (@id, @uid, @title, @body, @type, @read, @sent)
                    ON DUPLICATE KEY UPDATE is_read = @read, title = @title, body = @body", conn);
                cmd.Parameters.AddWithValue("@id", n.NotifId); cmd.Parameters.AddWithValue("@uid", DbFk(n.UserId)); cmd.Parameters.AddWithValue("@title", DbStr(n.Title)); cmd.Parameters.AddWithValue("@body", DbStr(n.Body)); cmd.Parameters.AddWithValue("@type", DbStr(n.Type)); cmd.Parameters.AddWithValue("@read", n.IsRead); cmd.Parameters.AddWithValue("@sent", DbDate(n.SentAt));
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { LogFailed("notifications", $"NotifId: {n.NotifId}", ex.Message); }
        }

        private async Task OnRating(Rating r)
        {
            if (r.RatingId <= 0) return;
            try
            {
                using var conn = await OpenAsync();
                var cmd = new MySqlCommand(@"
                    INSERT INTO ratings (rating_id, rental_id, customer_id, driver_id, vehicle_id, driver_score, vehicle_score, comment, rated_at)
                    VALUES (@id, @rental, @cust, @driver, @veh, @dScore, @vScore, @comment, @rated)
                    ON DUPLICATE KEY UPDATE driver_score = @dScore, vehicle_score = @vScore, comment = @comment", conn);
                cmd.Parameters.AddWithValue("@id", r.RatingId); cmd.Parameters.AddWithValue("@rental", DbFk(r.RentalId)); cmd.Parameters.AddWithValue("@cust", DbFk(r.CustomerId)); cmd.Parameters.AddWithValue("@driver", DbFk(r.DriverId)); cmd.Parameters.AddWithValue("@veh", DbFk(r.VehicleId)); cmd.Parameters.AddWithValue("@dScore", DbVal(r.DriverScore)); cmd.Parameters.AddWithValue("@vScore", DbVal(r.VehicleScore)); cmd.Parameters.AddWithValue("@comment", DbStr(r.Comment)); cmd.Parameters.AddWithValue("@rated", DbDate(r.RatedAt));
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { LogFailed("ratings", $"RatingId: {r.RatingId}", ex.Message); }
        }

        private async Task OnGpsLog(GpsLog g)
        {
            if (g.RentalId <= 0) return;
            try
            {
                using var conn = await OpenAsync();
                var cmd = new MySqlCommand("INSERT IGNORE INTO gps_logs (log_id, rental_id, latitude, longitude, odometer_km, logged_at) VALUES (@id, @rental, @lat, @lng, @odo, @logged)", conn);
                cmd.Parameters.AddWithValue("@id", g.LogId); cmd.Parameters.AddWithValue("@rental", DbFk(g.RentalId)); cmd.Parameters.AddWithValue("@lat", DbVal(g.Latitude)); cmd.Parameters.AddWithValue("@lng", DbVal(g.Longitude)); cmd.Parameters.AddWithValue("@odo", DbVal(g.OdometerKm)); cmd.Parameters.AddWithValue("@logged", DbDate(g.LoggedAt));
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { LogFailed("gps_logs", $"LogId: {g.LogId}", ex.Message); }
        }

        private async Task OnLocationLog(LocationLog l)
        {
            if (l.VehicleId <= 0) return;
            try
            {
                using var conn = await OpenAsync();
                var insertCmd = new MySqlCommand("INSERT IGNORE INTO location_logs (log_id, rental_id, vehicle_id, latitude, longitude, speed_kmh, logged_at) VALUES (@id, @rental, @veh, @lat, @lng, @speed, @logged)", conn);
                insertCmd.Parameters.AddWithValue("@id", l.LogId); insertCmd.Parameters.AddWithValue("@rental", DbFk(l.RentalId)); insertCmd.Parameters.AddWithValue("@veh", DbFk(l.VehicleId)); insertCmd.Parameters.AddWithValue("@lat", DbVal(l.Latitude)); insertCmd.Parameters.AddWithValue("@lng", DbVal(l.Longitude)); insertCmd.Parameters.AddWithValue("@speed", DbVal(l.SpeedKmh)); insertCmd.Parameters.AddWithValue("@logged", DbDate(l.LoggedAt));
                await insertCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { LogFailed("location_logs", $"LogId: {l.LogId}", ex.Message); }
        }

        private async Task OnIncident(Incident i)
        {
            if (i.IncidentId <= 0) return;
            try
            {
                using var conn = await OpenAsync();
                var cmd = new MySqlCommand(@"
                    INSERT INTO incidents (incident_id, rental_id, reported_by, description, photo_url, status, reported_at)
                    VALUES (@id, @rental, @reporter, @desc, @photo, @status, @reported)
                    ON DUPLICATE KEY UPDATE status = @status, description = @desc, photo_url = @photo", conn);
                cmd.Parameters.AddWithValue("@id", i.IncidentId); cmd.Parameters.AddWithValue("@rental", DbFk(i.RentalId)); cmd.Parameters.AddWithValue("@reporter", DbFk(i.ReportedBy)); cmd.Parameters.AddWithValue("@desc", DbStr(i.Description)); cmd.Parameters.AddWithValue("@photo", DbStr(i.PhotoUrl)); cmd.Parameters.AddWithValue("@status", DbStr(i.Status)); cmd.Parameters.AddWithValue("@reported", DbDate(i.ReportedAt));
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { LogFailed("incidents", $"IncidentId: {i.IncidentId}", ex.Message); }
        }

        private async Task OnIssue(Issue iss)
        {
            if (iss.IssueId <= 0) return;
            try
            {
                using var conn = await OpenAsync();
                var cmd = new MySqlCommand(@"
                    INSERT INTO issues (issue_id, rental_id, reporter_id, issue_type, description, image_url, status, reported_at)
                    VALUES (@id, @rental, @reporter, @type, @desc, @img, @status, @reported)
                    ON DUPLICATE KEY UPDATE status = @status, description = @desc, image_url = @img", conn);
                cmd.Parameters.AddWithValue("@id", iss.IssueId); cmd.Parameters.AddWithValue("@rental", DbFk(iss.RentalId)); cmd.Parameters.AddWithValue("@reporter", DbFk(iss.ReporterId)); cmd.Parameters.AddWithValue("@type", DbStr(iss.IssueType)); cmd.Parameters.AddWithValue("@desc", DbStr(iss.Description)); cmd.Parameters.AddWithValue("@img", DbStr(iss.ImageUrl)); cmd.Parameters.AddWithValue("@status", DbStr(iss.Status)); cmd.Parameters.AddWithValue("@reported", DbDate(iss.ReportedAt));
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { LogFailed("issues", $"IssueId: {iss.IssueId}", ex.Message); }
        }

        private async Task OnExtension(Extension ext)
        {
            if (ext.ExtensionId <= 0) return;
            try
            {
                using var conn = await OpenAsync();
                var cmd = new MySqlCommand(@"
                    INSERT INTO extensions (extension_id, rental_id, added_days, added_fee, status, requested_at)
                    VALUES (@id, @rental, @days, @fee, @status, @requested)
                    ON DUPLICATE KEY UPDATE status = @status, added_fee = @fee", conn);
                cmd.Parameters.AddWithValue("@id", ext.ExtensionId); cmd.Parameters.AddWithValue("@rental", DbFk(ext.RentalId)); cmd.Parameters.AddWithValue("@days", DbVal(ext.AddedDays)); cmd.Parameters.AddWithValue("@fee", DbVal(ext.AddedFee)); cmd.Parameters.AddWithValue("@status", DbStr(ext.Status)); cmd.Parameters.AddWithValue("@requested", DbDate(ext.RequestedAt));
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { LogFailed("extensions", $"ExtensionId: {ext.ExtensionId}", ex.Message); }
        }
    }
}