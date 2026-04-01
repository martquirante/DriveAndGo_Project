using System;

namespace DriveAndGo_Admin.Helpers
{
    public static class SessionManager
    {
        public static int UserId { get; set; }
        public static string FullName { get; set; } = string.Empty;
        public static string Role { get; set; } = string.Empty;
        public static string Email { get; set; } = string.Empty;

        // Pang-clear ng data kapag nag-Log Out
        public static void Clear()
        {
            UserId = 0;
            FullName = string.Empty;
            Role = string.Empty;
            Email = string.Empty;
        }
    }
}