using System;
using System.Text.Json.Serialization;

namespace DriveAndGo_API.Models
{
    public class VehicleDto
    {
        public int VehicleId { get; set; }
        public string Brand { get; set; } = "";
        public string Model { get; set; } = "";
        public string PlateNo { get; set; } = "";
        public string Type { get; set; } = "";
        public int? Cc { get; set; }
        public decimal RatePerDay { get; set; }
        public decimal RateWithDriver { get; set; }
        public string Status { get; set; } = "available";
        [JsonPropertyName("photo_url")]
        public string? PhotoUrlSnake => PhotoUrl;
        public string PhotoUrl { get; set; } = "";

        public string Description { get; set; } = "";
        public int SeatCapacity { get; set; }
        public string Transmission { get; set; } = "Automatic";
        public string Model3DUrl { get; set; } = "";
        public DateTime? CreatedAt { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int? CurrentSpeed { get; set; }
        public DateTime? LastUpdate { get; set; }
        public bool InGarage { get; set; }

        // ── Fleet Telematics & Telemetry ──────────────────────────────────
        public int FuelLevelPct { get; set; } = 100;
        public int OdometerKm { get; set; } = 0;
        public int HealthScore { get; set; } = 98;
        public string EngineStatus { get; set; } = "off";
        public int MaintenanceDueKm { get; set; } = 5000;
        public bool TelematicsLocked { get; set; } = true;
        public DateTime? LtoExpiryDate { get; set; }
        public DateTime? InsuranceExpiryDate { get; set; }

        [JsonPropertyName("orCrUrl")]
        public string? OrCrUrlCamel => OrCrUrl;
        [JsonPropertyName("or_cr_url")]
        public string OrCrUrl { get; set; } = "";

        [JsonPropertyName("insuranceUrl")]
        public string? InsuranceUrlCamel => InsuranceUrl;
        [JsonPropertyName("insurance_url")]
        public string InsuranceUrl { get; set; } = "";
        public int SafetyScore { get; set; } = 95;
        public int IdleMinutes { get; set; } = 0;
        public decimal RfidBalanceAutosweep { get; set; } = 500m;
        public decimal RfidBalanceEasytrip { get; set; } = 500m;
        public string Color { get; set; } = "Pearl White";
        public string FloodRiskStatus { get; set; } = "safe";
        public bool EngineWaterIngressAlert { get; set; } = false;
        public decimal LastWeatherTemp { get; set; } = 28.5m;
    }

    public class VehicleFleetDto
    {
        public int VehicleId { get; set; }
        public string Brand { get; set; } = "";
        public string Model { get; set; } = "";
        public string PlateNo { get; set; } = "";
        public string Type { get; set; } = "";
        public decimal RatePerDay { get; set; }
        public string Status { get; set; } = "available";
        public string PhotoUrl { get; set; } = "";
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string Color { get; set; } = "Pearl White";
        public string FloodRiskStatus { get; set; } = "safe";
        public bool EngineWaterIngressAlert { get; set; } = false;
    }

    // ── Telematics Request DTOs ───────────────────────────────────────────
    public class TelematicsCommandRequest
    {
        public string Command { get; set; } = "";
        public string? Notes { get; set; }
    }

    public class TelematicsUpdateRequest
    {
        public int? FuelLevelPct { get; set; }
        public int? OdometerKm { get; set; }
        public int? HealthScore { get; set; }
        public string? EngineStatus { get; set; }
        public int? SafetyScore { get; set; }
        public int? IdleMinutes { get; set; }
        public decimal? RfidBalanceAutosweep { get; set; }
        public decimal? RfidBalanceEasytrip { get; set; }
        public DateTime? LtoExpiryDate { get; set; }
        public DateTime? InsuranceExpiryDate { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
