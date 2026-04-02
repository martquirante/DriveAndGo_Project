#nullable disable

using CefSharp;
using CefSharp.WinForms;
using CefSharp;
using CefSharp.WinForms;
using DriveAndGo_Admin.Helpers;
using MySql.Data.MySqlClient;
using System.Data;
using System.Drawing.Drawing2D;

namespace DriveAndGo_Admin.Panels
{
    public class FleetPanel : UserControl
    {
        // ── Colors (Dynamic based on ThemeManager) ──
        private Color ColBg => ThemeManager.CurrentBackground;
        private Color ColCard => ThemeManager.CurrentCard;
        private Color ColText => ThemeManager.CurrentText;
        private Color ColSub => ThemeManager.CurrentSubText;
        private Color ColBorder => ThemeManager.CurrentBorder;

        // Fixed Colors
        private readonly Color ColAccent = Color.FromArgb(230, 81, 0);
        private readonly Color ColGreen = Color.FromArgb(34, 197, 94);
        private readonly Color ColRed = Color.FromArgb(239, 68, 68);
        private readonly Color ColBlue = Color.FromArgb(59, 130, 246);
        private readonly Color ColYellow = Color.FromArgb(234, 179, 8);
        private readonly Color ColPurple = Color.FromArgb(168, 85, 247);

        private readonly string _connStr =
            "Server=localhost;Database=vehicle_rental_db;" +
            "Uid=root;Pwd=;";

        // ── UI ──
        private Panel leftPanel;
        private Panel rightPanel;
        private DataGridView dgvVehicles;
        private ChromiumWebBrowser browser;
        private Label lblTitle;
        private Label lblCount;
        private Button btnAdd;
        private Button btnEdit;
        private Button btnDelete;
        private Button btnRefresh;
        private TextBox txtSearch;
        private Panel statsBar;
        private Panel btnRow;
        private Panel searchPanel;
        private Panel titleRow;

        // ── State ──
        private DataTable _vehicleData = new DataTable();
        private int _selectedVehicleId = -1;

        public FleetPanel()
        {
            this.Dock = DockStyle.Fill;
            this.DoubleBuffered = true;
            this.BackColor = ColBg;

            this.Resize += OnPanelResize;
            ThemeManager.ThemeChanged += OnThemeChanged;

            BuildUI();
            LoadVehiclesFromDB();
        }

        private void OnPanelResize(object sender, EventArgs e)
        {
            if (this.Width < 900)
                leftPanel.Width = 380;
            else
                leftPanel.Width = 480;

            leftPanel.Invalidate();
            statsBar?.Invalidate();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            this.BackColor = ColBg;
            leftPanel.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(14, 14, 22) : Color.FromArgb(250, 250, 250);

            lblTitle.ForeColor = ColText;
            lblCount.ForeColor = ColSub;

            statsBar.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(12, 12, 20) : Color.FromArgb(240, 240, 245);
            btnRow.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(12, 12, 20) : Color.FromArgb(240, 240, 245);

            txtSearch.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(20, 20, 32) : Color.White;
            txtSearch.ForeColor = ColText;

            StyleGrid(dgvVehicles);

            leftPanel.Invalidate();
            statsBar.Invalidate();
            btnRow.Invalidate();
        }

        private void BuildUI()
        {
            leftPanel = new Panel();
            leftPanel.Dock = DockStyle.Left;
            leftPanel.Width = 480;
            leftPanel.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(14, 14, 22) : Color.FromArgb(250, 250, 250);
            leftPanel.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(ColBorder, 1), leftPanel.Width - 1, 0, leftPanel.Width - 1, leftPanel.Height);
            };

            titleRow = new Panel();
            titleRow.Size = new Size(leftPanel.Width, 56);
            titleRow.Location = new Point(0, 0);
            titleRow.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            titleRow.BackColor = Color.Transparent;

            lblTitle = new Label();
            lblTitle.Text = "Fleet Management";
            lblTitle.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
            lblTitle.ForeColor = ColText;
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(20, 14);

            lblCount = new Label();
            lblCount.Text = "0 vehicles";
            lblCount.Font = new Font("Segoe UI", 9F);
            lblCount.ForeColor = ColSub;
            lblCount.AutoSize = true;
            lblCount.Location = new Point(22, 42);

