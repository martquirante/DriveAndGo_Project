namespace DriveAndGo_API.Models
{
    public class LocationLog
    {
        public int LogId { get; set; }
        public int RentalId { get; set; }
        public int VehicleId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public decimal? SpeedKmH { get; set; } // Opsyonal: kung gusto mong kunin ang bilis ng takbo
        public DateTime LoggedAt { get; set; }

        // Extra fields para sa GET display (pang Admin Map)
        public string? VehicleName { get; set; }
        public string? PlateNumber { get; set; }
        public string? DriverName { get; set; }
    }
}