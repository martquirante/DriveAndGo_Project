using DriveAndGo_API.Models.AiCopilot;

namespace DriveAndGo_API.Services.Ai;

/// <summary>
/// Contract for the AI Orchestration Service — the single entry point
/// for all AI Copilot operations in the DriveAndGo backend.
/// </summary>
public interface IAiOrchestrationService
{
    /// <summary>
    /// Processes a single user message turn:
    /// 1. Loads conversation history for context.
    /// 2. Builds tool definitions.
    /// 3. Sends to AI model with multi-tier fallback.
    /// 4. Intercepts and executes any tool calls.
    /// 5. Parses and validates GenUI JSON response.
    /// 6. Persists all turns to ai_copilot_messages.
    /// 7. Returns structured AiCopilotResponse.
    /// </summary>
    Task<AiCopilotResponse> ChatAsync(int sessionId, int adminUserId, string userMessage);

    /// <summary>Retrieves paginated message history for a session.</summary>
    Task<List<AiCopilotMessageDto>> GetHistoryAsync(int sessionId, int limit = 50);

    /// <summary>Creates a new conversation session and returns its ID.</summary>
    Task<int> CreateSessionAsync(int adminUserId, string title = "New Conversation");

    /// <summary>Lists all sessions for an admin user, most recent first.</summary>
    Task<List<AiCopilotSessionDto>> GetSessionsAsync(int adminUserId);

    /// <summary>Returns contextual prompt suggestions based on current system state.</summary>
    Task<AiSuggestionsResponse> GetSuggestionsAsync();
}
