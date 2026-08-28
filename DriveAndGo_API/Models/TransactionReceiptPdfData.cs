namespace DriveAndGo_API.Models
{
    public class TransactionReceiptPdfData
    {
        public int TransactionId { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public string RentalCode { get; set; } = string.Empty;
        public int RentalId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string VehicleName { get; set; } = string.Empty;
        public string PlateNo { get; set; } = string.Empty;
        public string VehicleColor { get; set; } = string.Empty;
        public string PickupDate { get; set; } = string.Empty;
        public string DropoffDate { get; set; } = string.Empty;
        public int DurationDays { get; set; } = 1;
        public decimal DailyRate { get; set; }
        public decimal RentalSubtotal { get; set; }
        public decimal SecurityDeposit { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string AmountInWords { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = "CASH";
        public string Status { get; set; } = "CONFIRMED";
        public string TransactionDate { get; set; } = string.Empty;
        public string AdminName { get; set; } = "Raymart Quirante";
        public string? AdminSignatureBase64 { get; set; }
        public string? ProofUrl { get; set; }
        public string VerificationUrl { get; set; } = string.Empty;
        public string CompanyAddress { get; set; } = "DriveAndGo Inc., CSJDM | Norzagaray, Bulacan, Philippines";
        public string CompanyPhone { get; set; } = "+63 935 966 7178";
        public string CompanyEmail { get; set; } = "support@driveandgo.ph";
        public string? PersonalMessage { get; set; }
    }
}
