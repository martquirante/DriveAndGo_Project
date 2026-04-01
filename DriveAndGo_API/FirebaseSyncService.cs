using System;
using System.Reactive.Linq;
using Firebase.Database;
using Firebase.Database.Query;
using MySql.Data.MySqlClient;
using DriveAndGo_API.Models;

namespace DriveAndGo_API.Services
{
    public class FirebaseSyncService
    {
        // ══ Updated with your Firebase Project ID ══
        // Note: I-verify ang exact URL na ito sa Firebase Console > Realtime Database tab mo.
        private readonly string _firebaseUrl = "https://vechiclerentaldb-default-rtdb.firebaseio.com/";
        private readonly string _mysqlConnectionString = "Server=localhost;Database=vehicle_rental_db;Uid=root;Pwd=;";

        private FirebaseClient _firebaseClient;

        public FirebaseSyncService()
        {
            _firebaseClient = new FirebaseClient(_firebaseUrl);
        }

        // ══ Tatawagin ito pagka-run ng system para mag-start ang Auto-Sync ══
        public void StartSync()
        {
            Console.WriteLine("Starting Firebase to MySQL Auto-Sync...");

            // Nakikinig (listening) ito sa "rentals" node ng Firebase mo in real-time
            _firebaseClient
                .Child("rentals")
                .AsObservable<Rental>()
                .Subscribe(d =>
                {
                    // Kapag may bagong data (Insert) o may nabago (Update) sa Firebase
                    if (d.EventType == Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate)
                    {
                        var rentalData = d.Object;
                        Console.WriteLine($"New data detected from Firebase! Syncing Rental ID: {d.Key} to XAMPP...");

                        // I-save sa MySQL XAMPP
                        SyncRentalToMySql(rentalData);
                    }
                });
        }

        // ══ Ito ang function na maglalagay ng data from Firebase JSON to XAMPP SQL ══
        private void SyncRentalToMySql(Rental rental)
        {
            try
            {
                using var conn = new MySqlConnection(_mysqlConnectionString);
                conn.Open();

                // Gagamit tayo ng INSERT ... ON DUPLICATE KEY UPDATE 
                var syncCmd = new MySqlCommand(@"
                    INSERT INTO rentals 
                        (rental_id, customer_id, vehicle_id, driver_id, start_date, end_date, destination, status, total_amount, payment_method, payment_status) 
                    VALUES 
                        (@id, @customer, @vehicle, @driver, @start, @end, @dest, @status, @total, @pay_method, @pay_status)
                    ON DUPLICATE KEY UPDATE 
                        status = @status, 
                        payment_status = @pay_status,
                        total_amount = @total", conn);

                syncCmd.Parameters.AddWithValue("@id", rental.RentalId);
                syncCmd.Parameters.AddWithValue("@customer", rental.CustomerId);
                syncCmd.Parameters.AddWithValue("@vehicle", rental.VehicleId);
                syncCmd.Parameters.AddWithValue("@driver", rental.DriverId.HasValue ? (object)rental.DriverId.Value : DBNull.Value);
                syncCmd.Parameters.AddWithValue("@start", rental.StartDate);
                syncCmd.Parameters.AddWithValue("@end", rental.EndDate);
                syncCmd.Parameters.AddWithValue("@dest", rental.Destination ?? "");
                syncCmd.Parameters.AddWithValue("@status", rental.Status ?? "pending");
                syncCmd.Parameters.AddWithValue("@total", rental.TotalAmount);
                syncCmd.Parameters.AddWithValue("@pay_method", rental.PaymentMethod ?? "cash");
                syncCmd.Parameters.AddWithValue("@pay_status", rental.PaymentStatus ?? "unpaid");

                syncCmd.ExecuteNonQuery();
                Console.WriteLine("Successfully synced to XAMPP MySQL!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Sync Error: " + ex.Message);
            }
        }
    }
}