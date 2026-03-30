namespace DriveAndGo_API.Models
{
    public class Driver
    {
        public int DriverId { get; set; }
        public int UserId { get; set; }
        public string? LicenseNo { get; set; }
        public string? Status { get; set; }
        public decimal RatingAvg { get; set; }
        public int TotalTrips { get; set; }

        // Extra fields mula sa JOIN (para sa display)
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }
}