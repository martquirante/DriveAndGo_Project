using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DriveAndGo_Admin.Helpers
{
    public static class ApiService
    {
        // Change this to your deployed URL kapag naka-deploy na
        private static readonly string BaseUrl =
            "http://localhost:5233/api";

        private static readonly HttpClient _client = new HttpClient();

        public static async Task<string> GetAsync(string endpoint)
        {
            var response = await _client.GetAsync(
                BuildUrl(endpoint));
            return await response.Content.ReadAsStringAsync();
        }

        public static async Task<string> PostAsync(
            string endpoint, object data)
        {
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(
                json, Encoding.UTF8, "application/json");
            var response = await _client.PostAsync(
                BuildUrl(endpoint), content);
            return await response.Content.ReadAsStringAsync();
        }

        public static async Task<string> PatchAsync(string endpoint)
        {
            var response = await _client.PatchAsync(
                BuildUrl(endpoint), null);
            return await response.Content.ReadAsStringAsync();
        }

        public static async Task<string> DeleteAsync(string endpoint)
        {
            var response = await _client.DeleteAsync(
                BuildUrl(endpoint));
            return await response.Content.ReadAsStringAsync();
        }

        public static async Task<string> PutAsync(
            string endpoint, object data)
        {
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(
                json, Encoding.UTF8, "application/json");
            var response = await _client.PutAsync(
                BuildUrl(endpoint), content);
            return await response.Content.ReadAsStringAsync();
        }

        public static string BuildUrl(string endpoint)
        {
            return $"{BaseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";
        }
    }
}
