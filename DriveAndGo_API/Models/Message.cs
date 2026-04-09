using System;

namespace DriveAndGo_API.Models
{
    public class Message
    {
        public int MessageId { get; set; }
        public int RentalId { get; set; }
        public int SenderId { get; set; }
        public string? MessageText { get; set; }
        public string? MediaUrl { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        // 🟢 MGA DAGDAG PARA SA UI BINDING:
        public int? ReceiverId { get; set; }
        public string? Content { get => MessageText; set => MessageText = value; }
        public string? AttachmentUrl { get => MediaUrl; set => MediaUrl = value; }
        public bool IsRead { get; set; } = false;
        public string? SenderName { get; set; }
    }
}