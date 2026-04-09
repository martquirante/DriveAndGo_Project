namespace DriveAndGo_API.Models
{
    public class Driver
    {
        public int DriverId { get; set; }
        public int UserId { get; set; }
        public string LicenseNo { get; set; }
        public string? LicensePhotoUrl { get; set; }
        public string Status { get; set; } = "inactive";
        public decimal? RatingAvg { get; set; }
        public int TotalTrips { get; set; } = 0;

        // 🟢 MGA DAGDAG PARA SA UI BINDING:
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }
}