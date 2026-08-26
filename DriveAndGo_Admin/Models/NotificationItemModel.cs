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
    }
}
