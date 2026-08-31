using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DriveAndGo_API.Models
{
    [Table("vehicles")]
    public class Vehicle
    {
        [NotMapped]
        [JsonPropertyName("vehicle_id")]
        public int? VehicleIdAlias { get => VehicleId; set => VehicleId = value ?? 0; }

        [Column("vehicle_id")]
        public int VehicleId { get; set; }

        [Column("brand")]
        public string Brand { get; set; } = "";

        [Column("model")]
        public string Model { get; set; } = "";

        [NotMapped]
        [JsonPropertyName("plate_no")]
        public string? PlateNoAlias { get => PlateNo; set => PlateNo = value ?? ""; }

        [Column("plate_no")]
        public string PlateNo { get; set; } = "";

        [Column("type")]
        public string Type { get; set; } = "Car";

        [Column("cc")]
        [JsonPropertyName("cc")]
        public int? CC { get; set; }

        [Column("status")]
        public string Status { get; set; } = "available";

        [NotMapped]
        [JsonPropertyName("rate_per_day")]
        public decimal? RatePerDayAlias { get => RatePerDay; set => RatePerDay = value ?? 0; }

        [Column("rate_per_day")]
        public decimal RatePerDay { get; set; }

        [NotMapped]
        [JsonPropertyName("rate_with_driver")]
        public decimal? RateWithDriverAlias { get => RateWithDriver; set => RateWithDriver = value ?? 0; }

        [Column("rate_with_driver")]
        public decimal RateWithDriver { get; set; }

        [NotMapped]
        [JsonPropertyName("photo_url")]
        public string? PhotoUrlAlias { get => PhotoUrl; set => PhotoUrl = value ?? ""; }

        [Column("photo_url")]
        public string PhotoUrl { get; set; } = "";

        [NotMapped]
        [JsonPropertyName("photo_urls")]
        public System.Collections.Generic.List<string> PhotoUrls { get; set; } = new();

        [NotMapped]
        [JsonPropertyName("photoUrls")]
        public System.Collections.Generic.List<string> PhotoUrlsCamel { get => PhotoUrls; set => PhotoUrls = value ?? new(); }

        [Column("description")]
        public string Description { get; set; } = "";

        [NotMapped]
        [JsonPropertyName("seat_capacity")]
        public int? SeatCapacityAlias { get => SeatCapacity; set => SeatCapacity = value ?? 5; }

        [Column("seat_capacity")]
        public int SeatCapacity { get; set; } = 5;

        [Column("transmission")]
        public string Transmission { get; set; } = "Automatic";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("latitude")]
        public double? Latitude { get; set; }

        [Column("longitude")]
        public double? Longitude { get; set; }

        [Column("current_speed")]
        public int? CurrentSpeed { get; set; }

        [Column("last_update")]
        public DateTime? LastUpdate { get; set; }

        [NotMapped]
        [JsonPropertyName("map_icon_url")]
        public string? Model3dUrlAlias { get => Model3dUrl; set => Model3dUrl = value ?? ""; }

        [Column("model_3d_url")]
        [JsonPropertyName("model_3d_url")]
        public string Model3dUrl { get; set; } = "";

        [NotMapped]
        [JsonPropertyName("or_cr_url")]
        public string? OrCrUrlAlias { get => OrCrUrl; set => OrCrUrl = value ?? ""; }

        [Column("or_cr_url")]
        public string OrCrUrl { get; set; } = "";

        [NotMapped]
        [JsonPropertyName("insurance_url")]
        public string? InsuranceUrlAlias { get => InsuranceUrl; set => InsuranceUrl = value ?? ""; }

        [Column("insurance_url")]
        public string InsuranceUrl { get; set; } = "";

        [Column("lto_expiry_date")]
        [JsonPropertyName("lto_expiry_date")]
        public DateTime? LtoExpiryDate { get; set; }

        [Column("insurance_expiry_date")]
        [JsonPropertyName("insurance_expiry_date")]
        public DateTime? InsuranceExpiryDate { get; set; }

        [Column("in_garage")]
        public bool InGarage { get; set; } = true;

        [Column("color")]
        public string Color { get; set; } = "Pearl White";

        [Column("flood_risk_status")]
        [JsonPropertyName("flood_risk_status")]
        public string FloodRiskStatus { get; set; } = "safe";

        [Column("engine_water_ingress_alert")]
        [JsonPropertyName("engine_water_ingress_alert")]
        public bool EngineWaterIngressAlert { get; set; } = false;

        [Column("last_weather_temp")]
        [JsonPropertyName("last_weather_temp")]
        public decimal LastWeatherTemp { get; set; } = 28.5m;
    }
}
