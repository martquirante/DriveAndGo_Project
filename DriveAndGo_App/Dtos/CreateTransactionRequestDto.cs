namespace DriveAndGo_App.Dtos;

public sealed class CreateTransactionRequestDto
{
    public int RentalId { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = "payment";
    public string Method { get; set; } = "cash";
    public string? ProofUrl { get; set; }
}
