#nullable disable
using DriveAndGo_Admin.Helpers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DriveAndGo_Admin.Panels
{
    public class TransactionsPanel : UserControl
    {
        private WebView2 _webView;
        private Panel    _loadingPanel;
        private Label    _loadingLabel;
        private bool     _webReady = false;

        public TransactionsPanel()
        {
            this.Dock      = DockStyle.Fill;
            this.BackColor = ThemeManager.CurrentBackground;
            BuildLoading();
            _ = InitWebView();
            ThemeManager.ThemeChanged += ThemeChanged_Handler;
            this.Disposed += (s, e) => ThemeManager.ThemeChanged -= ThemeChanged_Handler;
        }

        private void ThemeChanged_Handler(object sender, EventArgs e)
        {
            this.BackColor = ThemeManager.CurrentBackground;
            if (_webView != null) _webView.DefaultBackgroundColor = ThemeManager.CurrentBackground;
            if (_webReady && _webView?.CoreWebView2 != null)
            {
                bool dk = ThemeManager.IsDarkMode;
                _webView.BeginInvoke((MethodInvoker)(async () =>
                {
                    await _webView.CoreWebView2.ExecuteScriptAsync($"if(window.setTheme) setTheme({(dk ? "true" : "false")});");
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
                Text = "Loading Transaction Hub…",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 107, 0),
                AutoSize = true,
                BackColor = Color.Transparent
            };

            _loadingPanel.Controls.Add(_loadingLabel);
            _loadingPanel.Resize += (s, e) =>
                _loadingLabel.Location = new Point(
                    Math.Max(10, (_loadingPanel.Width  - _loadingLabel.Width)  / 2),
                    Math.Max(10, (_loadingPanel.Height - _loadingLabel.Height) / 2));

            this.Controls.Add(_loadingPanel);
        }

        private async Task InitWebView()
        {
            if (_webView != null) return;

            try
            {
                string htmlPath = WebAssetHelper.GetWebAssetPath("TransactionsWeb.html", "transactions");

                _webView = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = ThemeManager.CurrentBackground };
                this.Controls.Add(_webView);
                _webView.BringToFront();

                var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Path.GetTempPath(), "DriveAndGo_TransactionsWV2"));
                await _webView.EnsureCoreWebView2Async(env);

                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                string apiBase = ApiService.BaseUrl.TrimEnd('/');
                string networkBase = ApiService.ResolveNetworkBaseUrl().TrimEnd('/');
                string currentAdmin = !string.IsNullOrWhiteSpace(SessionManager.FullName) ? SessionManager.FullName : "Admin";
                string jwtToken = SessionManager.JwtToken ?? SessionManager.Token ?? "";
                bool dk = ThemeManager.IsDarkMode;

                await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                    $"window.API_BASE_URL = '{apiBase}'; " +
                    $"window.API_NETWORK_URL = '{networkBase}'; " +
                    $"window.AUTH_TOKEN = '{jwtToken.Replace("'", "\\'")}'; " +
                    $"localStorage.setItem('auth_token', '{jwtToken.Replace("'", "\\'")}'); " +
                    $"window.CURRENT_ADMIN_NAME = '{currentAdmin.Replace("'", "\\'")}'; " +
                    $"localStorage.setItem('admin_name', '{currentAdmin.Replace("'", "\\'")}'); " +
                    $"document.documentElement.setAttribute('data-theme', '{(dk ? "dark" : "light")}');");

                _webView.NavigationCompleted += async (s, e) =>
                {
                    if (!e.IsSuccess) return;

                    _webReady = true;
                    if (_loadingPanel != null) _loadingPanel.Visible = false;

                    await _webView.CoreWebView2.ExecuteScriptAsync(
                        $"window.API_BASE_URL = '{apiBase}'; " +
                        $"window.API_NETWORK_URL = '{networkBase}'; " +
                        $"window.AUTH_TOKEN = '{jwtToken.Replace("'", "\\'")}'; " +
                        $"if (window.setTheme) setTheme({(dk ? "true" : "false")}); " +
                        $"if (window.fetchTransactionsData) window.fetchTransactionsData();");
                };

                _webView.CoreWebView2.Navigate("file:///" + htmlPath.Replace('\\', '/') + "?v=" + DateTime.UtcNow.Ticks);
            }
            catch (Exception ex)
            {
                if (_loadingLabel != null)
                {
                    _loadingLabel.Text      = $"WebView2 Error: {ex.Message}";
                    _loadingLabel.ForeColor = Color.Red;
                }
            }
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json = e.WebMessageAsJson;
                if (!string.IsNullOrEmpty(json))
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("action", out var act))
                    {
                        string action = act.GetString();
                        if (action == "repairLogs")
                        {
                            this.BeginInvoke((MethodInvoker)(() =>
                            {
                                MessageBox.Show(
                                    "Payment log reconciliation is handled automatically by the server.\nTransactions are actively synced.",
                                    "Log Reconciliation",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                            }));
                        }
                    }
                }
            }
            catch { }
        }

        public void RefreshData()
        {
            if (_webReady && _webView?.CoreWebView2 != null)
            {
                _webView.BeginInvoke((MethodInvoker)(async () =>
                {
                    await _webView.CoreWebView2.ExecuteScriptAsync("if(window.fetchTransactionsData) window.fetchTransactionsData();");
                }));
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
