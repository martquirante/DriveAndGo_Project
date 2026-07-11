using System;

namespace DriveAndGo_Admin.Helpers
{
    /// <summary>
    /// Holds authenticated session data for the currently logged-in admin.
    /// Populated by ApiService.LoginAsync() after a successful /api/auth/login call.
    /// </summary>
    public static class SessionManager
    {
        public static int    UserId    { get; set; }
        public static string FullName  { get; set; } = string.Empty;
        public static string Role      { get; set; } = string.Empty;
        public static string Email     { get; set; } = string.Empty;

        /// <summary>
        /// JWT Bearer token returned by the API on login.
        /// Passed in the Authorization header for all subsequent API calls.
        /// </summary>
        public static string JwtToken  { get; set; } = string.Empty;

        public static bool IsLoggedIn => !string.IsNullOrWhiteSpace(JwtToken);

        /// <summary>Clears all session data on logout.</summary>
        public static void Clear()
        {
            UserId   = 0;
            FullName = string.Empty;
            Role     = string.Empty;
            Email    = string.Empty;
            JwtToken = string.Empty;
        }
    }
}