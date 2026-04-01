namespace DriveAndGo_Admin.Helpers
{
    public static class SessionManager
    {
        public static int UserId { get; set; }
        public static string FullName { get; set; } = string.Empty;
        public static string Email { get; set; } = string.Empty;
        public static string Role { get; set; } = string.Empty;

        public static bool IsLoggedIn => UserId > 0;

        public static void Clear()
        {
            UserId = 0;
            FullName = string.Empty;
            Email = string.Empty;
            Role = string.Empty;
        }
    }
}