using System.Text.Json.Serialization;

namespace DriveAndGo_App.Models;

public sealed class TransactionItem
{
    [JsonPropertyName("transactionId")]
    public int TransactionId { get; set; }

    [JsonPropertyName("rentalId")]
    public int RentalId { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("proofUrl")]
    public string? ProofUrl { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("paidAt")]
    public DateTime? PaidAt { get; set; }

    public string AmountLabel => $"PHP {Amount:N0}";
}
