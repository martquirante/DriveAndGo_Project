#nullable disable
using DriveAndGo_Admin.Helpers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DriveAndGo_Admin
{
    public class AIBusinessInsightsForm : Form
    {
        private WebView2 _webView;
        private Panel _titleBar;
        private Label _lblTitle;
        private Button _btnClose;
        private Panel _pnlWebView;

        public AIBusinessInsightsForm()
        {
            this.Size = new Size(840, 640);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = ThemeManager.CurrentBackground;

            SetDoubleBuffer(this);
            BuildUI();
            this.Shown += async (s, e) => await InitWebViewAsync();
        }

        private void BuildUI()
        {
            // Custom Title Bar
            _titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(18, 18, 34) : Color.FromArgb(240, 243, 250)
            };

            // Allow dragging
            Point lastClick = Point.Empty;
            _titleBar.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) lastClick = e.Location;
            };
            _titleBar.MouseMove += (s, e) =>
            {
                if (e.Button == MouseButtons.Left && lastClick != Point.Empty)
                {
                    this.Left += e.X - lastClick.X;
                    this.Top += e.Y - lastClick.Y;
                }
            };

            _lblTitle = new Label
            {
                Text = "🧠  AI BUSINESS OPERATIONS ADVISOR",
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentText,
                AutoSize = true,
                Location = new Point(20, 14),
                BackColor = Color.Transparent
            };

            _btnClose = new Button
            {
                Text = "✕",
                Size = new Size(36, 30),
                Location = new Point(this.Width - 46, 10),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentSubText,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 68, 68);
            _btnClose.Click += (s, e) => this.Close();

            _titleBar.Controls.Add(_lblTitle);
            _titleBar.Controls.Add(_btnClose);
            this.Controls.Add(_titleBar);

            // Container for WebView2
            _pnlWebView = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), BackColor = Color.Transparent };
            this.Controls.Add(_pnlWebView);
            _pnlWebView.BringToFront();

            // Outer border
            this.Paint += (s, e) =>
            {
                using var pen = new Pen(ThemeManager.CurrentPrimary, 1.5f);
                e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            };
        }

        private async Task InitWebViewAsync()
        {
            try
            {
                _webView = new WebView2 { Dock = DockStyle.Fill };
                _pnlWebView.Controls.Add(_webView);

                var env = await CoreWebView2Environment.CreateAsync(null, Path.GetTempPath());
                await _webView.EnsureCoreWebView2Async(env);

                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "AIBusinessInsights.html");
                if (File.Exists(htmlPath))
                {
                    string currentTheme = ThemeManager.IsDarkMode ? "dark" : "light";
                    await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                        $"document.documentElement.setAttribute('data-theme', '{currentTheme}');");

                    _webView.CoreWebView2.Navigate("file:///" + htmlPath.Replace('\\', '/'));

                    _webView.CoreWebView2.NavigationCompleted += async (sender, args) =>
                    {
                        if (this.IsDisposed || !this.IsHandleCreated) return;
                        try
                        {
                            await _webView.CoreWebView2.ExecuteScriptAsync(
                                $"if(window.setInsightsTheme) window.setInsightsTheme('{currentTheme}');" +
                                $"else document.documentElement.setAttribute('data-theme', '{currentTheme}');");

                            var res = await ApiService.GetAsync("admin/dashboard/ai-insights");
                            if (this.IsDisposed || !this.IsHandleCreated || _webView.IsDisposed || _webView.CoreWebView2 == null) return;

                            if (res.Success)
                            {
                                var root = JsonDocument.Parse(res.Body).RootElement;
                                string content = root.GetProperty("content").GetString();
                                string source = root.GetProperty("source").GetString();
                                double occupancy = root.TryGetProperty("occupancy", out var occ) ? occ.GetDouble() : 0;
                                decimal monthlyRevenue = root.TryGetProperty("monthlyRevenue", out var mr) ? mr.GetDecimal() : 0;
                                decimal totalRevenue = root.TryGetProperty("totalRevenue", out var tr) ? tr.GetDecimal() : 0;

                                _lblTitle.Text = $"🧠  AI BUSINESS OPERATIONS ADVISOR ({source.ToUpper()})";
                                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
                                string base64 = Convert.ToBase64String(bytes);
                                await _webView.CoreWebView2.ExecuteScriptAsync($"window.updateInsightsFromBase64('{base64}', false, {occupancy}, '{monthlyRevenue:N2}', '{totalRevenue:N2}', '{source}');");
                            }
                            else
                            {
                                byte[] bytes = System.Text.Encoding.UTF8.GetBytes("Failed to load AI insights. Live server returned error.");
                                string base64 = Convert.ToBase64String(bytes);
                                await _webView.CoreWebView2.ExecuteScriptAsync($"window.updateInsightsFromBase64('{base64}', false, 0, '0.00', '0.00', 'Error');");
                            }
                        }
                        catch (Exception ex)
                        {
                            if (this.IsDisposed || !this.IsHandleCreated || _webView.IsDisposed || _webView.CoreWebView2 == null) return;
                            byte[] bytes = System.Text.Encoding.UTF8.GetBytes($"Error: {ex.Message}");
                            string base64 = Convert.ToBase64String(bytes);
                            await _webView.CoreWebView2.ExecuteScriptAsync($"window.updateInsightsFromBase64('{base64}', false, 0, '0.00', '0.00', 'Error');");
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[AIBusinessInsightsForm] InitWebView error: " + ex.Message);
            }
        }

        private static void SetDoubleBuffer(Control c)
        {
            if (c == null) return;
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, c, new object[] { true });
        }
    }
}
