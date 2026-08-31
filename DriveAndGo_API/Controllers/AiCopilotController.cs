using DriveAndGo_API.Models.AiCopilot;
using DriveAndGo_API.Services.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriveAndGo_API.Controllers;

/// <summary>
/// AI Copilot REST API.
/// Endpoints:
///   POST   /api/ai/sessions          — Create a new conversation session
///   GET    /api/ai/sessions          — List sessions for an admin user
///   GET    /api/ai/sessions/{id}/history — Get full message history
///   POST   /api/ai/chat              — Send a message and get AI response
///   GET    /api/ai/suggestions       — Get smart contextual prompt suggestions
///   DELETE /api/ai/sessions/{id}     — Delete a session and all its messages
/// </summary>
[Route("api/ai")]
[ApiController]
public class AiCopilotController : ControllerBase
{
    private readonly IAiOrchestrationService _ai;
    private readonly ILogger<AiCopilotController> _logger;

    public AiCopilotController(IAiOrchestrationService ai, ILogger<AiCopilotController> logger)
    {
        _ai    = ai;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────
    //  POST /api/ai/sessions — Create new conversation session
    // ─────────────────────────────────────────────────────────────────
    [HttpPost("sessions")]
    [AllowAnonymous] // Allow for now; swap to [Authorize] in production
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest req)
    {
        try
        {
            if (req.AdminUserId <= 0)
                return BadRequest(new { Message = "adminUserId is required." });

            int sessionId = await _ai.CreateSessionAsync(req.AdminUserId, req.Title);
            return Ok(new
            {
                sessionId,
                title      = req.Title,
                createdAt  = DateTime.UtcNow,
                message    = "Session created. Drive&Go AI is ready."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create AI session");
            return StatusCode(500, new { Message = "Failed to create session: " + ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  GET /api/ai/sessions?adminUserId=N — List sessions
    // ─────────────────────────────────────────────────────────────────
    [HttpGet("sessions")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSessions([FromQuery] int adminUserId)
    {
        try
        {
            if (adminUserId <= 0)
                return BadRequest(new { Message = "adminUserId query param is required." });

            var sessions = await _ai.GetSessionsAsync(adminUserId);
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve sessions");
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  GET /api/ai/sessions/{sessionId}/history
    // ─────────────────────────────────────────────────────────────────
    [HttpGet("sessions/{sessionId:int}/history")]
    [AllowAnonymous]
    public async Task<IActionResult> GetHistory(int sessionId, [FromQuery] int limit = 50)
    {
        try
        {
            var history = await _ai.GetHistoryAsync(sessionId, Math.Clamp(limit, 1, 200));
            // Filter out system-role messages from public history (keep internal)
            var publicHistory = history.Where(m => m.LlmRole != "system").ToList();
            return Ok(publicHistory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve history for session {SessionId}", sessionId);
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  POST /api/ai/chat — THE MAIN CHAT ENDPOINT
    // ─────────────────────────────────────────────────────────────────
    [HttpPost("chat")]
    [AllowAnonymous]
    public async Task<IActionResult> Chat([FromBody] AiChatRequest req)
    {
        try
        {
            int targetSessionId = req.SessionId > 0 ? req.SessionId : 1;
            int targetAdminId = req.AdminUserId > 0 ? req.AdminUserId : 1;

            if (string.IsNullOrWhiteSpace(req.UserMessage))
                return BadRequest(new { Message = "userMessage cannot be empty." });

            if (req.UserMessage.Length > 2000)
                return BadRequest(new { Message = "Message too long (max 2000 characters)." });

            _logger.LogInformation(
                "AI Chat: Admin {AdminId}, Session {SessionId}, Message length {Length}",
                targetAdminId, targetSessionId, req.UserMessage.Length);

            var response = await _ai.ChatAsync(targetSessionId, targetAdminId, req.UserMessage);

            return Ok(new
            {
                sessionId    = response.SessionId,
                messageId    = response.MessageId,
                text         = response.Text,
                ui_component = response.UiComponent,
                data         = response.Data,
                providerUsed = response.ProviderUsed,
                timestamp    = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI Chat error for session {SessionId}", req.SessionId);
            return StatusCode(500, new
            {
                sessionId    = req.SessionId,
                text         = Services.UserFriendlyErrorMessage.Clean(ex.Message),
                ui_component = "Text Only",
                data         = Array.Empty<object>(),
                Message      = Services.UserFriendlyErrorMessage.Clean(ex.Message),
                errorDetails = ex.ToString()
            });
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  GET /api/ai/suggestions — Smart contextual prompt suggestions
    // ─────────────────────────────────────────────────────────────────
    [HttpGet("suggestions")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSuggestions()
    {
        try
        {
            var suggestions = await _ai.GetSuggestionsAsync();
            return Ok(suggestions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate suggestions");
            // Return static fallback suggestions on error
            return Ok(new AiSuggestionsResponse
            {
                Suggestions = new List<string>
                {
                    "Show me today's revenue breakdown.",
                    "Which rentals are overdue right now?",
                    "Give me a business health summary."
                },
                Context = "Static fallback"
            });
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  DELETE /api/ai/sessions/{sessionId} — Delete session + messages
    // ─────────────────────────────────────────────────────────────────
    [HttpDelete("sessions/{sessionId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> DeleteSession(int sessionId)
    {
        try
        {
            // Messages are cascade-deleted due to FK ON DELETE CASCADE
            var npgsql = HttpContext.RequestServices.GetRequiredService<Npgsql.NpgsqlDataSource>();
            await using var conn = await npgsql.OpenConnectionAsync();
            await using var cmd  = new Npgsql.NpgsqlCommand(
                "DELETE FROM ai_copilot_sessions WHERE session_id = @sid", conn);
            cmd.Parameters.AddWithValue("@sid", sessionId);
            int rows = await cmd.ExecuteNonQueryAsync();

            if (rows == 0) return NotFound(new { Message = $"Session {sessionId} not found." });
            return Ok(new { Message = $"Session {sessionId} and all its messages deleted." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete session {SessionId}", sessionId);
            return StatusCode(500, new { Message = ex.Message });
        }
    }
}
