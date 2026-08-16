#nullable disable
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using DriveAndGo_Admin.Helpers;

namespace DriveAndGo_Admin.Panels
{
    public class WeatherPanel : UserControl
    {
        private WebView2 _webView;
        private Panel _headerPanel;
        private Label _lblTitle;
        private Label _lblSub;
        private Panel _presetBar;

        public WeatherPanel()
        {
            Dock = DockStyle.Fill;
            DoubleBuffered = true;
            BackColor = ThemeManager.CurrentBackground;

            ThemeManager.ThemeChanged += (s, e) => ApplyTheme();

            BuildHeader();
            BuildPresetBar();
            InitWebView();
        }

        private void BuildHeader()
        {
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = ThemeManager.CurrentCard,
                Padding = new Padding(20, 10, 20, 10)
            };

            _lblTitle = new Label
            {
                Text = "⚡ Live Weather & Wind Radar (Philippines)",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentText,
                AutoSize = true,
                Location = new Point(18, 10)
            };

            _lblSub = new Label
            {
                Text = "Real-time wind particle streams, typhoon tracking, rain radar & fleet dispatch weather advisories",
                Font = new Font("Segoe UI", 9F),
                ForeColor = ThemeManager.CurrentSubText,
                AutoSize = true,
                Location = new Point(19, 36)
            };

            _headerPanel.Controls.Add(_lblTitle);
            _headerPanel.Controls.Add(_lblSub);
            Controls.Add(_headerPanel);
        }

        private void BuildPresetBar()
        {
            _presetBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(15, 20, 32) : Color.FromArgb(240, 243, 248),
                Padding = new Padding(16, 6, 16, 6)
            };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent
            };

            var btnWind = CreateFilterButton("🌀 Wind Streamlines", "wind", true);
            var btnRain = CreateFilterButton("🌧️ Rain & Thunder", "rain", false);
            var btnTemp = CreateFilterButton("🌡️ Temperature", "temp", false);
            var btnWaves = CreateFilterButton("🌊 Waves & Swell", "waves", false);
            var btnTyphoon = CreateFilterButton("🌀 Typhoon Tracker", "gust", false);

            var btnManila = CreateLocationButton("📍 Manila (Luzon)", 14.5995, 120.9842, 8);
            var btnCebu = CreateLocationButton("📍 Cebu (Visayas)", 10.3157, 123.8854, 8);
            var btnDavao = CreateLocationButton("📍 Davao (Mindanao)", 7.1907, 125.4553, 8);
            var btnPHAll = CreateLocationButton("🇵🇭 Entire Philippines", 12.8797, 121.7740, 6);

            flow.Controls.Add(btnWind);
            flow.Controls.Add(btnRain);
            flow.Controls.Add(btnTemp);
            flow.Controls.Add(btnWaves);
            flow.Controls.Add(btnTyphoon);
            flow.Controls.Add(new Label { Width = 20, Height = 10 }); // Spacer
            flow.Controls.Add(btnPHAll);
            flow.Controls.Add(btnManila);
            flow.Controls.Add(btnCebu);
            flow.Controls.Add(btnDavao);

            _presetBar.Controls.Add(flow);
            Controls.Add(_presetBar);
        }

        private Button CreateFilterButton(string text, string overlay, bool isActive)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 0),
                BackColor = isActive ? ThemeManager.CurrentPrimary : Color.Transparent,
                ForeColor = isActive ? Color.White : ThemeManager.CurrentText
            };
            btn.FlatAppearance.BorderSize = isActive ? 0 : 1;
            btn.FlatAppearance.BorderColor = ThemeManager.CurrentBorder;

            btn.Click += async (s, e) =>
            {
                foreach (Control c in btn.Parent.Controls)
                {
                    if (c is Button b && b.Tag?.ToString() == "overlay")
                    {
                        b.BackColor = Color.Transparent;
                        b.ForeColor = ThemeManager.CurrentText;
                        b.FlatAppearance.BorderSize = 1;
                    }
                }
                btn.BackColor = ThemeManager.CurrentPrimary;
                btn.ForeColor = Color.White;
                btn.FlatAppearance.BorderSize = 0;

                if (_webView?.CoreWebView2 != null)
                {
                    await _webView.CoreWebView2.ExecuteScriptAsync($"switchOverlay('{overlay}');");
                }
            };
            btn.Tag = "overlay";
            return btn;
        }

        private Button CreateLocationButton(string text, double lat, double lon, int zoom)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5F),
                Margin = new Padding(0, 0, 8, 0),
                BackColor = Color.Transparent,
                ForeColor = ThemeManager.CurrentSubText
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = ThemeManager.CurrentBorder;

            btn.Click += async (s, e) =>
            {
                if (_webView?.CoreWebView2 != null)
                {
                    await _webView.CoreWebView2.ExecuteScriptAsync($"setLocation({lat}, {lon}, {zoom});");
                }
            };
            return btn;
        }

        private async void InitWebView()
        {
            _webView = new WebView2 { Dock = DockStyle.Fill };
            Controls.Add(_webView);
            _webView.BringToFront();

            try
            {
                var env = await CoreWebView2Environment.CreateAsync(null, Path.GetTempPath());
                await _webView.EnsureCoreWebView2Async(env);

                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                string html = GetWeatherHtml();
                _webView.CoreWebView2.NavigateToString(html);
            }
            catch (Exception ex)
            {
                MessageBox.Show("WebView2 error initializing Weather Radar: " + ex.Message);
            }
        }

        private void ApplyTheme()
        {
            BackColor = ThemeManager.CurrentBackground;
            if (_headerPanel != null) _headerPanel.BackColor = ThemeManager.CurrentCard;
            if (_lblTitle != null) _lblTitle.ForeColor = ThemeManager.CurrentText;
            if (_lblSub != null) _lblSub.ForeColor = ThemeManager.CurrentSubText;
            if (_presetBar != null) _presetBar.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(15, 20, 32) : Color.FromArgb(240, 243, 248);
        }

        private string GetWeatherHtml()
        {
            return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0' />
    <title>DriveAndGo Live Weather & Wind Radar</title>
    <style>
        html, body {
            margin: 0;
            padding: 0;
            width: 100%;
            height: 100%;
            overflow: hidden;
            background-color: #07070e;
            font-family: 'Segoe UI', system-ui, sans-serif;
        }
        #windyFrame {
            width: 100%;
            height: 100%;
            border: none;
        }
        .advisory-badge {
            position: absolute;
            bottom: 20px;
            left: 20px;
            z-index: 1000;
            background: rgba(11, 16, 30, 0.88);
            backdrop-filter: blur(12px);
            border: 1px solid rgba(249, 115, 22, 0.3);
            border-radius: 12px;
            padding: 12px 18px;
            color: #fff;
            box-shadow: 0 10px 30px rgba(0,0,0,0.5);
            max-width: 320px;
        }
        .advisory-title {
            font-size: 11px;
            font-weight: 800;
            color: #f97316;
            text-transform: uppercase;
            letter-spacing: 0.08em;
            margin-bottom: 4px;
            display: flex;
            align-items: center;
            gap: 6px;
        }
        .advisory-text {
            font-size: 11px;
            color: rgba(255,255,255,0.75);
            line-height: 1.4;
        }
    </style>
