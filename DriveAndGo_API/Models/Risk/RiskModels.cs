namespace DriveAndGo_API.Models.Risk;

/// <summary>
/// Payload for License ID OCR / Fraud Analysis.
/// </summary>
public class LicenseAnalysisRequestDto
{
    public string Base64Image { get; set; } = string.Empty;
}

/// <summary>
/// Result from Gemini AI Vision License Analysis.
/// </summary>
public class LicenseAnalysisResultDto
{
    public string FullName       { get; set; } = string.Empty;
    public string LicenseNumber  { get; set; } = string.Empty;
    public string ExpirationDate { get; set; } = string.Empty;
    public bool   IsExpired      { get; set; }
    public int    FraudRiskScore { get; set; } // 0 - 100
    public string RiskReason     { get; set; } = string.Empty;
}

/// <summary>
/// Payload for Vehicle Damage Assessment.
/// </summary>
public class DamageAssessmentRequestDto
{
    public string Base64Image { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Result from Gemini AI Vision Vehicle Damage Assessment.
/// </summary>
public class DamageAssessmentResultDto
{
    public string  DamageType              { get; set; } = "Unknown";
    public string  Severity                { get; set; } = "Minor"; // Minor | Moderate | Severe
    public decimal EstimatedRepairCost     { get; set; }
    public decimal RecommendedPenaltyFee   { get; set; }
    public string  AssessmentNotes         { get; set; } = string.Empty;
}

/// <summary>
/// DTO for Fuel Overpricing / Theft Anomaly Analysis.
/// </summary>
public class FuelAnomalyDto
{
    public int     VehicleId               { get; set; }
    public decimal CurrentFuelCost         { get; set; }
    public decimal DistanceTraveled        { get; set; }
    public decimal CostPerKm               { get; set; }
    public decimal HistoricalAvgCostPerKm { get; set; }
    public double  DiscrepancyPercentage   { get; set; }
    public bool    IsAnomaly               { get; set; }
    public string  RiskLevel               { get; set; } = "Normal"; // High Risk | Moderate Risk | Normal
    public string  RiskReason              { get; set; } = string.Empty;
}

/// <summary>
/// DTO for Split Payment Reminder structure.
/// </summary>
public class SplitPayReminderDto
{
    public int     RentalId            { get; set; }
    public string  CustomerName        { get; set; } = string.Empty;
    public decimal TotalAmount         { get; set; }
    public decimal UnpaidAmount        { get; set; }
    public int     PendingMembersCount { get; set; }
    public string  PaymentLink         { get; set; } = string.Empty;
    public string  DraftedSmsText      { get; set; } = string.Empty;
    public string  DraftedEmailText    { get; set; } = string.Empty;
}
