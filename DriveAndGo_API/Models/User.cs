using System;

namespace DriveAndGo_API.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string Phone { get; set; }
        public string Role { get; set; }
        public string? IdPhotoUrl { get; set; } // Nullable kasi baka wala pa
        public string? FirebaseUid { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}