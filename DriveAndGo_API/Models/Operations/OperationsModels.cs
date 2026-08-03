namespace DriveAndGo_API.Models.Operations;

/// <summary>
/// Result DTO for Dynamic Surge Pricing Engine computations.
/// </summary>
public class SurgePricingResultDto
{
    public int     VehicleCategoryId     { get; set; }
    public string  CategoryName          { get; set; } = "Standard";
    public decimal OriginalRate          { get; set; }
    public decimal SurgeMultiplier       { get; set; } = 1.0m;
    public decimal FinalRate             { get; set; }
    public string  SurgeReason           { get; set; } = "Normal Demand";
    public double  UtilizationPercentage { get; set; }
    public int     TotalVehicles         { get; set; }
    public int     BookedVehicles        { get; set; }
}

/// <summary>
/// DTO for Predictive Maintenance Alerts.
/// </summary>
public class VehicleMaintenanceAlertDto
{
    public int     VehicleId               { get; set; }
    public string  BrandModel              { get; set; } = string.Empty;
    public string  PlateNo                 { get; set; } = string.Empty;
    public decimal CurrentOdometer         { get; set; }
    public decimal LastMaintenanceOdometer { get; set; }
    public decimal KmSinceMaintenance      { get; set; }
    public string  RiskLevel               { get; set; } = "Normal"; // 'High Risk' | 'Approaching' | 'Normal'
    public string  RecommendedAction       { get; set; } = string.Empty;
}

/// <summary>
/// Result DTO for AI Auto-Dispatcher booking assignment.
/// </summary>
public class AutoDispatchResultDto
{
    public int     RentalId     { get; set; }
    public int     VehicleId    { get; set; }
    public string  VehicleName  { get; set; } = string.Empty;
    public int?    DriverId     { get; set; }
    public string? DriverName   { get; set; }
    public string  CustomerName { get; set; } = string.Empty;
    public bool    Success      { get; set; }
    public string  Message      { get; set; } = string.Empty;
}
