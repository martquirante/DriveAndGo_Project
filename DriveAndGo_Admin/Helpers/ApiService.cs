using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DriveAndGo_Admin.Helpers
{
    // ──────────────────────────────────────────────────────────────────────
    //  DTOs returned by the DriveAndGo API
    // ──────────────────────────────────────────────────────────────────────

    public class LoginResponse
    {
        [JsonPropertyName("message")]      public string Message     { get; set; }
        [JsonPropertyName("userId")]       public int    UserId      { get; set; }
        [JsonPropertyName("fullName")]     public string FullName    { get; set; }
        [JsonPropertyName("email")]        public string Email       { get; set; }
        [JsonPropertyName("role")]         public string Role        { get; set; }
        [JsonPropertyName("token")]        public string Token       { get; set; }
        [JsonPropertyName("driverId")]     public int?   DriverId    { get; set; }
        [JsonPropertyName("requires2FA")]  public bool   Requires2FA { get; set; }
    }

    public class ApiResult
    {
        public bool    Success { get; set; }
        public string  Body    { get; set; }   // raw JSON string
        public string  Error   { get; set; }
        public int     StatusCode { get; set; }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  ApiService  — centralised HTTP client that calls DriveAndGo_API
    // ──────────────────────────────────────────────────────────────────────
    public static class ApiService
    {
        // ── Base URL: dynamically resolves active local Wi-Fi / LAN IP or Cloudflare/DevTunnel
        //              or production URL via environment variable API_BASE_URL / appsettings.json
        public static string ResolveNetworkBaseUrl()
        {
            var env = Environment.GetEnvironmentVariable("API_BASE_URL");
            if (!string.IsNullOrWhiteSpace(env)) return env.TrimEnd('/') + (env.EndsWith("/api", StringComparison.OrdinalIgnoreCase) ? "" : "/api");

            // Check if appsettings.json explicitly defines a BaseUrl in the Admin directory
            try
            {
                var localConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(localConfig))
                {
                    var json = File.ReadAllText(localConfig);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("ApiSettings", out var apiSettings) &&
                        apiSettings.TryGetProperty("BaseUrl", out var baseUrlProp))
                    {
                        var bUrl = baseUrlProp.GetString();
                        if (!string.IsNullOrWhiteSpace(bUrl) && 
                            !bUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) && 
                            !bUrl.Contains("127.0.0.1"))
                        {
                            return bUrl.TrimEnd('/') + (bUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase) ? "" : "/api");
                        }
                    }
                }
            }
            catch { }

            // Dynamic Active LAN IP Discovery via Routing Probe (auto-detects .6, .11, Wi-Fi, Ethernet)
            try
            {
                using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                if (socket.LocalEndPoint is System.Net.IPEndPoint endPoint &&
                    !System.Net.IPAddress.IsLoopback(endPoint.Address))
                {
                    return $"http://{endPoint.Address}:5233/api";
                }
            }
            catch { }

            // Computer Hostname adaptive fallback (permanent on LAN)
            try
            {
                var hostName = Environment.MachineName?.ToLowerInvariant() ?? "martquirante";
                return $"http://{hostName}:5233/api";
            }
            catch { }

            return "http://martquirante:5233/api";
        }

        public static readonly string BaseUrl = ResolveNetworkBaseUrl();

        private static readonly HttpClient _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private static readonly JsonSerializerOptions _jsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // ── Attach stored JWT token and active Admin Name header to every request ──
        private static void AttachBearerToken()
        {
            _client.DefaultRequestHeaders.Authorization =
                string.IsNullOrWhiteSpace(SessionManager.JwtToken)
                    ? null
                    : new AuthenticationHeaderValue("Bearer", SessionManager.JwtToken);

            string adminName = !string.IsNullOrWhiteSpace(SessionManager.FullName) ? SessionManager.FullName : "Raymart Quirante";
            _client.DefaultRequestHeaders.Remove("X-Admin-Name");
            _client.DefaultRequestHeaders.Add("X-Admin-Name", adminName);
        }

        // ─────────────────────────────────────────────
        //  AUTH & 2FA / OTP HELPERS
        // ─────────────────────────────────────────────

        /// <summary>
        /// Calls POST /api/auth/login. On success (without 2FA), populates SessionManager.
        /// Returns LoginResponse (check Requires2FA flag) or errorMessage on failure.
        /// </summary>
        public static async Task<(LoginResponse Response, string ErrorMessage)> LoginAsync(
            string email, string password)
        {
            string errorMessage = null;
            try
            {
                var payload = new { email, password };
                var json    = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PostAsync(BuildUrl("auth/login"), content);
                var body     = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    try
                    {
                        var errDoc = JsonDocument.Parse(body);
                        errorMessage = errDoc.RootElement.TryGetProperty("message", out var m)
                            ? m.GetString()
                            : $"HTTP {(int)response.StatusCode}";
                    }
                    catch { errorMessage = $"HTTP {(int)response.StatusCode}"; }
                    return (null, errorMessage);
                }

                var loginResp = JsonSerializer.Deserialize<LoginResponse>(body, _jsonOpts);

                if (loginResp != null && !loginResp.Requires2FA)
                {
                    // Populate session
                    SessionManager.UserId   = loginResp.UserId;
                    SessionManager.FullName = loginResp.FullName ?? string.Empty;
                    SessionManager.Email    = loginResp.Email    ?? string.Empty;
                    SessionManager.Role     = loginResp.Role     ?? string.Empty;
                    SessionManager.JwtToken = loginResp.Token    ?? string.Empty;
                }

                return (loginResp, null);
            }
            catch (Exception ex)
            {
                errorMessage = "Cannot reach API server. Is DriveAndGo_API running?\n\n" + ex.Message;
                return (null, errorMessage);
            }
        }

        /// <summary>
        /// Calls POST /api/auth/verify-2fa. On success, populates SessionManager.
        /// </summary>
        public static async Task<(LoginResponse Response, string ErrorMessage)> Verify2FaAsync(
            string email, string otp)
        {
            try
            {
                var payload = new { email, otp };
                var res = await PostAsync("auth/verify-2fa", payload);
                if (!res.Success)
                {
                    string err = ExtractMessage(res.Body) ?? res.Error ?? "2FA Verification failed.";
                    return (null, err);
                }

                var loginResp = Deserialize<LoginResponse>(res.Body);
                if (loginResp != null)
                {
                    SessionManager.UserId   = loginResp.UserId;
                    SessionManager.FullName = loginResp.FullName ?? string.Empty;
                    SessionManager.Email    = loginResp.Email    ?? string.Empty;
                    SessionManager.Role     = loginResp.Role     ?? string.Empty;
                    SessionManager.JwtToken = loginResp.Token    ?? string.Empty;
                }
                return (loginResp, null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        /// <summary>
        /// Calls POST /api/auth/send-reset-otp for Forgot Password.
        /// </summary>
        public static async Task<(bool Success, string Message)> SendResetOtpAsync(string email)
        {
            var res = await PostAsync("auth/send-reset-otp", new { email });
            if (!res.Success)
            {
                return (false, ExtractMessage(res.Body) ?? res.Error ?? "Failed to send reset code.");
            }
            return (true, ExtractMessage(res.Body) ?? "OTP verification code sent to your email.");
        }

        /// <summary>
        /// Calls POST /api/auth/verify-reset-otp for real-time validation.
        /// </summary>
        public static async Task<(bool Success, string Message)> VerifyResetOtpAsync(string email, string otp)
        {
            var res = await PostAsync("auth/verify-reset-otp", new { email, otp });
            if (!res.Success)
            {
                return (false, ExtractMessage(res.Body) ?? res.Error ?? "Invalid or expired verification code.");
            }
            return (true, ExtractMessage(res.Body) ?? "OTP code verified successfully.");
        }

        /// <summary>
        /// Calls POST /api/auth/reset-password-with-otp.
        /// </summary>
        public static async Task<(bool Success, string Message)> ResetPasswordWithOtpAsync(
            string email, string otp, string newPassword)
        {
            var res = await PostAsync("auth/reset-password-with-otp", new { email, otp, newPassword });
            if (!res.Success)
            {
                return (false, ExtractMessage(res.Body) ?? res.Error ?? "Failed to reset password.");
            }
            return (true, ExtractMessage(res.Body) ?? "Password reset successful!");
        }

        /// <summary>
        /// Calls POST /api/users/request-password-change-otp.
        /// </summary>
        public static async Task<(bool Success, string Message)> RequestPasswordChangeOtpAsync(
            int userId, string currentPassword)
        {
            var res = await PostAsync("users/request-password-change-otp", new { userId, currentPassword });
            if (!res.Success)
            {
                return (false, ExtractMessage(res.Body) ?? res.Error ?? "Failed to request password change OTP.");
            }
            return (true, ExtractMessage(res.Body) ?? "Verification OTP sent to your email.");
        }

        /// <summary>
        /// Calls POST /api/users/change-password-with-otp.
        /// </summary>
        public static async Task<(bool Success, string Message)> ChangePasswordWithOtpAsync(
            int userId, string currentPassword, string newPassword, string otp)
        {
            var res = await PostAsync("users/change-password-with-otp", new { userId, currentPassword, newPassword, otp });
            if (!res.Success)
            {
                return (false, ExtractMessage(res.Body) ?? res.Error ?? "Failed to update password.");
            }
            return (true, ExtractMessage(res.Body) ?? "Password updated successfully!");
        }

        /// <summary>
        /// Calls PUT /api/users/{userId}/security to persist 2FA & security toggles.
        /// </summary>
        public static async Task<bool> UpdateSecuritySettingsAsync(
            int userId, bool twoFactor, bool alerts, bool pin)
        {
            var payload = new { TwoFactorEnabled = twoFactor, LoginAlertsEnabled = alerts, PinRequired = pin };
            var res = await PutAsync($"users/{userId}/security", payload);
            return res.Success;
        }

        /// <summary>
        /// Calls GET /api/users/{userId}/security to fetch current 2FA, login alert, and PIN requirement toggles.
        /// </summary>
        public static async Task<(bool twoFactor, bool alerts, bool pin)> GetUserSecuritySettingsAsync(int userId)
        {
            var res = await GetAsync($"users/{userId}/security");
            if (res.Success && !string.IsNullOrWhiteSpace(res.Body))
            {
                try
                {
                    using var doc = JsonDocument.Parse(res.Body);
                    bool tf = (doc.RootElement.TryGetProperty("twoFactorEnabled", out var p1) || doc.RootElement.TryGetProperty("TwoFactorEnabled", out p1)) && p1.GetBoolean();
                    bool la = !(doc.RootElement.TryGetProperty("loginAlertsEnabled", out var p2) || doc.RootElement.TryGetProperty("LoginAlertsEnabled", out p2)) || p2.GetBoolean();
                    bool pr = (doc.RootElement.TryGetProperty("pinRequired", out var p3) || doc.RootElement.TryGetProperty("PinRequired", out p3)) && p3.GetBoolean();
                    return (tf, la, pr);
                }
                catch { }
            }
            return (false, true, false);
        }

        /// <summary>
        /// Calls GET /api/users/{userId}/activity to retrieve the user's audit activity timeline.
        /// </summary>
        public static async Task<List<ActivityLogDto>> GetActivityLogsAsync(int userId)
        {
            var list = new List<ActivityLogDto>();
            var res = await GetAsync($"users/{userId}/activity");
            if (res.Success && !string.IsNullOrWhiteSpace(res.Body))
            {
                try
                {
                    using var doc = JsonDocument.Parse(res.Body);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            int auditId = (el.TryGetProperty("auditId", out var p1) || el.TryGetProperty("AuditId", out p1)) ? p1.GetInt32() : 0;
                            string actionType = (el.TryGetProperty("actionType", out var p2) || el.TryGetProperty("ActionType", out p2)) ? p2.GetString() ?? "" : "";
                            string description = (el.TryGetProperty("description", out var p3) || el.TryGetProperty("Description", out p3)) ? p3.GetString() ?? "" : "";
                            DateTime timestamp = (el.TryGetProperty("timestamp", out var p4) || el.TryGetProperty("Timestamp", out p4)) ? p4.GetDateTime() : DateTime.UtcNow;
                            string ip = (el.TryGetProperty("ipAddress", out var p5) || el.TryGetProperty("IpAddress", out p5)) ? p5.GetString() ?? "127.0.0.1" : "127.0.0.1";

                            list.Add(new ActivityLogDto
                            {
                                AuditId = auditId,
                                ActionType = actionType,
                                Description = description,
                                Timestamp = timestamp,
                                IpAddress = ip
                            });
                        }
                    }
                }
                catch { }
            }
            return list;
        }

        private static string ExtractMessage(string jsonBody)
        {
            if (string.IsNullOrWhiteSpace(jsonBody)) return null;
            try
            {
                using var doc = JsonDocument.Parse(jsonBody);
                if (doc.RootElement.TryGetProperty("message", out var m)) return m.GetString();
                if (doc.RootElement.TryGetProperty("Message", out var m2)) return m2.GetString();
            }
            catch { }
            return null;
        }

        // ─────────────────────────────────────────────
        //  GENERIC REST HELPERS
        // ─────────────────────────────────────────────

        public static async Task<ApiResult> GetAsync(string endpoint)
        {
            AttachBearerToken();
            try
            {
                var response = await _client.GetAsync(BuildUrl(endpoint));
                var body     = await response.Content.ReadAsStringAsync();
                return new ApiResult
                {
                    Success    = response.IsSuccessStatusCode,
                    Body       = body,
                    StatusCode = (int)response.StatusCode
                };
            }
            catch (Exception ex)
            {
                return new ApiResult { Success = false, Error = ex.Message };
            }
        }

        public static async Task<ApiResult> PostAsync(string endpoint, object data)
        {
            AttachBearerToken();
            try
            {
                var json     = JsonSerializer.Serialize(data);
                var content  = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _client.PostAsync(BuildUrl(endpoint), content);
                var body     = await response.Content.ReadAsStringAsync();
                return new ApiResult
                {
                    Success    = response.IsSuccessStatusCode,
                    Body       = body,
                    StatusCode = (int)response.StatusCode
                };
            }
            catch (Exception ex)
            {
                return new ApiResult { Success = false, Error = ex.Message };
            }
        }

        public static async Task<ApiResult> PatchAsync(string endpoint, object data = null)
        {
            AttachBearerToken();
            try
            {
                HttpContent content = null;
                if (data != null)
                {
                    var json = JsonSerializer.Serialize(data);
                    content  = new StringContent(json, Encoding.UTF8, "application/json");
                }
                var response = await _client.PatchAsync(BuildUrl(endpoint), content);
                var body     = await response.Content.ReadAsStringAsync();
                return new ApiResult
                {
                    Success    = response.IsSuccessStatusCode,
                    Body       = body,
                    StatusCode = (int)response.StatusCode
                };
            }
            catch (Exception ex)
            {
                return new ApiResult { Success = false, Error = ex.Message };
            }
        }

        public static async Task<ApiResult> PutAsync(string endpoint, object data)
        {
            AttachBearerToken();
            try
            {
                var json     = JsonSerializer.Serialize(data);
                var content  = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _client.PutAsync(BuildUrl(endpoint), content);
                var body     = await response.Content.ReadAsStringAsync();
                return new ApiResult
                {
                    Success    = response.IsSuccessStatusCode,
                    Body       = body,
                    StatusCode = (int)response.StatusCode
                };
            }
            catch (Exception ex)
            {
                return new ApiResult { Success = false, Error = ex.Message };
            }
        }

        public static async Task<ApiResult> DeleteAsync(string endpoint)
        {
            AttachBearerToken();
            try
            {
                var response = await _client.DeleteAsync(BuildUrl(endpoint));
                var body     = await response.Content.ReadAsStringAsync();
                return new ApiResult
                {
                    Success    = response.IsSuccessStatusCode,
                    Body       = body,
                    StatusCode = (int)response.StatusCode
                };
            }
            catch (Exception ex)
            {
                return new ApiResult { Success = false, Error = ex.Message };
            }
        }

        // ─────────────────────────────────────────────
        //  URL BUILDER
        // ─────────────────────────────────────────────

        public static string BuildUrl(string endpoint) =>
            $"{BaseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";

        // ─────────────────────────────────────────────
        //  CONVENIENCE DESERIALIZATION
        // ─────────────────────────────────────────────

        public static string CleanErrorMessage(string rawError)
        {
            if (string.IsNullOrWhiteSpace(rawError))
                return "We encountered a temporary server issue. Please try again in a moment.";

            string lower = rawError.ToLowerInvariant();

            if (lower.Contains("refused") || lower.Contains("connection") || lower.Contains("5233") || lower.Contains("cannot connect") || lower.Contains("socket"))
                return "Unable to connect to the backend server. Please check your internet connection or verify that the server is running.";

            if (lower.Contains("23505") || lower.Contains("duplicate") || lower.Contains("already exists") || lower.Contains("unique"))
                return "A record with the same details (such as plate number, email, or booking) already exists. Please review your input.";

            if (lower.Contains("23503") || lower.Contains("foreign key"))
                return "This action cannot be completed because this record is linked to other active items in the system.";

            if (lower.Contains("db error") || lower.Contains("postgres") || lower.Contains("database"))
                return "Our database service is temporarily unreachable. Please try again in a moment.";

            if (lower.Contains("exception") || lower.Contains("error:") || lower.Contains("failed to"))
                return "We encountered a temporary issue processing your request. Please try again in a moment.";

            return rawError;
        }

        public static T Deserialize<T>(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json, _jsonOpts);
            }
            catch
            {
                return default;
            }
        }
    }

    public class ActivityLogDto
    {
        public int AuditId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string IpAddress { get; set; } = "127.0.0.1";
    }
}
