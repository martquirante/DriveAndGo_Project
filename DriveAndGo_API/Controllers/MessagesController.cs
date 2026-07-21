using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using DriveAndGo_API.Hubs;
using System.Text.Json;

namespace DriveAndGo_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MessagesController : ControllerBase
{
    private readonly NpgsqlDataSource _ds;
    private readonly IHubContext<AdminHub> _hubContext;

    public MessagesController(NpgsqlDataSource ds, IHubContext<AdminHub> hubContext)
    {
        _ds = ds;
        _hubContext = hubContext;
    }

    // GET /api/messages?senderId=admin&receiverId=d1
    [HttpGet]
    public async Task<IActionResult> GetChatHistory([FromQuery] string senderId, [FromQuery] string receiverId)
    {
        try
        {
            var list = new List<object>();
            await using var conn = await _ds.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT message_id, sender_id, receiver_id, message_body, timestamp, is_group_chat
                  FROM chat_messages
                  WHERE (sender_id = @sid AND receiver_id = @rid) OR (sender_id = @rid AND receiver_id = @sid)
                  ORDER BY timestamp ASC", conn);
            cmd.Parameters.AddWithValue("@sid", senderId);
            cmd.Parameters.AddWithValue("@rid", receiverId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new
                {
                    messageId = reader.GetInt32(reader.GetOrdinal("message_id")),
                    senderId = reader["sender_id"].ToString(),
                    receiverId = reader["receiver_id"].ToString(),
                    messageBody = reader["message_body"].ToString(),
                    timestamp = reader.GetDateTime(reader.GetOrdinal("timestamp")),
                    isGroupChat = reader.GetBoolean(reader.GetOrdinal("is_group_chat"))
                });
            }
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    // POST /api/messages
    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] MessageRequest req)
    {
        try
        {
            await using var conn = await _ds.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO chat_messages (sender_id, receiver_id, message_body, timestamp, is_group_chat)
                  VALUES (@sid, @rid, @body, NOW(), @group)
                  RETURNING message_id, timestamp", conn);
            cmd.Parameters.AddWithValue("@sid", req.SenderId);
            cmd.Parameters.AddWithValue("@rid", req.ReceiverId);
            cmd.Parameters.AddWithValue("@body", req.MessageBody);
            cmd.Parameters.AddWithValue("@group", req.IsGroupChat);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                int messageId = reader.GetInt32(0);
                DateTime ts = reader.GetDateTime(1);

                // Broadcast via SignalR Hub so active receivers get it in real-time
                await _hubContext.Clients.All.SendAsync("ReceiveChatMessage", req.SenderId, req.ReceiverId, req.MessageBody, ts.ToString("o"));

                return Ok(new { Message = "Message sent and stored.", MessageId = messageId, Timestamp = ts });
            }
            return BadRequest(new { Message = "Failed to store message." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    // GET /api/messages/conversations?userId=admin
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations([FromQuery] string userId)
    {
        try
        {
            var list = new List<object>();
            await using var conn = await _ds.OpenConnectionAsync();
            
            // Query to find distinct user IDs (drivers/customers) that have exchanged messages with this user,
            // along with the last message content and timestamp.
            await using var cmd = new NpgsqlCommand(
                @"WITH LastMsgs AS (
                    SELECT 
                        CASE WHEN sender_id = @userId THEN receiver_id ELSE sender_id END AS contact_id,
                        message_body,
                        timestamp,
                        ROW_NUMBER() OVER (PARTITION BY CASE WHEN sender_id = @userId THEN receiver_id ELSE sender_id END ORDER BY timestamp DESC) as rn
                    FROM chat_messages
                    WHERE sender_id = @userId OR receiver_id = @userId
                  )
                  SELECT contact_id, message_body, timestamp
                  FROM LastMsgs
                  WHERE rn = 1
                  ORDER BY timestamp DESC", conn);
            cmd.Parameters.AddWithValue("@userId", userId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string contactId = reader["contact_id"].ToString();
                // Map contact details from dummy database or custom name based on prefix
                string contactName = contactId.StartsWith("d") ? $"Driver {contactId.Substring(1)}" : $"Customer {contactId.Substring(1)}";
                string role = contactId.StartsWith("d") ? "Driver" : "Customer";

                list.Add(new
                {
                    id = contactId,
                    name = contactName,
                    role = role,
                    lastMessage = reader["message_body"].ToString(),
                    time = reader.GetDateTime(reader.GetOrdinal("timestamp")).ToString("t"),
                    unreadCount = 0 // dynamically computed or zero for now
                });
            }

            // If empty, supply default active conversation starters so they can write to them
            if (list.Count == 0)
            {
                list.Add(new { id = "d1", name = "Juan dela Cruz", role = "Driver", lastMessage = "Tap to start conversation", time = "12:00", unreadCount = 0 });
                list.Add(new { id = "d2", name = "Maria Santos", role = "Driver", lastMessage = "Tap to start conversation", time = "12:00", unreadCount = 0 });
                list.Add(new { id = "c1", name = "Rico Gonzalez", role = "Customer", lastMessage = "Tap to start conversation", time = "12:00", unreadCount = 0 });
            }

            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = ex.Message });
        }
    }
}

public class MessageRequest
{
    public string SenderId { get; set; } = string.Empty;
    public string ReceiverId { get; set; } = string.Empty;
    public string MessageBody { get; set; } = string.Empty;
    public bool IsGroupChat { get; set; } = false;
}