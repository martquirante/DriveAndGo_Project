namespace DriveAndGo_API.Models
{
    public class Rating
    {
        public int RatingId { get; set; }
        public int RentalId { get; set; }
        public int CustomerId { get; set; }
        public int? DriverId { get; set; }
        public int VehicleId { get; set; }
        public int? DriverScore { get; set; }
        public int VehicleScore { get; set; }
        public string? Comment { get; set; }
        public DateTime RatedAt { get; set; }

        // Extra display fields from JOIN
        public string? CustomerName { get; set; }
        public string? VehicleName { get; set; }
    }
}