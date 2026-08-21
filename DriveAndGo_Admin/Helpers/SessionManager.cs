using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

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

        /// <summary>Alias for JwtToken for convenience.</summary>
        public static string Token => JwtToken;

        public static bool IsLoggedIn => !string.IsNullOrWhiteSpace(JwtToken);

        public static System.Drawing.Image CustomAvatar { get; set; }

        public static async Task SetAvatarFromRawAsync(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                CustomAvatar = null;
                return;
            }

            try
            {
                string cleaned = raw.Trim();
                if (cleaned.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    using var client = new HttpClient();
                    var bytes = await client.GetByteArrayAsync(cleaned);
                    using var ms = new MemoryStream(bytes);
                    var img = Image.FromStream(ms);
                    CustomAvatar = (Image)img.Clone();
                    return;
                }

                int commaIdx = cleaned.IndexOf(',');
                if (commaIdx >= 0)
                {
                    cleaned = cleaned.Substring(commaIdx + 1).Trim();
                }

                byte[] imgBytes = Convert.FromBase64String(cleaned);
                using var memStream = new MemoryStream(imgBytes);
                var decodedImg = Image.FromStream(memStream);
                CustomAvatar = (Image)decodedImg.Clone();
            }
            catch
            {
                // Fallback: don't crash
            }
        }

        public static void SetAvatarFromRaw(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                CustomAvatar = null;
                return;
            }

            try
            {
                string cleaned = raw.Trim();
                if (cleaned.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    using var client = new HttpClient();
                    var bytes = client.GetByteArrayAsync(cleaned).GetAwaiter().GetResult();
                    using var ms = new MemoryStream(bytes);
                    var img = Image.FromStream(ms);
                    CustomAvatar = (Image)img.Clone();
                    return;
                }

                int commaIdx = cleaned.IndexOf(',');
                if (commaIdx >= 0)
                {
                    cleaned = cleaned.Substring(commaIdx + 1).Trim();
                }

                byte[] imgBytes = Convert.FromBase64String(cleaned);
                using var memStream = new MemoryStream(imgBytes);
                var decodedImg = Image.FromStream(memStream);
                CustomAvatar = (Image)decodedImg.Clone();
            }
            catch
            {
                // Fallback
            }
        }

        /// <summary>Clears all session data on logout.</summary>
        public static void Clear()
        {
            UserId   = 0;
            FullName = string.Empty;
            Role     = string.Empty;
            Email    = string.Empty;
            JwtToken = string.Empty;
            CustomAvatar = null;
        }
    }
}