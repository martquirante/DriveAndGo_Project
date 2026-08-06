using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using DriveAndGo_API.Hubs;

namespace DriveAndGo_API.Controllers
{
    [ApiController]
    [Route("api/messages")]
    public class InChatMentionController : ControllerBase
    {
        private readonly NpgsqlDataSource _ds;
        private readonly IHubContext<AdminHub> _hubContext;
        private readonly IConfiguration _config;
        private static readonly HttpClient _httpClient = new HttpClient();

        public InChatMentionController(NpgsqlDataSource ds, IHubContext<AdminHub> hubContext, IConfiguration config)
        {
            _ds = ds;
            _hubContext = hubContext;
            _config = config;
        }

        public class MentionAiRequest
        {
            public string ConversationId { get; set; } = "";
            public string SenderId { get; set; } = "admin";
            public string UserPrompt { get; set; } = "";
            public bool IsGroupChat { get; set; } = false;
        }

        [HttpPost("mention-ai")]
        public async Task<IActionResult> MentionAi([FromBody] MentionAiRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.ConversationId) || string.IsNullOrWhiteSpace(req.UserPrompt))
                return BadRequest(new { Message = "conversationId and userPrompt are required." });

            try
            {
                // 1. Fetch last 10 messages from this specific conversation for context
                var contextMessages = new List<string>();
                await using (var conn = await _ds.OpenConnectionAsync())
                {
                    await using var cmd = new NpgsqlCommand(
                        @"SELECT sender_id, message_body
                          FROM chat_messages
                          WHERE (sender_id = @cid AND receiver_id = @sid)
                             OR (sender_id = @sid AND receiver_id = @cid)
                             OR (receiver_id = @cid AND is_group_chat = true)
                          ORDER BY message_id DESC
                          LIMIT 10", conn);
                    cmd.Parameters.AddWithValue("@cid", req.ConversationId);
                    cmd.Parameters.AddWithValue("@sid", req.SenderId);

                    await using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                    {
                        string sId = r["sender_id"].ToString() ?? "User";
                        string body = r["message_body"].ToString() ?? "";
                        if (!string.IsNullOrWhiteSpace(body))
                        {
                            contextMessages.Add($"{sId}: {body}");
                        }
                    }
                }
                contextMessages.Reverse(); // Chronological order

                // 2. Build LLM prompt with thread context
                string cleanPrompt = req.UserPrompt.Replace("@Drive&Go AI", "").Replace("@DriveAndGo AI", "").Trim();
                if (string.IsNullOrWhiteSpace(cleanPrompt)) cleanPrompt = "Hello! How can you help our chat?";

                string systemPrompt = "You are @Drive&Go AI, a helpful, friendly, and smart in-chat assistant inside a Drive&Go rental platform chat thread. Answer concisely (2-4 sentences max), use helpful emojis, and assist users directly in the context of their chat.";

                var promptBuilder = new StringBuilder();
                promptBuilder.AppendLine(systemPrompt);
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("RECENT CHAT THREAD HISTORY:");
                foreach (var line in contextMessages)
                {
                    promptBuilder.AppendLine(line);
                }
                promptBuilder.AppendLine();
                promptBuilder.AppendLine($"USER QUESTION (@Drive&Go AI): {cleanPrompt}");

                // 3. Query LLM (Groq / Gemini fallback)
                string aiReplyText = await QueryLlmAsync(promptBuilder.ToString());
                if (string.IsNullOrWhiteSpace(aiReplyText))
                {
                    aiReplyText = "I'm @Drive&Go AI! I'm currently standing by to assist with any questions about bookings, vehicles, or fleet operations in this chat.";
                }

                // 4. Save AI response to chat_messages table under sender_id = "@Drive&Go AI"
                int newMessageId = 0;
                DateTime nowUtc = DateTime.UtcNow;
                await using (var conn = await _ds.OpenConnectionAsync())
                {
                    await using var insertCmd = new NpgsqlCommand(
                        @"INSERT INTO chat_messages (sender_id, receiver_id, message_body, timestamp, is_group_chat, delivery_status, sender_name)
                          VALUES ('@Drive&Go AI', @receiverId, @body, @ts, @isGroup, 'delivered', 'Drive&Go AI')
                          RETURNING message_id", conn);
                    insertCmd.Parameters.AddWithValue("@receiverId", req.ConversationId);
                    insertCmd.Parameters.AddWithValue("@body", aiReplyText);
                    insertCmd.Parameters.AddWithValue("@ts", nowUtc);
                    insertCmd.Parameters.AddWithValue("@isGroup", req.IsGroupChat);

                    var idRes = await insertCmd.ExecuteScalarAsync();
                    if (idRes != null && idRes != DBNull.Value)
                    {
                        newMessageId = Convert.ToInt32(idRes);
                    }
                }

                // 5. Broadcast using ReceiveChatMessage (5 positional parameters)
                await _hubContext.Clients.All.SendAsync("ReceiveChatMessage", "@Drive&Go AI", req.ConversationId, aiReplyText, nowUtc.ToString("o"), newMessageId.ToString());

                return Ok(new
                {
                    messageId   = newMessageId,
                    senderId    = "@Drive&Go AI",
                    receiverId  = req.ConversationId,
                    messageBody = aiReplyText
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        private async Task<string> QueryLlmAsync(string fullPrompt)
        {
            string groqKey = Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? _config["GROQ_API_KEY"] ?? "";
            if (!string.IsNullOrWhiteSpace(groqKey))
            {
                try
                {
                    var reqObj = new
                    {
                        model = "llama-3.3-70b-versatile",
                        messages = new[]
                        {
                            new { role = "system", content = "You are @Drive&Go AI, a friendly in-chat assistant." },
                            new { role = "user", content = fullPrompt }
                        },
                        temperature = 0.7,
                        max_tokens = 300
                    };
                    using var httpReq = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
                    httpReq.Headers.Add("Authorization", $"Bearer {groqKey}");
                    httpReq.Content = new StringContent(JsonSerializer.Serialize(reqObj), Encoding.UTF8, "application/json");

                    using var res = await _httpClient.SendAsync(httpReq);
                    if (res.IsSuccessStatusCode)
                    {
                        string json = await res.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                    }
                }
                catch { }
            }

            return "I'm @Drive&Go AI! I'm here to help with any questions inside this chat.";
        }
    }
}
