using System.Text.Json.Serialization;

namespace DriveAndGo_App.Models;

public sealed class DriverSummary
{
    [JsonPropertyName("driverId")]
    public int DriverId { get; set; }

    [JsonPropertyName("userId")]
    public int UserId { get; set; }

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "available";

    [JsonPropertyName("ratingAvg")]
    public decimal? RatingAvg { get; set; }

    [JsonPropertyName("totalTrips")]
    public int TotalTrips { get; set; }

    public string RatingLabel => RatingAvg.HasValue ? $"{RatingAvg:0.0} stars" : "New driver";
}
