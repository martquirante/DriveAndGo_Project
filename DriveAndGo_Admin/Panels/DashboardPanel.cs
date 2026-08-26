#nullable disable
using DriveAndGo_Admin.Helpers;
using System.Text.Json;

namespace DriveAndGo_Admin.Panels
{
    /// <summary>
    /// Pure Lightweight React Host for DashboardOverview.html.
    /// All legacy WinForms GDI+ controls have been permanently stripped to enable 120FPS sidebar animations.
    /// </summary>
    public class DashboardPanel : UserControl
    {
        private Microsoft.Web.WebView2.WinForms.WebView2 _dashWebView;
        private System.Windows.Forms.Timer _refreshTimer;

        public DashboardPanel()
        {
            this.Controls.Clear();
            this.Dock = DockStyle.Fill;
            this.BackColor = ThemeManager.CurrentBackground;

            ThemeManager.ThemeChanged += ThemeChanged_Handler;

            // Set up auto-refresh timer to ping React
            _refreshTimer = new System.Windows.Forms.Timer { Interval = 10000 };
            _refreshTimer.Tick += (s, e) => LoadStatsFromDB();
            _refreshTimer.Start();

            this.HandleCreated += (s, e) => BuildWebDashboard();

            this.VisibleChanged += (s, e) =>
            {
                if (this.Visible && this.IsHandleCreated && !this.IsDisposed)
                {
                    RefreshWebViewData();
                    PushThemeToWebView(ThemeManager.IsDarkMode ? "dark" : "light");
                }
            };
        }

        private void ThemeChanged_Handler(object sender, EventArgs e)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            this.BackColor = ThemeManager.CurrentBackground;
        }

        private async void BuildWebDashboard()
        {
            try
            {
                string htmlPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "DashboardOverview.html");

                if (!System.IO.File.Exists(htmlPath))
                {
                    Console.WriteLine("[Dashboard] DashboardOverview.html not found.");
                    return;
                }

                _dashWebView = new Microsoft.Web.WebView2.WinForms.WebView2 { Dock = DockStyle.Fill };
                this.Controls.Add(_dashWebView);
                _dashWebView.BringToFront();

                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(
                    null, System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DriveAndGo_DashWV2"));

                await _dashWebView.EnsureCoreWebView2Async(env);

                _dashWebView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;

                // Harden browser environment
                _dashWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _dashWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _dashWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;

                string token = SessionManager.Token ?? string.Empty;
                string apiBase = ApiService.BaseUrl.TrimEnd('/');
                string currentTheme = ThemeManager.IsDarkMode ? "dark" : "light";

                // Pre-inject variables before DOM loads
                await _dashWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                    $"window.API_BASE_URL = '{apiBase}'; window.AUTH_TOKEN = '{token}'; document.documentElement.setAttribute('data-theme', '{currentTheme}');");

                _dashWebView.CoreWebView2.NavigationCompleted += async (sender, args) =>
                {
                    if (_dashWebView == null || _dashWebView.IsDisposed || _dashWebView.CoreWebView2 == null) return;
                    try
                    {
                        await _dashWebView.CoreWebView2.ExecuteScriptAsync(
                            $"window.API_BASE_URL = '{apiBase}'; window.AUTH_TOKEN = '{token}';" +
                            $"if(window.setDashboardTheme) window.setDashboardTheme('{currentTheme}');" +
                            "if(window.forceDashboardRefresh) window.forceDashboardRefresh();" +
                            "else if(window.refreshDashboardData) window.refreshDashboardData();");
                    }
                    catch { }
                };

                _dashWebView.CoreWebView2.Navigate("file:///" + htmlPath.Replace('\\', '/'));
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Dashboard] BuildWebDashboard failed: " + ex.Message);
            }
        }

        public void RefreshWebViewData()
        {
            if (!this.IsHandleCreated || this.IsDisposed || _dashWebView == null) return;
            this.BeginInvoke((MethodInvoker)(async () =>
            {
                try
                {
                    if (_dashWebView.IsDisposed || _dashWebView.CoreWebView2 == null) return;
                    string token = SessionManager.JwtToken ?? string.Empty;
                    string apiBase = ApiService.BaseUrl.TrimEnd('/');
                    string initJs = $"window.API_BASE_URL='{apiBase}'; window.AUTH_TOKEN='{token}';"
                                  + " if(window.forceDashboardRefresh) window.forceDashboardRefresh();"
                                  + " else if(window.refreshDashboardData) window.refreshDashboardData();";
                    await _dashWebView.CoreWebView2.ExecuteScriptAsync(initJs);
                }
                catch { }
            }));
        }

        public void PushThemeToWebView(string theme)
        {
            if (!this.IsHandleCreated || this.IsDisposed || _dashWebView == null) return;
            this.BeginInvoke((MethodInvoker)(async () =>
            {
                try
                {
                    if (_dashWebView.IsDisposed || _dashWebView.CoreWebView2 == null) return;
                    string safeTheme = theme == "light" ? "light" : "dark";
                    await _dashWebView.CoreWebView2.ExecuteScriptAsync($"if(window.setDashboardTheme) window.setDashboardTheme('{safeTheme}');");
                }
                catch { }
            }));
        }

        public void LoadStatsFromDB()
        {
            // Now strictly triggers the React side to reload its data via JS bridge
            RefreshWebViewData();
        }

        private void WebView_WebMessageReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string rawStr = e.TryGetWebMessageAsString();
                string action = string.Empty;

                if (!string.IsNullOrEmpty(rawStr)) action = rawStr;
                else
                {
                    try
                    {
                        string json = e.WebMessageAsJson;
                        if (!string.IsNullOrEmpty(json))
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("action", out var actionProp))
                            {
                                action = actionProp.GetString();
                            }
                        }
                    }
                    catch { }
                }

                if (action?.Trim() == "open_ai_insights")
                {
                    if (!this.IsHandleCreated || this.IsDisposed) return;
                    this.BeginInvoke((MethodInvoker)(() =>
                    {
                        try
                        {
                            using var aiForm = new AIBusinessInsightsForm();
                            aiForm.ShowDialog(this.FindForm());
                        }
                        catch { }
                    }));
                }
                else if (action?.Trim() == "open_rentals" || action?.Trim() == "navigate_rentals")
                {
                    if (this.FindForm() is MainForm mainForm)
                    {
                        mainForm.NavigateToRentals();
                    }
                }

            }
            catch { }
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= ThemeChanged_Handler;
                _refreshTimer?.Stop();
                _refreshTimer?.Dispose();
                try { _dashWebView?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}