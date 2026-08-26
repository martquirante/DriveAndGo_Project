using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;
using System.Text.Json;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocVaultController : ControllerBase
    {
        private readonly NpgsqlDataSource _ds;

        public DocVaultController(NpgsqlDataSource ds)
        {
            _ds = ds;
        }

        // GET /api/docvault/alerts
        [HttpGet("alerts")]
        public async Task<IActionResult> GetDocumentAlerts()
        {
            try
            {
                var alerts = new List<object>();
                await using var conn = await _ds.OpenConnectionAsync();

                // 1. Query Driver licenses expiring soon (within 30 days or already expired)
                await using var cmdDrivers = new NpgsqlCommand(@"
                    SELECT d.driver_id, COALESCE(u.full_name, 'Driver #' || d.driver_id) AS full_name, d.license_expiry
                    FROM drivers d
                    JOIN users u ON d.user_id = u.user_id
                    WHERE d.license_expiry IS NOT NULL AND d.license_expiry <= NOW() + INTERVAL '30 days'
                    ORDER BY d.license_expiry ASC", conn);


                await using var readerD = await cmdDrivers.ExecuteReaderAsync();
                while (await readerD.ReadAsync())
                {
                    DateTime expiry = readerD.GetDateTime(readerD.GetOrdinal("license_expiry"));
                    bool expired = expiry < DateTime.Now;
                    int daysLeft = (expiry - DateTime.Now).Days;

                    alerts.Add(new
                    {
                        type = "driver_license",
                        targetId = readerD["driver_id"].ToString(),
                        targetName = readerD["full_name"].ToString(),
                        documentType = "Driver's License",
                        expiryDate = expiry.ToString("yyyy-MM-dd"),
                        status = expired ? "Expired" : "Expiring Soon",
                        severity = expired ? "high" : (daysLeft <= 7 ? "medium" : "low"),
                        message = expired 
                            ? $"License of {readerD["full_name"]} has expired!"
                            : $"License of {readerD["full_name"]} will expire in {daysLeft} days."
                    });
                }
                await readerD.CloseAsync();

                // 2. Query Vehicle insurances expiring (simulate since insurance details might be static/in-memory or in metadata)
                // Let's add some mock system alerts for fleet documents to simulate defense-grade OCR validation
                alerts.Add(new
                {
                    type = "vehicle_insurance",
                    targetId = "1",
                    targetName = "Nissan Navara (LND-482)",
                    documentType = "Comprehensive Insurance Certificate",
                    expiryDate = DateTime.Now.AddDays(12).ToString("yyyy-MM-dd"),
                    status = "Expiring Soon",
                    severity = "medium",
                    message = "Nissan Navara Comprehensive Insurance is expiring in 12 days."
                });

                alerts.Add(new
                {
                    type = "vehicle_registration",
                    targetId = "2",
                    targetName = "Hyundai Tucson (ZPR-918)",
                    documentType = "LTO CR/OR Registration",
                    expiryDate = DateTime.Now.AddDays(-3).ToString("yyyy-MM-dd"),
                    status = "Expired",
                    severity = "high",
                    message = "LTO Registration Certificate for Hyundai Tucson expired 3 days ago!"
                });

                return Ok(alerts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Error loading document alerts: " + ex.Message });
            }
        }

        // POST /api/docvault/ocr-validate
        [HttpPost("ocr-validate")]
        public async Task<IActionResult> OcrValidate([FromForm] IFormFile? documentFile, [FromForm] string? docType)
        {
            try
            {
                if (documentFile == null)
                {
                    return BadRequest(new { Message = "No file uploaded for OCR validation." });
                }

                // Simulate OCR process (Tesseract/Google Vision Cloud Mock)
                string filename = documentFile.FileName.ToLower();
                bool isValid = true;
                string fullName = "JOHN MARSTON";
                string docNumber = "N01-24-918233";
                string parsedExpiry = DateTime.Now.AddYears(3).ToString("yyyy-MM-dd");
                string confidence = "96.4%";

                if (filename.Contains("invalid") || filename.Contains("expired"))
                {
                    isValid = false;
                    parsedExpiry = DateTime.Now.AddDays(-10).ToString("yyyy-MM-dd");
                    confidence = "81.2%";
                }

                await Task.Delay(1200); // Simulate API latency of OCR engine

                return Ok(new
                {
                    success = true,
                    message = "Document OCR Analysis Completed.",
                    extractedData = new
                    {
                        fullName = fullName,
                        documentNumber = docNumber,
                        expirationDate = parsedExpiry,
                        documentType = docType ?? "Driver License",
                        confidenceScore = confidence,
                        validationStatus = isValid ? "Approved" : "Rejected",
                        rejectionFlags = isValid ? new string[] { } : new string[] { "Expired Credential", "Low Resolution Image" }
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "OCR parsing failed: " + ex.Message });
            }
        }
    }
}
