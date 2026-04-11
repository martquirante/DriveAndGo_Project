using MySql.Data.MySqlClient;

namespace DriveAndGo_API.Services
{
    public class DbService
    {
        private readonly IConfiguration _configuration;

        public DbService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public MySqlConnection CreateConnection()
        {
            var connStr = _configuration.GetConnectionString("DefaultConnection");
            return new MySqlConnection(connStr);
        }
    }
}