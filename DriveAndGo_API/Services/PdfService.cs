using DriveAndGo_API.Models;
using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using System;
using System.IO;
using System.Linq;

namespace DriveAndGo_API.Services
{
    public class PdfService
    {
        private static readonly DeviceRgb BrandOrange = new DeviceRgb(255, 107, 0);
        private static readonly DeviceRgb DarkNavy = new DeviceRgb(11, 25, 44);
        private static readonly DeviceRgb SlateMuted = new DeviceRgb(100, 116, 139);
        private static readonly DeviceRgb LightBg = new DeviceRgb(248, 250, 252);
        private static readonly DeviceRgb BorderSlate = new DeviceRgb(226, 232, 240);
        private static readonly DeviceRgb VerifiedGreen = new DeviceRgb(5, 150, 105);

        public byte[] GenerateRentalAgreementPdf(RentalAgreementEmailData data)
        {
            using var ms = new MemoryStream();
            using (var writer = new PdfWriter(ms))
            using (var pdf = new PdfDocument(writer))
            using (var document = new Document(pdf))
            {
                // Standard International A4 (595.28 x 841.89 points)
                pdf.SetDefaultPageSize(PageSize.A4);
                document.SetMargins(32, 32, 28, 32);

                PdfFont fontBold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                PdfFont fontNormal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

                // ── 1. Header Bar (Brand & Document Title) ──
                var headerTable = new Table(UnitValue.CreatePercentArray(new float[] { 52, 48 }))
                    .UseAllAvailableWidth()
                    .SetMarginBottom(12);

                // Left Header: Logo & Company
                var leftHeader = new Cell()
                    .SetBorder(Border.NO_BORDER)
                    .SetPadding(0);

                string[] candidateLogoPaths = new[]
                {
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.png"),
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "images", "logo.png"),
                    System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Resources", "logo.png"),
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "logo.png"),
                    @"C:\Users\martq\source\repos\DriveAndGo_Project\DriveAndGo_API\Resources\logo.png",
                    @"C:\Users\martq\source\repos\DriveAndGo_Project\DriveAndGo_Admin\WebAssets\logo.png"
                };
                string? foundLogo = candidateLogoPaths.FirstOrDefault(File.Exists);

                if (!string.IsNullOrEmpty(foundLogo))
                {
                    try
                    {
                        var imgData = ImageDataFactory.Create(foundLogo);
                        var logoImg = new iText.Layout.Element.Image(imgData)
                            .ScaleToFit(170f, 50f)
                            .SetHorizontalAlignment(HorizontalAlignment.LEFT)
                            .SetMarginBottom(6f);
                        leftHeader.Add(logoImg);
                    }
                    catch
                    {
                        var title = new Paragraph("Drive&Go")
                            .SetFont(fontBold)
                            .SetFontColor(BrandOrange)
                            .SetFontSize(22);
                        leftHeader.Add(title);
                    }
                }
                else
                {
                    var title = new Paragraph("Drive&Go")
                        .SetFont(fontBold)
                        .SetFontColor(BrandOrange)
                        .SetFontSize(22);
                    leftHeader.Add(title);
                }

                var subTitle = new Paragraph("VEHICLE RENTAL MANAGEMENT SYSTEM")
                    .SetFont(fontBold)
                    .SetFontColor(SlateMuted)
                    .SetFontSize(8f)
                    .SetCharacterSpacing(1.2f);

                var compInfo = new Paragraph($"{data.CompanyAddress}\nHotline: {data.CompanyPhone} | {data.CompanyEmail}")
                    .SetFont(fontNormal)
                    .SetFontColor(SlateMuted)
                    .SetFontSize(8f)
                    .SetMarginTop(3f);

                leftHeader.Add(subTitle);
                leftHeader.Add(compInfo);

                // Right Header: Document Meta
                var rightHeader = new Cell()
                    .SetBorder(Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.RIGHT)
                    .SetPadding(0);

                var docTitle = new Paragraph("VEHICLE RENTAL AGREEMENT")
                    .SetFont(fontBold)
                    .SetFontColor(DarkNavy)
                    .SetFontSize(15);

                var agreementNo = new Paragraph($"AGREEMENT #: {data.AgreementCode}")
                    .SetFont(fontBold)
                    .SetFontColor(BrandOrange)
                    .SetFontSize(11f)
                    .SetMarginTop(4f);

                var datePrep = new Paragraph($"DATE: {data.CreatedDate}")
                    .SetFont(fontNormal)
                    .SetFontColor(SlateMuted)
                    .SetFontSize(9f)
                    .SetMarginTop(3f);

                rightHeader.Add(docTitle);
                rightHeader.Add(agreementNo);
                rightHeader.Add(datePrep);

                headerTable.AddCell(leftHeader);
                headerTable.AddCell(rightHeader);
                document.Add(headerTable);

                // Brand Orange Divider Line
                var divider = new Table(1).UseAllAvailableWidth()
                    .SetBackgroundColor(BrandOrange)
                    .SetHeight(2.5f)
                    .SetMarginBottom(14);
                document.Add(divider);

                // ── 2. Customer & Vehicle Specifications (2-Column Box) ──
                var infoGrid = new Table(UnitValue.CreatePercentArray(new float[] { 50, 50 }))
                    .UseAllAvailableWidth()
                    .SetMarginBottom(14);

                // Customer Box
                var custCell = new Cell()
                    .SetBorder(new SolidBorder(BorderSlate, 1))
                    .SetBackgroundColor(LightBg)
                    .SetPadding(12)
                    .SetMarginRight(4);

                custCell.Add(new Paragraph("CUSTOMER / LESSEE INFORMATION")
                    .SetFont(fontBold)
                    .SetFontColor(BrandOrange)
                    .SetFontSize(9.5f)
                    .SetMarginBottom(6));

                custCell.Add(new Paragraph($"Full Name: {data.CustomerName}").SetFont(fontBold).SetFontSize(10f).SetFontColor(DarkNavy));
                custCell.Add(new Paragraph($"Contact No: {data.CustomerPhone}").SetFont(fontNormal).SetFontSize(9.5f).SetFontColor(DarkNavy).SetMarginTop(3f));
                custCell.Add(new Paragraph($"Email: {data.CustomerEmail}").SetFont(fontNormal).SetFontSize(9.5f).SetFontColor(DarkNavy).SetMarginTop(3f));
                if (!string.IsNullOrWhiteSpace(data.Destination))
                    custCell.Add(new Paragraph($"Destination: {data.Destination}").SetFont(fontNormal).SetFontSize(9.5f).SetFontColor(DarkNavy).SetMarginTop(3f));

                // Vehicle Box
                var vehCell = new Cell()
                    .SetBorder(new SolidBorder(BorderSlate, 1))
                    .SetBackgroundColor(LightBg)
                    .SetPadding(12)
                    .SetMarginLeft(4);

                var vehHeaderTbl = new Table(UnitValue.CreatePercentArray(new float[] { 75, 25 }))
                    .UseAllAvailableWidth()
                    .SetMarginBottom(6);

                var vhLeft = new Cell().SetBorder(Border.NO_BORDER).SetPadding(0);
                vhLeft.Add(new Paragraph("VEHICLE SPECIFICATIONS")
                    .SetFont(fontBold)
                    .SetFontColor(BrandOrange)
                    .SetFontSize(9.5f));
                vehHeaderTbl.AddCell(vhLeft);

                var vhRight = new Cell().SetBorder(Border.NO_BORDER).SetPadding(0).SetTextAlignment(TextAlignment.RIGHT);
                var brandBytes = DriveAndGo_API.Helpers.LogoHelper.GetBrandLogoBytes(data.VehicleName);
                if (brandBytes != null && brandBytes.Length > 0)
                {
                    try
                    {
                        var brandImg = new iText.Layout.Element.Image(ImageDataFactory.Create(brandBytes))
                            .ScaleToFit(42f, 22f)
                            .SetHorizontalAlignment(HorizontalAlignment.RIGHT);
                        vhRight.Add(brandImg);
                    }
                    catch { }
                }
                vehHeaderTbl.AddCell(vhRight);
                vehCell.Add(vehHeaderTbl);

