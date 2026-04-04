using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Firebase.Database;
using MySql.Data.MySqlClient;
using DriveAndGo_API.Models;

namespace DriveAndGo_API.Services
{
    // Ginawa nating BackgroundService para automatic siyang tumakbo sa background
    public class FirebaseSyncService : BackgroundService
    {
        private readonly string _firebaseUrl = "https://vechiclerentaldb-default-rtdb.firebaseio.com/";
        private readonly string _mysqlConnectionString;
        private readonly FirebaseClient _firebaseClient;

        // I-inject natin ang IConfiguration para makuha ang connection string sa appsettings.json
        public FirebaseSyncService(IConfiguration configuration)
        {
            _mysqlConnectionString = configuration.GetConnectionString("DefaultConnection")
                                   ?? "Server=localhost;Database=vehicle_rental_db;Uid=root;Pwd=;";
            _firebaseClient = new FirebaseClient(_firebaseUrl);
        }

        // Ito ang automatic na tatawagin ng system pagka-run ng API
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("🟢 [Background Service] Starting Firebase to MySQL Auto-Sync...");

            _firebaseClient
                .Child("rentals")
                .AsObservable<Rental>()
                .Subscribe(d =>
                {
                    if (d.EventType == Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate)
                    {
                        var rentalData = d.Object;
                        Console.WriteLine($"⚡ [SYNC] Firebase Update Detected! Syncing Rental: {d.Key} to XAMPP...");
                        SyncRentalToMySql(rentalData);
                    }
                });

            return Task.CompletedTask;
        }

        private void SyncRentalToMySql(Rental rental)
        {
            try
            {
                using var conn = new MySqlConnection(_mysqlConnectionString);
                conn.Open();

                // Note: Paki-check lang kung ang column name sa users table mo ay user_id o customer_id
                var syncCmd = new MySqlCommand(@"
                    INSERT INTO rentals 
                        (rental_id, user_id, vehicle_id, driver_id, start_date, end_date, status) 
                    VALUES 
                        (@id, @customer, @vehicle, @driver, @start, @end, @status)
                    ON DUPLICATE KEY UPDATE 
                        status = @status, 
                        end_date = @end", conn);

                // Assuming string ang ID sa app, pero kung INT sa mySQL, i-convert natin.
                syncCmd.Parameters.AddWithValue("@id", rental.RentalId);
                syncCmd.Parameters.AddWithValue("@customer", rental.CustomerId);
                syncCmd.Parameters.AddWithValue("@vehicle", rental.VehicleId);
                syncCmd.Parameters.AddWithValue("@driver", rental.DriverId.HasValue ? (object)rental.DriverId.Value : DBNull.Value);
                syncCmd.Parameters.AddWithValue("@start", rental.StartDate);
                syncCmd.Parameters.AddWithValue("@end", rental.EndDate);
                syncCmd.Parameters.AddWithValue("@status", rental.Status ?? "pending");
                // Pwede mong idiskarte pa dito ung total_amount at payment columns depende sa update ng DB mo.

                syncCmd.ExecuteNonQuery();
                Console.WriteLine($"✅ [SUCCESS] Rental {rental.RentalId} synced to MySQL!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [ERROR] Sync Failed: {ex.Message}");
            }
        }
    }
}