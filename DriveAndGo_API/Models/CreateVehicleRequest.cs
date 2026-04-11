namespace DriveAndGo_API.Models
{
    public class CreateVehicleRequest
    {
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
        public int SeatCapacity { get; set; } = 5;
        public string Transmission { get; set; } = "Automatic";
        public string Model3DUrl { get; set; } = "";
    }
}