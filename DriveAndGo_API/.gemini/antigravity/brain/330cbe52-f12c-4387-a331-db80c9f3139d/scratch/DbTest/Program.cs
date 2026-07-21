using System;
using Npgsql;

try
{
    var connString = "Host=localhost;Port=5432;Database=driveandgo_test_db;Username=postgres;Password=postgres_local_password;";
    Console.WriteLine("Connecting to local database...");
    using var conn = new NpgsqlConnection(connString);
    conn.Open();
    Console.WriteLine("Connection opened successfully!");
}
catch (Exception ex)
{
    Console.WriteLine("EXCEPTION CAUGHT:");
    Console.WriteLine(ex.Message);
}
