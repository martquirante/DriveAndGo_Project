using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;

namespace DriveAndGo_API.Helpers
{
    public static class LogoHelper
    {
        private static readonly ConcurrentDictionary<string, byte[]> _cache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        public static string GetBrandSlug(string brandName)
        {
            if (string.IsNullOrWhiteSpace(brandName)) return "toyota";
            string clean = brandName.Trim().ToLowerInvariant();
            if (clean.Contains("ford")) return "ford";
            if (clean.Contains("toyota")) return "toyota";
            if (clean.Contains("honda")) return "honda";
            if (clean.Contains("mitsubishi") || clean.Contains("mitsu")) return "mitsubishi";
            if (clean.Contains("nissan")) return "nissan";
            if (clean.Contains("hyundai")) return "hyundai";
            if (clean.Contains("suzuki")) return "suzuki";
            if (clean.Contains("kia")) return "kia";
            if (clean.Contains("chevrolet") || clean.Contains("chevy")) return "chevrolet";
            if (clean.Contains("mazda")) return "mazda";
            if (clean.Contains("isuzu")) return "isuzu";
            if (clean.Contains("bmw")) return "bmw";
            if (clean.Contains("merc") || clean.Contains("mercedes") || clean.Contains("benz")) return "mercedes-benz";
            if (clean.Contains("audi")) return "audi";
            if (clean.Contains("volkswagen") || clean.Contains("vw")) return "volkswagen";
            if (clean.Contains("subaru")) return "subaru";
            if (clean.Contains("landrover") || clean.Contains("land-rover") || clean.Contains("range rover")) return "land-rover";
            if (clean.Contains("geely")) return "geely";
            if (clean.Contains("byd")) return "byd";
            if (clean.Contains("chery")) return "chery";
            if (clean.Contains("gac")) return "gac";
            if (clean.Contains("changan")) return "changan";
            if (clean.Contains("jetour")) return "jetour";
            if (clean.Contains("mg") || clean.Contains("morris")) return "mg";
            if (clean.Contains("volvo")) return "volvo";
            if (clean.Contains("lexus")) return "lexus";
            if (clean.Contains("porsche")) return "porsche";
            if (clean.Contains("tesla")) return "tesla";
            if (clean.Contains("peugeot")) return "peugeot";
            if (clean.Contains("mini")) return "mini";
            if (clean.Contains("jaguar")) return "jaguar";
            if (clean.Contains("jeep")) return "jeep";
            if (clean.Contains("ram")) return "ram";
            if (clean.Contains("dodge")) return "dodge";
            if (clean.Contains("fiat")) return "fiat";
            if (clean.Contains("renault")) return "renault";
            if (clean.Contains("foton")) return "foton";
            if (clean.Contains("baic")) return "baic";
            if (clean.Contains("wuling")) return "wuling";
            if (clean.Contains("haval") || clean.Contains("gwm") || clean.Contains("great wall")) return "haval";
            return clean.Split(' ')[0].Replace(" ", "-").Replace("_", "-");
        }

