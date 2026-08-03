using System.Text.Json.Serialization;

namespace DriveAndGo_API.Models.AiCopilot;

// ═══════════════════════════════════════════════════════════════════
//  REQUEST / RESPONSE
// ═══════════════════════════════════════════════════════════════════

/// <summary>Payload sent from Admin frontend to POST /api/ai/chat</summary>
public class AiChatRequest
{
    [JsonPropertyName("sessionId")]
    public int SessionId { get; set; }

    [JsonPropertyName("adminUserId")]
    public int AdminUserId { get; set; }

    [JsonPropertyName("userMessage")]
    public string UserMessage { get; set; } = string.Empty;
}

/// <summary>Payload sent from Admin frontend to POST /api/ai/sessions</summary>
public class CreateSessionRequest
{
    [JsonPropertyName("adminUserId")]
    public int AdminUserId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "New Conversation";
}

// ═══════════════════════════════════════════════════════════════════
//  GENERATIVE UI — CORE RESPONSE
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// The canonical AI Copilot response.
/// Always contains a human-readable text, a UI component hint,
/// and an optional data array for chart rendering.
/// </summary>
public class AiCopilotResponse
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// One of: "Text Only", "BarChart", "PieChart", "MetricCard", "DataGrid"
    /// </summary>
    [JsonPropertyName("ui_component")]
    public string UiComponent { get; set; } = "Text Only";

    /// <summary>Key-value pairs for chart/table rendering on the frontend.</summary>
    [JsonPropertyName("data")]
    public List<Dictionary<string, object>> Data { get; set; } = new();

    // ── Metadata (not stored in DB response; used by controller for persistence) ──
    [JsonIgnore] public int SessionId { get; set; }
    [JsonIgnore] public long MessageId { get; set; }
    [JsonIgnore] public string ProviderUsed { get; set; } = "Unknown";
    [JsonIgnore] public int? TokensUsed { get; set; }
    [JsonIgnore] public bool IsToolResult { get; set; }
}

// ═══════════════════════════════════════════════════════════════════
//  HISTORY / SESSION DTOs
// ═══════════════════════════════════════════════════════════════════

public class AiCopilotSessionDto
{
    [JsonPropertyName("sessionId")]    public int    SessionId    { get; set; }
    [JsonPropertyName("adminUserId")] public int    AdminUserId  { get; set; }
    [JsonPropertyName("title")]       public string Title        { get; set; } = string.Empty;
    [JsonPropertyName("createdAt")]   public DateTime CreatedAt  { get; set; }
    [JsonPropertyName("updatedAt")]   public DateTime UpdatedAt  { get; set; }
    [JsonPropertyName("lastMessage")] public string LastMessage  { get; set; } = string.Empty;
}

public class AiCopilotMessageDto
{
    [JsonPropertyName("copilotMsgId")]      public long   CopilotMsgId     { get; set; }
    [JsonPropertyName("sessionId")]         public int    SessionId         { get; set; }
    [JsonPropertyName("senderId")]          public string SenderId          { get; set; } = string.Empty;
    [JsonPropertyName("llmRole")]           public string LlmRole           { get; set; } = string.Empty;
    [JsonPropertyName("content")]           public string Content            { get; set; } = string.Empty;
    [JsonPropertyName("uiComponentType")]   public string? UiComponentType  { get; set; }
    [JsonPropertyName("uiPayload")]         public string? UiPayload         { get; set; }
    [JsonPropertyName("toolName")]          public string? ToolName          { get; set; }
    [JsonPropertyName("providerUsed")]      public string? ProviderUsed      { get; set; }
    [JsonPropertyName("tokensUsed")]        public int?   TokensUsed        { get; set; }
    [JsonPropertyName("sentAt")]            public DateTime SentAt           { get; set; }
}

// ═══════════════════════════════════════════════════════════════════
//  SMART SUGGESTIONS
// ═══════════════════════════════════════════════════════════════════

public class AiSuggestionsResponse
{
    [JsonPropertyName("suggestions")]
    public List<string> Suggestions { get; set; } = new();

    [JsonPropertyName("context")]
    public string Context { get; set; } = string.Empty;
}

// ═══════════════════════════════════════════════════════════════════
//  TOOL RESULT DTOs  (returned by AiToolsService gatekeeper)
// ═══════════════════════════════════════════════════════════════════

public class TodayRevenueResult
{
    public decimal TodayRevenue       { get; set; }
    public int     TodayTransactions  { get; set; }
    public decimal WeekRevenue        { get; set; }
    public decimal MonthRevenue       { get; set; }
    public string  AsOf               { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC");
}

public class WeeklyAnalyticsResult
{
    public List<WeeklyDayData> DailyBreakdown { get; set; } = new();
    public decimal             WeekTotal      { get; set; }
    public int                 WeekRentals    { get; set; }
}

public class WeeklyDayData
{
    public string  DayLabel  { get; set; } = string.Empty;
    public decimal Revenue   { get; set; }
    public int     Rentals   { get; set; }
}

public class OverdueRentalsResult
{
    public int OverdueCount           { get; set; }
    public List<OverdueRentalItem> Items { get; set; } = new();
}

public class OverdueRentalItem
{
    public int    RentalId      { get; set; }
    public string CustomerName  { get; set; } = string.Empty;
    public string VehicleName   { get; set; } = string.Empty;
    public string EndDate       { get; set; } = string.Empty;
    public int    DaysOverdue   { get; set; }
    public decimal PenaltyEst   { get; set; }
}

public class FleetStatusResult
{
    public int TotalVehicles    { get; set; }
    public int Available        { get; set; }
    public int OnRent           { get; set; }
    public int Maintenance      { get; set; }
    public double UtilizationPct { get; set; }
    public List<VehicleStatusItem> Breakdown { get; set; } = new();
}

public class VehicleStatusItem
{
    public string Status { get; set; } = string.Empty;
    public int    Count  { get; set; }
}

public class PendingRentalsResult
{
    public int PendingCount     { get; set; }
    public int PendingExtensions { get; set; }
    public List<PendingRentalItem> Items { get; set; } = new();
}

public class PendingRentalItem
{
    public int    RentalId      { get; set; }
    public string CustomerName  { get; set; } = string.Empty;
    public string VehicleName   { get; set; } = string.Empty;
    public string StartDate     { get; set; } = string.Empty;
    public decimal TotalAmount  { get; set; }
}

public class TopDriversResult
{
    public List<TopDriverItem> Drivers { get; set; } = new();
}

public class TopDriverItem
{
    public int     DriverId    { get; set; }
    public string  FullName    { get; set; } = string.Empty;
    public decimal RatingAvg   { get; set; }
    public int     TotalTrips  { get; set; }
}

public class MonthlyRevenueResult
{
    public List<MonthlyRevenueItem> Months { get; set; } = new();
    public decimal                  GrandTotal { get; set; }
}

public class MonthlyRevenueItem
{
    public string  MonthLabel   { get; set; } = string.Empty;
    public decimal Revenue      { get; set; }
    public int     Transactions { get; set; }
}

public class VehicleUtilResult
{
    public List<VehicleUtilItem> Vehicles { get; set; } = new();
    public string Period { get; set; } = "all_time";
}

public class VehicleUtilItem
{
    public string VehicleName  { get; set; } = string.Empty;
    public string PlateNo      { get; set; } = string.Empty;
    public int    TotalRentals { get; set; }
    public decimal Revenue     { get; set; }
}
