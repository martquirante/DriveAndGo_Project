namespace DriveAndGo_API.Models
{
    public class Vehicle
    {
        public int VehicleId { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public string PlateNumber { get; set; }
        public string Type { get; set; }
        public decimal DailyRate { get; set; }
        public string Status { get; set; }
    }
}