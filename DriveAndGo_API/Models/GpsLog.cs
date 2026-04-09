using System;

namespace DriveAndGo_API.Models
{
    public class GpsLog
    {
        public int LogId { get; set; }
        public int RentalId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public decimal? OdometerKm { get; set; }
        public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
    }
}