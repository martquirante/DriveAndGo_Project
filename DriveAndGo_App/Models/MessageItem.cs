using System.Text.Json.Serialization;

namespace DriveAndGo_App.Models;

public sealed class MessageItem
{
    [JsonPropertyName("messageId")]
    public int MessageId { get; set; }

    [JsonPropertyName("rentalId")]
    public int RentalId { get; set; }

    [JsonPropertyName("senderId")]
    public int SenderId { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("attachmentUrl")]
    public string? AttachmentUrl { get; set; }

    [JsonPropertyName("sentAt")]
    public DateTime SentAt { get; set; }

    [JsonPropertyName("senderName")]
    public string? SenderName { get; set; }

    public bool IsMine { get; set; }
    public string SentAtLabel => SentAt.ToLocalTime().ToString("hh:mm tt");

    [JsonPropertyName("isEdited")]
    public bool IsEdited { get; set; }

    [JsonPropertyName("editHistory")]
    public string? EditHistory { get; set; }

    [JsonPropertyName("isUnsent")]
    public bool IsUnsent { get; set; }

    [JsonPropertyName("hiddenFor")]
    public string? HiddenFor { get; set; }

    [JsonPropertyName("reactions")]
    public string? Reactions { get; set; }
}
