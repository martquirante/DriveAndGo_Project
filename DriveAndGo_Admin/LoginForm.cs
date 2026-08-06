#nullable disable
using DriveAndGo_Admin.Helpers;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DriveAndGo_Admin
{
    /// <summary>
    /// World-Class SaaS & Mobile App Inspired Login Screen.
    /// Features Pepsi-Style Interactive Reveal Card with Mouse-Tracking Spotlight on Left Panel (60FPS GDI+),
    /// ultra-low ghost idle card opacity, exact business description, and dynamic ThemeManager Integration on Right Panel.
    /// </summary>
    public class LoginForm : Form
    {
        // ── UI Panels ───────────────────────────────────────────────────────────
        private Panel _leftPanel;
        private Panel _rightPanel;

        // ── Right Panel Controls ────────────────────────────────────────────────
        private Label      _lblPortal;
        private Label      _lblHint;
        private Label      _lblEmail;
        private Label      _lblPassword;
        private Label      _lblError;
        private Label      _lblVerRight;
        private Panel      _accentBar;
        private Panel      _txtEmailWrap;
        private Panel      _txtPasswordWrap;
        private TextBox    _txtEmail;
        private TextBox    _txtPassword;
        private Button     _btnLogin;
        private Button     _btnShowPass;
        private Button     _btnExit;
        private Button     _btnThemeToggle;
        private bool       _passVisible = false;
        private bool       _eyeHovered  = false;

        // ── Theme Switcher Animation State ──────────────────────────────────────
        private float _knobX;
        private float _knobTarget;
        private System.Windows.Forms.Timer _knobTimer;

        // ── Left Panel Physics & Mouse Spotlight State ──────────────────────────
        private PointF _targetMousePos;
        private PointF _currentMousePos;
        private float  _hoverProgress = 0f; // 0.0 (idle) -> 1.0 (fully hovered)
        private System.Windows.Forms.Timer _physicsTimer;

        // ── Button Hover Glow State ─────────────────────────────────────────────
        private System.Windows.Forms.Timer _btnGlowTimer;
        private float _btnGlow;
        private bool  _btnHovered;

        // ── General Form Timers & Focus ─────────────────────────────────────────
        private System.Windows.Forms.Timer _fadeTimer;
        private float _opacity;
        private Panel _focusedWrap;

        // ── Form Drag Support ───────────────────────────────────────────────────
        private bool  _dragging;
        private Point _dragStart;

        // ── Win32 DWM Drop Shadow ───────────────────────────────────────────────
        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS m);
        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS { public int Left, Right, Top, Bottom; }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ClassStyle |= 0x00020000; return cp; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                var m = new MARGINS { Left = 1, Right = 1, Top = 1, Bottom = 1 };
                DwmExtendFrameIntoClientArea(this.Handle, ref m);
            }
            catch { }
        }

        public LoginForm()
        {
            this.DoubleBuffered = true;
            this.SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint  |
                ControlStyles.UserPaint, true);
            this.UpdateStyles();

            IconHelper.ApplyToForm(this);

            BuildForm();
            BuildLeftPanel();
            BuildRightPanel();
            ApplyTheme();
            StartAnimations();
            InitializeNetworkMonitoring();
        }

        private void BuildForm()
        {
            this.Size            = new Size(940, 600);
            this.MinimumSize     = new Size(940, 600);
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor       = ThemeManager.CurrentBackground;
            this.Font            = new Font("Segoe UI", 10F);
            this.Opacity         = 0;
            this.Text            = "Drive & Go — Admin Portal";

            this.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) { _dragging = true; _dragStart = e.Location; }
            };
            this.MouseMove += (s, e) =>
            {
                if (_dragging)
                    this.Location = new Point(this.Left + e.X - _dragStart.X, this.Top + e.Y - _dragStart.Y);
            };
            this.MouseUp += (s, e) => _dragging = false;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  LEFT PANEL — PEPSI-STYLE INTERACTIVE REVEAL CARD (NATIVE GDI+)
        // ════════════════════════════════════════════════════════════════════════
        private void BuildLeftPanel()
        {
            _leftPanel = new Panel();
            EnableDB(_leftPanel);
            _leftPanel.Size      = new Size(390, 600);
            _leftPanel.Location  = new Point(0, 0);
            _leftPanel.BackColor = ThemeManager.CurrentBackground;
            _leftPanel.Paint     += OnLeftPanelPaint;

            _targetMousePos  = new PointF(195f, 300f);
            _currentMousePos = _targetMousePos;

            _leftPanel.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) { _dragging = true; _dragStart = new Point(e.X, e.Y); }
            };
            _leftPanel.MouseMove += (s, e) =>
            {
                _targetMousePos = e.Location;
                if (_dragging)
                    this.Location = new Point(this.Left + e.X - _dragStart.X, this.Top + e.Y - _dragStart.Y);
            };
            _leftPanel.MouseUp += (s, e) => _dragging = false;

            _leftPanel.MouseEnter += (s, e) =>
            {
                _leftPanel.Cursor = Cursors.Cross; // Interactive spotlight cursor pointer
            };
            _leftPanel.MouseLeave += (s, e) =>
            {
                _leftPanel.Cursor = Cursors.Default;
            };

            this.Controls.Add(_leftPanel);
        }

        private void OnLeftPanelPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int w = _leftPanel.Width;
            int h = _leftPanel.Height;

            // 1. Base Background from ThemeManager
            g.FillRectangle(new SolidBrush(ThemeManager.CurrentBackground), _leftPanel.ClientRectangle);

            // 2. Mouse-Tracking Ambient Spotlight Glow
            int spotRadius = (int)(190 + _hoverProgress * 70);
            float cx = _currentMousePos.X;
            float cy = _currentMousePos.Y;
            var spotRect = new RectangleF(cx - spotRadius, cy - spotRadius, spotRadius * 2, spotRadius * 2);

            try
            {
                using (var glowPath = new GraphicsPath())
                {
                    glowPath.AddEllipse(spotRect);
                    using var spotBrush = new PathGradientBrush(glowPath);

                    int alpha = (int)(55 + _hoverProgress * 125);
                    spotBrush.CenterColor    = Color.FromArgb(alpha, ThemeManager.CurrentPrimary);
                    spotBrush.SurroundColors = new[] { Color.Transparent };
                    g.FillPath(spotBrush, glowPath);
                }
            }
            catch { }

            // 3. Glassmorphic Card (0% Opacity when Idle; Fades in ONLY on Hover)
            int cardW = 320;
            int cardH = 460;
            int cardX = (w - cardW) / 2;
            int cardY = (h - cardH) / 2;
            float centerX = cardX + cardW / 2f;
            float centerY = cardY + cardH / 2f;

            // Parallax Tilt Shift
            float parallaxX = (cx - centerX) * 0.04f * _hoverProgress;
            float parallaxY = (cy - centerY) * 0.04f * _hoverProgress;

            var cardBounds = new Rectangle(cardX + (int)parallaxX, cardY + (int)parallaxY, cardW, cardH);
            using var cardPath = RR(cardBounds, 20);

            // Dynamic Hover Opacity Calculations (Completely invisible when idle)
            int cardBgAlpha = (int)(45 * _hoverProgress);
            int cardBorderAlpha = (int)(110 * _hoverProgress);

            if (cardBgAlpha > 0)
            {
                Color cardBgColor = ThemeManager.IsDarkMode
                    ? Color.FromArgb(cardBgAlpha, 255, 255, 255)
                    : Color.FromArgb((int)(70 * _hoverProgress), 255, 255, 255);

                using (var cardBrush = new SolidBrush(cardBgColor))
                {
                    g.FillPath(cardBrush, cardPath);
                }
            }

            if (cardBorderAlpha > 0)
            {
                Color cardBorderColor = ThemeManager.IsDarkMode
                    ? Color.FromArgb(cardBorderAlpha, 255, 255, 255)
                    : Color.FromArgb(cardBorderAlpha, ThemeManager.CurrentBorder);

                using (var borderPen = new Pen(cardBorderColor, 1.2f))
                {
                    g.DrawPath(borderPen, cardPath);
                }
            }

            // 4. Element Sliding & Fading Mathematics
            float lift = _hoverProgress * 80f; // 80px translation lift on hover

            // Draw Official Drive & Go Logo Image (Wide Aspect Ratio)
            Image logoImg = GetLogoImage();
            if (logoImg != null)
            {
                float aspect = (float)logoImg.Width / Math.Max(1, logoImg.Height);
                int logoW = 250;
                int logoH = Math.Min(160, (int)(logoW / aspect));

                float logoY = centerY - 95f - lift + parallaxY * 0.5f;
                var logoRect = new Rectangle((int)(centerX + parallaxX * 1.3f - logoW / 2f), (int)logoY, logoW, logoH);

                g.DrawImage(logoImg, logoRect);

                // Subtitle ("Vehicle Rental Platform") — Fades OUT on hover
                float subY = logoY + logoH + 10f;
                int subAlpha = (int)((1f - _hoverProgress) * 255f);
                subAlpha = Math.Clamp(subAlpha, 0, 255);

                if (subAlpha > 5)
                {
                    using (var subFont  = new Font("Segoe UI", 11F, FontStyle.Regular))
                    using (var subBrush = new SolidBrush(Color.FromArgb(subAlpha, ThemeManager.CurrentSubText)))
                    {
                        var sfSub = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
                        g.DrawString("Vehicle Rental Platform", subFont, subBrush, centerX + parallaxX, subY, sfSub);
                    }
                }
            }
            else
            {
                int logoSize = 80;
                float logoY  = centerY - 110f - lift + parallaxY * 0.5f;
                float titleY = logoY + 95f;
                float subY   = titleY + 42f;
                var logoRect = new Rectangle((int)(centerX + parallaxX * 1.3f - logoSize / 2f), (int)logoY, logoSize, logoSize);

                using var logoPath = RR(logoRect, 20);
                using var logoGrad = new LinearGradientBrush(logoRect, ThemeManager.CurrentPrimary, Color.FromArgb(249, 115, 22), LinearGradientMode.ForwardDiagonal);
                g.FillPath(logoGrad, logoPath);
                using var iconFont = new Font("Segoe UI Emoji", 32F);
                using var iconFmt  = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("🚗", iconFont, Brushes.White, logoRect, iconFmt);

                // Draw Main Title ("Drive & Go")
                using (var titleFont = new Font("Segoe UI", 26F, FontStyle.Bold))
                using (var titleBrush = new SolidBrush(ThemeManager.CurrentPrimary))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
                    g.DrawString("Drive & Go", titleFont, titleBrush, centerX + parallaxX, titleY, sf);
                }

                // Idle Subtitle ("Vehicle Rental Platform") — Fades OUT on hover
                int subAlpha = (int)((1f - _hoverProgress) * 255f);
                subAlpha = Math.Clamp(subAlpha, 0, 255);

                if (subAlpha > 5)
                {
                    using (var subFont = new Font("Segoe UI", 11F, FontStyle.Regular))
                    using (var subBrush = new SolidBrush(Color.FromArgb(subAlpha, ThemeManager.CurrentSubText)))
                    {
                        var sfSub = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
                        g.DrawString("Vehicle Rental Platform", subFont, subBrush, centerX + parallaxX, subY, sfSub);
                    }
                }
            }

            // Hover Business Description — Fades IN on hover
            int descAlpha = (int)(_hoverProgress * 255f);
            descAlpha = Math.Clamp(descAlpha, 0, 255);

            if (descAlpha > 5)
            {
                float descY = logoImg != null ? (centerY - 95f - lift + parallaxY * 0.5f + 155f) : (centerY - 110f - lift + parallaxY * 0.5f + 137f);
                string descText = "DriveAndGo is the definitive platform engineered to maximize fleet profitability and protect your automotive assets. By unifying real-time vehicle tracking, automated transaction billing, and deep operational telemetry, we empower business owners to minimize overhead, eliminate security leaks, and accelerate revenue growth effortlessly.";

                var descRect = new RectangleF(cardX + 22 + parallaxX, descY, cardW - 44, 200);
                using (var descFont = new Font("Segoe UI", 9.25F, FontStyle.Regular))
                using (var descBrush = new SolidBrush(Color.FromArgb(descAlpha, ThemeManager.CurrentText)))
                {
                    var sfDesc = new StringFormat
                    {
                        Alignment     = StringAlignment.Center,
                        LineAlignment = StringAlignment.Near,
                        Trimming      = StringTrimming.EllipsisWord
                    };
                    g.DrawString(descText, descFont, descBrush, descRect, sfDesc);
                }
            }

            // Version Footer at bottom of left panel
            using (var verFont = new Font("Segoe UI", 8.5F))
            using (var verBrush = new SolidBrush(ThemeManager.CurrentSubText))
            {
                var sfVer = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
                g.DrawString("v2.0 Enterprise  •  © 2026 DriveAndGo Inc.", verFont, verBrush, centerX, h - 30, sfVer);
            }

            // 1px Vertical divider line on right edge
            using var linePen = new Pen(ThemeManager.CurrentBorder, 1f);
            g.DrawLine(linePen, w - 1, 0, w - 1, h);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  RIGHT PANEL — AUTHENTICATION CARD & THEME SWITCHER
        // ════════════════════════════════════════════════════════════════════════
        private void BuildRightPanel()
        {
            _rightPanel = new Panel();
            EnableDB(_rightPanel);
            _rightPanel.Size      = new Size(550, 600);
            _rightPanel.Location  = new Point(390, 0);
            _rightPanel.BackColor = ThemeManager.CurrentBackground;
            _rightPanel.Paint     += OnRightPanelPaint;

            _rightPanel.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                { _dragging = true; _dragStart = new Point(e.X + _rightPanel.Left, e.Y); }
            };
            _rightPanel.MouseMove += (s, e) =>
            {
                if (_dragging)
                    this.Location = new Point(this.Left + e.X + _rightPanel.Left - _dragStart.X, this.Top + e.Y - _dragStart.Y);
            };
            _rightPanel.MouseUp += (s, e) => _dragging = false;

            // ── Exit Button (Top Right) ──
            _btnExit = new Button
            {
                Text      = "✕",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentSubText,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(36, 36),
                Location  = new Point(_rightPanel.Width - 44, 12),
                Cursor    = Cursors.Hand
            };
            _btnExit.FlatAppearance.BorderSize = 0;
            _btnExit.Click       += (s, e) => FadeAndClose();
            _btnExit.MouseEnter  += (s, e) => _btnExit.ForeColor = Color.FromArgb(239, 68, 68);
            _btnExit.MouseLeave  += (s, e) => _btnExit.ForeColor = ThemeManager.CurrentSubText;
            _rightPanel.Controls.Add(_btnExit);

            // ── Theme Switcher Toggle (Top Right, next to Exit) ──
            _btnThemeToggle = new Button
            {
                Size      = new Size(64, 34),
                Location  = new Point(_rightPanel.Width - 44 - 64 - 10, 13),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };
            _btnThemeToggle.FlatAppearance.BorderSize         = 0;
            _btnThemeToggle.FlatAppearance.MouseOverBackColor = Color.Transparent;

            _knobX      = ThemeManager.IsDarkMode ? 32f : 4f;
            _knobTarget = _knobX;
            _knobTimer  = new System.Windows.Forms.Timer { Interval = 12 };

            _btnThemeToggle.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var rect = new Rectangle(0, 0, _btnThemeToggle.Width - 1, _btnThemeToggle.Height - 1);
                using var path = RR(rect, 17);
                g.FillPath(new SolidBrush(ThemeManager.CurrentCard), path);
                g.DrawPath(new Pen(ThemeManager.CurrentBorder, 1f), path);

                int circleSize = 26;
                var circleRect = new RectangleF(_knobX, 3.5f, circleSize, circleSize);
                Color circleColor = ThemeManager.IsDarkMode
                    ? ThemeManager.CurrentPrimary : Color.FromArgb(245, 158, 11);

                using (var glow = new GraphicsPath())
                {
                    glow.AddEllipse(circleRect.X - 3, circleRect.Y - 3, circleSize + 6, circleSize + 6);
                    using var gb = new PathGradientBrush(glow);
                    gb.CenterColor    = Color.FromArgb(60, circleColor);
                    gb.SurroundColors = new[] { Color.Transparent };
                    g.FillPath(gb, glow);
                }

                g.FillEllipse(new SolidBrush(circleColor), circleRect);

                string icon = ThemeManager.IsDarkMode ? "🌙" : "☀️";
                using var font = new Font("Segoe UI Emoji", 9.5F);
                var sf = new StringFormat
                    { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(icon, font, Brushes.White, circleRect, sf);
            };

            _knobTimer.Tick += (s, e) =>
            {
                float diff = _knobTarget - _knobX;
                if (Math.Abs(diff) < 0.5f) { _knobX = _knobTarget; _knobTimer.Stop(); }
                else _knobX += diff * 0.22f;
                _btnThemeToggle.Invalidate();
            };

            _btnThemeToggle.Click += (s, e) =>
            {
                ThemeManager.IsDarkMode = !ThemeManager.IsDarkMode;
                _knobTarget = ThemeManager.IsDarkMode ? 32f : 4f;
                _knobTimer.Start();
                ApplyTheme();
            };

            _rightPanel.Controls.Add(_btnThemeToggle);

            // ── Heading ──
            _lblPortal = new Label
            {
                Text      = "Admin Portal",
                Font      = new Font("Segoe UI", 22F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentText,
                AutoSize  = false,
                Size      = new Size(_rightPanel.Width, 42),
                Location  = new Point(0, 105),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            _rightPanel.Controls.Add(_lblPortal);

            _accentBar = new Panel
            {
                Size      = new Size(50, 3),
                Location  = new Point((_rightPanel.Width - 50) / 2, 152),
                BackColor = ThemeManager.CurrentPrimary
            };
            _rightPanel.Controls.Add(_accentBar);

            _lblHint = new Label
            {
                Text      = "Sign in to continue",
                Font      = new Font("Segoe UI", 9.5F),
                ForeColor = ThemeManager.CurrentSubText,
                AutoSize  = false,
                Size      = new Size(_rightPanel.Width, 22),
                Location  = new Point(0, 162),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            _rightPanel.Controls.Add(_lblHint);

            // Inputs
            _lblEmail = MakeInputLabel("EMAIL ADDRESS", 212);
            _rightPanel.Controls.Add(_lblEmail);
            _txtEmailWrap = MakeInputWrapper(234, "✉", out _txtEmail, false);
            _txtEmail.PlaceholderText = "example@email.com";
            _txtEmail.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    _txtPassword.Focus();
                    _txtPassword.SelectAll();
                }
            };
            _rightPanel.Controls.Add(_txtEmailWrap);

            _lblPassword = MakeInputLabel("PASSWORD", 304);
            _rightPanel.Controls.Add(_lblPassword);
            _txtPasswordWrap = MakeInputWrapper(326, "🔒", out _txtPassword, true);
            _txtPassword.PlaceholderText = "••••••••";
            _txtPassword.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Up)
                {
                    e.SuppressKeyPress = true;
                    _txtEmail.Focus();
                    _txtEmail.SelectAll();
                }
                else if (e.KeyCode == Keys.Down)
                {
                    e.SuppressKeyPress = true;
                    _btnLogin.Focus();
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    DoLogin();
                }
            };
            _rightPanel.Controls.Add(_txtPasswordWrap);

            // Vector Eye Button
            _btnShowPass = new Button
            {
                Size      = new Size(36, 36),
                Location  = new Point(_txtPasswordWrap.Right - 42, 333),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };
            _btnShowPass.FlatAppearance.BorderSize = 0;
            _btnShowPass.Click += (s, e) =>
            {
                _passVisible = !_passVisible;
                _txtPassword.UseSystemPasswordChar = !_passVisible;
                _btnShowPass.Invalidate();
            };
            _btnShowPass.MouseEnter += (s, e) => { _eyeHovered = true;  _btnShowPass.Invalidate(); };
            _btnShowPass.MouseLeave += (s, e) => { _eyeHovered = false; _btnShowPass.Invalidate(); };
            _btnShowPass.Paint += OnEyeButtonPaint;

            _rightPanel.Controls.Add(_btnShowPass);
            _btnShowPass.BringToFront();

            // Forgot Password Button
            var btnForgotPass = new Button
            {
                Text      = "Forgot Password?",
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentPrimary,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(130, 24),
                Location  = new Point(_txtPasswordWrap.Right - 130, 375),
                Cursor    = Cursors.Hand
            };
            btnForgotPass.FlatAppearance.BorderSize = 0;
            btnForgotPass.Click += (s, e) =>
            {
                string em = _txtEmail?.Text?.Trim() ?? "";
                using var resetDlg = new ResetPasswordDialog(em);
                resetDlg.ShowDialog(this);
            };
            _rightPanel.Controls.Add(btnForgotPass);

            _lblError = new Label
            {
                AutoSize  = false,
                Size      = new Size(400, 22),
                Location  = new Point(75, 384),
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(239, 68, 68),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Visible   = false
            };
            _rightPanel.Controls.Add(_lblError);

            _btnLogin = new Button
            {
                Text      = "SIGN IN",
                Size      = new Size(400, 50),
                Location  = new Point(75, 416),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ThemeManager.CurrentPrimary,
                Cursor    = Cursors.Hand
            };
            _btnLogin.FlatAppearance.BorderSize         = 0;
            _btnLogin.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnLogin.FlatAppearance.MouseDownBackColor = Color.Transparent;
            _btnLogin.Paint += OnLoginBtnPaint;
            _btnLogin.Click += (s, e) => DoLogin();
            _btnLogin.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Up)
                {
                    e.SuppressKeyPress = true;
                    _txtPassword.Focus();
                    _txtPassword.SelectAll();
                }
            };

            _btnGlowTimer = new System.Windows.Forms.Timer { Interval = 12 };
            _btnGlowTimer.Tick += (s, e) =>
            {
                float target = _btnHovered ? 1f : 0f;
                float diff   = target - _btnGlow;
                if (Math.Abs(diff) < 0.02f) { _btnGlow = target; _btnGlowTimer.Stop(); }
                else _btnGlow += diff * 0.22f;
                _btnLogin.Invalidate();
            };
            _btnLogin.MouseEnter += (s, e) => { _btnHovered = true;  _btnGlowTimer.Start(); };
            _btnLogin.MouseLeave += (s, e) => { _btnHovered = false; _btnGlowTimer.Start(); };
            SetRoundRegion(_btnLogin, 12);
            _rightPanel.Controls.Add(_btnLogin);

            _lblVerRight = new Label
            {
                Text      = "DriveAndGo Admin v2.0  •  © 2026",
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = ThemeManager.CurrentSubText,
                AutoSize  = false,
                Size      = new Size(_rightPanel.Width, 20),
                Location  = new Point(0, 568),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            _rightPanel.Controls.Add(_lblVerRight);

            this.Controls.Add(_rightPanel);
        }

        private void OnEyeButtonPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color iconCol = _eyeHovered || _passVisible
                ? ThemeManager.CurrentPrimary
                : ThemeManager.CurrentSubText;

            using var pen = new Pen(iconCol, 1.8f);
            pen.StartCap = LineCap.Round;
            pen.EndCap   = LineCap.Round;

            int cx = _btnShowPass.Width / 2;
            int cy = _btnShowPass.Height / 2;

            var eyePath = new GraphicsPath();
            eyePath.AddArc(cx - 10, cy - 8, 20, 16, 200, 140);
            eyePath.AddArc(cx - 10, cy - 8, 20, 16, 20,  140);
            g.DrawPath(pen, eyePath);

            if (_passVisible)
            {
                using var pupilBrush = new SolidBrush(iconCol);
                g.FillEllipse(pupilBrush, cx - 3, cy - 3, 6, 6);
            }
            else
            {
                g.DrawLine(pen, cx - 7, cy - 6, cx + 7, cy + 6);
            }
        }

        private Label MakeInputLabel(string text, int y)
        {
            return new Label
            {
                Text      = text,
                Font      = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentSubText,
                AutoSize  = true,
                Location  = new Point(75, y),
                BackColor = Color.Transparent
            };
        }

        private Panel MakeInputWrapper(int y, string iconSymbol, out TextBox tb, bool isPassword)
        {
            var wrap = new Panel();
            EnableDB(wrap);
            wrap.Size      = new Size(400, 50);
            wrap.Location  = new Point(75, y);
            wrap.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(18, 18, 34) : Color.FromArgb(255, 255, 255);
            wrap.Cursor    = Cursors.IBeam;
            wrap.Paint    += (s, e) => DrawInputWrapper(e, wrap, iconSymbol);

            tb = new TextBox
            {
                Size        = new Size(isPassword ? wrap.Width - 46 - 48 : wrap.Width - 46 - 16, 28),
                Location    = new Point(44, 13),
                BorderStyle = BorderStyle.None,
                BackColor   = wrap.BackColor,
                ForeColor   = ThemeManager.CurrentText,
                Font        = new Font("Segoe UI", 10.5F)
            };
            if (isPassword) tb.UseSystemPasswordChar = true;

            var textBox = tb;
            textBox.Enter += (s, e) => { _focusedWrap = wrap; wrap.Invalidate(); };
            textBox.Leave += (s, e) => { _focusedWrap = null; wrap.Invalidate(); };
            wrap.Click    += (s, e) => textBox.Focus();
            wrap.Controls.Add(textBox);
            SetRoundRegion(wrap, 12);
            return wrap;
        }

        private void DrawInputWrapper(PaintEventArgs e, Panel wrap, string iconSymbol)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var r = new Rectangle(0, 0, wrap.Width - 1, wrap.Height - 1);
            using var path = RR(r, 12);
            wrap.Region = new Region(path);

            Color inputBg = ThemeManager.IsDarkMode ? Color.FromArgb(18, 18, 34) : Color.FromArgb(255, 255, 255);
            g.FillPath(new SolidBrush(inputBg), path);

            bool focused = _focusedWrap == wrap;
            Color borderCol = focused
                ? ThemeManager.CurrentPrimary
                : ThemeManager.CurrentBorder;
            
            using var pen = new Pen(borderCol, focused ? 1.5f : 1f);
            g.DrawPath(pen, path);

            Color iconCol = focused ? ThemeManager.CurrentPrimary : ThemeManager.CurrentSubText;
            using var iconFont = new Font("Segoe UI Emoji", 11F);
            using var iconBrush = new SolidBrush(iconCol);
            
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            var iconRect = new RectangleF(10, 0, 30, wrap.Height);
            g.DrawString(iconSymbol, iconFont, iconBrush, iconRect, sf);

            if (focused)
            {
                using var accentPen = new Pen(ThemeManager.CurrentPrimary, 2f);
                g.DrawLine(accentPen, 16, wrap.Height - 1, wrap.Width - 16, wrap.Height - 1);
            }
        }

        private void OnRightPanelPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            g.FillRectangle(new SolidBrush(ThemeManager.CurrentBackground), _rightPanel.ClientRectangle);

            int cx = _rightPanel.Width / 2;
            try
            {
                using var gb = new PathGradientBrush(new Point[]
                {
                    new(cx - 200, -10), new(cx + 200, -10),
                    new(cx + 200, 90),  new(cx - 200, 90)
                })
                {
                    CenterColor    = Color.FromArgb(14, ThemeManager.CurrentPrimary),
                    SurroundColors = new[] { Color.Transparent }
                };
                g.FillEllipse(gb, cx - 205, -15, 410, 110);
            }
            catch { }
        }

        private void OnLoginBtnPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, _btnLogin.Width - 1, _btnLogin.Height - 1);
            using var path = RR(rect, 12);
            _btnLogin.Region = new Region(path);

            Color btnColor = ThemeManager.CurrentPrimary;
            g.FillPath(new SolidBrush(btnColor), path);

            if (_btnGlow > 0.01f)
            {
                int a = (int)(40 * _btnGlow);
                using var glow = new SolidBrush(Color.FromArgb(a, 255, 255, 255));
                g.FillPath(glow, path);
            }

            using var fmt = new StringFormat
            { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(_btnLogin.Text,
                new Font("Segoe UI", 12F, FontStyle.Bold),
                Brushes.White, rect, fmt);
        }

        private void ApplyTheme()
        {
            this.BackColor = ThemeManager.CurrentBackground;

            if (_leftPanel != null) _leftPanel.BackColor = ThemeManager.CurrentBackground;
            if (_rightPanel != null) _rightPanel.BackColor = ThemeManager.CurrentBackground;
            if (_btnExit != null) _btnExit.ForeColor = ThemeManager.CurrentSubText;
            if (_lblPortal != null) _lblPortal.ForeColor = ThemeManager.CurrentText;
            if (_lblHint != null) _lblHint.ForeColor = ThemeManager.CurrentSubText;
            if (_lblEmail != null) _lblEmail.ForeColor = ThemeManager.CurrentSubText;
            if (_lblPassword != null) _lblPassword.ForeColor = ThemeManager.CurrentSubText;
            if (_lblVerRight != null) _lblVerRight.ForeColor = ThemeManager.CurrentSubText;
            if (_accentBar != null) _accentBar.BackColor = ThemeManager.CurrentPrimary;
            if (_btnLogin != null) _btnLogin.BackColor = ThemeManager.CurrentPrimary;

            Color inputBg = ThemeManager.IsDarkMode ? Color.FromArgb(18, 18, 34) : Color.FromArgb(255, 255, 255);
            Color inputText = ThemeManager.CurrentText;

            if (_txtEmailWrap != null) { _txtEmailWrap.BackColor = inputBg; _txtEmailWrap.Invalidate(); }
            if (_txtEmail != null) { _txtEmail.BackColor = inputBg; _txtEmail.ForeColor = inputText; }

            if (_txtPasswordWrap != null) { _txtPasswordWrap.BackColor = inputBg; _txtPasswordWrap.Invalidate(); }
            if (_txtPassword != null) { _txtPassword.BackColor = inputBg; _txtPassword.ForeColor = inputText; }

            _btnShowPass?.Invalidate();
            _btnLogin?.Invalidate();
            _btnThemeToggle?.Invalidate();
            _leftPanel?.Invalidate();
            _rightPanel?.Invalidate();
            this.Invalidate();
        }

        private void StartAnimations()
        {
            _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _fadeTimer.Tick += (s, e) =>
            {
                _opacity += 0.06f;
                this.Opacity = Math.Min(_opacity, 1.0);
                if (_opacity >= 1.0) { _fadeTimer.Stop(); _fadeTimer.Dispose(); }
            };
            _fadeTimer.Start();

            // 60FPS Physics Timer for Mouse-Tracking Spotlight + Reveal Card
            _physicsTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _physicsTimer.Tick += (s, e) =>
            {
                _currentMousePos.X += (_targetMousePos.X - _currentMousePos.X) * 0.15f;
                _currentMousePos.Y += (_targetMousePos.Y - _currentMousePos.Y) * 0.15f;

                if (_leftPanel != null && !_leftPanel.IsDisposed)
                {
                    Point clientPt = _leftPanel.PointToClient(Cursor.Position);
                    float targetHover = _leftPanel.ClientRectangle.Contains(clientPt) ? 1f : 0f;
                    _hoverProgress += (targetHover - _hoverProgress) * 0.12f;
                    _leftPanel.Invalidate();
                }
            };
            _physicsTimer.Start();
        }

        private void FadeAndClose()
        {
            var t = new System.Windows.Forms.Timer { Interval = 16 };
            t.Tick += (s, e) =>
            {
                this.Opacity -= 0.08f;
                if (this.Opacity <= 0) { t.Stop(); Application.Exit(); }
            };
            t.Start();
        }

        private void DoLogin()
        {
            string email = _txtEmail?.Text?.Trim() ?? "";
            string pass  = _txtPassword?.Text ?? "";

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
            {
                ShowError("Please enter both email and password.");
                return;
            }

            _btnLogin.Text    = "AUTHENTICATING...";
            _btnLogin.Enabled = false;
            _lblError.Visible = false;
            _btnLogin.Invalidate();

            Task.Run(async () =>
            {
                var (result, apiError) = await ApiService.LoginAsync(email, pass);

                this.Invoke((Action)(() =>
                {
                    _btnLogin.Text    = "SIGN IN";
                    _btnLogin.Enabled = true;
                    _btnLogin.Invalidate();

                    if (result == null)
                    {
                        ShowError("⚠  " + (apiError ?? "Login failed. Check API server."));
                        return;
                    }

                    if (result.Requires2FA)
                    {
                        using var otpDlg = new OtpVerificationDialog(
                            email, 
                            "2FA Login Verification", 
                            resendCallback: async () =>
                            {
                                var (res, err) = await ApiService.LoginAsync(email, pass);
                                return res != null && res.Requires2FA;
                            },
                            verifyCallback: async (code) =>
                            {
                                var (vResp, vErr) = await ApiService.Verify2FaAsync(email, code);
                                if (vResp == null)
                                {
                                    return (false, vErr ?? "Invalid or expired 2FA code.");
                                }
                                if (!string.Equals(vResp.Role, "admin", StringComparison.OrdinalIgnoreCase))
                                {
                                    SessionManager.Clear();
                                    return (false, "Access denied — Admin accounts only.");
                                }
                                return (true, null);
                            }
                        );

                        otpDlg.ResendRequested += async (senderEvt, argsEvt) =>
                        {
                            await ApiService.LoginAsync(email, pass);
                        };

                        if (otpDlg.ShowDialog(this) == DialogResult.OK)
                        {
                            ProceedToDashboard();
                        }
                        return;
                    }
                    else
                    {
                        if (!string.Equals(result.Role, "admin", StringComparison.OrdinalIgnoreCase))
                        {
                            ShowError("⚠  Access denied — Admin accounts only.");
                            SessionManager.Clear();
                            return;
                        }

                        ProceedToDashboard();
                    }
                }));
            });
        }

        private void ProceedToDashboard()
        {
            var loader = new PostLoginLoaderForm();
            loader.Show();

            var fadeOut = new System.Windows.Forms.Timer { Interval = 16 };
            fadeOut.Tick += (s2, e2) =>
            {
                this.Opacity -= 0.08f;
                if (this.Opacity <= 0)
                {
                    fadeOut.Stop();
                    fadeOut.Dispose();
                    this.Hide();
                }
            };
            fadeOut.Start();
        }

        private void ShowError(string message)
        {
            _lblError.Text    = message;
            _lblError.Visible = true;

            int origX = _rightPanel.Left;
            int count = 0;
            var shake = new System.Windows.Forms.Timer { Interval = 22 };
            shake.Tick += (s, e) =>
            {
                count++;
                _rightPanel.Left = count % 2 == 0 ? origX + 6 : origX - 6;
                if (count >= 10) { _rightPanel.Left = origX; shake.Stop(); shake.Dispose(); }
            };
            shake.Start();
        }

        private GraphicsPath RR(Rectangle r, int radius)
        {
            int d = radius * 2;
            var arc = new Rectangle(r.Location, new Size(d, d));
            var path = new GraphicsPath();
            path.AddArc(arc, 180, 90); arc.X = r.Right - d;
            path.AddArc(arc, 270, 90); arc.Y = r.Bottom - d;
            path.AddArc(arc, 0,   90); arc.X = r.Left;
            path.AddArc(arc, 90,  90); path.CloseFigure();
            return path;
        }

        private void SetRoundRegion(Control c, int r)
        {
            c.Region = new Region(RR(new Rectangle(0, 0, c.Width, c.Height), r));
        }

        private static void EnableDB(Control c)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, c, new object[] { true });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  NETWORK MONITORING & OFFLINE BANNER
        // ════════════════════════════════════════════════════════════════════════
        private Panel _pnlOfflineWarning;
        private Label _lblOfflineWarningText;
        private System.Windows.Forms.Timer _netCheckTimer;
        private System.Windows.Forms.Timer _restoreBannerTimer;
        private bool _wasOffline = false;
        private bool _isCheckingNet = false;
        private static readonly System.Net.Http.HttpClient _netCheckClient = new System.Net.Http.HttpClient();

        private void InitializeNetworkMonitoring()
        {
            _pnlOfflineWarning = new Panel
            {
                Height = 36,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(220, 38, 38), // Crimson Red
                Visible = false
            };

            _lblOfflineWarningText = new Label
            {
                Text = "⚠️ No Internet Connection. Working offline — Live features are paused.",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };

            _pnlOfflineWarning.Controls.Add(_lblOfflineWarningText);
            this.Controls.Add(_pnlOfflineWarning);
            this.Controls.SetChildIndex(_pnlOfflineWarning, 0);

            _restoreBannerTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _restoreBannerTimer.Tick += (s, e) =>
            {
                _restoreBannerTimer.Stop();
                if (!_wasOffline)
                {
                    _pnlOfflineWarning.Visible = false;
                }
            };

            _netCheckTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _netCheckTimer.Tick += async (s, e) =>
            {
                if (_isCheckingNet) return;
                _isCheckingNet = true;

                try
                {
                    bool isOnline = await CheckInternetConnectionAsync();

                    if (this.IsDisposed || !this.IsHandleCreated) return;

                    if (!isOnline)
                    {
                        // 🔴 State: OFFLINE (Spotify Red Banner)
                        _wasOffline = true;
                        _restoreBannerTimer.Stop();
                        _pnlOfflineWarning.BackColor = Color.FromArgb(220, 38, 38);
                        _lblOfflineWarningText.Text = "⚠️ No Internet Connection. Working offline — Live features are paused.";
                        _pnlOfflineWarning.Visible = true;
                    }
                    else if (_wasOffline)
                    {
                        // 🟢 State: JUST RESTORED (Spotify Emerald Green Banner - Displays for 3s then auto-hides)
                        _wasOffline = false;
                        _pnlOfflineWarning.BackColor = Color.FromArgb(16, 185, 129);
                        _lblOfflineWarningText.Text = "📶 Internet Connection Restored! You are back online.";
                        _pnlOfflineWarning.Visible = true;
                        _restoreBannerTimer.Stop();
                        _restoreBannerTimer.Start();
                    }
                }
                catch
                {
                    // Swallowing exception in async void event handler to prevent crashes
                }
                finally
                {
                    _isCheckingNet = false;
                }
            };
            _netCheckTimer.Start();
        }

        private static async System.Threading.Tasks.Task<bool> CheckInternetConnectionAsync()
        {
            try
            {
                // 1. Hardware network interface check (ignore loopback & tunnel)
                bool hasActiveAdapter = System.Linq.Enumerable.Any(
                    System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces(),
                    ni => ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                          ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback &&
                          ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Tunnel);

                if (!hasActiveAdapter)
                {
                    return false;
                }
            }
            catch
            {
                if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) return false;
            }

            // 2. ICMP Ping check to 8.8.8.8 (Google Public DNS)
            // SendPingAsync returns IPStatus.TimedOut when offline — WITHOUT throwing any TaskCanceledException!
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = await ping.SendPingAsync("8.8.8.8", 1200);
                if (reply != null && reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                {
                    return true;
                }
            }
            catch
            {
            }

            // 3. Backup Ping check to 1.1.1.1 (Cloudflare DNS)
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = await ping.SendPingAsync("1.1.1.1", 1200);
                if (reply != null && reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                {
                    return true;
                }
            }
            catch
            {
            }

            // 4. HTTP fallback for networks blocking ICMP ping
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(1200);
                using var response = await _netCheckClient.GetAsync("http://clients3.google.com/generate_204", cts.Token);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private static Image _cachedLogoImage = null;

        private static Image GetLogoImage()
        {
            if (_cachedLogoImage != null) return _cachedLogoImage;

            // 1. Try Assembly Manifest Resource Stream
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                foreach (string name in asm.GetManifestResourceNames())
                {
                    if (name.EndsWith("DriveAndGo_Logo.png", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith("logo.png", StringComparison.OrdinalIgnoreCase))
                    {
                        using var stream = asm.GetManifestResourceStream(name);
                        if (stream != null)
                        {
                            _cachedLogoImage = Image.FromStream(stream);
                            return _cachedLogoImage;
                        }
                    }
                }
            }
            catch { }

            // 2. Try Properties.Resources
            try
            {
                if (Properties.Resources.DriveAndGo_Logo != null)
                {
                    _cachedLogoImage = (Image)Properties.Resources.DriveAndGo_Logo.Clone();
                    return _cachedLogoImage;
                }
            }
            catch { }

            try
            {
                if (Properties.Resources.logo != null)
                {
                    _cachedLogoImage = (Image)Properties.Resources.logo.Clone();
                    return _cachedLogoImage;
                }
            }
            catch { }

            // 3. Try Disk File Paths
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string projectDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
                string[] candidates = new string[]
                {
                    Path.Combine(baseDir, "Resources", "DriveAndGo_Logo.png"),
                    Path.Combine(baseDir, "Resources", "logo.png"),
                    Path.Combine(projectDir, "Resources", "DriveAndGo_Logo.png"),
                    Path.Combine(projectDir, "Resources", "logo.png"),
                    Path.Combine(projectDir, "WebAssets", "logo.png"),
                    Path.Combine(Application.StartupPath, "Resources", "DriveAndGo_Logo.png"),
                    @"C:\Users\martq\source\repos\DriveAndGo_Project\DriveAndGo_Admin\Resources\DriveAndGo_Logo.png",
                    @"C:\Users\martq\source\repos\DriveAndGo_Project\DriveAndGo_Admin\Resources\logo.png"
                };

                foreach (var path in candidates)
                {
                    if (File.Exists(path))
                    {
                        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        _cachedLogoImage = Image.FromStream(stream);
                        return _cachedLogoImage;
                    }
                }
            }
            catch { }

            return _cachedLogoImage;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _fadeTimer?.Dispose();
                _physicsTimer?.Dispose();
                _btnGlowTimer?.Dispose();
                _knobTimer?.Dispose();
                _netCheckTimer?.Dispose();
                _restoreBannerTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}