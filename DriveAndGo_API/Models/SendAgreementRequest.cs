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
    }
}
