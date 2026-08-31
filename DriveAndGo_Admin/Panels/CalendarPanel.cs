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
            _ = InitWebView();
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
            _loadingPanel = new Panel { Dock = DockStyle.Fill, BackColor = ThemeManager.CurrentBackground };
            _loadingLabel = new Label
            {
                Text = "Loading Calendar…",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(99, 179, 237),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            _loadingPanel.Controls.Add(_loadingLabel);
            _loadingPanel.Resize += (s, e) =>
                _loadingLabel.Location = new Point(
                    Math.Max(10, (_loadingPanel.Width - _loadingLabel.Width) / 2),
                    Math.Max(10, (_loadingPanel.Height - _loadingLabel.Height) / 2));
            this.Controls.Add(_loadingPanel);
        }

        private async Task InitWebView()
        {
            if (_webView != null) return;
            _webView = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.Transparent };
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
            string apiBase = ApiService.ResolveNetworkBaseUrl();
            var html = CalendarHtmlGenerator.Build(
                _currentYear, _currentMonth, eventsJson, _currentView, dark, apiBase);
            File.WriteAllText(_tempHtmlPath, html, System.Text.Encoding.UTF8);
            _webView.CoreWebView2.Navigate(new Uri(_tempHtmlPath).AbsoluteUri);
            if (_loadingPanel != null)
            {
                if (this.Controls.Contains(_loadingPanel))
                {
                    this.Controls.Remove(_loadingPanel);
                }
                _loadingPanel.Dispose();
                _loadingPanel = null;
            }
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
                    bool clientHandled = root.TryGetProperty("clientHandled", out var ch) && ch.GetBoolean();
                    if (!clientHandled)
                    {
                        _ = LoadCalendar();
                    }
                }
                else if (action == "viewChanged")
                {
                    _currentView = root.GetProperty("view").GetString();
                }
                else if (action == "saveNote")
                {
                    var noteDate = root.GetProperty("noteDate").GetString();
                    var title = root.GetProperty("title").GetString();
                    var content = root.TryGetProperty("content", out var cp) ? cp.GetString() : "";
                    var category = root.TryGetProperty("category", out var catp) ? catp.GetString() : "reminder";
                    var payload = new {
                        noteDate,
                        title,
                        content,
                        category,
                        createdBy = !string.IsNullOrWhiteSpace(SessionManager.FullName) ? SessionManager.FullName : "Admin"
                    };
                    _ = Task.Run(async () => {
                        await ApiService.PostAsync("rentals/calendar/notes", payload);
                        if (this.IsHandleCreated && !this.IsDisposed)
                        {
                            this.BeginInvoke((MethodInvoker)(() => _ = LoadCalendar()));
                        }
                    });
                }
                else if (action == "deleteNote")
                {
                    int noteId = root.GetProperty("noteId").GetInt32();
                    _ = Task.Run(async () => {
                        await ApiService.DeleteAsync($"rentals/calendar/notes/{noteId}");
                        if (this.IsHandleCreated && !this.IsDisposed)
                        {
                            this.BeginInvoke((MethodInvoker)(() => _ = LoadCalendar()));
                        }
                    });
                }
            }
            catch { }
        }
    }
}
