using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DriveAndGo_API.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);
        Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, string purpose);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public EmailService(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            string? apiKey = _configuration["RESEND_API_KEY"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY");
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine($"[DEV MODE - NO RESEND API KEY] Email to {toEmail} | Subject: {subject}");
                return true;
            }

            try
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

                var payload = new
                {
                    from = "DriveAndGo Security <onboarding@resend.dev>",
                    to = new[] { toEmail.Trim() },
                    subject = subject,
                    html = htmlBody
                };

                string jsonPayload = JsonSerializer.Serialize(payload);
                requestMessage.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(requestMessage);
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[RESEND EMAIL SUCCESS] Sent email to {toEmail} with subject: {subject}");
                    return true;
                }
                else
                {
                    string errResp = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[RESEND EMAIL ERROR] Status: {response.StatusCode}, Response: {errResp}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RESEND EMAIL EXCEPTION] Failed to send email to {toEmail}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, string purpose)
        {
            string title = purpose switch
            {
                "2FA" => "2FA Security Verification",
                "PASSWORD_RESET" => "Reset Account Password",
                "PASSWORD_CHANGE" => "Confirm Password Change",
                _ => "Security Verification Code"
            };

            string logoHeaderUrl = "https://raw.githubusercontent.com/martquirante/DriveAndGo_Project/main/DriveAndGo_Admin/WebAssets/logo.png";

            string htmlTemplate = $@"<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.0 Transitional//EN"" ""http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd"">
<html xmlns=""http://www.w3.org/1999/xhtml"">
<head>
    <meta http-equiv=""Content-Type"" content=""text/html; charset=UTF-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0""/>
    <meta name=""color-scheme"" content=""light dark"" />
    <meta name=""supported-color-schemes"" content=""light dark"" />
    <title>Drive&amp;Go Security Portal</title>
    <style type=""text/css"">
        :root {{
            color-scheme: light dark;
            supported-color-schemes: light dark;
        }}
        body {{ margin: 0; padding: 0; font-family: 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; -webkit-font-smoothing: antialiased; }}
        
        /* Dark Mode Overrides for Dark Mode Devices & Gmail Apps */
        @media (prefers-color-scheme: dark) {{
            .bg-body {{ background-color: #12121A !important; }}
            .bg-card {{ background-color: #232433 !important; border-color: #3A3B4C !important; }}
            .bg-otp  {{ background-color: #1A1B26 !important; border-color: #F97316 !important; }}
            .bg-footer {{ background-color: #16161E !important; border-top-color: #2D2E3F !important; }}
            .text-main {{ color: #FFFFFF !important; }}
            .text-sub  {{ color: #A0A0B0 !important; }}
            .text-muted {{ color: #888899 !important; }}
            .hr-border {{ border-top-color: #2D2E3F !important; }}
        }}

        [data-ogsc] .bg-body {{ background-color: #12121A !important; }}
        [data-ogsc] .bg-card {{ background-color: #232433 !important; border-color: #3A3B4C !important; }}
        [data-ogsc] .bg-otp  {{ background-color: #1A1B26 !important; border-color: #F97316 !important; }}
        [data-ogsc] .bg-footer {{ background-color: #16161E !important; border-top-color: #2D2E3F !important; }}
        [data-ogsc] .text-main {{ color: #FFFFFF !important; }}
        [data-ogsc] .text-sub  {{ color: #A0A0B0 !important; }}

        a {{ text-decoration: none; }}
        a:hover {{ text-decoration: underline !important; }}
    </style>
</head>
<body class=""bg-body"" style=""margin: 0; padding: 0; background-color: #F4F5FA;"">
    <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" class=""bg-body"" style=""background-color: #F4F5FA; padding: 32px 10px;"">
        <tr>
            <td align=""center"">
                <!-- Main Container Card (Adaptive Light/Dark Theme) -->
                <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" class=""bg-card"" style=""max-width: 480px; background-color: #FFFFFF; border: 1px solid #E2E8F0; border-radius: 16px; overflow: hidden; box-shadow: 0 10px 25px rgba(0,0,0,0.08);"">
                    <!-- Top Orange Accent Bar -->
                    <tr>
                        <td height=""4"" style=""background-color: #F97316; font-size: 0; line-height: 0;"">&nbsp;</td>
                    </tr>
                    
                    <!-- Header with System WebAssets Logo -->
                    <tr>
                        <td align=""center"" style=""padding: 32px 24px 16px 24px;"">
                            <table border=""0"" cellpadding=""0"" cellspacing=""0"">
                                <tr>
                                    <td align=""center"">
                                        <img src=""{logoHeaderUrl}"" alt=""Drive&amp;Go Logo"" width=""130"" style=""display: block; border: 0; width: 130px; height: auto; max-width: 130px; margin: 0 auto 8px auto;"" />
                                    </td>
                                </tr>
                                <tr>
                                    <td align=""center"" class=""text-sub"" style=""color: #475569; font-size: 12.5px; font-weight: 500; padding-top: 4px;"">
                                        Enterprise Security &amp; Authentication Portal
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Divider -->
                    <tr>
                        <td align=""center"" style=""padding: 0 28px;"">
                            <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"">
                                <tr>
                                    <td class=""hr-border"" style=""border-bottom: 1px solid #E2E8F0; font-size: 0; line-height: 0;"">&nbsp;</td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Body Content -->
                    <tr>
                        <td style=""padding: 24px 28px 20px 28px; text-align: left;"">
                            <h2 class=""text-main"" style=""color: #0F172A; font-size: 18px; font-weight: 700; margin: 0 0 10px 0; text-align: center;"">
                                {title}
                            </h2>
                            <p class=""text-sub"" style=""color: #475569; font-size: 13.5px; line-height: 1.5; margin: 0 0 20px 0; text-align: center;"">
                                Use the following 6-digit verification code to complete your security action.
                            </p>

                            <!-- OTP Box -->
                            <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""margin-bottom: 20px;"">
                                <tr>
                                    <td align=""center"">
                                        <table border=""0"" cellpadding=""0"" cellspacing=""0"" class=""bg-otp"" style=""background-color: #FFF7ED; border: 2px dashed #F97316; border-radius: 12px;"">
                                            <tr>
                                                <td align=""center"" style=""padding: 16px 36px; font-family: 'Courier New', Courier, monospace; font-size: 34px; font-weight: 800; color: #EA580C; letter-spacing: 8px; text-indent: 8px;"">
                                                    {otpCode}
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>

                            <!-- Expiry Alert Banner -->
                            <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background-color: #FFF7ED; border: 1px solid #FDBA74; border-radius: 8px; margin-bottom: 8px;"">
                                <tr>
                                    <td align=""center"" style=""padding: 10px 14px; color: #C2410C; font-size: 12.5px; font-weight: 600;"">
                                        ⚠️ This verification code will expire in <strong>exactly 2 minutes</strong>.
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Premium Spotify-Style Footer Block -->
                    <tr>
                        <td align=""center"" class=""bg-footer"" style=""padding: 20px 28px 24px 28px; background-color: #F8FAFC; border-top: 1px solid #E2E8F0;"">
                            <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"">
                                <!-- Brand Signature Header -->
                                <tr>
                                    <td align=""left"" style=""padding-bottom: 10px;"">
                                        <img src=""{logoHeaderUrl}"" alt=""Drive&amp;Go Logo"" width=""100"" style=""display: block; border: 0; width: 100px; height: auto; max-width: 100px;"" />
                                    </td>
                                </tr>
                                
                                <!-- Divider 1 -->
                                <tr>
                                    <td>
                                        <hr class=""hr-border"" style=""border: none; border-top: 1px solid #E2E8F0; margin: 0 0 12px 0;"" />
                                    </td>
                                </tr>

                                <!-- Recipient & Security Notice -->
                                <tr>
                                    <td align=""left"" class=""text-muted"" style=""color: #64748B; font-size: 11px; line-height: 1.5; padding-bottom: 12px;"">
                                        This message was sent to <a href=""mailto:{toEmail}"" style=""color: #EA580C; font-weight: 600; text-decoration: none;"">{toEmail}</a>. If you didn't attempt to log in or request this verification code, you can safely ignore this email. For security questions or complaints, please <a href=""mailto:support@driveandgo.ph"" class=""text-main"" style=""color: #0F172A; font-weight: bold; text-decoration: underline;"">contact support</a>.
                                    </td>
                                </tr>

                                <!-- Legal & Help Links (Clickable) -->
                                <tr>
                                    <td align=""left"" class=""text-muted"" style=""color: #64748B; font-size: 11px; padding-bottom: 10px;"">
                                        <a href=""https://driveandgo.ph/terms"" target=""_blank"" style=""color: #EA580C; text-decoration: none; font-weight: 600;"">Terms of Use</a> &nbsp;&bull;&nbsp; 
                                        <a href=""https://driveandgo.ph/privacy"" target=""_blank"" style=""color: #EA580C; text-decoration: none; font-weight: 600;"">Privacy Policy</a> &nbsp;&bull;&nbsp; 
                                        <a href=""mailto:support@driveandgo.ph"" style=""color: #EA580C; text-decoration: none; font-weight: 600;"">Contact Us</a>
                                    </td>
                                </tr>

                                <!-- Company Address & Copyright -->
                                <tr>
                                    <td align=""left"" style=""color: #94A3B8; font-size: 10px; line-height: 1.4;"">
                                        DriveAndGo Inc., CSJDM | Norzagaray, Bulacan, Philippines<br />
                                        &copy; 2026 DriveAndGo Inc. All rights reserved.
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

            Console.WriteLine($"[DEV MODE] OTP Code for {toEmail}: {otpCode} (Purpose: {purpose} - Expiring in 2 minutes)");

            // Send clean email subject WITHOUT exposing OTP code in subject line
            await SendEmailAsync(toEmail, $"Drive & Go: {title}", htmlTemplate);
            return true;
        }
    }
}
