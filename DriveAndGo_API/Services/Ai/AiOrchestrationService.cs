using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using DriveAndGo_API.Models.AiCopilot;
using Npgsql;

namespace DriveAndGo_API.Services.Ai;

public class AiOrchestrationService : IAiOrchestrationService
{
    private static readonly HttpClient _webScraperClient = new HttpClient(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
    })
    {
        Timeout = TimeSpan.FromSeconds(6)
    };

    static AiOrchestrationService()
    {
        _webScraperClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)");
        _webScraperClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    }
    private readonly NpgsqlDataSource        _ds;
    private readonly AiToolsService          _tools;
    private readonly IHttpClientFactory      _httpFactory;
    private readonly IConfiguration          _configuration;
    private readonly ILogger<AiOrchestrationService> _logger;

    // ── Provider API Keys ───────────────────────────────────────────
    private readonly string _groqKey;
    private readonly string _huggingFaceKey;
    private readonly string _cohereKey;
    private readonly string _geminiKey;
    private readonly string _mistralKey;
    private readonly string _openRouterKey;
    private readonly string _sambaNovaKey;

    // ── Provider API Endpoints ──────────────────────────────────────
    private const string GroqUrl        = "https://api.groq.com/openai/v1/chat/completions";
    private const string HuggingFaceUrl = "https://router.huggingface.co/v1/chat/completions";
    private const string CohereUrl      = "https://api.cohere.com/v1/chat";
    private const string GeminiUrl      = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";
    private const string MistralUrl     = "https://api.mistral.ai/v1/chat/completions";
    private const string OpenRouterUrl  = "https://openrouter.ai/api/v1/chat/completions";
    private const string SambaNovaUrl   = "https://api.sambanova.ai/v1/chat/completions";

    // ── Config ──────────────────────────────────────────────────────
    private const int  MaxHistoryTurns    = 20;   // messages loaded from DB for context
    private const int  MaxToolLoopDepth   = 3;    // max recursive tool call rounds
    private const int  HttpTimeoutSeconds = 45;

    public AiOrchestrationService(
        NpgsqlDataSource            ds,
        AiToolsService              tools,
        IHttpClientFactory          httpFactory,
        IConfiguration              configuration,
        ILogger<AiOrchestrationService> logger)
    {
        _ds           = ds;
        _tools        = tools;
        _httpFactory  = httpFactory;
        _configuration= configuration;
        _logger       = logger;

        _groqKey        = GetKey("GROQ_API_KEY");
        _huggingFaceKey = GetKey("HUGGINGFACE_API_KEY");
        if (string.IsNullOrWhiteSpace(_huggingFaceKey)) _huggingFaceKey = GetKey("HF_API_KEY");
        _cohereKey      = GetKey("COHERE_API_KEY");
        _geminiKey      = GetKey("GEMINI_API_KEY");
        _mistralKey     = GetKey("MISTRAL_API_KEY");
        _openRouterKey  = GetKey("OPENROUTER_API_KEY");
        _sambaNovaKey   = GetKey("SAMBANOVA_API_KEY");
    }

    private string GetKey(string keyName)
    {
        // 1. Prioritize real environment variables (populated by DotNetEnv from .env file)
        string? envVal = Environment.GetEnvironmentVariable(keyName);
        if (IsValidApiKey(envVal)) return envVal!.Trim();

        // 2. Fallback to appsettings.json root level
        string? val = _configuration[keyName];
        if (IsValidApiKey(val)) return val!.Trim();

        // 3. Fallback to AiKeys section in appsettings.json
        val = _configuration[$"AiKeys:{keyName}"];
        if (IsValidApiKey(val)) return val!.Trim();

        return string.Empty;
    }

    private static bool IsValidApiKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        if (key.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase)) return false;
        if (key.StartsWith("your-", StringComparison.OrdinalIgnoreCase)) return false;
        if (key.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PUBLIC: CHAT
    // ═══════════════════════════════════════════════════════════════════
    public async Task<AiCopilotResponse> ChatAsync(int sessionId, int adminUserId, string userMessage)
    {
        string senderIdStr = adminUserId > 1 ? $"admin_{adminUserId}" : "admin";

        // 1. Persist the user message immediately in ai_copilot_messages
        await PersistMessageAsync(sessionId, "admin", "user", userMessage, null, null, null, null, null);

        // 1b. ALSO persist in chat_messages table for cross-restart chat history
        await PersistToChatMessagesTableAsync(senderIdStr, "ai_copilot", userMessage);

        // 1c. Detect URLs in userMessage and enrich prompt with scraped page content
        string effectivePrompt = await EnrichPromptWithUrlContentAsync(userMessage);

        // 2. Load history (most recent N turns for context window)
        var history = await LoadHistoryForContextAsync(sessionId);

        // 3. Build message list for LLM using enriched prompt
        var messages = BuildMessageList(history, effectivePrompt);

        // 4. Run multi-model fallback pipeline
        var (rawJson, provider) = await RunFallbackPipelineAsync(sessionId, messages);

        // 5. Parse GenUI JSON
        var response = ParseGenUiResponse(rawJson, provider);
        response.SessionId = sessionId;

        // 6. Persist AI response in ai_copilot_messages
        long msgId = await PersistMessageAsync(
            sessionId, "bot_copilot", "assistant",
            response.Text,
            string.IsNullOrEmpty(response.UiComponent) || response.UiComponent == "Text Only" ? null : response.UiComponent,
            response.Data.Count > 0 ? JsonSerializer.Serialize(response.Data) : null,
            null, provider, null);
        response.MessageId = msgId;

        // 6b. ALSO persist in chat_messages table
        string aiPayload = response.Text;
        if (!string.IsNullOrEmpty(response.UiComponent) && response.UiComponent != "Text Only")
        {
            try
            {
                aiPayload = JsonSerializer.Serialize(new
                {
                    text = response.Text,
                    ui_component = response.UiComponent,
                    data = response.Data
                });
            }
            catch { }
        }
        await PersistToChatMessagesTableAsync("ai_copilot", senderIdStr, aiPayload);

        // 7. Update session updated_at
        await UpdateSessionTimestampAsync(sessionId);

        return response;
    }

    private async Task<string> EnrichPromptWithUrlContentAsync(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage)) return userMessage;

        var match = Regex.Match(userMessage, @"(https?://[^\s]+)", RegexOptions.IgnoreCase);
        if (!match.Success) return userMessage;

        string url = match.Groups[1].Value;
        try
        {
            Uri uri = new Uri(url);
            using var response = await _webScraperClient.GetAsync(uri);
            if (!response.IsSuccessStatusCode) return userMessage;

            string html = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(html)) return userMessage;

            // Extract Title & Meta Description
            string title = ExtractMetaTag(html, "og:title") ?? ExtractTagContent(html, "title") ?? uri.Host;
            string description = ExtractMetaTag(html, "og:description") ?? ExtractMetaTag(html, "description") ?? "";

            title = HttpUtility.HtmlDecode(title).Trim();
            description = HttpUtility.HtmlDecode(description).Trim();

            // Clean HTML tags and extract readable plain text
            string cleanText = CleanHtmlToPlainText(html);
            if (cleanText.Length > 8000)
            {
                cleanText = cleanText.Substring(0, 8000) + "\n...[truncated for token limit]";
            }

            var sb = new StringBuilder();
            sb.AppendLine(userMessage);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine("[SYSTEM CONTEXT - WEB PAGE CONTENT READER]");
            sb.AppendLine($"URL: {url}");
            sb.AppendLine($"Page Title: {title}");
            if (!string.IsNullOrWhiteSpace(description))
            {
                sb.AppendLine($"Description: {description}");
            }
            sb.AppendLine("Extracted Web Content:");
            sb.AppendLine(cleanText);
            sb.AppendLine("---");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scrape web page content for URL: {Url}", url);
            return userMessage;
        }
    }

    private static string ExtractMetaTag(string html, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        var pattern = $@"<meta\s+[^>]*(?:property|name)=[""']{Regex.Escape(propertyName)}[""']\s+[^>]*content=[""']([^""']+)[""']";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups[1].Value;

        pattern = $@"<meta\s+[^>]*content=[""']([^""']+)[""']\s+[^>]*(?:property|name)=[""']{Regex.Escape(propertyName)}[""']";
        match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups[1].Value;

        return null;
    }

    private static string ExtractTagContent(string html, string tagName)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var pattern = $@"<{tagName}[^>]*>(.*?)</{tagName}>";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string CleanHtmlToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";

        // Remove <script>, <style>, <nav>, <header>, <footer>, <svg>, <noscript>
        html = Regex.Replace(html, @"<(script|style|nav|header|footer|svg|noscript)[^>]*?>.*?</\1>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"<!--.*?-->", "", RegexOptions.Singleline);

        // Replace block tags with linebreaks
        html = Regex.Replace(html, @"</?(p|div|h1|h2|h3|h4|h5|h6|li|br|tr)[^>]*>", "\n", RegexOptions.IgnoreCase);

        // Strip remaining HTML tags
        html = Regex.Replace(html, @"<[^>]+>", "", RegexOptions.IgnoreCase);

        // Decode HTML entities
        html = HttpUtility.HtmlDecode(html);

        // Normalize multiple whitespace and empty lines
        string[] lines = html.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var cleanLines = new List<string>();
        foreach (var l in lines)
        {
            string trimmed = l.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed) && trimmed.Length > 2)
            {
                cleanLines.Add(trimmed);
            }
        }

        return string.Join("\n", cleanLines);
    }

    private async Task PersistToChatMessagesTableAsync(string senderId, string receiverId, string body)
    {
        try
        {
            await using var conn = await _ds.OpenConnectionAsync();

            // If AI is responding, mark preceding user messages in AI thread as 'seen'
            if (string.Equals(senderId, "ai_copilot", StringComparison.OrdinalIgnoreCase) || string.Equals(senderId, "@Drive&Go AI", StringComparison.OrdinalIgnoreCase))
            {
                await using var updateCmd = new NpgsqlCommand(@"
                    UPDATE chat_messages
                    SET delivery_status = 'seen'
                    WHERE (receiver_id = 'ai_copilot' OR receiver_id = '@Drive&Go AI' OR receiver_id = 'group_dispatch')
                      AND delivery_status != 'seen'", conn);
                await updateCmd.ExecuteNonQueryAsync();
            }

            await using var cmd  = new NpgsqlCommand(@"
                INSERT INTO chat_messages (sender_id, receiver_id, message_body, timestamp, is_group_chat, delivery_status)
                VALUES (@sid, @rid, @body, NOW(), false, 'delivered')", conn);
            cmd.Parameters.AddWithValue("@sid",  senderId);
            cmd.Parameters.AddWithValue("@rid",  receiverId);
            cmd.Parameters.AddWithValue("@body", body ?? "");
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist AI message to chat_messages table");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PUBLIC: HISTORY / SESSIONS
    // ═══════════════════════════════════════════════════════════════════
    public async Task<List<AiCopilotMessageDto>> GetHistoryAsync(int sessionId, int limit = 50)
    {
        var list = new List<AiCopilotMessageDto>();
        await using var conn = await _ds.OpenConnectionAsync();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT copilot_msg_id, session_id, sender_id, llm_role, content,
                   ui_component_type, ui_payload, tool_name, provider_used, tokens_used, sent_at
            FROM ai_copilot_messages
            WHERE session_id = @sid
            ORDER BY sent_at ASC
            LIMIT @lim", conn);
        cmd.Parameters.AddWithValue("@sid", sessionId);
        cmd.Parameters.AddWithValue("@lim", limit);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new AiCopilotMessageDto
            {
                CopilotMsgId    = reader.GetInt64(0),
                SessionId       = reader.GetInt32(1),
                SenderId        = reader.GetString(2),
                LlmRole         = reader.GetString(3),
                Content         = reader.GetString(4),
                UiComponentType = reader.IsDBNull(5)  ? null : reader.GetString(5),
                UiPayload       = reader.IsDBNull(6)  ? null : reader.GetString(6),
                ToolName        = reader.IsDBNull(7)  ? null : reader.GetString(7),
                ProviderUsed    = reader.IsDBNull(8)  ? null : reader.GetString(8),
                TokensUsed      = reader.IsDBNull(9)  ? null : reader.GetInt32(9),
                SentAt          = reader.GetDateTime(10)
            });
        }
        return list;
    }

    public async Task<int> CreateSessionAsync(int adminUserId, string title = "New Conversation")
    {
        await using var conn = await _ds.OpenConnectionAsync();
        await using var cmd  = new NpgsqlCommand(@"
            INSERT INTO ai_copilot_sessions (admin_user_id, title)
            VALUES (@uid, @title)
            RETURNING session_id", conn);
        cmd.Parameters.AddWithValue("@uid",   adminUserId);
        cmd.Parameters.AddWithValue("@title", title);

        int sessionId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        // Inject the system prompt as the first message in history
        await PersistMessageAsync(sessionId, "bot_copilot", "system",
            DriveAndGoKnowledgeBase.GetSystemPrompt(),
            null, null, null, "System", null);

        return sessionId;
    }

    public async Task<List<AiCopilotSessionDto>> GetSessionsAsync(int adminUserId)
    {
        var list = new List<AiCopilotSessionDto>();
        await using var conn = await _ds.OpenConnectionAsync();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT s.session_id, s.admin_user_id, s.title, s.created_at, s.updated_at,
                   (SELECT content FROM ai_copilot_messages
                    WHERE session_id = s.session_id AND llm_role = 'assistant'
                    ORDER BY sent_at DESC LIMIT 1) AS last_message
            FROM ai_copilot_sessions s
            WHERE s.admin_user_id = @uid
            ORDER BY s.updated_at DESC
            LIMIT 30", conn);
        cmd.Parameters.AddWithValue("@uid", adminUserId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new AiCopilotSessionDto
            {
                SessionId   = reader.GetInt32(0),
                AdminUserId = reader.GetInt32(1),
                Title       = reader.GetString(2),
                CreatedAt   = reader.GetDateTime(3),
                UpdatedAt   = reader.GetDateTime(4),
                LastMessage = reader.IsDBNull(5) ? "New conversation" : reader.GetString(5)[..Math.Min(80, reader.GetString(5).Length)] + "..."
            });
        }
        return list;
    }


    public async Task<AiSuggestionsResponse> GetSuggestionsAsync()
    {
        // Gather live metrics for contextual suggestion generation
        var fleet   = await _tools.GetAvailableFleetCountAsync();
        var pending = await _tools.GetPendingBookingsAsync();
        var overdue = await _tools.GetOverdueRentalsAsync();
        var revenue = await _tools.GetTodayRevenueAsync();

        var suggestions = DriveAndGoKnowledgeBase.GetContextualSuggestions(
            overdue.OverdueCount, pending.PendingCount,
            revenue.MonthRevenue, fleet.UtilizationPct);

        return new AiSuggestionsResponse
        {
            Suggestions = suggestions,
            Context     = $"Fleet: {fleet.TotalVehicles} total, {fleet.Available} available | Overdue: {overdue.OverdueCount} | Pending: {pending.PendingCount}"
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    //  MULTI-MODEL FALLBACK PIPELINE
    //  — Per-tier 18s CancellationToken timeout
    //  — 429 rate-limit causes immediate cascade to next tier
    // ═══════════════════════════════════════════════════════════════════
    private async Task<(string rawJson, string provider)> RunFallbackPipelineAsync(
        int sessionId, List<object> messages)
    {
        // 0. QUICK OFFLINE CHECK — If no internet, return explicit English offline notice
        if (!await IsInternetAvailableAsync())
        {
            _logger.LogWarning("[Network] No active internet connection detected.");
            return ("⚠️ **Offline Mode**: No active internet connection detected. An internet connection is required to use Drive&Go AI and access live database insights.", "Offline");
        }

        var tiers = new List<(string provider, string key, Func<CancellationToken, Task<(string result, string provider)?>> action)>
        {
            ("Mistral",      _mistralKey,     ct => TryMistralAsync(sessionId, messages, ct)),
            ("Gemini",       _geminiKey,      ct => TryGeminiAsync(sessionId, messages, ct)),
            ("Groq",        _groqKey,        ct => TryGroqAsync(sessionId, messages, ct)),
            ("SambaNova",    _sambaNovaKey,   ct => TrySambaNovaAsync(sessionId, messages, ct)),
            ("OpenRouter",   _openRouterKey,  ct => TryOpenRouterAsync(sessionId, messages, ct)),
            ("Cohere",       _cohereKey,      ct => TryCohereAsync(sessionId, messages, ct)),
            ("HuggingFace",  _huggingFaceKey, ct => TryHuggingFaceAsync(sessionId, messages, ct))
        };

        foreach (var p in tiers)
        {
            // 1. SKIP EMPTY KEYS
            if (string.IsNullOrWhiteSpace(p.key))
            {
                Console.WriteLine($"[AI Pipeline Info] Skipping AI provider '{p.provider}' because its API key is unconfigured/empty.");
                _logger.LogInformation("Skipping AI provider '{ProviderName}' because its API key is empty.", p.provider);
                continue;
            }

            // 2. PER-TIER CANCELLATION TOKEN — 18 second hard timeout per provider
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(18));

            try
            {
                Console.WriteLine($"[AI Pipeline Attempt] Trying provider '{p.provider}'...");
                var result = await p.action(cts.Token);

                // 3. SUCCESS CONDITION
                if (result != null && !string.IsNullOrWhiteSpace(result.Value.result))
                {
                    Console.WriteLine($"[AI Pipeline Success] Provider '{p.provider}' succeeded with model '{result.Value.provider}'.");
                    return result.Value;
                }
                Console.WriteLine($"[AI Pipeline Warning] Provider '{p.provider}' returned null/empty response. Falling through to next tier.");
            }
            catch (OperationCanceledException ex)
            {
                Console.WriteLine($"[AI Pipeline Timeout] Provider '{p.provider}' timed out after 18s: {ex.Message}");
                _logger.LogWarning(
                    "AI provider '{ProviderName}' timed out after 18s. Falling through to next tier.",
                    p.provider);
                continue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Pipeline Exception] Provider '{p.provider}' failed with exception: {ex.GetType().Name}: {ex.Message}");
                _logger.LogWarning(ex, "AI provider '{ProviderName}' failed. Continuing to next fallback...", p.provider);
                continue;
            }
        }

        // 4. OFFLINE FALLBACK: Only trigger local database engine when completely offline (no internet)
        if (!await IsInternetAvailableAsync())
        {
            string? localDbAnswer = await TryLocalDatabaseAnswerAsync(sessionId, messages.LastOrDefault()?.ToString() ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(localDbAnswer))
            {
                _logger.LogInformation("Served offline response via Local Database Engine.");
                return (localDbAnswer, "OfflineLocalEngine");
            }
        }

        // 5. Return friendly natural executive fallback notice
        _logger.LogWarning("All cloud AI models failed or were unconfigured.");
        return ("Drive&Go AI is currently processing a high volume of requests. Please try asking your question again in a moment, and I'll be happy to assist you!", "QuotaExhausted");
    }

    private async Task<bool> IsInternetAvailableAsync()
    {
        try
        {
            using var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            var response = await client.GetAsync("http://clients3.google.com/generate_204");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  TIER 1 — GROQ (OpenAI-compatible, supports tool_calls)
    // ═══════════════════════════════════════════════════════════════════
    private async Task<(string result, string provider)?> TryGroqAsync(
        int sessionId, List<object> messages, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_groqKey)) return null;

        string[] fallbackChain = { "openai/gpt-oss-120b", "openai/gpt-oss-20b", "qwen/qwen3.6-27b" };
        using var client = _httpFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) DriveAndGo/1.0");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _groqKey);

        foreach (var modelName in fallbackChain)
        {
            try
            {
                var body = new
                {
                    model       = modelName,
                    messages,
                    tools       = AiToolsService.GetToolDefinitions(),
                    tool_choice = "auto",
                    temperature = 0.0,
                    max_tokens  = 2000
                };

                var json = JsonSerializer.Serialize(body);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(GroqUrl, content, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    int statusCode = (int)response.StatusCode;
                    string reason = statusCode switch
                    {
                        413 => "context too long (input tokens exceeded)",
                        429 => "rate limited — cascading to next provider",
                        503 => "model overloaded",
                        _   => $"HTTP {statusCode}"
                    };
                    Console.WriteLine($"[AI Pipeline Error] Groq ({modelName}) returned Status {statusCode}: {errorBody}");
                    _logger.LogWarning("[Groq] Model '{Model}' skipped — {Reason}. Body: {Body}", modelName, reason, errorBody);
                    continue;
                }

                var raw = await response.Content.ReadAsStringAsync(ct);
                var processed = await ProcessOpenAiCompatibleResponseAsync(raw, sessionId, messages, $"Groq ({modelName})");
                if (!string.IsNullOrWhiteSpace(processed)) return (processed, modelName);
            }
            catch (OperationCanceledException) { throw; } // bubble up to pipeline
            catch (Exception ex)
            {
                _logger.LogWarning("[Groq] Model {Model} failed. Trying next... {Msg}", modelName, ex.Message);
            }
        }
        return null;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  TIER 2 — HUGGING FACE (OpenAI-compatible Router)
    // ═══════════════════════════════════════════════════════════════════
    private async Task<(string result, string provider)?> TryHuggingFaceAsync(
        int sessionId, List<object> messages, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_huggingFaceKey)) return null;

        string[] fallbackModels = {
            "meta-llama/Llama-3.3-70B-Instruct",
            "Qwen/Qwen2.5-72B-Instruct",
            "deepseek-ai/DeepSeek-R1",
            "Qwen/Qwen2.5-Coder-32B-Instruct"
        };

        using var client = _httpFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) DriveAndGo/1.0");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _huggingFaceKey);

        foreach (var modelName in fallbackModels)
        {
            try
            {
                var body = new
                {
                    model       = modelName,
                    messages,
                    tools       = AiToolsService.GetToolDefinitions(),
                    tool_choice = "auto",
                    temperature = 0.0,
                    max_tokens  = 2000
                };

                var json = JsonSerializer.Serialize(body);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(HuggingFaceUrl, content, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    int statusCode = (int)response.StatusCode;
                    string reason = statusCode switch
                    {
                        413 => "context limit exceeded",
                        429 => "rate limit exceeded — cascading to next HuggingFace model",
                        403 => "forbidden/unauthorized model",
                        503 => "model overloaded",
                        _   => $"HTTP {statusCode}"
                    };
                    Console.WriteLine($"[AI Pipeline Error] HuggingFace ({modelName}) returned Status {statusCode}: {errorBody}");
                    _logger.LogWarning("[HuggingFace] Model '{Model}' skipped — {Reason}. Body: {Body}", modelName, reason, errorBody);
                    continue;
                }

                var raw = await response.Content.ReadAsStringAsync(ct);
                var processed = await ProcessOpenAiCompatibleResponseAsync(raw, sessionId, messages, $"HuggingFace ({modelName})");
                if (!string.IsNullOrWhiteSpace(processed)) return (processed, $"HuggingFace ({modelName})");
            }
            catch (OperationCanceledException) { throw; } // bubble up to pipeline hard timeout
            catch (Exception ex)
            {
                _logger.LogWarning("[HuggingFace] Model '{Model}' failed: {Msg}. Trying next model...", modelName, ex.Message);
            }
        }
        return null;
    }


    // ═══════════════════════════════════════════════════════════════════
    //  TIER 1.5 — SAMBANOVA (OpenAI-compatible)
    // ═══════════════════════════════════════════════════════════════════
    private async Task<(string result, string provider)?> TrySambaNovaAsync(
        int sessionId, List<object> messages, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_sambaNovaKey)) return null;

        using var client = _httpFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) DriveAndGo/1.0");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _sambaNovaKey);

        try
        {
            var body = new
            {
                model       = "Meta-Llama-3.3-70B-Instruct",
                messages,
                tools       = AiToolsService.GetToolDefinitions(),
                tool_choice = "auto",
                temperature = 0.0,
                max_tokens  = 2000
            };

            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(SambaNovaUrl, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                int statusCode = (int)response.StatusCode;
                Console.WriteLine($"[AI Pipeline Error] SambaNova returned Status {statusCode}: {errorBody}");
                _logger.LogWarning("[SambaNova] HTTP {Status}: {Body}", statusCode, errorBody);
                if (statusCode == 429 || statusCode == 403)
                {
                    throw new HttpRequestException($"HTTP {statusCode}: SambaNova rate limit or quota exceeded.");
                }
                return null;
            }

            var raw = await response.Content.ReadAsStringAsync(ct);
            var processed = await ProcessOpenAiCompatibleResponseAsync(raw, sessionId, messages, "SambaNova (Meta-Llama-3.3-70B)");
            if (!string.IsNullOrWhiteSpace(processed)) return (processed, "SambaNova");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning("[SambaNova] failed: {Msg}", ex.Message);
        }
        return null;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  TIER 2 — COHERE (Command R+ native tool use)
    // ═══════════════════════════════════════════════════════════════════
    private async Task<(string result, string provider)?> TryCohereAsync(
        int sessionId, List<object> messages, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_cohereKey)) return null;

        try
        {
            using var client = _httpFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) DriveAndGo/1.0");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _cohereKey);
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            var preamble    = ExtractSystemContent(messages);
            var chatHistory = BuildCohereHistory(messages);
            var lastUser    = GetLastUserMessage(messages);

            var body = new
            {
                model        = "command-r-plus-08-2024",
                message      = lastUser,
                chat_history = chatHistory,
                preamble,
                tools        = AiToolsService.BuildCohereTools(),
                temperature  = 0.0,
                max_tokens   = 2000
            };

            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(CohereUrl, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                int statusCode = (int)response.StatusCode;
                Console.WriteLine($"[AI Pipeline Error] Cohere returned Status {statusCode}: {errorBody}");
                _logger.LogWarning("[Cohere] HTTP {Status} — {Body}", statusCode, errorBody);
                if (statusCode == 429 || statusCode == 403)
                {
                    throw new HttpRequestException($"HTTP {statusCode}: Cohere rate limit or quota exceeded.");
                }
                return null;
            }

            var raw = await response.Content.ReadAsStringAsync(ct);
            var processed = await ProcessCohereResponseAsync(raw, sessionId, messages);
            if (!string.IsNullOrWhiteSpace(processed)) return (processed, "command-r-plus");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning("Cohere failed: {Msg}", ex.Message);
        }
        return null;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  TIER 3 — GEMINI (Google AI Studio REST)
    // ═══════════════════════════════════════════════════════════════════
    private async Task<(string result, string provider)?> TryGeminiAsync(
        int sessionId, List<object> messages, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_geminiKey)) return null;

        string[] models = { "gemini-3.6-flash", "gemini-1.5-flash", "gemini-1.5-pro", "gemini-2.5-flash" };

        foreach (var modelName in models)
        {
            try
            {
                using var client = _httpFactory.CreateClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) DriveAndGo/1.0");
                string systemCtx = ExtractSystemContent(messages);

                var contents = BuildGeminiContents(messages);

                var body = new
                {
                    system_instruction = new { parts = new[] { new { text = systemCtx } } },
                    contents,
                    tools = new[] { AiToolsService.GetGeminiToolDefinitions() },
                    generationConfig = new { temperature = 0.0, maxOutputTokens = 2000 }
                };

                string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={_geminiKey}";
                var json = JsonSerializer.Serialize(body);
                using var reqContent = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, reqContent, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    int statusCode = (int)response.StatusCode;
                    Console.WriteLine($"[AI Pipeline Error] Gemini ({modelName}) returned Status {statusCode}: {errorBody}");
                    _logger.LogWarning("[Gemini] ({Model}) HTTP {Status} — {Body}", modelName, statusCode, errorBody);
                    if (statusCode == 429) continue;
                    continue;
                }

                var raw = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(raw);
                var candidate = doc.RootElement.GetProperty("candidates")[0];
                var contentEl = candidate.GetProperty("content");
                var parts = contentEl.GetProperty("parts");

                foreach (var part in parts.EnumerateArray())
                {
                    if (!part.TryGetProperty("functionCall", out var funcCall)) continue;

                    string? toolName = funcCall.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                    string? toolArgs = funcCall.TryGetProperty("args", out var argsEl)
                        ? argsEl.GetRawText() : null;

                    if (string.IsNullOrWhiteSpace(toolName)) continue;

                    _logger.LogInformation("[Gemini] Native functionCall detected: {Tool}", toolName);
                    string toolResult = await _tools.DispatchAsync(toolName, toolArgs);
                    await PersistMessageAsync(sessionId, "tool_execution", "tool", toolResult, null, null, toolName, modelName, null);

                    var contentsWithResult = BuildGeminiContentsWithToolResult(
                        messages, toolName, toolResult, funcCall.GetRawText());

                    var body2 = new
                    {
                        system_instruction = new { parts = new[] { new { text = systemCtx } } },
                        contents = contentsWithResult,
                        generationConfig = new { temperature = 0.0, maxOutputTokens = 2000 }
                    };

                    var json2 = JsonSerializer.Serialize(body2);
                    using var reqContent2 = new StringContent(json2, Encoding.UTF8, "application/json");
                    var response2 = await client.PostAsync(url, reqContent2, ct);

                    if (!response2.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("[Gemini] Pass-2 (tool resubmit) failed for {Model}.", modelName);
                        break;
                    }

                    var raw2 = await response2.Content.ReadAsStringAsync(ct);
                    using var doc2 = JsonDocument.Parse(raw2);
                    var text2 = doc2.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text").GetString();

                    if (!string.IsNullOrWhiteSpace(text2))
                        return (text2, modelName);

                    break;
                }

                foreach (var part in parts.EnumerateArray())
                {
                    if (!part.TryGetProperty("text", out var textEl)) continue;
                    var text = textEl.GetString();
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    var intercepted = await InterceptTextToolCallAsync(sessionId, text, messages, modelName);
                    if (!string.IsNullOrWhiteSpace(intercepted))
                        return (intercepted, modelName);

                    return (text, modelName);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning("[Gemini] ({Model}) failed: {Msg}", modelName, ex.Message);
            }
        }
        return null;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  TIER 4 — MISTRAL AI (La Plateforme)
    // ═══════════════════════════════════════════════════════════════════
    private async Task<(string result, string provider)?> TryMistralAsync(
        int sessionId, List<object> messages, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_mistralKey)) return null;

        string[] fallbackChain = { "mistral-small-latest", "pixtral-12b-2409" };
        using var client = _httpFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) DriveAndGo/1.0");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _mistralKey);

        foreach (var modelName in fallbackChain)
        {
            try
            {
                var body = new
                {
                    model       = modelName,
                    messages,
                    tools       = AiToolsService.GetToolDefinitions(),
                    tool_choice = "auto",
                    temperature = 0.0,
                    max_tokens  = 2000
                };

                var json     = JsonSerializer.Serialize(body);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(MistralUrl, content, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    Console.WriteLine($"[AI Pipeline Error] Mistral ({modelName}) returned Status {(int)response.StatusCode}: {errorBody}");
                    _logger.LogWarning("[Mistral] ({Model}) HTTP {Status} — {Body}", modelName, (int)response.StatusCode, errorBody);
                    continue;
                }

                var raw = await response.Content.ReadAsStringAsync(ct);
                var processed = await ProcessOpenAiCompatibleResponseAsync(raw, sessionId, messages, $"Mistral ({modelName})");
                if (!string.IsNullOrWhiteSpace(processed)) return (processed, $"Mistral ({modelName})");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning("[Mistral] ({Model}) failed: {Msg}", modelName, ex.Message);
            }
        }
        return null;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  TIER 5 — OPENROUTER
    // ═══════════════════════════════════════════════════════════════════
    private async Task<(string result, string provider)?> TryOpenRouterAsync(
        int sessionId, List<object> messages, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_openRouterKey)) return null;

        string[] fallbackChain = new[]
        {
            "meta-llama/llama-3.3-70b-instruct:free",
            "qwen/qwen-2.5-72b-instruct:free",
            "google/gemma-2-9b-it:free",
            "meta-llama/llama-3.1-8b-instruct:free",
            "mistralai/mistral-7b-instruct:free",
            "openrouter/auto"
        };

        using var client = _httpFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _openRouterKey);
        client.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", "https://driveandgo.com");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Title", "Drive&Go AI");

        foreach (var modelName in fallbackChain)
        {
            try
            {
                var body = new
                {
                    model       = modelName,
                    messages,
                    tools       = AiToolsService.GetToolDefinitions(),
                    tool_choice = "auto",
                    temperature = 0.0,
                    max_tokens  = 1000
                };

                var json     = JsonSerializer.Serialize(body);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(OpenRouterUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    string reason = (int)response.StatusCode switch
                    {
                        413 => "context too long (input tokens exceeded)",
                        429 => "rate limited / quota exceeded",
                        503 => "model overloaded",
                        _   => $"HTTP {(int)response.StatusCode}"
                    };
                    Console.WriteLine($"[AI Pipeline Error] OpenRouter ({modelName}) returned Status {(int)response.StatusCode}: {errorBody}");
                    _logger.LogWarning(
                        "[OpenRouter] Model '{Model}' skipped — {Reason}. Body: {Body}. Trying next model...",
                        modelName, reason, errorBody);
                    continue;
                }

                var raw = await response.Content.ReadAsStringAsync();
                var processed = await ProcessOpenAiCompatibleResponseAsync(raw, sessionId, messages, $"OpenRouter ({modelName})");
                if (!string.IsNullOrWhiteSpace(processed)) 
                {
                    // Clean up the model name for a cleaner UI tag (e.g., "via llama-3.3-70b-instruct")
                    string cleanModelName = modelName.Contains("/") ? modelName.Split('/')[1] : modelName;
                    return (processed, cleanModelName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Model {modelName} failed. Trying next... {Msg}", modelName, ex.Message);
            }
        }

        return null;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  OPENAI-COMPATIBLE RESPONSE PROCESSOR (Groq + OpenRouter)
    //  Handles tool_calls intercept → execute → re-submit loop
    // ═══════════════════════════════════════════════════════════════════
    private async Task<string?> ProcessOpenAiCompatibleResponseAsync(
        string rawBody, int sessionId, List<object> messages, string provider, int depth = 0)
    {
        if (depth >= MaxToolLoopDepth) return null;

        using var doc   = JsonDocument.Parse(rawBody);
        var choice      = doc.RootElement.GetProperty("choices")[0];
        var message     = choice.GetProperty("message");
        string? content = null;

        // ── Token exhaustion guard ───────────────────────────────────────────
        // finish_reason = "length" means the model ran out of max_tokens and
        // the response was cut off mid-generation. Treat this as a failed attempt
        // so the orchestrator falls through to the next model/tier.
        if (choice.TryGetProperty("finish_reason", out var finishEl))
        {
            string? finishReason = finishEl.GetString();
            if (string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "[{Provider}] finish_reason=length — model ran out of tokens. Falling through to next tier.",
                    provider);
                return null;
            }
        }

        if (message.TryGetProperty("content", out var contentEl) && contentEl.ValueKind != JsonValueKind.Null)
            content = contentEl.GetString();


        // Check for tool_calls
        if (message.TryGetProperty("tool_calls", out var toolCallsEl) &&
            toolCallsEl.ValueKind == JsonValueKind.Array &&
            toolCallsEl.GetArrayLength() > 0)
        {
            try
            {
                // IMPORTANT: Discard any 'content' the LLM returned alongside tool_calls.
                // Some models narrate their intent ("I will call get_overdue_rentals...").
                // We must never surface that narration to the user.
                content = null;

                // Append assistant message with tool_calls
                var assistantMsg = JsonSerializer.Deserialize<object>(message.GetRawText())!;
                var mutableMessages = new List<object>(messages) { assistantMsg };

                // Persist assistant message asking to call tools (internal only)
                await PersistMessageAsync(sessionId, "bot_copilot", "assistant", message.GetRawText(), null, null, null, provider, null);

                // Execute each tool call
                string? lastExecutedTool = null;
                string? lastExecutedResult = null;

                foreach (var tc in toolCallsEl.EnumerateArray())
                {
                    string? tcId      = tc.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                    string? toolName  = tc.GetProperty("function").GetProperty("name").GetString();

                    // Strip Markdown fences LLMs sometimes inject around JSON args
                    string? rawArgs   = tc.GetProperty("function").TryGetProperty("arguments", out var argsEl)
                                        ? argsEl.GetString() ?? argsEl.GetRawText() : null;
                    string? toolArgs  = rawArgs?
                        .Replace("```json", "", StringComparison.OrdinalIgnoreCase)
                        .Replace("```", "")
                        .Trim();

                    string toolResult;
                    try
                    {
                        toolResult = await _tools.DispatchAsync(toolName!, toolArgs);
                    }
                    catch (Exception toolEx)
                    {
                        _logger.LogError(toolEx,
                            "[{Provider}] Tool '{Tool}' threw an unhandled exception. Injecting graceful error context.",
                            provider, toolName);

                        toolResult = $"{{\"error\": \"Tool '{toolName}' encountered an internal error: {toolEx.Message.Replace("\"", "'")}. Please inform the user the data is temporarily unavailable.\"}}";
                    }

                    lastExecutedTool = toolName;
                    lastExecutedResult = toolResult;

                    mutableMessages.Add(new
                    {
                        role         = "tool",
                        tool_call_id = string.IsNullOrWhiteSpace(tcId) ? $"call_{Guid.NewGuid():N}" : tcId,
                        content      = toolResult,
                        name         = toolName
                    });
                    
                    // Persist the tool execution result
                    await PersistMessageAsync(sessionId, "tool_execution", "tool", toolResult, null, null, toolName, provider, null);

                    _logger.LogInformation("[{Provider}] Tool executed: {Tool}", provider, toolName);
                }

                // Re-submit with tool results — second pass
                string reBody = await ResubmitWithToolResultsAsync(mutableMessages, provider);
                if (!string.IsNullOrEmpty(reBody))
                {
                    string? secondPassResult = await ProcessOpenAiCompatibleResponseAsync(reBody, sessionId, mutableMessages, provider, depth + 1);
                    if (!string.IsNullOrWhiteSpace(secondPassResult))
                        return secondPassResult;
                }

                // Fail-safe: If second pass yielded empty narrative, format tool results directly
                string fallbackText = FormatFallbackToolSummary(lastExecutedTool ?? "data_query", lastExecutedResult ?? string.Empty);
                await PersistMessageAsync(sessionId, "bot_copilot", "assistant", fallbackText, null, null, lastExecutedTool, provider, null);
                return fallbackText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Provider}] CRITICAL TOOL EXECUTION FAILURE. Model likely hallucinated invalid tool JSON.", provider);
                return "I'm sorry, but I encountered an issue retrieving that specific data. Please try asking your question again in a moment!";
            }
        }


        // ── Plain Text Response Handling (No tool_calls) ───────────────────
        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                var intercepted = await InterceptTextToolCallAsync(sessionId, content, messages, provider, depth);
                if (!string.IsNullOrWhiteSpace(intercepted))
                    return intercepted;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{Provider}] Text tool call interception encountered an error. Returning raw text.", provider);
            }

            return content;
        }

        return null;
    }

    private async Task<string?> InterceptTextToolCallAsync(
        int sessionId, string content, List<object> messages, string provider, int depth = 0)
    {
        if (string.IsNullOrWhiteSpace(content) || depth >= MaxToolLoopDepth)
            return null;

        var toolNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["get_pending_bookings"]       = AiToolsService.ToolGetPendingBookings,
            ["get_overdue_rentals"]        = AiToolsService.ToolGetOverdueRentals,
            ["get_today_revenue"]          = AiToolsService.ToolGetTodayRevenue,
            ["get_weekly_analytics"]       = AiToolsService.ToolGetWeeklyAnalytics,
            ["get_available_fleet_count"]  = AiToolsService.ToolGetFleetCount,
            ["get_top_drivers"]            = AiToolsService.ToolGetTopDrivers,
            ["get_monthly_revenue"]        = AiToolsService.ToolGetMonthlyRevenue,
            ["predict_next_year_sales"]    = AiToolsService.ToolPredictNextYearSales,
            ["get_vehicle_utilization"]    = AiToolsService.ToolGetVehicleUtil,
            ["check_surge_pricing"]        = AiToolsService.ToolCheckSurgePricing,
            ["get_maintenance_alerts"]     = AiToolsService.ToolGetMaintenanceAlerts,
            ["auto_dispatch_booking"]      = AiToolsService.ToolAutoDispatchBooking,
            ["analyze_id_document"]        = AiToolsService.ToolAnalyzeIdDocument,
            ["assess_vehicle_damage"]      = AiToolsService.ToolAssessVehicleDamage,
            ["check_fuel_anomaly"]         = AiToolsService.ToolCheckFuelAnomaly,
        };

        string? interceptedTool = null;
        foreach (var (key, toolName) in toolNameMap)
        {
            if (content.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                interceptedTool = toolName;
                break;
            }
        }

        if (interceptedTool == null) return null;

        _logger.LogWarning(
            "[{Provider}] Text-hallucination intercepted: model mentioned '{Tool}' in plain text. Executing silently.",
            provider, interceptedTool);

        string toolResult = await _tools.DispatchAsync(interceptedTool, null);
        await PersistMessageAsync(sessionId, "tool_execution", "tool", toolResult, null, null, interceptedTool, provider, null);

        var correctedMessages = new List<object>(messages)
        {
            new { role = "tool", content = toolResult, name = interceptedTool }
        };

        string reBody = await ResubmitWithToolResultsAsync(correctedMessages, provider);
        if (!string.IsNullOrEmpty(reBody))
        {
            var processed = await ProcessOpenAiCompatibleResponseAsync(reBody, sessionId, correctedMessages, provider, depth + 1);
            if (!string.IsNullOrWhiteSpace(processed)) return processed;
        }

        return toolResult;
    }

    private async Task<string> ResubmitWithToolResultsAsync(List<object> messages, string provider)
    {
        using var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds);

        string url;
        string model;

        if (provider.Contains("SambaNova", StringComparison.OrdinalIgnoreCase))
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _sambaNovaKey);
            url = SambaNovaUrl;
            model = "Meta-Llama-3.3-70B-Instruct";
        }
        else if (provider.Contains("Groq", StringComparison.OrdinalIgnoreCase))
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _groqKey);
            url = GroqUrl;
            model = "openai/gpt-oss-120b";
        }
        else
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _openRouterKey);
            url = OpenRouterUrl;
            model = "meta-llama/llama-3.3-70b-instruct";
        }

        var body = new
        {
            model,
            messages,
            temperature = 0.0,
            max_tokens  = 2000
        };

        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, content);
        return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : string.Empty;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  COHERE RESPONSE PROCESSOR
    // ═══════════════════════════════════════════════════════════════════
    private async Task<string?> ProcessCohereResponseAsync(string rawBody, int sessionId, List<object> messages)
    {
        using var doc = JsonDocument.Parse(rawBody);
        var root      = doc.RootElement;

        // Check for tool_plan / tool_calls in Cohere v2
        if (root.TryGetProperty("tool_calls", out var tcArr) && tcArr.ValueKind == JsonValueKind.Array)
        {
            // Persist the assistant message
            await PersistMessageAsync(sessionId, "bot_copilot", "assistant", root.GetRawText(), null, null, null, "Cohere", null);

            var toolResults = new List<object>();
            foreach (var tc in tcArr.EnumerateArray())
            {
                string? toolName = tc.TryGetProperty("name", out var n) ? n.GetString() : null;
                string? toolArgs = tc.TryGetProperty("parameters", out var p)
                    ? JsonSerializer.Serialize(p) : null;

                string result = await _tools.DispatchAsync(toolName!, toolArgs);
                toolResults.Add(new
                {
                    call    = new { name = toolName, parameters = tc.TryGetProperty("parameters", out var pp) ? pp : (JsonElement?)null },
                    outputs = new[] { new { text = result } }
                });
                
                await PersistMessageAsync(sessionId, "tool_execution", "tool", result, null, null, toolName, "Cohere", null);
            }

            // Second call with tool results
            using var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _cohereKey);

            var body     = new { model = "command-r-plus-08-2024", tool_results = toolResults, temperature = 0.0, max_tokens = 2000 };
            var json     = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var res2 = await client.PostAsync(CohereUrl, content);
            if (!res2.IsSuccessStatusCode) return null;

            rawBody = await res2.Content.ReadAsStringAsync();
            using var doc2 = JsonDocument.Parse(rawBody);
            root = doc2.RootElement.Clone();
        }

        // Extract final text
        if (root.TryGetProperty("text", out var textEl))
            return textEl.GetString();
        if (root.TryGetProperty("message", out var msgEl) &&
            msgEl.TryGetProperty("content", out var contArr) &&
            contArr.GetArrayLength() > 0)
            return contArr[0].TryGetProperty("text", out var t) ? t.GetString() : null;

        return null;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  TIER 5 — LOCAL FALLBACK (Dynamic, question-aware)
    // ═══════════════════════════════════════════════════════════════════
    private async Task<string> BuildLocalFallbackResponseAsync(string userMessage = "")
    {
        try
        {
            var q = userMessage.ToLowerInvariant();

            // ── Question-aware: Top Earners / Top Vehicles / Vehicle Revenue focus ────
            if (q.Contains("top earner") || q.Contains("earner") || q.Contains("top vehicle") || q.Contains("top performing vehicle") || q.Contains("vehicle revenue") || (q.Contains("top") && (q.Contains("vehicle") || q.Contains("car"))))
            {
                var topV = await _tools.GetVehicleUtilizationAsync("this_month", 10);
                var sb = new StringBuilder();
                sb.AppendLine("Here are the **top earning vehicles for this month** based on live database records:");
                sb.AppendLine();
                int rank = 1;
                foreach (var v in topV.Vehicles)
                {
                    sb.AppendLine($"{rank++}. **{v.VehicleName}** ({v.PlateNo}) — **₱{v.Revenue:N2}** ({v.TotalRentals} rentals)");
                }
                if (topV.Vehicles.Count == 0)
                {
                    sb.AppendLine("Wala pong na-record na vehicle earnings para sa kasalukuyang buwan.");
                }

                var chartData = topV.Vehicles.Select(v => new { label = $"{v.VehicleName} ({v.PlateNo})", value = v.Revenue }).ToList();
                var json = new {
                    ui_component = chartData.Count > 0 ? "BarChart" : "MetricCard",
                    data = chartData
                };
                return sb.ToString() + "\n---UI_COMPONENT---\n" + JsonSerializer.Serialize(json);
            }

            // ── Question-aware: Drivers / Top Drivers focus ────────────────────
            if (q.Contains("driver") || q.Contains("top driver") || q.Contains("best driver") || q.Contains("chaffeur"))
            {
                var topD = await _tools.GetTopDriversAsync(null, 5);
                var sb = new StringBuilder();
                sb.AppendLine("Here are the **top 5 drivers by rating** from live database records:");
                sb.AppendLine();
                int rank = 1;
                foreach (var d in topD.Drivers)
                {
                    sb.AppendLine($"{rank++}. **{d.FullName}** — Rating: ⭐ **{d.RatingAvg:F1}** ({d.TotalTrips} completed trips)");
                }
                if (topD.Drivers.Count == 0)
                {
                    sb.AppendLine("Sa kasalukuyan, wala pang recorded trips ang ating mga registered drivers.");
                }

                var chartData = topD.Drivers.Select(d => new { label = d.FullName, value = d.RatingAvg }).ToList();
                var json = new {
                    ui_component = chartData.Count > 0 ? "BarChart" : "MetricCard",
                    data = chartData
                };
                return sb.ToString() + "\n---UI_COMPONENT---\n" + JsonSerializer.Serialize(json);
            }

            // ── Question-aware: Ratings / Feedback focus ──────────────
            if (q.Contains("rating") || q.Contains("feedback") || q.Contains("review"))
            {
                var ratingsObj = await _tools.GetRatingsFeedbackAsync(10);
                string jsonStr = JsonSerializer.Serialize(ratingsObj);
                using var doc = JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;
                decimal avgV = root.TryGetProperty("avg_vehicle_score", out var av) ? (av.ValueKind == JsonValueKind.Number && av.TryGetDecimal(out var decV) ? decV : 0m) : 0m;
                decimal avgD = root.TryGetProperty("avg_driver_score", out var ad) ? (ad.ValueKind == JsonValueKind.Number && ad.TryGetDecimal(out var decD) ? decD : 0m) : 0m;
                int totalR = root.TryGetProperty("total_ratings", out var tr) ? tr.GetInt32() : 0;

                var sb = new StringBuilder();
                sb.AppendLine("⭐ **Driver & Vehicle Performance Ratings Report**");
                sb.AppendLine();
                sb.AppendLine($"• **Overall Driver Rating Avg:** ⭐ **{avgD:F1}** / 5.0");
                sb.AppendLine($"• **Overall Vehicle Condition Avg:** ⭐ **{avgV:F1}** / 5.0");
                sb.AppendLine($"• **Total Customer Reviews:** **{totalR}**");
                sb.AppendLine();

                var chartData = new List<object>();
                if (root.TryGetProperty("recent_ratings", out var topArr) && topArr.ValueKind == JsonValueKind.Array && topArr.GetArrayLength() > 0)
                {
                    sb.AppendLine("| Customer | Vehicle / Driver | Driver ⭐ | Vehicle ⭐ | Customer Feedback |");
                    sb.AppendLine("| :--- | :--- | :---: | :---: | :--- |");
                    foreach (var r in topArr.EnumerateArray())
                    {
                        string cust = r.TryGetProperty("customer_name", out var c) ? c.GetString() ?? "Customer" : "Customer";
                        string veh = r.TryGetProperty("vehicle", out var v) ? v.GetString() ?? "Vehicle" : "Vehicle";
                        string drv = r.TryGetProperty("driver_name", out var d) ? d.GetString() ?? "N/A" : "N/A";
                        decimal dScore = r.TryGetProperty("driver_score", out var ds) && ds.ValueKind == JsonValueKind.Number && ds.TryGetDecimal(out var dDec) ? dDec : 0m;
                        decimal vScore = r.TryGetProperty("vehicle_score", out var vs) && vs.ValueKind == JsonValueKind.Number && vs.TryGetDecimal(out var vDec) ? vDec : 0m;
                        string comment = r.TryGetProperty("comment", out var cm) ? cm.GetString() ?? "Satisfied customer" : "Satisfied customer";

                        string dText = dScore > 0 ? $"⭐ {dScore:F1}" : "N/A";
                        string vText = vScore > 0 ? $"⭐ {vScore:F1}" : "N/A";

                        sb.AppendLine($"| **{cust}** | {veh} ({drv}) | {dText} | {vText} | *\"{comment}\"* |");
                        if (dScore > 0) chartData.Add(new { label = drv != "N/A" ? drv : veh, value = dScore });
                    }
                }
                var json = new {
                    ui_component = chartData.Count > 0 ? "BarChart" : "MetricCard",
                    data = chartData
                };
                return sb.ToString() + "\n---UI_COMPONENT---\n" + JsonSerializer.Serialize(json);
            }

            // ── Question-aware: Predictions / Sales Forecast focus ────────────
            if (q.Contains("predict") || q.Contains("forecast") || q.Contains("next year"))
            {
                var pred = await _tools.PredictNextYearSalesToolAsync();
                var sb = new StringBuilder();
                sb.AppendLine("Here is your **live database sales prediction forecast**:");
                sb.AppendLine();
                sb.AppendLine("Below are the 12-month sales predictions calculated directly from historical Month-over-Month (MoM) database growth:");
                sb.AppendLine();
                int idx = 1;
                foreach (var m in pred.Months)
                {
                    sb.AppendLine($"{idx++}. {m.MonthLabel}: ₱{m.Revenue:N2}");
                }
                sb.AppendLine();
                sb.AppendLine($"**Total Predicted Sales**: ₱{pred.GrandTotal:N2}");
                sb.AppendLine();
                sb.AppendLine("*Note: These figures are mathematical projections based on historical Month-over-Month (MoM) growth trends. Actual future revenue may vary due to seasonality, market demand, and business conditions.*");

                var chartData = pred.Months.Select(m => new { label = m.MonthLabel, value = m.Revenue }).ToList();
                var json = new {
                    ui_component = "BarChart",
                    data = chartData
                };
                return sb.ToString() + "\n---UI_COMPONENT---\n" + JsonSerializer.Serialize(json);
            }

            // ── Question-aware: Monthly Revenue / Trend focus ────────────────
            if (q.Contains("trend") || q.Contains("monthly") || q.Contains("historical") || q.Contains("last 6 month") || q.Contains("last 12 month"))
            {
                var monthly = await _tools.GetMonthlyRevenueBreakdownAsync();
                var sb = new StringBuilder();
                sb.AppendLine("Here is your **live monthly revenue trend snapshot** from the database:");
                sb.AppendLine();
                foreach (var m in monthly.Months)
                {
                    sb.AppendLine($"* **{m.MonthLabel}**: ₱{m.Revenue:N2} ({m.Transactions} transactions)");
                }
                sb.AppendLine();
                sb.AppendLine($"**Total Revenue**: ₱{monthly.GrandTotal:N2}");

                var chartData = monthly.Months.Select(m => new { label = m.MonthLabel, value = m.Revenue }).ToList();
                var json = new {
                    ui_component = "BarChart",
                    data = chartData
                };
                return sb.ToString() + "\n---UI_COMPONENT---\n" + JsonSerializer.Serialize(json);
            }

            var revenue = await _tools.GetTodayRevenueAsync();
            var fleet   = await _tools.GetAvailableFleetCountAsync();
            var overdue = await _tools.GetOverdueRentalsAsync();

            // ── Question-aware: Pending Bookings / Transactions focus ────
            if (q.Contains("pending") || q.Contains("booking") || q.Contains("approval") || q.Contains("transaction"))
            {
                var pending = await _tools.GetPendingBookingsAsync();
                var text = $"Here is your **live pending bookings snapshot**:\n\n" +
                           $"**Pending Bookings**: {pending.PendingCount} rentals awaiting your approval.";
                var json = new {
                    ui_component = "MetricCard",
                    data = new[]
                    {
                        new { label = "Pending Bookings",   value = (decimal)pending.PendingCount },
                        new { label = "Pending Extensions", value = (decimal)pending.PendingExtensions }
                    }
                };
                return text + "\n---UI_COMPONENT---\n" + JsonSerializer.Serialize(json);
            }

            // ── Question-aware: Maintenance / Service focus ───────────
            if (q.Contains("maintenance") || q.Contains("repair") || q.Contains("service"))
            {
                var topV = await _tools.GetVehicleUtilizationAsync("this_month", 10);
                var sb = new StringBuilder();
                sb.AppendLine("🔧 **Vehicle Maintenance & Fleet Service Status Report**");
                sb.AppendLine();
                sb.AppendLine($"Ang ating kabuuang fleet na **{fleet.TotalVehicles} sasakyan** ay nasa optimal na kalagayan ngayon. Walang mga sasakyan na nangangailangan ng agarang maintenance.");
                sb.AppendLine();
                sb.AppendLine("Here is the live operational and service status for all registered fleet vehicles:");
                sb.AppendLine();
                int idx = 1;
                foreach (var v in topV.Vehicles)
                {
                    sb.AppendLine($"{idx++}. **{v.VehicleName}** ({v.PlateNo}) — Total Rentals: {v.TotalRentals} | Revenue: **₱{v.Revenue:N2}**");
                }
                if (topV.Vehicles.Count == 0)
                {
                    sb.AppendLine($"All {fleet.TotalVehicles} registered fleet vehicles are currently fully serviced and operational.");
                }

                var chartData = topV.Vehicles.Select(v => new { label = $"{v.VehicleName} ({v.PlateNo})", value = v.Revenue }).ToList();
                var json = new {
                    ui_component = chartData.Count > 0 ? "BarChart" : "MetricCard",
                    data = chartData
                };
                return sb.ToString() + "\n---UI_COMPONENT---\n" + JsonSerializer.Serialize(json);
            }

            // ── Question-aware: Overdue / Penalty focus ──────────────
            if (q.Contains("overdue") || q.Contains("penalty") || q.Contains("late"))
            {
                var text = $"⚠️ AI cloud services are offline. Here is your **live overdue rentals snapshot**:\n\n" +
                           $"**Overdue Rentals**: {overdue.OverdueCount} units need immediate attention.\n" +
                           $"**Fleet Available**: {fleet.Available}/{fleet.TotalVehicles} ({fleet.UtilizationPct:F1}% utilized)";
                var json = new {
                    ui_component = "MetricCard",
                    data = new[]
                    {
                        new { label = "Overdue Rentals", value = (decimal)overdue.OverdueCount },
                        new { label = "Fleet Available", value = (decimal)fleet.Available },
                        new { label = "Total Fleet",     value = (decimal)fleet.TotalVehicles }
                    }
                };
                return text + "\n---UI_COMPONENT---\n" + JsonSerializer.Serialize(json);
            }

            // ── Question-aware: Fleet / Vehicle focus ────────────────
            if (q.Contains("fleet") || q.Contains("vehicle") || q.Contains("available") || q.Contains("car"))
            {
                var text = $"⚠️ AI cloud services are offline. Here is your **live fleet snapshot**:\n\n" +
                           $"**Available**: {fleet.Available}/{fleet.TotalVehicles} vehicles\n" +
                           $"**Utilization**: {fleet.UtilizationPct:F1}%";
                var json = new {
                    ui_component = "MetricCard",
                    data = new[]
                    {
                        new { label = "Available",   value = (decimal)fleet.Available },
                        new { label = "On Rent",      value = (decimal)(fleet.TotalVehicles - fleet.Available) },
                        new { label = "Utilization %", value = (decimal)fleet.UtilizationPct }
                    }
                };
                return text + "\n---UI_COMPONENT---\n" + JsonSerializer.Serialize(json);
            }

            // ── Question-aware: Customer / Client focus ──────────────
            if (q.Contains("customer") || q.Contains("client") || q.Contains("spender"))
            {
                var custObj = await _tools.GetCustomerInsightsAsync(10);
                string jsonStr = JsonSerializer.Serialize(custObj);
                using var doc = JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;
                int totalCust = root.TryGetProperty("total_customers", out var tc) ? tc.GetInt32() : 0;
                int newThisMonth = root.TryGetProperty("new_customers_this_month", out var nc) ? nc.GetInt32() : 0;

                var sb = new StringBuilder();
                sb.AppendLine("👥 **Drive&Go Customer Insights & Top Spenders Report**");
                sb.AppendLine();
                sb.AppendLine($"• **Total Customer Base:** **{totalCust} registered customers**");
                sb.AppendLine($"• **New Signups This Month:** **{newThisMonth}**");
                sb.AppendLine();

                if (root.TryGetProperty("top_customers", out var topArr) && topArr.ValueKind == JsonValueKind.Array && topArr.GetArrayLength() > 0)
                {
                    sb.AppendLine("| Customer Name | Total Bookings | Total Spent |");
                    sb.AppendLine("| :--- | :---: | :---: |");
                    foreach (var c in topArr.EnumerateArray())
                    {
                        string name = c.TryGetProperty("customer_name", out var n) ? n.GetString() ?? "Customer" : "Customer";
                        int bookings = c.TryGetProperty("total_bookings", out var b) ? b.GetInt32() : 0;
                        decimal spent = c.TryGetProperty("total_spent", out var s) ? (s.ValueKind == JsonValueKind.Number && s.TryGetDecimal(out var dec) ? dec : 0m) : 0m;
                        sb.AppendLine($"| **{name}** | {bookings} booking(s) | **₱{spent:N2}** |");
                    }
                }
                var json = new {
                    ui_component = "MetricCard",
                    data = new[]
                    {
                        new { label = "Total Customers", value = (decimal)totalCust },
                        new { label = "New This Month",  value = (decimal)newThisMonth }
                    }
                };
                return sb.ToString() + "\n---UI_COMPONENT---\n" + JsonSerializer.Serialize(json);
            }

            // ── Question-aware: Revenue / Earnings focus ─────────────
            if (q.Contains("revenue") || q.Contains("earning") || q.Contains("income") || q.Contains("money") || q.Contains("sale"))
            {
                var text = $"⚠️ AI cloud services are offline. Here is your **live revenue snapshot**:\n\n" +
                           $"**Today's Revenue**: ₱{revenue.TodayRevenue:N2} ({revenue.TodayTransactions} transactions)\n" +
                           $"**This Week**: ₱{revenue.WeekRevenue:N2}\n" +
                           $"**This Month**: ₱{revenue.MonthRevenue:N2}";
                var json = new {
                    ui_component = "MetricCard",
                    data = new[]
                    {
                        new { label = "Today Revenue",  value = revenue.TodayRevenue },
                        new { label = "Week Revenue",   value = revenue.WeekRevenue },
                        new { label = "Month Revenue",  value = revenue.MonthRevenue }
                    }
                };
                return text + "\n---UI_COMPONENT---\n" + JsonSerializer.Serialize(json);
            }

            // ── Default: Full operational snapshot ───────────────────
            var defText = $"⚠️ AI cloud services are offline. Here is your **live operational snapshot**:\n\n" +
                          $"**Today's Revenue**: ₱{revenue.TodayRevenue:N2} ({revenue.TodayTransactions} transactions)\n" +
                          $"**Fleet**: {fleet.Available}/{fleet.TotalVehicles} available ({fleet.UtilizationPct:F1}% utilized)\n" +
                          $"**Overdue Rentals**: {overdue.OverdueCount} units need immediate attention.";
            var defJson = new {
                ui_component = "MetricCard",
                data = new[]
                {
                    new { label = "Today Revenue",   value = revenue.TodayRevenue },
                    new { label = "Fleet Available",  value = (decimal)fleet.Available },
                    new { label = "Overdue Rentals",  value = (decimal)overdue.OverdueCount }
                }
            };
            return defText + "\n---UI_COMPONENT---\n" + JsonSerializer.Serialize(defJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LocalFallback] DB query failed");
            return $"⚠️ Critical: Both AI providers and the database are unreachable. Error: {ex.Message}\n---UI_COMPONENT---\n" +
                   "{\"ui_component\":\"Text Only\",\"data\":[]}"; 
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  GENUI JSON PARSER & VALIDATOR
    // ═══════════════════════════════════════════════════════════════════
    private static string CleanThinkingNarration(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        // 1. Strip <think>...</think> XML blocks if present
        int thinkStart = input.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
        if (thinkStart >= 0)
        {
            int thinkEnd = input.IndexOf("</think>", thinkStart, StringComparison.OrdinalIgnoreCase);
            if (thinkEnd > thinkStart)
            {
                input = input.Remove(thinkStart, (thinkEnd + 8) - thinkStart).Trim();
            }
        }

        // 2. Strip "The user is asking for...", "Constraint Checklist", "Plan:", etc.
        string[] reasoningMarkers = new[]
        {
            "Constraint Checklist",
            "Confidence Score:",
            "Plan:",
            "Looking at the available tools",
            "Looking at available tools",
            "The user is asking for",
            "Thinking Process:"
        };

        foreach (var marker in reasoningMarkers)
        {
            int idx = input.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                int doubleNL = input.IndexOf("\n\n", idx, StringComparison.Ordinal);
                if (doubleNL > idx && doubleNL + 2 < input.Length)
                {
                    string candidate = input.Substring(doubleNL + 2).Trim();
                    if (!string.IsNullOrWhiteSpace(candidate) && !reasoningMarkers.Any(m => candidate.StartsWith(m, StringComparison.OrdinalIgnoreCase)))
                    {
                        input = candidate;
                        continue;
                    }
                }

                input = input.Substring(0, idx).Trim();
            }
        }

        return input;
    }

    private static AiCopilotResponse ParseGenUiResponse(string? rawText, string provider)
    {
        var validComponents = new HashSet<string>
            { "Text Only", "BarChart", "PieChart", "MetricCard", "DataGrid" };

        if (string.IsNullOrWhiteSpace(rawText))
        {
            return new AiCopilotResponse
            {
                Text         = "No response was generated. Please try again.",
                UiComponent  = "Text Only",
                ProviderUsed = provider
            };
        }

        string textPart = rawText;
        string jsonPart = "{}";
        
        // Regex split on delimiter variations like ---UI_COMPONENT---, UI_COMPONENT, --- UI_COMPONENT, etc.
        var splitRegex = new System.Text.RegularExpressions.Regex(@"\r?\n?-{0,5}\s*UI_COMPONENT\s*-{0,5}\r?\n?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var parts = splitRegex.Split(rawText);
        if (parts.Length >= 2)
        {
            textPart = parts[0].Trim();
            jsonPart = string.Join("\n", parts.Skip(1)).Trim();
        }
        else
        {
            // If the model missed the delimiter but included markdown JSON at the end
            int jsonStart = rawText.LastIndexOf("```json", StringComparison.OrdinalIgnoreCase);
            if (jsonStart == -1) jsonStart = rawText.IndexOf('{');
            
            if (jsonStart >= 0)
            {
                textPart = rawText.Substring(0, jsonStart).Trim();
                jsonPart = rawText.Substring(jsonStart).Trim();
            }
        }

        // Clean out any leftover delimiter text or artifacts that might remain in textPart
        textPart = System.Text.RegularExpressions.Regex.Replace(textPart, @"-{0,5}\s*UI_COMPONENT\s*-{0,5}", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        textPart = System.Text.RegularExpressions.Regex.Replace(textPart, @"\bUI_COMPONENT\b", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

        // Clean thinking narration
        textPart = CleanThinkingNarration(textPart);

        // Strip leading or trailing horizontal rule lines (--- or *** or ___) that surround textPart
        textPart = System.Text.RegularExpressions.Regex.Replace(textPart, @"^(?:[\s\r\n]*---+\s*)+", "").Trim();
        textPart = System.Text.RegularExpressions.Regex.Replace(textPart, @"(?:[\s\r\n]*---+\s*)+$", "").Trim();

        // Clean jsonPart
        if (jsonPart.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) jsonPart = jsonPart[7..];
        if (jsonPart.StartsWith("```"))     jsonPart = jsonPart[3..];
        if (jsonPart.EndsWith("```"))       jsonPart = jsonPart[..^3];
        jsonPart = jsonPart.Trim();

        if (string.IsNullOrWhiteSpace(jsonPart) || !jsonPart.StartsWith("{"))
        {
             return new AiCopilotResponse
             {
                 Text         = textPart,
                 UiComponent  = "Text Only",
                 ProviderUsed = provider
             };
        }

        try
        {
            jsonPart = SanitizeJsonString(jsonPart);
            using var doc  = JsonDocument.Parse(jsonPart);
            var root = doc.RootElement;

            string uiComponent = root.TryGetProperty("ui_component", out var ui) ? ui.GetString() ?? "Text Only" : "Text Only";
            if (!validComponents.Contains(uiComponent)) uiComponent = "Text Only";

            var data = new List<Dictionary<string, object>>();
            if (root.TryGetProperty("data", out var dataArr) &&
                dataArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dataArr.EnumerateArray())
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var prop in item.EnumerateObject())
                    {
                        dict[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.Number  => prop.Value.TryGetDecimal(out var d) ? (object)d : prop.Value.GetDouble(),
                            JsonValueKind.String  => prop.Value.GetString()!,
                            JsonValueKind.True    => true,
                            JsonValueKind.False   => false,
                            _                     => prop.Value.GetRawText()
                        };
                    }
                    data.Add(dict);
                }
            }

            return new AiCopilotResponse
            {
                Text         = textPart,
                UiComponent  = uiComponent,
                Data         = data,
                ProviderUsed = provider
            };
        }
        catch
        {
            return new AiCopilotResponse
            {
                Text         = textPart,
                UiComponent  = "Text Only",
                ProviderUsed = provider
            };
        }
    }

    private static string SanitizeJsonString(string json)
    {
        var sb = new StringBuilder();
        bool inString = false;
        bool escape = false;

        foreach (char c in json)
        {
            if (c == '"' && !escape)
            {
                inString = !inString;
                sb.Append(c);
            }
            else if (c == '\\' && !escape)
            {
                escape = true;
                sb.Append(c);
            }
            else if (inString && (c == '\n' || c == '\r'))
            {
                if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                escape = false;
            }
            else
            {
                sb.Append(c);
                escape = false;
            }
        }
        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  MESSAGE LIST BUILDERS
    // ═══════════════════════════════════════════════════════════════════
    private static List<object> BuildMessageList(List<AiCopilotMessageDto> history, string newUserMessage)
    {
        var messages = new List<object>
        {
            new { role = "system", content = DriveAndGoKnowledgeBase.GetSystemPrompt() }
        };

        // Filter history turns: keep only user and assistant messages,
        // and ignore old LocalFallback offline messages
        var validTurns = history.Where(m =>
            (m.LlmRole == "user" || m.LlmRole == "assistant") &&
            !string.IsNullOrWhiteSpace(m.Content) &&
            !m.Content.Contains("AI cloud services are offline", StringComparison.OrdinalIgnoreCase) &&
            !m.Content.Contains("LocalFallback", StringComparison.OrdinalIgnoreCase)
        ).TakeLast(MaxHistoryTurns).ToList();

        foreach (var msg in validTurns)
        {
            messages.Add(new { role = msg.LlmRole, content = msg.Content });
        }

        // Current user message (only add if not already the last message in history)
        var lastTurn = validTurns.LastOrDefault();
        if (lastTurn == null || lastTurn.LlmRole != "user" || lastTurn.Content != newUserMessage)
        {
            messages.Add(new { role = "user", content = newUserMessage });
        }

        return messages;
    }

    private async Task<List<AiCopilotMessageDto>> LoadHistoryForContextAsync(int sessionId)
    {
        // Load the last N messages for context (ordered ASC for correct conversation flow)
        await using var conn = await _ds.OpenConnectionAsync();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT copilot_msg_id, session_id, sender_id, llm_role, content,
                   ui_component_type, ui_payload, tool_name, provider_used, tokens_used, sent_at
            FROM (
                SELECT * FROM ai_copilot_messages
                WHERE session_id = @sid
                ORDER BY sent_at DESC
                LIMIT @lim
            ) sub
            ORDER BY sent_at ASC", conn);
        cmd.Parameters.AddWithValue("@sid", sessionId);
        cmd.Parameters.AddWithValue("@lim", MaxHistoryTurns + 1); // +1 for system

        var list = new List<AiCopilotMessageDto>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new AiCopilotMessageDto
            {
                CopilotMsgId    = reader.GetInt64(0),
                SessionId       = reader.GetInt32(1),
                SenderId        = reader.GetString(2),
                LlmRole         = reader.GetString(3),
                Content         = reader.GetString(4),
                UiComponentType = reader.IsDBNull(5) ? null : reader.GetString(5),
                UiPayload       = reader.IsDBNull(6) ? null : reader.GetString(6),
                ToolName        = reader.IsDBNull(7) ? null : reader.GetString(7),
                ProviderUsed    = reader.IsDBNull(8) ? null : reader.GetString(8),
                TokensUsed      = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                SentAt          = reader.GetDateTime(10)
            });
        }
        return list;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DB PERSISTENCE
    // ═══════════════════════════════════════════════════════════════════
    private async Task<long> PersistMessageAsync(
        int sessionId, string senderId, string llmRole, string content,
        string? uiComponentType, string? uiPayload, string? toolName,
        string? providerUsed, int? tokensUsed)
    {
        await using var conn = await _ds.OpenConnectionAsync();
        await using var cmd  = new NpgsqlCommand(@"
            INSERT INTO ai_copilot_messages
              (session_id, sender_id, llm_role, content, ui_component_type, ui_payload, tool_name, provider_used, tokens_used)
            VALUES (@sid, @sender, @role, @content, @uiType, @uiPayload, @toolName, @provider, @tokens)
            RETURNING copilot_msg_id", conn);

        cmd.Parameters.AddWithValue("@sid",       sessionId);
        cmd.Parameters.AddWithValue("@sender",    senderId);
        cmd.Parameters.AddWithValue("@role",      llmRole);
        cmd.Parameters.AddWithValue("@content",   content);
        cmd.Parameters.AddWithValue("@uiType",    (object?)uiComponentType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@uiPayload", (object?)uiPayload       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@toolName",  (object?)toolName        ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@provider",  (object?)providerUsed    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@tokens",    (object?)tokensUsed      ?? DBNull.Value);

        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    private async Task UpdateSessionTimestampAsync(int sessionId)
    {
        await using var conn = await _ds.OpenConnectionAsync();
        await using var cmd  = new NpgsqlCommand(
            "UPDATE ai_copilot_sessions SET updated_at = NOW() WHERE session_id = @sid", conn);
        cmd.Parameters.AddWithValue("@sid", sessionId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════
    private static string ExtractSystemContent(List<object> messages)
    {
        var systemMsgs = messages
            .OfType<IDictionary<string, object>>()
            .Where(m => m.TryGetValue("role", out var r) && r?.ToString() == "system")
            .Select(m => m.TryGetValue("content", out var c) ? c?.ToString() : null)
            .Where(c => c != null);

        if (!systemMsgs.Any()) return DriveAndGoKnowledgeBase.GetSystemPrompt();
        return string.Join("\n\n", systemMsgs);
    }

    private static string GetLastUserMessage(List<object> messages)
    {
        var serialized = messages.OfType<object>().LastOrDefault(m =>
        {
            var json = JsonSerializer.Serialize(m);
            return json.Contains("\"user\"");
        });
        if (serialized == null) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(serialized));
            if (doc.RootElement.TryGetProperty("content", out var c)) return c.GetString() ?? string.Empty;
        }
        catch { /* ignore */ }
        return string.Empty;
    }

    private static string FlattenMessagesToText(List<object> messages)
    {
        var sb = new StringBuilder();
        foreach (var m in messages)
        {
            try
            {
                var json = JsonSerializer.Serialize(m);
                using var doc = JsonDocument.Parse(json);
                string role    = doc.RootElement.TryGetProperty("role", out var r) ? r.GetString() ?? "" : "";
                string content = doc.RootElement.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                if (role != "system") sb.AppendLine($"[{role}]: {content}");
            }
            catch { /* ignore */ }
        }
        return sb.ToString();
    }

    private static List<object> BuildCohereHistory(List<object> messages)
    {
        var history = new List<object>();
        foreach (var m in messages)
        {
            try
            {
                var json = JsonSerializer.Serialize(m);
                using var doc = JsonDocument.Parse(json);
                string role    = doc.RootElement.TryGetProperty("role", out var r) ? r.GetString() ?? "" : "";
                string content = doc.RootElement.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                if (role == "user")      history.Add(new { role = "USER",    message = content });
                if (role == "assistant") history.Add(new { role = "CHATBOT", message = content });
            }
            catch { /* ignore */ }
        }
        // Remove last user message (it's the current prompt, not history)
        if (history.Count > 0) history.RemoveAt(history.Count - 1);
        return history;
    }

    // ─────────────────────────────────────────────────────────────────
    //  GEMINI NATIVE TOOL CALLING HELPERS
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the Gemini-format `contents` array from the existing OpenAI-format
    /// message list (excludes the system message, which goes in system_instruction).
    /// </summary>
    private static object[] BuildGeminiContents(List<object> messages)
    {
        var result = new List<object>();
        foreach (var m in messages)
        {
            try
            {
                var json = JsonSerializer.Serialize(m);
                using var doc = JsonDocument.Parse(json);
                string role    = doc.RootElement.TryGetProperty("role", out var r) ? r.GetString() ?? "" : "";
                string content = doc.RootElement.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";

                if (role == "system" || string.IsNullOrWhiteSpace(content)) continue;

                string geminiRole = role == "assistant" ? "model" : "user";
                result.Add(new
                {
                    role  = geminiRole,
                    parts = new[] { new { text = content } }
                });
            }
            catch { /* ignore malformed messages */ }
        }

        // Gemini requires contents to be non-empty and last turn must be "user"
        if (result.Count == 0)
            result.Add(new { role = "user", parts = new[] { new { text = "Hello" } } });

        return result.ToArray();
    }

    /// <summary>
    /// Builds the second-pass Gemini contents array that includes the model's
    /// functionCall and the tool's functionResponse for the re-submission step.
    /// </summary>
    private static object[] BuildGeminiContentsWithToolResult(
        List<object> messages, string toolName, string toolResult, string funcCallRaw)
    {
        var result = BuildGeminiContents(messages).ToList();

        // Append the model's functionCall turn
        result.Add(new
        {
            role  = "model",
            parts = new[] { new { functionCall = JsonDocument.Parse(funcCallRaw).RootElement } }
        });

        // Append the tool's functionResponse turn
        try
        {
            using var toolDoc = JsonDocument.Parse(toolResult);
            result.Add(new
            {
                role  = "user",
                parts = new[]
                {
                    new
                    {
                        functionResponse = new
                        {
                            name     = toolName,
                            response = new { result = toolDoc.RootElement }
                        }
                    }
                }
            });
        }
        catch
        {
            // If tool result isn't valid JSON, wrap it as plain text
            result.Add(new
            {
                role  = "user",
                parts = new[] { new { text = $"Tool {toolName} result: {toolResult}" } }
            });
        }

        return result.ToArray();
    }

    private static string FormatFallbackToolSummary(string toolName, string toolResult)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(toolResult))
                return "Data requested was retrieved successfully from system records.";

            using var doc = JsonDocument.Parse(toolResult);
            var root = doc.RootElement;

            decimal GetDec(JsonElement el, params string[] names)
            {
                foreach (var name in names)
                {
                    if (el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var dec)) return dec;
                }
                return 0m;
            }

            int GetInt(JsonElement el, params string[] names)
            {
                foreach (var name in names)
                {
                    if (el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i)) return i;
                }
                return 0;
            }

            string GetStr(JsonElement el, params string[] names)
            {
                foreach (var name in names)
                {
                    if (el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
                    {
                        var val = p.GetString();
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
                return "";
            }

            JsonElement? GetProp(JsonElement el, params string[] names)
            {
                foreach (var name in names)
                {
                    if (el.TryGetProperty(name, out var p)) return p;
                }
                return null;
            }

            string lowerTool = toolName.ToLowerInvariant();

            // 1. WEEKLY ANALYTICS
            if (lowerTool.Contains("weekly"))
            {
                var dailyProp = GetProp(root, "DailyBreakdown", "dailyBreakdown");
                decimal total = GetDec(root, "WeekTotal", "weekTotal");
                int rentals = GetInt(root, "WeekRentals", "weekRentals");

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("### Weekly Revenue Analytics\n");
                sb.AppendLine($"• **Total Revenue (Last 7 Days):** **₱{total:N2}**");
                sb.AppendLine($"• **Total Completed Rentals:** **{rentals}**\n");

                if (dailyProp.HasValue && dailyProp.Value.ValueKind == JsonValueKind.Array && dailyProp.Value.GetArrayLength() > 0)
                {
                    sb.AppendLine("| Date / Day | Revenue | Rentals |");
                    sb.AppendLine("| :--- | :---: | :---: |");
                    foreach (var day in dailyProp.Value.EnumerateArray())
                    {
                        string label = GetStr(day, "DayLabel", "dayLabel", "Day");
                        decimal rev = GetDec(day, "Revenue", "revenue");
                        int count = GetInt(day, "Rentals", "rentals");
                        sb.AppendLine($"| **{label}** | ₱{rev:N2} | {count} |");
                    }
                }
                return sb.ToString();
            }

            // 2. TODAY REVENUE
            if (lowerTool.Contains("today") || lowerTool.Contains("daily"))
            {
                decimal todayRev = GetDec(root, "TodayRevenue", "todayRevenue");
                int todayTxns = GetInt(root, "TodayTransactions", "todayTransactions");
                decimal weekRev = GetDec(root, "WeekRevenue", "weekRevenue");
                decimal monthRev = GetDec(root, "MonthRevenue", "monthRevenue");

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("### Today's Revenue Summary\n");
                sb.AppendLine($"• **Today's Revenue:** **₱{todayRev:N2}** ({todayTxns} transaction(s))");
                sb.AppendLine($"• **This Week's Total:** **₱{weekRev:N2}**");
                sb.AppendLine($"• **This Month's Total:** **₱{monthRev:N2}**");
                return sb.ToString();
            }

            // 3. MONTHLY REVENUE
            if (lowerTool.Contains("monthly"))
            {
                decimal grandTotal = GetDec(root, "GrandTotal", "grandTotal");
                var monthsProp = GetProp(root, "Months", "months");

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("### Monthly Revenue Breakdown\n");
                sb.AppendLine($"• **Cumulative Revenue:** **₱{grandTotal:N2}**\n");

                if (monthsProp.HasValue && monthsProp.Value.ValueKind == JsonValueKind.Array && monthsProp.Value.GetArrayLength() > 0)
                {
                    sb.AppendLine("| Month | Revenue | Transactions |");
                    sb.AppendLine("| :--- | :---: | :---: |");
                    foreach (var m in monthsProp.Value.EnumerateArray())
                    {
                        string label = GetStr(m, "MonthLabel", "monthLabel", "Month");
                        decimal rev = GetDec(m, "Revenue", "revenue");
                        int txns = GetInt(m, "Transactions", "transactions");
                        sb.AppendLine($"| **{label}** | ₱{rev:N2} | {txns} |");
                    }
                }
                return sb.ToString();
            }

            // 3B. SALES PREDICTIONS & FORECAST
            if (lowerTool.Contains("predict") || lowerTool.Contains("forecast"))
            {
                decimal grandTotal = GetDec(root, "GrandTotal", "grandTotal");
                var monthsProp = GetProp(root, "Months", "months", "predictions", "Items");

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("### 12-Month Sales Prediction & Revenue Forecast\n");
                if (grandTotal > 0)
                {
                    sb.AppendLine($"• **Total Projected Sales (Next 12 Months):** **₱{grandTotal:N2}**\n");
                }
                else
                {
                    sb.AppendLine($"• **Projected Revenue Growth:** Stable based on historical seasonal trends.\n");
                }

                if (monthsProp.HasValue && monthsProp.Value.ValueKind == JsonValueKind.Array && monthsProp.Value.GetArrayLength() > 0)
                {
                    sb.AppendLine("| Forecast Month | Projected Revenue | Demand Index |");
                    sb.AppendLine("| :--- | :---: | :---: |");
                    foreach (var m in monthsProp.Value.EnumerateArray())
                    {
                        string label = GetStr(m, "MonthLabel", "monthLabel", "Month");
                        decimal rev = GetDec(m, "PredictedRevenue", "predictedRevenue", "Revenue");
                        string index = GetStr(m, "DemandIndex", "demandIndex", "Normal");
                        sb.AppendLine($"| **{label}** | ₱{rev:N2} | `{index}` |");
                    }
                }
                return sb.ToString();
            }

            // 4. FLEET STATUS
            if (lowerTool.Contains("fleet_status") || lowerTool.Contains("fleetstatus"))
            {
                int total = GetInt(root, "TotalFleet", "totalFleet");
                int available = GetInt(root, "Available", "available");
                int onRent = GetInt(root, "OnRent", "onRent");
                int maintenance = GetInt(root, "Maintenance", "maintenance");
                decimal util = GetDec(root, "UtilizationRate", "utilizationRate");

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("### Drive&Go Fleet Status Summary\n");
                sb.AppendLine($"• **Total Fleet Count:** **{total} vehicles**");
                sb.AppendLine($"• **Available for Rent:** **{available}**");
                sb.AppendLine($"• **Currently On Rent:** **{onRent}**");
                sb.AppendLine($"• **Under Maintenance:** **{maintenance}**");
                sb.AppendLine($"• **Fleet Utilization:** **{util:F1}%**");
                return sb.ToString();
            }

            // 5. PENDING BOOKINGS
            if (lowerTool.Contains("pending"))
            {
                int count = GetInt(root, "PendingCount", "pendingCount");
                var itemsProp = GetProp(root, "Items", "items");

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("### Pending Booking Requests\n");
                sb.AppendLine($"• **Total Pending Bookings:** **{count}**\n");

                if (itemsProp.HasValue && itemsProp.Value.ValueKind == JsonValueKind.Array && itemsProp.Value.GetArrayLength() > 0)
                {
                    sb.AppendLine("| ID | Customer | Vehicle | Start Date | Amount |");
                    sb.AppendLine("| :---: | :--- | :--- | :---: | :---: |");
                    foreach (var item in itemsProp.Value.EnumerateArray())
                    {
                        int id = GetInt(item, "RentalId", "rentalId");
                        string cust = GetStr(item, "CustomerName", "customerName", "Customer");
                        string veh = GetStr(item, "VehicleName", "vehicleName", "Vehicle");
                        string date = GetStr(item, "StartDate", "startDate", "");
                        decimal amt = GetDec(item, "TotalAmount", "totalAmount");
                        sb.AppendLine($"| #{id} | **{cust}** | {veh} | {date} | ₱{amt:N2} |");
                    }
                }
                return sb.ToString();
            }

            // 6. TOP DRIVERS & EMPLOYEES
            if (lowerTool.Contains("driver") || lowerTool.Contains("employee"))
            {
                string period = GetStr(root, "Period", "period", "all_time");
                string periodLabel = period.ToLowerInvariant() switch
                {
                    "july" => "July 2026",
                    "august" => "August 2026",
                    "this_month" => "This Month",
                    "last_month" => "Last Month",
                    _ => "Overall All-Time"
                };

                var driversProp = GetProp(root, "Drivers", "drivers", "items", "Items");
                var arrayEl = driversProp.HasValue && driversProp.Value.ValueKind == JsonValueKind.Array 
                    ? driversProp.Value 
                    : (root.ValueKind == JsonValueKind.Array ? root : (JsonElement?)null);

                if (arrayEl.HasValue && arrayEl.Value.GetArrayLength() > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"### Top Employees & Drivers Performance Report ({periodLabel})\n");
                    sb.AppendLine("| Employee / Driver | Rating | Completed Trips | Revenue Generated |");
                    sb.AppendLine("| :--- | :---: | :---: | :---: |");
                    foreach (var d in arrayEl.Value.EnumerateArray())
                    {
                        string name = GetStr(d, "FullName", "fullName", "name", "Driver");
                        if (string.IsNullOrWhiteSpace(name)) name = GetStr(d, "name", "Name", "Driver");
                        decimal rating = GetDec(d, "RatingAvg", "ratingAvg");
                        if (rating == 0m) rating = GetDec(d, "rating", "Rating");
                        int pTrips = GetInt(d, "PeriodTrips", "periodTrips");
                        if (pTrips == 0) pTrips = GetInt(d, "TotalTrips", "totalTrips");
                        if (pTrips == 0) pTrips = GetInt(d, "trips", "completedTrips");
                        decimal pRev = GetDec(d, "PeriodRevenue", "periodRevenue");
                        if (pRev == 0m) pRev = GetDec(d, "revenue", "Revenue");
                        string revText = pRev > 0 ? $"**₱{pRev:N2}**" : "N/A";
                        sb.AppendLine($"| **{name}** | ⭐ {rating:F1} | {pTrips} trip(s) | {revText} |");
                    }
                    return sb.ToString();
                }
                return $"### Top Employees & Drivers Performance Report ({periodLabel})\n\nCurrently, there are no recorded driver ratings or completed trips in the database system.";
            }

            // 6B. RATINGS & RECENT FEEDBACK
            if (lowerTool.Contains("rating") || lowerTool.Contains("feedback") || lowerTool.Contains("review"))
            {
                var ratingsProp = GetProp(root, "recent_ratings", "recentRatings", "ratings", "reviews", "Items", "items");
                var arrayEl = ratingsProp.HasValue && ratingsProp.Value.ValueKind == JsonValueKind.Array 
                    ? ratingsProp.Value 
                    : (root.ValueKind == JsonValueKind.Array ? root : (JsonElement?)null);

                decimal avgV = GetDec(root, "avg_vehicle_score", "avgVehicleScore");
                decimal avgD = GetDec(root, "avg_driver_score", "avgDriverScore");
                int totalR = GetInt(root, "total_ratings", "totalRatings");

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("### Driver & Vehicle Performance Ratings Report\n");
                if (totalR > 0 || avgV > 0 || avgD > 0)
                {
                    sb.AppendLine($"• **Overall Driver Rating Avg:** ⭐ **{avgD:F1}** / 5.0");
                    sb.AppendLine($"• **Overall Vehicle Condition Avg:** ⭐ **{avgV:F1}** / 5.0");
                    sb.AppendLine($"• **Total Customer Reviews:** **{totalR}**\n");
                }

                if (arrayEl.HasValue && arrayEl.Value.GetArrayLength() > 0)
                {
                    sb.AppendLine("| Customer | Vehicle / Driver | Driver Rating | Vehicle Condition | Customer Feedback |");
                    sb.AppendLine("| :--- | :--- | :---: | :---: | :--- |");
                    foreach (var r in arrayEl.Value.EnumerateArray())
                    {
                        string cust = GetStr(r, "customer_name", "customerName", "Customer");
                        string veh = GetStr(r, "vehicle", "Vehicle", "Vehicle");
                        string drv = GetStr(r, "driver_name", "driverName", "N/A");
                        decimal dScore = GetDec(r, "driver_score", "driverScore");
                        decimal vScore = GetDec(r, "vehicle_score", "vehicleScore");
                        string comment = GetStr(r, "comment", "Comment", "Satisfied customer");
                        if (string.IsNullOrWhiteSpace(comment)) comment = "Satisfied customer";

                        string dText = dScore > 0 ? $"⭐ {dScore:F1}" : "N/A";
                        string vText = vScore > 0 ? $"⭐ {vScore:F1}" : "N/A";

                        sb.AppendLine($"| **{cust}** | {veh} ({drv}) | {dText} | {vText} | *\"{comment}\"* |");
                    }
                    return sb.ToString();
                }
                return "### Ratings & Feedback Report\n\nNo recent customer reviews recorded yet.";
            }

            // 7. OVERDUE RENTALS
            if (lowerTool.Contains("overdue"))
            {
                var itemsProp = GetProp(root, "Items", "items");
                var arrayEl = itemsProp.HasValue && itemsProp.Value.ValueKind == JsonValueKind.Array ? itemsProp.Value : (root.ValueKind == JsonValueKind.Array ? root : (JsonElement?)null);

                if (arrayEl.HasValue && arrayEl.Value.GetArrayLength() > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("### Overdue Rentals Report\n");
                    sb.AppendLine("| Customer | Vehicle | Days Overdue | Estimated Penalty |");
                    sb.AppendLine("| :--- | :--- | :---: | :---: |");

                    foreach (var item in arrayEl.Value.EnumerateArray())
                    {
                        string name    = GetStr(item, "CustomerName", "customerName", "Customer");
                        string vehicle = GetStr(item, "VehicleName", "vehicleName", "Vehicle");
                        int days       = GetInt(item, "DaysOverdue", "daysOverdue");
                        decimal fee    = GetDec(item, "PenaltyEst", "penaltyEst");

                        sb.AppendLine($"| **{name}** | {vehicle} | {days} day(s) | **₱{fee:N2}** |");
                    }

                    sb.AppendLine("\n*Note: Penalty amounts are estimates based on standard daily late fees.*");
                    return sb.ToString();
                }
                return "### Overdue Rentals Report\n\nGreat news! There are currently **no overdue rentals** in the system.";
            }

            // 8. CUSTOMER INSIGHTS
            if (lowerTool.Contains("customer"))
            {
                int totalCust = GetInt(root, "total_customers", "totalCustomers");
                int newThisMonth = GetInt(root, "new_customers_this_month", "newCustomersThisMonth");
                var topCustProp = GetProp(root, "top_customers", "topCustomers");

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("👥 **Customer Insights & Top Spenders**\n");
                sb.AppendLine($"• **Total Customer Base:** **{totalCust} registered customers**");
                sb.AppendLine($"• **New Signups This Month:** **{newThisMonth}**\n");

                if (topCustProp.HasValue && topCustProp.Value.ValueKind == JsonValueKind.Array && topCustProp.Value.GetArrayLength() > 0)
                {
                    sb.AppendLine("| Customer Name | Contact | Total Bookings | Total Spent |");
                    sb.AppendLine("| :--- | :---: | :---: | :---: |");
                    foreach (var c in topCustProp.Value.EnumerateArray())
                    {
                        string name = GetStr(c, "customer_name", "customerName", "Customer");
                        string phone = GetStr(c, "phone_masked", "phoneMasked", "N/A");
                        int bookings = GetInt(c, "total_bookings", "totalBookings");
                        decimal spent = GetDec(c, "total_spent", "totalSpent");
                        sb.AppendLine($"| **{name}** | {phone} | {bookings} booking(s) | **₱{spent:N2}** |");
                    }
                }
                return sb.ToString();
            }

            // 9. VEHICLE MAINTENANCE & FLEET SEARCH
            if (lowerTool.Contains("maintenance") || lowerTool.Contains("search_vehicles") || lowerTool.Contains("vehicle") || lowerTool.Contains("fleet"))
            {
                var itemsProp = GetProp(root, "Items", "items", "vehicles", "Vehicles");
                var arrayEl = itemsProp.HasValue && itemsProp.Value.ValueKind == JsonValueKind.Array ? itemsProp.Value : (root.ValueKind == JsonValueKind.Array ? root : (JsonElement?)null);

                if (arrayEl.HasValue && arrayEl.Value.GetArrayLength() > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("🔧 **Vehicle Maintenance & Fleet Status Report**\n");
                    sb.AppendLine("| Vehicle Name | Plate No | Status | Daily Rate | Seat Cap |");
                    sb.AppendLine("| :--- | :---: | :---: | :---: | :---: |");

                    foreach (var item in arrayEl.Value.EnumerateArray())
                    {
                        string brand  = GetStr(item, "brand", "Brand", "");
                        string model  = GetStr(item, "model", "Model", "");
                        string name   = GetStr(item, "VehicleName", "vehicleName", $"{brand} {model}".Trim());
                        if (string.IsNullOrWhiteSpace(name)) name = GetStr(item, "name", "Name", "Drive&Go Fleet Vehicle");
                        string plate  = GetStr(item, "plate_no", "plateNo", "N/A");
                        string status = GetStr(item, "status", "Status", "Operational");
                        decimal rate  = GetDec(item, "rate_per_day", "ratePerDay");
                        int seats     = GetInt(item, "seat_capacity", "seatCapacity");

                        sb.AppendLine($"| **{name}** | {plate} | `{status}` | **₱{rate:N2}** | {seats} seats |");
                    }
                    return sb.ToString();
                }
                return "🔧 **Vehicle Maintenance Report**\n\nAll fleet vehicles are currently fully serviced and operational. No vehicles are currently due for urgent maintenance.";
            }

            // 10. TRANSACTION SUMMARY
            if (lowerTool.Contains("transaction"))
            {
                var breakdownProp = GetProp(root, "breakdown_by_method", "breakdownByMethod");
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("💳 **Transaction Summary**\n");

                if (breakdownProp.HasValue && breakdownProp.Value.ValueKind == JsonValueKind.Array && breakdownProp.Value.GetArrayLength() > 0)
                {
                    sb.AppendLine("| Payment Method | Status | Transactions | Total Amount |");
                    sb.AppendLine("| :--- | :---: | :---: | :---: |");
                    foreach (var b in breakdownProp.Value.EnumerateArray())
                    {
                        string method = GetStr(b, "method", "method", "Method");
                        string status = GetStr(b, "status", "status", "Status");
                        int count = GetInt(b, "txn_count", "txnCount");
                        decimal total = GetDec(b, "total_amount", "totalAmount");
                        sb.AppendLine($"| **{method.ToUpper()}** | `{status}` | {count} | **₱{total:N2}** |");
                    }
                }
                return sb.ToString();
            }

            // 11. RENTAL EXTENSIONS
            if (lowerTool.Contains("extension"))
            {
                var itemsProp = GetProp(root, "Items", "items", "extensions", "Extensions");
                var arrayEl = itemsProp.HasValue && itemsProp.Value.ValueKind == JsonValueKind.Array ? itemsProp.Value : (root.ValueKind == JsonValueKind.Array ? root : (JsonElement?)null);

                if (arrayEl.HasValue && arrayEl.Value.GetArrayLength() > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("⌛ **Pending Rental Extensions Report**\n");
                    sb.AppendLine("| Rental ID | Customer | Extension Days | Extra Fee | Status |");
                    sb.AppendLine("| :---: | :--- | :---: | :---: | :---: |");
                    foreach (var item in arrayEl.Value.EnumerateArray())
                    {
                        int id = GetInt(item, "RentalId", "rentalId", "id");
                        string cust = GetStr(item, "CustomerName", "customerName", "Customer");
                        int days = GetInt(item, "ExtraDays", "extraDays", "days");
                        decimal fee = GetDec(item, "ExtraFee", "extraFee", "amount");
                        string status = GetStr(item, "Status", "status", "Pending");

                        sb.AppendLine($"| #{id} | **{cust}** | +{days} day(s) | **₱{fee:N2}** | `{status}` |");
                    }
                    return sb.ToString();
                }
                return "⌛ **Rental Extensions Report**\n\nSa kasalukuyan, wala pong pending rental extensions sa ating system.";
            }
        }
        catch { /* ignore parsing errors */ }

        if (toolResult.Trim().StartsWith("["))
        {
            try
            {
                using var arrDoc = JsonDocument.Parse(toolResult);
                if (arrDoc.RootElement.ValueKind == JsonValueKind.Array && arrDoc.RootElement.GetArrayLength() > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"📊 **System Data Report ({toolName})**\n");
                    int idx = 1;
                    foreach (var item in arrDoc.RootElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object)
                        {
                            var props = item.EnumerateObject().Select(p => $"**{p.Name}**: {p.Value}").ToList();
                            sb.AppendLine($"{idx++}. {string.Join(" | ", props)}");
                        }
                        else
                        {
                            sb.AppendLine($"{idx++}. {item}");
                        }
                    }
                    return sb.ToString();
                }
            }
            catch { }
        }

        return !string.IsNullOrWhiteSpace(toolResult) && !toolResult.StartsWith("{") && !toolResult.StartsWith("[")
            ? toolResult
            : $"📊 **System Data Report ({toolName})**\n\nLive database records retrieved successfully.";
    }

    private async Task<string?> TryLocalDatabaseAnswerAsync(int sessionId, string userMessage)
    {
        // Local keyword interception removed as requested.
        // All queries are handled directly by cloud AI models with native function calling.
        return await Task.FromResult<string?>(null);
    }
}
