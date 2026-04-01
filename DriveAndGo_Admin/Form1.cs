using System;
using System.Drawing;
using System.Windows.Forms;

namespace DriveAndGo_Admin
{
    public partial class Form1 : Form
    {
        //I-declare ang mga UI elements manually
        private Panel sidebarPanel;
        private Panel headerPanel;
        private Panel mainContentPanel;

        private Button btnDashboard;
        private Button btnVehicles;
        private Button btnRentals;
        private Button btnIssues;
        private Button btnMessages;
        private Button btnReports;
        private Button btnLogout;
        private Button btnThemeToggle;

        private Label lblHeaderTitle;
        private Label lblLogo;

        public Form1()
        {
            
            SetupManualUI();
            ApplyTheme();
        }

        // Dito natin bubuuin ang UI manually (Walang Drag & Drop designer)
        private void SetupManualUI()
        {
            // Set Form Properties
            this.Size = new Size(1200, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Drive & Go - Admin Dashboard";
            this.Font = new Font("Segoe UI", 10F, FontStyle.Regular);

            // ══ HEADER PANEL (Top) ══
            headerPanel = new Panel();
            headerPanel.Height = 70;
            headerPanel.Dock = DockStyle.Top;
            headerPanel.Padding = new Padding(20, 0, 20, 0);

            lblHeaderTitle = new Label();
            lblHeaderTitle.Text = "📊 DASHBOARD";
            lblHeaderTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            lblHeaderTitle.AutoSize = true;
            lblHeaderTitle.Location = new Point(20, 20);
            headerPanel.Controls.Add(lblHeaderTitle);

            btnThemeToggle = new Button();
            btnThemeToggle.Text = "🌙 Toggle Theme";
            btnThemeToggle.Size = new Size(160, 45);
            btnThemeToggle.Location = new Point(this.Width - 200, 12);
            btnThemeToggle.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnThemeToggle.FlatStyle = FlatStyle.Flat;
            btnThemeToggle.FlatAppearance.BorderSize = 0;
            btnThemeToggle.Cursor = Cursors.Hand;
            btnThemeToggle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            btnThemeToggle.Click += BtnThemeToggle_Click;
            headerPanel.Controls.Add(btnThemeToggle);

            // ══ SIDEBAR PANEL (Left) ══
            sidebarPanel = new Panel();
            sidebarPanel.Width = 260;
            sidebarPanel.Dock = DockStyle.Left;
            sidebarPanel.Padding = new Padding(0, 0, 0, 20);

            lblLogo = new Label();
            lblLogo.Text = "Drive & Go";
            lblLogo.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
            lblLogo.AutoSize = true;
            lblLogo.Location = new Point(45, 25);
            sidebarPanel.Controls.Add(lblLogo);

            // Setup Navigation Buttons manually
            int startY = 100;
            int spacingY = 55;
            btnDashboard = CreateNavButton("📊 Dashboard", startY);
            btnVehicles = CreateNavButton("🚗 Vehicles Fleet", startY + spacingY);
            btnRentals = CreateNavButton("📝 Rentals & Bookings", startY + spacingY * 2);
            btnIssues = CreateNavButton("🔧 Incident Reports", startY + spacingY * 3);
            btnMessages = CreateNavButton("💬 Live Chat Support", startY + spacingY * 4);
            btnReports = CreateNavButton("📈 Sales & Reports", startY + spacingY * 5);

            // Logout button sa baba
            btnLogout = CreateNavButton("🔓 Log Out", this.Height - 130);
            btnLogout.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnLogout.Click += BtnLogout_Click;

            sidebarPanel.Controls.Add(btnDashboard);
            sidebarPanel.Controls.Add(btnVehicles);
            sidebarPanel.Controls.Add(btnRentals);
            sidebarPanel.Controls.Add(btnIssues);
            sidebarPanel.Controls.Add(btnMessages);
            sidebarPanel.Controls.Add(btnReports);
            sidebarPanel.Controls.Add(btnLogout);

            // ══ MAIN CONTENT PANEL (Center) ══
            mainContentPanel = new Panel();
            mainContentPanel.Dock = DockStyle.Fill; // Sasakupin nito ang natitirang space
            mainContentPanel.Padding = new Padding(30);

            // Placeholder content para sa Dashboard
            Label lblWelcome = new Label();
            lblWelcome.Text = "Welcome, Super Admin!";
            lblWelcome.Font = new Font("Segoe UI", 24F, FontStyle.Bold);
            lblWelcome.AutoSize = true;
            lblWelcome.ForeColor = ThemeManager.DarkText;
            mainContentPanel.Controls.Add(lblWelcome);

            // Idagdag lahat sa mismong Form (Importante ang pagkakasunod-sunod)
            this.Controls.Add(mainContentPanel);
            this.Controls.Add(headerPanel);
            this.Controls.Add(sidebarPanel);
        }

        // Helper function para madaling gumawa ng menu buttons manually
        private Button CreateNavButton(string text, int yPosition)
        {
            Button btn = new Button();
            btn.Text = "   " + text;
            btn.Size = new Size(260, 50);
            btn.Location = new Point(0, yPosition);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.Font = new Font("Segoe UI", 12F, FontStyle.Regular);
            btn.Cursor = Cursors.Hand;
            return btn;
        }

        // 3. Logic para sa pag-switch ng Light at Dark mode
        private void BtnThemeToggle_Click(object sender, EventArgs e)
        {
            ThemeManager.IsDarkMode = !ThemeManager.IsDarkMode;
            btnThemeToggle.Text = ThemeManager.IsDarkMode ? "☀️ Light Mode" : "🌙 Dark Mode";
            ApplyTheme();
        }

        private void BtnLogout_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("Are you sure you want to log out?", "Confirm Log Out", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                // Buksan ulit ang login form at isara itong dashboard
                LoginForm loginForm = new LoginForm();
                loginForm.Show();
                this.Hide(); // Pansamantalang itago muna ito
            }
        }

        //  I-apply ang kulay sa lahat ng ginawa natin (Manually coded theme apply)
        private void ApplyTheme()
        {
            this.BackColor = ThemeManager.CurrentBackground;

            sidebarPanel.BackColor = ThemeManager.CurrentSidebar;
            headerPanel.BackColor = ThemeManager.CurrentBackground; // Header takes bg color
            mainContentPanel.BackColor = ThemeManager.CurrentBackground;

            lblHeaderTitle.ForeColor = ThemeManager.CurrentText;
            foreach (Control c in sidebarPanel.Controls)
            {
                if (c is Button btn)
                {
                    btn.ForeColor = ThemeManager.CurrentText;
                    btn.BackColor = ThemeManager.CurrentSidebar;
                }
                else if (c is Label lbl)
                {
                    lbl.ForeColor = ThemeManager.CurrentText;
                }
            }

            btnThemeToggle.ForeColor = ThemeManager.CurrentText;
            btnThemeToggle.BackColor = ThemeManager.CurrentSidebar;

            // Placeholder para sa mga labels sa main content
            foreach (Control c in mainContentPanel.Controls)
            {
                if (c is Label lbl)
                {
                    lbl.ForeColor = ThemeManager.CurrentText;
                }
            }
        }
    }
}