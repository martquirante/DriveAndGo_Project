namespace DriveAndGo_App.Dtos;

public sealed class CreateIssueRequestDto
{
    public int RentalId { get; set; }
    public int ReporterId { get; set; }
    public string IssueType { get; set; } = "General";
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}