                vehCell.Add(new Paragraph($"Vehicle: {data.VehicleName}").SetFont(fontBold).SetFontSize(10f).SetFontColor(DarkNavy));
                vehCell.Add(new Paragraph($"Plate Number: {data.PlateNo}").SetFont(fontBold).SetFontSize(10f).SetFontColor(BrandOrange).SetMarginTop(3f));
                if (!string.IsNullOrWhiteSpace(data.VehicleColor))
                    vehCell.Add(new Paragraph($"Color: {data.VehicleColor}").SetFont(fontNormal).SetFontSize(9.5f).SetFontColor(DarkNavy).SetMarginTop(3f));
                if (!string.IsNullOrWhiteSpace(data.DriverName))
                    vehCell.Add(new Paragraph($"Assigned Driver: {data.DriverName} ({data.DriverPhone})").SetFont(fontNormal).SetFontSize(9.5f).SetFontColor(DarkNavy).SetMarginTop(3f));

                infoGrid.AddCell(custCell);
                infoGrid.AddCell(vehCell);
                document.Add(infoGrid);

                // ── 3. Rental Period & Schedule Banner ──
                var periodTable = new Table(UnitValue.CreatePercentArray(new float[] { 66, 34 }))
                    .UseAllAvailableWidth()
                    .SetBackgroundColor(LightBg)
                    .SetBorder(new SolidBorder(BorderSlate, 1))
                    .SetMarginBottom(14)
                    .SetPadding(12);

                var periodLeft = new Cell().SetBorder(Border.NO_BORDER).SetPadding(8);
                periodLeft.Add(new Paragraph("RENTAL PERIOD & SCHEDULE")
                    .SetFont(fontBold)
                    .SetFontColor(BrandOrange)
                    .SetFontSize(9.5f)
                    .SetMarginBottom(6));
                periodLeft.Add(new Paragraph($"Pick-up Schedule: {data.PickupDate}").SetFont(fontNormal).SetFontSize(10f).SetFontColor(DarkNavy));
                periodLeft.Add(new Paragraph($"Return Schedule:  {data.DropoffDate}").SetFont(fontNormal).SetFontSize(10f).SetFontColor(DarkNavy).SetMarginTop(3f));
                periodLeft.Add(new Paragraph($"Authorized Duration: {data.DurationDays} Day(s)").SetFont(fontBold).SetFontSize(10f).SetFontColor(BrandOrange).SetMarginTop(4f));

                var periodRight = new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.CENTER).SetPadding(8);
                periodRight.Add(new Paragraph("OFFICIAL CONTRACT").SetFont(fontBold).SetFontSize(8.5f).SetFontColor(SlateMuted));
                periodRight.Add(new Paragraph("VERIFIED").SetFont(fontBold).SetFontSize(13f).SetFontColor(VerifiedGreen).SetMarginTop(3f));
                
                string payMethod = !string.IsNullOrWhiteSpace(data.PaymentMethod) ? data.PaymentMethod : "Cash";
                var payLogoBytes = DriveAndGo_API.Helpers.LogoHelper.GetPaymentLogoBytes(payMethod);
                if (payLogoBytes != null && payLogoBytes.Length > 0)
                {
                    try
                    {
                        var payImg = new iText.Layout.Element.Image(ImageDataFactory.Create(payLogoBytes))
                            .ScaleToFit(32f, 16f)
                            .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                            .SetMarginTop(3f)
                            .SetMarginBottom(2f);
                        periodRight.Add(payImg);
                    }
                    catch { }
                }
                periodRight.Add(new Paragraph($"{payMethod.ToUpper()} • {data.PaymentStatus.ToUpper()}").SetFont(fontBold).SetFontSize(8f).SetFontColor(DarkNavy).SetMarginTop(2f));

                periodTable.AddCell(periodLeft);
                periodTable.AddCell(periodRight);
                document.Add(periodTable);

                // ── 4. Payment & Fees Breakdown Table ──
                var feesTable = new Table(UnitValue.CreatePercentArray(new float[] { 48, 16, 16, 20 }))
                    .UseAllAvailableWidth()
                    .SetMarginBottom(14);

                feesTable.AddHeaderCell(CreateHeaderCell("DESCRIPTION", fontBold));
                feesTable.AddHeaderCell(CreateHeaderCell("QTY / DAYS", fontBold, TextAlignment.CENTER));
                feesTable.AddHeaderCell(CreateHeaderCell("RATE", fontBold, TextAlignment.RIGHT));
                feesTable.AddHeaderCell(CreateHeaderCell("AMOUNT", fontBold, TextAlignment.RIGHT));

                feesTable.AddCell(CreateRowCell($"Daily Rental Fee ({data.VehicleName})", fontNormal));
                feesTable.AddCell(CreateRowCell($"{data.DurationDays} day(s)", fontNormal, TextAlignment.CENTER));
                feesTable.AddCell(CreateRowCell($"PHP {data.DailyRate:N2}", fontNormal, TextAlignment.RIGHT));
                feesTable.AddCell(CreateRowCell($"PHP {data.DailyTotal:N2}", fontNormal, TextAlignment.RIGHT));

                if (data.InsuranceFee > 0)
                {
                    feesTable.AddCell(CreateRowCell("Comprehensive Insurance Coverage", fontNormal));
                    feesTable.AddCell(CreateRowCell("1", fontNormal, TextAlignment.CENTER));
                    feesTable.AddCell(CreateRowCell($"PHP {data.InsuranceFee:N2}", fontNormal, TextAlignment.RIGHT));
                    feesTable.AddCell(CreateRowCell($"PHP {data.InsuranceFee:N2}", fontNormal, TextAlignment.RIGHT));
                }

                if (data.VatAmount > 0)
                {
                    feesTable.AddCell(CreateRowCell("Value Added Tax (VAT 12%)", fontNormal));
                    feesTable.AddCell(CreateRowCell("-", fontNormal, TextAlignment.CENTER));
                    feesTable.AddCell(CreateRowCell("-", fontNormal, TextAlignment.RIGHT));
                    feesTable.AddCell(CreateRowCell($"PHP {data.VatAmount:N2}", fontNormal, TextAlignment.RIGHT));
                }

                // Total Amount Row
                var totalLabelCell = new Cell(1, 3)
                    .Add(new Paragraph("TOTAL AMOUNT PAID").SetFont(fontBold).SetFontSize(10.5f).SetFontColor(DarkNavy))
                    .SetTextAlignment(TextAlignment.RIGHT)
                    .SetBorder(new SolidBorder(DarkNavy, 1.5f))
                    .SetPadding(9);

                var totalValCell = new Cell()
                    .Add(new Paragraph($"PHP {data.TotalAmount:N2}").SetFont(fontBold).SetFontSize(12.5f).SetFontColor(BrandOrange))
                    .SetTextAlignment(TextAlignment.RIGHT)
                    .SetBorder(new SolidBorder(DarkNavy, 1.5f))
                    .SetPadding(9);

                feesTable.AddCell(totalLabelCell);
                feesTable.AddCell(totalValCell);
                document.Add(feesTable);

                // ── 5. Terms & Conditions Summary ──
                var termsTable = new Table(1).UseAllAvailableWidth().SetMarginBottom(16);
                var termsCell = new Cell().SetBorder(new SolidBorder(BorderSlate, 1)).SetPadding(10).SetBackgroundColor(LightBg);
                termsCell.Add(new Paragraph("TERMS & CONDITIONS").SetFont(fontBold).SetFontColor(SlateMuted).SetFontSize(8f).SetMarginBottom(4));
                termsCell.Add(new Paragraph("1. The renter must return the vehicle on the agreed date and time. Late returns incur standard hourly penalties.\n" +
                                            "2. Additional fuel or excess mileage charges apply if returning below the logged handover fuel level.\n" +
                                            "3. Smoking is strictly prohibited inside the vehicle. The renter is solely responsible for traffic and toll violations.\n" +
                                            "4. In case of emergency or mechanical breakdown, contact the Drive&Go 24/7 hotline (+63 935 966 7178) immediately.")
                    .SetFont(fontNormal)
                    .SetFontSize(8f)
                    .SetFontColor(SlateMuted));
                termsTable.AddCell(termsCell);
                document.Add(termsTable);

