namespace DriveAndGo_API.Models
{
    public class VehicleReturnEmailData
    {
        // Booking identifiers
        public string ReturnCertCode { get; set; } = string.Empty;   // VR-000082
        public string AgreementCode  { get; set; } = string.Empty;   // RN-000082

        // Customer
        public string CustomerName  { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;

        // Vehicle
        public string VehicleName     { get; set; } = string.Empty;  // Ford Ranger Wildtrak
        public string PlateNo         { get; set; } = string.Empty;  // TIE-4833
        public string? VehicleImageUrl { get; set; }                  // vehicles.image_url

        // Rental period
        public string PickupDate    { get; set; } = string.Empty;
        public string ReturnDate    { get; set; } = string.Empty;
        public int    DurationDays  { get; set; }

        // Inspection metrics
        public decimal? StartOdometer  { get; set; }
        public decimal? ReturnOdometer { get; set; }
        public string   ReturnFuel     { get; set; } = "Full";       // "Full","3/4","1/2","1/4","Empty"
        public string   InspectionStatus { get; set; } = "PASSED";  // "PASSED" | "INSPECTED"
        public bool     HasDamage      { get; set; }

        // Billing
        public decimal BaseAmount   { get; set; }
        public decimal PenaltyFee   { get; set; }
        public decimal DamageFee    { get; set; }
        public decimal TotalSettled { get; set; }

        // Admin inspector
        public string AdminName { get; set; } = string.Empty;

        // Optional Thank-You gift voucher
        public bool    IncludePromoGift  { get; set; }
        public string? PromoCode         { get; set; }
        public string? PromoDescription  { get; set; }   // e.g. "10% OFF on your next rental"
        public string? PromoExpiry       { get; set; }

        // Links
        public string VerificationUrl  { get; set; } = string.Empty;
        public string PdfDownloadUrl   { get; set; } = string.Empty;

        // Company
        public string CompanyAddress { get; set; } = "San Jose del Monte, Bulacan, Philippines";
        public string CompanyPhone   { get; set; } = "+63 935 966 7178";
        public string CompanyEmail   { get; set; } = "support@driveandgo.com";
    }
}
