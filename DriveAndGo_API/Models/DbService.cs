using Npgsql;

namespace DriveAndGo_API.Services
{
    /// <summary>
    /// Provides raw Npgsql ADO.NET connections for controllers that use direct SQL queries.
    /// For schema management, use AppDbContext (EF Core) instead.
    /// </summary>
    public class DbService
    {
        private readonly IConfiguration _configuration;

        public DbService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public NpgsqlConnection CreateConnection()
        {
            var connStr = _configuration.GetConnectionString("DefaultConnection");
            return new NpgsqlConnection(connStr);
        }
    }
}