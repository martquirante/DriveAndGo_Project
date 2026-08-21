using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace DriveAndGo_API.Services
{
    public interface IStorageService
    {
        Task<string> UploadFileAsync(IFormFile file, string folderName);
    }

    public class StorageService : IStorageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public StorageService(IWebHostEnvironment environment, IConfiguration configuration, HttpClient httpClient)
        {
            _environment = environment;
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public async Task<string> UploadFileAsync(IFormFile file, string folderName)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("No file uploaded.");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".jfif" };
            if (!System.Linq.Enumerable.Contains(allowedExtensions, extension))
            {
                throw new ArgumentException("Invalid file type.");
            }

            // Determine environment (Development vs Production)
            var isProduction = _environment.IsProduction();
            var envString = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (!string.IsNullOrEmpty(envString) && envString.Equals("Production", StringComparison.OrdinalIgnoreCase))
            {
                isProduction = true;
            }

            var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL") ?? _configuration["Supabase:Url"];
            var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_SECRET_KEY") ?? _configuration["Supabase:SecretKey"];

            if (isProduction && !string.IsNullOrEmpty(supabaseUrl) && !string.IsNullOrEmpty(supabaseKey))
            {
                // Supabase Storage Bucket: avatars (or user-documents)
                string bucket = (folderName == "avatars" || folderName == "pfp") ? "avatars" : "user-documents";
                var fileName = $"{Guid.NewGuid():N}{extension}";
                var uploadUrl = $"{supabaseUrl.TrimEnd('/')}/storage/v1/object/{bucket}/{fileName}";

                using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
                request.Headers.Add("apikey", supabaseKey);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);

                using var stream = file.OpenReadStream();
                using var content = new StreamContent(stream);
                content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "image/jpeg");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return $"{supabaseUrl.TrimEnd('/')}/storage/v1/object/public/{bucket}/{fileName}";
                }
                
                var errorText = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Supabase upload failed: {response.StatusCode} - {errorText}");
                throw new Exception($"Supabase Storage upload failed: {response.ReasonPhrase}");
            }

            // Development Environment: Save locally in wwwroot/uploads
            var folder = Path.Combine(_environment.ContentRootPath, "wwwroot", "uploads", folderName);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var localFileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(folder, localFileName);

            await using (var localStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(localStream);
            }

            // Return network-accessible local hosting address
            string serverBase = DriveAndGo_API.Helpers.NetworkHelper.GetServerBaseUrl(_configuration);
            return $"{serverBase}/uploads/{folderName}/{localFileName}";
        }
    }
}
