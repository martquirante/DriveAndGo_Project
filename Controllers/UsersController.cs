using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using DriveAndGo_API.Models;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly string _connectionString;

        public UsersController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet]
        public IActionResult GetUsers()
        {
            List<User> users = new List<User>();

            using (MySqlConnection conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                // Kukunin natin ang details ng users, pero hindi natin isasama ang password sa display para secure!
                MySqlCommand cmd = new MySqlCommand("SELECT user_id, full_name, email, phone, role FROM users", conn);

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        users.Add(new User
                        {
                            UserId = Convert.ToInt32(reader["user_id"]),
                            FullName = reader["full_name"].ToString(),
                            Email = reader["email"].ToString(),
                            Phone = reader["phone"].ToString(),
                            Role = reader["role"].ToString()
                        });
                    }
                }
            }

            return Ok(users);
        }
    }
}