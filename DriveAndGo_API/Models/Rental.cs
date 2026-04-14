using System;

namespace DriveAndGo_API.Models
{
    public class Rental
    {
        public int RentalId { get; set; }
        public int CustomerId { get; set; }
        public int VehicleId { get; set; }
        public int? DriverId { get; set; }
        public string? Destination { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; } = "pending";
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = "cash";
        public string PaymentStatus { get; set; } = "unpaid";
        public string? QrCode { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
        public string? VehicleName { get; set; }
        public string? VehiclePlateNo { get; set; }
        public string? DriverName { get; set; }
        public string? DriverPhone { get; set; }
    }
}
