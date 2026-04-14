using System.Text.Json.Serialization;

namespace DriveAndGo_App.Models;

public sealed class AppNotificationModel
{
    [JsonPropertyName("notifId")]
    public int NotifId { get; set; }

    [JsonPropertyName("userId")]
    public int UserId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("isRead")]
    public bool IsRead { get; set; }

    [JsonPropertyName("sentAt")]
    public DateTime SentAt { get; set; }

    public string TimeAgoLabel => SentAt.ToLocalTime().ToString("MMM dd, hh:mm tt");
}
