using System;

namespace DriveAndGo_API.Models
{
    public class Incident
    {
        public int IncidentId { get; set; }
        public int RentalId { get; set; }
        public int ReportedBy { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? PhotoUrl { get; set; }
        public string Status { get; set; } = "pending";
        public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    }
}