                // ── 6. Official Dual Signature & E-Verification Blocks ──
                var signTable = new Table(UnitValue.CreatePercentArray(new float[] { 37, 37, 26 }))
                    .UseAllAvailableWidth()
                    .SetMarginTop(8)
                    .SetMarginBottom(10);

                var custSignCell = new Cell()
                    .SetBorder(new SolidBorder(BorderSlate, 1))
                    .SetPadding(8)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginRight(3);

                custSignCell.Add(new Paragraph("RENTED / CONFORME BY").SetFont(fontBold).SetFontSize(7.2f).SetFontColor(SlateMuted).SetMarginBottom(2f));

                bool custSigAdded = false;
                if (!string.IsNullOrWhiteSpace(data.CustomerSignatureBase64))
                {
                    try
                    {
                        string b64 = data.CustomerSignatureBase64;
                        int commaIdx = b64.IndexOf(',');
                        if (commaIdx >= 0) b64 = b64.Substring(commaIdx + 1);
                        byte[] sigBytes = Convert.FromBase64String(b64.Trim());
                        var sigImg = new iText.Layout.Element.Image(ImageDataFactory.Create(sigBytes))
                            .ScaleToFit(90f, 24f)
                            .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                            .SetMarginTop(1f)
                            .SetMarginBottom(1f);
                        custSignCell.Add(sigImg);
                        custSigAdded = true;
                    }
                    catch { }
                }

                if (!custSigAdded)
                {
                    custSignCell.Add(new Paragraph(data.CustomerName)
                        .SetFont(fontBold)
                        .SetFontSize(8.5f)
                        .SetFontColor(DarkNavy)
                        .SetMarginTop(18f)
                        .SetMarginBottom(1f));
                }
                else
                {
                    custSignCell.Add(new Paragraph(data.CustomerName)
                        .SetFont(fontBold)
                        .SetFontSize(8.5f)
                        .SetFontColor(DarkNavy)
                        .SetMarginTop(1f)
                        .SetMarginBottom(1f));
                }

                custSignCell.Add(new Paragraph("Customer / Authorized Driver Signature")
                    .SetFont(fontNormal)
                    .SetFontSize(6.8f)
                    .SetFontColor(SlateMuted)
                    .SetBorderTop(new SolidBorder(DarkNavy, 0.75f))
                    .SetPaddingTop(2f)
                    .SetMarginTop(0));

                var adminSignCell = new Cell()
                    .SetBorder(new SolidBorder(BorderSlate, 1))
                    .SetPadding(8)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginLeft(3)
                    .SetMarginRight(3);

                string dispAdmin = !string.IsNullOrWhiteSpace(data.AdminName) ? data.AdminName : "Raymart Quirante";
                adminSignCell.Add(new Paragraph("APPROVED & DISPATCHED BY").SetFont(fontBold).SetFontSize(7.2f).SetFontColor(SlateMuted).SetMarginBottom(2f));

                bool adminSigAdded = false;
                if (!string.IsNullOrWhiteSpace(data.AdminSignatureBase64))
                {
                    try
                    {
                        string b64 = data.AdminSignatureBase64;
                        int commaIdx = b64.IndexOf(',');
                        if (commaIdx >= 0) b64 = b64.Substring(commaIdx + 1);
                        byte[] sigBytes = Convert.FromBase64String(b64.Trim());
                        var sigImg = new iText.Layout.Element.Image(ImageDataFactory.Create(sigBytes))
                            .ScaleToFit(90f, 24f)
                            .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                            .SetMarginTop(1f)
                            .SetMarginBottom(1f);
                        adminSignCell.Add(sigImg);
                        adminSigAdded = true;
                    }
                    catch { }
                }

                if (!adminSigAdded)
                {
                    adminSignCell.Add(new Paragraph(dispAdmin)
                        .SetFont(fontBold)
                        .SetFontSize(8.5f)
                        .SetFontColor(DarkNavy)
                        .SetMarginTop(18f)
                        .SetMarginBottom(1f));
                }
                else
                {
                    adminSignCell.Add(new Paragraph(dispAdmin)
                        .SetFont(fontBold)
                        .SetFontSize(8.5f)
                        .SetFontColor(DarkNavy)
                        .SetMarginTop(1f)
                        .SetMarginBottom(1f));
                }

                adminSignCell.Add(new Paragraph("Drive&Go Administrator")
                    .SetFont(fontNormal)
                    .SetFontSize(6.8f)
                    .SetFontColor(SlateMuted)
                    .SetBorderTop(new SolidBorder(DarkNavy, 0.75f))
                    .SetPaddingTop(2f)
                    .SetMarginTop(0));

                // Verification QR Code Column
                var qrSignCell = new Cell()
                    .SetBorder(new SolidBorder(BorderSlate, 1))
                    .SetPadding(6)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginLeft(3);

                byte[]? agreeQrBytes = null;
                try
                {
                    using var qrGen = new QRCoder.QRCodeGenerator();
                    string vUrl = !string.IsNullOrWhiteSpace(data.VerificationUrl)
                        ? data.VerificationUrl
                        : $"https://driveandgo.ph/rentals/verify/{data.AgreementCode}";
                    var qrData = qrGen.CreateQrCode(vUrl, QRCoder.QRCodeGenerator.ECCLevel.H);
                    var qrCode = new QRCoder.PngByteQRCode(qrData);
                    agreeQrBytes = qrCode.GetGraphic(4, new byte[] { 11, 25, 44, 255 }, new byte[] { 255, 255, 255, 255 }, true);
                }
                catch { }

                if (agreeQrBytes != null)
                {
                    try
                    {
                        var qrImg = new iText.Layout.Element.Image(ImageDataFactory.Create(agreeQrBytes))
                            .ScaleToFit(44f, 44f)
                            .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                            .SetMarginBottom(2f);
                        qrSignCell.Add(qrImg);
                    }
                    catch { }
                }

                qrSignCell.Add(new Paragraph("E-SIGNATURE SEAL")
                    .SetFont(fontBold)
                    .SetFontSize(6.5f)
                    .SetFontColor(VerifiedGreen)
                    .SetCharacterSpacing(0.4f));
                qrSignCell.Add(new Paragraph(data.AgreementCode)
                    .SetFont(fontBold)
                    .SetFontSize(6.5f)
                    .SetFontColor(DarkNavy));

                signTable.AddCell(custSignCell);
                signTable.AddCell(adminSignCell);
                signTable.AddCell(qrSignCell);
                document.Add(signTable);

