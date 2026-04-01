using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DriveAndGo_Admin.Helpers;
using DriveAndGo_Admin.Panels;

namespace DriveAndGo_Admin
{
    public class MainForm : Form
    {
        // ── UI Elements ──
        private Panel sidebarPanel;
        private Panel headerPanel;
        private Panel contentPanel;
        private Panel activeIndicator;

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

        public MainForm()
        {
            InitializeForm();
            BuildSidebar();
            BuildHeader();
            BuildContent();
            ApplyTheme();

            // Load dashboard by default
            LoadPanel(new DashboardPanel());
            SetActiveButton(btnDashboard);
        }

        // ── Form setup ──
        private void InitializeForm()
        {
            this.Size = new Size(1280, 800);
            this.MinimumSize = new Size(1000, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Drive & Go — Vehicle Rental System";
            this.Font = new Font("Segoe UI", 10F);
            this.BackColor = ThemeManager.CurrentBackground;
        }

        // ── Sidebar ──
        private void BuildSidebar()
        {
            sidebarPanel = new Panel();
            sidebarPanel.Width = 260;
            sidebarPanel.Dock = DockStyle.Left;
            sidebarPanel.BackColor = ThemeManager.CurrentSidebar;

            // Logo area
            var logoPanel = new Panel();
            logoPanel.Size = new Size(260, 90);
            logoPanel.Location = new Point(0, 0);
            logoPanel.BackColor = ThemeManager.CurrentSidebar;

            lblLogo = new Label();
            lblLogo.Text = "Drive&Go";
            lblLogo.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            lblLogo.ForeColor = ThemeManager.CurrentPrimary;
            lblLogo.Location = new Point(24, 18);
            lblLogo.AutoSize = true;

            lblLogoSub = new Label();
            lblLogoSub.Text = "Admin Dashboard";
            lblLogoSub.Font = new Font("Segoe UI", 9F);
            lblLogoSub.ForeColor = ThemeManager.CurrentSubText;
            lblLogoSub.Location = new Point(26, 48);
            lblLogoSub.AutoSize = true;

            logoPanel.Controls.Add(lblLogo);
            logoPanel.Controls.Add(lblLogoSub);

            // Divider
            var divider = new Panel();
            divider.Size = new Size(220, 1);
            divider.Location = new Point(20, 90);
            divider.BackColor = ThemeManager.CurrentBorder;

            // Nav section label
            var lblNav = new Label();
            lblNav.Text = "NAVIGATION";
            lblNav.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            lblNav.ForeColor = ThemeManager.CurrentSubText;
            lblNav.Location = new Point(24, 106);
            lblNav.AutoSize = true;

            // Create nav buttons
            btnDashboard = CreateNavButton("Dashboard", "📊", 130);
            btnVehicles = CreateNavButton("Fleet", "🚗", 185);
            btnRentals = CreateNavButton("Rentals", "📝", 240);
            btnDrivers = CreateNavButton("Drivers", "👤", 295);
            btnTransactions = CreateNavButton("Transactions", "💳", 350);
            btnReports = CreateNavButton("Reports", "📈", 405);

            // Wire up click events
            btnDashboard.Click += (s, e) => {
                LoadPanel(new DashboardPanel());
                SetActiveButton(btnDashboard);
                lblHeaderTitle.Text = "Dashboard";
            };
            btnVehicles.Click += (s, e) => {
                LoadPanel(new VehiclesPanel());
                SetActiveButton(btnVehicles);
                lblHeaderTitle.Text = "Fleet Management";
            };
            btnRentals.Click += (s, e) => {
                LoadPanel(new RentalsPanel());
                SetActiveButton(btnRentals);
                lblHeaderTitle.Text = "Rentals & Bookings";
            };
            btnDrivers.Click += (s, e) => {
                LoadPanel(new DriversPanel());
                SetActiveButton(btnDrivers);
                lblHeaderTitle.Text = "Driver Management";
            };
            btnTransactions.Click += (s, e) => {
                LoadPanel(new TransactionsPanel());
                SetActiveButton(btnTransactions);
                lblHeaderTitle.Text = "Transactions";
            };
            btnReports.Click += (s, e) => {
                LoadPanel(new ReportsPanel());
                SetActiveButton(btnReports);
                lblHeaderTitle.Text = "Sales & Reports";
            };

            // User info at bottom
            var userPanel = new Panel();
            userPanel.Size = new Size(260, 80);
            userPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            userPanel.Location = new Point(0, this.Height - 160);
            userPanel.BackColor = ThemeManager.CurrentSidebar;

            var dividerBottom = new Panel();
            dividerBottom.Size = new Size(220, 1);
            dividerBottom.Location = new Point(20, 0);
            dividerBottom.BackColor = ThemeManager.CurrentBorder;

            lblUserName = new Label();
            lblUserName.Text = SessionManager.FullName;
            lblUserName.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblUserName.ForeColor = ThemeManager.CurrentText;
            lblUserName.Location = new Point(24, 16);
            lblUserName.AutoSize = true;

            lblUserRole = new Label();
            lblUserRole.Text = SessionManager.Role.ToUpper();
            lblUserRole.Font = new Font("Segoe UI", 9F);
            lblUserRole.ForeColor = ThemeManager.CurrentAccent;
            lblUserRole.Location = new Point(26, 40);
            lblUserRole.AutoSize = true;

            // Online indicator
            var dot = new Panel();
            dot.Size = new Size(8, 8);
            dot.Location = new Point(10, 22);
            dot.BackColor = ThemeManager.CurrentAccent;

            userPanel.Controls.Add(dividerBottom);
            userPanel.Controls.Add(lblUserName);
            userPanel.Controls.Add(lblUserRole);
            userPanel.Controls.Add(dot);

            // Logout button
            btnLogout = new Button();
            btnLogout.Text = "  🔓  Log Out";
            btnLogout.Size = new Size(260, 50);
            btnLogout.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnLogout.Location = new Point(0, this.Height - 80);
            btnLogout.FlatStyle = FlatStyle.Flat;
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.Font = new Font("Segoe UI", 11F);
            btnLogout.ForeColor = Color.FromArgb(239, 68, 68);
            btnLogout.BackColor = ThemeManager.CurrentSidebar;
            btnLogout.TextAlign = ContentAlignment.MiddleLeft;
            btnLogout.Cursor = Cursors.Hand;
            btnLogout.Click += OnLogout;

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
            headerPanel.Height = 65;
            headerPanel.Dock = DockStyle.Top;
            headerPanel.BackColor = ThemeManager.CurrentBackground;
            headerPanel.Padding = new Padding(20, 0, 20, 0);

            // Bottom border
            headerPanel.Paint += (s, e) => {
                e.Graphics.DrawLine(
                    new Pen(ThemeManager.CurrentBorder, 1),
                    0, headerPanel.Height - 1,
                    headerPanel.Width, headerPanel.Height - 1);
            };

            lblHeaderTitle = new Label();
            lblHeaderTitle.Text = "Dashboard";
            lblHeaderTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            lblHeaderTitle.ForeColor = ThemeManager.CurrentText;
            lblHeaderTitle.AutoSize = true;
            lblHeaderTitle.Location = new Point(24, 18);

            // Date label
            var lblDate = new Label();
            lblDate.Text = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
            lblDate.Font = new Font("Segoe UI", 10F);
            lblDate.ForeColor = ThemeManager.CurrentSubText;
            lblDate.AutoSize = true;
            lblDate.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblDate.Location = new Point(this.Width - 420, 22);

            btnThemeToggle = new Button();
            btnThemeToggle.Text = ThemeManager.IsDarkMode ? "☀️" : "🌙";
            btnThemeToggle.Size = new Size(44, 44);
            btnThemeToggle.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnThemeToggle.Location = new Point(this.Width - 280, 10);
            btnThemeToggle.FlatStyle = FlatStyle.Flat;
            btnThemeToggle.FlatAppearance.BorderColor =
                ThemeManager.CurrentBorder;
            btnThemeToggle.FlatAppearance.BorderSize = 1;
            btnThemeToggle.Font = new Font("Segoe UI", 14F);
            btnThemeToggle.BackColor = ThemeManager.CurrentCard;
            btnThemeToggle.ForeColor = ThemeManager.CurrentText;
            btnThemeToggle.Cursor = Cursors.Hand;
            btnThemeToggle.Click += OnThemeToggle;

            headerPanel.Controls.Add(lblHeaderTitle);
            headerPanel.Controls.Add(lblDate);
            headerPanel.Controls.Add(btnThemeToggle);

            this.Controls.Add(headerPanel);
        }

        // ── Content area ──
        private void BuildContent()
        {
            contentPanel = new Panel();
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.BackColor = ThemeManager.CurrentBackground;
            contentPanel.Padding = new Padding(24);

            this.Controls.Add(contentPanel);
        }

        // ── Load panel into content area ──
        public void LoadPanel(UserControl panel)
        {
            contentPanel.Controls.Clear();
            panel.Dock = DockStyle.Fill;
            contentPanel.Controls.Add(panel);
        }

        // ── Highlight active nav button ──
        private void SetActiveButton(Button btn)
        {
            // Reset all buttons
            foreach (Control c in sidebarPanel.Controls)
            {
                if (c is Button b && b != btnLogout)
                {
                    b.BackColor = ThemeManager.CurrentSidebar;
                    b.ForeColor = ThemeManager.CurrentText;
                    b.Font = new Font("Segoe UI", 11F, FontStyle.Regular);
                }
            }

            // Highlight selected
            btn.BackColor = Color.FromArgb(
                ThemeManager.IsDarkMode ? 40 : 230,
                ThemeManager.IsDarkMode ? 40 : 230,
                ThemeManager.IsDarkMode ? 80 : 255);
            btn.ForeColor = ThemeManager.CurrentPrimary;
            btn.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            activeButton = btn;
        }

        // ── Create nav button helper ──
        private Button CreateNavButton(string text, string icon, int y)
        {
            var btn = new Button();
            btn.Text = $"  {icon}  {text}";
            btn.Size = new Size(260, 50);
            btn.Location = new Point(0, y);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.Font = new Font("Segoe UI", 11F);
            btn.ForeColor = ThemeManager.CurrentText;
            btn.BackColor = ThemeManager.CurrentSidebar;
            btn.Cursor = Cursors.Hand;

            // Hover effect
            btn.MouseEnter += (s, e) => {
                if (btn != activeButton)
                    btn.BackColor = ThemeManager.CurrentCard;
            };
            btn.MouseLeave += (s, e) => {
                if (btn != activeButton)
                    btn.BackColor = ThemeManager.CurrentSidebar;
            };

            return btn;
        }

        // ── Theme toggle ──
        private void OnThemeToggle(object sender, EventArgs e)
        {
            ThemeManager.IsDarkMode = !ThemeManager.IsDarkMode;
            btnThemeToggle.Text =
                ThemeManager.IsDarkMode ? "☀️" : "🌙";
            ApplyTheme();

            // Reload current panel with new theme
            if (contentPanel.Controls.Count > 0 &&
                contentPanel.Controls[0] is UserControl panel)
            {
                var type = panel.GetType();
                var newPanel = (UserControl)Activator
                    .CreateInstance(type)!;
                LoadPanel(newPanel);
            }
        }

        // ── Apply theme to all controls ──
        private void ApplyTheme()
        {
            this.BackColor = ThemeManager.CurrentBackground;
            sidebarPanel.BackColor = ThemeManager.CurrentSidebar;
            headerPanel.BackColor = ThemeManager.CurrentBackground;
            contentPanel.BackColor = ThemeManager.CurrentBackground;

            lblHeaderTitle.ForeColor = ThemeManager.CurrentText;
            lblLogo.ForeColor = ThemeManager.CurrentPrimary;
            lblLogoSub.ForeColor = ThemeManager.CurrentSubText;

            if (lblUserName != null)
                lblUserName.ForeColor = ThemeManager.CurrentText;
            if (lblUserRole != null)
                lblUserRole.ForeColor = ThemeManager.CurrentAccent;

            btnThemeToggle.BackColor = ThemeManager.CurrentCard;
            btnThemeToggle.ForeColor = ThemeManager.CurrentText;

            // Re-apply nav buttons
            foreach (Control c in sidebarPanel.Controls)
            {
                if (c is Button b && b != btnLogout && b != activeButton)
                {
                    b.BackColor = ThemeManager.CurrentSidebar;
                    b.ForeColor = ThemeManager.CurrentText;
                }
            }

            btnLogout.BackColor = ThemeManager.CurrentSidebar;
            headerPanel.Invalidate();
        }

        // ── Logout ──
        private void OnLogout(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to log out?",
                "Confirm Logout",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                SessionManager.Clear();
                var login = new LoginForm();
                login.Show();
                this.Close();
            }
        }
    }
}