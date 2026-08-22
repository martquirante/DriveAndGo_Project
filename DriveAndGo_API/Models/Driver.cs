namespace DriveAndGo_API.Models
{
    public class Driver
    {
        public int     DriverId          { get; set; }
        public int     UserId            { get; set; }
        public string  LicenseNo         { get; set; } = string.Empty;
        public string? LicensePhotoUrl   { get; set; }
        public string? LicenseExpiry     { get; set; }
        public string? LicenseClass      { get; set; }
        public string? Restrictions      { get; set; }
        public string? Conditions        { get; set; }
        public string  Status            { get; set; } = "inactive";
        public decimal? RatingAvg        { get; set; }
        public int     TotalTrips        { get; set; } = 0;

        // Personal / medical
        public string? BloodType         { get; set; }
        public string? BirthDate         { get; set; }
        public string? Address           { get; set; }
        public string? Nationality       { get; set; }
        public string? Sex               { get; set; }
        public string? WeightKg          { get; set; }
        public string? HeightM           { get; set; }
        public string? EyeColor          { get; set; }

        // Compliance expiries
        public string? NbiExpiry         { get; set; }
        public string? PoliceExpiry      { get; set; }
        public string? DrugTestExpiry    { get; set; }
        public string? MedicalExpiry     { get; set; }

        // Operations
        public string? ShiftSchedule     { get; set; }
        public string? SkillFlags        { get; set; }
        public decimal CashOnHand        { get; set; } = 0;

        // UI binding joined from users table
        public string? FullName          { get; set; }
        public string? DriverName        { get => FullName; set => FullName = value; }
        public string? Email             { get; set; }
        public string? Phone             { get; set; }
        public string? AvatarUrl         { get; set; }
        public string? VerificationStatus{ get; set; }
    }
}
