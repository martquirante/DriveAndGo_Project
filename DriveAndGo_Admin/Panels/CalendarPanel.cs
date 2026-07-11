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
    public class CalendarPanel : UserControl
    {
        private WebView2 _webView;
        private Panel    _loadingPanel;
        private Label    _loadingLabel;
        private int      _currentYear  = DateTime.Now.Year;
        private int      _currentMonth = DateTime.Now.Month;
        private string   _currentView  = "month";
        private string   _tempHtmlPath;

        public CalendarPanel()
        {
            this.Dock      = DockStyle.Fill;
            this.BackColor = Color.Transparent;
            BuildLoading();
            this.HandleCreated += async (s, e) => await InitWebView();
            ThemeManager.ThemeChanged += ThemeChanged_Handler;
            this.Disposed += (s, e) => ThemeManager.ThemeChanged -= ThemeChanged_Handler;
        }

        private void ThemeChanged_Handler(object sender, EventArgs e)
        {
            if (this.IsHandleCreated && !this.IsDisposed)
            {
                // Re-render immediately on theme change to sync colors
                this.BeginInvoke((MethodInvoker)(() => _ = LoadCalendar()));
            }
        }

        private void BuildLoading()
        {
            _loadingPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(15, 23, 42) };
            _loadingLabel = new Label
            {
                Text = "Loading Calendar\u2026",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(99, 179, 237),
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
            _webView = new WebView2 { Dock = DockStyle.Fill };
            this.Controls.Add(_webView);
            _webView.BringToFront();
            var env = await CoreWebView2Environment.CreateAsync(null, Path.GetTempPath());
            await _webView.EnsureCoreWebView2Async(env);
            _webView.CoreWebView2.WebMessageReceived += OnWebMessage;
            _loadingPanel.Visible = false;
            await LoadCalendar();
        }

        private async Task LoadCalendar()
        {
            if (_webView?.CoreWebView2 == null) return;
            try
            {
                var result = await ApiService.GetAsync(
                    string.Concat("rentals/calendar?year=", _currentYear, "&month=", _currentMonth));
                var eventsJson = result.Success ? result.Body : "[]";
                WriteAndNavigate(eventsJson);
            }
            catch
            {
                WriteAndNavigate("[]");
            }
        }

        // Write HTML to a temp file and navigate — avoids ALL C# string-escaping
        private void WriteAndNavigate(string eventsJson)
        {
            if (_tempHtmlPath == null)
                _tempHtmlPath = Path.Combine(Path.GetTempPath(), "driveandgo_calendar.html");

            bool dark = ThemeManager.IsDarkMode;
            var html = CalendarHtmlGenerator.Build(
                _currentYear, _currentMonth, eventsJson, _currentView, dark);
            File.WriteAllText(_tempHtmlPath, html, System.Text.Encoding.UTF8);
            _webView.CoreWebView2.Navigate(new Uri(_tempHtmlPath).AbsoluteUri);
        }

        private void OnWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                var root = doc.RootElement;
                if (!root.TryGetProperty("action", out var act)) return;
                string action = act.GetString();
                if (action == "navigate")
                {
                    _currentYear  = root.GetProperty("year").GetInt32();
                    _currentMonth = root.GetProperty("month").GetInt32();
                    _ = LoadCalendar();
                }
                else if (action == "viewChanged")
                {
                    _currentView = root.GetProperty("view").GetString();
                }
            }
            catch { }
        }
    }
}
