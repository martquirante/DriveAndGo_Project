namespace DriveAndGo_API.Contracts;

public sealed class AuthResponse
{
    public string Message { get; set; } = string.Empty;
    public int UserId { get; set; }
    public int? DriverId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = "customer";
}
