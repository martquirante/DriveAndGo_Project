using System.Text.Json.Serialization;

namespace DriveAndGo_API.Models
{
    public class Vehicle
    {
        [JsonPropertyName("vehicle_id")]
        public int? VehicleIdAlias { get => VehicleId; set => VehicleId = value ?? 0; }
        public int VehicleId { get; set; }

        public string Brand { get; set; } = "";
        public string Model { get; set; } = "";

        [JsonPropertyName("plate_no")]
        public string? PlateNoAlias { get => PlateNo; set => PlateNo = value ?? ""; }
        public string PlateNo { get; set; } = "";

        public string Type { get; set; } = "Car";

        [JsonPropertyName("cc")]
        public int? CC { get; set; }

        public string Status { get; set; } = "available";

        [JsonPropertyName("rate_per_day")]
        public decimal? RatePerDayAlias { get => RatePerDay; set => RatePerDay = value ?? 0; }
        public decimal RatePerDay { get; set; }

        [JsonPropertyName("rate_with_driver")]
        public decimal? RateWithDriverAlias { get => RateWithDriver; set => RateWithDriver = value ?? 0; }
        public decimal RateWithDriver { get; set; }

        [JsonPropertyName("photo_url")]
        public string? PhotoUrlAlias { get => PhotoUrl; set => PhotoUrl = value ?? ""; }
        public string PhotoUrl { get; set; } = "";

        public string Description { get; set; } = "";

        [JsonPropertyName("seat_capacity")]
        public int? SeatCapacityAlias { get => SeatCapacity; set => SeatCapacity = value ?? 5; }
        public int SeatCapacity { get; set; } = 5;

        public string Transmission { get; set; } = "Automatic";

        public DateTime CreatedAt { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int? CurrentSpeed { get; set; }
        public DateTime? LastUpdate { get; set; }

        [JsonPropertyName("map_icon_url")]
        public string? Model3dUrlAlias { get => Model3dUrl; set => Model3dUrl = value ?? ""; }

        [JsonPropertyName("model_3d_url")]
        public string Model3dUrl { get; set; } = "";

        [JsonPropertyName("or_cr_url")]
        public string? OrCrUrlAlias { get => OrCrUrl; set => OrCrUrl = value ?? ""; }
        public string OrCrUrl { get; set; } = "";

        [JsonPropertyName("insurance_url")]
        public string? InsuranceUrlAlias { get => InsuranceUrl; set => InsuranceUrl = value ?? ""; }
        public string InsuranceUrl { get; set; } = "";

        [JsonPropertyName("lto_expiry_date")]
        public DateTime? LtoExpiryDate { get; set; }

        [JsonPropertyName("insurance_expiry_date")]
        public DateTime? InsuranceExpiryDate { get; set; }

        public bool InGarage { get; set; } = true;

        public string Color { get; set; } = "Pearl White";

        [JsonPropertyName("flood_risk_status")]
        public string FloodRiskStatus { get; set; } = "safe";

        [JsonPropertyName("engine_water_ingress_alert")]
        public bool EngineWaterIngressAlert { get; set; } = false;

        [JsonPropertyName("last_weather_temp")]
        public decimal LastWeatherTemp { get; set; } = 28.5m;
    }
}
