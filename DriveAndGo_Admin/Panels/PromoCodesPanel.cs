using DriveAndGo_Admin.Helpers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DriveAndGo_Admin.Panels
{
    public class PromoCodesPanel : UserControl
    {
        private WebView2 _webView;

        public PromoCodesPanel()
        {
            this.Dock = DockStyle.Fill;
            this.DoubleBuffered = true;
            this.BackColor = ThemeManager.CurrentBackground;

            _ = InitWebViewAsync();
            ThemeManager.ThemeChanged += (s, e) =>
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
            };
        }

        private async Task InitWebViewAsync()
        {
            if (_webView != null) return;
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
                string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "promos_manager.html");
                if (File.Exists(htmlPath))
                {
                    string navUrl = "file:///" + htmlPath.Replace('\\', '/') + "?theme=" + theme;
                    _webView.CoreWebView2.Navigate(navUrl);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PromoCodesPanel] WebView2 init error: {ex.Message}");
            }
        }
    }
}
