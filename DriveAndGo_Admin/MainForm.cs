using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection; // Para sa Double Buffering
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DriveAndGo_Admin
{
    public class MainForm : Form
    {
        // ── Aesthetic Color Palette ──
        private readonly Color ColBg = Color.FromArgb(10, 10, 18);
        private readonly Color ColSidebar = Color.FromArgb(16, 16, 28);
        private readonly Color ColCard = Color.FromArgb(22, 22, 35);
        private readonly Color ColAccent = Color.FromArgb(230, 81, 0);
        private readonly Color ColText = Color.FromArgb(240, 240, 255);
        private readonly Color ColSubText = Color.FromArgb(100, 100, 140);
        private readonly Color ColBorder = Color.FromArgb(30, 30, 45);

        // ── UI Elements ──
        private Panel sidebarPanel;
        private Panel headerPanel;
        private Panel contentPanel;
        private Panel activeIndicator; // Smooth sliding line

        private PictureBox picLogo; // <--- Idinagdag para sa Logo
        private Label lblLogo;
        private Label lblLogoSub;
        private Label lblHeaderTitle;
        private Label lblUserName;
        private Label lblUserRole;
        private Button btnThemeToggle;
        private Button activeButton;

        // ── Nav Buttons ──
        private Button btnDashboard;
        private Button btnVehicles;
        private Button btnRentals;
        private Button btnDrivers;
        private Button btnTransactions;
        private Button btnReports;
        private Button btnLogout;

        // ── Animations ──
        private System.Windows.Forms.Timer _animTimer;
        private float _targetIndicatorY = 150; // In-adjust para sa bagong logo height
        private float _currentIndicatorY = 150;
        private float _opacity = 0f;

        // ==========================================
        // 3D EFFECT: FORM DROP SHADOW
        // ==========================================
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        public MainForm()
        {
            // Optimize Rendering
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint, true);
            this.UpdateStyles();

            InitializeForm();
            BuildSidebar();
            BuildHeader();
            BuildContent();

            // Setup Animations
            StartAnimations();

            // Default View
            SetActiveButton(btnDashboard);
            ShowCardPanel("📊 Dashboard Stats", "Welcome back! Here's your overview for today.");
        }

        private void EnableDoubleBuffering(Control control)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, control, new object[] { true });
        }

        // ── Form setup ──
        private void InitializeForm()
        {
            this.Size = new Size(1280, 800);
            this.MinimumSize = new Size(1000, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Drive & Go — Admin Dashboard";
            this.Font = new Font("Segoe UI", 10F);
            this.BackColor = ColBg;
            this.Opacity = 0; // Starts hidden for fade-in
        }

        // ── Sidebar ──
        private void BuildSidebar()
        {
            sidebarPanel = new Panel();
            EnableDoubleBuffering(sidebarPanel);
            sidebarPanel.Width = 260;
            sidebarPanel.Dock = DockStyle.Left;
            sidebarPanel.BackColor = ColSidebar;
            sidebarPanel.Paint += OnSidebarPaint; // Adds 3D shadow on right edge

            // ── Logo Area ──
            var logoPanel = new Panel { Size = new Size(260, 100), Location = new Point(0, 0), BackColor = Color.Transparent };

            picLogo = new PictureBox();
            picLogo.Size = new Size(50, 50);
            picLogo.Location = new Point(15, 25);
            picLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picLogo.BackColor = Color.Transparent;
            try
            {
                picLogo.Image = Properties.Resources.DriveAndGo_Logo;
            }
            catch { }

            lblLogo = new Label { Text = "Drive&Go", Font = new Font("Segoe UI", 16F, FontStyle.Bold), ForeColor = ColAccent, Location = new Point(70, 25), AutoSize = true, BackColor = Color.Transparent };
            lblLogoSub = new Label { Text = "Admin Dashboard", Font = new Font("Segoe UI", 9F), ForeColor = ColSubText, Location = new Point(72, 52), AutoSize = true, BackColor = Color.Transparent };

            logoPanel.Controls.Add(picLogo);
            logoPanel.Controls.Add(lblLogo);
            logoPanel.Controls.Add(lblLogoSub);

            var divider = new Panel { Size = new Size(220, 1), Location = new Point(20, 100), BackColor = ColBorder };
            var lblNav = new Label { Text = "NAVIGATION", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = ColSubText, Location = new Point(24, 120), AutoSize = true, BackColor = Color.Transparent };

            // Create smooth sliding indicator
            activeIndicator = new Panel();
            activeIndicator.Size = new Size(4, 30);
            activeIndicator.Location = new Point(0, 150);
            activeIndicator.BackColor = ColAccent;
            // Add a subtle glow to the indicator
            activeIndicator.Paint += (s, e) => {
                var g = e.Graphics;
                using var blur = new Pen(Color.FromArgb(100, ColAccent), 2f);
                g.DrawRectangle(blur, 0, 0, activeIndicator.Width, activeIndicator.Height);
            };

            // Nav buttons
            btnDashboard = CreateNavButton("Dashboard", "📊", 150);
            btnVehicles = CreateNavButton("Fleet Map (3D)", "🚗", 205);
            btnRentals = CreateNavButton("Rentals", "📝", 260);
            btnDrivers = CreateNavButton("Drivers", "👤", 315);
            btnTransactions = CreateNavButton("Transactions", "💳", 370);
            btnReports = CreateNavButton("Reports", "📈", 425);

            // Wire up click events
            btnDashboard.Click += (s, e) => { SetActiveButton(btnDashboard); ShowCardPanel("📊 Dashboard Stats", "Live overview of your fleet."); };
            btnVehicles.Click += (s, e) => { SetActiveButton(btnVehicles); ShowCardPanel("🚗 Fleet Management", "3D Honda Civic tracking will appear here."); };
            btnRentals.Click += (s, e) => { SetActiveButton(btnRentals); ShowCardPanel("📝 Rentals & Bookings", "Manage active and upcoming reservations."); };
            btnDrivers.Click += (s, e) => { SetActiveButton(btnDrivers); ShowCardPanel("👤 Drivers List", "Verify and manage driver profiles."); };
            btnTransactions.Click += (s, e) => { SetActiveButton(btnTransactions); ShowCardPanel("💳 Transactions", "Review payments and invoices."); };
            btnReports.Click += (s, e) => { SetActiveButton(btnReports); ShowCardPanel("📈 Sales & Reports", "Analytics and financial charts."); };

            // User Profile Area
            var userPanel = new Panel { Size = new Size(260, 80), Anchor = AnchorStyles.Bottom | AnchorStyles.Left, Location = new Point(0, this.Height - 160), BackColor = Color.Transparent };
            var dividerBottom = new Panel { Size = new Size(220, 1), Location = new Point(20, 0), BackColor = ColBorder };

            lblUserName = new Label { Text = "Raymart Quirante", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = ColText, Location = new Point(24, 16), AutoSize = true };
            lblUserRole = new Label { Text = "SUPER ADMIN", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = ColAccent, Location = new Point(26, 40), AutoSize = true };
            var dot = new Panel { Size = new Size(8, 8), Location = new Point(10, 20), BackColor = Color.FromArgb(34, 197, 94) }; // Green online dot

            // Add smooth circle to dot
            dot.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillEllipse(new SolidBrush(dot.BackColor), 0, 0, dot.Width - 1, dot.Height - 1);
            };

            userPanel.Controls.Add(dividerBottom);
            userPanel.Controls.Add(lblUserName);
            userPanel.Controls.Add(lblUserRole);
            userPanel.Controls.Add(dot);

            // Logout button
            btnLogout = new Button { Text = "  🔓  Log Out", Size = new Size(260, 50), Anchor = AnchorStyles.Bottom | AnchorStyles.Left, Location = new Point(0, this.Height - 80), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.FromArgb(239, 68, 68), BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft, Cursor = Cursors.Hand };
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, 239, 68, 68); // Soft red hover
            btnLogout.Click += (s, e) => { Application.Exit(); };

            sidebarPanel.Controls.Add(activeIndicator);
            sidebarPanel.Controls.Add(logoPanel);
            sidebarPanel.Controls.Add(divider);
            sidebarPanel.Controls.Add(lblNav);
            sidebarPanel.Controls.Add(btnDashboard);
            sidebarPanel.Controls.Add(btnVehicles);
            sidebarPanel.Controls.Add(btnRentals);
            sidebarPanel.Controls.Add(btnDrivers);
            sidebarPanel.Controls.Add(btnTransactions);
            sidebarPanel.Controls.Add(btnReports);
            sidebarPanel.Controls.Add(userPanel);
            sidebarPanel.Controls.Add(btnLogout);

            this.Controls.Add(sidebarPanel);
        }

        // ── Header ──
        private void BuildHeader()
        {
            headerPanel = new Panel();
            EnableDoubleBuffering(headerPanel);
            headerPanel.Height = 70;
            headerPanel.Dock = DockStyle.Top;
            headerPanel.BackColor = ColBg;
            headerPanel.Padding = new Padding(20, 0, 20, 0);

            // Draw Bottom Border and subtle shadow
            headerPanel.Paint += (s, e) => {
                var g = e.Graphics;
                g.DrawLine(new Pen(ColBorder, 1), 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
            };

            lblHeaderTitle = new Label { Text = "Dashboard", Font = new Font("Segoe UI", 18F, FontStyle.Bold), ForeColor = ColText, AutoSize = true, Location = new Point(24, 20) };

            var lblDate = new Label { Text = DateTime.Now.ToString("dddd, MMMM dd, yyyy"), Font = new Font("Segoe UI", 10F), ForeColor = ColSubText, AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(this.Width - 440, 26) };

            btnThemeToggle = new Button { Text = "🌙", Size = new Size(40, 40), Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(this.Width - 290, 15), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 14F), BackColor = ColCard, ForeColor = ColText, Cursor = Cursors.Hand };
            btnThemeToggle.FlatAppearance.BorderColor = ColBorder;
            btnThemeToggle.FlatAppearance.BorderSize = 1;
            btnThemeToggle.Region = new Region(GetRoundedRect(new Rectangle(0, 0, 40, 40), 20)); // Circular button

            headerPanel.Controls.Add(lblHeaderTitle);
            headerPanel.Controls.Add(lblDate);
            headerPanel.Controls.Add(btnThemeToggle);

            this.Controls.Add(headerPanel);
        }

        // ── Content area ──
        private void BuildContent()
        {
            contentPanel = new Panel();
            EnableDoubleBuffering(contentPanel);
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.BackColor = ColBg;
            contentPanel.Padding = new Padding(30);

            // Inner shadow effect top-left
            contentPanel.Paint += (s, e) => {
                var g = e.Graphics;
                using var shadowPen = new Pen(Color.FromArgb(40, 0, 0, 0), 4);
                g.DrawLine(shadowPen, 0, 0, 0, contentPanel.Height); // Left shadow
                g.DrawLine(shadowPen, 0, 0, contentPanel.Width, 0);  // Top shadow
            };

            this.Controls.Add(contentPanel);
            this.Controls.SetChildIndex(contentPanel, 0);
            this.Controls.SetChildIndex(sidebarPanel, 1); // Ensure sidebar is above content
        }

        // ── 3D Card Panel Loader ──
        private void ShowCardPanel(string title, string subtitle)
        {
            contentPanel.Controls.Clear();

            // The main card
            var card = new Panel();
            EnableDoubleBuffering(card);
            card.Size = new Size(800, 400);
            card.Location = new Point(40, 40);
            card.BackColor = ColBg; // We paint it manually below

            // Float up animation for the card
            int offset = 20;
            card.Top += offset;
            var cardTimer = new System.Windows.Forms.Timer { Interval = 16 };
            cardTimer.Tick += (s, e) => {
                if (offset > 0)
                {
                    offset -= 2;
                    card.Top -= 2;
                }
                else
                {
                    cardTimer.Stop();
                    cardTimer.Dispose();
                }
            };
            cardTimer.Start();

            // Custom Paint for 3D Card Look
            card.Paint += (s, e) => {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(5, 5, card.Width - 15, card.Height - 15);
                using var path = GetRoundedRect(rect, 15);

                // Drop Shadow
                for (int i = 5; i >= 1; i--)
                {
                    using var shadowPath = GetRoundedRect(new Rectangle(rect.X + i, rect.Y + i, rect.Width, rect.Height), 15);
                    using var shadowBrush = new SolidBrush(Color.FromArgb(10 * (6 - i), 0, 0, 0));
                    g.FillPath(shadowBrush, shadowPath);
                }

                // Card Gradient Background
                using var brush = new LinearGradientBrush(rect, ColCard, Color.FromArgb(18, 18, 28), LinearGradientMode.ForwardDiagonal);
                g.FillPath(brush, path);

                // Subtle Top Border Highlight
                using var highlightPen = new Pen(Color.FromArgb(40, 255, 255, 255), 1f);
                g.DrawArc(highlightPen, rect.X, rect.Y, 30, 30, 180, 90);
                g.DrawLine(highlightPen, rect.X + 15, rect.Y, rect.Right - 15, rect.Y);
            };

            var lblTitle = new Label { Text = title, Font = new Font("Segoe UI", 20F, FontStyle.Bold), ForeColor = ColText, AutoSize = true, Location = new Point(40, 40), BackColor = Color.Transparent };
            var lblSub = new Label { Text = subtitle, Font = new Font("Segoe UI", 12F), ForeColor = ColSubText, AutoSize = true, Location = new Point(42, 80), BackColor = Color.Transparent };

            card.Controls.Add(lblTitle);
            card.Controls.Add(lblSub);

            contentPanel.Controls.Add(card);
        }

        // ── Sliding Navigation & Highlight ──
        private void SetActiveButton(Button btn)
        {
            if (activeButton == btn) return;

            if (activeButton != null)
            {
                activeButton.BackColor = Color.Transparent;
                activeButton.ForeColor = ColText;
                activeButton.Font = new Font("Segoe UI", 11F, FontStyle.Regular);
            }

            btn.BackColor = Color.FromArgb(10, 255, 255, 255); // Very subtle glass hover
            btn.ForeColor = ColAccent;
            btn.Font = new Font("Segoe UI", 11F, FontStyle.Bold);

            activeButton = btn;
            lblHeaderTitle.Text = btn.Text.Replace("   ", "").Trim(); // Clean text for header

            // Set Target for smooth slide
            _targetIndicatorY = btn.Top + 10;
        }

        // ── Custom Paint Handlers ──
        private void OnSidebarPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Subtle Drop Shadow on right edge
            using var shadowBrush = new LinearGradientBrush(
                new Rectangle(sidebarPanel.Width - 10, 0, 10, sidebarPanel.Height),
                Color.FromArgb(0, 0, 0, 0), Color.FromArgb(40, 0, 0, 0), LinearGradientMode.Horizontal);
            g.FillRectangle(shadowBrush, sidebarPanel.Width - 10, 0, 10, sidebarPanel.Height);

            // Border line
            g.DrawLine(new Pen(ColBorder, 1), sidebarPanel.Width - 1, 0, sidebarPanel.Width - 1, sidebarPanel.Height);
        }

        private Button CreateNavButton(string text, string icon, int y)
        {
            var btn = new Button();
            btn.Text = $"   {icon}   {text}";
            btn.Size = new Size(240, 50); // Slightly smaller than sidebar for padding
            btn.Location = new Point(10, y);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(15, 255, 255, 255);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(5, 255, 255, 255);
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.Font = new Font("Segoe UI", 11F);
            btn.ForeColor = ColText;
            btn.BackColor = Color.Transparent;
            btn.Cursor = Cursors.Hand;

            // Round the button slightly
            btn.Region = new Region(GetRoundedRect(new Rectangle(0, 0, btn.Width, btn.Height), 8));

            return btn;
        }

        // ── Universal Animations ──
        private void StartAnimations()
        {
            _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animTimer.Tick += (s, e) =>
            {
                // Form Fade-in
                if (_opacity < 1f)
                {
                    _opacity += 0.05f;
                    this.Opacity = _opacity;
                }

                // Smooth Sliding Indicator (Lerp interpolation)
                if (Math.Abs(_currentIndicatorY - _targetIndicatorY) > 0.5f)
                {
                    _currentIndicatorY += (_targetIndicatorY - _currentIndicatorY) * 0.2f;
                    activeIndicator.Top = (int)_currentIndicatorY;
                }
                else
                {
                    activeIndicator.Top = (int)_targetIndicatorY;
                }
            };
            _animTimer.Start();
        }

        // ── Helpers ──
        private GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var size = new Size(diameter, diameter);
            var arc = new Rectangle(bounds.Location, size);
            var path = new GraphicsPath();

            if (radius == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            // Top left arc
            path.AddArc(arc, 180, 90);
            // Top right arc
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            // Bottom right arc
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            // Bottom left arc
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}