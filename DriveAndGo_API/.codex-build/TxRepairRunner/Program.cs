using DriveAndGo_Admin.Helpers;

const string connectionString = "Server=localhost;Database=vehicle_rental_db;Uid=root;Pwd=;";

int changed = AdminDataHelper.ReconcilePaidRentalTransactions(connectionString);
Console.WriteLine($"changed={changed}");
