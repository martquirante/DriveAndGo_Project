using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using DriveAndGo_API.Models;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MessagesController : ControllerBase
    {
        private readonly string _connectionString;

        public MessagesController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        // ══ POST — Mag-send ng message (Customer to Driver or Driver to Customer) ══
        [HttpPost("send")]
        public IActionResult SendMessage([FromBody] Message msg)
        {
            if (msg.RentalId == 0 || msg.SenderId == 0 || msg.ReceiverId == 0 || string.IsNullOrWhiteSpace(msg.Content))
                return BadRequest(new { message = "RentalId, SenderId, ReceiverId, and Content are required." });

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var insertCmd = new MySqlCommand(@"
                    INSERT INTO messages 
                        (rental_id, sender_id, receiver_id, content, attachment_url, is_read, sent_at) 
                    VALUES 
                        (@rental_id, @sender, @receiver, @content, @attachment, 0, NOW())", conn);

                insertCmd.Parameters.AddWithValue("@rental_id", msg.RentalId);
                insertCmd.Parameters.AddWithValue("@sender", msg.SenderId);
                insertCmd.Parameters.AddWithValue("@receiver", msg.ReceiverId);
                insertCmd.Parameters.AddWithValue("@content", msg.Content);
                insertCmd.Parameters.AddWithValue("@attachment", msg.AttachmentUrl ?? (object)DBNull.Value);

                insertCmd.ExecuteNonQuery();

                return Ok(new { message = "Message sent successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database Error: " + ex.Message });
            }
        }

        // ══ GET — Kunin ang conversation history ng isang byahe (Rental ID) ══
        [HttpGet("history/{rentalId}")]
        public IActionResult GetChatHistory(int rentalId)
        {
            try
            {
                List<Message> messages = new List<Message>();
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT m.*, u.full_name AS sender_name 
                    FROM messages m
                    JOIN users u ON m.sender_id = u.user_id
                    WHERE m.rental_id = @rental_id
                    ORDER BY m.sent_at ASC", conn);
                cmd.Parameters.AddWithValue("@rental_id", rentalId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    messages.Add(new Message
                    {
                        MessageId = Convert.ToInt32(reader["message_id"]),
                        RentalId = Convert.ToInt32(reader["rental_id"]),
                        SenderId = Convert.ToInt32(reader["sender_id"]),
                        ReceiverId = Convert.ToInt32(reader["receiver_id"]),
                        Content = reader["content"].ToString(),
                        AttachmentUrl = reader["attachment_url"].ToString(),
                        IsRead = Convert.ToBoolean(reader["is_read"]),
                        SentAt = Convert.ToDateTime(reader["sent_at"]),
                        SenderName = reader["sender_name"].ToString()
                    });
                }

                return Ok(messages);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database Error: " + ex.Message });
            }
        }

        // ══ PATCH — I-mark as read ang mga messages kapag binuksan ang chatbox ══
        [HttpPatch("mark-read")]
        public IActionResult MarkAsRead([FromQuery] int rentalId, [FromQuery] int receiverId)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // I-update na nabasa na ng receiver ang mga message sa specific na byahe
                var updateCmd = new MySqlCommand(@"
                    UPDATE messages 
                    SET is_read = 1 
                    WHERE rental_id = @rental_id AND receiver_id = @receiver AND is_read = 0", conn);
                updateCmd.Parameters.AddWithValue("@rental_id", rentalId);
                updateCmd.Parameters.AddWithValue("@receiver", receiverId);

                updateCmd.ExecuteNonQuery();

                return Ok(new { message = "Messages marked as read." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database Error: " + ex.Message });
            }
        }
    }
}