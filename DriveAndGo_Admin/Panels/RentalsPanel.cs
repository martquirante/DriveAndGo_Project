#nullable disable
using DriveAndGo_Admin.Helpers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DriveAndGo_Admin.Panels
{
    public class RentalsPanel : UserControl
    {
        private WebView2 _webView;
        private Panel    _loadingPanel;
        private Label    _loadingLabel;
        private bool     _webReady = false;

        public RentalsPanel()
        {
            this.Dock      = DockStyle.Fill;
            this.BackColor = ThemeManager.CurrentBackground;
            BuildLoading();
            this.HandleCreated += async (s, e) => await InitWebView();
            ThemeManager.ThemeChanged += ThemeChanged_Handler;
            this.Disposed += (s, e) => ThemeManager.ThemeChanged -= ThemeChanged_Handler;
        }

        private void ThemeChanged_Handler(object sender, EventArgs e)
        {
            this.BackColor = ThemeManager.CurrentBackground;
            if (_webReady && _webView?.CoreWebView2 != null)
            {
                bool dk = ThemeManager.IsDarkMode;
                _webView.BeginInvoke((MethodInvoker)(async () =>
                {
                    await _webView.CoreWebView2.ExecuteScriptAsync($"setTheme({(dk ? "true" : "false")});");
                }));
            }
        }

        private void BuildLoading()
        {
            _loadingPanel = new Panel 
            { 
                Dock = DockStyle.Fill, 
                BackColor = ThemeManager.CurrentBackground 
            };

            _loadingLabel = new Label
            {
                Text = "Loading Rentals Management\u2026",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 107, 0),
                AutoSize = true,
                BackColor = Color.Transparent
            };

            _loadingPanel.Controls.Add(_loadingLabel);
            _loadingPanel.Resize += (s, e) =>
                _loadingLabel.Location = new Point(
                    (_loadingPanel.Width - _loadingLabel.Width) / 2,
                    (_loadingPanel.Height - _loadingLabel.Height) / 2);

            this.Controls.Add(_loadingPanel);
        }

        private async Task InitWebView()
        {
            if (_webView != null) return;

            try
            {
                _webView = new WebView2 { Dock = DockStyle.Fill };
                this.Controls.Add(_webView);
                _webView.BringToFront();

                var env = await CoreWebView2Environment.CreateAsync(null, Path.GetTempPath());
                await _webView.EnsureCoreWebView2Async(env);

                string outputAssetsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets");
                if (!Directory.Exists(outputAssetsFolder))
                    Directory.CreateDirectory(outputAssetsFolder);

                string[] sourceCandidates =
                {
                    Path.Combine(Application.StartupPath, "WebAssets", "RentalsWeb.html"),
                    Path.Combine(Application.StartupPath, "RentalsWeb.html"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "RentalsWeb.html"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RentalsWeb.html"),
                    Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\WebAssets\RentalsWeb.html")),
                    Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\..\WebAssets\RentalsWeb.html")),
                    @"C:\Users\martq\source\repos\DriveAndGo_Project\DriveAndGo_Admin\WebAssets\RentalsWeb.html"
                };

                string sourceHtml = sourceCandidates.FirstOrDefault(File.Exists);
                string destHtml = Path.Combine(outputAssetsFolder, "RentalsWeb.html");

                if (!string.IsNullOrEmpty(sourceHtml))
                {
                    if (!string.Equals(sourceHtml, destHtml, StringComparison.OrdinalIgnoreCase))
                        File.Copy(sourceHtml, destHtml, true);
                }

                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "appassets",
                    outputAssetsFolder,
                    CoreWebView2HostResourceAccessKind.Allow);

                _webView.NavigationCompleted += async (s, e) =>
                {
                    if (!e.IsSuccess) return;

                    _webReady = true;
                    if (_loadingPanel != null) _loadingPanel.Visible = false;
                    bool dk = ThemeManager.IsDarkMode;
                    string networkBase = ApiService.BaseUrl.TrimEnd('/');
                    string currentAdmin = !string.IsNullOrWhiteSpace(SessionManager.FullName) ? SessionManager.FullName : "Raymart Quirante";
                    await _webView.CoreWebView2.ExecuteScriptAsync($"window.API_BASE_URL = 'http://localhost:5233/api'; window.API_NETWORK_URL = '{networkBase}'; window.CURRENT_ADMIN_NAME = '{currentAdmin.Replace("'", "\\'")}'; localStorage.setItem('admin_name', '{currentAdmin.Replace("'", "\\'")}'); setTheme({(dk ? "true" : "false")}); if (window.fetchRentalsData) window.fetchRentalsData();");
                };

                if (File.Exists(destHtml))
                {
                    _webView.CoreWebView2.Navigate("https://appassets/RentalsWeb.html");
                }
                else
                {
                    string fallbackPath = sourceCandidates.FirstOrDefault(File.Exists);
                    if (!string.IsNullOrEmpty(fallbackPath))
                    {
                        _webView.CoreWebView2.Navigate(new Uri(fallbackPath).AbsoluteUri);
                    }
                    else
                    {
                        _webView.NavigateToString(
                            "<html><body style='background:#090D16;color:#F8FAFC;font-family:Segoe UI;" +
                            "display:flex;align-items:center;justify-content:center;height:100vh;margin:0'>" +
                            "<div style='text-align:center'><div style='font-size:36px;color:#FF6B00;'>⬡</div>" +
                            "<p style='font-weight:bold;font-size:16px;'>RentalsWeb.html not found in WebAssets.</p>" +
                            "<p style='color:#94A3B8;font-size:12px;'>Please ensure RentalsWeb.html is copied to output directory.</p></div></body></html>");
                    }
                }
            }
            catch (Exception ex)
            {
                if (_loadingLabel != null)
                {
                    _loadingLabel.Text = $"WebView2 Error: {ex.Message}";
                    _loadingLabel.ForeColor = Color.Red;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= ThemeChanged_Handler;
                _webView?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}