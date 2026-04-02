#nullable disable
using DriveAndGo_Admin.Helpers;
using DriveAndGo_Admin.Panels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

namespace DriveAndGo_Admin
{
    public class MainForm : Form
    {
        // ── Sidebar constants ──
        private const int SidebarFullWidth = 240;
        private const int SidebarCollapsedWidth = 64;
        private const int HeaderHeight = 65;

        // ── State ──
        private bool _sidebarCollapsed = false;

        // ── UI ──
        private Panel sidebarPanel;
        private Panel headerPanel;
        private Panel contentPanel;
        private Panel activeIndicator;
        private Panel glassOverlay;

        private PictureBox picLogo;
        private Label lblLogo;
        private Label lblLogoSub;
        private Label lblHeaderTitle;
        private Label lblUserName;
        private Label lblUserRole;
        private Label lblOnlineStatus;
        private Panel userPanel;
        private Panel dividerBottom;
        private Panel dividerTop;

        private Button btnToggleSidebar;
        private Button btnThemeToggle;
        private Button btnNotifications;
        private Button activeButton;

        // ── Nav buttons ──
        private Button btnDashboard;
        private Button btnVehicles;
        private Button btnRentals;
        private Button btnDrivers;
        private Button btnTransactions;
        private Button btnReports;
        private Button btnLogout;

        // ── Animation ──
        private System.Windows.Forms.Timer _animTimer;
        private System.Windows.Forms.Timer _sidebarTimer;
        private System.Windows.Forms.Timer _themeTimer;
        private System.Windows.Forms.Timer _glassTimer;
        private bool _sidebarAnimating = false;

        private float _targetIndicatorY = 155;
        private float _currentIndicatorY = 155;
        private float _opacity = 0f;

        private float _targetSidebarW = SidebarFullWidth;
        private float _currentSidebarW = SidebarFullWidth;

        // ── Theme fade state ──
        private Color _fromBg, _toBg;
        private Color _fromSidebar, _toSidebar;
        private Color _fromText, _toText;
        private float _themeFade = 1f;   // 0..1
        private bool _themeTransitioning = false;

        // ── Ripple state per button ──
        private class RippleState
        {
            public float X, Y, Radius, MaxRadius, Alpha;
            public System.Windows.Forms.Timer Timer;
        }
        private readonly Dictionary<Button, RippleState> _ripples = new();

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        public MainForm()
        {
            SetDoubleBuffer(this);
            InitializeForm();
            BuildSidebar();
            BuildHeader();
            BuildContent();
            ApplyTheme(animated: false);
            StartAnimations();
            SetActiveButton(btnDashboard);
            var dash = new DashboardPanel();
            ThemeManager.ThemeChanged += (s, e) => dash?.GetType();
            LoadPanel(dash);
        }

        // ══════════════════════════════════════════════
        //  INIT
        // ══════════════════════════════════════════════
        private void InitializeForm()
        {
            this.Size = new Size(1280, 800);
            this.MinimumSize = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Drive & Go — Admin Dashboard";
            this.Font = new Font("Segoe UI", 10F);
            this.Opacity = 0;
            this.Resize += OnFormResize;
        }

        // ══════════════════════════════════════════════
        //  THEME
        // ══════════════════════════════════════════════
        private void ApplyTheme(bool animated = true)
        {
            if (animated && !_themeTransitioning)
            {
                _fromBg = this.BackColor;
                _fromSidebar = sidebarPanel.BackColor;
                _fromText = lblHeaderTitle.ForeColor;

                _toBg = ThemeManager.CurrentBackground;
                _toSidebar = ThemeManager.CurrentSidebar;
                _toText = ThemeManager.CurrentText;

                _themeFade = 0f;
                _themeTransitioning = true;

                if (_themeTimer != null) _themeTimer.Dispose();
                _themeTimer = new System.Windows.Forms.Timer { Interval = 12 };
                _themeTimer.Tick += OnThemeFadeTick;
                _themeTimer.Start();
            }
            else
            {
                ApplyThemeImmediate();
            }
        }

