namespace DriveAndGo_API.Models
{
    public class RegisterRequest
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? Phone { get; set; }
        public string? Role { get; set; } // "customer" or "driver"
    }
}