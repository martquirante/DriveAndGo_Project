namespace DriveAndGo_App.Dtos;

public sealed class CreateRatingRequestDto
{
    public int RentalId { get; set; }
    public int CustomerId { get; set; }
    public int? DriverId { get; set; }
    public int VehicleId { get; set; }
    public int? DriverScore { get; set; }
    public int VehicleScore { get; set; }
    public string? Comment { get; set; }
}
