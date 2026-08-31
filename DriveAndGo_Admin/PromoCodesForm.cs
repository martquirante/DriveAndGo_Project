using DriveAndGo_Admin.Helpers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DriveAndGo_Admin
{
    public class PromoCodesForm : Form
    {
        private WebView2 _webView;
        private bool _isDragging = false;
        private Point _dragCursorPoint;
        private Point _dragFormPoint;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        public PromoCodesForm()
        {
            this.Size = new Size(1140, 710);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = ThemeManager.CurrentBackground;
            this.DoubleBuffered = true;

            // Set rounded region for smooth corners
            SetRoundedRegion(18);

            this.HandleCreated += async (s, e) => await InitWebViewAsync();
            ThemeManager.ThemeChanged += OnThemeChanged;
            this.Disposed += (s, e) => ThemeManager.ThemeChanged -= OnThemeChanged;
        }

        private void SetRoundedRegion(int radius)
        {
            try
            {
                using var path = new GraphicsPath();
                int d = radius * 2;
                var rect = new Rectangle(0, 0, this.Width, this.Height);
                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                this.Region = new Region(path);
            }
            catch { }
        }

        private async Task InitWebViewAsync()
        {
            try
            {
                _webView = new WebView2
                {
                    Dock = DockStyle.Fill,
                    DefaultBackgroundColor = ThemeManager.CurrentBackground
                };
                this.Controls.Add(_webView);

                var env = await CoreWebView2Environment.CreateAsync(null, Path.GetTempPath());
                await _webView.EnsureCoreWebView2Async(env);

                var s = _webView.CoreWebView2.Settings;
                s.IsStatusBarEnabled = false;
                s.AreDefaultContextMenusEnabled = false;
                s.IsZoomControlEnabled = false;

                string theme = ThemeManager.IsDarkMode ? "dark" : "light";
                string apiBase = Helpers.ApiService.ResolveNetworkBaseUrl();
                await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                    $"window.API_BASE_URL = '{apiBase}'; document.documentElement.setAttribute('data-theme', '{theme}');");


                _webView.CoreWebView2.WebMessageReceived += (sender, args) =>
                {
                    try
                    {
                        string json = args.WebMessageAsJson;
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("action", out var act))
                        {
                            string actionStr = act.GetString() ?? "";
                            if (actionStr == "close")
                            {
                                this.BeginInvoke((Action)(() => this.Close()));
                            }
                            else if (actionStr == "copyToClipboard" && doc.RootElement.TryGetProperty("text", out var textEl))
                            {
                                string textToCopy = textEl.GetString() ?? "";
                                if (!string.IsNullOrEmpty(textToCopy))
                                {
                                    this.BeginInvoke((Action)(() =>
                                    {
                                        try
                                        {
                                            Clipboard.SetDataObject(textToCopy, true);
                                        }
                                        catch { }
                                    }));
                                }
                            }
                        }
                    }
                    catch { }
                };

                string htmlPath = Helpers.WebAssetHelper.GetWebAssetPath("promos_manager.html", "promos");
                if (File.Exists(htmlPath))
                {
                    string navUrl = "file:///" + htmlPath.Replace('\\', '/') + "?theme=" + theme;
                    _webView.CoreWebView2.Navigate(navUrl);
                }
                else
                {
                    _webView.NavigateToString("<html><body style='background:#0B0B16;color:#FFF;padding:40px;font-family:sans-serif;'><h2>promos_manager.html not found</h2></body></html>");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PromoCodesForm] WebView2 init failed: {ex.Message}");
            }
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            this.BackColor = ThemeManager.CurrentBackground;
            if (_webView?.CoreWebView2 != null)
            {
                string theme = ThemeManager.IsDarkMode ? "dark" : "light";
                _webView.BeginInvoke((Action)(async () =>
                {
                    await _webView.CoreWebView2.ExecuteScriptAsync($"window.setTheme && window.setTheme('{theme}');");
                }));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(100, ThemeManager.CurrentPrimary), 1.5f);
            g.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
        }
    }
}