        private void OnThemeFadeTick(object s, EventArgs e)
        {
            _themeFade += 0.07f;
            if (_themeFade >= 1f)
            {
                _themeFade = 1f;
                _themeTimer.Stop();
                _themeTimer.Dispose();
                _themeTransitioning = false;
            }

            Color Lerp(Color a, Color b, float t) => Color.FromArgb(
                Clamp((int)(a.A + (b.A - a.A) * t)),
                Clamp((int)(a.R + (b.R - a.R) * t)),
                Clamp((int)(a.G + (b.G - a.G) * t)),
                Clamp((int)(a.B + (b.B - a.B) * t)));

            var bg = Lerp(_fromBg, _toBg, _themeFade);
            var sidebar = Lerp(_fromSidebar, _toSidebar, _themeFade);
            var text = Lerp(_fromText, _toText, _themeFade);

            this.BackColor = bg;
            contentPanel.BackColor = bg;
            headerPanel.BackColor = bg;
            sidebarPanel.BackColor = sidebar;

            lblHeaderTitle.ForeColor = text;
            btnToggleSidebar.ForeColor = text;
            lblUserName.ForeColor = text;
            lblUserRole.ForeColor = ThemeManager.CurrentPrimary;

            if (_themeFade >= 1f) ApplyThemeImmediate();
            else
            {
                btnThemeToggle.Invalidate();
                btnToggleSidebar.Invalidate(); // Refresh toggle color
                headerPanel.Invalidate();
                sidebarPanel.Invalidate();
            }
        }

        private void ApplyThemeImmediate()
        {
            this.BackColor = ThemeManager.CurrentBackground;
            contentPanel.BackColor = ThemeManager.CurrentBackground;
            headerPanel.BackColor = ThemeManager.CurrentBackground;
            sidebarPanel.BackColor = ThemeManager.CurrentSidebar;

            lblHeaderTitle.ForeColor = ThemeManager.CurrentText;
            btnToggleSidebar.ForeColor = ThemeManager.CurrentText;

            btnNotifications.BackColor = ThemeManager.CurrentCard;
            btnNotifications.ForeColor = ThemeManager.CurrentText;
            btnNotifications.FlatAppearance.BorderColor = ThemeManager.CurrentBorder;

            lblLogo.ForeColor = ThemeManager.CurrentPrimary;
            lblLogoSub.ForeColor = ThemeManager.CurrentSubText;
            lblUserName.ForeColor = ThemeManager.CurrentText;
            lblUserRole.ForeColor = ThemeManager.CurrentPrimary;

            dividerTop.BackColor = ThemeManager.CurrentBorder;
            dividerBottom.BackColor = ThemeManager.CurrentBorder;
            activeIndicator.BackColor = ThemeManager.CurrentPrimary;

            foreach (Control c in sidebarPanel.Controls)
            {
                if (c is Button btn && btn != btnLogout)
                {
                    if (btn == activeButton)
                    {
                        btn.BackColor = ThemeManager.IsDarkMode
                            ? Color.FromArgb(28, 255, 255, 255)
                            : Color.FromArgb(18, 0, 0, 0);
                        btn.ForeColor = ThemeManager.CurrentPrimary;
                    }
                    else
                    {
                        btn.BackColor = Color.Transparent;
                        btn.ForeColor = ThemeManager.CurrentText;
                    }
                }
            }

            foreach (Control c in contentPanel.Controls)
                c.BackColor = Color.Transparent;

            btnThemeToggle.Invalidate();
            btnToggleSidebar.Invalidate(); // Refresh toggle color
            headerPanel.Invalidate();
            sidebarPanel.Invalidate();
        }

        // ══════════════════════════════════════════════
        //  SIDEBAR
        // ══════════════════════════════════════════════
        private void BuildSidebar()
        {
            sidebarPanel = new Panel();
            SetDoubleBuffer(sidebarPanel);
            sidebarPanel.Width = SidebarFullWidth;
            sidebarPanel.Dock = DockStyle.Left;
            sidebarPanel.Paint += OnSidebarPaint;

            var logoPanel = new Panel();
            logoPanel.Size = new Size(SidebarFullWidth, 95);
            logoPanel.Location = new Point(0, 0);
            logoPanel.BackColor = Color.Transparent;
            logoPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            picLogo = new PictureBox();
            picLogo.Size = new Size(38, 38);
            picLogo.Location = new Point(13, 28);
            picLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picLogo.BackColor = Color.Transparent;
            try { picLogo.Image = Properties.Resources.DriveAndGo_Logo; } catch { }

            lblLogo = new Label();
            lblLogo.Text = "Drive&Go";
            lblLogo.UseMnemonic = false;
            lblLogo.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            lblLogo.Location = new Point(56, 24);
            lblLogo.AutoSize = true;
            lblLogo.BackColor = Color.Transparent;

            lblLogoSub = new Label();
            lblLogoSub.Text = "Admin Portal";
            lblLogoSub.Font = new Font("Segoe UI", 8F);
            lblLogoSub.Location = new Point(58, 48);
            lblLogoSub.AutoSize = true;
            lblLogoSub.BackColor = Color.Transparent;

            logoPanel.Controls.Add(picLogo);
            logoPanel.Controls.Add(lblLogo);
            logoPanel.Controls.Add(lblLogoSub);

            dividerTop = new Panel();
            dividerTop.Size = new Size(200, 1);
            dividerTop.Location = new Point(20, 95);

            activeIndicator = new Panel();
            activeIndicator.Size = new Size(4, 34);
            activeIndicator.Location = new Point(0, 155);
            activeIndicator.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = GetRoundedRect(new Rectangle(0, 0, 4, activeIndicator.Height), 2);
                e.Graphics.FillPath(new SolidBrush(ThemeManager.CurrentPrimary), path);
            };
            activeIndicator.BackColor = Color.Transparent;

