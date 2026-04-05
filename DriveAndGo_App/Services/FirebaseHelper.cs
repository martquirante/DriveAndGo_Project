using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reactive.Linq;
using Firebase.Database;
using Firebase.Database.Query;

namespace DriveAndGo_App.Services
{
    public class FirebaseHelper
    {
        // 1. SECURED & UPDATED URL (Kinuha natin sa config na binigay mo)
        private readonly string _firebaseUrl = "https://vechiclerentaldb-default-rtdb.asia-southeast1.firebasedatabase.app/";
        private readonly FirebaseClient _firebase;

        public FirebaseHelper()
        {
            _firebase = new FirebaseClient(_firebaseUrl);
        }

        // ==========================================
        // 📍 1. LOCATION LOGS (GPS Tracking)
        // ==========================================

        // App -> Firebase: Ibabato ng app ng driver ang location niya
        public async Task SendLocationAsync(string rentalId, double lat, double lon, double speed)
        {
            await _firebase
                .Child("location_logs")
                .Child(rentalId) // I-group natin per rental para madali hanapin
                .PostAsync(new
                {
                    Latitude = lat,
                    Longitude = lon,
                    SpeedKmh = speed,
                    LoggedAt = DateTime.UtcNow.ToString("o")
                });
        }

        // Firebase -> App: Pakikinggan ng app ng customer kung nasaan na ang driver
        public void ListenToDriverLocation(string rentalId, Action<double, double> onLocationUpdated)
        {
            _firebase
                .Child("location_logs")
                .Child(rentalId)
                .AsObservable<dynamic>()
                .Subscribe(d =>
                {
                    if (d.EventType == Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate)
                    {
                        var lat = (double)d.Object.Latitude;
                        var lon = (double)d.Object.Longitude;
                        onLocationUpdated?.Invoke(lat, lon);
                    }
                });
        }

        // ==========================================
        // 💬 2. MESSAGES (Live Chat System)
        // ==========================================

        // Mag-send ng chat (Driver to Customer or vice versa)
        public async Task SendMessageAsync(string rentalId, string senderId, string messageText)
        {
            await _firebase
                .Child("messages")
                .Child(rentalId)
                .PostAsync(new
                {
                    SenderId = senderId,
                    MessageText = messageText,
                    SentAt = DateTime.UtcNow.ToString("o")
                });
        }

        // Makinig sa mga bagong chat para lumabas agad sa screen nang walang refresh
        public void ListenToMessages(string rentalId, Action<string, string> onNewMessage)
        {
            _firebase
                .Child("messages")
                .Child(rentalId)
                .AsObservable<dynamic>()
                .Subscribe(d =>
                {
                    if (d.EventType == Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate)
                    {
                        string sender = d.Object.SenderId;
                        string text = d.Object.MessageText;
                        onNewMessage?.Invoke(sender, text);
                    }
                });
        }

        // ==========================================
        // 🔔 3. NOTIFICATIONS (Live Alerts)
        // ==========================================

        public async Task SendNotificationAsync(string userId, string title, string body, string type)
        {
            await _firebase
                .Child("notifications")
                .Child(userId)
                .PostAsync(new
                {
                    Title = title,
                    Body = body,
                    Type = type,
                    IsRead = false,
                    SentAt = DateTime.UtcNow.ToString("o")
                });
        }

        // ==========================================
        // 🚗 4. RENTAL STATUS UPDATES (Bridge to XAMPP)
        // ==========================================

        // Kapag nag-book ang user, isesend dito. (Kukunin ito ng API natin para i-save sa XAMPP MySQL)
        public async Task CreateRentalBookingAsync(string rentalId, string customerId, string vehicleId, string destination)
        {
            await _firebase
                .Child("rentals")
                .Child(rentalId)
                .PutAsync(new // PutAsync para exact ID ang gamitin
                {
                    RentalId = rentalId,
                    CustomerId = customerId,
                    VehicleId = vehicleId,
                    Destination = destination,
                    Status = "pending",
                    StartDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                });
        }

        // Kapag in-update ng Admin sa system, makikita agad ng user sa app
        public void ListenToRentalStatus(string rentalId, Action<string> onStatusChanged)
        {
            _firebase
                .Child("rentals")
                .Child(rentalId)
                .AsObservable<dynamic>()
                .Subscribe(d =>
                {
                    if (d.EventType == Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate)
                    {
                        string currentStatus = d.Object.Status;
                        onStatusChanged?.Invoke(currentStatus);
                    }
                });
        }
    }
}