namespace DriveAndGo_API.Models;

public class VerifyDriverRequest
{
    public bool Approve { get; set; }
    public string? Reason { get; set; }
}
