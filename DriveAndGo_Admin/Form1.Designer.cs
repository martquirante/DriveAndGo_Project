using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DriveAndGo_Admin
{
    public partial class Form1 : Form
    {
        // UI Elements
        private Panel sidePanel;
        private Panel topPanel;
        private Panel mainContentPanel;
        private DataGridView dgvMain;
        private Label lblHeaderTitle;

        // Sidebar Buttons
        private Button btnDashboard;
        private Button btnRentals;
        private Button btnVehicles;

        // Custom Colors (Dark Theme + Orange Accent matching your Logo)
        private Color colorBg = Color.FromArgb(27, 27, 41);       // Main background
        private Color colorSidebar = Color.FromArgb(21, 21, 33);  // Darker sidebar
        private Color colorCard = Color.FromArgb(39, 41, 61);     // Panel cards
        private Color colorText = Color.FromArgb(224, 224, 224);  // White/Gray text
        private Color colorPrimary = Color.FromArgb(230, 81, 0);  // Orange highlight (Updated!)

        public Form1()
        {
            InitializeComponent();
            SetupModernDarkUI();
        }

        // ==========================================
        // MODERN DARK UI SETUP
        // ==========================================
        private void SetupModernDarkUI()
        {
            this.Text = "Drive & Go: Vehicle Rental System";
            this.Size = new Size(1100, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = colorBg;
            this.Font = new Font("Segoe UI", 10);

            // 1. SIDEBAR SETUP
            sidePanel = new Panel();
            sidePanel.Size = new Size(220, this.ClientSize.Height);
            sidePanel.Dock = DockStyle.Left;
            sidePanel.BackColor = colorSidebar;
            this.Controls.Add(sidePanel);

            // App Logo (Image - Updated!)
            PictureBox pbLogo = new PictureBox();
            // Siguraduhin na ang pangalan dito ay tugma sa pangalan ng in-add mo sa Resources
            pbLogo.Image = Properties.Resources.DriveAndGo_Logo;
            pbLogo.SizeMode = PictureBoxSizeMode.Zoom;
            pbLogo.Size = new Size(180, 80);
            pbLogo.Location = new Point(20, 20);
            sidePanel.Controls.Add(pbLogo);

            // User Profile sa ibaba
            Label lblUser = new Label();
            lblUser.Text = "👤 Raymart Quirante\n🟢 Online";
            lblUser.ForeColor = colorText;
            lblUser.Font = new Font("Segoe UI", 10);
            lblUser.AutoSize = true;
            lblUser.Location = new Point(20, this.ClientSize.Height - 80);
            lblUser.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            sidePanel.Controls.Add(lblUser);

            // Sidebar Buttons (Inusog natin pababa para sa logo)
            btnDashboard = CreateSidebarButton("📊 Dashboard Stats", 120);
            btnRentals = CreateSidebarButton("📄 Transaction Logs", 170);
            btnVehicles = CreateSidebarButton("🚐 Live Fleet Map", 220);

            sidePanel.Controls.Add(btnDashboard);
            sidePanel.Controls.Add(btnRentals);
            sidePanel.Controls.Add(btnVehicles);

            // 2. MAIN CONTENT AREA
            mainContentPanel = new Panel();
            mainContentPanel.Location = new Point(220, 0);
            mainContentPanel.Size = new Size(this.ClientSize.Width - 220, this.ClientSize.Height);
            mainContentPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.Controls.Add(mainContentPanel);

            // Header Title
            lblHeaderTitle = new Label();
            lblHeaderTitle.Text = "DASHBOARD STATS\n📅 " + DateTime.Now.ToString("dddd, MMMM dd, yyyy");
            lblHeaderTitle.ForeColor = Color.White;
            lblHeaderTitle.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            lblHeaderTitle.AutoSize = true;
            lblHeaderTitle.Location = new Point(30, 25);
            mainContentPanel.Controls.Add(lblHeaderTitle);

            // 3. SUMMARY CARDS
            CreateSummaryCard("TOTAL REVENUE", "₱0.00", Color.FromArgb(46, 204, 113), 30, 90);
            CreateSummaryCard("ACTIVE RENTALS", "0", Color.FromArgb(52, 152, 219), 230, 90);
            CreateSummaryCard("TOTAL CUSTOMERS", "0", Color.FromArgb(241, 196, 15), 430, 90);
            CreateSummaryCard("FLEET SIZE", "0", Color.FromArgb(231, 76, 60), 630, 90);

            // 4. MAIN TABLE (Para sa listahan)
            dgvMain = new DataGridView();
            dgvMain.Location = new Point(30, 220);
            dgvMain.Size = new Size(800, 400);
            dgvMain.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvMain.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvMain.AllowUserToAddRows = false;
            dgvMain.ReadOnly = true;
            dgvMain.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            // Dark Theme Table Styling
            dgvMain.BackgroundColor = colorCard;
            dgvMain.BorderStyle = BorderStyle.None;
            dgvMain.EnableHeadersVisualStyles = false;
            dgvMain.ColumnHeadersDefaultCellStyle.BackColor = colorSidebar;
            dgvMain.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvMain.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            dgvMain.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            dgvMain.DefaultCellStyle.BackColor = colorCard;
            dgvMain.DefaultCellStyle.ForeColor = colorText;
            dgvMain.DefaultCellStyle.SelectionBackColor = colorPrimary;
            dgvMain.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvMain.RowHeadersVisible = false;
            dgvMain.RowTemplate.Height = 40;
            dgvMain.GridColor = colorSidebar;

            mainContentPanel.Controls.Add(dgvMain);

            // Trigger default view
            BtnDashboard_Click(btnDashboard, null);
        }

        // ==========================================
        // UI HELPERS & ANIMATIONS
        // ==========================================
        private Button CreateSidebarButton(string text, int yPos)
        {
            Button btn = new Button();
            btn.Text = "  " + text;
            btn.Size = new Size(220, 50);
            btn.Location = new Point(0, yPos);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.ForeColor = colorText;
            btn.Font = new Font("Segoe UI", 11);
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.Cursor = Cursors.Hand;

            // HOVER ANIMATIONS
            btn.MouseEnter += (s, e) => { if (btn.BackColor != colorPrimary) btn.BackColor = colorCard; };
            btn.MouseLeave += (s, e) => { if (btn.BackColor != colorPrimary) btn.BackColor = colorSidebar; };

            // CLICK EVENT ROUTING
            if (text.Contains("Dashboard")) btn.Click += BtnDashboard_Click;
            if (text.Contains("Transaction")) btn.Click += BtnRentals_Click;
            if (text.Contains("Fleet")) btn.Click += BtnVehicles_Click;

            return btn;
        }

        private void CreateSummaryCard(string title, string value, Color accentColor, int xPos, int yPos)
        {
            Panel card = new Panel();
            card.Size = new Size(180, 100);
            card.Location = new Point(xPos, yPos);
            card.BackColor = colorCard;

            // Accent Line sa itaas ng card
            Panel accent = new Panel();
            accent.Size = new Size(180, 4);
            accent.Dock = DockStyle.Top;
            accent.BackColor = accentColor;
            card.Controls.Add(accent);

            Label lblTitle = new Label();
            lblTitle.Text = title;
            lblTitle.ForeColor = Color.Gray;
            lblTitle.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            lblTitle.Location = new Point(15, 20);
            lblTitle.AutoSize = true;
            card.Controls.Add(lblTitle);

            Label lblValue = new Label();
            lblValue.Text = value;
            lblValue.ForeColor = Color.White;
            lblValue.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            lblValue.Location = new Point(15, 45);
            lblValue.AutoSize = true;
            card.Controls.Add(lblValue);

            mainContentPanel.Controls.Add(card);
        }

        private void ResetButtonColors()
        {
            btnDashboard.BackColor = colorSidebar;
            btnRentals.BackColor = colorSidebar;
            btnVehicles.BackColor = colorSidebar;
        }

        // ==========================================
        // BUTTON ACTIONS / API CALLS
        // ==========================================
        private void BtnDashboard_Click(object sender, EventArgs e)
        {
            ResetButtonColors();
            btnDashboard.BackColor = colorPrimary; // Active color
            lblHeaderTitle.Text = "DASHBOARD STATS\n📅 " + DateTime.Now.ToString("dddd, MMMM dd, yyyy");
            dgvMain.DataSource = null; // Clear table sa dashboard view muna
        }

        private async void BtnRentals_Click(object sender, EventArgs e)
        {
            ResetButtonColors();
            btnRentals.BackColor = colorPrimary;
            lblHeaderTitle.Text = "TRANSACTION LOGS (Rentals)";

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string apiUrl = "https://localhost:7243/api/Rentals";
                    HttpResponseMessage response = await client.GetAsync(apiUrl);
                    response.EnsureSuccessStatusCode();

                    string responseBody = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    List<Rental> rentalsList = JsonSerializer.Deserialize<List<Rental>>(responseBody, options);

                    dgvMain.DataSource = rentalsList;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading rentals: " + ex.Message);
            }
        }

        private async void BtnVehicles_Click(object sender, EventArgs e)
        {
            ResetButtonColors();
            btnVehicles.BackColor = colorPrimary;
            lblHeaderTitle.Text = "FLEET MANAGEMENT (Vehicles)";

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string apiUrl = "https://localhost:7243/api/Vehicles";
                    HttpResponseMessage response = await client.GetAsync(apiUrl);
                    response.EnsureSuccessStatusCode();

                    string responseBody = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    List<Vehicle> vehiclesList = JsonSerializer.Deserialize<List<Vehicle>>(responseBody, options);

                    dgvMain.DataSource = vehiclesList;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading vehicles: " + ex.Message);
            }
        }
    }
}