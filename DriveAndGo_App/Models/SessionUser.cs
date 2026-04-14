using System.Text.Json.Serialization;

namespace DriveAndGo_App.Models;

public sealed class SessionUser
{
    [JsonPropertyName("userId")]
    public int UserId { get; set; }

    [JsonPropertyName("driverId")]
    public int? DriverId { get; set; }

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
    public string Phone { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = "customer";

    public bool IsDriver => string.Equals(Role, "driver", StringComparison.OrdinalIgnoreCase);
    public string FirstName => string.IsNullOrWhiteSpace(FullName) ? "User" : FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? FullName;
    public string Initials
    {
        get
        {
            var parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return "DG";
            }

            if (parts.Length == 1)
            {
                return parts[0][0].ToString().ToUpperInvariant();
            }

            return string.Concat(parts[0][0], parts[^1][0]).ToUpperInvariant();
        }
    }
}
