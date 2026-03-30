using System;

namespace DriveAndGo_Admin // <-- DAPAT DriveAndGo_Admin ITO!
{
    public class Rental
    {
        public int RentalId { get; set; }
        public int CustomerId { get; set; }
        public int VehicleId { get; set; }
        public int? DriverId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Destination { get; set; }
        public string Status { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentStatus { get; set; }

        public string CustomerName { get; set; }
        public string VehicleName { get; set; }
    }
}