        public static string GetPaymentKey(string paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod)) return "cash";
            string key = paymentMethod.Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
            if (key.Contains("gcash")) return "gcash";
            if (key.Contains("maya") || key.Contains("paymaya")) return "maya";
            if (key.Contains("bdo")) return "bdo";
            if (key.Contains("bpi")) return "bpi";
            if (key.Contains("unionbank") || key.Contains("ubp")) return "unionbank";
            if (key.Contains("metrobank") || key.Contains("mbt")) return "metrobank";
            if (key.Contains("bank") || key.Contains("transfer") || key.Contains("instapay") || key.Contains("pesonet")) return "bank";
            if (key.Contains("card") || key.Contains("visa") || key.Contains("mastercard")) return "card";
            return key;
        }

        public static byte[]? GetBrandLogoBytes(string? brandName)
        {
            if (string.IsNullOrWhiteSpace(brandName)) return null;
            string slug = GetBrandSlug(brandName);

            if (_cache.TryGetValue($"brand_{slug}", out var cached)) return cached;

            string[] localPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "brands", $"{slug}.png"),
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "brands", $"{slug}.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", $"{slug}.png"),
                Path.Combine(Directory.GetCurrentDirectory(), "Resources", $"{slug}.png"),
                $@"C:\Users\martq\source\repos\DriveAndGo_Project\DriveAndGo_API\wwwroot\brands\{slug}.png",
                $@"C:\Users\martq\source\repos\DriveAndGo_Project\DriveAndGo_Admin\WebAssets\brands\{slug}.png"
            };
            foreach (var p in localPaths)
            {
                if (File.Exists(p))
                {
                    try
                    {
                        var bytes = File.ReadAllBytes(p);
                        _cache[$"brand_{slug}"] = bytes;
                        return bytes;
                    }
                    catch { }
                }
            }

            try
            {
                string url = $"https://cdn.jsdelivr.net/gh/filippofilip95/car-logos-dataset@master/logos/original/{slug}.png";
                var resp = _http.GetAsync(url).GetAwaiter().GetResult();
                if (resp.IsSuccessStatusCode)
                {
                    var bytes = resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    if (bytes != null && bytes.Length > 0)
                    {
                        _cache[$"brand_{slug}"] = bytes;
                        // Auto-persist to local disk for permanent zero-latency offline access
                        try
                        {
                            string dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "brands");
                            Directory.CreateDirectory(dir);
                            File.WriteAllBytes(Path.Combine(dir, $"{slug}.png"), bytes);
                        }
                        catch { }
                        return bytes;
                    }
                }
            }
            catch { }

            return null;
        }

        private static readonly Dictionary<string, string> _paymentDomainMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["gcash"] = "gcash.com",
            ["maya"] = "maya.ph",
            ["paymaya"] = "maya.ph",
            ["bdo"] = "bdo.com.ph",
            ["bpi"] = "bpi.com.ph",
            ["unionbank"] = "unionbankph.com",
            ["ubp"] = "unionbankph.com",
            ["metrobank"] = "metrobank.com.ph",
            ["landbank"] = "landbank.com",
            ["chinabank"] = "chinabank.ph",
            ["rcbc"] = "rcbc.com",
            ["pnb"] = "pnb.com.ph",
            ["securitybank"] = "securitybank.com",
            ["eastwest"] = "eastwestbanker.com",
            ["gotyme"] = "gotyme.com.ph",
            ["seabank"] = "seabank.com.ph",
            ["tonik"] = "tonikbank.com",
            ["cimb"] = "cimbbank.com.ph",
            ["shopeepay"] = "shopee.ph",
            ["grabpay"] = "grab.com",
            ["palawanpay"] = "palawanpay.com",
            ["psbank"] = "psbank.com.ph",
            ["aub"] = "aub.com.ph",
            ["visa"] = "visa.com",
            ["mastercard"] = "mastercard.com"
        };

        public static byte[]? GetPaymentLogoBytes(string? paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod)) return null;
            string key = GetPaymentKey(paymentMethod);

            if (_cache.TryGetValue($"pay_{key}", out var cached)) return cached;

            string[] localPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "payments", $"{key}.png"),
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "payments", $"{key}.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", $"{key}.png"),
                Path.Combine(Directory.GetCurrentDirectory(), "Resources", $"{key}.png"),
                $@"C:\Users\martq\source\repos\DriveAndGo_Project\DriveAndGo_API\wwwroot\payments\{key}.png",
                $@"C:\Users\martq\source\repos\DriveAndGo_Project\DriveAndGo_Admin\WebAssets\payments\{key}.png"
            };
            foreach (var p in localPaths)
            {
                if (File.Exists(p))
                {
                    try
                    {
                        var bytes = File.ReadAllBytes(p);
                        _cache[$"pay_{key}"] = bytes;
                        return bytes;
                    }
                    catch { }
                }
            }

            string domain = _paymentDomainMap.TryGetValue(key, out var d) ? d : $"{key}.com.ph";

            string[] candidateUrls = new[]
            {
                $"https://www.google.com/s2/favicons?domain={domain}&sz=256",
                $"https://unavatar.io/{domain}?fallback=false",
                $"https://logo.clearbit.com/{domain}",
                $"https://icons.duckduckgo.com/ip3/{domain}.ico"
            };

            foreach (var url in candidateUrls)
            {
                try
                {
                    var resp = _http.GetAsync(url).GetAwaiter().GetResult();
                    if (resp.IsSuccessStatusCode)
                    {
                        var bytes = resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                        if (bytes != null && bytes.Length > 150)
                        {
                            _cache[$"pay_{key}"] = bytes;
                            try
                            {
                                string dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "payments");
                                Directory.CreateDirectory(dir);
                                File.WriteAllBytes(Path.Combine(dir, $"{key}.png"), bytes);
                            }
                            catch { }
                            return bytes;
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        public static byte[]? GetSystemLogoBytes()
        {
            if (_cache.TryGetValue("sys_logo", out var cached)) return cached;

            string[] candidateLogoPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "images", "logo.png"),
                Path.Combine(Directory.GetCurrentDirectory(), "Resources", "logo.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "logo.png"),
                @"C:\Users\martq\source\repos\DriveAndGo_Project\DriveAndGo_API\Resources\logo.png"
            };
            foreach (var p in candidateLogoPaths)
            {
                if (File.Exists(p))
                {
                    try
                    {
                        var bytes = File.ReadAllBytes(p);
                        _cache["sys_logo"] = bytes;
                        return bytes;
                    }
                    catch { }
                }
            }
            return null;
        }
    }
}
