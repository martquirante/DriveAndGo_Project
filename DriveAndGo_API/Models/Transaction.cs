namespace DriveAndGo_API.Models
{
    public class Transaction
    {
        public int TransactionId { get; set; }
        public int RentalId { get; set; }
        public decimal Amount { get; set; }
        public string? Type { get; set; } // payment, refund, extension
        public string? Method { get; set; } // cash, gcash, maya, bank
        public string? ProofUrl { get; set; } // photo ng receipt/QR
        public string? Status { get; set; } // pending, confirmed, rejected
        public DateTime? PaidAt { get; set; }

        // Extra fields mula sa JOIN
        public string? CustomerName { get; set; }
        public string? VehicleName { get; set; }
    }
}