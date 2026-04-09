using System;

namespace DriveAndGo_API.Models
{
    public class LocationLog
    {
        public int LogId { get; set; }
        public int RentalId { get; set; }
        public int VehicleId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public decimal? SpeedKmh { get; set; }
        public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

        // 🟢 MGA DAGDAG PARA SA UI BINDING:
        public decimal? SpeedKmH { get => SpeedKmh; set => SpeedKmh = value; } // Fix para sa capital 'H'
        public string? VehicleName { get; set; }
        public string? PlateNumber { get; set; }
        public string? DriverName { get; set; }
    }
}