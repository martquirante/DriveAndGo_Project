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
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "DriveAndGo_Logo.png"),
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.png"),
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "images", "DriveAndGo_Logo.png"),
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "images", "logo.png"),
                    System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Resources", "DriveAndGo_Logo.png"),
                    System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Resources", "logo.png"),
                    @"C:\Users\martq\source\repos\DriveAndGo_Project\DriveAndGo_API\Resources\DriveAndGo_Logo.png"
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

                vehCell.Add(new Paragraph("VEHICLE SPECIFICATIONS")
                    .SetFont(fontBold)
                    .SetFontColor(BrandOrange)
                    .SetFontSize(9.5f)
                    .SetMarginBottom(6));

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
                var periodTable = new Table(UnitValue.CreatePercentArray(new float[] { 68, 32 }))
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
                periodRight.Add(new Paragraph($"Status: {data.PaymentStatus.ToUpper()}").SetFont(fontBold).SetFontSize(9f).SetFontColor(DarkNavy).SetMarginTop(3f));

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
                                            "4. In case of emergency or mechanical breakdown, contact the Drive&Go 24/7 hotline immediately.")
                    .SetFont(fontNormal)
                    .SetFontSize(8f)
                    .SetFontColor(SlateMuted));
                termsTable.AddCell(termsCell);
                document.Add(termsTable);

                // ── 6. Official Dual Signature Blocks ──
                var signTable = new Table(UnitValue.CreatePercentArray(new float[] { 50, 50 }))
                    .UseAllAvailableWidth()
                    .SetMarginTop(8)
                    .SetMarginBottom(10);

                var custSignCell = new Cell()
                    .SetBorder(new SolidBorder(BorderSlate, 1))
                    .SetPadding(10)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginRight(4);

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
                            .ScaleToFit(100f, 26f)
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
                        .SetMarginTop(22f)
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
                    .SetPadding(10)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginLeft(4);

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
                            .ScaleToFit(100f, 26f)
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
                        .SetMarginTop(22f)
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

                signTable.AddCell(custSignCell);
                signTable.AddCell(adminSignCell);
                document.Add(signTable);

                // ── 7. Document Footer Stamp ──
                var footerText = new Paragraph("Drive&Go Vehicle Rental System • Official Digitally Verified Agreement • CSJDM | Norzagaray, Bulacan")
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
    }
}