            btnDashboard = CreateNavButton("Dashboard", "📊", 120);
            btnVehicles = CreateNavButton("Fleet", "🚗", 172);
            btnRentals = CreateNavButton("Rentals", "📝", 224);
            btnDrivers = CreateNavButton("Drivers", "👤", 276);
            btnTransactions = CreateNavButton("Transactions", "💳", 328);
            btnReports = CreateNavButton("Reports", "📈", 380);

            btnDashboard.Click += (s, e) => { SetActiveButton(btnDashboard); LoadPanel(new DashboardPanel()); };
            btnVehicles.Click += (s, e) => { SetActiveButton(btnVehicles); ShowPlaceholder("🚗 Fleet Management", "Vehicle CRUD will be here."); };
            btnRentals.Click += (s, e) => { SetActiveButton(btnRentals); ShowPlaceholder("📝 Rentals & Bookings", "Manage active reservations."); };
            btnDrivers.Click += (s, e) => { SetActiveButton(btnDrivers); ShowPlaceholder("👤 Driver Management", "Driver profiles and assignments."); };
            btnTransactions.Click += (s, e) => { SetActiveButton(btnTransactions); ShowPlaceholder("💳 Transactions", "Payment confirmations."); };
            btnReports.Click += (s, e) => { SetActiveButton(btnReports); ShowPlaceholder("📈 Sales & Reports", "Financial reports and exports."); };

            dividerBottom = new Panel();
            dividerBottom.Size = new Size(200, 1);
            dividerBottom.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            dividerBottom.Location = new Point(20, this.Height - 155);

            userPanel = new Panel();
            userPanel.Size = new Size(SidebarFullWidth, 70);
            userPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            userPanel.Location = new Point(0, this.Height - 140);
            userPanel.BackColor = Color.Transparent;

            var dot = new Panel();
            dot.Size = new Size(8, 8);
            dot.Location = new Point(12, 26);
            dot.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillEllipse(new SolidBrush(Color.FromArgb(34, 197, 94)), 0, 0, 7, 7);
            };

            lblUserName = new Label();
            lblUserName.Text = SessionManager.UserId > 0 ? SessionManager.FullName : "Admin";
            lblUserName.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblUserName.Location = new Point(26, 10);
            lblUserName.AutoSize = true;
            lblUserName.BackColor = Color.Transparent;

            lblUserRole = new Label();
            lblUserRole.Text = SessionManager.UserId > 0 ? SessionManager.Role.ToUpper() : "ADMIN";
            lblUserRole.Font = new Font("Segoe UI", 8F);
            lblUserRole.Location = new Point(28, 32);
            lblUserRole.AutoSize = true;
            lblUserRole.BackColor = Color.Transparent;

            lblOnlineStatus = new Label();
            lblOnlineStatus.Text = "● Online";
            lblOnlineStatus.Font = new Font("Segoe UI", 8F);
            lblOnlineStatus.ForeColor = Color.FromArgb(34, 197, 94);
            lblOnlineStatus.Location = new Point(28, 50);
            lblOnlineStatus.AutoSize = true;
            lblOnlineStatus.BackColor = Color.Transparent;

            userPanel.Controls.Add(dot);
            userPanel.Controls.Add(lblUserName);
            userPanel.Controls.Add(lblUserRole);
            userPanel.Controls.Add(lblOnlineStatus);

