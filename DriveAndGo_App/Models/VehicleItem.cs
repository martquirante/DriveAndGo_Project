using System.Text.Json.Serialization;

namespace DriveAndGo_App.Models;

public sealed class VehicleItem
{
    [JsonPropertyName("vehicleId")]
    public int VehicleId { get; set; }

    [JsonPropertyName("plateNo")]
    public string PlateNo { get; set; } = string.Empty;

    [JsonPropertyName("brand")]
    public string Brand { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("cc")]
    public int? Cc { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "available";

    [JsonPropertyName("ratePerDay")]
    public decimal RatePerDay { get; set; }

    [JsonPropertyName("rateWithDriver")]
    public decimal RateWithDriver { get; set; }

    [JsonPropertyName("photoUrl")]
    public string PhotoUrl { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("seatCapacity")]
    public int SeatCapacity { get; set; }

    [JsonPropertyName("transmission")]
    public string Transmission { get; set; } = "Automatic";

    [JsonPropertyName("model3DUrl")]
    public string Model3DUrl { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }

    [JsonPropertyName("currentSpeed")]
    public int? CurrentSpeed { get; set; }

    [JsonPropertyName("lastUpdate")]
    public DateTime? LastUpdate { get; set; }

    [JsonPropertyName("inGarage")]
    public bool InGarage { get; set; }

    public string DisplayName => $"{Brand} {Model}".Trim();
    public string CategoryLabel => string.IsNullOrWhiteSpace(Type) ? "Vehicle" : Type;
    public bool IsAvailable => string.Equals(Status, "available", StringComparison.OrdinalIgnoreCase);
    public string ImageSource => string.IsNullOrWhiteSpace(PhotoUrl) ? "logo.png" : PhotoUrl;
    public string PricePerDayLabel => $"PHP {RatePerDay:N0} / day";
    public string WithDriverLabel => RateWithDriver > 0 ? $"PHP {RateWithDriver:N0} with driver" : "Driver rate unavailable";
}
