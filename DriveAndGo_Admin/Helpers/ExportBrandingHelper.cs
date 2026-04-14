using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace DriveAndGo_Admin.Helpers
{
    internal static class ExportBrandingHelper
    {
        public static string ResolveLogoPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "WebAssets", "logo.png"),
                Path.Combine(AppContext.BaseDirectory, "WebAssets", "logo.png"),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "WebAssets", "logo.png")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "DriveAndGo_Admin", "WebAssets", "logo.png"))
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        public static string GetDisplayStatus(string status, string fallback = "Pending Review")
        {
            string normalized = status?.Trim().ToLowerInvariant() ?? string.Empty;
            return normalized switch
            {
                "" => fallback,
                "confirmed" => "Confirmed",
                "paid" => "Paid",
                "verified" => "Verified",
                "pending" => "Pending",
                "rejected" => "Rejected",
                "refunded" => "Refunded",
                "duplicate" => "Duplicate",
                _ => CultureText(normalized)
            };
        }

        public static string GetDisplayMethod(string method)
        {
            string normalized = method?.Trim().ToLowerInvariant() ?? string.Empty;
            return normalized switch
            {
                "" => "Cash",
                "gcash" => "GCash",
                "maya" => "Maya",
                "bank" => "Bank",
                "bank_transfer" => "Bank Transfer",
                _ => CultureText(normalized)
            };
        }

        public static string EscapeHtml(string value) =>
            WebUtility.HtmlEncode(value ?? string.Empty);

        public static string GetLogoDataUri()
        {
            string logoPath = ResolveLogoPath();
            if (string.IsNullOrWhiteSpace(logoPath) || !File.Exists(logoPath))
                return string.Empty;

            byte[] bytes = File.ReadAllBytes(logoPath);
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }

        public static void AppendExcelCell(StringBuilder html, string value, string cssClass)
        {
            html.Append("<td class=\"")
                .Append(cssClass)
                .Append("\">")
                .Append(EscapeHtml(value))
                .AppendLine("</td>");
        }

        private static string CultureText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return string.Join(" ",
                value.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        }
    }
}
