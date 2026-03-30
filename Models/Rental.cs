namespace DriveAndGo_API.Models
{
    public class Rental
    {
        public int RentalId { get; set; }
        public int CustomerId { get; set; }
        public int VehicleId { get; set; }
        public int? DriverId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Destination { get; set; } // May (?) na
        public string? Status { get; set; }      // May (?) na rin para hindi mag-error!
        public decimal TotalAmount { get; set; }
    }
}