namespace DriveAndGo_API.Contracts;

public sealed class UpdateUserProfileRequest
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
