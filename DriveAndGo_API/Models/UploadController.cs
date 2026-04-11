using Microsoft.AspNetCore.Mvc;

namespace DriveAndGo_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public UploadController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpPost("vehicle-image")]
        public async Task<IActionResult> UploadVehicleImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded." });

            var allowedExtensions = new[]
            {
                ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".jfif",
                ".mp4", ".webm", ".mov", ".m4v"
            };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(ext))
                return BadRequest(new { message = "Invalid media type." });

            var folder = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "vehicles");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(folder, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"{Request.Scheme}://{Request.Host}/uploads/vehicles/{fileName}";
            return Ok(new { url });
        }

        [HttpPost("map-icon")]
        public async Task<IActionResult> UploadMapIcon(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded." });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".jfif" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(ext))
                return BadRequest(new { message = "Invalid file type." });

            var folder = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "mapicons");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(folder, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"{Request.Scheme}://{Request.Host}/uploads/mapicons/{fileName}";
            return Ok(new { url });
        }
    }
}