            titleRow.Controls.Add(lblTitle);
            titleRow.Controls.Add(lblCount);

            statsBar = new Panel();
            statsBar.Size = new Size(leftPanel.Width, 52);
            statsBar.Location = new Point(0, 60);
            statsBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            statsBar.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(12, 12, 20) : Color.FromArgb(240, 240, 245);
            statsBar.Paint += OnStatsBarPaint;

            searchPanel = new Panel();
            searchPanel.Size = new Size(leftPanel.Width, 48);
            searchPanel.Location = new Point(0, 112);
            searchPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            searchPanel.BackColor = Color.Transparent;
            searchPanel.Padding = new Padding(16, 8, 16, 8);

            txtSearch = new TextBox();
            txtSearch.Size = new Size(leftPanel.Width - 32, 32);
            txtSearch.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtSearch.Location = new Point(16, 8);
            txtSearch.Font = new Font("Segoe UI", 10F);
            txtSearch.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(20, 20, 32) : Color.White;
            txtSearch.ForeColor = ColText;
            txtSearch.BorderStyle = BorderStyle.FixedSingle;
            txtSearch.PlaceholderText = "🔍  Search vehicle...";
            txtSearch.TextChanged += OnSearch;
            searchPanel.Controls.Add(txtSearch);

            dgvVehicles = new DataGridView();
            dgvVehicles.Location = new Point(0, 160);
            dgvVehicles.Size = new Size(leftPanel.Width, this.Height - 216);
            dgvVehicles.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            StyleGrid(dgvVehicles);
            dgvVehicles.SelectionChanged += OnVehicleSelected;
            dgvVehicles.CellFormatting += OnCellFormatting;

