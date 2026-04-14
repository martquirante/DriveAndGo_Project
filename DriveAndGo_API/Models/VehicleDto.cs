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
    }
}
