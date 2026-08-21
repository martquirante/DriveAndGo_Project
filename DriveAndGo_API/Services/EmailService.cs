using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DriveAndGo_API.Helpers;
using DriveAndGo_API.Models;

namespace DriveAndGo_API.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);
        Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, string purpose);
        Task<(bool Success, string Message, string? ResendId)> SendRentalAgreementAsync(
            string toEmail,
            string? ccEmail,
            string? subject,
            string? personalMessage,
            RentalAgreementEmailData data,
            byte[]? pdfAttachment = null);
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

        private async Task<bool> TrySendViaSmtpAsync(string toEmail, string? ccEmail, string subject, string htmlBody, byte[]? pdfAttachment = null, string? attachmentFilename = null)
        {
            string? smtpEmail = _configuration["Smtp:Email"] ?? Environment.GetEnvironmentVariable("SMTP_EMAIL");
            string? smtpPass = _configuration["Smtp:AppPassword"] ?? _configuration["Smtp:Password"] ?? Environment.GetEnvironmentVariable("SMTP_APP_PASSWORD");

            if (string.IsNullOrWhiteSpace(smtpEmail) || string.IsNullOrWhiteSpace(smtpPass) || smtpPass.Contains("YOUR_") || smtpEmail.Contains("YOUR_"))
            {
                return false;
            }

            try
            {
                using var client = new System.Net.Mail.SmtpClient("smtp.gmail.com", 587)
                {
                    EnableSsl = true,
                    Credentials = new System.Net.NetworkCredential(smtpEmail.Trim(), smtpPass.Trim().Replace(" ", "")),
                    DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network,
                    Timeout = 20000
                };

                using var message = new System.Net.Mail.MailMessage
                {
                    From = new System.Net.Mail.MailAddress(smtpEmail.Trim(), "DriveAndGo Inc."),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                message.To.Add(toEmail.Trim());

                if (!string.IsNullOrWhiteSpace(ccEmail))
                {
                    foreach (var cc in ccEmail.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = cc.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmed)) message.CC.Add(trimmed);
                    }
                }

                if (pdfAttachment != null && pdfAttachment.Length > 0)
                {
                    var stream = new MemoryStream(pdfAttachment);
                    var attachment = new System.Net.Mail.Attachment(stream, attachmentFilename ?? "Rental_Agreement.pdf", "application/pdf");
                    message.Attachments.Add(attachment);
                }

                await client.SendMailAsync(message);
                Console.WriteLine($"[GMAIL SMTP SUCCESS] Sent email to {toEmail} with subject: {subject}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GMAIL SMTP ERROR] Failed to send via SMTP: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                if (await TrySendViaSmtpAsync(toEmail, null, subject, htmlBody))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SMTP FALLBACK TO RESEND] SMTP failed: {ex.Message}");
            }

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
                                        This verification code will expire in <strong>exactly 2 minutes</strong>.
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

        public async Task<(bool Success, string Message, string? ResendId)> SendRentalAgreementAsync(
            string toEmail,
            string? ccEmail,
            string? subject,
            string? personalMessage,
            RentalAgreementEmailData data,
            byte[]? pdfAttachment = null)
        {
            try
            {
                var htmlBody = BuildRentalAgreementHtmlBody(data, personalMessage);
                var emailSubject = !string.IsNullOrWhiteSpace(subject) 
                    ? subject 
                    : $"Rental Agreement & Booking Confirmation - {data.AgreementCode}";

                // 1. Try Gmail SMTP first if configured (Allows sending to ANY recipient email!)
                try
                {
                    if (await TrySendViaSmtpAsync(toEmail, ccEmail, emailSubject, htmlBody, pdfAttachment, $"Rental_Agreement_{data.AgreementCode}.pdf"))
                    {
                        return (true, "Email dispatched successfully via Gmail SMTP.", "smtp-msg-" + Guid.NewGuid().ToString("N")[..8]);
                    }
                }
                catch (Exception smtpEx)
                {
                    Console.WriteLine($"[SMTP ATTEMPT FAILED] {smtpEx.Message}. Falling back to Resend API.");
                }

                // 2. Fallback to Resend API
                string? apiKey = _configuration["RESEND_API_KEY"] ?? Environment.GetEnvironmentVariable("RESEND_API_KEY");

                if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains("YOUR_API_KEY"))
                {
                    Console.WriteLine($"[DEV MODE] Simulated sending rental agreement email to {toEmail} for {data.AgreementCode}");
                    return (true, "Resend API simulated in Development Mode", "dev-simulated-id");
                }

                var toList = new List<string> { toEmail.Trim() };
                var ccList = new List<string>();
                if (!string.IsNullOrWhiteSpace(ccEmail))
                {
                    foreach (var cc in ccEmail.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = cc.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmed)) ccList.Add(trimmed);
                    }
                }

                var payload = new Dictionary<string, object?>
                {
                    { "from", "DriveAndGo <onboarding@resend.dev>" },
                    { "to", toList },
                    { "subject", emailSubject },
                    { "html", htmlBody }
                };

                if (ccList.Count > 0)
                {
                    payload["cc"] = ccList;
                }

                if (pdfAttachment != null && pdfAttachment.Length > 0)
                {
                    payload["attachments"] = new[]
                    {
                        new
                        {
                            filename = $"Rental_Agreement_{data.AgreementCode}.pdf",
                            content = Convert.ToBase64String(pdfAttachment)
                        }
                    };
                }

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
                requestMessage.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(requestMessage);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseContent);
                    var id = doc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                    return (true, "Email dispatched successfully via Resend.", id);
                }
                else
                {
                    Console.WriteLine($"[RESEND ERROR] ({response.StatusCode}): {responseContent}");
                    return (false, $"Resend API error: {responseContent}", null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RESEND EXCEPTION] Failed to send email agreement: {ex.Message}");
                return (false, $"Email delivery error: {ex.Message}", null);
            }
        }

        private string BuildRentalAgreementHtmlBody(RentalAgreementEmailData data, string? personalMessage)
        {
            var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EmailTemplates", "RentalAgreement.html");
            if (!File.Exists(templatePath))
            {
                templatePath = Path.Combine(Directory.GetCurrentDirectory(), "EmailTemplates", "RentalAgreement.html");
            }

            string template;
            if (File.Exists(templatePath))
            {
                template = File.ReadAllText(templatePath);
            }
            else
            {
                template = @"<!DOCTYPE html><html><body style='font-family:sans-serif;padding:20px;'>
                            <h2>Drive&Go Rental Agreement: {{AgreementCode}}</h2>
                            <p>Hello {{CustomerName}}, your booking for {{VehicleName}} ({{PlateNo}}) is confirmed.</p>
                            <p>Total Paid: PHP {{TotalAmount}}</p>
                            </body></html>";
            }

            var personalMessageHtml = !string.IsNullOrWhiteSpace(personalMessage)
                ? $"<div style='margin-top:12px; padding:10px 14px; background:rgba(255,107,0,0.08); border-left:3px solid #FF6B00; border-radius:6px; font-size:13px; color:#334155;'><strong>Note from Drive&Go:</strong><br/>{System.Web.HttpUtility.HtmlEncode(personalMessage)}</div>"
                : "";

            string logoSrc = "https://raw.githubusercontent.com/martquirante/DriveAndGo_Project/main/DriveAndGo_Admin/WebAssets/logo.png";
            string serverBase = NetworkHelper.GetServerBaseUrl(_configuration);
            var verificationUrl = $"{serverBase}/api/Rentals/verify/{data.AgreementCode}";
            var verificationUrlEncoded = System.Web.HttpUtility.UrlEncode(verificationUrl);
            var pdfDownloadUrl = $"{serverBase}/api/Rentals/code/{data.AgreementCode}/pdf";
            var appDeepLink = $"driveandgo://booking/{data.AgreementCode}";

            return template
                .Replace("{{LogoSrc}}", logoSrc)
                .Replace("{{AgreementCode}}", data.AgreementCode)
                .Replace("{{CustomerName}}", data.CustomerName)
                .Replace("{{VehicleName}}", data.VehicleName)
                .Replace("{{PlateNo}}", data.PlateNo)
                .Replace("{{VehicleColor}}", string.IsNullOrWhiteSpace(data.VehicleColor) ? "Standard" : data.VehicleColor)
                .Replace("{{PickupDate}}", data.PickupDate)
                .Replace("{{DropoffDate}}", data.DropoffDate)
                .Replace("{{DurationDays}}", data.DurationDays.ToString())
                .Replace("{{DailyTotal}}", data.DailyTotal.ToString("N2"))
                .Replace("{{InsuranceFee}}", data.InsuranceFee.ToString("N2"))
                .Replace("{{VatAmount}}", data.VatAmount.ToString("N2"))
                .Replace("{{TotalAmount}}", data.TotalAmount.ToString("N2"))
                .Replace("{{PersonalMessageBlock}}", personalMessageHtml)
                .Replace("{{VerificationUrlEncoded}}", verificationUrlEncoded)
                .Replace("{{PdfDownloadUrl}}", pdfDownloadUrl)
                .Replace("{{AppDeepLink}}", appDeepLink)
                .Replace("{{CompanyPhone}}", data.CompanyPhone)
                .Replace("{{CompanyEmail}}", data.CompanyEmail)
                .Replace("{{CompanyAddress}}", data.CompanyAddress);
        }
    }
}
