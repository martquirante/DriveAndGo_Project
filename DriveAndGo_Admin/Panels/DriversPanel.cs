#nullable disable
using DriveAndGo_Admin.Helpers;
using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

using Button = System.Windows.Forms.Button;
using ComboBox = System.Windows.Forms.ComboBox;
using TextBox = System.Windows.Forms.TextBox;
using WinColor = System.Drawing.Color;

namespace DriveAndGo_Admin.Panels
{
    public class DriversPanel : UserControl
    {
        // ── Dynamic theme colors ──
        private WinColor ColBg => ThemeManager.CurrentBackground;
        private WinColor ColCard => ThemeManager.CurrentCard;
        private WinColor ColText => ThemeManager.CurrentText;
        private WinColor ColSub => ThemeManager.CurrentSubText;
        private WinColor ColBorder => ThemeManager.CurrentBorder;

        private readonly WinColor ColGreen = WinColor.FromArgb(34, 197, 94);
        private readonly WinColor ColRed = WinColor.FromArgb(239, 68, 68);
        private readonly WinColor ColBlue = WinColor.FromArgb(59, 130, 246);
        private readonly WinColor ColYellow = WinColor.FromArgb(245, 158, 11);
        private readonly WinColor ColAccent = WinColor.FromArgb(230, 81, 0);

        private readonly string _connStr = "Server=localhost;Database=vehicle_rental_db;Uid=root;Pwd=;";

        // ── UI ──
        private SplitContainer splitContainer;
        private Panel topBar;
        private DataGridView dgvDrivers;

        // Driver Profile Card (Right Panel)
        private Panel rightPanel;
        private Panel pnlProfileCard;
        private Label lblDriverName;
        private Label lblStatus;
        private Label lblRating;
        private Label lblTrips;
        private Label lblLicense;
        private Label lblActiveRentals;
        private Label lblRevenueHandled;
        private Label lblLastAssigned;

        private TextBox txtSearch;
        private ComboBox cboStatus;

        private Button btnActivate, btnOffDuty, btnSuspend, btnDelete;

        // ── State ──
        private DataTable _data = new DataTable();
        private int _selectedDriverId = -1;

        public DriversPanel()
        {
            this.Dock = DockStyle.Fill;
            this.DoubleBuffered = true;
            this.BackColor = ColBg;
            ThemeManager.ThemeChanged += OnThemeChanged;

            BuildUI();

            // Ligtas na pagtawag sa LoadFromDB para sure na tapos na ang UI
            this.Load += (s, e) => LoadFromDB();
        }

        // ══ BUILD UI ══
        private void BuildUI()
        {
            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 5,
                SplitterDistance = 650,
                BackColor = ColBorder
            };
            splitContainer.Panel1.BackColor = ColBg;
            splitContainer.Panel2.BackColor = ColBg;
            splitContainer.SplitterMoved += (s, e) => { dgvDrivers?.Invalidate(); };

            BuildLeftPanel();
            BuildRightPanel();

            this.Controls.Add(splitContainer);
        }

        // ── LEFT PANEL (Table & Controls) ──
        private void BuildLeftPanel()
        {
            topBar = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = WinColor.Transparent, Padding = new Padding(16, 12, 16, 8) };

            var lblTitle = new Label { Text = "Driver Management", Font = new Font("Segoe UI", 18F, FontStyle.Bold), ForeColor = ColText, AutoSize = true, Location = new Point(16, 12), BackColor = WinColor.Transparent };

            txtSearch = new TextBox { Size = new Size(200, 30), Location = new Point(16, 56), Font = new Font("Segoe UI", 10F), BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White, ForeColor = ColText, BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "🔍 Search driver name..." };
            txtSearch.TextChanged += (s, e) => FilterGrid();

            cboStatus = new ComboBox { Size = new Size(140, 30), Location = new Point(226, 56), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White, ForeColor = ColText };
            cboStatus.Items.AddRange(new object[] { "All Status", "available", "on-trip", "off-duty", "active", "inactive", "suspended" });
            cboStatus.SelectedIndex = 0;
            cboStatus.SelectedIndexChanged += (s, e) => FilterGrid();

            var btnRefresh = CreateBtn("⟳ Reload", ColSub, 376, 54, 90);
            btnRefresh.Click += (s, e) => LoadFromDB();

