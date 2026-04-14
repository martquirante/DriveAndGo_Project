namespace DriveAndGo_API.Contracts;

public sealed class CreateNotificationRequest
{
    public int UserId { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? Type { get; set; }
}