</head>
<body>
    <iframe id='windyFrame' 
        src='https://embed.windy.com/embed2.html?lat=12.8797&lon=121.7740&detailLat=14.5995&detailLon=120.9842&width=650&height=450&zoom=6&level=surface&overlay=wind&product=ecmwf&menu=&message=true&marker=&calendar=now&pressure=&type=map&location=coordinates&detail=&metricWind=kt&metricTemp=%C2%B0C&radarRange=-1'
        allowfullscreen>
    </iframe>

    <div class='advisory-badge'>
        <div class='advisory-title'>
            <span>🌀 Live Fleet Dispatch Advisory</span>
        </div>
        <div class='advisory-text'>
            Real-time particle wind vectors active over Philippines airspace. Check wind gusts (>25 kt) before dispatching high-profile passenger vans or rental SUVs on highway routes.
        </div>
    </div>

    <script>
        function switchOverlay(overlayName) {
            const frame = document.getElementById('windyFrame');
            let currentUrl = frame.src;
            // Replace overlay parameter
            let newUrl = currentUrl.replace(/overlay=[a-z]+/, 'overlay=' + overlayName);
            frame.src = newUrl;
        }

        function setLocation(lat, lon, zoom) {
            const frame = document.getElementById('windyFrame');
            let currentUrl = frame.src;
            let newUrl = currentUrl.replace(/lat=[0-9.-]+/, 'lat=' + lat)
                                   .replace(/lon=[0-9.-]+/, 'lon=' + lon)
                                   .replace(/zoom=[0-9]+/, 'zoom=' + zoom);
            frame.src = newUrl;
        }
    </script>
</body>
</html>";
        }
    }
}
