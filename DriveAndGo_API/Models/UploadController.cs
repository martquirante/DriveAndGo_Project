using Microsoft.AspNetCore.Mvc;

namespace DriveAndGo_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;

    public UploadController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpPost("vehicle-image")]
    public Task<IActionResult> UploadVehicleImage(IFormFile file)
    {
        return Upload(file, "vehicles", allowVideo: true);
    }

    [HttpPost("map-icon")]
    public Task<IActionResult> UploadMapIcon(IFormFile file)
    {
        return Upload(file, "mapicons");
    }

    [HttpPost("payment-proof")]
    public Task<IActionResult> UploadPaymentProof(IFormFile file)
    {
        return Upload(file, "payments");
    }

    [HttpPost("issue-image")]
    public Task<IActionResult> UploadIssueImage(IFormFile file)
    {
        return Upload(file, "issues");
    }

    [HttpPost("message-attachment")]
    public Task<IActionResult> UploadMessageAttachment(IFormFile file)
    {
        return Upload(file, "messages", allowVideo: true);
    }

    private async Task<IActionResult> Upload(IFormFile file, string folderName, bool allowVideo = false)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { Message = "No file uploaded." });
        }

        var allowedExtensions = allowVideo
            ? new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".jfif", ".mp4", ".webm", ".mov", ".m4v" }
            : new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".jfif" };

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { Message = "Invalid file type." });
        }

        var folder = Path.Combine(_environment.ContentRootPath, "wwwroot", "uploads", folderName);
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(folder, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var url = $"{Request.Scheme}://{Request.Host}/uploads/{folderName}/{fileName}";
        return Ok(new { Url = url });
    }
}
