#nullable disable
using DriveAndGo_Admin.Helpers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DriveAndGo_Admin.Panels
{
    public class AccountsPanel : UserControl
    {
        private WebView2 _webView;
        private Panel    _loadingPanel;
        private Label    _loadingLabel;
        private string   _tempHtmlPath;

        public AccountsPanel()
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
                this.BeginInvoke((MethodInvoker)(() => LoadAccountsHtml()));
            }
        }

        private void BuildLoading()
        {
            _loadingPanel = new Panel { Dock = DockStyle.Fill, BackColor = ThemeManager.CurrentBackground };
            _loadingLabel = new Label
            {
                Text = "Loading Accounts Dashboard…",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(234, 88, 12),
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
            
            _loadingPanel.Visible = false;
            LoadAccountsHtml();
        }

        private void LoadAccountsHtml()
        {
            if (_webView?.CoreWebView2 == null) return;
            
            if (_tempHtmlPath == null)
                _tempHtmlPath = Path.Combine(Path.GetTempPath(), "driveandgo_accounts.html");

            bool dark = ThemeManager.IsDarkMode;
            
            // Get API Base URL from the client's centralised service
            string apiBaseUrl = ApiService.BaseUrl;

            var html = AccountsHtmlGenerator.Build(apiBaseUrl, dark);
            File.WriteAllText(_tempHtmlPath, html, System.Text.Encoding.UTF8);
            _webView.CoreWebView2.Navigate(new Uri(_tempHtmlPath).AbsoluteUri);
        }
    }
}
