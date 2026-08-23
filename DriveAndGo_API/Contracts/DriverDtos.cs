namespace DriveAndGo_API.Contracts
{
    // ── Lightweight DTO for driver roster table (avoids over-fetching) ──
    public class DriverListDto
    {
        public int     DriverId           { get; set; }
        public int     UserId             { get; set; }
        public string  FullName           { get; set; } = string.Empty;
        public string  EmployeeCode       { get; set; } = string.Empty;
        public string  Email              { get; set; } = string.Empty;
        public string  Phone              { get; set; } = string.Empty;
        public string  LicenseNo          { get; set; } = string.Empty;
        public string? LicenseExpiry      { get; set; }
        public string  Status             { get; set; } = "available";
        public decimal RatingAvg          { get; set; }
        public int     TotalTrips         { get; set; }
        public decimal TotalRevenue       { get; set; }
        public string? AvatarUrl          { get; set; }
        public string? CurrentVehiclePlate{ get; set; }
        public string? CurrentVehicleName { get; set; }
        public string? CurrentVehicleImg  { get; set; }
        public string  ShiftSchedule      { get; set; } = "Morning Shift";
        public decimal CashOnHand         { get; set; }
        public string? SkillFlags         { get; set; }
        public string? VerificationStatus { get; set; }
        public string? SignatureUrl       { get; set; }
        public string? SignatureBase64    { get; set; }
    }

    // ── Full driver detail DTO for profile modals ──
    public class DriverDetailDto : DriverListDto
    {
        // License info
        public string? LicensePhotoUrl  { get; set; }
        public string? LicenseClass     { get; set; }
        public string? Restrictions     { get; set; }
        public string? Conditions       { get; set; }

        // Personal info
        public string? BirthDate        { get; set; }
        public string? Address          { get; set; }
        public string? BloodType        { get; set; }
        public string? Nationality      { get; set; }
        public string? Sex              { get; set; }
        public string? WeightKg         { get; set; }
        public string? HeightM          { get; set; }
        public string? EyeColor         { get; set; }

        // Compliance documents expiries
        public string? NbiExpiry        { get; set; }
        public string? PoliceExpiry     { get; set; }
        public string? DrugTestExpiry   { get; set; }
        public string? MedicalExpiry    { get; set; }

        // Relational sub-collections
        public List<DriverPayoutAccountDto>   PayoutAccounts    { get; set; } = new();
        public List<DriverEmergencyContactDto> EmergencyContacts { get; set; } = new();
        public List<DriverDocumentDto>        Documents         { get; set; } = new();
        public List<DriverIncidentDto>        Incidents         { get; set; } = new();
    }

    // ── Payout account DTO ──
    public class DriverPayoutAccountDto
    {
        public int    PayoutId     { get; set; }
        public string Channel      { get; set; } = string.Empty;  // GCash, Maya, BDO, BPI, Cash
        public string AccountName  { get; set; } = string.Empty;
        public string AccountNo    { get; set; } = string.Empty;
        public bool   IsPrimary    { get; set; }
    }

    // ── Emergency contact DTO ──
    public class DriverEmergencyContactDto
    {
        public int    ContactId    { get; set; }
        public string FullName     { get; set; } = string.Empty;
        public string Relationship { get; set; } = string.Empty;
        public string Phone        { get; set; } = string.Empty;
        public string BloodType    { get; set; } = string.Empty;
        public string? MedicalNotes{ get; set; }
        public bool   IsPrimary    { get; set; }
    }

    // ── Compliance document DTO ──
    public class DriverDocumentDto
    {
        public int    DocId       { get; set; }
        public string DocType     { get; set; } = string.Empty;
        public string? FileUrl    { get; set; }
        public string? ExpiryDate { get; set; }
        public string  Status     { get; set; } = "pending";
        public string? UploadedAt { get; set; }
    }

    // ── Incident / violation DTO ──
    public class DriverIncidentDto
    {
        public int     IncidentId     { get; set; }
        public string  Type           { get; set; } = string.Empty;  // Violation, Delay, Commendation
        public string  Description    { get; set; } = string.Empty;
        public string? IncidentDate   { get; set; }
        public decimal PenaltyAmount  { get; set; }
        public string  Status         { get; set; } = "open";
    }

    // ── Payslip DTO ──
    public class DriverPayslipDto
    {
        public int     DriverId       { get; set; }
        public string  FullName       { get; set; } = string.Empty;
        public string  EmployeeCode   { get; set; } = string.Empty;
        public string  Email          { get; set; } = string.Empty;
        public string  Phone          { get; set; } = string.Empty;
        public string  LicenseNo      { get; set; } = string.Empty;
        public string? AvatarUrl      { get; set; }
        public string? PayPeriodStart { get; set; }
        public string? PayPeriodEnd   { get; set; }
        public string? StatementNo    { get; set; }

        // Primary payout account
        public string? PayoutChannel  { get; set; }
        public string? PayoutAccountName { get; set; }
        public string? PayoutAccountNo{ get; set; }

        // Assigned vehicle
        public string? VehicleName    { get; set; }
        public string? VehiclePlate   { get; set; }

        // Trip summary
        public int     TotalTrips     { get; set; }
        public decimal TotalDistanceKm{ get; set; }
        public decimal CustomerRating { get; set; }
        public decimal CompletionRate { get; set; }

        // Earnings
        public decimal GrossFares         { get; set; }
        public decimal DriverShare70      { get; set; }  // 70% of GrossFares
        public decimal PlatformCut30      { get; set; }  // 30% of GrossFares
        public decimal Incentives         { get; set; }
        public decimal Tips               { get; set; }
        public decimal TollReimbursements { get; set; }
        public decimal TotalEarnings      { get; set; }

        // Deductions
        public decimal CashAdvance        { get; set; }
        public decimal FuelDeduction      { get; set; }
        public decimal TollDeduction      { get; set; }
        public decimal UniformDeduction   { get; set; }
        public decimal OtherDeductions    { get; set; }
        public decimal TotalDeductions    { get; set; }

        // Net
        public decimal NetPayout          { get; set; }

        // Trip items
        public List<PayslipTripItemDto> Trips { get; set; } = new();
    }

    // ── Individual trip row on payslip ──
    public class PayslipTripItemDto
    {
        public int     RentalId     { get; set; }
        public string? TripDate     { get; set; }
        public string? VehicleName  { get; set; }
        public string? VehiclePlate { get; set; }
        public string? Destination  { get; set; }
        public decimal TotalFare    { get; set; }
        public decimal DriverShare  { get; set; }
        public decimal PlatformCut  { get; set; }
        public string  PaymentStatus{ get; set; } = "unpaid";
    }

    // ── Request DTOs ──
    public class CreateDriverRequest
    {
        public int?   UserId    { get; set; }
        public string? Email    { get; set; }
        public string? FullName { get; set; }
        public string? Phone    { get; set; }
        public string  LicenseNo{ get; set; } = string.Empty;
        public string  Status   { get; set; } = "available";
        public string? LicenseClass   { get; set; }
        public string? LicenseExpiry  { get; set; }
        public string? Restrictions   { get; set; }
        public string? Conditions     { get; set; }
        public string? SkillFlags     { get; set; }
        public string? ShiftSchedule  { get; set; }
        public string? BloodType      { get; set; }
        public string? Address        { get; set; }
        public string? BirthDate      { get; set; }
        public string? Nationality    { get; set; }
        public string? Sex            { get; set; }
        public string? WeightKg       { get; set; }
        public string? HeightM        { get; set; }
        public string? EyeColor       { get; set; }
        public string? NbiExpiry      { get; set; }
        public string? PoliceExpiry   { get; set; }
        public string? DrugTestExpiry { get; set; }
        public string? MedicalExpiry  { get; set; }
    }

    public class UpdateDriverRequest : CreateDriverRequest
    {
        // Inherits all fields from CreateDriverRequest
    }

    public class RemitCashRequest
    {
        public decimal Amount           { get; set; }
        public string? Notes            { get; set; }
        public string? ReferenceNo      { get; set; }
    }

    public class CreateEmergencyContactRequest
    {
        public string  FullName     { get; set; } = string.Empty;
        public string? Relationship { get; set; }
        public string  Phone        { get; set; } = string.Empty;
        public string? BloodType    { get; set; }
        public string? MedicalNotes { get; set; }
        public bool    IsPrimary    { get; set; }
    }

    public class CreateDocumentRequest
    {
        public string  DocType    { get; set; } = string.Empty;
        public string? FileUrl    { get; set; }
        public string? ExpiryDate { get; set; }
        public string  Status     { get; set; } = "valid";
    }

    public class UpdateMedicalNotesRequest
    {
        public string? BloodType    { get; set; }
        public string? MedicalNotes { get; set; }
    }

    public class UpdateBloodTypeRequest
    {
        public string BloodType { get; set; } = string.Empty;
    }
}
