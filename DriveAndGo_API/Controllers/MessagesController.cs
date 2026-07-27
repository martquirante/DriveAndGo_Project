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
                  WHERE (sender_id = @sid AND receiver_id = @rid)
                     OR (sender_id = @rid AND receiver_id = @sid)
                     OR (receiver_id = @rid AND is_group_chat = true)
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
            var seenIds = new HashSet<string>();
            
            // 1. Fetch conversations with messages in chat_messages
            await using (var cmd = new NpgsqlCommand(
                @"WITH LastMsgs AS (
                    SELECT 
                        CASE WHEN sender_id = @userId THEN receiver_id ELSE sender_id END AS contact_id,
                        message_body,
                        timestamp,
                        is_group_chat,
                        ROW_NUMBER() OVER (PARTITION BY CASE WHEN sender_id = @userId THEN receiver_id ELSE sender_id END ORDER BY timestamp DESC) as rn
                    FROM chat_messages
                    WHERE sender_id = @userId OR receiver_id = @userId OR is_group_chat = true
                  )
                  SELECT contact_id, message_body, timestamp, is_group_chat
                  FROM LastMsgs
                  WHERE rn = 1
                  ORDER BY timestamp DESC", conn))
            {
                cmd.Parameters.AddWithValue("@userId", userId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string contactId = reader["contact_id"].ToString();
                    if (seenIds.Contains(contactId)) continue;
                    seenIds.Add(contactId);

                    bool isGroup = reader.GetBoolean(reader.GetOrdinal("is_group_chat")) || contactId.StartsWith("gc_") || contactId.StartsWith("g");
                    string role = isGroup ? "Group" : (contactId.StartsWith("d") ? "Driver" : "Customer");
                    string name = isGroup ? (contactId == "gc_drivers" ? "Drivers Community GC" : contactId == "gc_customers" ? "Customers General Support" : $"Group {contactId}")
                                          : (contactId.StartsWith("d") ? $"Driver {contactId.TrimStart('d')}" : $"Customer {contactId}");

                    list.Add(new
                    {
                        id = contactId,
                        name = name,
                        role = role,
                        lastMessage = reader["message_body"].ToString(),
                        time = reader.GetDateTime(reader.GetOrdinal("timestamp")).ToString("h:mm tt"),
                        unreadCount = 0
                    });
                }
            }

            // 2. Query real users table for active drivers and customers if no messages exist yet
            await using (var userCmd = new NpgsqlCommand(
                @"SELECT user_id, full_name, role FROM users WHERE role != 'admin' ORDER BY full_name ASC LIMIT 10", conn))
            {
                await using var userReader = await userCmd.ExecuteReaderAsync();
                while (await userReader.ReadAsync())
                {
                    string uid = userReader["user_id"].ToString();
                    if (seenIds.Contains(uid)) continue;
                    seenIds.Add(uid);

                    string name = userReader["full_name"]?.ToString() ?? ("User " + uid);
                    string userRole = userReader["role"]?.ToString() ?? "";
                    string role = string.Equals(userRole, "driver", StringComparison.OrdinalIgnoreCase) ? "Driver" : "Customer";

                    list.Add(new
                    {
                        id = uid,
                        name = name,
                        role = role,
                        lastMessage = "No messages yet",
                        time = "",
                        unreadCount = 0
                    });
                }
            }

            // 3. Ensure standard Group Chat channels exist
            if (!seenIds.Contains("gc_drivers"))
            {
                list.Add(new { id = "gc_drivers", name = "Drivers Community GC", role = "Group", lastMessage = "Group Chat Channel", time = "", unreadCount = 0 });
            }
            if (!seenIds.Contains("gc_customers"))
            {
                list.Add(new { id = "gc_customers", name = "Customers General Support", role = "Group", lastMessage = "Group Chat Channel", time = "", unreadCount = 0 });
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