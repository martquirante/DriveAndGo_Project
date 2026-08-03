using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using DriveAndGo_API.Hubs;
using DriveAndGo_API.Services.Ai;
using System.Text.Json;

namespace DriveAndGo_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MessagesController : ControllerBase
{
    private static string SanitizeNonTechText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        string lower = text.ToLowerInvariant();
        if (lower.Contains(".env") || lower.Contains("api key") || lower.Contains("groq") || lower.Contains("gemini api") || lower.Contains("quotaexhausted") || lower.Contains("rate limit") || lower.Contains("limitasyon sa sistema"))
        {
            return "Pasensya na po, pansamantalang hindi ma-access ang datos na iyan ngayon. Maaari niyo po itong subukan ulit sa susunod na minuto.";
        }
        return text;
    }
    private readonly NpgsqlDataSource _ds;
    private readonly IHubContext<AdminHub> _hubContext;
    private readonly IAiOrchestrationService _ai;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(NpgsqlDataSource ds, IHubContext<AdminHub> hubContext, IAiOrchestrationService ai, ILogger<MessagesController> logger)
    {
        _ds         = ds;
        _hubContext = hubContext;
        _ai         = ai;
        _logger     = logger;
    }

    // ══════════════════════════════════════════════════════════════════
    //  GET /api/messages?senderId=admin&receiverId=d1
    //  Returns full chat history with delivery_status for each message.
    //  Special case: receiverId=ai_copilot bypasses hidden_for / group-chat
    //  filters since AI messages are never group messages or hideable.
    // ══════════════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> GetChatHistory([FromQuery] string senderId, [FromQuery] string receiverId)
    {
        try
        {
            var list = new List<object>();
            await using var conn = await _ds.OpenConnectionAsync();

            // AI Copilot thread: simple bidirectional fetch — no group/hidden_for complexity
            bool isAiThread = string.Equals(receiverId, "ai_copilot", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(senderId,   "ai_copilot", StringComparison.OrdinalIgnoreCase);

            string sql = isAiThread
                ? @"SELECT message_id, sender_id, receiver_id, message_body,
                           timestamp, is_group_chat, delivery_status,
                           is_edited, edit_history, is_unsent, hidden_for, reactions
                    FROM   chat_messages
                    WHERE  (sender_id = @sid AND receiver_id = @rid)
                        OR (sender_id = @rid AND receiver_id = @sid)
                    ORDER  BY timestamp ASC"
                : @"SELECT message_id, sender_id, receiver_id, message_body,
                           timestamp, is_group_chat, delivery_status,
                           is_edited, edit_history, is_unsent, hidden_for, reactions
                    FROM   chat_messages
                    WHERE  ((sender_id = @sid AND receiver_id = @rid)
                         OR (sender_id = @rid AND receiver_id = @sid)
                         OR (receiver_id = @rid AND is_group_chat = true))
                      AND  NOT (hidden_for @> CAST(CONCAT('[""', @sid, '""]') AS JSONB))
                    ORDER  BY timestamp ASC";

            await using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@sid", senderId);
                cmd.Parameters.AddWithValue("@rid", receiverId);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new
                    {
                        messageId      = reader.GetInt32(reader.GetOrdinal("message_id")),
                        senderId       = reader["sender_id"].ToString(),
                        receiverId     = reader["receiver_id"].ToString(),
                        messageBody    = SanitizeNonTechText(reader["message_body"].ToString()),
                        timestamp      = reader.GetDateTime(reader.GetOrdinal("timestamp")),
                        isGroupChat    = reader.GetBoolean(reader.GetOrdinal("is_group_chat")),
                        deliveryStatus = reader["delivery_status"].ToString(),
                        isEdited       = reader.GetBoolean(reader.GetOrdinal("is_edited")),
                        editHistory    = reader["edit_history"].ToString(),
                        isUnsent       = reader.GetBoolean(reader.GetOrdinal("is_unsent")),
                        reactions      = reader["reactions"].ToString()
                    });
                }
            }

            // Fallback: If chat_messages has 0 rows for AI thread, fetch from ai_copilot_messages
            if (list.Count == 0 && isAiThread)
            {
                await using var aiCmd = new NpgsqlCommand(@"
                    SELECT copilot_msg_id AS message_id,
                           CASE WHEN llm_role = 'user' THEN 'admin' ELSE 'ai_copilot' END AS sender_id,
                           CASE WHEN llm_role = 'user' THEN 'ai_copilot' ELSE 'admin' END AS receiver_id,
                           content AS message_body,
                           sent_at AS timestamp
                    FROM ai_copilot_messages
                    WHERE session_id = (SELECT session_id FROM ai_copilot_sessions ORDER BY updated_at DESC LIMIT 1)
                      AND llm_role != 'system'
                    ORDER BY sent_at ASC", conn);

                await using var aiReader = await aiCmd.ExecuteReaderAsync();
                while (await aiReader.ReadAsync())
                {
                    list.Add(new
                    {
                        messageId      = aiReader.GetInt64(aiReader.GetOrdinal("message_id")),
                        senderId       = aiReader["sender_id"].ToString(),
                        receiverId     = aiReader["receiver_id"].ToString(),
                        messageBody    = SanitizeNonTechText(aiReader["message_body"].ToString()),
                        timestamp      = aiReader.GetDateTime(aiReader.GetOrdinal("timestamp")),
                        isGroupChat    = false,
                        deliveryStatus = "seen",
                        isEdited       = false,
                        editHistory    = "[]",
                        isUnsent       = false,
                        reactions      = "{}"
                    });
                }
            }

            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  POST /api/messages
    //  Saves with delivery_status = 'sent'.
    //  Broadcasts ReceiveChatMessage (with messageId) via SignalR.
    // ══════════════════════════════════════════════════════════════════
    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] MessageRequest req)
    {
        try
        {
            await using var conn = await _ds.OpenConnectionAsync();
            await using var cmd  = new NpgsqlCommand(
                @"INSERT INTO chat_messages
                    (sender_id, receiver_id, message_body, timestamp, is_group_chat, delivery_status)
                  VALUES (@sid, @rid, @body, NOW(), @group, 'sent')
                  RETURNING message_id, timestamp", conn);

            cmd.Parameters.AddWithValue("@sid",   req.SenderId);
            cmd.Parameters.AddWithValue("@rid",   req.ReceiverId);
            cmd.Parameters.AddWithValue("@body",  req.MessageBody);
            cmd.Parameters.AddWithValue("@group", req.IsGroupChat);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                int      messageId = reader.GetInt32(0);
                DateTime ts        = reader.GetDateTime(1);

                // Broadcast so recipients can render the incoming message in real-time
                // and can immediately ACK delivery (POST /api/messages/{id}/delivered)
                await _hubContext.Clients.All.SendAsync(
                    "ReceiveChatMessage",
                    req.SenderId,
                    req.ReceiverId,
                    req.MessageBody,
                    ts.ToString("o"),
                    messageId.ToString());   // ← now includes messageId so client can ACK

                // If message is addressed to AI Copilot thread, trigger AI Orchestration Service in background
                if (string.Equals(req.ReceiverId, "ai_copilot", StringComparison.OrdinalIgnoreCase))
                {
                    string userMessage = req.MessageBody;
                    string senderId = req.SenderId;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var aiResponse = await _ai.ChatAsync(1, 1, userMessage);
                            string aiPayload = aiResponse.Text;
                            if (!string.IsNullOrEmpty(aiResponse.UiComponent) && aiResponse.UiComponent != "Text Only")
                            {
                                try
                                {
                                    aiPayload = JsonSerializer.Serialize(new
                                    {
                                        text = aiResponse.Text,
                                        ui_component = aiResponse.UiComponent,
                                        data = aiResponse.Data
                                    });
                                }
                                catch { }
                            }

                            await _hubContext.Clients.All.SendAsync(
                                "ReceiveChatMessage",
                                "ai_copilot",
                                senderId,
                                aiPayload,
                                DateTime.UtcNow.ToString("o"),
                                aiResponse.MessageId.ToString());
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to execute AI orchestration for incoming chat message.");
                        }
                    });
                }

                return Ok(new
                {
                    Message        = "Message sent.",
                    MessageId      = messageId,
                    Timestamp      = ts,
                    DeliveryStatus = "sent"
                });
            }
            return BadRequest(new { Message = "Failed to store message." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  POST /api/messages/{id}/delivered
    //  Called by the recipient's device/app the moment it receives
    //  the SignalR "ReceiveChatMessage" event.
    //  → Updates DB status to 'delivered'
    //  → Pushes SignalR "MessageStatusChanged" back to the sender
    // ══════════════════════════════════════════════════════════════════
    [HttpPost("{id:int}/delivered")]
    public async Task<IActionResult> MarkDelivered(int id)
    {
        try
        {
            await using var conn = await _ds.OpenConnectionAsync();

            // Fetch sender/receiver so we can push status back to sender
            string? senderId = null, receiverId = null;
            await using (var fetchCmd = new NpgsqlCommand(
                "SELECT sender_id, receiver_id FROM chat_messages WHERE message_id = @id", conn))
            {
                fetchCmd.Parameters.AddWithValue("@id", id);
                await using var r = await fetchCmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    senderId   = r["sender_id"].ToString();
                    receiverId = r["receiver_id"].ToString();
                }
            }

            if (senderId == null)
                return NotFound(new { Message = $"Message {id} not found." });

            // Only advance forward: sent → delivered (never downgrade)
            await using var updateCmd = new NpgsqlCommand(
                @"UPDATE chat_messages
                  SET    delivery_status = 'delivered'
                  WHERE  message_id = @id
                    AND  delivery_status = 'sent'", conn);
            updateCmd.Parameters.AddWithValue("@id", id);
            int rows = await updateCmd.ExecuteNonQueryAsync();

            if (rows > 0)
            {
                // Push real-time status update to the original sender
                await _hubContext.Clients.All.SendAsync(
                    "MessageStatusChanged",
                    id.ToString(),
                    "delivered",
                    senderId,
                    receiverId);
            }

            return Ok(new { MessageId = id, DeliveryStatus = "delivered" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  POST /api/messages/{id}/seen
    //  Called when the recipient OPENS the conversation thread.
    //  → Marks ALL unseen messages in that thread as 'seen'
    //  → Pushes "MessageStatusChanged" for every affected message
    //  Body: { "viewerId": "d1" }   (who is opening the conversation)
    // ══════════════════════════════════════════════════════════════════
    [HttpPost("{id:int}/seen")]
    public async Task<IActionResult> MarkSeen(int id, [FromBody] SeenRequest req)
    {
        try
        {
            await using var conn = await _ds.OpenConnectionAsync();

            // Identify the thread: find all messages sent TO the viewer
            // in the same conversation that are not yet seen
            await using var listCmd = new NpgsqlCommand(
                @"UPDATE chat_messages
                  SET    delivery_status = 'seen'
                  WHERE  receiver_id = @viewer
                    AND  (sender_id = (SELECT sender_id FROM chat_messages WHERE message_id = @id)
                          OR
                          (is_group_chat = true AND receiver_id = (SELECT receiver_id FROM chat_messages WHERE message_id = @id)))
                    AND  delivery_status != 'seen'
                  RETURNING message_id, sender_id, receiver_id", conn);

            listCmd.Parameters.AddWithValue("@viewer", req.ViewerId);
            listCmd.Parameters.AddWithValue("@id",     id);

            var updatedMessages = new List<(int msgId, string senderId, string receiverId)>();
            await using (var r = await listCmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                {
                    updatedMessages.Add((
                        r.GetInt32(0),
                        r["sender_id"].ToString()!,
                        r["receiver_id"].ToString()!
                    ));
                }
            }

            // Push "MessageStatusChanged" (seen) for every affected message
            foreach (var (msgId, sndr, rcvr) in updatedMessages)
            {
                await _hubContext.Clients.All.SendAsync(
                    "MessageStatusChanged",
                    msgId.ToString(),
                    "seen",
                    sndr,
                    rcvr);
            }

            return Ok(new
            {
                MarkedSeen = updatedMessages.Count,
                MessageIds = updatedMessages.Select(m => m.msgId).ToArray()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  PUT /api/messages/{id} (Edit)
    // ══════════════════════════════════════════════════════════════════
    [HttpPut("{id:int}")]
    public async Task<IActionResult> EditMessage(int id, [FromBody] EditMessageRequest req)
    {
        try
        {
            await using var conn = await _ds.OpenConnectionAsync();

            // 1. Fetch current message and edit_history
            string currentBody = "";
            string editHistoryJson = "[]";
            string receiverId = "";
            await using (var fetchCmd = new NpgsqlCommand("SELECT message_body, edit_history, receiver_id FROM chat_messages WHERE message_id = @id", conn))
            {
                fetchCmd.Parameters.AddWithValue("@id", id);
                await using var r = await fetchCmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    currentBody = r["message_body"].ToString() ?? "";
                    editHistoryJson = r["edit_history"].ToString() ?? "[]";
                    receiverId = r["receiver_id"].ToString() ?? "";
                }
                else return NotFound("Message not found");
            }

            // 2. Append to edit_history
            var history = JsonSerializer.Deserialize<List<object>>(editHistoryJson) ?? new List<object>();
            history.Add(new { text = currentBody, edited_at = DateTime.UtcNow });
            string newHistoryJson = JsonSerializer.Serialize(history);

            // 3. Update message
            await using var updateCmd = new NpgsqlCommand(
                @"UPDATE chat_messages
                  SET message_body = @newBody,
                      is_edited = true,
                      edit_history = CAST(@history AS JSONB)
                  WHERE message_id = @id", conn);
            updateCmd.Parameters.AddWithValue("@newBody", req.NewText);
            updateCmd.Parameters.AddWithValue("@history", newHistoryJson);
            updateCmd.Parameters.AddWithValue("@id", id);
            await updateCmd.ExecuteNonQueryAsync();

            // 4. Broadcast
            await _hubContext.Clients.All.SendAsync("MessageEdited", id.ToString(), req.NewText, newHistoryJson, receiverId);

            return Ok(new { Message = "Message edited" });
        }
        catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
    }

    // ══════════════════════════════════════════════════════════════════
    //  DELETE /api/messages/{id}/unsend
    // ══════════════════════════════════════════════════════════════════
    [HttpDelete("{id:int}/unsend")]
    public async Task<IActionResult> UnsendMessage(int id)
    {
        try
        {
            await using var conn = await _ds.OpenConnectionAsync();
            string receiverId = "";
            await using (var fetchCmd = new NpgsqlCommand("SELECT receiver_id FROM chat_messages WHERE message_id = @id", conn))
            {
                fetchCmd.Parameters.AddWithValue("@id", id);
                var res = await fetchCmd.ExecuteScalarAsync();
                if (res != null) receiverId = res.ToString() ?? "";
            }

            await using var updateCmd = new NpgsqlCommand(
                @"UPDATE chat_messages
                  SET is_unsent = true, message_body = ''
                  WHERE message_id = @id", conn);
            updateCmd.Parameters.AddWithValue("@id", id);
            await updateCmd.ExecuteNonQueryAsync();

            await _hubContext.Clients.All.SendAsync("MessageUnsent", id.ToString(), receiverId);

            return Ok(new { Message = "Message unsent" });
        }
        catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
    }

    // ══════════════════════════════════════════════════════════════════
    //  POST /api/messages/{id}/remove
    // ══════════════════════════════════════════════════════════════════
    [HttpPost("{id:int}/remove")]
    public async Task<IActionResult> RemoveMessageForUser(int id, [FromBody] RemoveMessageRequest req)
    {
        try
        {
            await using var conn = await _ds.OpenConnectionAsync();
            
            // fetch current hidden_for array
            string hiddenForJson = "[]";
            await using (var fetchCmd = new NpgsqlCommand("SELECT hidden_for FROM chat_messages WHERE message_id = @id", conn))
            {
                fetchCmd.Parameters.AddWithValue("@id", id);
                var res = await fetchCmd.ExecuteScalarAsync();
                if (res != null) hiddenForJson = res.ToString() ?? "[]";
            }

            var hiddenList = JsonSerializer.Deserialize<List<string>>(hiddenForJson) ?? new List<string>();
            if (!hiddenList.Contains(req.UserId))
            {
                hiddenList.Add(req.UserId);
                string newHiddenJson = JsonSerializer.Serialize(hiddenList);
                
                await using var updateCmd = new NpgsqlCommand(
                    "UPDATE chat_messages SET hidden_for = CAST(@hidden AS JSONB) WHERE message_id = @id", conn);
                updateCmd.Parameters.AddWithValue("@hidden", newHiddenJson);
                updateCmd.Parameters.AddWithValue("@id", id);
                await updateCmd.ExecuteNonQueryAsync();
            }

            return Ok(new { Message = "Message removed for user" });
        }
        catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
    }

    // ══════════════════════════════════════════════════════════════════
    //  POST /api/messages/{id}/react
    // ══════════════════════════════════════════════════════════════════
    [HttpPost("{id:int}/react")]
    public async Task<IActionResult> ReactToMessage(int id, [FromBody] ReactMessageRequest req)
    {
        try
        {
            await using var conn = await _ds.OpenConnectionAsync();
            
            string reactionsJson = "{}";
            string receiverId = "";
            await using (var fetchCmd = new NpgsqlCommand("SELECT reactions, receiver_id FROM chat_messages WHERE message_id = @id", conn))
            {
                fetchCmd.Parameters.AddWithValue("@id", id);
                await using var r = await fetchCmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    reactionsJson = r["reactions"].ToString() ?? "{}";
                    receiverId = r["receiver_id"].ToString() ?? "";
                }
            }

            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(reactionsJson) ?? new Dictionary<string, string>();
            if (req == null || string.IsNullOrWhiteSpace(req.Emoji))
            {
                if (req?.UserId != null) dict.Remove(req.UserId);
                else dict.Remove("admin");
            }
            else
            {
                dict[req.UserId] = req.Emoji;
            }

            string newReactionsJson = JsonSerializer.Serialize(dict);

            await using var updateCmd = new NpgsqlCommand(
                "UPDATE chat_messages SET reactions = CAST(@reactions AS JSONB) WHERE message_id = @id", conn);
            updateCmd.Parameters.AddWithValue("@reactions", newReactionsJson);
            updateCmd.Parameters.AddWithValue("@id", id);
            await updateCmd.ExecuteNonQueryAsync();

            await _hubContext.Clients.All.SendAsync("MessageReactionChanged", id.ToString(), newReactionsJson, receiverId);

            return Ok(new { Message = "Reaction updated" });
        }
        catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
    }

    // ══════════════════════════════════════════════════════════════════
    //  DELETE /api/messages/{id}/react
    // ══════════════════════════════════════════════════════════════════
    [HttpDelete("{id:int}/react")]
    public async Task<IActionResult> DeleteReaction(int id, [FromQuery] string userId = "admin")
    {
        try
        {
            await using var conn = await _ds.OpenConnectionAsync();
            
            string reactionsJson = "{}";
            string receiverId = "";
            await using (var fetchCmd = new NpgsqlCommand("SELECT reactions, receiver_id FROM chat_messages WHERE message_id = @id", conn))
            {
                fetchCmd.Parameters.AddWithValue("@id", id);
                await using var r = await fetchCmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    reactionsJson = r["reactions"].ToString() ?? "{}";
                    receiverId = r["receiver_id"].ToString() ?? "";
                }
            }

            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(reactionsJson) ?? new Dictionary<string, string>();
            dict.Remove(string.IsNullOrWhiteSpace(userId) ? "admin" : userId);

            string newReactionsJson = JsonSerializer.Serialize(dict);

            await using var updateCmd = new NpgsqlCommand(
                "UPDATE chat_messages SET reactions = CAST(@reactions AS JSONB) WHERE message_id = @id", conn);
            updateCmd.Parameters.AddWithValue("@reactions", newReactionsJson);
            updateCmd.Parameters.AddWithValue("@id", id);
            await updateCmd.ExecuteNonQueryAsync();

            await _hubContext.Clients.All.SendAsync("MessageReactionChanged", id.ToString(), newReactionsJson, receiverId);

            return Ok(new { Message = "Reaction removed" });
        }
        catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
    }

    // ══════════════════════════════════════════════════════════════════
    //  POST /api/messages/forward
    // ══════════════════════════════════════════════════════════════════
    [HttpPost("forward")]
    public async Task<IActionResult> ForwardMessage([FromBody] ForwardMessageRequest req)
    {
        try
        {
            await using var conn = await _ds.OpenConnectionAsync();
            
            // fetch original content
            string content = "";
            await using (var fetchCmd = new NpgsqlCommand("SELECT message_body FROM chat_messages WHERE message_id = @id", conn))
            {
                fetchCmd.Parameters.AddWithValue("@id", req.OriginalMessageId);
                var res = await fetchCmd.ExecuteScalarAsync();
                if (res != null) content = res.ToString() ?? "";
                else return NotFound("Original message not found");
            }

            // create new message
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO chat_messages (sender_id, receiver_id, message_body, timestamp, is_group_chat, delivery_status)
                  VALUES (@sid, @rid, @body, NOW(), false, 'sent') RETURNING message_id, timestamp", conn);
            cmd.Parameters.AddWithValue("@sid", req.SenderId);
            cmd.Parameters.AddWithValue("@rid", req.NewReceiverId);
            cmd.Parameters.AddWithValue("@body", content);
            
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                int newId = reader.GetInt32(0);
                DateTime ts = reader.GetDateTime(1);
                
                await _hubContext.Clients.All.SendAsync(
                    "ReceiveChatMessage", req.SenderId, req.NewReceiverId, content, ts.ToString("o"), newId.ToString());

                return Ok(new { MessageId = newId, Timestamp = ts });
            }
            return BadRequest("Failed to forward");
        }
        catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
    }

    // ══════════════════════════════════════════════════════════════════
    //  GET /api/messages/conversations?userId=admin
    //  Includes live unreadCount per thread from the database.
    // ══════════════════════════════════════════════════════════════════
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations([FromQuery] string userId)
    {
        try
        {
            var list    = new List<object>();
            var seenIds = new HashSet<string>();

            await using var conn = await _ds.OpenConnectionAsync();

            // 0. Pre-fetch all non-admin users in memory to resolve any indexed IDs like c1, d1 safely
            var allUsers = new List<(int userId, string fullName, string role)>();
            await using (var uCmd = new NpgsqlCommand(
                "SELECT user_id, full_name, role FROM users WHERE role != 'admin' ORDER BY user_id ASC", conn))
            {
                await using var uReader = await uCmd.ExecuteReaderAsync();
                while (await uReader.ReadAsync())
                {
                    allUsers.Add((
                        uReader.GetInt32(0),
                        uReader["full_name"]?.ToString() ?? "",
                        uReader["role"]?.ToString() ?? ""
                    ));
                }
            }

            // 1. Conversations that have at least one message, with live unread count and real user names
            await using (var cmd = new NpgsqlCommand(
                @"WITH LastMsgs AS (
                      SELECT
                          CASE WHEN sender_id = @userId THEN receiver_id ELSE sender_id END AS contact_id,
                          message_body,
                          timestamp,
                          is_group_chat,
                          delivery_status,
                          ROW_NUMBER() OVER (
                              PARTITION BY CASE WHEN sender_id = @userId THEN receiver_id ELSE sender_id END
                              ORDER BY timestamp DESC
                          ) AS rn
                      FROM chat_messages
                      WHERE sender_id = @userId OR receiver_id = @userId OR is_group_chat = true
                  ),
                  UnreadCounts AS (
                      SELECT
                          sender_id AS contact_id,
                          COUNT(*) AS unread
                      FROM chat_messages
                      WHERE receiver_id = @userId
                        AND delivery_status != 'seen'
                      GROUP BY sender_id
                  )
                  SELECT lm.contact_id, lm.message_body, lm.timestamp, lm.is_group_chat, lm.delivery_status,
                         COALESCE(uc.unread, 0) AS unread_count,
                         u.full_name, u.role AS user_role
                  FROM   LastMsgs lm
                  LEFT JOIN UnreadCounts uc ON uc.contact_id = lm.contact_id
                  LEFT JOIN users u ON (
                      (u.user_id::text = lm.contact_id OR u.user_id::text = NULLIF(REGEXP_REPLACE(lm.contact_id, '[^0-9]', '', 'g'), ''))
                      AND u.role != 'admin'
                  )
                  WHERE  lm.rn = 1
                  ORDER  BY lm.timestamp DESC", conn))
            {
                cmd.Parameters.AddWithValue("@userId", userId);
                await using var reader = await cmd.ExecuteReaderAsync();
                var rawItems = new List<(string contactId, string msgBody, DateTime ts, bool isGroup, string delStatus, int unread, string? fullName, string? userRole)>();

                while (await reader.ReadAsync())
                {
                    string cId = reader["contact_id"].ToString()!;
                    if (seenIds.Contains(cId)) continue;
                    seenIds.Add(cId);

                    rawItems.Add((
                        cId,
                        reader["message_body"].ToString()!,
                        reader.GetDateTime(reader.GetOrdinal("timestamp")),
                        reader.GetBoolean(reader.GetOrdinal("is_group_chat")),
                        reader["delivery_status"].ToString()!,
                        reader.IsDBNull(reader.GetOrdinal("unread_count")) ? 0 : Convert.ToInt32(reader["unread_count"]),
                        reader.IsDBNull(reader.GetOrdinal("full_name")) ? null : reader["full_name"].ToString(),
                        reader.IsDBNull(reader.GetOrdinal("user_role")) ? null : reader["user_role"].ToString()
                    ));
                }

                foreach (var item in rawItems)
                {
                    string contactId = item.contactId;
                    string? fullName = item.fullName;
                    string? userRole = item.userRole;

                    bool isGroup = item.isGroup
                                 || contactId.StartsWith("gc_")
                                 || contactId.StartsWith("g");

                    // Pure in-memory fallback for indexed IDs like "c1", "d1" if direct user_id JOIN returned null
                    if (string.IsNullOrWhiteSpace(fullName) && !isGroup && !string.Equals(contactId, "ai_copilot", StringComparison.OrdinalIgnoreCase))
                    {
                        string targetRole = contactId.StartsWith("d") ? "driver" : "customer";
                        string numStr = System.Text.RegularExpressions.Regex.Replace(contactId, @"[^\d]", "");
                        if (int.TryParse(numStr, out int index) && index > 0)
                        {
                            var matchedUsers = allUsers.Where(u => string.Equals(u.role, targetRole, StringComparison.OrdinalIgnoreCase)).ToList();
                            if (index <= matchedUsers.Count)
                            {
                                fullName = matchedUsers[index - 1].fullName;
                                userRole = matchedUsers[index - 1].role;
                            }
                            else if (matchedUsers.Count > 0)
                            {
                                fullName = matchedUsers[0].fullName;
                                userRole = matchedUsers[0].role;
                            }
                        }
                    }

                    string role;
                    string name;

                    if (string.Equals(contactId, "ai_copilot", StringComparison.OrdinalIgnoreCase))
                    {
                        name = "Drive\u0026Go AI";
                        role = "AI COPILOT";
                    }
                    else if (isGroup)
                    {
                        name = contactId == "gc_drivers"   ? "Drivers Community GC"
                             : contactId == "gc_customers" ? "Customers General Support"
                             : $"Group {contactId}";
                        role = "Group";
                    }
                    else
                    {
                        role = !string.IsNullOrEmpty(userRole)
                            ? (userRole.Equals("driver", StringComparison.OrdinalIgnoreCase) ? "Driver" : "Customer")
                            : (contactId.StartsWith("d") ? "Driver" : "Customer");

                        name = !string.IsNullOrWhiteSpace(fullName)
                            ? fullName
                            : (contactId.StartsWith("d") ? $"Driver {contactId.TrimStart('d')}"
                              : $"Customer {contactId.TrimStart('c')}");
                    }

                    list.Add(new
                    {
                        id             = contactId,
                        name,
                        role,
                        lastMessage    = item.msgBody,
                        time           = item.ts.ToString("h:mm tt"),
                        deliveryStatus = item.delStatus,
                        unreadCount    = item.unread
                    });
                }
            }

            // 3. Standard Group Chat channels
            if (!seenIds.Contains("gc_drivers"))
                list.Add(new { id = "gc_drivers",   name = "Drivers Community GC",
                               role = "Group", lastMessage = "Group Chat Channel",
                               time = "", deliveryStatus = "sent", unreadCount = 0 });

            if (!seenIds.Contains("gc_customers"))
                list.Add(new { id = "gc_customers", name = "Customers General Support",
                               role = "Group", lastMessage = "Group Chat Channel",
                               time = "", deliveryStatus = "sent", unreadCount = 0 });

            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  POST /api/messages/thread/{contactId}/seen
    //  Bulk-marks ALL unseen messages from contactId → viewerId as seen.
    //  No need to know a specific messageId — just pass the contactId.
    //  Body: { "viewerId": "admin" }
    // ══════════════════════════════════════════════════════════════════
    [HttpPost("thread/{contactId}/seen")]
    public async Task<IActionResult> MarkThreadSeen(string contactId, [FromBody] SeenRequest req)
    {
        try
        {
            await using var conn = await _ds.OpenConnectionAsync();

            // Update all unread messages sent by contactId to viewerId in one query
            await using var updateCmd = new NpgsqlCommand(
                @"UPDATE chat_messages
                  SET    delivery_status = 'seen'
                  WHERE  sender_id   = @contactId
                    AND  receiver_id = @viewer
                    AND  delivery_status != 'seen'
                  RETURNING message_id, sender_id, receiver_id", conn);

            updateCmd.Parameters.AddWithValue("@contactId", contactId);
            updateCmd.Parameters.AddWithValue("@viewer",    req.ViewerId);

            var updatedMessages = new List<(int msgId, string senderId, string receiverId)>();
            await using (var r = await updateCmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                {
                    updatedMessages.Add((
                        r.GetInt32(0),
                        r["sender_id"].ToString()!,
                        r["receiver_id"].ToString()!
                    ));
                }
            }

            // Push "MessageStatusChanged" (seen) for every affected message
            // so the sender's UI updates their bubble icon to the seen avatar
            foreach (var (msgId, sndr, rcvr) in updatedMessages)
            {
                await _hubContext.Clients.All.SendAsync(
                    "MessageStatusChanged",
                    msgId.ToString(),
                    "seen",
                    sndr,
                    rcvr);
            }

            return Ok(new
            {
                MarkedSeen = updatedMessages.Count,
                MessageIds = updatedMessages.Select(m => m.msgId).ToArray()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = ex.Message });
        }
    }
}

// ══════════════════════════════════════════════════════════════════════
//  REQUEST DTOs
// ══════════════════════════════════════════════════════════════════════
public class MessageRequest
{
    public string SenderId    { get; set; } = string.Empty;
    public string ReceiverId  { get; set; } = string.Empty;
    public string MessageBody { get; set; } = string.Empty;
    public bool   IsGroupChat { get; set; } = false;
}

/// <summary>Used by POST /api/messages/{id}/seen to identify who opened the chat.</summary>
public class SeenRequest
{
    /// <summary>The user ID of the person who opened and read the messages.</summary>
    public string ViewerId { get; set; } = string.Empty;
}

public class EditMessageRequest
{
    public string NewText { get; set; } = string.Empty;
}

public class RemoveMessageRequest
{
    public string UserId { get; set; } = string.Empty;
}

public class ReactMessageRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
}

public class ForwardMessageRequest
{
    public int OriginalMessageId { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string NewReceiverId { get; set; } = string.Empty;
}