            btnLogout = new Button();
            btnLogout.Text = "  🔓  Log Out";
            btnLogout.Size = new Size(SidebarFullWidth - 20, 46);
            btnLogout.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnLogout.Location = new Point(10, this.Height - 68);
            btnLogout.FlatStyle = FlatStyle.Flat;
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 239, 68, 68);
            btnLogout.Font = new Font("Segoe UI", 10F);
            btnLogout.ForeColor = Color.FromArgb(220, 68, 68);
            btnLogout.BackColor = Color.Transparent;
            btnLogout.TextAlign = ContentAlignment.MiddleLeft;
            btnLogout.Cursor = Cursors.Hand;
            SetRoundRegion(btnLogout, 8);
            AttachRipple(btnLogout, Color.FromArgb(239, 68, 68));
            btnLogout.Click += (s, e) =>
            {
                var r = MessageBox.Show("Are you sure you want to log out?", "Confirm Logout",
                                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r == DialogResult.Yes)
                {
                    SessionManager.Clear();
                    new LoginForm().Show();
                    this.Hide();
                }
            };

            sidebarPanel.Controls.Add(activeIndicator);
            sidebarPanel.Controls.Add(logoPanel);
            sidebarPanel.Controls.Add(dividerTop);
            sidebarPanel.Controls.Add(btnDashboard);
            sidebarPanel.Controls.Add(btnVehicles);
            sidebarPanel.Controls.Add(btnRentals);
            sidebarPanel.Controls.Add(btnDrivers);
            sidebarPanel.Controls.Add(btnTransactions);
            sidebarPanel.Controls.Add(btnReports);
            sidebarPanel.Controls.Add(dividerBottom);
            sidebarPanel.Controls.Add(userPanel);
            sidebarPanel.Controls.Add(btnLogout);

            this.Controls.Add(sidebarPanel);
        }

        // ══════════════════════════════════════════════
        //  HEADER
        // ══════════════════════════════════════════════
        private void BuildHeader()
        {
            headerPanel = new Panel();
            SetDoubleBuffer(headerPanel);
            headerPanel.Height = HeaderHeight;
            headerPanel.Dock = DockStyle.Top;
            headerPanel.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(ThemeManager.CurrentBorder, 1),
                    0, HeaderHeight - 1, headerPanel.Width, HeaderHeight - 1);
            };

            // ── FIX: Custom Drawn Toggle Button (No Text/Flashing) ──
            btnToggleSidebar = new Button();
            btnToggleSidebar.Text = ""; // Walang text!
            btnToggleSidebar.Size = new Size(40, 40);
            btnToggleSidebar.Location = new Point(16, 12);
            btnToggleSidebar.FlatStyle = FlatStyle.Flat;
            btnToggleSidebar.FlatAppearance.BorderSize = 0;
            btnToggleSidebar.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, 128, 128, 128);
            btnToggleSidebar.FlatAppearance.MouseDownBackColor = Color.Transparent; // No weird clicks
            btnToggleSidebar.BackColor = Color.Transparent;
            btnToggleSidebar.Cursor = Cursors.Hand;
            btnToggleSidebar.Click += OnToggleSidebar;
            SetDoubleBuffer(btnToggleSidebar);

            // Manual Draw para walang flash at steady ang lines/arrow
            btnToggleSidebar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using var pen = new Pen(ThemeManager.CurrentText, 2.5f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };

                // Center of the button
                float cx = btnToggleSidebar.Width / 2f;
                float cy = btnToggleSidebar.Height / 2f;
                float w = 8f; // Half width of lines

                if (_sidebarCollapsed)
                {
                    // Draw "Arrow Right"
                    g.DrawLine(pen, cx - 4, cy - 6, cx + 2, cy); // Top angled
                    g.DrawLine(pen, cx - 4, cy + 6, cx + 2, cy); // Bottom angled
                    g.DrawLine(pen, cx - 6, cy, cx + 2, cy); // Center line
                }
                else
                {
                    // Draw "Hamburger" (3 lines)
                    g.DrawLine(pen, cx - w, cy - 6, cx + w, cy - 6);
                    g.DrawLine(pen, cx - w, cy, cx + w, cy);
                    g.DrawLine(pen, cx - w, cy + 6, cx + w, cy + 6);
                }
            };

            lblHeaderTitle = new Label();
            lblHeaderTitle.Text = "Dashboard";
            lblHeaderTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            lblHeaderTitle.AutoSize = true;
            lblHeaderTitle.Location = new Point(66, 18);
            lblHeaderTitle.BackColor = Color.Transparent;

            btnNotifications = new Button();
            btnNotifications.Text = "🔔";
            btnNotifications.Size = new Size(40, 40);
            btnNotifications.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnNotifications.Location = new Point(this.Width - 140, 12);
            btnNotifications.FlatStyle = FlatStyle.Flat;
            btnNotifications.FlatAppearance.BorderSize = 1;
            btnNotifications.Font = new Font("Segoe UI", 12F);
            btnNotifications.Cursor = Cursors.Hand;
            SetRoundRegion(btnNotifications, 20);
            AttachRipple(btnNotifications, ThemeManager.CurrentPrimary);

            btnThemeToggle = new Button();
            btnThemeToggle.Size = new Size(70, 36);
            btnThemeToggle.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnThemeToggle.Location = new Point(this.Width - 90, 14);
            btnThemeToggle.FlatStyle = FlatStyle.Flat;
            btnThemeToggle.FlatAppearance.BorderSize = 0;
            btnThemeToggle.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnThemeToggle.Cursor = Cursors.Hand;
            btnThemeToggle.Text = "";
            btnThemeToggle.BackColor = Color.Transparent;

            float _knobX = ThemeManager.IsDarkMode ? btnThemeToggle.Width - 32 : 4f;
            float _knobTarget = _knobX;
            var _knobTimer = new System.Windows.Forms.Timer { Interval = 12 };

            btnThemeToggle.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(headerPanel.BackColor);

                var rect = new Rectangle(0, 0, btnThemeToggle.Width - 1, btnThemeToggle.Height - 1);
                using var path = GetRoundedRect(rect, 18);

                Color trackColor = ThemeManager.IsDarkMode
                    ? Color.FromArgb(60, 30, 60, 80)
                    : Color.FromArgb(60, 220, 200, 100);
                g.FillPath(new SolidBrush(ThemeManager.CurrentCard), path);
                g.DrawPath(new Pen(ThemeManager.CurrentBorder, 1f), path);

                int circleSize = 28;
                var circleRect = new RectangleF(_knobX, 4, circleSize, circleSize);
                Color circleColor = ThemeManager.IsDarkMode
                    ? ThemeManager.CurrentPrimary
                    : Color.FromArgb(245, 158, 11);

                using (var glow = new GraphicsPath())
                {
                    glow.AddEllipse(circleRect.X - 3, circleRect.Y - 3, circleSize + 6, circleSize + 6);
                    using var gb = new PathGradientBrush(glow);
                    gb.CenterColor = Color.FromArgb(60, circleColor);
                    gb.SurroundColors = new[] { Color.Transparent };
                    g.FillPath(gb, glow);
                }

                g.FillEllipse(new SolidBrush(circleColor), circleRect);

                string icon = ThemeManager.IsDarkMode ? "🌙" : "☀️";
                using var font = new Font("Segoe UI Emoji", 10F);
                var sf = new StringFormat
                { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(icon, font, Brushes.White, circleRect, sf);
            };

            _knobTimer.Tick += (s, e) =>
            {
                float diff = _knobTarget - _knobX;
                if (Math.Abs(diff) < 0.5f)
                {
                    _knobX = _knobTarget;
                    _knobTimer.Stop();
                }
                else
                {
                    _knobX += diff * 0.22f;
                }
                btnThemeToggle.Invalidate();
            };

            btnThemeToggle.Click += (s, e) =>
            {
                ThemeManager.IsDarkMode = !ThemeManager.IsDarkMode;
                _knobTarget = ThemeManager.IsDarkMode ? btnThemeToggle.Width - 32 : 4f;
                _knobTimer.Start();
                ApplyTheme(animated: true);
            };

            headerPanel.Controls.Add(btnToggleSidebar);
            headerPanel.Controls.Add(lblHeaderTitle);
            headerPanel.Controls.Add(btnNotifications);
            headerPanel.Controls.Add(btnThemeToggle);

            this.Controls.Add(headerPanel);
        }

        // ══════════════════════════════════════════════
        //  CONTENT
        // ══════════════════════════════════════════════
        private void BuildContent()
        {
            contentPanel = new Panel();
            SetDoubleBuffer(contentPanel);
            contentPanel.AutoScroll = false;
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.Padding = new Padding(0);

            this.Controls.Add(contentPanel);
            this.Controls.SetChildIndex(contentPanel, 0);
            this.Controls.SetChildIndex(sidebarPanel, 1);
        }

        // ══════════════════════════════════════════════
        //  LOAD PANEL — smooth slide-in FIX
        // ══════════════════════════════════════════════
        public void LoadPanel(UserControl panel)
        {
            contentPanel.Controls.Clear();

            panel.Size = contentPanel.ClientSize;
            panel.BackColor = Color.Transparent;

            panel.Top = 30;
            panel.Left = 0;
            panel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            contentPanel.Controls.Add(panel);

            int targetTop = 0;
            var t = new System.Windows.Forms.Timer { Interval = 15 };
            t.Tick += (s, e) =>
            {
                int diff = targetTop - panel.Top;
                if (Math.Abs(diff) < 2)
                {
                    panel.Top = targetTop;
                    panel.Dock = DockStyle.Fill;
                    t.Stop();
                    t.Dispose();
                }
                else
                {
                    panel.Top += diff / 3;
                }
            };
            t.Start();
        }

        private void ShowPlaceholder(string title, string sub)
        {
            contentPanel.Controls.Clear();

            var card = new Panel();
            SetDoubleBuffer(card);
            card.Dock = DockStyle.Fill;
            card.BackColor = Color.Transparent;

            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(20, 20, 700, 300);
                using var path = GetRoundedRect(rect, 16);

                using var bg = new LinearGradientBrush(rect,
                    Color.FromArgb(ThemeManager.IsDarkMode ? 40 : 80, ThemeManager.CurrentCard),
                    ThemeManager.CurrentBackground,
                    LinearGradientMode.ForwardDiagonal);
                g.FillPath(bg, path);

                using var border = new Pen(Color.FromArgb(60, ThemeManager.CurrentBorder), 1f);
                g.DrawPath(border, path);

                g.DrawLine(new Pen(Color.FromArgb(30, 255, 255, 255), 1), rect.X + 10, rect.Y, rect.Right - 10, rect.Y);
            };

            var lblTitle = new Label();
            lblTitle.Text = title;
            lblTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
            lblTitle.ForeColor = ThemeManager.CurrentText;
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(50, 60);
            lblTitle.BackColor = Color.Transparent;

            card.Controls.Add(lblTitle);
            contentPanel.Controls.Add(card);
        }

        // ══════════════════════════════════════════════
        //  SIDEBAR TOGGLE
        // ══════════════════════════════════════════════
        private void OnToggleSidebar(object sender, EventArgs e)
        {
            _sidebarCollapsed = !_sidebarCollapsed;
            _targetSidebarW = _sidebarCollapsed ? SidebarCollapsedWidth : SidebarFullWidth;

            // Trigger repaint on the button to change icon shape
            btnToggleSidebar.Invalidate();

            AnimateLabelFade(lblLogo, !_sidebarCollapsed);
            AnimateLabelFade(lblLogoSub, !_sidebarCollapsed);
            userPanel.Visible = !_sidebarCollapsed;

            UpdateNavButtonText();
            AnimateSidebar();
        }

        private void AnimateLabelFade(Label lbl, bool show)
        {
            if (show)
            {
                lbl.Visible = true;
            }
            else
            {
                var t = new System.Windows.Forms.Timer { Interval = 120 };
                t.Tick += (s, e2) => { lbl.Visible = false; t.Stop(); t.Dispose(); };
                t.Start();
            }
        }

        private void UpdateNavButtonText()
        {
            var map = new[]
            {
                (btnDashboard,    "📊", "Dashboard"),
                (btnVehicles,     "🚗", "Fleet"),
                (btnRentals,      "📝", "Rentals"),
                (btnDrivers,      "👤", "Drivers"),
                (btnTransactions, "💳", "Transactions"),
                (btnReports,      "📈", "Reports"),
            };

            foreach (var (btn, icon, text) in map)
            {
                if (_sidebarCollapsed)
                {
                    btn.Text = icon;
                    btn.TextAlign = ContentAlignment.MiddleCenter;
                    btn.Padding = new Padding(0);
                    btn.Font = new Font("Segoe UI Emoji", 14F);
                }
                else
                {
                    btn.Text = $"   {icon}   {text}";
                    btn.TextAlign = ContentAlignment.MiddleLeft;
                    btn.Padding = new Padding(0);
                    btn.Font = new Font("Segoe UI", 11F,
                        btn == activeButton ? FontStyle.Bold : FontStyle.Regular);
                }
            }

            btnLogout.Text = _sidebarCollapsed ? "🔓" : "  🔓  Log Out";
            btnLogout.TextAlign = _sidebarCollapsed
                ? ContentAlignment.MiddleCenter
                : ContentAlignment.MiddleLeft;
            btnLogout.Font = _sidebarCollapsed
                ? new Font("Segoe UI Emoji", 14F)
                : new Font("Segoe UI", 10F);
        }

        private void AnimateSidebar()
        {
            _sidebarAnimating = true;
            if (_animTimer == null || !_animTimer.Enabled)
            {
                StartAnimations();
            }
        }

        private void ApplySidebarWidth(int w)
        {
            sidebarPanel.SuspendLayout();
            sidebarPanel.Width = w;

            int btnW = Math.Max(10, w - 20);
            foreach (Control c in sidebarPanel.Controls)
            {
                if (c is Button btn)
                {
                    btn.Width = btnW;
                    btn.Location = new Point(10, btn.Location.Y);
                }
                if (c == dividerTop || c == dividerBottom)
                {
                    int dw = w - 40;
                    if (dw > 0) c.Width = dw;
                }
            }

            sidebarPanel.ResumeLayout();
            sidebarPanel.Invalidate();
        }

        private void RefreshNavButtonRegions()
        {
            var buttons = new[] { btnDashboard, btnVehicles, btnRentals, btnDrivers, btnTransactions, btnReports, btnLogout };
            foreach (var b in buttons)
                SetRoundRegion(b, 8);
        }

        // ══════════════════════════════════════════════
        //  RESPONSIVE
        // ══════════════════════════════════════════════
        private void OnFormResize(object sender, EventArgs e)
        {
            if (this.Width < 1050 && !_sidebarCollapsed)
                OnToggleSidebar(null, null);
            else if (this.Width >= 1050 && _sidebarCollapsed)
                OnToggleSidebar(null, null);

            if (btnNotifications != null) btnNotifications.Left = this.Width - 140;
            if (btnThemeToggle != null) btnThemeToggle.Left = this.Width - 90;
        }

        // ══════════════════════════════════════════════
        //  NAV ACTIVE STATE
        // ══════════════════════════════════════════════
        private void SetActiveButton(Button btn)
        {
            if (activeButton != null)
            {
                activeButton.BackColor = Color.Transparent;
                activeButton.ForeColor = ThemeManager.CurrentText;
                activeButton.Font = _sidebarCollapsed
                    ? new Font("Segoe UI Emoji", 14F)
                    : new Font("Segoe UI", 11F, FontStyle.Regular);
            }

            btn.BackColor = ThemeManager.IsDarkMode
                ? Color.FromArgb(28, 255, 255, 255)
                : Color.FromArgb(18, 0, 0, 0);
            btn.ForeColor = ThemeManager.CurrentPrimary;
            btn.Font = _sidebarCollapsed
                ? new Font("Segoe UI Emoji", 14F, FontStyle.Bold)
                : new Font("Segoe UI", 11F, FontStyle.Bold);
            activeButton = btn;

            string raw = btn.Text
                .Replace("📊", "").Replace("🚗", "").Replace("📝", "")
                .Replace("👤", "").Replace("💳", "").Replace("📈", "")
                .Trim();
            if (lblHeaderTitle != null && !string.IsNullOrWhiteSpace(raw))
                lblHeaderTitle.Text = raw;

            _targetIndicatorY = btn.Top + (btn.Height / 2) - (activeIndicator.Height / 2);
        }

        // ══════════════════════════════════════════════
        //  RIPPLE 
        // ══════════════════════════════════════════════
        private void AttachRipple(Button btn, Color rippleColor)
        {
            btn.MouseDown += (s, e) =>
            {
                if (_ripples.TryGetValue(btn, out var old))
                {
                    old.Timer?.Stop(); old.Timer?.Dispose();
                    _ripples.Remove(btn);
                }

                float maxR = (float)Math.Sqrt(btn.Width * btn.Width + btn.Height * btn.Height) * 0.7f;
                var rs = new RippleState
                {
                    X = e.X,
                    Y = e.Y,
                    Radius = 0,
                    MaxRadius = maxR,
                    Alpha = 180,
                    Timer = new System.Windows.Forms.Timer { Interval = 13 }
                };
                _ripples[btn] = rs;

                rs.Timer.Tick += (ts, te) =>
                {
                    rs.Radius += maxR * 0.1f;
                    rs.Alpha = (int)(180 * (1f - rs.Radius / rs.MaxRadius));
                    if (rs.Radius >= rs.MaxRadius || rs.Alpha <= 0)
                    {
                        rs.Timer.Stop();
                        rs.Timer.Dispose();
                        _ripples.Remove(btn);
                    }
                    btn.Invalidate();
                };
                rs.Timer.Start();
                btn.Invalidate();
            };

            btn.Paint += (s, e) =>
            {
                if (!_ripples.TryGetValue(btn, out var rs)) return;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int alpha = Clamp((int)rs.Alpha);
                using var br = new SolidBrush(Color.FromArgb(alpha, rippleColor));
                e.Graphics.FillEllipse(br,
                    rs.X - rs.Radius, rs.Y - rs.Radius,
                    rs.Radius * 2, rs.Radius * 2);
            };
        }

        private void AttachAllRipples()
        {
            var navBtns = new[] { btnDashboard, btnVehicles, btnRentals, btnDrivers, btnTransactions, btnReports };
            foreach (var b in navBtns)
                AttachRipple(b, ThemeManager.CurrentPrimary);
        }

        private void AttachHoverGlow(Button btn)
        {
            float _glowAlpha = 0f;
            bool _hovering = false;
            var glowTimer = new System.Windows.Forms.Timer { Interval = 12 };

            glowTimer.Tick += (s, e) =>
            {
                float target = _hovering ? 1f : 0f;
                float diff = target - _glowAlpha;
                if (Math.Abs(diff) < 0.02f) { _glowAlpha = target; glowTimer.Stop(); }
                else _glowAlpha += diff * 0.25f;
                btn.Invalidate();
            };

            btn.MouseEnter += (s, e) => { _hovering = true; glowTimer.Start(); };
            btn.MouseLeave += (s, e) => { _hovering = false; glowTimer.Start(); };

            btn.Paint += (s, e) =>
            {
                if (_glowAlpha <= 0.01f) return;
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int alpha = (int)(30 * _glowAlpha);
                using var path = GetRoundedRect(new Rectangle(0, 0, btn.Width, btn.Height), 8);
                using var gb = new PathGradientBrush(path);
                gb.CenterColor = Color.FromArgb(alpha * 2, ThemeManager.CurrentPrimary);
                gb.SurroundColors = new[] { Color.Transparent };
                g.FillPath(gb, path);
            };
        }

        // ══════════════════════════════════════════════
        //  ANIMATIONS
        // ══════════════════════════════════════════════
        private void StartAnimations()
        {
            var navBtns = new[] { btnDashboard, btnVehicles, btnRentals, btnDrivers, btnTransactions, btnReports };
            foreach (var b in navBtns)
            {
                AttachHoverGlow(b);
                AttachRipple(b, ThemeManager.CurrentPrimary);
            }
            AttachRipple(btnLogout, Color.FromArgb(239, 68, 68));
            AttachRipple(btnNotifications, ThemeManager.CurrentPrimary);

            _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animTimer.Tick += (s, e) =>
            {
                if (_opacity < 1f)
                {
                    _opacity += 0.05f;
                    this.Opacity = Math.Min(_opacity, 1f);
                }

                float diff = _targetIndicatorY - _currentIndicatorY;
                if (Math.Abs(diff) > 0.3f)
                {
                    _currentIndicatorY += diff * 0.22f;
                    activeIndicator.Top = (int)_currentIndicatorY;
                    activeIndicator.Invalidate();
                }

                if (_sidebarAnimating)
                {
                    float sd = _targetSidebarW - _currentSidebarW;
                    if (Math.Abs(sd) < 0.5f)
                    {
                        _currentSidebarW = _targetSidebarW;
                        ApplySidebarWidth((int)_currentSidebarW);
                        _sidebarAnimating = false;
                        RefreshNavButtonRegions();
                    }
                    else
                    {
                        _currentSidebarW += sd * 0.28f;
                        ApplySidebarWidth((int)_currentSidebarW);
                    }
                }
            };
            _animTimer.Start();
        }

        // ══════════════════════════════════════════════
        //  PAINT — sidebar
        // ══════════════════════════════════════════════
        private void OnSidebarPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            g.DrawLine(new Pen(ThemeManager.CurrentBorder, 1),
                sidebarPanel.Width - 1, 0,
                sidebarPanel.Width - 1, sidebarPanel.Height);

            if (ThemeManager.IsDarkMode)
            {
                var shimmerRect = new Rectangle(0, 0, sidebarPanel.Width, 120);
                using var shimmerBrush = new LinearGradientBrush(shimmerRect,
                    Color.FromArgb(12, 255, 255, 255),
                    Color.Transparent,
                    LinearGradientMode.Vertical);
                g.FillRectangle(shimmerBrush, shimmerRect);
            }
        }

        // ══════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════
        private Button CreateNavButton(string text, string icon, int y)
        {
            var btn = new Button();
            btn.Text = $"   {icon}   {text}";
            btn.Size = new Size(SidebarFullWidth - 20, 46);
            btn.Location = new Point(10, y);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btn.FlatAppearance.MouseDownBackColor = Color.Transparent;
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.Font = new Font("Segoe UI", 11F);
            btn.BackColor = Color.Transparent;
            btn.Cursor = Cursors.Hand;
            SetRoundRegion(btn, 8);
            return btn;
        }

        private void SetRoundRegion(Control ctrl, int radius)
        {
            ctrl.Region = new Region(GetRoundedRect(
                new Rectangle(0, 0, ctrl.Width, ctrl.Height), radius));
        }

        private GraphicsPath GetRoundedRect(Rectangle b, int r)
        {
            int d = r * 2;
            var arc = new Rectangle(b.Location, new Size(d, d));
            var path = new GraphicsPath();
            path.AddArc(arc, 180, 90);
            arc.X = b.Right - d;
            path.AddArc(arc, 270, 90);
            arc.Y = b.Bottom - d;
            path.AddArc(arc, 0, 90);
            arc.X = b.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static int Clamp(int v, int min = 0, int max = 255)
            => v < min ? min : v > max ? max : v;

        private static void SetDoubleBuffer(Control c)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, c, new object[] { true });
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            _animTimer?.Dispose();
            _sidebarTimer?.Dispose();
            _themeTimer?.Dispose();
            _glassTimer?.Dispose();
            base.Dispose(disposing);
        }
    }
}