namespace DriveAndGo_API.Models
{
    public class Issue
    {
        public int IssueId { get; set; }
        public int RentalId { get; set; }
        public int ReporterId { get; set; } // User ID ng nag-report (Customer or Driver)
        public string IssueType { get; set; } = string.Empty; // e.g., "Breakdown", "Accident", "Others"
        public string Description { get; set; } = string.Empty;
        public string? ImageUrl { get; set; } // Optional: Kung may na-upload na picture ng sira
        public string Status { get; set; } = "Pending"; // "Pending", "In Progress", "Resolved"
        public DateTime ReportedAt { get; set; }

        // Extra fields para sa Admin Dashboard display
        public string? ReporterName { get; set; }
        public string? VehicleName { get; set; }
    }
}
