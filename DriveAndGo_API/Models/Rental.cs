using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace DriveAndGo_API.Models
{
    [Table("rentals")]
    public class Rental
    {
        [Column("rental_id")]
        public int RentalId { get; set; }

        [Column("customer_id")]
        public int CustomerId { get; set; }

        [Column("vehicle_id")]
        public int VehicleId { get; set; }

        [Column("driver_id")]
        public int? DriverId { get; set; }

        [Column("destination")]
        public string? Destination { get; set; }

        [Column("start_date")]
        public DateTime StartDate { get; set; }

        [Column("end_date")]
        public DateTime? EndDate { get; set; }

        [Column("status")]
        public string Status { get; set; } = "pending";

        [Column("total_amount")]
        public decimal TotalAmount { get; set; }

        [Column("payment_method")]
        public string PaymentMethod { get; set; } = "cash";

        [Column("payment_status")]
        public string PaymentStatus { get; set; } = "unpaid";

        [Column("qr_code")]
        public string? QrCode { get; set; }

        [Column("start_odometer")]
        public decimal? StartOdometer { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public string? CustomerName { get; set; }

        [NotMapped]
        public string? CustomerPhone { get; set; }

        [NotMapped]
        public string? CustomerEmail { get; set; }

        [NotMapped]
        public string? CustomerAvatar { get; set; }

        [NotMapped]
        public string? CustomerSignatureBase64 { get; set; }

        [NotMapped]
        public string? VehicleName { get; set; }

        [NotMapped]
        public string? VehiclePlateNo { get; set; }

        [NotMapped]
        public decimal? VehicleRate { get; set; }

        [NotMapped]
        public decimal? VehicleOdometer { get; set; }

        [NotMapped]
        public int? VehicleFuelLevelPct { get; set; }

        [NotMapped]
        public string? DriverName { get; set; }

        [NotMapped]
        public string? DriverPhone { get; set; }

        [NotMapped]
        public string? DriverAvatar { get; set; }

        [NotMapped]
        public string? DriverSignatureBase64 { get; set; }
    }
}

