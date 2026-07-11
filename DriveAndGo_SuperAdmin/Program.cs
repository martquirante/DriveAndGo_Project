using System;
using Npgsql;

namespace DriveAndGo_SuperAdmin
{
    class Program
    {
        // PostgreSQL local Docker connection string as default
        static string connectionString = "Host=localhost;Database=driveandgo_test_db;Username=postgres;Password=postgres_local_password;Port=5432";

        static void Main(string[] args)
        {
            while (true)
            {
                Console.Clear(); // Para malinis ang screen tuwing babalik sa menu
                Console.WriteLine("==================================================");
                Console.WriteLine("      VEHICLE RENTAL: SUPER ADMIN CONSOLE         ");
                Console.WriteLine("==================================================");
                Console.WriteLine("1. Create Admin Account");
                Console.WriteLine("2. View All Admins");
                Console.WriteLine("3. Update Admin Account");
                Console.WriteLine("4. Delete Admin Account");
                Console.WriteLine("5. Exit");
                Console.WriteLine("==================================================");
                Console.Write("Select an option (1-5): ");

                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        CreateAdmin();
                        break;
                    case "2":
                        ViewAdmins();
                        break;
                    case "3":
                        UpdateAdmin();
                        break;
                    case "4":
                        DeleteAdmin();
                        break;
                    case "5":
                        Console.WriteLine("Exiting program...");
                        return; // Lalabas na sa app
                    default:
                        Console.WriteLine("\nInvalid choice. Press Enter to try again.");
                        Console.ReadLine();
                        break;
                }
            }
        }

        // ==========================================
        // 1. CREATE 
        // ==========================================
        static void CreateAdmin()
        {
            Console.WriteLine("\n--- CREATE NEW ADMIN ---");
            Console.Write("Enter Full Name: ");
            string fullName = Console.ReadLine() ?? string.Empty;

            Console.Write("Enter Email: ");
            string email = Console.ReadLine() ?? string.Empty;

            Console.Write("Enter Password: ");
            string password = Console.ReadLine() ?? string.Empty;

            Console.Write("Enter Phone Number: ");
            string phone = Console.ReadLine() ?? string.Empty;

            string role = "admin";

            try
            {
                using (NpgsqlConnection conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "INSERT INTO users (full_name, email, password_hash, phone, role) VALUES (@name, @email, @pass, @phone, @role)";
                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@name", fullName);
                        cmd.Parameters.AddWithValue("@email", email);
                        cmd.Parameters.AddWithValue("@pass", password); // Stores plain text password (or hash if user enters it)
                        cmd.Parameters.AddWithValue("@phone", phone);
                        cmd.Parameters.AddWithValue("@role", role);

                        cmd.ExecuteNonQuery();
                    }

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\nSUCCESS: ADMIN account created for {fullName}!");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nERROR: " + ex.Message);
            }

            Console.ResetColor();
            Console.WriteLine("Press Enter to return to menu...");
            Console.ReadLine();
        }

        // ==========================================
        // 2. READ / VIEW
        // ==========================================
        static void ViewAdmins()
        {
            Console.WriteLine("\n--- LIST OF ADMINS ---");
            try
            {
                using (NpgsqlConnection conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT user_id, full_name, email, phone FROM users WHERE role = 'admin'";
                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
                    {
                        using (NpgsqlDataReader reader = cmd.ExecuteReader())
                        {
                            Console.WriteLine("ID\tName\t\tEmail\t\t\tPhone");
                            Console.WriteLine("----------------------------------------------------------------------");
                            while (reader.Read())
                            {
                                Console.WriteLine($"{reader["user_id"]}\t{reader["full_name"]}\t{reader["email"]}\t{reader["phone"]}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nERROR: " + ex.Message);
                Console.ResetColor();
            }

            Console.WriteLine("\nPress Enter to return to menu...");
            Console.ReadLine();
        }

        // ==========================================
        // 3. UPDATE
        // ==========================================
        static void UpdateAdmin()
        {
            Console.WriteLine("\n--- UPDATE ADMIN ACCOUNT ---");
            Console.Write("Enter the User ID of the Admin you want to update: ");
            if (int.TryParse(Console.ReadLine(), out int userId))
            {
                Console.Write("Enter NEW Full Name: ");
                string newName = Console.ReadLine() ?? string.Empty;

                Console.Write("Enter NEW Phone Number: ");
                string newPhone = Console.ReadLine() ?? string.Empty;

                try
                {
                    using (NpgsqlConnection conn = new NpgsqlConnection(connectionString))
                    {
                        conn.Open();
                        string query = "UPDATE users SET full_name = @name, phone = @phone WHERE user_id = @id AND role = 'admin'";
                        using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@name", newName);
                            cmd.Parameters.AddWithValue("@phone", newPhone);
                            cmd.Parameters.AddWithValue("@id", userId);

                            int rows = cmd.ExecuteNonQuery();

                            if (rows > 0)
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("\nSUCCESS: Admin account updated!");
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("\nWARNING: No admin found with that ID.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\nERROR: " + ex.Message);
                }
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine("Invalid ID format.");
            }

            Console.WriteLine("Press Enter to return to menu...");
            Console.ReadLine();
        }

        // ==========================================
        // 4. DELETE
        // ==========================================
        static void DeleteAdmin()
        {
            Console.WriteLine("\n--- DELETE ADMIN ACCOUNT ---");
            Console.Write("Enter the User ID of the Admin you want to delete: ");

            if (int.TryParse(Console.ReadLine(), out int userId))
            {
                Console.Write($"Are you sure you want to delete Admin ID {userId}? (Y/N): ");
                if ((Console.ReadLine() ?? string.Empty).ToUpper() == "Y")
                {
                    try
                    {
                        using (NpgsqlConnection conn = new NpgsqlConnection(connectionString))
                        {
                            conn.Open();
                            string query = "DELETE FROM users WHERE user_id = @id AND role = 'admin'";
                            using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
                            {
                                cmd.Parameters.AddWithValue("@id", userId);

                                int rows = cmd.ExecuteNonQuery();

                                if (rows > 0)
                                {
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine("\nSUCCESS: Admin account deleted!");
                                }
                                else
                                {
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("\nWARNING: No admin found with that ID.");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\nERROR: " + ex.Message);
                    }
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine("Deletion cancelled.");
                }
            }
            else
            {
                Console.WriteLine("Invalid ID format.");
            }

            Console.WriteLine("Press Enter to return to menu...");
            Console.ReadLine();
        }
    }
}