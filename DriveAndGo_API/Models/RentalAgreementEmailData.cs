namespace DriveAndGo_API.Models
{
    public class RentalAgreementEmailData
    {
        public string AgreementCode { get; set; } = string.Empty;
        public string AdminName { get; set; } = "Raymart Quirante";
        public string? AdminSignatureBase64 { get; set; }
        public string? CustomerSignatureBase64 { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string VehicleName { get; set; } = string.Empty;
        public string PlateNo { get; set; } = string.Empty;
        public string VehicleColor { get; set; } = string.Empty;
        public string PickupDate { get; set; } = string.Empty;
        public string DropoffDate { get; set; } = string.Empty;
        public int DurationDays { get; set; }
        public decimal DailyRate { get; set; }
        public decimal DailyTotal { get; set; }
        public decimal InsuranceFee { get; set; } = 500m;
        public decimal VatAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string Destination { get; set; } = string.Empty;
        public string DriverName { get; set; } = string.Empty;
        public string DriverPhone { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = "Paid";
        public string PaymentMethod { get; set; } = "Cash";
        public string CreatedDate { get; set; } = string.Empty;
        public string VerificationUrl { get; set; } = string.Empty;
        public string? PersonalMessage { get; set; }
        public string CompanyAddress { get; set; } = "DriveAndGo Inc., CSJDM | Norzagaray, Bulacan, Philippines";
        public string CompanyPhone { get; set; } = "+63 935 966 7178";
        public string CompanyEmail { get; set; } = "support@driveandgo.com";

        public bool IsRescheduled { get; set; } = false;
        public string? OriginalPickupDate { get; set; }
        public string? OriginalDropoffDate { get; set; }
        public string? AcceptScheduleUrl { get; set; }
        public string? RequestRescheduleUrl { get; set; }
        public string? DeclineBookingUrl { get; set; }

        public bool PerkFuelWaiver { get; set; } = false;
        public bool PerkTollCredits { get; set; } = false;
        public bool PerkWashWaiver { get; set; } = false;

        public bool IncludePromoGift { get; set; } = false;
        public string? PromoCode { get; set; }
        public string? PromoDescription { get; set; }
        public string? PromoExpiry { get; set; }
    }
}

