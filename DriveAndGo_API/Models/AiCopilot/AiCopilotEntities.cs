namespace DriveAndGo_API.Models.AiCopilot;

/// <summary>EF Core entity for ai_copilot_sessions table.</summary>
public class AiCopilotSession
{
    public int      SessionId    { get; set; }
    public int      AdminUserId  { get; set; }
    public string   Title        { get; set; } = "New Conversation";
    public DateTime CreatedAt    { get; set; }
    public DateTime UpdatedAt    { get; set; }
}

/// <summary>EF Core entity for ai_copilot_messages table.</summary>
public class AiCopilotMessage
{
    public long     CopilotMsgId    { get; set; }
    public int      SessionId        { get; set; }
    public string   SenderId         { get; set; } = "bot_copilot";
    public string   LlmRole          { get; set; } = "user";
    public string   Content          { get; set; } = string.Empty;
    public string?  UiComponentType  { get; set; }
    public string?  UiPayload        { get; set; }
    public string?  ToolName         { get; set; }
    public string?  ProviderUsed     { get; set; }
    public int?     TokensUsed       { get; set; }
    public DateTime SentAt           { get; set; }
}
