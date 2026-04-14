using System.Text.Json.Serialization;

namespace DriveAndGo_App.Models;

public sealed class LocationPoint
{
    [JsonPropertyName("lat")]
    public decimal Latitude { get; set; }

    [JsonPropertyName("lng")]
    public decimal Longitude { get; set; }

    [JsonPropertyName("speed")]
    public decimal Speed { get; set; }

    [JsonPropertyName("time")]
    public DateTime Time { get; set; }

    public string CoordinateLabel => $"{Latitude:0.000000}, {Longitude:0.000000}";
}
