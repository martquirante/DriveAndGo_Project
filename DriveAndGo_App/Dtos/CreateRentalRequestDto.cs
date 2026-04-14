namespace DriveAndGo_App.Dtos;

public sealed class CreateRentalRequestDto
{
    public int CustomerId { get; set; }
    public int VehicleId { get; set; }
    public int? DriverId { get; set; }
    public string? Destination { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = "cash";
}
