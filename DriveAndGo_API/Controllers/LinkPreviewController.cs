using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Mvc;

namespace DriveAndGo_API.Controllers
{
    [ApiController]
    [Route("api/media")]
    public class LinkPreviewController : ControllerBase
    {
        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        })
        {
            Timeout = TimeSpan.FromSeconds(6)
        };

        static LinkPreviewController()
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.5");
        }

        // GET /api/media/link-preview?url=https://youtu.be/...
        [HttpGet("link-preview")]
        public async Task<IActionResult> GetLinkPreview([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest(new { Message = "URL parameter is required" });

            url = url.Trim();
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            try
            {
                Uri uri = new Uri(url);
                string domain = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host.Substring(4) : uri.Host;

                string html = "";
                using (var response = await _httpClient.GetAsync(uri))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return Ok(new LinkPreviewDto
                        {
                            Url = url,
                            Title = domain,
                            Description = url,
                            Domain = domain,
                            Image = "",
                            SiteName = domain
                        });
                    }
                    html = await response.Content.ReadAsStringAsync();
                }

                string title = ExtractMetaTag(html, "og:title") ?? ExtractTagContent(html, "title") ?? domain;
                string description = ExtractMetaTag(html, "og:description") ?? ExtractMetaTag(html, "description") ?? "";
                string image = ExtractMetaTag(html, "og:image") ?? ExtractMetaTag(html, "twitter:image") ?? "";
                string siteName = ExtractMetaTag(html, "og:site_name") ?? domain;

                // Decode HTML entities
                title = HttpUtility.HtmlDecode(title).Trim();
                description = HttpUtility.HtmlDecode(description).Trim();

                // Make relative image URLs absolute
                if (!string.IsNullOrWhiteSpace(image) && !image.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    try { image = new Uri(uri, image).ToString(); } catch { }
                }

                return Ok(new LinkPreviewDto
                {
                    Url = url,
                    Title = title,
                    Description = description,
                    Domain = domain,
                    Image = image,
                    SiteName = siteName
                });
            }
            catch (Exception)
            {
                string fallbackDomain = "link";
                try { fallbackDomain = new Uri(url).Host; } catch { }

                return Ok(new LinkPreviewDto
                {
                    Url = url,
                    Title = fallbackDomain,
                    Description = url,
                    Domain = fallbackDomain,
                    Image = "",
                    SiteName = fallbackDomain
                });
            }
        }

        private static string ExtractMetaTag(string html, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(html)) return null;

            // Match <meta property="og:title" content="..." /> or <meta name="description" content="..." />
            var pattern = $@"<meta\s+[^>]*(?:property|name)=[""']{Regex.Escape(propertyName)}[""']\s+[^>]*content=[""']([^""']+)[""']";
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value;

            // Alternative order: content="..." property="..."
            pattern = $@"<meta\s+[^>]*content=[""']([^""']+)[""']\s+[^>]*(?:property|name)=[""']{Regex.Escape(propertyName)}[""']";
            match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value;

            return null;
        }

        private static string ExtractTagContent(string html, string tagName)
        {
            if (string.IsNullOrWhiteSpace(html)) return null;
            var pattern = $@"<{tagName}[^>]*>(.*?)</{tagName}>";
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return match.Success ? match.Groups[1].Value : null;
        }
    }

    public class LinkPreviewDto
    {
        public string Url { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Domain { get; set; } = "";
        public string Image { get; set; } = "";
        public string SiteName { get; set; } = "";
    }
}
