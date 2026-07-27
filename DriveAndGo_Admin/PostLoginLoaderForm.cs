#nullable disable
using DriveAndGo_Admin.Helpers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DriveAndGo_Admin
{
    /// <summary>
    /// Re-engineered PostLoginLoaderForm with Dynamic ThemeManager Light/Dark mode integration.
    /// A premium, frameless, Rectilinear floating widget centered on the Windows Desktop.
    /// Adapts card background, text colors, and borders to the theme selected during login.
    /// </summary>
    public class PostLoginLoaderForm : Form
    {
        // ── Controls ────────────────────────────────────────────────────────────
        private WebView2 _webView;
        private Panel _cardPanel;
        private Label _lblTitle;
        private Label _lblSub;
        private Label _lblSequence;
        private Label _lblPhase;
        private Label _lblPercentage;
        private Panel _pnlVideoContainer;

        // ── Logs Panel & Rows ───────────────────────────────────────────────────
        private Panel _pnlLogs;
        private Label _lblLog1;
        private Label _lblLog2;
        private Label _lblLog3;
        private Panel _dot1;
        private Panel _dot2;
        private Panel _dot3;

        // ── Timing & State ──────────────────────────────────────────────────────
        private System.Windows.Forms.Timer _animationTimer;
        private Stopwatch _stopwatch;
        private float _progress = 0f;
        private bool _completed = false;
        private float _formOpacity = 0f;
        private bool _isFadingOut = false;

        // ── Layout Dimensions ──────────────────────────────────────────────────
        private const int CARD_WIDTH = 480;
        private const int CARD_HEIGHT = 560;
        private const int WINDOW_PADDING = 15; // Padding for the glow shadow
        private const int DURATION_MS = 4500; // 4.5 Seconds

        public PostLoginLoaderForm()
        {
            SetDoubleBuffer(this);
            this.SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint, true);
            this.UpdateStyles();

            BuildForm();
            BuildCardLayout();
            InitWebView();
            StartLoadingSequence();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  FORM SETUP
        // ════════════════════════════════════════════════════════════════════════
        private void BuildForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(CARD_WIDTH + (WINDOW_PADDING * 2), CARD_HEIGHT + (WINDOW_PADDING * 2));
            this.TopMost = true;
            this.Opacity = 0;
            this.Text = "Drive & Go — System Handshake";

            // Make the form background transparent to show only the floating card
            this.BackColor = Color.Magenta;
            this.TransparencyKey = Color.Magenta;

            // Set app icon
            IconHelper.ApplyToForm(this);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  FLOATING CARD LAYOUT
        // ════════════════════════════════════════════════════════════════════════
        private void BuildCardLayout()
        {
            // Main Rectangular Card Container
            _cardPanel = new Panel
            {
                Size = new Size(CARD_WIDTH, CARD_HEIGHT),
                Location = new Point(WINDOW_PADDING, WINDOW_PADDING),
                BackColor = ThemeManager.CurrentBackground,
                Cursor = Cursors.Default
            };
            SetDoubleBuffer(_cardPanel);
            _cardPanel.Paint += OnCardPaint;
            this.Controls.Add(_cardPanel);

            // Header - Subtitle Brand
            _lblSub = new Label
            {
                Text = "DRIVEANDGO",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentSubText,
                AutoSize = true,
                Location = new Point(30, 25),
                BackColor = Color.Transparent
            };
            _cardPanel.Controls.Add(_lblSub);

            // Header - Title
            _lblTitle = new Label
            {
                Text = "System Initializing",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentText,
                Size = new Size(290, 36),
                Location = new Point(26, 42),
                BackColor = Color.Transparent
            };
            _cardPanel.Controls.Add(_lblTitle);

            // Sequence Badge Panel
            var pnlBadge = new Panel
            {
                Size = new Size(110, 22),
                Location = new Point(CARD_WIDTH - 140, 32),
                BackColor = Color.FromArgb(26, ThemeManager.CurrentPrimary)
            };
            pnlBadge.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, pnlBadge.Width - 1, pnlBadge.Height - 1);
                using var path = CreateRoundedRectanglePath(r, 4);
                using var pen = new Pen(Color.FromArgb(51, ThemeManager.CurrentPrimary), 1f);
                g.DrawPath(pen, path);
            };

            _lblSequence = new Label
            {
                Text = "SEQUENCE: 07-X",
                Font = new Font("Consolas", 8F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentPrimary,
                AutoSize = false,
                Size = pnlBadge.Size,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            pnlBadge.Controls.Add(_lblSequence);
            _cardPanel.Controls.Add(pnlBadge);

            // Rectangular Video Frame Region
            _pnlVideoContainer = new Panel
            {
                Size = new Size(420, 200),
                Location = new Point(30, 95),
                BackColor = Color.Transparent
            };
            SetDoubleBuffer(_pnlVideoContainer);
            _pnlVideoContainer.Paint += OnVideoFramePaint;
            _cardPanel.Controls.Add(_pnlVideoContainer);

            // Progress labels
            _lblPhase = new Label
            {
                Text = "TRANSIT PHASE",
                Font = new Font("Consolas", 9F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentSubText,
                Location = new Point(30, 315),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            _cardPanel.Controls.Add(_lblPhase);

            _lblPercentage = new Label
            {
                Text = "0%",
                Font = new Font("Consolas", 9.5F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentPrimary,
                Location = new Point(CARD_WIDTH - 80, 315),
                AutoSize = true,
                TextAlign = ContentAlignment.TopRight,
                BackColor = Color.Transparent
            };
            _cardPanel.Controls.Add(_lblPercentage);

            // Terminal Logs Panel Container
            _pnlLogs = new Panel
            {
                Size = new Size(420, 125),
                Location = new Point(30, 360),
                BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(12, 5, 11, 20) : Color.FromArgb(240, 243, 250)
            };
            SetDoubleBuffer(_pnlLogs);
            _pnlLogs.Paint += OnLogsPanelPaint;

            // Log lines & dot indicators
            BuildLogLines();
            _cardPanel.Controls.Add(_pnlLogs);

            // Footer Section
            var lblFooter = new Label
            {
                Text = "SECURED BY OBSIDIAN-9",
                Font = new Font("Consolas", 8F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentSubText,
                Location = new Point(CARD_WIDTH - 170, 505),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            _cardPanel.Controls.Add(lblFooter);
        }

        private void BuildLogLines()
        {
            // Dots
            _dot1 = CreateLogDot(20);
            _dot2 = CreateLogDot(55);
            _dot3 = CreateLogDot(90);
            _pnlLogs.Controls.Add(_dot1);
            _pnlLogs.Controls.Add(_dot2);
            _pnlLogs.Controls.Add(_dot3);

            // Log Texts
            _lblLog1 = CreateLogLabel("Verifying encrypted garage handshake...", 18);
            _lblLog2 = CreateLogLabel("VERIFYING DISTRIBUTED ACCESS KEY...", 53);
            _lblLog3 = CreateLogLabel("Allocating redundant neural core...", 88);
            _pnlLogs.Controls.Add(_lblLog1);
            _pnlLogs.Controls.Add(_lblLog2);
            _pnlLogs.Controls.Add(_lblLog3);
        }

        private Panel CreateLogDot(int y)
        {
            var dot = new Panel
            {
                Size = new Size(16, 16),
                Location = new Point(15, y),
                BackColor = Color.Transparent
            };
            SetDoubleBuffer(dot);
            return dot;
        }

        private Label CreateLogLabel(string text, int y)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Consolas", 8.5F, FontStyle.Regular),
                ForeColor = ThemeManager.CurrentSubText,
                Location = new Point(40, y),
                Size = new Size(365, 20),
                BackColor = Color.Transparent
            };
        }

        // ════════════════════════════════════════════════════════════════════════
        //  WEBVIEW2 — Autoplay cinematic video playback
        // ════════════════════════════════════════════════════════════════════════
        private async void InitWebView()
        {
            try
            {
                _webView = new WebView2
                {
                    Size = new Size(420, 200),
                    Location = new Point(0, 0),
                    DefaultBackgroundColor = ThemeManager.CurrentBackground
                };
                _pnlVideoContainer.Controls.Add(_webView);

                await _webView.EnsureCoreWebView2Async();

                // Lock down browser settings
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

                // Pass active theme to WebView2
                string currentTheme = ThemeManager.IsDarkMode ? "dark" : "light";
                await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                    $"document.documentElement.setAttribute('data-theme', '{currentTheme}');");

                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "loading_video.html");
                if (File.Exists(htmlPath))
                {
                    _webView.CoreWebView2.Navigate("file:///" + htmlPath.Replace('\\', '/'));
                }
                else
                {
                    _webView.Visible = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebView2 failed to load: {ex.Message}");
                try { _webView?.Dispose(); } catch { }
                _webView = null;
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  LOADING SEQUENCE LOOP
        // ════════════════════════════════════════════════════════════════════════
        private void StartLoadingSequence()
        {
            _stopwatch = new Stopwatch();
            _stopwatch.Start();

            FadeIn();

            _animationTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animationTimer.Tick += OnSequenceTick;
            _animationTimer.Start();
        }

        private void FadeIn()
        {
            var fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
            fadeTimer.Tick += (s, e) =>
            {
                _formOpacity += 0.08f;
                this.Opacity = Math.Min(_formOpacity, 1.0);
                if (_formOpacity >= 1.0)
                {
                    fadeTimer.Stop();
                    fadeTimer.Dispose();
                }
            };
            fadeTimer.Start();
        }

        private async void OnSequenceTick(object sender, EventArgs e)
        {
            if (_completed || _isFadingOut) return;

            long elapsed = _stopwatch.ElapsedMilliseconds;
            _progress = Math.Min(100f, ((float)elapsed / DURATION_MS) * 100f);

            _cardPanel.Invalidate();
            UpdateStateLogic();

            if (_progress >= 100f)
            {
                _completed = true;
                _stopwatch.Stop();
                _animationTimer.Stop();

                TriggerCompletedState();
                await Task.Delay(1200);
                FadeOutAndRoute();
            }
        }

        private void UpdateStateLogic()
        {
            _lblPercentage.Text = $"{(int)Math.Round(_progress)}%";

            if (_progress < 50f)
            {
                _lblPhase.Text = "TRANSIT PHASE";
            }
            else if (_progress < 100f)
            {
                _lblPhase.Text = "PARKING PHASE";
            }

            UpdateLogRows();
        }

        private void UpdateLogRows()
        {
            if (_completed) return;

            if (_progress < 33.3f)
            {
                SetRowState(1, isActive: true);
                SetRowState(2, isActive: false);
                SetRowState(3, isActive: false);
            }
            else if (_progress < 66.6f)
            {
                SetRowState(1, isActive: false);
                SetRowState(2, isActive: true);
                SetRowState(3, isActive: false);
            }
            else
            {
                SetRowState(1, isActive: false);
                SetRowState(2, isActive: false);
                SetRowState(3, isActive: true);
            }
        }

        private void SetRowState(int rowIndex, bool isActive)
        {
            Label targetLabel = rowIndex switch
            {
                1 => _lblLog1,
                2 => _lblLog2,
                3 => _lblLog3,
                _ => null
            };
            Panel targetDot = rowIndex switch
            {
                1 => _dot1,
                2 => _dot2,
                3 => _dot3,
                _ => null
            };

            if (targetLabel == null || targetDot == null) return;

            if (isActive)
            {
                targetLabel.ForeColor = ThemeManager.CurrentText;
                targetLabel.Font = new Font("Consolas", 8.5F, FontStyle.Bold);
                targetDot.Invalidate();
            }
            else
            {
                targetLabel.ForeColor = ThemeManager.CurrentSubText;
                targetLabel.Font = new Font("Consolas", 8.5F, FontStyle.Regular);
                targetDot.Invalidate();
            }
        }

        private void TriggerCompletedState()
        {
            _lblTitle.Text = "Loading complete, welcome back!";
            _lblTitle.ForeColor = ThemeManager.CurrentPrimary;
            _lblTitle.Font = new Font("Segoe UI", 15F, FontStyle.Bold);

            _lblPhase.Text = "DOCKING COMPLETE";
            _lblPercentage.Text = "100%";

            _lblLog2.Text = "DOCKING SECURE. DRIVER WELCOME.";
            SetRowState(1, isActive: false);
            SetRowState(2, isActive: true);
            SetRowState(3, isActive: false);

            _pnlLogs.Invalidate();
            _cardPanel.Invalidate();
        }

        private void FadeOutAndRoute()
        {
            _isFadingOut = true;
            var fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
            fadeTimer.Tick += (s, e) =>
            {
                _formOpacity -= 0.08f;
                this.Opacity = Math.Max(_formOpacity, 0f);
                if (_formOpacity <= 0f)
                {
                    fadeTimer.Stop();
                    fadeTimer.Dispose();
                    
                    this.Hide();

                    var mainForm = new MainForm();
                    mainForm.Show();

                    this.Dispose();
                }
            };
            fadeTimer.Start();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  CUSTOM GDI+ PAINT EVENTS
        // ════════════════════════════════════════════════════════════════════════
        private void OnCardPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, _cardPanel.Width - 1, _cardPanel.Height - 1);
            using var path = CreateRoundedRectanglePath(rect, 10);
            
            // Theme background fill
            using var cardBrush = new SolidBrush(ThemeManager.CurrentBackground);
            g.FillPath(cardBrush, path);

            // Border
            using var borderPen = new Pen(ThemeManager.CurrentBorder, 1f);
            g.DrawPath(borderPen, path);

            // Top accent line
            using var accentPen = new Pen(Color.FromArgb(38, ThemeManager.CurrentPrimary), 2f);
            g.DrawLine(accentPen, 30, 0, _cardPanel.Width - 30, 0);

            // HUD corner ticks on Card corners
            using var hudPen = new Pen(ThemeManager.CurrentSubText, 1f);
            g.DrawLine(hudPen, 8, 8, 16, 8);
            g.DrawLine(hudPen, 8, 8, 8, 16);

            g.DrawLine(hudPen, _cardPanel.Width - 8, 8, _cardPanel.Width - 16, 8);
            g.DrawLine(hudPen, _cardPanel.Width - 8, 8, _cardPanel.Width - 8, 16);

            g.DrawLine(hudPen, 8, _cardPanel.Height - 8, 16, _cardPanel.Height - 8);
            g.DrawLine(hudPen, 8, _cardPanel.Height - 8, 8, _cardPanel.Height - 16);

            g.DrawLine(hudPen, _cardPanel.Width - 8, _cardPanel.Height - 8, _cardPanel.Width - 16, _cardPanel.Height - 8);
            g.DrawLine(hudPen, _cardPanel.Width - 8, _cardPanel.Height - 8, _cardPanel.Width - 8, _cardPanel.Height - 16);

            // Progress Bar Track & Fill
            int pbX = 30;
            int pbY = 338;
            int pbW = CARD_WIDTH - 60;
            int pbH = 6;

            Color trackBg = ThemeManager.IsDarkMode ? Color.FromArgb(10, 5, 11, 20) : Color.FromArgb(220, 225, 235);
            using var trackBrush = new SolidBrush(trackBg);
            using var trackPen = new Pen(ThemeManager.CurrentBorder, 1f);
            var trackRect = new Rectangle(pbX, pbY, pbW, pbH);
            using var trackPath = CreateRoundedRectanglePath(trackRect, pbH / 2);
            g.FillPath(trackBrush, trackPath);
            g.DrawPath(trackPen, trackPath);

            int fillW = (int)((_progress / 100f) * pbW);
            if (fillW > 2)
            {
                var fillRect = new Rectangle(pbX, pbY, fillW, pbH);
                using var fillPath = CreateRoundedRectanglePath(fillRect, pbH / 2);
                using var fillBrush = new SolidBrush(ThemeManager.CurrentPrimary);
                g.FillPath(fillBrush, fillPath);
            }
        }

        private void OnVideoFramePaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = new Rectangle(0, 0, _pnlVideoContainer.Width - 1, _pnlVideoContainer.Height - 1);
            using var path = CreateRoundedRectanglePath(r, 8);
            using var borderPen = new Pen(ThemeManager.CurrentBorder, 1f);
            g.DrawPath(borderPen, path);

            // HUD corner ticks on Video border
            using var cornerPen = new Pen(ThemeManager.CurrentPrimary, 1.5f);
            int offset = 0;
            int len = 8;

            g.DrawLine(cornerPen, offset, offset, offset + len, offset);
            g.DrawLine(cornerPen, offset, offset, offset, offset + len);

            g.DrawLine(cornerPen, _pnlVideoContainer.Width - offset - 1, offset, _pnlVideoContainer.Width - offset - 1 - len, offset);
            g.DrawLine(cornerPen, _pnlVideoContainer.Width - offset - 1, offset, _pnlVideoContainer.Width - offset - 1, offset + len);

            g.DrawLine(cornerPen, offset, _pnlVideoContainer.Height - offset - 1, offset + len, _pnlVideoContainer.Height - offset - 1);
            g.DrawLine(cornerPen, offset, _pnlVideoContainer.Height - offset - 1, offset, _pnlVideoContainer.Height - offset - 1 - len);

            g.DrawLine(cornerPen, _pnlVideoContainer.Width - offset - 1, _pnlVideoContainer.Height - offset - 1, _pnlVideoContainer.Width - offset - 1 - len, _pnlVideoContainer.Height - offset - 1);
            g.DrawLine(cornerPen, _pnlVideoContainer.Width - offset - 1, _pnlVideoContainer.Height - offset - 1, _pnlVideoContainer.Width - offset - 1, _pnlVideoContainer.Height - offset - 1 - len);
        }

        private void OnLogsPanelPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = new Rectangle(0, 0, _pnlLogs.Width - 1, _pnlLogs.Height - 1);
            using var path = CreateRoundedRectanglePath(r, 10);
            
            Color logsBg = ThemeManager.IsDarkMode ? Color.FromArgb(12, 5, 11, 20) : Color.FromArgb(240, 243, 250);
            using var fill = new SolidBrush(logsBg);
            using var pen = new Pen(ThemeManager.CurrentBorder, 1f);

            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var cardBounds = new Rectangle(WINDOW_PADDING - 1, WINDOW_PADDING - 1, CARD_WIDTH + 2, CARD_HEIGHT + 2);
            using var shadowPath = CreateRoundedRectanglePath(cardBounds, 11);
            
            using var neonPen = new Pen(ThemeManager.CurrentPrimary, 1.5f);
            g.DrawPath(neonPen, shadowPath);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var br = new SolidBrush(Color.Magenta);
            e.Graphics.FillRectangle(br, this.ClientRectangle);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _dot1.Paint += (s, ev) => DrawIndicatorDot(ev, 1);
            _dot2.Paint += (s, ev) => DrawIndicatorDot(ev, 2);
            _dot3.Paint += (s, ev) => DrawIndicatorDot(ev, 3);
        }

        private void DrawIndicatorDot(PaintEventArgs ev, int rowIndex)
        {
            var g = ev.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool isActive = false;
            if (_completed)
            {
                isActive = (rowIndex == 2);
            }
            else
            {
                isActive = rowIndex switch
                {
                    1 => _progress < 33.3f,
                    2 => _progress >= 33.3f && _progress < 66.6f,
                    3 => _progress >= 66.6f,
                    _ => false
                };
            }

            int size = 5;
            int cx = _dot1.Width / 2;
            int cy = _dot1.Height / 2;

            if (isActive)
            {
                using var dotBrush = new SolidBrush(ThemeManager.CurrentPrimary);
                g.FillEllipse(dotBrush, cx - size / 2, cy - size / 2, size, size);

                int pingSize = 10;
                using var pingPen = new Pen(Color.FromArgb(100, ThemeManager.CurrentPrimary), 1f);
                g.DrawEllipse(pingPen, cx - pingSize / 2, cy - pingSize / 2, pingSize, pingSize);
            }
            else
            {
                using var dotBrush = new SolidBrush(ThemeManager.CurrentSubText);
                g.FillEllipse(dotBrush, cx - size / 2, cy - size / 2, size, size);
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.StartFigure();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void SetDoubleBuffer(Control c)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, c, new object[] { true });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer?.Dispose();
                try { _webView?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
