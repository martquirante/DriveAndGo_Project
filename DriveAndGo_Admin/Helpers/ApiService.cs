using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DriveAndGo_Admin.Helpers
{
    // ──────────────────────────────────────────────────────────────────────
    //  DTOs returned by the DriveAndGo API
    // ──────────────────────────────────────────────────────────────────────

    public class LoginResponse
    {
        [JsonPropertyName("message")]   public string Message  { get; set; }
        [JsonPropertyName("userId")]    public int    UserId   { get; set; }
        [JsonPropertyName("fullName")]  public string FullName { get; set; }
        [JsonPropertyName("email")]     public string Email    { get; set; }
        [JsonPropertyName("role")]      public string Role     { get; set; }
        [JsonPropertyName("token")]     public string Token    { get; set; }
        [JsonPropertyName("driverId")]  public int?   DriverId { get; set; }
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
        // ── Base URL: local dev = http://localhost:5233
        //              production = set via environment variable API_BASE_URL
        public static readonly string BaseUrl =
            Environment.GetEnvironmentVariable("API_BASE_URL")
            ?? "http://localhost:5233/api";

        private static readonly HttpClient _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private static readonly JsonSerializerOptions _jsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // ── Attach the stored JWT token to every request ──────────────────
        private static void AttachBearerToken()
        {
            _client.DefaultRequestHeaders.Authorization =
                string.IsNullOrWhiteSpace(SessionManager.JwtToken)
                    ? null
                    : new AuthenticationHeaderValue("Bearer", SessionManager.JwtToken);
        }

        // ─────────────────────────────────────────────
        //  AUTH
        // ─────────────────────────────────────────────

        /// <summary>
        /// Calls POST /api/auth/login.  On success, populates SessionManager.
        /// Returns null and sets errorMessage on failure.
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

                // Populate session
                SessionManager.UserId   = loginResp.UserId;
                SessionManager.FullName = loginResp.FullName ?? string.Empty;
                SessionManager.Email    = loginResp.Email    ?? string.Empty;
                SessionManager.Role     = loginResp.Role     ?? string.Empty;
                SessionManager.JwtToken = loginResp.Token    ?? string.Empty;

                return (loginResp, null);
            }
            catch (Exception ex)
            {
                errorMessage = "Cannot reach API server. Is DriveAndGo_API running?\n\n" + ex.Message;
                return (null, errorMessage);
            }
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
}

