namespace DriveAndGo_API.Models
{
    public class Vehicle
    {
        public int VehicleId { get; set; }

        public string Brand { get; set; } = "";
        public string Model { get; set; } = "";

        public string PlateNo { get; set; } = "";
        public string Type { get; set; } = "Car";
        public int? CC { get; set; }

        public string Status { get; set; } = "available";
        public decimal RatePerDay { get; set; }
        public decimal RateWithDriver { get; set; }

        public string PhotoUrl { get; set; } = "";
        public string Description { get; set; } = "";
        public int SeatCapacity { get; set; } = 5;
        public string Transmission { get; set; } = "Automatic";

        public DateTime CreatedAt { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int? CurrentSpeed { get; set; }
        public DateTime? LastUpdate { get; set; }

        public string Model3dUrl { get; set; } = "";
        public bool InGarage { get; set; } = true;
    }
}
