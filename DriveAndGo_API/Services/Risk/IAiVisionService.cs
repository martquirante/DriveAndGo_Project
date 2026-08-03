using DriveAndGo_API.Models.Risk;

namespace DriveAndGo_API.Services.Risk;

/// <summary>
/// Contract for AI Vision Service powered by Gemini 1.5 Flash multimodal REST API.
/// Performs driver license KYC fraud inspection and vehicle damage assessment.
/// </summary>
public interface IAiVisionService
{
    /// <summary>
    /// Analyzes a Base64-encoded driver license image for OCR extraction and fraud detection.
    /// </summary>
    Task<LicenseAnalysisResultDto> AnalyzeDriverLicenseAsync(string base64Image);

    /// <summary>
    /// Evaluates a Base64-encoded vehicle damage photo for severity classification and repair cost estimation.
    /// </summary>
    Task<DamageAssessmentResultDto> AssessVehicleDamageAsync(string base64Image, string description);
}
