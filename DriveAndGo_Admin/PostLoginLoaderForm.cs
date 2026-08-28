#nullable disable
using DriveAndGo_Admin.Helpers;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;

namespace DriveAndGo_Admin
{
    /// <summary>
    /// Drive &amp; Go — Automotive HUD Loading Screen v2.1
    /// • Full WebView2 HTML/CSS/JS UI (Dark / Light Ford Everest video)
    /// • Real geolocation via ip-api.com → real weather via Open-Meteo (no API key)
    /// • Minimum 1.5 s display · Auto-launches MainForm on completion
    /// </summary>
    public class PostLoginLoaderForm : Form
    {
        // ── WebView2 ─────────────────────────────────────────────────────────
        private WebView2 _webView;
        private bool     _webViewReady = false;

        // ── HTTP for weather (reused across calls) ───────────────────────────
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(6)
        };

        // ── Timing & state ───────────────────────────────────────────────────
        private System.Windows.Forms.Timer _progressTimer;
        private Stopwatch _stopwatch;
        private float     _progress    = 0f;
        private bool      _completed   = false;
        private bool      _isFadingOut = false;
        private float     _formOpacity = 0f;

        // Real telemetry values fetched async
        private string _realLocation = "Fetching…";
        private string _realWeather  = "--°C";
        private string _realCondition = "";

        private const int MIN_DISPLAY_MS = 1500;   // hard minimum
        private const int MAX_DISPLAY_MS = 8000;   // hard cap

        // ── Card dimensions (landscape / cinematic, matches mockup) ───────────
        private const int CARD_W = 740;
        private const int CARD_H = 430;
        private const int SHADOW = 20;

        // ════════════════════════════════════════════════════════════════════
        public PostLoginLoaderForm()
        {
            BuildForm();
            InitWebViewAsync();
            StartProgressSequence();

            // Kick off real data fetch in background — results pushed to page when ready
            _ = FetchRealTelemetryAsync();
        }

        // ════════════════════════════════════════════════════════════════════
        //  FORM SETUP
        // ════════════════════════════════════════════════════════════════════
        private void BuildForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.Size            = new Size(CARD_W + SHADOW * 2, CARD_H + SHADOW * 2);
            this.TopMost         = true;
            this.Opacity         = 0;
            this.Text            = "Drive & Go — Initializing";
            this.BackColor       = Color.Magenta;
            this.TransparencyKey = Color.Magenta;

            IconHelper.ApplyToForm(this);