                // ── 7. Document Footer Stamp ──
                var footerText = new Paragraph("Drive&Go Vehicle Rental System • Official Digitally Verified Agreement • CSJDM | Norzagaray, Bulacan, Philippines • Hotline: +63 935 966 7178")
                    .SetFont(fontNormal)
                    .SetFontSize(8f)
                    .SetFontColor(SlateMuted)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginTop(8);
                document.Add(footerText);
            }

            return ms.ToArray();
        }

        private static Cell CreateHeaderCell(string text, PdfFont fontBold, TextAlignment align = TextAlignment.LEFT)
        {
            return new Cell()
                .Add(new Paragraph(text).SetFont(fontBold).SetFontSize(9f).SetFontColor(DarkNavy))
                .SetBackgroundColor(LightBg)
                .SetBorder(new SolidBorder(BorderSlate, 1))
                .SetTextAlignment(align)
                .SetPadding(8);
        }

        private static Cell CreateRowCell(string text, PdfFont fontNormal, TextAlignment align = TextAlignment.LEFT)
        {
            return new Cell()
                .Add(new Paragraph(text).SetFont(fontNormal).SetFontSize(9.5f).SetFontColor(DarkNavy))
                .SetBorder(new SolidBorder(BorderSlate, 0.5f))
                .SetTextAlignment(align)
                .SetPadding(7.5f);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  VEHICLE RETURN CERTIFICATE & FINAL SETTLEMENT RECEIPT
        // ─────────────────────────────────────────────────────────────────────
        public byte[] GenerateVehicleReturnCertificatePdf(VehicleReturnEmailData d)
        {
            using var ms = new MemoryStream();
            using (var writer = new PdfWriter(ms))
            using (var pdf    = new PdfDocument(writer))
            using (var doc    = new Document(pdf))
            {
                pdf.SetDefaultPageSize(PageSize.A4);
                doc.SetMargins(30, 32, 28, 32);

                PdfFont fontBold   = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                PdfFont fontNormal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

                // Color palette matching user's approved design
                var OrangeAccent  = new DeviceRgb(255, 107, 0);
                var NavyBg        = new DeviceRgb(11, 25, 44);
                var SlateGray     = new DeviceRgb(100, 116, 139);
                var LightGrayBg   = new DeviceRgb(248, 250, 252);
                var BorderColor   = new DeviceRgb(226, 232, 240);
                var GreenAccent   = new DeviceRgb(5, 150, 105);
                var BlackText     = new DeviceRgb(15, 23, 42);

                // ── Generate QR Code PNG ──
                byte[]? qrBytes = null;
                try
                {
                    using var qrGenerator = new QRCoder.QRCodeGenerator();
                    var qrData   = qrGenerator.CreateQrCode(d.VerificationUrl, QRCoder.QRCodeGenerator.ECCLevel.H);
                    var qrCode   = new QRCoder.PngByteQRCode(qrData);
                    qrBytes      = qrCode.GetGraphic(6);
                }
                catch { /* QR generation failed gracefully */ }

                // ══════════════════════════════════════════════════
                //  TOP HEADER (Logo | Title | Cert# & QR)
                // ══════════════════════════════════════════════════
                var headerTbl = new Table(UnitValue.CreatePercentArray(new float[] { 25, 48, 27 }))
                    .UseAllAvailableWidth()
                    .SetMarginBottom(0);

                // Cell 1 – Logo
                var logoCell = new Cell().SetBorder(Border.NO_BORDER).SetPaddingRight(8).SetVerticalAlignment(VerticalAlignment.MIDDLE);
                string[] logoPaths =
                {
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.png"),
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "images", "logo.png"),
                    System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Resources", "logo.png"),
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "logo.png"),
                    @"C:\Users\martq\source\repos\DriveAndGo_Project\DriveAndGo_API\Resources\logo.png",
                    @"C:\Users\martq\source\repos\DriveAndGo_Project\DriveAndGo_Admin\WebAssets\logo.png"
                };
                string? foundLogo = logoPaths.FirstOrDefault(File.Exists);
                if (!string.IsNullOrEmpty(foundLogo))
                {
                    try
                    {
                        var imgData = ImageDataFactory.Create(foundLogo);
                        logoCell.Add(new iText.Layout.Element.Image(imgData).ScaleToFit(130f, 45f));
                    }
                    catch { logoCell.Add(new Paragraph("Drive&Go").SetFont(fontBold).SetFontColor(OrangeAccent).SetFontSize(18)); }
                }
                else
                {
                    logoCell.Add(new Paragraph("Drive&Go").SetFont(fontBold).SetFontColor(OrangeAccent).SetFontSize(18));
                }
                headerTbl.AddCell(logoCell);

                // Cell 2 – Title
                var titleCell = new Cell()
                    .SetBorder(Border.NO_BORDER)
                    .SetVerticalAlignment(VerticalAlignment.MIDDLE);
                titleCell.Add(new Paragraph("VEHICLE RETURN CERTIFICATE &")
                    .SetFont(fontBold).SetFontSize(15f).SetFontColor(BlackText).SetMarginBottom(1));
                titleCell.Add(new Paragraph("FINAL SETTLEMENT RECEIPT")
                    .SetFont(fontBold).SetFontSize(15f).SetFontColor(BlackText));
                headerTbl.AddCell(titleCell);

                // Cell 3 – Cert# box + QR Code
                var certCell = new Cell()
                    .SetBorder(Border.NO_BORDER)
                    .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                    .SetTextAlignment(TextAlignment.RIGHT);
                certCell.Add(new Paragraph("CERTIFICATE NO:")
                    .SetFont(fontNormal).SetFontSize(8f).SetFontColor(SlateGray).SetTextAlignment(TextAlignment.RIGHT));
                certCell.Add(new Paragraph(d.ReturnCertCode)
                    .SetFont(fontBold).SetFontSize(13f).SetFontColor(OrangeAccent).SetTextAlignment(TextAlignment.RIGHT).SetMarginBottom(4));
                if (qrBytes != null)
                {
                    try
                    {
                        var qrImgData = ImageDataFactory.Create(qrBytes);
                        var qrImg = new iText.Layout.Element.Image(qrImgData).ScaleToFit(58f, 58f);
                        certCell.Add(qrImg);
                        certCell.Add(new Paragraph("SCAN TO VERIFY ONLINE")
                            .SetFont(fontNormal).SetFontSize(6.5f).SetFontColor(SlateGray).SetTextAlignment(TextAlignment.RIGHT));
                    }
                    catch { }
                }
                headerTbl.AddCell(certCell);
                doc.Add(headerTbl);

                // ── Orange top accent bar ──
                var topBar = new Table(1).UseAllAvailableWidth().SetMarginBottom(10);
                topBar.AddCell(new Cell().SetHeight(3f).SetBackgroundColor(OrangeAccent).SetBorder(Border.NO_BORDER));
                doc.Add(topBar);

                // ── Date & Branch meta-bar ──
                doc.Add(new Paragraph($"   Date & Time:  {d.ReturnDate}       |       Branch:  {d.CompanyAddress}")
                    .SetFont(fontNormal).SetFontSize(9f).SetFontColor(SlateGray)
                    .SetBorderBottom(new SolidBorder(BorderColor, 1)).SetPaddingBottom(6).SetMarginBottom(12));

                // ══════════════════════════════════════════════════
                //  SECTION 1 – Customer & Vehicle Registration
                // ══════════════════════════════════════════════════
                doc.Add(BuildSectionHeader("1", "CUSTOMER & VEHICLE REGISTRATION", fontBold, OrangeAccent, BlackText));

                var sec1 = new Table(UnitValue.CreatePercentArray(new float[] { 50, 50 }))
                    .UseAllAvailableWidth()
                    .SetMarginBottom(14)
                    .SetBorder(new SolidBorder(BorderColor, 1))
                    .SetBorderRadius(new iText.Layout.Properties.BorderRadius(6));

                var custCell = new Cell().SetBorder(Border.NO_BORDER).SetPadding(12);
                custCell.Add(new Paragraph("Customer Name").SetFont(fontNormal).SetFontSize(8f).SetFontColor(SlateGray));
                custCell.Add(new Paragraph(d.CustomerName.ToUpperInvariant()).SetFont(fontBold).SetFontSize(13f).SetFontColor(BlackText).SetMarginBottom(8));
                custCell.Add(new Paragraph($"Contact  {d.CustomerPhone}").SetFont(fontNormal).SetFontSize(8.5f).SetFontColor(BlackText).SetMarginBottom(2));
                custCell.Add(new Paragraph($"Email    {d.CustomerEmail}").SetFont(fontNormal).SetFontSize(8.5f).SetFontColor(BlackText));
                sec1.AddCell(custCell);

                var vehCell = new Cell()
                    .SetBorder(Border.NO_BORDER)
                    .SetBorderLeft(new SolidBorder(BorderColor, 1))
                    .SetPadding(12);

                var vehHeadTbl = new Table(UnitValue.CreatePercentArray(new float[] { 75, 25 })).UseAllAvailableWidth();
                var vhLeft = new Cell().SetBorder(Border.NO_BORDER);
                vhLeft.Add(new Paragraph("Vehicle").SetFont(fontNormal).SetFontSize(8f).SetFontColor(SlateGray));
                vehHeadTbl.AddCell(vhLeft);

                var vhRight = new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT);
                var retBrandBytes = DriveAndGo_API.Helpers.LogoHelper.GetBrandLogoBytes(d.VehicleName);
                if (retBrandBytes != null && retBrandBytes.Length > 0)
                {
                    try
                    {
                        var brandImg = new iText.Layout.Element.Image(ImageDataFactory.Create(retBrandBytes))
                            .ScaleToFit(40f, 22f)
                            .SetHorizontalAlignment(HorizontalAlignment.RIGHT);
                        vhRight.Add(brandImg);
                    }
                    catch { }
                }
                vehHeadTbl.AddCell(vhRight);
                vehCell.Add(vehHeadTbl);

                vehCell.Add(new Paragraph(d.VehicleName.ToUpperInvariant()).SetFont(fontBold).SetFontSize(13f).SetFontColor(BlackText).SetMarginBottom(8));
                vehCell.Add(new Paragraph($"Plate No.   {d.PlateNo}").SetFont(fontNormal).SetFontSize(8.5f).SetFontColor(BlackText).SetMarginBottom(2));
                vehCell.Add(new Paragraph($"Rental Period:  {d.PickupDate}  -  {d.ReturnDate}  ({d.DurationDays} Days)").SetFont(fontNormal).SetFontSize(8.5f).SetFontColor(BlackText));
                sec1.AddCell(vehCell);

                doc.Add(sec1);

                // ══════════════════════════════════════════════════
                //  SECTION 2 – Handover vs Return Inspection Matrix
                // ══════════════════════════════════════════════════
                doc.Add(BuildSectionHeader("2", "HANDOVER VS RETURN INSPECTION MATRIX", fontBold, OrangeAccent, BlackText));

                var matrixTbl = new Table(UnitValue.CreatePercentArray(new float[] { 28, 22, 22, 28 }))
                    .UseAllAvailableWidth()
                    .SetMarginBottom(14);

                matrixTbl.AddCell(CreateHeaderCell("Inspection Item",  fontBold));
                matrixTbl.AddCell(CreateHeaderCell("Pickup Handover",  fontBold, TextAlignment.CENTER));
                matrixTbl.AddCell(CreateHeaderCell("Final Return",     fontBold, TextAlignment.CENTER));
                matrixTbl.AddCell(CreateHeaderCell("Difference / Status", fontBold, TextAlignment.CENTER));

                string odomDiff     = (d.ReturnOdometer.HasValue && d.StartOdometer.HasValue) ? $"+{d.ReturnOdometer.Value - d.StartOdometer.Value:N0} km Traveled" : "N/A";
                string retOdoStr    = d.ReturnOdometer.HasValue ? $"{d.ReturnOdometer.Value:N0} km" : "N/A";
                string startOdoStr  = d.StartOdometer.HasValue  ? $"{d.StartOdometer.Value:N0} km"  : "N/A";
                string damageStatus = d.HasDamage ? "DAMAGE NOTED" : "NO DAMAGE PASSED";
                string accessStatus = d.HasDamage ? "VERIFY WITH ADMIN" : "ALL ITEMS RETURNED";

                AddMatrixRow(matrixTbl, "Odometer Mileage", startOdoStr, retOdoStr, odomDiff,     fontNormal, GreenAccent, false);
                AddMatrixRow(matrixTbl, "Fuel Tank Level",  "100% (Full)", $"{d.ReturnFuel} (Full)", "0% Fuel Differential", fontNormal, GreenAccent, false);
                AddMatrixRow(matrixTbl, "Exterior Body",    "Clean / Good", "Clean / Passed", damageStatus, fontNormal, d.HasDamage ? new DeviceRgb(220,38,38) : GreenAccent, false);
                AddMatrixRow(matrixTbl, "Interior & Accessories", "Key & RFID Present", "Key & RFID Returned", accessStatus, fontNormal, d.HasDamage ? new DeviceRgb(220,38,38) : GreenAccent, true);

                doc.Add(matrixTbl);

                // ══════════════════════════════════════════════════
                //  SECTION 3 – Final Billing & Settlement Ledger
                // ══════════════════════════════════════════════════
                doc.Add(BuildSectionHeader("3", "FINAL BILLING & SETTLEMENT LEDGER", fontBold, OrangeAccent, BlackText));

                var billWrap = new Table(UnitValue.CreatePercentArray(new float[] { 58, 42 }))
                    .UseAllAvailableWidth()
                    .SetMarginBottom(14);

                // Left: line items
                var billLeft = new Cell().SetBorder(Border.NO_BORDER).SetPaddingRight(14);
                AddBillRow(billLeft, "Base Rental Charge",           $"₱{d.BaseAmount:N2}",   fontNormal, fontBold, BlackText, false);
                AddBillRow(billLeft, "Extra Mileage / Fuel Fee",     $"₱{d.PenaltyFee:N2}",   fontNormal, fontBold, BlackText, false);
                AddBillRow(billLeft, "Late Penalty Surcharges",      $"₱{d.PenaltyFee:N2}",   fontNormal, fontBold, BlackText, false);
                AddBillRow(billLeft, "Damage Assessment",            $"₱{d.DamageFee:N2}",    fontNormal, fontBold, BlackText, false);

                var totalLine = new Table(UnitValue.CreatePercentArray(new float[] { 60, 40 })).UseAllAvailableWidth()
                    .SetBorderTop(new SolidBorder(OrangeAccent, 1.5f)).SetMarginTop(4);
                totalLine.AddCell(new Cell().SetBorder(Border.NO_BORDER).Add(
                    new Paragraph("TOTAL PAID").SetFont(fontBold).SetFontSize(11f).SetFontColor(BlackText)));
                totalLine.AddCell(new Cell().SetBorder(Border.NO_BORDER).Add(
                    new Paragraph($"₱{d.TotalSettled:N2}").SetFont(fontBold).SetFontSize(11f).SetFontColor(OrangeAccent).SetTextAlignment(TextAlignment.RIGHT)));
                billLeft.Add(totalLine);
                billWrap.AddCell(billLeft);

                // Right: PAID IN FULL badge
                var paidBadge = new Cell()
                    .SetBorder(new SolidBorder(GreenAccent, 2f))
                    .SetBorderRadius(new iText.Layout.Properties.BorderRadius(8))
                    .SetPadding(14)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetVerticalAlignment(VerticalAlignment.MIDDLE);
                paidBadge.Add(new Paragraph("PAID IN FULL")
                    .SetFont(fontBold).SetFontSize(16f).SetFontColor(GreenAccent).SetTextAlignment(TextAlignment.CENTER));
                paidBadge.Add(new Paragraph("ZERO BALANCE CLEARED")
                    .SetFont(fontBold).SetFontSize(9f).SetFontColor(GreenAccent).SetTextAlignment(TextAlignment.CENTER).SetCharacterSpacing(0.5f));
                paidBadge.Add(new Paragraph($"(₱0.00 DUE)")
                    .SetFont(fontBold).SetFontSize(11f).SetFontColor(GreenAccent).SetTextAlignment(TextAlignment.CENTER));
                billWrap.AddCell(paidBadge);

                doc.Add(billWrap);

                // ══════════════════════════════════════════════════
                //  SECTION 4 – Authorization & Security Seal
                // ══════════════════════════════════════════════════
                doc.Add(BuildSectionHeader("4", "OFFICIAL AUTHORIZATION & SECURITY SEAL", fontBold, OrangeAccent, BlackText));

                var sigTbl = new Table(UnitValue.CreatePercentArray(new float[] { 35, 35, 30 }))
                    .UseAllAvailableWidth()
                    .SetMarginBottom(10);

                // Customer sig
                var custSig = new Cell().SetBorder(Border.NO_BORDER).SetPaddingRight(12);
                custSig.Add(new Paragraph("________________________")
                    .SetFont(fontNormal).SetFontSize(10f).SetFontColor(SlateGray));
                custSig.Add(new Paragraph(d.CustomerName)
                    .SetFont(fontBold).SetFontSize(9f).SetFontColor(BlackText).SetMarginTop(3));
                custSig.Add(new Paragraph("(Signed Digitally)")
                    .SetFont(fontNormal).SetFontSize(8f).SetFontColor(SlateGray));
                custSig.Add(new Paragraph("CUSTOMER SIGNATURE")
                    .SetFont(fontBold).SetFontSize(7.5f).SetFontColor(OrangeAccent).SetCharacterSpacing(0.5f));
                sigTbl.AddCell(custSig);

                // Admin inspector sig
                var adminSig = new Cell().SetBorder(Border.NO_BORDER).SetPaddingRight(12);
                adminSig.Add(new Paragraph("________________________")
                    .SetFont(fontNormal).SetFontSize(10f).SetFontColor(SlateGray));
                adminSig.Add(new Paragraph(d.AdminName)
                    .SetFont(fontBold).SetFontSize(9f).SetFontColor(BlackText).SetMarginTop(3));
                adminSig.Add(new Paragraph("(Admin)")
                    .SetFont(fontNormal).SetFontSize(8f).SetFontColor(SlateGray));
                adminSig.Add(new Paragraph("INSPECTING ADMIN OFFICER")
                    .SetFont(fontBold).SetFontSize(7.5f).SetFontColor(OrangeAccent).SetCharacterSpacing(0.5f));
                sigTbl.AddCell(adminSig);

                // Official seal + clearance statement
                var sealCell = new Cell().SetBorder(Border.NO_BORDER);
                sealCell.Add(new Paragraph("DRIVE & GO")
                    .SetFont(fontBold).SetFontSize(9f).SetFontColor(NavyBg).SetTextAlignment(TextAlignment.CENTER));
                sealCell.Add(new Paragraph("VEHICLE INSPECTED & SETTLED")
                    .SetFont(fontBold).SetFontSize(8f).SetFontColor(NavyBg).SetTextAlignment(TextAlignment.CENTER));
                sealCell.Add(new Paragraph("OFFICIAL VERIFICATION SEAL")
                    .SetFont(fontNormal).SetFontSize(7f).SetFontColor(SlateGray).SetTextAlignment(TextAlignment.CENTER));
                sealCell.Add(new Paragraph($"This certifies that the vehicle was officially received in good order and all obligations under booking {d.AgreementCode} are fully settled.")
                    .SetFont(fontNormal).SetFontSize(7.5f).SetFontColor(SlateGray).SetTextAlignment(TextAlignment.CENTER).SetMarginTop(4));
                sigTbl.AddCell(sealCell);

                doc.Add(sigTbl);

                // Terms note
                doc.Add(new Paragraph(
                    "Terms & Notes:\n" +
                    "  \u2022  This certificate serves as the official proof of vehicle return and settlement.\n" +
                    "  \u2022  No further balance is due from the customer under this booking.\n" +
                    "  \u2022  Please keep this document for your records.")
                    .SetFont(fontNormal).SetFontSize(8f).SetFontColor(SlateGray)
                    .SetBorder(new SolidBorder(BorderColor, 0.8f))
                    .SetPadding(8).SetMarginBottom(10));

                // Footer
                var footer = new Table(1).UseAllAvailableWidth().SetMarginTop(6);
                footer.AddCell(new Cell()
                    .SetBackgroundColor(NavyBg)
                    .SetBorder(Border.NO_BORDER)
                    .SetPadding(10)
                    .Add(new Paragraph($"Drive&Go Corporate Office   |   {d.CompanyAddress}   |   {d.CompanyPhone}   |   {d.CompanyEmail}   |   Booking Ref No: {d.AgreementCode}   |   Page 1 of 1")
                        .SetFont(fontNormal).SetFontSize(7.5f).SetFontColor(new DeviceRgb(148,163,184))
                        .SetTextAlignment(TextAlignment.CENTER)));
                doc.Add(footer);
            }

            return ms.ToArray();
        }

        private static Paragraph BuildSectionHeader(string num, string title, PdfFont fontBold, DeviceRgb orange, DeviceRgb dark)
        {
            return new Paragraph($"{num}.  {title}")
                .SetFont(fontBold).SetFontSize(10f)
                .SetFontColor(orange)
                .SetBorderBottom(new SolidBorder(new DeviceRgb(226, 232, 240), 1))
                .SetPaddingBottom(5).SetMarginBottom(8);
        }

        private static void AddMatrixRow(Table tbl, string item, string pickup, string ret, string status, PdfFont fontNormal, DeviceRgb statusColor, bool isLast)
        {
            var border = isLast ? (Border)Border.NO_BORDER : new SolidBorder(new DeviceRgb(226, 232, 240), 0.5f);
            var bodyBorder = new SolidBorder(new DeviceRgb(226, 232, 240), 0.5f);

            tbl.AddCell(new Cell().SetBorder(bodyBorder).SetPadding(8)
                .Add(new Paragraph(item).SetFont(fontNormal).SetFontSize(9f).SetFontColor(new DeviceRgb(15,23,42))));
            tbl.AddCell(new Cell().SetBorder(bodyBorder).SetPadding(8).SetTextAlignment(TextAlignment.CENTER)
                .Add(new Paragraph(pickup).SetFont(fontNormal).SetFontSize(9f).SetFontColor(new DeviceRgb(15,23,42))));
            tbl.AddCell(new Cell().SetBorder(bodyBorder).SetPadding(8).SetTextAlignment(TextAlignment.CENTER)
                .Add(new Paragraph(ret).SetFont(fontNormal).SetFontSize(9f).SetFontColor(new DeviceRgb(15,23,42))));
            tbl.AddCell(new Cell().SetBorder(bodyBorder).SetPadding(8).SetTextAlignment(TextAlignment.CENTER)
                .Add(new Paragraph(status).SetFont(fontNormal).SetFontSize(9f).SetFontColor(statusColor)));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  OFFICIAL TRANSACTION & PAYMENT RECEIPT (A4 CORPORATE PDF)
        // ─────────────────────────────────────────────────────────────────────
        public byte[] GenerateTransactionReceiptPdf(TransactionReceiptPdfData d)
        {
            using var ms = new MemoryStream();
            using (var writer = new PdfWriter(ms))
            using (var pdf = new PdfDocument(writer))
            using (var doc = new Document(pdf))
            {
                pdf.SetDefaultPageSize(PageSize.A4);
                doc.SetMargins(28, 30, 24, 30);

                PdfFont fontBold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                PdfFont fontNormal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

                DeviceRgb brandOrange = new DeviceRgb(255, 107, 0);
                DeviceRgb darkNavy = new DeviceRgb(9, 13, 22);
                DeviceRgb cardNavy = new DeviceRgb(15, 23, 42);
                DeviceRgb slateMuted = new DeviceRgb(100, 116, 139);
                DeviceRgb borderSlate = new DeviceRgb(226, 232, 240);
                DeviceRgb lightBg = new DeviceRgb(248, 250, 252);
                DeviceRgb successGreen = new DeviceRgb(16, 185, 129);
                DeviceRgb pendingAmber = new DeviceRgb(245, 158, 11);

                bool isPaid = d.Status.Equals("confirmed", StringComparison.OrdinalIgnoreCase) ||
                             d.Status.Equals("paid", StringComparison.OrdinalIgnoreCase) ||
                             d.Status.Equals("verified", StringComparison.OrdinalIgnoreCase);

                DeviceRgb statusColor = isPaid ? successGreen : pendingAmber;

                // ── 1. Top Brand Header Bar ──
                var headerTable = new Table(UnitValue.CreatePercentArray(new float[] { 55, 45 }))
                    .UseAllAvailableWidth()
                    .SetMarginBottom(10);

                var leftHeader = new Cell().SetBorder(Border.NO_BORDER).SetPadding(0);

                string[] candidateLogoPaths = new[]
                {
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.png"),
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "images", "logo.png"),
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "logo.png"),
                    System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Resources", "logo.png"),
                    @"C:\Users\martq\source\repos\DriveAndGo_Project\DriveAndGo_API\Resources\logo.png",
                    @"C:\Users\martq\source\repos\DriveAndGo_Project\DriveAndGo_Admin\WebAssets\logo.png"
                };
                string? foundLogo = candidateLogoPaths.FirstOrDefault(File.Exists);

                if (!string.IsNullOrEmpty(foundLogo))
                {
                    try
                    {
                        var imgData = ImageDataFactory.Create(foundLogo);
                        var logoImg = new iText.Layout.Element.Image(imgData)
                            .ScaleToFit(160f, 44f)
                            .SetHorizontalAlignment(HorizontalAlignment.LEFT)
                            .SetMarginBottom(4f);
                        leftHeader.Add(logoImg);
                    }
                    catch
                    {
                        leftHeader.Add(new Paragraph("DRIVE & GO").SetFont(fontBold).SetFontColor(brandOrange).SetFontSize(20));
                    }
                }
                else
                {
                    leftHeader.Add(new Paragraph("DRIVE & GO").SetFont(fontBold).SetFontColor(brandOrange).SetFontSize(20));
                }

                leftHeader.Add(new Paragraph("VEHICLE RENTAL SYSTEM").SetFont(fontBold).SetFontColor(brandOrange).SetFontSize(8.5f).SetCharacterSpacing(1.2f));
                leftHeader.Add(new Paragraph($"{d.CompanyAddress}\nHotline: {d.CompanyPhone}   |   {d.CompanyEmail}")
                    .SetFont(fontNormal).SetFontColor(slateMuted).SetFontSize(8f).SetMarginTop(2f));

                var rightHeader = new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT).SetPadding(0);
                rightHeader.Add(new Paragraph("OFFICIAL PAYMENT RECEIPT").SetFont(fontBold).SetFontColor(darkNavy).SetFontSize(14));
                rightHeader.Add(new Paragraph($"RECEIPT #: {d.ReceiptNumber}").SetFont(fontBold).SetFontColor(brandOrange).SetFontSize(11f).SetMarginTop(3f));
                rightHeader.Add(new Paragraph($"ISSUED: {d.TransactionDate}").SetFont(fontNormal).SetFontColor(slateMuted).SetFontSize(8.5f).SetMarginTop(2f));

                headerTable.AddCell(leftHeader);
                headerTable.AddCell(rightHeader);
                doc.Add(headerTable);

                // Orange Accent Line
                var divider = new Table(1).UseAllAvailableWidth().SetBackgroundColor(brandOrange).SetHeight(2.5f).SetMarginBottom(12);
                doc.Add(divider);

                // ── 2. Customer & Vehicle 2-Column Info Grid ──
                var infoGrid = new Table(UnitValue.CreatePercentArray(new float[] { 50, 50 })).UseAllAvailableWidth().SetMarginBottom(12);

                var custCell = new Cell().SetBorder(new SolidBorder(borderSlate, 1)).SetBackgroundColor(lightBg).SetPadding(10).SetMarginRight(4);
                custCell.Add(new Paragraph("CUSTOMER / BILLED TO").SetFont(fontBold).SetFontColor(brandOrange).SetFontSize(9f).SetMarginBottom(4));
                custCell.Add(new Paragraph($"Full Name: {d.CustomerName}").SetFont(fontBold).SetFontSize(9.5f).SetFontColor(darkNavy));
                custCell.Add(new Paragraph($"Contact: {d.CustomerPhone}").SetFont(fontNormal).SetFontSize(8.5f).SetFontColor(darkNavy).SetMarginTop(2f));
                custCell.Add(new Paragraph($"Email: {d.CustomerEmail}").SetFont(fontNormal).SetFontSize(8.5f).SetFontColor(darkNavy).SetMarginTop(2f));

                var vehCell = new Cell().SetBorder(new SolidBorder(borderSlate, 1)).SetBackgroundColor(lightBg).SetPadding(10).SetMarginLeft(4);
                var vehTbl = new Table(UnitValue.CreatePercentArray(new float[] { 75, 25 })).UseAllAvailableWidth().SetMarginBottom(4);
                var vtLeft = new Cell().SetBorder(Border.NO_BORDER);
                vtLeft.Add(new Paragraph("BOOKING & VEHICLE DETAILS").SetFont(fontBold).SetFontColor(brandOrange).SetFontSize(9f));
                vehTbl.AddCell(vtLeft);

                var vtRight = new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT);
                var rcptBrandBytes = DriveAndGo_API.Helpers.LogoHelper.GetBrandLogoBytes(d.VehicleName);
                if (rcptBrandBytes != null && rcptBrandBytes.Length > 0)
                {
                    try
                    {
                        var brandImg = new iText.Layout.Element.Image(ImageDataFactory.Create(rcptBrandBytes))
                            .ScaleToFit(38f, 20f)
                            .SetHorizontalAlignment(HorizontalAlignment.RIGHT);
                        vtRight.Add(brandImg);
                    }
                    catch { }
                }
                vehTbl.AddCell(vtRight);
                vehCell.Add(vehTbl);

                vehCell.Add(new Paragraph($"Rental Reference: {d.RentalCode}").SetFont(fontBold).SetFontSize(9.5f).SetFontColor(brandOrange));
                vehCell.Add(new Paragraph($"Vehicle Unit: {d.VehicleName} ({d.PlateNo})").SetFont(fontNormal).SetFontSize(8.5f).SetFontColor(darkNavy).SetMarginTop(2f));
                if (!string.IsNullOrWhiteSpace(d.PickupDate))
                    vehCell.Add(new Paragraph($"Schedule: {d.PickupDate} - {d.DropoffDate} ({d.DurationDays} Days)").SetFont(fontNormal).SetFontSize(8.5f).SetFontColor(darkNavy).SetMarginTop(2f));

                infoGrid.AddCell(custCell);
                infoGrid.AddCell(vehCell);
                doc.Add(infoGrid);

                // ── 3. Payment Status & Method Banner ──
                var statusBanner = new Table(UnitValue.CreatePercentArray(new float[] { 60, 40 }))
                    .UseAllAvailableWidth()
                    .SetBackgroundColor(lightBg)
                    .SetBorder(new SolidBorder(borderSlate, 1))
                    .SetPadding(10)
                    .SetMarginBottom(12);

                var sbLeft = new Cell().SetBorder(Border.NO_BORDER).SetPadding(6);
                sbLeft.Add(new Paragraph("PAYMENT METHOD & TRANSACTION CHANNEL").SetFont(fontBold).SetFontSize(8.5f).SetFontColor(slateMuted));

                var methodRowTbl = new Table(UnitValue.CreatePercentArray(new float[] { 22, 78 })).UseAllAvailableWidth().SetMarginTop(3f);
                var payLogBytes = DriveAndGo_API.Helpers.LogoHelper.GetPaymentLogoBytes(d.PaymentMethod);
                if (payLogBytes != null && payLogBytes.Length > 0)
                {
                    try
                    {
                        var payLogoImg = new iText.Layout.Element.Image(ImageDataFactory.Create(payLogBytes))
                            .ScaleToFit(30f, 18f)
                            .SetHorizontalAlignment(HorizontalAlignment.LEFT);
                        methodRowTbl.AddCell(new Cell().SetBorder(Border.NO_BORDER).Add(payLogoImg));
                    }
                    catch
                    {
                        methodRowTbl.AddCell(new Cell().SetBorder(Border.NO_BORDER));
                    }
                }
                else
                {
                    methodRowTbl.AddCell(new Cell().SetBorder(Border.NO_BORDER));
                }

                var methodTextCell = new Cell().SetBorder(Border.NO_BORDER).SetVerticalAlignment(VerticalAlignment.MIDDLE);
                methodTextCell.Add(new Paragraph($"Channel: {d.PaymentMethod.ToUpper()}").SetFont(fontBold).SetFontSize(11f).SetFontColor(darkNavy));
                methodRowTbl.AddCell(methodTextCell);
                sbLeft.Add(methodRowTbl);

                var sbRight = new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT).SetPadding(6);
                sbRight.Add(new Paragraph("SETTLEMENT STATUS").SetFont(fontBold).SetFontSize(8f).SetFontColor(slateMuted));
                sbRight.Add(new Paragraph(d.Status.ToUpper()).SetFont(fontBold).SetFontSize(12f).SetFontColor(statusColor).SetMarginTop(2f));

                statusBanner.AddCell(sbLeft);
                statusBanner.AddCell(sbRight);
                doc.Add(statusBanner);

                // ── 4. Itemized Financial Schedule ──
                var feesTable = new Table(UnitValue.CreatePercentArray(new float[] { 50, 15, 15, 20 }))
                    .UseAllAvailableWidth()
                    .SetMarginBottom(12);

                feesTable.AddHeaderCell(CreateHeaderCell("ITEM / CHARGE DESCRIPTION", fontBold));
                feesTable.AddHeaderCell(CreateHeaderCell("QTY / DAYS", fontBold, TextAlignment.CENTER));
                feesTable.AddHeaderCell(CreateHeaderCell("RATE", fontBold, TextAlignment.RIGHT));
                feesTable.AddHeaderCell(CreateHeaderCell("AMOUNT", fontBold, TextAlignment.RIGHT));

                feesTable.AddCell(CreateRowCell($"Vehicle Rental Rate ({d.VehicleName})", fontNormal));
                feesTable.AddCell(CreateRowCell($"{d.DurationDays} day(s)", fontNormal, TextAlignment.CENTER));
                feesTable.AddCell(CreateRowCell($"PHP {d.DailyRate:N2}", fontNormal, TextAlignment.RIGHT));
                feesTable.AddCell(CreateRowCell($"PHP {d.RentalSubtotal:N2}", fontNormal, TextAlignment.RIGHT));

                if (d.SecurityDeposit > 0)
                {
                    feesTable.AddCell(CreateRowCell("Security Deposit (Refundable upon inspection)", fontNormal));
                    feesTable.AddCell(CreateRowCell("1", fontNormal, TextAlignment.CENTER));
                    feesTable.AddCell(CreateRowCell($"PHP {d.SecurityDeposit:N2}", fontNormal, TextAlignment.RIGHT));
                    feesTable.AddCell(CreateRowCell($"PHP {d.SecurityDeposit:N2}", fontNormal, TextAlignment.RIGHT));
                }

                if (d.DiscountAmount > 0)
                {
                    feesTable.AddCell(CreateRowCell("Promotional Discount / Courtesy Waiver", fontNormal));
                    feesTable.AddCell(CreateRowCell("1", fontNormal, TextAlignment.CENTER));
                    feesTable.AddCell(CreateRowCell($"-PHP {d.DiscountAmount:N2}", fontNormal, TextAlignment.RIGHT));
                    feesTable.AddCell(CreateRowCell($"-PHP {d.DiscountAmount:N2}", fontNormal, TextAlignment.RIGHT));
                }

                // Total Row
                var totalLabel = new Cell(1, 3)
                    .Add(new Paragraph("TOTAL AMOUNT PAID").SetFont(fontBold).SetFontSize(10f).SetFontColor(darkNavy))
                    .SetTextAlignment(TextAlignment.RIGHT)
                    .SetBorder(new SolidBorder(darkNavy, 1.2f))
                    .SetPadding(8);

                var totalVal = new Cell()
                    .Add(new Paragraph($"PHP {d.TotalAmount:N2}").SetFont(fontBold).SetFontSize(12f).SetFontColor(brandOrange))
                    .SetTextAlignment(TextAlignment.RIGHT)
                    .SetBorder(new SolidBorder(darkNavy, 1.2f))
                    .SetPadding(8);

                feesTable.AddCell(totalLabel);
                feesTable.AddCell(totalVal);
                doc.Add(feesTable);

                // Amount in Words Box
                if (!string.IsNullOrWhiteSpace(d.AmountInWords))
                {
                    var wordsBox = new Table(1).UseAllAvailableWidth().SetMarginBottom(12);
                    var wordsCell = new Cell().SetBackgroundColor(new DeviceRgb(255, 247, 237)).SetBorder(new SolidBorder(new DeviceRgb(254, 215, 170), 1)).SetPadding(8);
                    wordsCell.Add(new Paragraph($"AMOUNT IN WORDS: {d.AmountInWords.ToUpper()}")
                        .SetFont(fontBold).SetFontSize(8.5f).SetFontColor(new DeviceRgb(194, 65, 12)));
                    wordsBox.AddCell(wordsCell);
                    doc.Add(wordsBox);
                }

                // ── 5. Dual Verification & Signature Block ──
                var signGrid = new Table(UnitValue.CreatePercentArray(new float[] { 50, 50 })).UseAllAvailableWidth().SetMarginBottom(10);

                var qrSignCell = new Cell().SetBorder(new SolidBorder(borderSlate, 1)).SetPadding(8).SetMarginRight(4);
                var qrLayoutTable = new Table(UnitValue.CreatePercentArray(new float[] { 32, 68 })).UseAllAvailableWidth();

                byte[]? txQrBytes = null;
                try
                {
                    using var qrGen = new QRCoder.QRCodeGenerator();
                    string verifyUrl = !string.IsNullOrWhiteSpace(d.VerificationUrl) 
                        ? d.VerificationUrl 
                        : $"https://driveandgo.ph/transactions/verify/TX-{d.TransactionId:D6}";
                    var qrData = qrGen.CreateQrCode(verifyUrl, QRCoder.QRCodeGenerator.ECCLevel.H);
                    var qrCode = new QRCoder.PngByteQRCode(qrData);
                    txQrBytes = qrCode.GetGraphic(4, new byte[] { 15, 23, 42, 255 }, new byte[] { 255, 255, 255, 255 }, true);
                }
                catch { }

                if (txQrBytes != null)
                {
                    try
                    {
                        var qrImg = new iText.Layout.Element.Image(ImageDataFactory.Create(txQrBytes))
                            .ScaleToFit(46f, 46f)
                            .SetHorizontalAlignment(HorizontalAlignment.LEFT);
                        qrLayoutTable.AddCell(new Cell().SetBorder(Border.NO_BORDER).Add(qrImg));
                    }
                    catch
                    {
                        qrLayoutTable.AddCell(new Cell().SetBorder(Border.NO_BORDER));
                    }
                }
                else
                {
                    qrLayoutTable.AddCell(new Cell().SetBorder(Border.NO_BORDER));
                }

                var qrDescCell = new Cell().SetBorder(Border.NO_BORDER).SetPaddingLeft(4);
                qrDescCell.Add(new Paragraph("ELECTRONIC VERIFICATION SEAL").SetFont(fontBold).SetFontSize(7.5f).SetFontColor(brandOrange).SetMarginBottom(2f));
                qrDescCell.Add(new Paragraph($"Ref: TX-{d.TransactionId:D6}").SetFont(fontBold).SetFontSize(8f).SetFontColor(darkNavy));
                qrDescCell.Add(new Paragraph("Scan QR to confirm official receipt validity on Drive&Go portal.")
                    .SetFont(fontNormal).SetFontSize(7f).SetFontColor(slateMuted).SetMarginTop(2f));
                qrLayoutTable.AddCell(qrDescCell);
                qrSignCell.Add(qrLayoutTable);

                var cashierSignCell = new Cell().SetBorder(new SolidBorder(borderSlate, 1)).SetPadding(8).SetTextAlignment(TextAlignment.CENTER).SetMarginLeft(4);
                cashierSignCell.Add(new Paragraph("ISSUED & VERIFIED BY").SetFont(fontBold).SetFontSize(7.5f).SetFontColor(slateMuted).SetMarginBottom(2f));
                cashierSignCell.Add(new Paragraph(d.AdminName)
                    .SetFont(fontBold).SetFontSize(9f).SetFontColor(darkNavy).SetMarginTop(18f).SetMarginBottom(1f));
                cashierSignCell.Add(new Paragraph("Drive&Go Finance Officer / Authorized Cashier")
                    .SetFont(fontNormal).SetFontSize(6.8f).SetFontColor(slateMuted).SetBorderTop(new SolidBorder(darkNavy, 0.75f)).SetPaddingTop(2f));

                signGrid.AddCell(qrSignCell);
                signGrid.AddCell(cashierSignCell);
                doc.Add(signGrid);

                // ── 6. Document Footer ──
                var footerText = new Paragraph("Drive&Go Vehicle Rental System • Official Digitally Verified Payment Receipt • CSJDM | Norzagaray, Bulacan, Philippines • Hotline: +63 935 966 7178")
                    .SetFont(fontNormal)
                    .SetFontSize(7.5f)
                    .SetFontColor(slateMuted)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginTop(6);
                doc.Add(footerText);
            }

            return ms.ToArray();
        }

        private static void AddBillRow(Cell container, string label, string amount, PdfFont fontNormal, PdfFont fontBold, DeviceRgb color, bool isTotalRow)
        {
            var row = new Table(UnitValue.CreatePercentArray(new float[] { 60, 40 })).UseAllAvailableWidth();
            row.AddCell(new Cell().SetBorder(Border.NO_BORDER).Add(
                new Paragraph(label).SetFont(fontNormal).SetFontSize(9.5f).SetFontColor(color)));
            row.AddCell(new Cell().SetBorder(Border.NO_BORDER).Add(
                new Paragraph(amount).SetFont(isTotalRow ? fontBold : fontNormal).SetFontSize(9.5f).SetFontColor(color).SetTextAlignment(TextAlignment.RIGHT)));
            container.Add(row);
        }
    }
}

