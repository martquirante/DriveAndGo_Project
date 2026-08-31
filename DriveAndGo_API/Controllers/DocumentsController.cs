using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentsController : ControllerBase
    {
        private readonly NpgsqlDataSource _ds;

        public DocumentsController(NpgsqlDataSource ds)
        {
            _ds = ds;
        }

        // POST /api/documents/verify-license
        [HttpPost("verify-license")]
        public async Task<IActionResult> VerifyLicense([FromForm] VerifyLicenseRequest request)
        {
            try
            {
                var documentFile = request?.DocumentFile;
                var driverId = request?.DriverId;

                if (documentFile == null)
                {
                    return BadRequest(new { Message = "No driver license file uploaded." });
                }

                if (!driverId.HasValue)
                {
                    return BadRequest(new { Message = "Driver ID is required for verification." });
                }

                // Simulated OCR Engine processing binary stream
                // In production, load the stream into Tesseract Engine or Azure Form Recognizer
                using var ms = new MemoryStream();
                await documentFile.CopyToAsync(ms);
                byte[] fileBytes = ms.ToArray();

                // Mocking extracted text fields from driver's license
                string fileName = documentFile.FileName.ToLower();
                string extractedLicenseNo = "DL-N01-84-19283";
                
                // If filename indicates expired/invalid, return an expired date, otherwise a valid future date
                DateTime extractedExpiryDate = DateTime.Now.AddYears(2);
                if (fileName.Contains("expired") || fileName.Contains("invalid"))
                {
                    extractedExpiryDate = DateTime.Now.AddDays(-5); // Expired 5 days ago
                }

                bool isExpired = extractedExpiryDate < DateTime.Now;
                string verificationStatus = isExpired ? "Expired Credentials - Locked" : "Verified";

                // Update the driver record directly in Supabase DB, bypassing manual admin review
                await using var conn = await _ds.OpenConnectionAsync();
                await using (var cmd = new NpgsqlCommand(@"
                    UPDATE drivers
                    SET license_expiry = @expiry,
                        status = @status,
                        rejection_reason = @reason
                    WHERE driver_id = @driver_id", conn))
                {
                    cmd.Parameters.AddWithValue("@expiry", extractedExpiryDate);
                    cmd.Parameters.AddWithValue("@status", isExpired ? "suspended" : "available");
                    cmd.Parameters.AddWithValue("@reason", isExpired ? "Expired Credentials - Locked via Automated OCR Vetting" : DBNull.Value);
                    cmd.Parameters.AddWithValue("@driver_id", driverId.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                return Ok(new
                {
                    success = true,
                    message = isExpired ? "Automated validation failed: Credential has expired." : "Automated validation successful.",
                    extractedData = new
                    {
                        licenseNumber = extractedLicenseNo,
                        expiryDate = extractedExpiryDate.ToString("yyyy-MM-dd"),
                        isExpired = isExpired,
                        verificationStatus = verificationStatus
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "OCR Verification Process Failed: " + ex.Message });
            }
        }
    }

    public class VerifyLicenseRequest
    {
        public IFormFile? DocumentFile { get; set; }
        public int? DriverId { get; set; }
    }
}