            SetDoubleBuffer(this);
            this.SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint  |
                ControlStyles.UserPaint, true);
            this.UpdateStyles();
        }

        // ════════════════════════════════════════════════════════════════════
        //  WEBVIEW2
        // ════════════════════════════════════════════════════════════════════
        private async void InitWebViewAsync()
        {
            try
            {
                _webView = new WebView2
                {
                    Bounds                = new Rectangle(SHADOW, SHADOW, CARD_W, CARD_H),
                    DefaultBackgroundColor = Color.Transparent
                };
                this.Controls.Add(_webView);

                await _webView.EnsureCoreWebView2Async();

                // Harden browser
                var s = _webView.CoreWebView2.Settings;
                s.AreDefaultContextMenusEnabled      = false;
                s.AreDevToolsEnabled                 = false;
                s.IsStatusBarEnabled                 = false;
                s.IsZoomControlEnabled               = false;
                s.AreBrowserAcceleratorKeysEnabled   = false;

                string theme = ThemeManager.IsDarkMode ? "dark" : "light";
                await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                    $"document.documentElement.setAttribute('data-theme', '{theme}');");

                string htmlPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "WebAssets", "loading_video.html");

                if (File.Exists(htmlPath))
                {
                    _webView.NavigationCompleted += (s, e) =>
                    {
                        _webViewReady = true;
                        PushTelemetryToWebView();
                    };
                    string navUrl = "file:///" + htmlPath.Replace('\\', '/') + "?theme=" + theme;
                    _webView.CoreWebView2.Navigate(navUrl);
                }
                else
                {
                    _webView.Visible = false;
                    Debug.WriteLine("[Loader] loading_video.html not found.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Loader] WebView2 failed: {ex.Message}");
                try { _webView?.Dispose(); } catch { }
                _webView = null;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  REAL TELEMETRY — Location + Weather via existing DriveAndGo API
        // ════════════════════════════════════════════════════════════════════
        private string _realWind = "12 km/h";

        private async Task FetchRealTelemetryAsync()
        {
            // ── 1. Geolocation (ip-api.com, existing GeoLocationService) ─────
            try
            {
                var geo = await GeoLocationService.GetGeoAsync();
                _realLocation = geo.LocationLabel;
                PushTelemetryToWebView();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Loader] Geo failed: {ex.Message}");
            }

            // ── 2. Weather — call existing /api/weather/current endpoint ─────
            try
            {
                var res = await ApiService.GetAsync("weather/current");
                if (res != null && res.Success && !string.IsNullOrWhiteSpace(res.Body))
                {
                    using var doc = JsonDocument.Parse(res.Body);
                    var root = doc.RootElement;

                    double tempC   = root.TryGetProperty("temperature",    out var t)  ? t.GetDouble()  : 28.0;
                    double windKmh = root.TryGetProperty("wind_speed_kmh", out var w)  ? w.GetDouble()  : 18.0;
                    string cond    = root.TryGetProperty("condition",       out var c)  ? c.GetString()  : "Monsoon Surge / Rain";

                    _realWeather   = $"{tempC:F0}°C";
                    _realCondition = cond;
                    _realWind      = $"{windKmh:F0} km/h";

                    PushTelemetryToWebView();
                }
                else
                {
                    _realWeather   = "28°C";
                    _realCondition = "Monsoon Surge / Rain";
                    _realWind      = "18 km/h";
                    PushTelemetryToWebView();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Loader] Weather API failed: {ex.Message}");
                _realWeather   = "28°C";
                _realCondition = "Monsoon Surge / Rain";
                _realWind      = "18 km/h";
                PushTelemetryToWebView();
            }
        }

        private async void PushTelemetryToWebView()
        {
            if (!_webViewReady || _webView == null || _webView.IsDisposed) return;

            string theme = ThemeManager.IsDarkMode ? "dark" : "light";
            await SafeExecuteScript($"window.setTheme && window.setTheme('{theme}');");

            if (!string.IsNullOrEmpty(_realLocation))
            {
                await SafeExecuteScript($"window.setLocation({EscapeJs(_realLocation)});");
            }
            if (!string.IsNullOrEmpty(_realWeather))
            {
                await SafeExecuteScript(
                    $"window.setWeather(" +
                    $"{EscapeJs(_realWeather)}, " +
                    $"{EscapeJs(_realCondition)}, " +
                    $"{EscapeJs(_realWind)});");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  PROGRESS SEQUENCE
        // ════════════════════════════════════════════════════════════════════
        private const int TARGET_DURATION_MS = 1800; // 1.8 seconds smooth target load
        private const int MIN_DURATION_MS    = 1500; // 1.5 seconds minimum threshold

        private void StartProgressSequence()
        {
            _stopwatch = new Stopwatch();
            _stopwatch.Start();
            FadeIn();

            _progressTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _progressTimer.Tick += OnProgressTick;
            _progressTimer.Start();
        }

        private void FadeIn()
        {
            var t = new System.Windows.Forms.Timer { Interval = 16 };
            t.Tick += (s, e) =>
            {
                _formOpacity = Math.Min(_formOpacity + 0.08f, 1.0f);
                this.Opacity = _formOpacity;
                if (_formOpacity >= 1.0f) { t.Stop(); t.Dispose(); }
            };
            t.Start();
        }

        private async void OnProgressTick(object sender, EventArgs e)
        {
            if (_completed || _isFadingOut) return;

            long elapsed = _stopwatch.ElapsedMilliseconds;

            // Smooth linear/ease curve reaching 100% at ~1.8 seconds
            _progress = Math.Min(100f, ((float)elapsed / TARGET_DURATION_MS) * 100f);

            await SafeExecuteScript($"window.setProgress({_progress:F1});");

            if (_progress >= 100f && elapsed >= MIN_DURATION_MS)
            {
                _completed = true;
                _stopwatch.Stop();
                _progressTimer.Stop();

                await Task.Delay(250);   // snappy 100% completion pause
                FadeOutAndRoute();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  AUTO-LAUNCH DASHBOARD
        // ════════════════════════════════════════════════════════════════════
        private void FadeOutAndRoute()
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            if (this.InvokeRequired)
            {
                this.BeginInvoke((Action)FadeOutAndRoute);
                return;
            }

            _isFadingOut = true;
            var t = new System.Windows.Forms.Timer { Interval = 16 };
            t.Tick += (s, e) =>
            {
                _formOpacity = Math.Max(_formOpacity - 0.08f, 0f);
                this.Opacity = _formOpacity;
                if (_formOpacity <= 0f)
                {
                    t.Stop(); t.Dispose();
                    this.Hide();

                    var mainForm = new MainForm();
                    mainForm.Show();
                    mainForm.BringToFront();
                    mainForm.Activate();

                    this.Dispose();
                }
            };
            t.Start();
        }

        // ════════════════════════════════════════════════════════════════════
        //  PAINT — Ambient neon glow border (outside WebView, GDI+)
        // ════════════════════════════════════════════════════════════════════
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var cardRect = new Rectangle(SHADOW, SHADOW, CARD_W - 1, CARD_H - 1);
            using var path = RoundedRect(cardRect, 18);

            using var glow2 = new Pen(Color.FromArgb(16, ThemeManager.CurrentPrimary), 8f);
            using var glow1 = new Pen(Color.FromArgb(30, ThemeManager.CurrentPrimary), 4f);
            using var edge  = new Pen(Color.FromArgb(60, ThemeManager.CurrentPrimary), 1.2f);

            g.DrawPath(glow2, path);
            g.DrawPath(glow1, path);
            g.DrawPath(edge,  path);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
            => e.Graphics.Clear(Color.Magenta);

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Safe ExecuteScriptAsync: no-op if WebView is not yet ready or disposed.</summary>
        private async Task SafeExecuteScript(string js)
        {
            if (!_webViewReady || _webView == null || _webView.IsDisposed) return;
            try { await _webView.CoreWebView2.ExecuteScriptAsync(js); }
            catch { /* ignore — page may still be loading */ }
        }

        /// <summary>Wrap a string in JS single-quoted string literal (escape single quotes).</summary>
        private static string EscapeJs(string value)
            => "'" + (value ?? "").Replace("\\", "\\\\").Replace("'", "\\'") + "'";

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            int d    = radius * 2;
            var path = new GraphicsPath();
            path.StartFigure();
            path.AddArc(rect.X,          rect.Y,          d, d, 180, 90);
            path.AddArc(rect.Right - d,  rect.Y,          d, d, 270, 90);
            path.AddArc(rect.Right - d,  rect.Bottom - d, d, d,   0, 90);
            path.AddArc(rect.X,          rect.Bottom - d, d, d,  90, 90);
            path.CloseFigure();
            return path;
        }

        private static void SetDoubleBuffer(Control c)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, c, new object[] { true });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _progressTimer?.Stop();
                _progressTimer?.Dispose();
                try { _webView?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
