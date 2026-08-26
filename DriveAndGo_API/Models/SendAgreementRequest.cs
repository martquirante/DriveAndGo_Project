using System.Text.Json.Serialization;

namespace DriveAndGo_API.Models
{
    public class SendAgreementRequest
    {
        [JsonPropertyName("rentalId")]
        public int RentalId { get; set; }

        [JsonPropertyName("recipientEmail")]
        public string RecipientEmail { get; set; } = string.Empty;

        [JsonPropertyName("ccEmail")]
        public string? CcEmail { get; set; }

        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        [JsonPropertyName("personalMessage")]
        public string? PersonalMessage { get; set; }

        [JsonPropertyName("attachPdf")]
        public bool AttachPdf { get; set; } = true;

        [JsonPropertyName("includeReceipt")]
        public bool IncludeReceipt { get; set; } = false;

        [JsonPropertyName("isRescheduled")]
        public bool IsRescheduled { get; set; } = false;

        [JsonPropertyName("originalPickupDate")]
        public string? OriginalPickupDate { get; set; }

        [JsonPropertyName("originalDropoffDate")]
        public string? OriginalDropoffDate { get; set; }

        [JsonPropertyName("perkFuelWaiver")]
        public bool PerkFuelWaiver { get; set; } = false;

        [JsonPropertyName("perkTollCredits")]
        public bool PerkTollCredits { get; set; } = false;

        [JsonPropertyName("perkWashWaiver")]
        public bool PerkWashWaiver { get; set; } = false;

        [JsonPropertyName("includeCourtesyPromo")]
        public bool IncludeCourtesyPromo { get; set; } = false;

        [JsonPropertyName("courtesyPromoCode")]
        public string? CourtesyPromoCode { get; set; }

        [JsonPropertyName("courtesyPromoDiscount")]
        public string? CourtesyPromoDiscount { get; set; }
    }
}

