using DriveAndGo_Admin.Helpers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DriveAndGo_Admin
{
    public class ReplayMapForm : Form
    {
        private readonly int _rentalId;
        private WebView2 _webView;
        private Panel _loadingPanel;
        private Label _loadingLabel;
        private string _htmlPath;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        public ReplayMapForm(int rentalId)
        {
            _rentalId = rentalId;
            this.Size = new Size(950, 650);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(11, 11, 22);

            // Double Buffering
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            this.UpdateStyles();

            BuildUI();
            this.HandleCreated += async (s, e) => await InitWebView();
        }

        private void BuildUI()
        {
            // Custom Title Bar
            var titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(8, 8, 16)
            };
            titleBar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawLine(new Pen(Color.FromArgb(255, 90, 31), 1), 0, titleBar.Height - 1, titleBar.Width, titleBar.Height - 1);
            };

            var lblTitle = new Label
            {
                Text = $"GPS ROUTE REPLAY  —  RENTAL #{_rentalId}",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 14),
                BackColor = Color.Transparent
            };

            var btnClose = new Button
            {
                Text = "X",
                Size = new Size(36, 30),
                Location = new Point(this.Width - 50, 10),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 200, 200),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 68, 68);
            btnClose.Click += (s, e) => this.Close();

            titleBar.Controls.Add(lblTitle);
            titleBar.Controls.Add(btnClose);
            this.Controls.Add(titleBar);

            // Loading screen while fetching routes
            _loadingPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(11, 11, 22)
            };
            _loadingLabel = new Label
            {
                Text = "Retrieving GPS coordinates...",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 90, 31),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            _loadingPanel.Controls.Add(_loadingLabel);
            _loadingPanel.Resize += (s, e) =>
            {
                _loadingLabel.Location = new Point(
                    (_loadingPanel.Width - _loadingLabel.Width) / 2,
                    (_loadingPanel.Height - _loadingLabel.Height) / 2);
            };
            this.Controls.Add(_loadingPanel);
            _loadingPanel.BringToFront();

            // Form border painting
            this.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(255, 90, 31), 2);
                e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            };
        }

        private async Task InitWebView()
        {
            try
            {
                _webView = new WebView2 { Dock = DockStyle.Fill };
                this.Controls.Add(_webView);
                _webView.BringToFront();

                var env = await CoreWebView2Environment.CreateAsync(null, Path.GetTempPath());
                await _webView.EnsureCoreWebView2Async(env);

                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                _htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "ReplayMap.html");
                
                if (File.Exists(_htmlPath))
                {
                    _webView.CoreWebView2.Navigate("file:///" + _htmlPath.Replace('\\', '/'));
                    _webView.CoreWebView2.NavigationCompleted += async (s, e) =>
                    {
                        await LoadHistoryData();
                    };
                }
                else
                {
                    _loadingLabel.Text = "Replay file missing in WebAssets.";
                }
            }
            catch (Exception ex)
            {
                _loadingLabel.Text = "WebView2 failed to initialize: " + ex.Message;
            }
        }

        private async Task LoadHistoryData()
        {
            try
            {
                var result = await ApiService.GetAsync($"locations/history/{_rentalId}");
                if (result.Success && !string.IsNullOrWhiteSpace(result.Body) && result.Body != "[]")
                {
                    // Escape single quotes for JS injection
                    string escapedJson = result.Body.Replace("'", "\\'");
                    
                    // We must delay slightly to ensure DOM script binds
                    await Task.Delay(500);

                    this.Invoke((MethodInvoker)(async () =>
                    {
                        await _webView.CoreWebView2.ExecuteScriptAsync($"window.setData('{escapedJson}');");
                        _loadingPanel.Visible = false;
                    }));
                }
                else
                {
                    this.Invoke((MethodInvoker)(() =>
                    {
                        _loadingLabel.Text = "No GPS logs recorded for this active/completed rental.";
                    }));
                }
            }
            catch (Exception ex)
            {
                this.Invoke((MethodInvoker)(() =>
                {
                    _loadingLabel.Text = "Error fetching logs: " + ex.Message;
                }));
            }
        }
    }
}
