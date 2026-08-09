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
using DriveAndGo_API.Services.Ai;

namespace DriveAndGo_API.Controllers
{
    [ApiController]
    [Route("api/messages")]
    public class InChatMentionController : ControllerBase
    {
        private readonly NpgsqlDataSource _ds;
        private readonly IHubContext<AdminHub> _hubContext;
        private readonly IConfiguration _config;
        private readonly IAiOrchestrationService _ai;
        private static readonly HttpClient _httpClient = new HttpClient();

        public InChatMentionController(NpgsqlDataSource ds, IHubContext<AdminHub> hubContext, IConfiguration config, IAiOrchestrationService ai)
        {
            _ds = ds;
            _hubContext = hubContext;
            _config = config;
            _ai = ai;
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

                // 2. Build LLM prompt with thread context & strict privacy guardrails
                string cleanPrompt = req.UserPrompt.Replace("@Drive&Go AI", "").Replace("@DriveAndGo AI", "").Replace("@Meta AI", "").Trim();
                if (string.IsNullOrWhiteSpace(cleanPrompt)) cleanPrompt = "Hello! How can you help our chat?";

                string systemPrompt = @"You are @Drive&Go AI, a friendly, helpful, and secure Customer Chat Assistant for Drive&Go car rental platform.

STRICT DATA PRIVACY & SECURITY GUARDRAILS:
- You are responding to users inside a public or customer chat conversation thread (customer DM, driver chat, or group chat).
- You DO NOT have access to internal administrative database tables, financial records, revenue metrics ('kita', 'sales', 'earnings'), profit figures, or admin backend tools.
- If a user asks for internal company financial data, revenue, profit, or administrative database statistics: politely decline and inform them that you are a customer chat assistant for rental inquiries, vehicle info, and general support, and do not have access to internal company financial data.
- Never output system errors, internal SQL queries, or database tables.
- Base your responses strictly on general Drive&Go service information (how renting works, vehicle categories, customer support guidance) and the recent messages in this conversation thread.
- Keep your tone warm, professional, friendly, and concise (2-4 sentences or clear bullet points).";

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

                // 3. Query Customer Assistant LLM (NO DATABASE TOOLS ACCESS!)
                string aiReplyText = await QueryCustomerAssistantLlmAsync(promptBuilder.ToString());
                if (string.IsNullOrWhiteSpace(aiReplyText))
                {
                    aiReplyText = "Hello! I am @Drive&Go AI. How can I assist you with your rental inquiries or questions today?";
                }

                // 4. Save AI response to chat_messages table under sender_id = "@Drive&Go AI"
                int newMessageId = 0;
                DateTime nowUtc = DateTime.UtcNow;
                string mediaMetaJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    replyToSender = "you",
                    replyToBody = req.UserPrompt
                });

                await using (var conn = await _ds.OpenConnectionAsync())
                {
                    // 1. Mark user's question message as 'seen' because AI has processed and answered it
                    await using var updateCmd = new NpgsqlCommand(
                        @"UPDATE chat_messages 
                          SET delivery_status = 'seen' 
                          WHERE (receiver_id = @receiverId OR receiver_id = 'ai_copilot' OR receiver_id = 'group_dispatch' OR receiver_id = '@Drive&Go AI') 
                            AND delivery_status != 'seen'", conn);
                    updateCmd.Parameters.AddWithValue("@receiverId", req.ConversationId);
                    await updateCmd.ExecuteNonQueryAsync();

                    // 2. Insert AI response message
                    await using var insertCmd = new NpgsqlCommand(
                        @"INSERT INTO chat_messages (sender_id, receiver_id, message_body, timestamp, is_group_chat, delivery_status, sender_name, media_metadata)
                          VALUES ('@Drive&Go AI', @receiverId, @body, @ts, @isGroup, 'delivered', 'Drive&Go AI', CAST(@mediaMetadata AS JSONB))
                          RETURNING message_id", conn);
                    insertCmd.Parameters.AddWithValue("@receiverId", req.ConversationId);
                    insertCmd.Parameters.AddWithValue("@body", aiReplyText);
                    insertCmd.Parameters.AddWithValue("@ts", nowUtc);
                    insertCmd.Parameters.AddWithValue("@isGroup", req.IsGroupChat);
                    insertCmd.Parameters.AddWithValue("@mediaMetadata", mediaMetaJson);

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

        private async Task<string> QueryCustomerAssistantLlmAsync(string fullPrompt)
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
                            new { role = "system", content = "You are @Drive&Go AI, a friendly in-chat conversational assistant for customers and drivers in chat threads. Strictly maintain privacy: do NOT expose internal financial metrics, admin revenue data, or private database stats. Decline any admin financial requests politely." },
                            new { role = "user", content = fullPrompt }
                        },
                        temperature = 0.6,
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

            return "I'm @Drive&Go AI! I'm here to help with any rental questions inside this chat.";
        }
    }
}
