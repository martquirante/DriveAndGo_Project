#nullable enable
using System;

namespace DriveAndGo_Admin.Models
{
    public class NotificationItemModel
    {
        public string id { get; set; } = "";
        public string title { get; set; } = "";
        public string body { get; set; } = "";
        public string type { get; set; } = "general";
        public bool unread { get; set; } = true;
        public string time { get; set; } = "Just now";

        // Rich booking & customer metadata
        public int? rentalId { get; set; }
        public string? rentalCode { get; set; }
        public string? customerName { get; set; }
        public string? customerPhone { get; set; }
        public string? customerEmail { get; set; }
        public string? customerAvatar { get; set; }
        public string? vehicleName { get; set; }
        public string? vehiclePlate { get; set; }
        public string? bookingStatus { get; set; }
        public string? destination { get; set; }
        public string? startDate { get; set; }
        public string? endDate { get; set; }
        public decimal? totalAmount { get; set; }
        public string? eventType { get; set; }
    }
}