            topBar.Controls.Add(lblTitle);
            topBar.Controls.Add(txtSearch);
            topBar.Controls.Add(cboStatus);
            topBar.Controls.Add(btnRefresh);

            dgvDrivers = new DataGridView { Dock = DockStyle.Fill };
            StyleGrid(dgvDrivers);
            dgvDrivers.SelectionChanged += OnRowSelected;
            dgvDrivers.CellPainting += OnCellPainting;

            splitContainer.Panel1.Controls.Add(dgvDrivers);
            splitContainer.Panel1.Controls.Add(topBar);
        }

        // ── RIGHT PANEL (Driver Profile Card) ──
        private void BuildRightPanel()
        {
            rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(12, 12, 20) : WinColor.FromArgb(240, 241, 248), Padding = new Padding(20) };

            pnlProfileCard = new Panel
            {
                BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(25, 25, 35) : WinColor.White,
                Location = new Point(20, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Height = 430
            };
            pnlProfileCard.Paint += (s, e) => {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundRect(new Rectangle(0, 0, pnlProfileCard.Width - 1, pnlProfileCard.Height - 1), 12);
                g.FillPath(new SolidBrush(pnlProfileCard.BackColor), path);
                g.DrawPath(new Pen(ColBorder, 1), path);

                using var headerPath = new GraphicsPath();
                headerPath.AddArc(0, 0, 24, 24, 180, 90);
                headerPath.AddArc(pnlProfileCard.Width - 25, 0, 24, 24, 270, 90);
                headerPath.AddLine(pnlProfileCard.Width - 1, 10, pnlProfileCard.Width - 1, 60);
                headerPath.AddLine(0, 60, 0, 10);
                g.FillPath(new SolidBrush(ColAccent), headerPath);
            };

            Panel pnlAvatar = new Panel { Size = new Size(80, 80), Location = new Point(20, 30), BackColor = WinColor.Transparent };
            pnlAvatar.Paint += (s, e) => {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(new SolidBrush(ThemeManager.IsDarkMode ? WinColor.FromArgb(40, 40, 55) : WinColor.FromArgb(220, 220, 230)), 0, 0, 78, 78);
                g.DrawEllipse(new Pen(WinColor.White, 3), 0, 0, 78, 78);

                string initial = "?";
                if (lblDriverName != null && !string.IsNullOrEmpty(lblDriverName.Text) && lblDriverName.Text != "Select a driver")
                {
                    initial = lblDriverName.Text.Substring(0, 1).ToUpper();
                }
                TextRenderer.DrawText(g, initial, new Font("Segoe UI", 24F, FontStyle.Bold), new Rectangle(0, 0, 80, 80), ColText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };

            lblDriverName = new Label { Text = "Select a driver", Font = new Font("Segoe UI", 16F, FontStyle.Bold), ForeColor = ColText, AutoSize = true, Location = new Point(110, 70), BackColor = WinColor.Transparent };
            lblStatus = new Label { Text = "STATUS", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = ColSub, AutoSize = true, Location = new Point(115, 100), BackColor = WinColor.Transparent };

            Label lblLicenseTitle = new Label { Text = "LICENSE NUMBER", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = ColSub, AutoSize = true, Location = new Point(20, 150) };
            lblLicense = new Label { Text = "—", Font = new Font("Consolas", 14F), ForeColor = ColText, AutoSize = true, Location = new Point(20, 170) };

            Label lblTripsTitle = new Label { Text = "TOTAL TRIPS", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = ColSub, AutoSize = true, Location = new Point(20, 220) };
            lblTrips = new Label { Text = "0", Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = ColBlue, AutoSize = true, Location = new Point(20, 240) };

            Label lblRatingTitle = new Label { Text = "RATING", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = ColSub, AutoSize = true, Location = new Point(150, 220) };
            lblRating = new Label { Text = "0.0", Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = ColYellow, AutoSize = true, Location = new Point(150, 240) };

            Label lblActiveTitle = new Label { Text = "ACTIVE RENTALS", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = ColSub, AutoSize = true, Location = new Point(20, 292) };
            lblActiveRentals = new Label { Text = "0", Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = ColGreen, AutoSize = true, Location = new Point(20, 312) };

            Label lblRevenueTitle = new Label { Text = "REVENUE HANDLED", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = ColSub, AutoSize = true, Location = new Point(150, 292) };
            lblRevenueHandled = new Label { Text = "₱0.00", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = ColAccent, AutoSize = true, Location = new Point(150, 312) };

            Label lblLastAssignedTitle = new Label { Text = "LAST ASSIGNMENT", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = ColSub, AutoSize = true, Location = new Point(20, 360) };
            lblLastAssigned = new Label { Text = "No active booking", Font = new Font("Segoe UI", 9F), ForeColor = ColText, AutoSize = true, Location = new Point(20, 380) };

            pnlProfileCard.Controls.Add(pnlAvatar);
            pnlProfileCard.Controls.Add(lblDriverName);
            pnlProfileCard.Controls.Add(lblStatus);
            pnlProfileCard.Controls.Add(lblLicenseTitle);
            pnlProfileCard.Controls.Add(lblLicense);
            pnlProfileCard.Controls.Add(lblTripsTitle);
            pnlProfileCard.Controls.Add(lblTrips);
            pnlProfileCard.Controls.Add(lblRatingTitle);
            pnlProfileCard.Controls.Add(lblRating);
            pnlProfileCard.Controls.Add(lblActiveTitle);
            pnlProfileCard.Controls.Add(lblActiveRentals);
            pnlProfileCard.Controls.Add(lblRevenueTitle);
            pnlProfileCard.Controls.Add(lblRevenueHandled);
            pnlProfileCard.Controls.Add(lblLastAssignedTitle);
            pnlProfileCard.Controls.Add(lblLastAssigned);

            Panel pnlActions = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = WinColor.Transparent };
            btnActivate = CreateBtn("✔ Ready", ColGreen, 0, 12, 92);
            btnOffDuty = CreateBtn("☾ Off Duty", ColBlue, 100, 12, 98);
            btnSuspend = CreateBtn("⏸ Suspend", ColYellow, 206, 12, 96);
            btnDelete = CreateBtn("🗑 Delete", ColRed, 310, 12, 90);

            btnActivate.Click += (s, e) => UpdateDriverStatus("available");
            btnOffDuty.Click += (s, e) => UpdateDriverStatus("off-duty");
            btnSuspend.Click += (s, e) => UpdateDriverStatus("suspended");
            btnDelete.Click += OnDeleteDriver;

            btnActivate.Enabled = false;
            btnOffDuty.Enabled = false;
            btnSuspend.Enabled = false;
            btnDelete.Enabled = false;

            pnlActions.Controls.Add(btnActivate);
            pnlActions.Controls.Add(btnOffDuty);
            pnlActions.Controls.Add(btnSuspend);
            pnlActions.Controls.Add(btnDelete);

            rightPanel.Controls.Add(pnlProfileCard);
            rightPanel.Controls.Add(pnlActions);

            splitContainer.Panel2.Controls.Add(rightPanel);
        }

        // ══ THEME CHANGED ══
        private void OnThemeChanged(object s, EventArgs e)
        {
            this.BackColor = ColBg;
            splitContainer.Panel1.BackColor = ColBg;
            splitContainer.Panel2.BackColor = ColBg;
            splitContainer.BackColor = ColBorder;

            rightPanel.BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(12, 12, 20) : WinColor.FromArgb(240, 241, 248);

            if (pnlProfileCard != null)
            {
                pnlProfileCard.BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(25, 25, 35) : WinColor.White;
            }

            topBar.BackColor = ThemeManager.IsDarkMode ? ColBg : WinColor.FromArgb(250, 250, 255);
            txtSearch.BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White;
            txtSearch.ForeColor = ColText;
            cboStatus.BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White;
            cboStatus.ForeColor = ColText;

            if (lblDriverName != null) lblDriverName.ForeColor = ColText;
            if (lblLicense != null) lblLicense.ForeColor = ColText;
            if (lblLastAssigned != null) lblLastAssigned.ForeColor = ColText;

            foreach (Control c in topBar.Controls) { if (c is Label l) l.ForeColor = ColText; }

            StyleGrid(dgvDrivers);
            this.Invalidate(true);
        }

        // ══ PURE DATABASE LOAD (NO DUMMY DATA) ══
        private void LoadFromDB()
        {
            try
            {
                _data = new DataTable();
                using var conn = new MySqlConnection(_connStr);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT 
                        d.driver_id, 
                        d.user_id, 
                        COALESCE(u.full_name, 'Unknown User') AS driver_name,
                        d.license_no, 
                        d.status, 
                        COALESCE(d.rating_avg, 0) AS rating_avg, 
                        COALESCE(d.total_trips, 0) AS total_trips,
                        COALESCE(active_summary.active_rentals, 0) AS active_rentals,
                        COALESCE(revenue_summary.revenue_handled, 0) AS revenue_handled,
                        active_summary.last_assignment
                    FROM drivers d
                    LEFT JOIN users u ON d.user_id = u.user_id
                    LEFT JOIN
                    (
                        SELECT driver_id,
                               COUNT(*) AS active_rentals,
                               MAX(start_date) AS last_assignment
                        FROM rentals
                        WHERE driver_id IS NOT NULL
                          AND LOWER(COALESCE(status, '')) IN ('approved', 'in-use', 'rented')
                        GROUP BY driver_id
                    ) active_summary ON active_summary.driver_id = d.user_id
                    LEFT JOIN
                    (
                        SELECT r.driver_id,
                               SUM(CASE WHEN LOWER(COALESCE(t.status, '')) IN ('confirmed', 'paid') THEN t.amount ELSE 0 END) AS revenue_handled
                        FROM rentals r
                        LEFT JOIN transactions t ON t.rental_id = r.rental_id
                        WHERE r.driver_id IS NOT NULL
                        GROUP BY r.driver_id
                    ) revenue_summary ON revenue_summary.driver_id = d.user_id
                    ORDER BY d.driver_id DESC", conn);

                using var adapter = new MySqlDataAdapter(cmd);
                adapter.Fill(_data);

                RefreshGrid(_data);
            }
            catch (Exception ex)
            {
                // NO DUMMY DATA. Print exact stack trace for easy debugging.
                RefreshGrid(new DataTable());
                MessageBox.Show($"DB Error Details:\n{ex.Message}", "Database Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ══ SAFE GRID REFRESH ══
        private void RefreshGrid(DataTable dt)
        {
            if (dgvDrivers == null) return;
            if (dgvDrivers.InvokeRequired) { dgvDrivers.Invoke(new Action(() => RefreshGrid(dt))); return; }

            dgvDrivers.DataSource = null;
            dgvDrivers.Columns.Clear();

            var display = new DataTable();
            display.Columns.Add("ID", typeof(int));
            display.Columns.Add("Driver Name", typeof(string));
            display.Columns.Add("License", typeof(string));
            display.Columns.Add("Trips", typeof(int));
            display.Columns.Add("Active", typeof(int));
            display.Columns.Add("Revenue", typeof(string));
            display.Columns.Add("Rating", typeof(string));
            display.Columns.Add("Status", typeof(string));

            if (dt != null && dt.Rows.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    decimal rating = row["rating_avg"] != DBNull.Value ? Convert.ToDecimal(row["rating_avg"]) : 0;
                    int trips = row["total_trips"] != DBNull.Value ? Convert.ToInt32(row["total_trips"]) : 0;
                    int activeRentals = row["active_rentals"] != DBNull.Value ? Convert.ToInt32(row["active_rentals"]) : 0;
                    decimal revenueHandled = row["revenue_handled"] != DBNull.Value ? Convert.ToDecimal(row["revenue_handled"]) : 0;

                    // Super safe null conversions
                    string status = row["status"] != DBNull.Value ? row["status"].ToString() : "inactive";
                    string driverName = row["driver_name"] != DBNull.Value ? row["driver_name"].ToString() : "Unknown";
                    string license = row["license_no"] != DBNull.Value ? row["license_no"].ToString() : "N/A";

                    display.Rows.Add(
                        row["driver_id"],
                        driverName,
                        license,
                        trips,
                        activeRentals,
                        $"₱{revenueHandled:N0}",
                        rating > 0 ? $"⭐ {rating:0.0}" : "No Rating",
                        status
                    );
                }
            }

            dgvDrivers.DataSource = display;

            if (dgvDrivers.Columns.Count >= 8)
            {
                dgvDrivers.Columns[0].Width = 40;
                dgvDrivers.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                dgvDrivers.Columns[2].Width = 120;
                dgvDrivers.Columns[3].Width = 60;
                dgvDrivers.Columns[4].Width = 60;
                dgvDrivers.Columns[5].Width = 90;
                dgvDrivers.Columns[6].Width = 90;
                dgvDrivers.Columns[7].Width = 90;
            }
        }

        private void FilterGrid()
        {
            if (_data == null || cboStatus == null || txtSearch == null) return;

            string status = cboStatus.SelectedItem?.ToString() ?? "All Status";
            string search = txtSearch.Text?.Trim().ToLower() ?? "";

            var filtered = _data.Clone();
            foreach (DataRow row in _data.Rows)
            {
                bool ok = true;
                string rowStatus = row["status"] != DBNull.Value ? row["status"].ToString().ToLower() : "";

                if (status != "All Status" && rowStatus != status.ToLower()) ok = false;

                if (!string.IsNullOrEmpty(search))
                {
                    string dName = row["driver_name"] != DBNull.Value ? row["driver_name"].ToString().ToLower() : "";
                    string lNum = row["license_no"] != DBNull.Value ? row["license_no"].ToString().ToLower() : "";

                    if (!dName.Contains(search) && !lNum.Contains(search)) ok = false;
                }

                if (ok) filtered.ImportRow(row);
            }
            RefreshGrid(filtered);
        }

        // ══ ROW SELECTED → Update Profile Card ══
        private void OnRowSelected(object sender, EventArgs e)
        {
            // Abort if controls aren't ready or no selection
            if (dgvDrivers == null || lblDriverName == null || dgvDrivers.SelectedRows.Count == 0) return;

            if (dgvDrivers.SelectedRows[0].Cells[0].Value == null || dgvDrivers.SelectedRows[0].Cells[0].Value == DBNull.Value) return;

            int id = Convert.ToInt32(dgvDrivers.SelectedRows[0].Cells[0].Value);
            _selectedDriverId = id;

            var rows = _data.Select($"driver_id = {id}");
            if (rows.Length == 0) return;

            DataRow r = rows[0];

            lblDriverName.Text = r["driver_name"] != DBNull.Value ? r["driver_name"].ToString() : "Unknown Driver";
            lblLicense.Text = r["license_no"] != DBNull.Value ? r["license_no"].ToString() : "N/A";
            lblTrips.Text = r["total_trips"] != DBNull.Value ? r["total_trips"].ToString() : "0";
            lblActiveRentals.Text = r["active_rentals"] != DBNull.Value ? r["active_rentals"].ToString() : "0";
            lblRevenueHandled.Text = r["revenue_handled"] != DBNull.Value ? $"₱{Convert.ToDecimal(r["revenue_handled"]):N2}" : "₱0.00";
            lblLastAssigned.Text = r["last_assignment"] != DBNull.Value ? Convert.ToDateTime(r["last_assignment"]).ToString("MMM dd, yyyy") : "No active booking";

            decimal rating = r["rating_avg"] != DBNull.Value ? Convert.ToDecimal(r["rating_avg"]) : 0;
            lblRating.Text = rating > 0 ? $"{rating:0.0} / 5.0" : "N/A";

            string status = r["status"] != DBNull.Value ? r["status"].ToString().ToLower() : "inactive";
            lblStatus.Text = "● " + status.ToUpper();
            lblStatus.ForeColor = StatusToColor(status);

            // Trigger repaint for Avatar Initial
            pnlProfileCard.Invalidate(true);

            btnActivate.Enabled = !IsReadyStatus(status);
            btnOffDuty.Enabled = status != "off-duty";
            btnSuspend.Enabled = status != "suspended";
            btnDelete.Enabled = true;
        }

        // ══ ACTIONS ══
        private void UpdateDriverStatus(string newStatus)
        {
            if (_selectedDriverId < 0) return;

            string actionName = newStatus == "available" ? "Mark ready" : newStatus == "off-duty" ? "Set off duty" : "Suspend";
            if (MessageBox.Show($"Are you sure you want to {actionName.ToLower()} this driver?", "Confirm Action", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();
                var cmd = new MySqlCommand("UPDATE drivers SET status = @s WHERE driver_id = @id", conn);
                cmd.Parameters.AddWithValue("@s", newStatus);
                cmd.Parameters.AddWithValue("@id", _selectedDriverId);
                cmd.ExecuteNonQuery();

                MessageBox.Show($"Driver status updated to {newStatus}.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadFromDB();
            }
            catch (Exception ex)
            {
                MessageBox.Show("DB Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnDeleteDriver(object s, EventArgs e)
        {
            if (_selectedDriverId < 0) return;
            if (MessageBox.Show("Warning: Deleting a driver cannot be undone. Proceed?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();
                var cmd = new MySqlCommand("DELETE FROM drivers WHERE driver_id = @id", conn);
                cmd.Parameters.AddWithValue("@id", _selectedDriverId);
                cmd.ExecuteNonQuery();

                LoadFromDB();

                // Reset card
                lblDriverName.Text = "Select a driver";
                lblStatus.Text = "STATUS";
                lblLicense.Text = "—";
                lblTrips.Text = "0";
                lblRating.Text = "0.0";
                lblActiveRentals.Text = "0";
                lblRevenueHandled.Text = "₱0.00";
                lblLastAssigned.Text = "No active booking";
                btnActivate.Enabled = false;
                btnOffDuty.Enabled = false;
                btnSuspend.Enabled = false;
                btnDelete.Enabled = false;
                pnlProfileCard.Invalidate(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show("DB Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ══ GRID DESIGN ══
        private void OnCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.Value == null || e.Value == DBNull.Value) return;

            // Status column index is 7
            if (e.ColumnIndex == 7)
            {
                e.Handled = true;
                e.PaintBackground(e.ClipBounds, true);

                string val = e.Value.ToString();
                WinColor c = StatusToColor(val);

                var rect = new Rectangle(e.CellBounds.X + 6, e.CellBounds.Y + 9, e.CellBounds.Width - 12, e.CellBounds.Height - 18);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundRect(rect, 8);
                e.Graphics.FillPath(new SolidBrush(WinColor.FromArgb(30, c)), path);
                e.Graphics.DrawPath(new Pen(c, 1), path);
                TextRenderer.DrawText(e.Graphics, val, new Font("Segoe UI", 8F, FontStyle.Bold), rect, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private void StyleGrid(DataGridView dgv)
        {
            bool dk = ThemeManager.IsDarkMode;
            dgv.BackgroundColor = ColBg;
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.GridColor = ColBorder;
            dgv.RowHeadersVisible = false;
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.ReadOnly = true;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.Font = new Font("Segoe UI", 10F);
            dgv.RowTemplate.Height = 42;
            dgv.EnableHeadersVisualStyles = false;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            dgv.DefaultCellStyle.BackColor = dk ? ColBg : WinColor.White;
            dgv.DefaultCellStyle.ForeColor = ColText;
            dgv.DefaultCellStyle.SelectionBackColor = dk ? WinColor.FromArgb(30, 30, 48) : WinColor.FromArgb(220, 232, 255);
            dgv.DefaultCellStyle.SelectionForeColor = dk ? ColAccent : WinColor.FromArgb(10, 10, 30);
            dgv.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);

            dgv.ColumnHeadersDefaultCellStyle.BackColor = dk ? WinColor.FromArgb(8, 8, 16) : WinColor.FromArgb(235, 236, 245);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = ColSub;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgv.ColumnHeadersHeight = 38;
        }

        // ══ UTILS ══
        private Button CreateBtn(string text, WinColor color, int x, int y, int w)
        {
            var btn = new Button { Text = text, Size = new Size(w, 36), Location = new Point(x, y), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand, BackColor = WinColor.FromArgb(20, color), ForeColor = color };
            btn.FlatAppearance.BorderColor = color; btn.FlatAppearance.BorderSize = 1; btn.FlatAppearance.MouseOverBackColor = WinColor.FromArgb(45, color); return btn;
        }

        private GraphicsPath RoundRect(Rectangle b, int r)
        {
            int d = r * 2; var arc = new Rectangle(b.Location, new Size(d, d)); var path = new GraphicsPath();
            path.AddArc(arc, 180, 90); arc.X = b.Right - d; path.AddArc(arc, 270, 90); arc.Y = b.Bottom - d;
            path.AddArc(arc, 0, 90); arc.X = b.Left; path.AddArc(arc, 90, 90); path.CloseFigure(); return path;
        }

        private bool IsReadyStatus(string status) =>
            string.Equals(status, "available", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "active", StringComparison.OrdinalIgnoreCase);

        private WinColor StatusToColor(string status)
        {
            string value = status?.ToLower() ?? "";
            return value switch
            {
                "available" or "active" => ColGreen,
                "on-trip" => ColBlue,
                "suspended" => ColRed,
                _ => ColYellow
            };
        }

        protected override void Dispose(bool disposing)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }
    }
}
