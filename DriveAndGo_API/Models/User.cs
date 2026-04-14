using System;

namespace DriveAndGo_API.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? IdPhotoUrl { get; set; } // Nullable kasi baka wala pa
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
