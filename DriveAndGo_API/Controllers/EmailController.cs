using DriveAndGo_API.Helpers;
using DriveAndGo_API.Models;
using DriveAndGo_API.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Globalization;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmailController : ControllerBase
    {
        private readonly NpgsqlDataSource _ds;
        private readonly IEmailService _emailService;
        private readonly PdfService _pdfService;
        private readonly IConfiguration _configuration;

        public EmailController(
            IConfiguration configuration,
            NpgsqlDataSource ds,
            IEmailService emailService,
            PdfService pdfService)
        {
            _configuration = configuration;
            _ds = ds;
            _emailService = emailService;
            _pdfService = pdfService;
        }

        [HttpPost("rental-agreement")]
        public async Task<IActionResult> SendRentalAgreement([FromBody] SendAgreementRequest request)
        {
            if (request.RentalId <= 0 || string.IsNullOrWhiteSpace(request.RecipientEmail))
            {
                return BadRequest(new { Success = false, Message = "Valid Rental ID and Recipient Email are required." });
            }

            try
            {
                var rentalData = await FetchRentalAgreementDataAsync(request.RentalId);
                if (rentalData == null)
                {
                    return NotFound(new { Success = false, Message = $"Rental booking #{request.RentalId} was not found." });
                }

                rentalData.CustomerEmail = request.RecipientEmail.Trim();
                rentalData.PersonalMessage = request.PersonalMessage;
                rentalData.IsRescheduled = request.IsRescheduled;
                rentalData.OriginalPickupDate = request.OriginalPickupDate;
                rentalData.OriginalDropoffDate = request.OriginalDropoffDate;
                rentalData.PerkFuelWaiver = request.PerkFuelWaiver;
                rentalData.PerkTollCredits = request.PerkTollCredits;
                rentalData.PerkWashWaiver = request.PerkWashWaiver;
                rentalData.IncludePromoGift = request.IncludeCourtesyPromo;
                rentalData.PromoCode = request.CourtesyPromoCode;
                rentalData.PromoDescription = !string.IsNullOrWhiteSpace(request.CourtesyPromoDiscount)
                    ? $"{request.CourtesyPromoDiscount} discount courtesy gift on your next Drive&Go booking."
                    : "Special courtesy discount on your next Drive&Go booking.";

                var serverBase = NetworkHelper.GetServerBaseUrl(_configuration);
                rentalData.AcceptScheduleUrl = $"{serverBase}/api/Rentals/respond/{rentalData.AgreementCode}?action=accept";
                rentalData.RequestRescheduleUrl = $"{serverBase}/api/Rentals/respond/{rentalData.AgreementCode}?action=reschedule";
                rentalData.DeclineBookingUrl = $"{serverBase}/api/Rentals/respond/{rentalData.AgreementCode}?action=decline";


                rentalData.IncludeReceipt = request.IncludeReceipt;
                rentalData.ReceiptNumber = $"TX-RN{request.RentalId:D5}";

                byte[]? pdfBytes = null;
                if (request.AttachPdf)
                {
                    pdfBytes = _pdfService.GenerateRentalAgreementPdf(rentalData);
                }

                byte[]? receiptBytes = null;
                if (request.IncludeReceipt)
                {
                    var receiptData = new TransactionReceiptPdfData
                    {
                        TransactionId = request.RentalId,
                        ReceiptNumber = rentalData.ReceiptNumber,
                        RentalCode = rentalData.AgreementCode,
                        RentalId = request.RentalId,
                        CustomerName = rentalData.CustomerName,
                        CustomerEmail = request.RecipientEmail.Trim(),
                        CustomerPhone = rentalData.CustomerPhone,
                        VehicleName = rentalData.VehicleName,
                        PlateNo = rentalData.PlateNo,
                        VehicleColor = rentalData.VehicleColor,
                        PickupDate = rentalData.PickupDate,
                        DropoffDate = rentalData.DropoffDate,
                        DurationDays = rentalData.DurationDays,
                        DailyRate = rentalData.DailyRate,
                        RentalSubtotal = rentalData.DailyTotal,
                        SecurityDeposit = 0m,
                        DiscountAmount = 0m,
                        TotalAmount = rentalData.TotalAmount,
                        AmountInWords = DriveAndGo_API.Helpers.NumberToWordsHelper.ConvertNumberToWords(rentalData.TotalAmount),
                        PaymentMethod = string.IsNullOrWhiteSpace(rentalData.PaymentMethod) ? "CASH" : rentalData.PaymentMethod.ToUpperInvariant(),
                        Status = string.IsNullOrWhiteSpace(rentalData.PaymentStatus) ? "PAID" : rentalData.PaymentStatus.ToUpperInvariant(),
                        TransactionDate = DateTime.Now.ToString("MMM dd, yyyy hh:mm tt"),
                        AdminName = "Raymart Quirante",
                        VerificationUrl = rentalData.VerificationUrl,
                        CompanyAddress = rentalData.CompanyAddress,
                        CompanyPhone = rentalData.CompanyPhone,
                        CompanyEmail = rentalData.CompanyEmail,
                        PersonalMessage = request.PersonalMessage
                    };

                    receiptBytes = _pdfService.GenerateTransactionReceiptPdf(receiptData);
                }

                var result = await _emailService.SendRentalAgreementAsync(
                    request.RecipientEmail,
                    request.CcEmail,
                    request.Subject,
                    request.PersonalMessage,
                    rentalData,
                    pdfBytes,
                    receiptBytes);

                if (result.Success)
                {
                    return Ok(new
                    {
                        Success = true,
                        Message = $"Rental Agreement email dispatched to {request.RecipientEmail}.",
                        ResendId = result.ResendId,
                        AgreementCode = rentalData.AgreementCode
                    });
                }
                else
                {
                    return StatusCode(500, new
                    {
                        Success = false,
                        Message = result.Message
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = "Email Dispatch Error: " + ex.Message });
            }
        }

        private async Task<RentalAgreementEmailData?> FetchRentalAgreementDataAsync(int rentalId)
        {
            await using var connection = await _ds.OpenConnectionAsync();

            const string sql = @"
                SELECT 
                    r.rental_id,
                    r.start_date,
                    r.end_date,
                    r.destination,
                    r.status,
                    r.total_amount,
                    r.payment_method,
                    r.payment_status,
                    r.created_at,
                    customer.full_name AS customer_name,
                    customer.phone AS customer_phone,
                    customer.email AS customer_email,
                    COALESCE(NULLIF(customer.avatar_base64, ''), NULLIF(customer.id_photo_url, '')) AS customer_avatar,
                    CONCAT(v.brand, ' ', v.model) AS vehicle_name,
                    v.plate_no AS vehicle_plate_no,
                    driver_user.full_name AS driver_name,
                    driver_user.phone AS driver_phone
                FROM rentals r
                JOIN users customer ON r.customer_id = customer.user_id
                JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                LEFT JOIN drivers d ON r.driver_id = d.driver_id
                LEFT JOIN users driver_user ON d.user_id = driver_user.user_id
                WHERE r.rental_id = @id
                LIMIT 1";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@id", rentalId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            var startDate = Convert.ToDateTime(reader["start_date"], CultureInfo.InvariantCulture);
            var endDate = reader["end_date"] == DBNull.Value ? startDate.AddDays(1) : Convert.ToDateTime(reader["end_date"], CultureInfo.InvariantCulture);
            var duration = Math.Max(1, (int)Math.Ceiling((endDate - startDate).TotalDays));

            var totalAmount = reader["total_amount"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["total_amount"], CultureInfo.InvariantCulture);
            var dailyRate = duration > 0 ? (totalAmount > 0 ? totalAmount / duration : 3000m) : 3000m;
            var dailyTotal = dailyRate * duration;
            var insurance = 500m;
            var vat = Math.Round((dailyTotal + insurance) * 0.12m, 2);

            var createdAt = reader["created_at"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["created_at"], CultureInfo.InvariantCulture);
            var agreementCode = $"RN-{createdAt:yyMMdd}-{rentalId:D3}";

            return new RentalAgreementEmailData
            {
                AgreementCode = agreementCode,
                CustomerAvatarUrl = reader["customer_avatar"] == DBNull.Value ? null : reader["customer_avatar"]?.ToString(),
                CustomerName = reader["customer_name"]?.ToString() ?? "Valued Customer",
                CustomerPhone = reader["customer_phone"]?.ToString() ?? "",
                CustomerEmail = reader["customer_email"]?.ToString() ?? "",
                VehicleName = reader["vehicle_name"]?.ToString() ?? "Rental Vehicle",
                PlateNo = reader["vehicle_plate_no"]?.ToString() ?? "—",
                VehicleColor = "Standard",
                PickupDate = startDate.ToString("MMM dd, yyyy (hh:mm tt)", CultureInfo.InvariantCulture),
                DropoffDate = endDate.ToString("MMM dd, yyyy (hh:mm tt)", CultureInfo.InvariantCulture),
                DurationDays = duration,
                DailyRate = dailyRate,
                DailyTotal = dailyTotal,
                InsuranceFee = insurance,
                VatAmount = vat,
                TotalAmount = totalAmount > 0 ? totalAmount : (dailyTotal + insurance + vat),
                Destination = reader["destination"] == DBNull.Value ? "Metro Manila / Region" : reader["destination"]?.ToString() ?? "",
                DriverName = reader["driver_name"] == DBNull.Value ? "" : reader["driver_name"]?.ToString() ?? "",
                DriverPhone = reader["driver_phone"] == DBNull.Value ? "" : reader["driver_phone"]?.ToString() ?? "",
                PaymentStatus = reader["payment_status"] == DBNull.Value ? "Unpaid" : reader["payment_status"]?.ToString() ?? "Paid",
                PaymentMethod = reader["payment_method"] == DBNull.Value ? "Cash" : reader["payment_method"]?.ToString() ?? "Cash",
                CreatedDate = createdAt.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture),
                VerificationUrl = $"{NetworkHelper.GetServerBaseUrl(_configuration)}/api/Rentals/verify/{agreementCode}",
                CompanyAddress = _configuration?["CompanyInfo:Address"] ?? "DriveAndGo Inc., CSJDM | Norzagaray, Bulacan, Philippines",
                CompanyPhone = _configuration?["CompanyInfo:Phone"] ?? "+63 935 966 7178",
                CompanyEmail = _configuration?["CompanyInfo:Email"] ?? "support@driveandgo.com"
            };
        }
    }
}
