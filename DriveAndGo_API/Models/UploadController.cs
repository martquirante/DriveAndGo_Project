using DriveAndGo_API.Services;
using Microsoft.AspNetCore.Mvc;

namespace DriveAndGo_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private readonly IStorageService _storageService;

    public UploadController(IStorageService storageService)
    {
        _storageService = storageService;
    }

    [HttpPost("vehicle-image")]
    public Task<IActionResult> UploadVehicleImage(IFormFile file)
    {
        return Upload(file, "vehicles");
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
        return Upload(file, "messages");
    }

    private async Task<IActionResult> Upload(IFormFile file, string folderName)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { Message = "No file uploaded." });
            }

            var url = await _storageService.UploadFileAsync(file, folderName);
            return Ok(new { Url = url });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Upload error: " + ex.Message });
        }
    }
}
