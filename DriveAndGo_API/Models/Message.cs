namespace DriveAndGo_API.Models
{
    public class Message
    {
        public int MessageId { get; set; }
        public int RentalId { get; set; }
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public string Content { get; set; }
        public string? AttachmentUrl { get; set; } // Optional: Kung may sinend na image/video link
        public bool IsRead { get; set; }
        public DateTime SentAt { get; set; }

        // Extra field para sa display
        public string? SenderName { get; set; }
    }
}