            btnRow = new Panel();
            btnRow.Height = 56;
            btnRow.Dock = DockStyle.Bottom;
            btnRow.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(12, 12, 20) : Color.FromArgb(240, 240, 245);
            btnRow.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(ColBorder, 1), 0, 0, btnRow.Width, 0);
            };

            btnAdd = CreateActionButton("＋ Add", ColAccent, 10, 8);
            btnEdit = CreateActionButton("✏ Edit", ColBlue, 10 + 110, 8);
            btnDelete = CreateActionButton("🗑 Delete", ColRed, 10 + 220, 8);
            btnRefresh = CreateActionButton("⟳ Refresh", ColGreen, 10 + 330, 8);

            btnAdd.Click += OnAddVehicle;
            btnEdit.Click += OnEditVehicle;
            btnDelete.Click += OnDeleteVehicle;
            btnRefresh.Click += (s, e) => LoadVehiclesFromDB();

            btnRow.Controls.Add(btnAdd);
            btnRow.Controls.Add(btnEdit);
            btnRow.Controls.Add(btnDelete);
            btnRow.Controls.Add(btnRefresh);

            leftPanel.Controls.Add(titleRow);
            leftPanel.Controls.Add(statsBar);
            leftPanel.Controls.Add(searchPanel);
            leftPanel.Controls.Add(dgvVehicles);
            leftPanel.Controls.Add(btnRow);

            rightPanel = new Panel();
            rightPanel.Dock = DockStyle.Fill;
            rightPanel.BackColor = ColBg;

            string mapPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "map.html");

            if (File.Exists(mapPath))
            {
                browser = new ChromiumWebBrowser(mapPath);
                browser.Dock = DockStyle.Fill;

                // ✅ CORRECTED: LoadingStateChanged event signature
                browser.LoadingStateChanged += (sender, args) =>
                {
                    if (!args.IsLoading)
                    {
                        this.Invoke(new Action(() => PushAllMarkersToMap()));
                    }
                };

                rightPanel.Controls.Add(browser);
            }
            else
            {
                var lbl = new Label();
                lbl.Text = "⚠  map.html not found in WebAssets/ folder.";
                lbl.Font = new Font("Segoe UI", 12F);
                lbl.ForeColor = ColRed;
                lbl.AutoSize = true;
                lbl.Location = new Point(40, 40);
                lbl.BackColor = Color.Transparent;
                rightPanel.Controls.Add(lbl);
            }

            this.Controls.Add(rightPanel);
            this.Controls.Add(leftPanel);
        }

        private void LoadVehiclesFromDB()
        {
            _vehicleData.Clear();
            _vehicleData = new DataTable();

            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();

                bool hasGpsTable = false;
                try
                {
                    using var checkCmd = new MySqlCommand("SHOW TABLES LIKE 'gps_log'", conn);
                    hasGpsTable = checkCmd.ExecuteScalar() != null;
                }
                catch { }

                string query = "";

                if (hasGpsTable)
                {
                    query = @"
                        SELECT
                            v.vehicle_id,
                            CONCAT(v.brand, ' ', v.model) AS vehicle_name,
                            v.plate_number,
                            v.type,
                            v.status,
                            v.daily_rate,
                            v.model_3d_url,
                            g.latitude,
                            g.longitude,
                            g.odometer_km,
                            g.logged_at      AS last_update,
                            CASE WHEN g.latitude IS NULL THEN 1 ELSE 0 END AS is_lost
                        FROM vehicles v
                        LEFT JOIN (
                            SELECT vehicle_id, latitude, longitude, odometer_km, logged_at
                            FROM   gps_log
                            WHERE  (vehicle_id, logged_at) IN (
                                SELECT vehicle_id, MAX(logged_at) FROM gps_log GROUP BY vehicle_id
                            )
                        ) g ON v.vehicle_id = g.vehicle_id
                        ORDER BY v.brand, v.model";
                }
                else
                {
                    query = @"
                        SELECT
                            vehicle_id,
                            CONCAT(brand, ' ', model) AS vehicle_name,
                            plate_number,
                            type,
                            status,
                            daily_rate,
                            model_3d_url,
                            NULL AS latitude,
                            NULL AS longitude,
                            0 AS odometer_km,
                            NULL AS last_update,
                            1 AS is_lost
                        FROM vehicles
                        ORDER BY brand, model";
                }

                var cmd = new MySqlCommand(query, conn);
                using var adapter = new MySqlDataAdapter(cmd);
                adapter.Fill(_vehicleData);

                RefreshGrid(_vehicleData);
                UpdateStatsBar();
                PushAllMarkersToMap();
            }
            catch (Exception ex)
            {
                LoadDummyData();
                MessageBox.Show("Using Dummy Data because Database connection failed.\n\nError: " + ex.Message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadDummyData()
        {
            _vehicleData.Clear();
            _vehicleData.Columns.Add("vehicle_id", typeof(int));
            _vehicleData.Columns.Add("vehicle_name", typeof(string));
            _vehicleData.Columns.Add("plate_number", typeof(string));
            _vehicleData.Columns.Add("type", typeof(string));
            _vehicleData.Columns.Add("status", typeof(string));
            _vehicleData.Columns.Add("daily_rate", typeof(decimal));
            _vehicleData.Columns.Add("model_3d_url", typeof(string));
            _vehicleData.Columns.Add("latitude", typeof(double));
            _vehicleData.Columns.Add("longitude", typeof(double));
            _vehicleData.Columns.Add("odometer_km", typeof(double));
            _vehicleData.Columns.Add("last_update", typeof(DateTime));
            _vehicleData.Columns.Add("is_lost", typeof(int));

            _vehicleData.Rows.Add(1, "Yamaha NMAX Neo", "NMAX-123", "Motorcycle", "Rented", 800, "https://sketchfab.com/models/5f4d6331dae349b8b3d0b8ea9d8bc089/embed?autostart=1&ui_theme=dark", 14.6760, 121.0437, 1250, DateTime.Now.AddMinutes(-5), 0);
            _vehicleData.Rows.Add(2, "Honda Civic Type R", "TYPR-999", "Car", "Available", 3500, "https://sketchfab.com/models/30baf88fcfa642a8bda71f84d081816e/embed?autostart=1&ui_theme=dark", 14.8850, 121.0500, 500, DateTime.Now.AddHours(-1), 0);
            _vehicleData.Rows.Add(3, "Toyota Hiace", "VAN-777", "Van", "Lost Signal", 4000, "", DBNull.Value, DBNull.Value, 15000, DBNull.Value, 1);

            RefreshGrid(_vehicleData);
            UpdateStatsBar();
            PushAllMarkersToMap();
        }

        private void RefreshGrid(DataTable dt)
        {
            dgvVehicles.DataSource = null;
            dgvVehicles.Columns.Clear();

            var display = new DataTable();
            display.Columns.Add("ID", typeof(int));
            display.Columns.Add("Vehicle", typeof(string));
            display.Columns.Add("Plate", typeof(string));
            display.Columns.Add("Type", typeof(string));
            display.Columns.Add("Status", typeof(string));
            display.Columns.Add("Rate/Day", typeof(string));
            display.Columns.Add("Last GPS", typeof(string));

            foreach (DataRow row in dt.Rows)
            {
                string lastGps = row["last_update"] != DBNull.Value
                    ? Convert.ToDateTime(row["last_update"]).ToString("MMM dd HH:mm")
                    : "No signal";

                display.Rows.Add(
                    row["vehicle_id"],
                    row["vehicle_name"],
                    row["plate_number"],
                    row["type"],
                    row["status"],
                    "₱" + Convert.ToDecimal(row["daily_rate"]).ToString("N0"),
                    lastGps);
            }

            dgvVehicles.DataSource = display;

            if (dgvVehicles.Columns.Count >= 7)
            {
                dgvVehicles.Columns[0].Width = 40;
                dgvVehicles.Columns[1].Width = 140;
                dgvVehicles.Columns[2].Width = 80;
                dgvVehicles.Columns[3].Width = 60;
                dgvVehicles.Columns[4].Width = 80;
                dgvVehicles.Columns[5].Width = 60;
                dgvVehicles.Columns[6].Width = 90;
            }

            lblCount.Text = $"{dt.Rows.Count} vehicles";
        }

        // ✅ FIXED: Use GetMainFrame().ExecuteJavaScriptAsync()
        private void PushAllMarkersToMap()
        {
            if (browser == null || !browser.IsBrowserInitialized) return;

            browser.GetMainFrame().ExecuteJavaScriptAsync("clearMarkers();", null);

            foreach (DataRow row in _vehicleData.Rows)
            {
                double lat = row["latitude"] != DBNull.Value ? Convert.ToDouble(row["latitude"]) : 14.6760;
                double lng = row["longitude"] != DBNull.Value ? Convert.ToDouble(row["longitude"]) : 121.0437;
                bool isLost = row["latitude"] == DBNull.Value || Convert.ToInt32(row["is_lost"]) == 1;

                string name = Escape(row["vehicle_name"]);
                string plate = Escape(row["plate_number"]);
                string status = Escape(row["status"]);
                string modelUrl = Escape(row["model_3d_url"]);
                string lastUpd = row["last_update"] != DBNull.Value
                    ? Convert.ToDateTime(row["last_update"]).ToString("MMM dd, HH:mm")
                    : "No data";

                int id = Convert.ToInt32(row["vehicle_id"]);

                string script = $"updateVehicle({id}, '{name}', '{plate}', '{status}', {lat}, {lng}, 0, '{lastUpd}', '{modelUrl}', {(isLost ? "true" : "false")});";
                browser.GetMainFrame().ExecuteJavaScriptAsync(script, null);
            }
        }

        // ✅ FIXED: Use GetMainFrame().ExecuteJavaScriptAsync()
        private void OnVehicleSelected(object sender, EventArgs e)
        {
            if (dgvVehicles.SelectedRows.Count == 0) return;

            var row = dgvVehicles.SelectedRows[0];
            int id = Convert.ToInt32(row.Cells[0].Value);
            _selectedVehicleId = id;

            var dataRow = _vehicleData.Select($"vehicle_id = {id}");
            if (dataRow.Length == 0) return;

            var dr = dataRow[0];
            double lat = dr["latitude"] != DBNull.Value ? Convert.ToDouble(dr["latitude"]) : 14.6760;
            double lng = dr["longitude"] != DBNull.Value ? Convert.ToDouble(dr["longitude"]) : 121.0437;

            string name = Escape(dr["vehicle_name"]);
            string plate = Escape(dr["plate_number"]);
            string status = Escape(dr["status"]);
            string modelUrl = Escape(dr["model_3d_url"]);
            double odo = dr["odometer_km"] != DBNull.Value ? Convert.ToDouble(dr["odometer_km"]) : 0;
            string lastUpd = dr["last_update"] != DBNull.Value
                ? Convert.ToDateTime(dr["last_update"]).ToString("MMM dd, yyyy HH:mm")
                : "No data";

            if (browser != null && browser.IsBrowserInitialized)
            {
                string script = $"selectVehicle({id}, '{name}', '{plate}', '{status}', {lat}, {lng}, {(int)odo}, '{lastUpd}', '{modelUrl}');";
                browser.GetMainFrame().ExecuteJavaScriptAsync(script, null);
            }
        }

        private void OnSearch(object sender, EventArgs e)
        {
            string q = txtSearch.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(q))
            {
                RefreshGrid(_vehicleData);
                return;
            }

            var filtered = _vehicleData.Clone();
            foreach (DataRow row in _vehicleData.Rows)
            {
                string name = row["vehicle_name"].ToString()!.ToLower();
                string plate = row["plate_number"].ToString()!.ToLower();
                string type = row["type"].ToString()!.ToLower();
                if (name.Contains(q) || plate.Contains(q) || type.Contains(q))
                    filtered.ImportRow(row);
            }
            RefreshGrid(filtered);
        }

        private void OnAddVehicle(object sender, EventArgs e)
        {
            using var dlg = new VehicleFormDialog(null, _connStr);
            if (dlg.ShowDialog() == DialogResult.OK)
                LoadVehiclesFromDB();
        }

        private void OnEditVehicle(object sender, EventArgs e)
        {
            if (_selectedVehicleId < 0)
            {
                MessageBox.Show("Please select a vehicle first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var dataRow = _vehicleData.Select($"vehicle_id = {_selectedVehicleId}");
            if (dataRow.Length == 0) return;

            using var dlg = new VehicleFormDialog(dataRow[0], _connStr);
            if (dlg.ShowDialog() == DialogResult.OK)
                LoadVehiclesFromDB();
        }

        private void OnDeleteVehicle(object sender, EventArgs e)
        {
            if (_selectedVehicleId < 0)
            {
                MessageBox.Show("Please select a vehicle first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var dataRow = _vehicleData.Select($"vehicle_id = {_selectedVehicleId}");
            string name = dataRow.Length > 0 ? dataRow[0]["vehicle_name"].ToString()! : "this vehicle";

            var result = MessageBox.Show($"Delete {name}?\n\nThis cannot be undone.", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;

            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();

                bool hasRentalsTable = false;
                try
                {
                    using var chk = new MySqlCommand("SHOW TABLES LIKE 'rentals'", conn);
                    hasRentalsTable = chk.ExecuteScalar() != null;
                }
                catch { }

                if (hasRentalsTable)
                {
                    var check = new MySqlCommand(@"SELECT COUNT(*) FROM rentals WHERE vehicle_id = @id AND status IN ('pending','approved')", conn);
                    check.Parameters.AddWithValue("@id", _selectedVehicleId);
                    int active = Convert.ToInt32(check.ExecuteScalar());

                    if (active > 0)
                    {
                        MessageBox.Show("Cannot delete — vehicle has active rentals.", "Delete Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                var del = new MySqlCommand("DELETE FROM vehicles WHERE vehicle_id = @id", conn);
                del.Parameters.AddWithValue("@id", _selectedVehicleId);
                del.ExecuteNonQuery();

                _selectedVehicleId = -1;
                LoadVehiclesFromDB();
                MessageBox.Show("Vehicle deleted.", "Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("DB Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateStatsBar()
        {
            statsBar.Tag = _vehicleData;
            statsBar.Invalidate();
        }

        private void OnStatsBarPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int available = 0, rented = 0, maintenance = 0, lost = 0;

            if (statsBar.Tag is DataTable dt)
            {
                foreach (DataRow row in dt.Rows)
                {
                    string s = row["status"].ToString()!;
                    if (s == "Available") available++;
                    else if (s == "Rented") rented++;
                    else if (s == "Maintenance") maintenance++;
                    if (Convert.ToInt32(row["is_lost"]) == 1) lost++;
                }
            }

            var items = new[] {
                ($"✓ {available} Available",  ColGreen),
                ($"🔑 {rented} Rented",       ColYellow),
                ($"🔧 {maintenance} Maint.",   ColPurple),
                ($"⚠ {lost} Lost Signal",     ColRed),
            };

            int x = 16;
            foreach (var (label, color) in items)
            {
                using var b = new SolidBrush(color);
                g.DrawString(label, new Font("Segoe UI", 9F, FontStyle.Bold), b, x, 16);
                x += 110;
            }
        }

        private void OnCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvVehicles.Columns.Count > 4 && e.ColumnIndex == 4)
            {
                string val = e.Value?.ToString() ?? "";
                e.CellStyle.ForeColor = val switch
                {
                    "Available" => ColGreen,
                    "Rented" => ColYellow,
                    "Maintenance" => ColPurple,
                    "Retired" => ColRed,
                    "Lost Signal" => ColRed,
                    _ => ColSub
                };
                e.CellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            }
        }

        private void StyleGrid(DataGridView dgv)
        {
            dgv.BackgroundColor = ThemeManager.IsDarkMode ? Color.FromArgb(14, 14, 22) : Color.FromArgb(250, 250, 250);
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.GridColor = ThemeManager.IsDarkMode ? Color.FromArgb(25, 25, 38) : Color.FromArgb(230, 230, 235);
            dgv.RowHeadersVisible = false;
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.ReadOnly = true;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgv.Font = new Font("Segoe UI", 10F);
            dgv.RowTemplate.Height = 36;
            dgv.EnableHeadersVisualStyles = false;

            dgv.DefaultCellStyle.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(14, 14, 22) : Color.White;
            dgv.DefaultCellStyle.ForeColor = ColText;
            dgv.DefaultCellStyle.SelectionBackColor = ThemeManager.IsDarkMode ? Color.FromArgb(35, 35, 52) : Color.FromArgb(220, 230, 250);
            dgv.DefaultCellStyle.SelectionForeColor = ThemeManager.CurrentPrimary;
            dgv.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);

            dgv.ColumnHeadersDefaultCellStyle.BackColor = ThemeManager.CurrentBackground;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = ColSub;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgv.ColumnHeadersHeight = 36;
        }

        private Button CreateActionButton(string text, Color color, int x, int y)
        {
            var btn = new Button();
            btn.Text = text;
            btn.Size = new Size(106, 38);
            btn.Location = new Point(x, y);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = color;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, color);
            btn.BackColor = Color.FromArgb(20, color);
            btn.ForeColor = color;
            btn.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btn.Cursor = Cursors.Hand;
            return btn;
        }

        private string Escape(object val) => val?.ToString()?.Replace("'", "\\'") ?? "";

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }
    }

    public class VehicleFormDialog : Form
    {
        private readonly string _connStr;
        private readonly DataRow _existing;
        private TextBox txtBrand, txtModel, txtPlate, txtType, txtRate, txtModel3D;
        private ComboBox cboStatus;

        public VehicleFormDialog(DataRow existing, string connStr)
        {
            _existing = existing;
            _connStr = connStr;
            BuildForm();
        }

        private void BuildForm()
        {
            bool isEdit = _existing != null;
            this.Text = isEdit ? "Edit Vehicle" : "Add Vehicle";
            this.Size = new Size(440, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = ThemeManager.CurrentBackground;
            this.Font = new Font("Segoe UI", 10F);

            int y = 20;
            int tbW = 260;
            int tbX = 140;

            txtBrand = AddField("Brand:", tbX, ref y, tbW);
            txtModel = AddField("Model:", tbX, ref y, tbW);
            txtPlate = AddField("Plate Number:", tbX, ref y, tbW);
            txtType = AddField("Type:", tbX, ref y, tbW);
            txtRate = AddField("Daily Rate (₱):", tbX, ref y, tbW);
            txtModel3D = AddField("Sketchfab URL:", tbX, ref y, tbW);

            var lblStatus = MakeLabel("Status:", 20, y);
            cboStatus = new ComboBox();
            cboStatus.Size = new Size(tbW, 30);
            cboStatus.Location = new Point(tbX, y);
            cboStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            cboStatus.Items.AddRange(new object[] { "Available", "Rented", "Maintenance", "Retired" });
            cboStatus.SelectedIndex = 0;
            cboStatus.BackColor = ThemeManager.CurrentCard;
            cboStatus.ForeColor = ThemeManager.CurrentText;
            this.Controls.Add(lblStatus);
            this.Controls.Add(cboStatus);
            y += 50;

            if (isEdit)
            {
                string fullName = _existing["vehicle_name"].ToString()!;
                var parts = fullName.Split(' ');
                txtBrand.Text = parts.Length > 0 ? parts[0] : "";
                txtModel.Text = parts.Length > 1 ? string.Join(" ", parts, 1, parts.Length - 1) : "";
                txtPlate.Text = _existing["plate_number"].ToString();
                txtType.Text = _existing["type"].ToString();
                txtRate.Text = Convert.ToDecimal(_existing["daily_rate"]).ToString("0.00");
                txtModel3D.Text = _existing["model_3d_url"].ToString();
                var s = _existing["status"].ToString();
                int idx = cboStatus.Items.IndexOf(s);
                if (idx >= 0) cboStatus.SelectedIndex = idx;
            }

            var btnSave = new Button();
            btnSave.Text = isEdit ? "Save Changes" : "Add Vehicle";
            btnSave.Size = new Size(180, 44);
            btnSave.Location = new Point((this.Width - 180) / 2, y);
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.BackColor = ThemeManager.CurrentPrimary;
            btnSave.ForeColor = Color.White;
            btnSave.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            btnSave.Cursor = Cursors.Hand;
            btnSave.Click += OnSave;
            this.Controls.Add(btnSave);
        }

        private void OnSave(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtBrand.Text) || string.IsNullOrWhiteSpace(txtModel.Text) || string.IsNullOrWhiteSpace(txtPlate.Text))
            {
                MessageBox.Show("Brand, Model, and Plate are required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!decimal.TryParse(txtRate.Text, out decimal rate))
            {
                MessageBox.Show("Invalid daily rate.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();

                if (_existing == null)
                {
                    var cmd = new MySqlCommand(@"INSERT INTO vehicles (brand, model, plate_number, type, daily_rate, status, model_3d_url) 
                                                 VALUES (@brand, @model, @plate, @type, @rate, @status, @url)", conn);
                    AddParams(cmd, rate);
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    var cmd = new MySqlCommand(@"UPDATE vehicles SET brand = @brand, model = @model, plate_number = @plate, 
                                                 type = @type, daily_rate = @rate, status = @status, model_3d_url = @url 
                                                 WHERE vehicle_id = @id", conn);
                    AddParams(cmd, rate);
                    cmd.Parameters.AddWithValue("@id", _existing["vehicle_id"]);
                    cmd.ExecuteNonQuery();
                }

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("DB Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddParams(MySqlCommand cmd, decimal rate)
        {
            cmd.Parameters.AddWithValue("@brand", txtBrand.Text.Trim());
            cmd.Parameters.AddWithValue("@model", txtModel.Text.Trim());
            cmd.Parameters.AddWithValue("@plate", txtPlate.Text.Trim());
            cmd.Parameters.AddWithValue("@type", txtType.Text.Trim());
            cmd.Parameters.AddWithValue("@rate", rate);
            cmd.Parameters.AddWithValue("@status", cboStatus.SelectedItem?.ToString() ?? "Available");
            cmd.Parameters.AddWithValue("@url", txtModel3D.Text.Trim());
        }

        private TextBox AddField(string label, int x, ref int y, int w)
        {
            this.Controls.Add(MakeLabel(label, 20, y));
            var tb = new TextBox();
            tb.Size = new Size(w, 30);
            tb.Location = new Point(x, y);
            tb.Font = new Font("Segoe UI", 10F);
            tb.BackColor = ThemeManager.CurrentCard;
            tb.ForeColor = ThemeManager.CurrentText;
            tb.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(tb);
            y += 44;
            return tb;
        }

        private Label MakeLabel(string text, int x, int y)
        {
            var lbl = new Label();
            lbl.Text = text;
            lbl.Font = new Font("Segoe UI", 9F);
            lbl.ForeColor = ThemeManager.CurrentSubText;
            lbl.AutoSize = true;
            lbl.Location = new Point(x, y + 6);
            lbl.BackColor = Color.Transparent;
            return lbl;
        }
    }
}