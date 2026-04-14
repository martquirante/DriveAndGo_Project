using DriveAndGo_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;

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

        [HttpPost("send")]
        public IActionResult SendMessage([FromBody] Message msg)
        {
            if (msg.RentalId == 0 || msg.SenderId == 0 || string.IsNullOrWhiteSpace(msg.Content))
            {
                return BadRequest(new { message = "RentalId, SenderId, and Content are required." });
            }

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // Direct SQL based on your schema
                string sql = @"
                    INSERT INTO messages (rental_id, sender_id, message_text, media_url, sent_at)
                    VALUES (@rental_id, @sender, @content, @media, NOW())";

                using var insertCmd = new MySqlCommand(sql, conn);
                insertCmd.Parameters.AddWithValue("@rental_id", msg.RentalId);
                insertCmd.Parameters.AddWithValue("@sender", msg.SenderId);
                insertCmd.Parameters.AddWithValue("@content", msg.Content);
                insertCmd.Parameters.AddWithValue("@media", string.IsNullOrWhiteSpace(msg.AttachmentUrl) ? DBNull.Value : msg.AttachmentUrl);

                insertCmd.ExecuteNonQuery();

                return Ok(new { message = "Message sent successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database Error: " + ex.Message });
            }
        }

        [HttpGet("history/{rentalId}")]
        public IActionResult GetChatHistory(int rentalId)
        {
            try
            {
                var messages = new List<Message>();

                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                string sql = @"
                    SELECT
                        m.message_id,
                        m.rental_id,
                        m.sender_id,
                        m.message_text AS content,
                        m.media_url AS attachment_url,
                        m.sent_at,
                        u.full_name AS sender_name
                    FROM messages m
                    JOIN users u ON m.sender_id = u.user_id
                    WHERE m.rental_id = @rental_id
                    ORDER BY m.sent_at ASC, m.message_id ASC";

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@rental_id", rentalId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    messages.Add(new Message
                    {
                        MessageId = Convert.ToInt32(reader["message_id"]),
                        RentalId = Convert.ToInt32(reader["rental_id"]),
                        SenderId = Convert.ToInt32(reader["sender_id"]),
                        Content = reader["content"] == DBNull.Value ? null : reader["content"].ToString(),
                        AttachmentUrl = reader["attachment_url"] == DBNull.Value ? null : reader["attachment_url"].ToString(),
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

        [HttpPatch("mark-read")]
        public IActionResult MarkAsRead([FromQuery] int rentalId, [FromQuery] int receiverId)
        {
            // Dahil wala namang 'is_read' column sa database mo, ibabalik na lang natin itong Ok
            // para hindi mag-error ang Frontend kung sakaling tinatawag niya ito.
            return Ok(new { message = "Mark as read acknowledged." });
        }
    }
}