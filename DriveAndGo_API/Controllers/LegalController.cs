using Microsoft.AspNetCore.Mvc;

namespace DriveAndGo_API.Controllers
{
    [ApiController]
    public class LegalController : ControllerBase
    {
        [HttpGet("/terms")]
        [Produces("text/html")]
        public IActionResult GetTermsOfUse()
        {
            string html = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Terms of Use - Drive&amp;Go Enterprise</title>
    <style>
        body { font-family: 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #12121A; color: #E2E8F0; margin: 0; padding: 40px 20px; line-height: 1.6; }
        .container { max-width: 800px; margin: 0 auto; background: #1E1F2E; padding: 40px; border-radius: 16px; border: 1px solid #3A3B4C; box-shadow: 0 10px 30px rgba(0,0,0,0.5); }
        h1 { color: #F97316; font-size: 28px; margin-bottom: 8px; }
        h2 { color: #FFFFFF; font-size: 20px; border-bottom: 1px solid #3A3B4C; padding-bottom: 8px; margin-top: 30px; }
        p, li { color: #A0A0B0; font-size: 15px; }
        .badge { display: inline-block; background: rgba(249,115,22,0.15); color: #F97316; padding: 4px 12px; border-radius: 20px; font-size: 13px; font-weight: 600; margin-bottom: 24px; }
        .footer { margin-top: 40px; border-top: 1px solid #3A3B4C; padding-top: 20px; text-align: center; color: #64748B; font-size: 13px; }
    </style>
</head>
<body>
    <div class=""container"">
        <span class=""badge"">Drive&amp;Go Legal &amp; Compliance</span>
        <h1>Terms of Use &amp; Service Agreement</h1>
        <p>Last updated: August 6, 2026</p>
        
        <h2>1. Acceptance of Terms</h2>
        <p>By accessing or using the Drive&amp;Go Enterprise Fleet Management and Rental System (""Platform""), including the Admin Portal, Mobile Application, and Web API, you agree to be bound by these Terms of Use.</p>

        <h2>2. Security &amp; Account Responsibilities</h2>
        <p>Users are responsible for maintaining the confidentiality of their credentials and Two-Factor Authentication (2FA) security codes. Any unauthorized access resulting from compromised credentials must be reported immediately to Drive&amp;Go Security Team.</p>

        <h2>3. Acceptable Use Policy</h2>
        <p>You agree not to reverse engineer, disrupt, or attempt unauthorized access to the Drive&amp;Go API endpoints, database servers, or telemetry streams.</p>

        <h2>4. Termination of Access</h2>
        <p>Drive&amp;Go reserves the right to suspend or terminate accounts that violate system policies, exhibit suspicious activity, or fail security verification challenges.</p>

        <div class=""footer"">
            &copy; 2026 DriveAndGo Inc., CSJDM | Norzagaray, Bulacan, Philippines. All rights reserved.
        </div>
    </div>
</body>
</html>";
            return Content(html, "text/html");
        }

        [HttpGet("/privacy")]
        [Produces("text/html")]
        public IActionResult GetPrivacyPolicy()
        {
            string html = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Privacy Policy - Drive&amp;Go Enterprise</title>
    <style>
        body { font-family: 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #12121A; color: #E2E8F0; margin: 0; padding: 40px 20px; line-height: 1.6; }
        .container { max-width: 800px; margin: 0 auto; background: #1E1F2E; padding: 40px; border-radius: 16px; border: 1px solid #3A3B4C; box-shadow: 0 10px 30px rgba(0,0,0,0.5); }
        h1 { color: #F97316; font-size: 28px; margin-bottom: 8px; }
        h2 { color: #FFFFFF; font-size: 20px; border-bottom: 1px solid #3A3B4C; padding-bottom: 8px; margin-top: 30px; }
        p, li { color: #A0A0B0; font-size: 15px; }
        .badge { display: inline-block; background: rgba(249,115,22,0.15); color: #F97316; padding: 4px 12px; border-radius: 20px; font-size: 13px; font-weight: 600; margin-bottom: 24px; }
        .footer { margin-top: 40px; border-top: 1px solid #3A3B4C; padding-top: 20px; text-align: center; color: #64748B; font-size: 13px; }
    </style>
</head>
<body>
    <div class=""container"">
        <span class=""badge"">Drive&amp;Go Privacy &amp; Data Protection</span>
        <h1>Privacy Policy</h1>
        <p>Last updated: August 6, 2026</p>

        <h2>1. Information We Collect</h2>
        <p>Drive&amp;Go collects account details (email address, full name, phone number), authentication logs, 2FA verification tokens, and GPS telemetry data required for vehicle tracking and operational security.</p>

        <h2>2. How We Protect Your Data</h2>
        <p>All sensitive information, including user passwords and security tokens, is hashed using Industry-Standard BCrypt encryption and transmitted exclusively over HTTPS SSL channels.</p>

        <h2>3. Data Sharing &amp; Third Parties</h2>
        <p>We do not sell or rent personal data. Security emails and OTP notifications are processed securely via Resend API under strict data privacy compliance.</p>

        <h2>4. Contact Us Regarding Privacy</h2>
        <p>If you have questions regarding data privacy, contact our Data Protection Officer at <a href=""mailto:support@driveandgo.ph"" style=""color:#F97316;"">support@driveandgo.ph</a>.</p>

        <div class=""footer"">
            &copy; 2026 DriveAndGo Inc., CSJDM | Norzagaray, Bulacan, Philippines. All rights reserved.
        </div>
    </div>
</body>
</html>";
            return Content(html, "text/html");
        }
    }
}
