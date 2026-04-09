using System;

namespace DriveAndGo_API.Models
{
    public class Vehicle
    {
        public int VehicleId { get; set; }
        public string PlateNo { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public string Type { get; set; }
        public int? CC { get; set; }
        public string Status { get; set; } = "available";
        public decimal RatePerDay { get; set; }
        public decimal RateWithDriver { get; set; }
        public string? PhotoUrl { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public int? CurrentSpeed { get; set; }
        public DateTime? LastUpdate { get; set; }
        public string? Model3dUrl { get; set; }
        public bool InGarage { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 🟢 MGA DAGDAG PARA SA UI BINDING (Alias):
        public string? PlateNumber { get => PlateNo; set => PlateNo = value ?? ""; }
        public decimal DailyRate { get => RatePerDay; set => RatePerDay = value; }
    }
}