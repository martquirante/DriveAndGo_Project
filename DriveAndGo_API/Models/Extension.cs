namespace DriveAndGo_API.Models
{
    public class Extension
    {
        public int ExtensionId { get; set; }
        public int RentalId { get; set; }
        public int AddedDays { get; set; }
        public decimal AddedFee { get; set; }
        public string? Status { get; set; }
        public DateTime RequestedAt { get; set; }

        // Extra display fields from JOIN
        public string? CustomerName { get; set; }
        public string? VehicleName { get; set; }
    }
}