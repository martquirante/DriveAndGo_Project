using DriveAndGo_API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace DriveAndGo_API.Controllers
{
    [ApiController]
    [Route("api/media")]
    public class ImageUploadController : ControllerBase
    {
        private readonly IStorageService _storageService;

        public ImageUploadController(IStorageService storageService)
        {
            _storageService = storageService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadImage([FromForm] IFormFile file, [FromForm] string folderName = "pfp")
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
                return StatusCode(500, new { Message = "Internal server error: " + ex.Message });
            }
        }
    }
}
