namespace DriveAndGo_API.Models;

public class BatchVehicleRequest
{
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public string Type { get; set; } = "";
    public int? CC { get; set; }
    public decimal RatePerDay { get; set; }
    public decimal RateWithDriver { get; set; }
    public string Transmission { get; set; } = "";
    public string PhotoUrl { get; set; } = "";
    public string Description { get; set; } = "";
    public int SeatCapacity { get; set; }
    public List<BatchVehicleUnit> Units { get; set; } = new();
}

public class BatchVehicleUnit
{
    public string PlateNo { get; set; } = "";
    public DateTime? LtoExpiryDate { get; set; }
    public DateTime? InsuranceExpiryDate { get; set; }
    public decimal? RfidBalanceAutosweep { get; set; }
    public decimal? RfidBalanceEasytrip { get; set; }
}
