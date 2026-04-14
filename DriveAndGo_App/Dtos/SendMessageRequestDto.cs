namespace DriveAndGo_App.Dtos;

public sealed class SendMessageRequestDto
{
    public int RentalId { get; set; }
    public int SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
}
