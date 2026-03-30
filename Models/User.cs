namespace DriveAndGo_API.Models
{
    public class User
    {
        // User.cs
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? PasswordHash { get; set; }
        public string? Phone { get; set; }
        public string? Role { get; set; }

        // Vehicle.cs
        public string? Brand { get; set; }
        public string? Model { get; set; }
        public string? PlateNumber { get; set; }
        public string? Type { get; set; }
        public string? Status { get; set; }
    }
}
