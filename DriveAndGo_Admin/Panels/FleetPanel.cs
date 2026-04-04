#nullable disable
using DriveAndGo_Admin;
using DriveAndGo_Admin.Helpers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

// Para walang conflict sa VisualStyles
using Button = System.Windows.Forms.Button;
using ComboBox = System.Windows.Forms.ComboBox;
using TextBox = System.Windows.Forms.TextBox;

namespace DriveAndGo_Admin.Panels
{
    public class FleetPanel : UserControl
    {
        // ── Colors ──
        private Color ColBg => ThemeManager.CurrentBackground;
        private Color ColCard => ThemeManager.CurrentCard;
        private Color ColText => ThemeManager.CurrentText;
        private Color ColSub => ThemeManager.CurrentSubText;
        private Color ColBorder => ThemeManager.CurrentBorder;

        private readonly Color ColGreen = Color.FromArgb(34, 197, 94);
        private readonly Color ColRed = Color.FromArgb(239, 68, 68);
        private readonly Color ColBlue = Color.FromArgb(59, 130, 246);
        private readonly Color ColYellow = Color.FromArgb(245, 158, 11);
        private readonly Color ColPurple = Color.FromArgb(168, 85, 247);
        private readonly Color ColAccent = Color.FromArgb(230, 81, 0);

        private const string FirebaseUrl = "https://vechiclerentaldb-default-rtdb.asia-southeast1.firebasedatabase.app";
        private const string FirebaseGpsPath = "/gps.json";

        private readonly string _connStr = "Server=localhost;Database=vehicle_rental_db;Uid=root;Pwd=;";

        // ── UI ──
        private SplitContainer splitContainer;
        private Panel topBar;
        private Panel bottomBar;
        private DataGridView dgvVehicles;
        private WebView2 browser;

        private Label lblTitle, lblCount, lblLiveStatus;
        private Button btnAdd, btnEdit, btnDelete, btnRefresh;
        private TextBox txtSearch;
        private ComboBox cboFilterStatus;

        // ── State ──
        private DataTable _vehicleData = new DataTable();
        private int _selectedId = -1;
        private bool _mapReady = false;

        private System.Windows.Forms.Timer _liveTimer;
        private static readonly HttpClient _http = new HttpClient();

        private double _hqLat = 14.6760;
        private double _hqLng = 121.0437;

        public FleetPanel()
        {
            this.Dock = DockStyle.Fill;
            this.DoubleBuffered = true;
            this.BackColor = ColBg;
            ThemeManager.ThemeChanged += OnThemeChanged;

            BuildUI();
            GetPCApproximateLocation();
            LoadVehiclesFromDB();
            StartLiveGPSPolling();
        }

        private async void GetPCApproximateLocation()
        {
            try
            {
                var resp = await _http.GetStringAsync("http://ip-api.com/json/?fields=lat,lon,city");
                using var doc = JsonDocument.Parse(resp);
                _hqLat = doc.RootElement.GetProperty("lat").GetDouble();
                _hqLng = doc.RootElement.GetProperty("lon").GetDouble();
                string city = doc.RootElement.GetProperty("city").GetString() ?? "HQ";

                if (_mapReady && browser?.CoreWebView2 != null)
                    await browser.CoreWebView2.ExecuteScriptAsync($"setHeadquarters({_hqLat}, {_hqLng}, '{city}');");
            }
            catch { _hqLat = 14.9080; _hqLng = 121.0422; }
        }

        private void BuildUI()
        {
            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 4,
                SplitterDistance = 550,
                BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(40, 40, 55) : Color.FromArgb(220, 220, 230)
            };

            splitContainer.Panel1.BackColor = ColBg;
            splitContainer.Panel2.BackColor = ColBg;
            splitContainer.SplitterMoved += (s, e) => { dgvVehicles?.Invalidate(); };

            BuildLeftPanel();
            BuildRightPanel();

            this.Controls.Add(splitContainer);
        }

        private void BuildLeftPanel()
        {
            topBar = new Panel { Dock = DockStyle.Top, Height = 110, BackColor = ThemeManager.IsDarkMode ? ColBg : Color.FromArgb(250, 250, 255), Padding = new Padding(16, 12, 16, 8) };

            lblTitle = new Label { Text = "Fleet Management", Font = new Font("Segoe UI", 16F, FontStyle.Bold), ForeColor = ColText, AutoSize = true, Location = new Point(16, 14) };
            lblCount = new Label { Text = "Loading...", Font = new Font("Segoe UI", 9F), ForeColor = ColSub, AutoSize = true, Location = new Point(270, 22) };
            lblLiveStatus = new Label { Text = "● LIVE", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = ColGreen, AutoSize = true, Location = new Point(390, 22) };

            topBar.Controls.Add(lblTitle); topBar.Controls.Add(lblCount); topBar.Controls.Add(lblLiveStatus);

            txtSearch = new TextBox { Size = new Size(200, 30), Location = new Point(16, 62), Font = new Font("Segoe UI", 10F), BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(20, 20, 32) : Color.White, ForeColor = ColText, BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "🔍 Search..." };
            txtSearch.TextChanged += OnSearch;

            cboFilterStatus = new ComboBox { Size = new Size(130, 30), Location = new Point(226, 62), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(20, 20, 32) : Color.White, ForeColor = ColText };
            cboFilterStatus.Items.AddRange(new object[] { "All", "available", "Rented", "Maintenance", "Retired" });
            cboFilterStatus.SelectedIndex = 0;
            cboFilterStatus.SelectedIndexChanged += (s, e) => FilterGrid();

            btnRefresh = CreateBtn("⟳", ColGreen, 366, 60, 40);
            btnRefresh.Font = new Font("Segoe UI", 12F);
            btnRefresh.Click += (s, e) => LoadVehiclesFromDB();

            topBar.Controls.Add(txtSearch); topBar.Controls.Add(cboFilterStatus); topBar.Controls.Add(btnRefresh);

            bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(10, 10, 18) : Color.FromArgb(240, 240, 245) };
            bottomBar.Paint += (s, e) => { e.Graphics.DrawLine(new Pen(ColBorder, 1), 0, 0, bottomBar.Width, 0); };

            btnAdd = CreateBtn("✚ Add Vehicle", ColGreen, 16, 12, 130);
            btnEdit = CreateBtn("✎ Edit", ColBlue, 156, 12, 90);
            btnDelete = CreateBtn("🗑 Delete", ColRed, 256, 12, 90);

            btnAdd.Click += OnAddVehicle; btnEdit.Click += OnEditVehicle; btnDelete.Click += OnDeleteVehicle;
            bottomBar.Controls.Add(btnAdd); bottomBar.Controls.Add(btnEdit); bottomBar.Controls.Add(btnDelete);

            dgvVehicles = new DataGridView { Dock = DockStyle.Fill };
            StyleGrid(dgvVehicles);
            dgvVehicles.SelectionChanged += OnVehicleSelected;
            dgvVehicles.CellPainting += OnCellPainting;

            splitContainer.Panel1.Controls.Add(dgvVehicles);
            splitContainer.Panel1.Controls.Add(topBar);
            splitContainer.Panel1.Controls.Add(bottomBar);
            dgvVehicles.BringToFront();
        }

        private void BuildRightPanel()
        {
            browser = new WebView2 { Dock = DockStyle.Fill };
            splitContainer.Panel2.Controls.Add(browser);
            InitWebView();
        }

        private async void InitWebView()
        {
            await browser.EnsureCoreWebView2Async(null);

            string assetsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets");
            if (!Directory.Exists(assetsFolder)) Directory.CreateDirectory(assetsFolder);
            browser.CoreWebView2.SetVirtualHostNameToFolderMapping("appassets", assetsFolder, CoreWebView2HostResourceAccessKind.Allow);

            browser.NavigateToString(GetMapHtml());

            browser.NavigationCompleted += async (s, e) =>
            {
                if (!e.IsSuccess) return;
                _mapReady = true;
                await browser.CoreWebView2.ExecuteScriptAsync($"setTheme({(ThemeManager.IsDarkMode ? "true" : "false")});");
                await browser.CoreWebView2.ExecuteScriptAsync($"setHeadquarters({_hqLat}, {_hqLng}, 'Admin HQ');");
                await PushAllMarkersAsync();
            };
        }

        private async void OnThemeChanged(object s, EventArgs e)
        {
            this.BackColor = ColBg;
            splitContainer.Panel1.BackColor = ColBg;
            splitContainer.Panel2.BackColor = ColBg;
            splitContainer.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(40, 40, 55) : Color.FromArgb(220, 220, 230);

            topBar.BackColor = ThemeManager.IsDarkMode ? ColBg : Color.FromArgb(250, 250, 255);
            bottomBar.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(10, 10, 18) : Color.FromArgb(240, 240, 245);
            lblTitle.ForeColor = ColText;
            lblCount.ForeColor = ColSub;

            txtSearch.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(20, 20, 32) : Color.White;
            txtSearch.ForeColor = ColText;
            cboFilterStatus.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(20, 20, 32) : Color.White;
            cboFilterStatus.ForeColor = ColText;

            StyleGrid(dgvVehicles);

            if (browser?.CoreWebView2 != null)
                await browser.CoreWebView2.ExecuteScriptAsync($"setTheme({(ThemeManager.IsDarkMode ? "true" : "false")});");

            this.Invalidate(true);
        }

        private void LoadVehiclesFromDB()
        {
            _vehicleData = new DataTable();
            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();
                string query = @"
                    SELECT 
                        vehicle_id, CONCAT(brand, ' ', model) AS vehicle_name, plate_no, type, status, rate_per_day,
                        COALESCE(model_3d_url, '') AS model_3d_url, COALESCE(photo_url, '') AS custom_icon_url,
                        latitude, longitude, current_speed AS odometer_km, last_update,
                        CASE WHEN latitude IS NULL THEN 1 ELSE 0 END AS is_lost,
                        NULL AS destination_lat, NULL AS destination_lng
                    FROM vehicles ORDER BY brand, model";

                using var adapter = new MySqlDataAdapter(new MySqlCommand(query, conn));
                adapter.Fill(_vehicleData);
                RefreshGrid(_vehicleData);
                if (_mapReady) _ = PushAllMarkersAsync();

                if (lblLiveStatus.InvokeRequired) lblLiveStatus.Invoke(new Action(() => { lblLiveStatus.Text = "● LIVE"; lblLiveStatus.ForeColor = ColGreen; }));
            }
            catch (Exception ex)
            {
                if (lblLiveStatus.InvokeRequired) lblLiveStatus.Invoke(new Action(() => { lblLiveStatus.Text = "⚠ DB Error"; lblLiveStatus.ForeColor = ColRed; }));
                MessageBox.Show(ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshGrid(DataTable dt)
        {
            if (dgvVehicles.InvokeRequired) { dgvVehicles.Invoke(new Action(() => RefreshGrid(dt))); return; }

            dgvVehicles.DataSource = null; dgvVehicles.Columns.Clear();
            var display = new DataTable();
            display.Columns.Add("ID", typeof(int)); display.Columns.Add("Vehicle", typeof(string)); display.Columns.Add("Plate", typeof(string));
            display.Columns.Add("Type", typeof(string)); display.Columns.Add("Rate/Day", typeof(string)); display.Columns.Add("Status", typeof(string));

            int avail = 0;
            foreach (DataRow row in dt.Rows)
            {
                if (row["vehicle_id"] == DBNull.Value) continue;
                string status = row["status"]?.ToString() ?? "Unknown";
                if (status.ToLower() == "available") avail++;
                display.Rows.Add(row["vehicle_id"], row["vehicle_name"], row["plate_no"], row["type"], "₱" + Convert.ToDecimal(row["rate_per_day"]).ToString("N0"), status);
            }

            dgvVehicles.DataSource = display;
            if (dgvVehicles.Columns.Count >= 6)
            {
                dgvVehicles.Columns[0].Width = 36; dgvVehicles.Columns[1].Width = 160; dgvVehicles.Columns[2].Width = 90;
                dgvVehicles.Columns[3].Width = 80; dgvVehicles.Columns[4].Width = 80; dgvVehicles.Columns[5].Width = 100;
            }
            lblCount.Text = $"{avail} available · {dt.Rows.Count} total";
        }

        private void FilterGrid()
        {
            string filter = cboFilterStatus.SelectedItem?.ToString().ToLower();
            string search = txtSearch.Text.Trim().ToLower();
            var filtered = _vehicleData.Clone();
            foreach (DataRow row in _vehicleData.Rows)
            {
                bool matchStatus = filter == "all" || row["status"].ToString().ToLower() == filter;
                bool matchSearch = string.IsNullOrEmpty(search) || row["vehicle_name"].ToString()!.ToLower().Contains(search) || row["plate_no"].ToString()!.ToLower().Contains(search);
                if (matchStatus && matchSearch) filtered.ImportRow(row);
            }
            RefreshGrid(filtered);
        }

        private void OnSearch(object s, EventArgs e) => FilterGrid();

        private async Task PushAllMarkersAsync()
        {
            if (!_mapReady || browser?.CoreWebView2 == null) return;
            await browser.CoreWebView2.ExecuteScriptAsync("clearMarkers();");
            await browser.CoreWebView2.ExecuteScriptAsync($"setHeadquarters({_hqLat}, {_hqLng}, 'Admin HQ');");
            foreach (DataRow row in _vehicleData.Rows) await PushVehicleMarker(row);
        }

        private async Task PushVehicleMarker(DataRow row)
        {
            if (browser?.CoreWebView2 == null) return;

            double lat = row["latitude"] != DBNull.Value ? Convert.ToDouble(row["latitude"]) : _hqLat;
            double lng = row["longitude"] != DBNull.Value ? Convert.ToDouble(row["longitude"]) : _hqLng;
            bool isLost = row["is_lost"] != DBNull.Value && Convert.ToInt32(row["is_lost"]) == 1;

            int id = Convert.ToInt32(row["vehicle_id"]);
            string name = Esc(row["vehicle_name"]);
            string plate = Esc(row["plate_no"]);
            string type = Esc(row["type"]);
            string status = Esc(row["status"]);
            string model3d = Esc(row["model_3d_url"]);
            string customIcon = Esc(row["custom_icon_url"]);
            double odo = row["odometer_km"] != DBNull.Value ? Convert.ToDouble(row["odometer_km"]) : 0;
            string lastUpd = row["last_update"] != DBNull.Value ? Convert.ToDateTime(row["last_update"]).ToString("MMM dd, HH:mm") : "No data";

            string js = $"updateVehicle({id}, '{name}', '{plate}', '{type}', '{status}', {lat}, {lng}, null, null, 0, {odo}, '{lastUpd}', '{model3d}', '{customIcon}', {(isLost ? "true" : "false")});";
            await browser.CoreWebView2.ExecuteScriptAsync(js);
        }

        private void StartLiveGPSPolling()
        {
            _liveTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _liveTimer.Tick += async (s, e) => { await PollFirebaseGPS(); };
            _liveTimer.Start();
        }

        private async Task PollFirebaseGPS()
        {
            try
            {
                var resp = await _http.GetStringAsync(FirebaseUrl + FirebaseGpsPath);
                if (resp == "null" || string.IsNullOrEmpty(resp)) return;

                using var doc = JsonDocument.Parse(resp);
                foreach (var vehicle in doc.RootElement.EnumerateObject())
                {
                    if (!int.TryParse(vehicle.Name, out int vid)) continue;
                    var val = vehicle.Value;
                    if (!val.TryGetProperty("lat", out var latEl) || !val.TryGetProperty("lng", out var lngEl)) continue;

                    double lat = latEl.GetDouble(); double lng = lngEl.GetDouble();
                    double speed = val.TryGetProperty("speed", out var spEl) ? spEl.GetDouble() : 0;

                    if (_mapReady && browser?.CoreWebView2 != null)
                        await browser.CoreWebView2.ExecuteScriptAsync($"liveUpdateGPS({vid}, {lat}, {lng}, {speed});");

                    using var conn = new MySqlConnection(_connStr);
                    await conn.OpenAsync();
                    using var cmd = new MySqlCommand("UPDATE vehicles SET latitude=@lat, longitude=@lng, current_speed=@sp, last_update=NOW() WHERE vehicle_id=@vid", conn);
                    cmd.Parameters.AddWithValue("@lat", lat); cmd.Parameters.AddWithValue("@lng", lng); cmd.Parameters.AddWithValue("@sp", speed); cmd.Parameters.AddWithValue("@vid", vid);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch { }
        }

        private async void OnVehicleSelected(object sender, EventArgs e)
        {
            if (dgvVehicles.SelectedRows.Count == 0) return;
            int id = Convert.ToInt32(dgvVehicles.SelectedRows[0].Cells[0].Value);
            _selectedId = id;

            var rows = _vehicleData.Select($"vehicle_id = {id}");
            if (rows.Length == 0) return;

            var dr = rows[0];
            double lat = dr["latitude"] != DBNull.Value ? Convert.ToDouble(dr["latitude"]) : _hqLat;
            double lng = dr["longitude"] != DBNull.Value ? Convert.ToDouble(dr["longitude"]) : _hqLng;
            string name = Esc(dr["vehicle_name"]); string plate = Esc(dr["plate_no"]); string status = Esc(dr["status"]);
            string modelUrl = Esc(dr["model_3d_url"]);
            string lastUpd = dr["last_update"] != DBNull.Value ? Convert.ToDateTime(dr["last_update"]).ToString("MMM dd, yyyy HH:mm") : "No data";

            if (_mapReady && browser?.CoreWebView2 != null)
                await browser.CoreWebView2.ExecuteScriptAsync($"focusVehicle({id}, '{name}', '{plate}', '{status}', {lat}, {lng}, 0, '{lastUpd}', '{modelUrl}');");
        }

        private void OnAddVehicle(object s, EventArgs e)
        {
            using var dlg = new VehicleFormDialog(null, _connStr);
            if (dlg.ShowDialog() == DialogResult.OK) LoadVehiclesFromDB();
        }

        private void OnEditVehicle(object s, EventArgs e)
        {
            if (_selectedId < 0) return;
            var rows = _vehicleData.Select($"vehicle_id = {_selectedId}");
            if (rows.Length == 0) return;
            using var dlg = new VehicleFormDialog(rows[0], _connStr);
            if (dlg.ShowDialog() == DialogResult.OK) LoadVehiclesFromDB();
        }

        private void OnDeleteVehicle(object s, EventArgs e)
        {
            if (_selectedId < 0) return;
            if (MessageBox.Show("Delete this vehicle?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                try
                {
                    using var conn = new MySqlConnection(_connStr); conn.Open();
                    using var cmd = new MySqlCommand("DELETE FROM vehicles WHERE vehicle_id=@id", conn);
                    cmd.Parameters.AddWithValue("@id", _selectedId);
                    cmd.ExecuteNonQuery();
                    LoadVehiclesFromDB();
                }
                catch { }
            }
        }

        private void OnCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 5 || e.Value == null) return;
            e.Handled = true;
            e.PaintBackground(e.ClipBounds, true);

            string val = e.Value.ToString();
            Color baseColor = val.ToLower() switch
            {
                "available" => ColGreen,
                "rented" => ColYellow,
                "maintenance" => ColPurple,
                _ => ColRed
            };

            var rect = new Rectangle(e.CellBounds.X + 6, e.CellBounds.Y + 8, e.CellBounds.Width - 12, e.CellBounds.Height - 16);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundRect(rect, 10);

            if (ThemeManager.IsDarkMode)
            {
                e.Graphics.FillPath(new SolidBrush(Color.FromArgb(30, baseColor)), path);
                e.Graphics.DrawPath(new Pen(baseColor, 1.5f), path);
                TextRenderer.DrawText(e.Graphics, val, new Font("Segoe UI", 8.5F, FontStyle.Bold), rect, baseColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                e.Graphics.FillPath(new SolidBrush(Color.FromArgb(20, baseColor)), path);
                e.Graphics.DrawPath(new Pen(baseColor, 1.5f), path);
                TextRenderer.DrawText(e.Graphics, val, new Font("Segoe UI", 8.5F, FontStyle.Bold), rect, baseColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private void StyleGrid(DataGridView dgv)
        {
            dgv.BackgroundColor = ThemeManager.IsDarkMode ? ColBg : Color.FromArgb(250, 250, 255);
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.GridColor = ThemeManager.IsDarkMode ? ColBorder : Color.FromArgb(230, 230, 240);
            dgv.RowHeadersVisible = false;
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.ReadOnly = true;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.Font = new Font("Segoe UI", 10F);
            dgv.RowTemplate.Height = 42;
            dgv.EnableHeadersVisualStyles = false;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            if (ThemeManager.IsDarkMode)
            {
                dgv.DefaultCellStyle.BackColor = ColBg;
                dgv.DefaultCellStyle.ForeColor = ColText;
                dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(30, 30, 45);
                dgv.DefaultCellStyle.SelectionForeColor = ColAccent;
                dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(8, 8, 16);
                dgv.ColumnHeadersDefaultCellStyle.ForeColor = ColSub;
            }
            else
            {
                dgv.DefaultCellStyle.BackColor = Color.White;
                dgv.DefaultCellStyle.ForeColor = Color.FromArgb(30, 30, 45);
                dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 235, 255);
                dgv.DefaultCellStyle.SelectionForeColor = Color.FromArgb(10, 10, 30);
                dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 235, 245);
                dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(80, 80, 100);
            }

            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgv.ColumnHeadersHeight = 40;
        }

        private Button CreateBtn(string text, Color color, int x, int y, int w)
        {
            var btn = new Button { Text = text, Size = new Size(w, 36), Location = new Point(x, y), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand };
            btn.BackColor = Color.FromArgb(20, color); btn.ForeColor = color;
            btn.FlatAppearance.BorderColor = color; btn.FlatAppearance.BorderSize = 1; btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, color);
            return btn;
        }

        private GraphicsPath RoundRect(Rectangle b, int r)
        {
            int d = r * 2; var arc = new Rectangle(b.Location, new Size(d, d)); var path = new GraphicsPath();
            path.AddArc(arc, 180, 90); arc.X = b.Right - d; path.AddArc(arc, 270, 90); arc.Y = b.Bottom - d;
            path.AddArc(arc, 0, 90); arc.X = b.Left; path.AddArc(arc, 90, 90); path.CloseFigure(); return path;
        }

        private string Esc(object v) => v?.ToString()?.Replace("'", "\\'") ?? "";

        protected override void Dispose(bool disposing)
        {
            _liveTimer?.Stop(); _liveTimer?.Dispose();
            ThemeManager.ThemeChanged -= OnThemeChanged;
            browser?.Dispose();
            base.Dispose(disposing);
        }

        private string GetMapHtml()
        {
            return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>DriveAndGo Map</title>
    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body, html { height: 100%; font-family: 'Segoe UI',sans-serif; display: flex; flex-direction: column; overflow: hidden; transition: background 0.3s, color 0.3s; }

        body     { background: #0a0a12; color: #e0e0f0; }
        #topbar  { background: #0e0e1c; border-bottom: 1px solid #1e1e32; }
        #infobar { background: #0c0c18; border-top: 1px solid #1e1e32; }
        #viewer3d { background: #06060e; border-top: 1px solid #1e1e32; }
        .btn { background: #14142a; border: 1px solid #2a2a42; color: #a0a0c0; }
        .btn.active { background: #e6510d; color: #fff; border-color: #e6510d; }
        .leaflet-popup-content-wrapper { background: #1a1a2e !important; color: #e0e0f0 !important; border: 1px solid #3a3a55 !important; }
        .leaflet-popup-tip { background: #1a1a2e !important; }
        .popup-plate { background: #1a1a35; }

        /* Light Theme Overrides */
        body.light-theme { background: #f0f0f5; color: #1a1a2e; }
        body.light-theme #topbar   { background: #ffffff; border-bottom: 1px solid #d0d0e0; }
        body.light-theme #infobar  { background: #ffffff; border-top: 1px solid #d0d0e0; }
        body.light-theme #viewer3d { background: #e8e8f0; border-top: 1px solid #d0d0e0; }
        body.light-theme .btn { background: #f0f0f5; border-color: #d0d0e0; color: #404060; }
        body.light-theme .leaflet-popup-content-wrapper { background: #ffffff !important; color: #1a1a2e !important; border: 1px solid #d0d0e0 !important; box-shadow: 0 4px 15px rgba(0,0,0,0.2) !important; }
        body.light-theme .leaflet-popup-tip { background: #ffffff !important; }
        body.light-theme .popup-plate { background: #e0e0eb; color: #1a1a2e; }
        body.light-theme #speed-badge { background: rgba(255,255,255,0.9); }
        body.light-theme #speed-val   { color: #1a1a2e; }
        body.light-theme .distance-label      { background: rgba(255,255,255,0.9); color: #3b82f6; border-color: #3b82f6; }
        body.light-theme .distance-label.dest { color: #eab308; border-color: #eab308; }
        body.light-theme .car-icon { filter: drop-shadow(0px 4px 6px rgba(0,0,0,0.3)); }

        #topbar { display: flex; align-items: center; justify-content: space-between; padding: 8px 14px; flex-shrink: 0; gap: 10px; transition: background 0.3s; }
        .controls { display: flex; gap: 6px; flex-wrap: wrap; }
        .btn { padding: 4px 10px; border-radius: 5px; cursor: pointer; font-size: 10px; transition: all .2s; white-space: nowrap; }
        #map { flex: 1; width: 100%; min-height: 0; position: relative; }

        #speed-badge { position: absolute; bottom: 14px; right: 14px; background: rgba(10,10,20,.9); border: 2px solid #e6510d; border-radius: 50%; width: 72px; height: 72px; display: none; flex-direction: column; align-items: center; justify-content: center; z-index: 1000; backdrop-filter: blur(8px); box-shadow: 0 2px 10px rgba(0,0,0,0.5); }
        #speed-val  { font-size: 22px; font-weight: 700; color: #fff; line-height: 1; }
        #speed-unit { font-size: 9px; color: #a0a0c0; letter-spacing: 1px; }

        .distance-label      { background: rgba(10,10,20,0.85); color: #3b82f6; border: 1px solid #3b82f6; border-radius: 4px; padding: 2px 6px; font-size: 10px; font-weight: bold; text-align: center; white-space: nowrap; backdrop-filter: blur(4px); box-shadow: 0 2px 5px rgba(0,0,0,0.5); }
        .distance-label.dest { color: #eab308; border-color: #eab308; }

        #infobar { display: flex; align-items: center; gap: 16px; padding: 8px 16px; flex-shrink: 0; min-height: 44px; font-size: 12px; transition: background 0.3s; }
        #viewer3d     { height: 220px; position: relative; flex-shrink: 0; transition: background 0.3s; }
        #model-iframe { width: 100%; height: 100%; border: none; display: none; }
        #placeholder  { position: absolute; top: 50%; left: 50%; transform: translate(-50%,-50%); color: #8080a0; font-size: 13px; text-align: center; pointer-events: none; }

        .car-icon            { width: 44px; height: 44px; background-size: cover; background-repeat: no-repeat; background-position: center; filter: drop-shadow(0px 4px 6px rgba(0,0,0,0.8)); transition: transform 0.5s ease-out; border-radius: 8px; }
        .car-icon.car        { background-image: url('https://cdn-icons-png.flaticon.com/512/2933/2933930.png'); }
        .car-icon.motorcycle { background-image: url('https://cdn-icons-png.flaticon.com/512/2933/2933983.png'); }
        .car-icon.van        { background-image: url('https://cdn-icons-png.flaticon.com/512/2933/2933994.png'); }
        .car-icon.default    { background-image: url('https://cdn-icons-png.flaticon.com/512/2933/2933930.png'); }

        .vehicle-pin       { width: 18px; height: 18px; border-radius: 50%; border: 3px solid #fff; animation: pulse 2s infinite; }
        .vehicle-pin.lost { animation: pulse-red 1.5s infinite; }
        @keyframes pulse     { 0% { box-shadow: 0 0 0 0 rgba(255,255,255,.4); } 70% { box-shadow: 0 0 0 10px rgba(255,255,255,0); } 100% { box-shadow: 0 0 0 0 rgba(255,255,255,0); } }
        @keyframes pulse-red { 0% { box-shadow: 0 0 0 0 rgba(239,68,68,.6);   } 70% { box-shadow: 0 0 0 12px rgba(239,68,68,0);   } 100% { box-shadow: 0 0 0 0 rgba(239,68,68,0);   } }

        .hq-icon  { width: 70px; height: 70px; background-image: url('http://appassets/garage_3D.png'); background-size: cover; background-position: center; border-radius: 8px; border: 2px solid #e6510d; box-shadow: 0 4px 12px rgba(0,0,0,.6); }
        .hq-label { background: #e6510d; color: #fff; padding: 2px 6px; border-radius: 4px; font-size: 10px; font-weight: 700; margin-top: 4px; white-space: nowrap; box-shadow: 0 2px 4px rgba(0,0,0,0.5); text-align: center; }

        .leaflet-popup-content-wrapper { border-radius: 10px !important; }
        .popup-name   { font-size: 13px; font-weight: 700; color: #e6510d; }
        .popup-plate  { font-size: 11px; padding: 1px 6px; border-radius: 4px; margin-top: 2px; display: inline-block; }
        .popup-status { font-size: 11px; margin-top: 6px; }
    </style>
</head>
<body>
    <div id='topbar'>
        <div style='font-weight:bold; color:#e6510d; font-size:12px; display:flex; align-items:center;'>🗺 LIVE FLEET MAP</div>
        <div class='controls'>
            <button class='btn active' id='btnStreet'  onclick=""setLayer('street')"">Street</button>
            <button class='btn'        id='btnSat'     onclick=""setLayer('satellite')"">Satellite</button>
            <button class='btn'        id='btnTraffic' onclick=""toggleTraffic()"">Traffic</button>
            <button class='btn'                        onclick=""fitAllMarkers()"">Fit All</button>
            <button class='btn active' id='btnLines'   onclick=""toggleLines()"">Route Lines</button>
        </div>
    </div>
    <div id='map'>
        <div id='speed-badge'>
            <div id='speed-val'>0</div>
            <div id='speed-unit'>km/h</div>
        </div>
    </div>
    <div id='infobar'>
        <div>🚗 <span id='lblName'>No vehicle selected</span></div>
        <div>🔖 <span id='lblPlate'>—</span></div>
        <div>📍 <span id='lblStatus'>—</span></div>
        <div>⚡ <span id='lblSpeed'>—</span></div>
        <div style='margin-left:auto; color:#a0a0c0; font-size:10px;'>🕐 <span id='lblLastUpdate'>—</span></div>
    </div>
    <div id='viewer3d'>
        <div id='placeholder'>Select a vehicle to view 3D model</div>
        <iframe id='model-iframe' allow='autoplay; fullscreen; xr-spatial-tracking' execution-while-out-of-viewport execution-while-not-rendered web-share></iframe>
    </div>

    <script>
        var map = L.map('map', { zoomControl: true, attributionControl: false }).setView([14.6760, 121.0437], 12);

        var darkStreetLayer  = L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png',    { maxZoom: 20 });
        var lightStreetLayer = L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png',   { maxZoom: 20 });
        var satelliteLayer   = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', { maxZoom: 20 });
        var trafficLayer     = L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', { opacity: 0.25 });

        var activeBaseLayer = darkStreetLayer;
        activeBaseLayer.addTo(map);

        var isDarkMode = true;
        var currentMapType = 'street';
        var trafficOn = false;
        var linesOn = true;

        var markers = {};
        var polylines = {};
        var distMarkers = {};
        var hqMarker = null;
        var hqLat = 14.6760;
        var hqLng = 121.0437;

        function setTheme(isDark) {
            isDarkMode = isDark;
            if (isDark) { document.body.classList.remove('light-theme'); }
            else        { document.body.classList.add('light-theme'); }
            updateBaseLayer();
        }

        function updateBaseLayer() {
            map.removeLayer(darkStreetLayer);
            map.removeLayer(lightStreetLayer);
            map.removeLayer(satelliteLayer);
            if (currentMapType === 'satellite') { activeBaseLayer = satelliteLayer; }
            else { activeBaseLayer = isDarkMode ? darkStreetLayer : lightStreetLayer; }
            activeBaseLayer.addTo(map);
            if (trafficOn) trafficLayer.bringToFront();
        }

        function setLayer(type) {
            currentMapType = type;
            document.getElementById('btnStreet').classList.toggle('active', type === 'street');
            document.getElementById('btnSat').classList.toggle('active', type === 'satellite');
            updateBaseLayer();
        }

        function toggleTraffic() {
            trafficOn = !trafficOn;
            var btn = document.getElementById('btnTraffic');
            if (trafficOn) { trafficLayer.addTo(map); btn.classList.add('active'); trafficLayer.bringToFront(); }
            else           { map.removeLayer(trafficLayer); btn.classList.remove('active'); }
        }

        function toggleLines() {
            linesOn = !linesOn;
            document.getElementById('btnLines').classList.toggle('active', linesOn);
            for (var id in polylines) {
                polylines[id].forEach(function(p) { if(linesOn) map.addLayer(p); else map.removeLayer(p); });
            }
            for (var id in distMarkers) {
                distMarkers[id].forEach(function(m) { if(linesOn) map.addLayer(m); else map.removeLayer(m); });
            }
        }

        function setHeadquarters(lat, lng, cityName) {
            hqLat = lat; hqLng = lng;
            if (hqMarker) map.removeLayer(hqMarker);
            var hqIcon = L.divIcon({
                className: '',
                html: ""<div style='display:flex;flex-direction:column;align-items:center;'><div class='hq-icon'></div><div class='hq-label'>"" + cityName + ""</div></div>"",
                iconSize: [70, 80],
                iconAnchor: [35, 40]
            });
            hqMarker = L.marker([lat, lng], { icon: hqIcon, zIndexOffset: 1000 }).addTo(map);
        }

        function getIconType(type) {
            type = (type || '').toLowerCase();
            if (type.indexOf('motor') !== -1) return 'motorcycle';
            if (type.indexOf('van') !== -1) return 'van';
            return 'car';
        }

        function clearMarkers() {
            for (var id in markers)     { map.removeLayer(markers[id]); }
            for (var id in polylines)   { polylines[id].forEach(function(p) { map.removeLayer(p); }); }
            for (var id in distMarkers) { distMarkers[id].forEach(function(m) { map.removeLayer(m); }); }
            markers = {}; polylines = {}; distMarkers = {};
        }

        async function drawRoute(vehicleId, startLat, startLng, endLat, endLng, color, isDest) {
            try {
                var url = ""https://router.project-osrm.org/route/v1/driving/"" + startLng + "","" + startLat + "";"" + endLng + "","" + endLat + ""?overview=full&geometries=geojson"";
                var response = await fetch(url);
                var data = await response.json();

                var lines = polylines[vehicleId] || [];
                var dMarkers = distMarkers[vehicleId] || [];

                if (data.routes && data.routes.length > 0) {
                    var coords = data.routes[0].geometry.coordinates.map(function(c) { return [c[1], c[0]]; });
                    var distKm = (data.routes[0].distance / 1000).toFixed(1) + ' km';

                    var poly = L.polyline(coords, { color: color, weight: 4, opacity: 0.8 });
                    if (linesOn) poly.addTo(map);
                    lines.push(poly);

                    var midPt = coords[Math.floor(coords.length / 2)];
                    var dm = L.marker(midPt, {
                        icon: L.divIcon({
                            className: isDest ? 'distance-label dest' : 'distance-label',
                            html: distKm,
                            iconSize: [50, 20]
                        })
                    });
                    if (linesOn) dm.addTo(map);
                    dMarkers.push(dm);
                } else {
                    var p = L.polyline([[startLat, startLng], [endLat, endLng]], { color: color, weight: 3, opacity: 0.6, dashArray: '5, 10' });
                    if (linesOn) p.addTo(map);
                    lines.push(p);
                }
                polylines[vehicleId] = lines;
                distMarkers[vehicleId] = dMarkers;
            } catch(err) { }
        }

        function updateVehicle(vehicleId, name, plate, type, status, lat, lng, destLat, destLng, speed, odo, lastUpdate, modelUrl, customIconUrl, isLost) {
            if (markers[vehicleId]) map.removeLayer(markers[vehicleId]);
            if (polylines[vehicleId]) polylines[vehicleId].forEach(function(p) { map.removeLayer(p); });
            if (distMarkers[vehicleId]) distMarkers[vehicleId].forEach(function(m) { map.removeLayer(m); });

            polylines[vehicleId] = [];
            distMarkers[vehicleId] = [];

            var color = '#3b82f6';
            var cssClass = 'vehicle-pin';

            if (isLost || status === 'Lost Signal') {
                color = '#ef4444';
                cssClass = 'vehicle-pin lost';
            } else if (status.toLowerCase() === 'available') { color = '#22c55e'; }
              else if (status.toLowerCase() === 'rented') { color = '#eab308'; }
              else if (status.toLowerCase() === 'maintenance') { color = '#a855f7'; }

            if (!isLost) {
                drawRoute(vehicleId, hqLat, hqLng, lat, lng, '#3b82f6', false);
                if (destLat && destLng && destLat !== 'null' && destLng !== 'null') {
                    drawRoute(vehicleId, lat, lng, destLat, destLng, '#eab308', true);
                }
            }

            var iconHtml = '';
            if (isLost) {
                iconHtml = ""<div class='"" + cssClass + ""' style='background:"" + color + ""'></div>"";
            } else if (customIconUrl && customIconUrl.trim() !== '' && customIconUrl !== 'null') {
                iconHtml = ""<div class='car-icon' style='background-image:url(\"""" + customIconUrl + ""\"");transform:rotate(0deg);'></div>"";
            } else {
                iconHtml = ""<div class='car-icon "" + getIconType(type) + ""' style='transform:rotate(0deg);'></div>"";
            }

            var icon = L.divIcon({ className: '', html: iconHtml, iconSize: [44, 44], iconAnchor: [22, 22] });

            var popupHtml = 
                ""<div class='popup-name'>"" + name + ""</div>"" +
                ""<div class='popup-plate'>"" + plate + ""</div>"" +
                ""<div style='font-size:11px; margin-top:6px;'><span style='color:"" + color + "";'>● </span>"" + status + ""</div>"";

            var marker = L.marker([lat, lng], { icon: icon });
            marker.bindPopup(popupHtml);
            marker.addTo(map);
            marker.on('click', function() { focusVehicle(vehicleId, name, plate, status, lat, lng, speed, lastUpdate, modelUrl); });
            markers[vehicleId] = marker;
        }

        function liveUpdateGPS(vehicleId, lat, lng, speed) {
            if (!markers[vehicleId]) return;

            var oldLatLng = markers[vehicleId].getLatLng();
            markers[vehicleId].setLatLng([lat, lng]);

            var badge = document.getElementById('speed-badge');
            if (speed > 0) {
                badge.style.display = 'flex';
                document.getElementById('speed-val').textContent = speed;
                document.getElementById('lblSpeed').textContent = speed + ' km/h';
                badge.style.borderColor = speed > 80 ? '#ef4444' : speed > 40 ? '#eab308' : '#22c55e';
            } else {
                badge.style.display = 'none';
                document.getElementById('lblSpeed').textContent = 'Parked';
            }

            if (lat !== oldLatLng.lat || lng !== oldLatLng.lng) {
                var dy = lat - oldLatLng.lat;
                var dx = Math.cos(Math.PI / 180 * oldLatLng.lat) * (lng - oldLatLng.lng);
                var angle = Math.atan2(dy, dx) * 180 / Math.PI;
                var bearing = (90 - angle + 360) % 360;
                var iconDiv = markers[vehicleId].getElement().querySelector('.car-icon');
                if (iconDiv) iconDiv.style.transform = ""rotate("" + bearing + ""deg)"";
            }
        }

        function focusVehicle(vehicleId, name, plate, status, lat, lng, speed, lastUpdate, modelUrl) {
            document.getElementById('lblName').textContent = name;
            document.getElementById('lblPlate').textContent = plate;
            document.getElementById('lblStatus').textContent = status;
            document.getElementById('lblSpeed').textContent = speed > 0 ? speed + ' km/h' : 'Parked';
            document.getElementById('lblLastUpdate').textContent = lastUpdate;

            map.flyTo([lat, lng], 16, { animate: true, duration: 1.2 });
            if (markers[vehicleId]) markers[vehicleId].openPopup();

            var iframe = document.getElementById('model-iframe');
            var ph = document.getElementById('placeholder');
            if (modelUrl && modelUrl.trim() !== '' && modelUrl !== 'null') {
                iframe.src = modelUrl;
                iframe.style.display = 'block';
                ph.style.display = 'none';
            } else {
                iframe.src = '';
                iframe.style.display = 'none';
                ph.style.display = 'block';
            }
        }

        function fitAllMarkers() {
            var bounds = [];
            for (var id in markers) bounds.push(markers[id].getLatLng());
            if (hqMarker) bounds.push(hqMarker.getLatLng());
            if (bounds.length > 0) map.fitBounds(bounds, { padding: [50, 50] });
        }
    </script>
</body>
</html>";
        }
    }

    // ══ VEHICLE ADD/EDIT DIALOG WITH BROWSE BUTTON ══
    public class VehicleFormDialog : Form
    {
        private readonly string _connStr;
        private readonly DataRow _existing;
        private TextBox txtBrand, txtModel, txtPlate, txtType, txtRate, txtModel3D, txtIconUrl;
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
            this.Size = new Size(440, 550);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = ThemeManager.CurrentBackground;
            this.Font = new Font("Segoe UI", 10F);

            int y = 20, tbW = 260, tbX = 140;

            txtBrand = AddField("Brand:", tbX, ref y, tbW);
            txtModel = AddField("Model:", tbX, ref y, tbW);
            txtPlate = AddField("Plate Number:", tbX, ref y, tbW);
            txtType = AddField("Type:", tbX, ref y, tbW);
            txtRate = AddField("Daily Rate (₱):", tbX, ref y, tbW);
            txtModel3D = AddField("3D Model URL:", tbX, ref y, tbW);

            this.Controls.Add(new Label { Text = "Map Icon:", Font = new Font("Segoe UI", 9F), ForeColor = ThemeManager.CurrentSubText, AutoSize = true, Location = new Point(20, y + 6), BackColor = Color.Transparent });
            txtIconUrl = new TextBox { Size = new Size(tbW - 40, 30), Location = new Point(tbX, y), Font = new Font("Segoe UI", 10F), BackColor = ThemeManager.CurrentCard, ForeColor = ThemeManager.CurrentText, BorderStyle = BorderStyle.FixedSingle, ReadOnly = true };
            this.Controls.Add(txtIconUrl);

            Button btnBrowse = new Button { Text = "...", Size = new Size(35, 30), Location = new Point(tbX + tbW - 35, y), FlatStyle = FlatStyle.Flat, BackColor = ThemeManager.CurrentPrimary, ForeColor = Color.White, Cursor = Cursors.Hand };
            btnBrowse.FlatAppearance.BorderSize = 0;
            btnBrowse.Click += (s, e) =>
            {
                using OpenFileDialog ofd = new OpenFileDialog { Filter = "Image Files|*.png;*.jpg;*.jpeg", Title = "Select Top-Down Vehicle Image" };
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtIconUrl.Text = ofd.FileName;
                }
            };
            this.Controls.Add(btnBrowse);
            y += 44;

            this.Controls.Add(new Label { Text = "Status:", Font = new Font("Segoe UI", 9F), ForeColor = ThemeManager.CurrentSubText, AutoSize = true, Location = new Point(20, y + 6), BackColor = Color.Transparent });
            cboStatus = new ComboBox
            {
                Size = new Size(tbW, 30),
                Location = new Point(tbX, y),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = ThemeManager.CurrentCard,
                ForeColor = ThemeManager.CurrentText
            };
            cboStatus.Items.AddRange(new object[] { "Available", "Rented", "Maintenance", "Retired" });
            cboStatus.SelectedIndex = 0;
            this.Controls.Add(cboStatus);
            y += 50;

            if (isEdit)
            {
                string fullName = _existing["vehicle_name"].ToString()!;
                var parts = fullName.Split(' ');
                txtBrand.Text = parts.Length > 0 ? parts[0] : "";
                txtModel.Text = parts.Length > 1 ? string.Join(" ", parts, 1, parts.Length - 1) : "";
                txtPlate.Text = _existing["plate_no"].ToString();
                txtType.Text = _existing["type"].ToString();
                txtRate.Text = Convert.ToDecimal(_existing["rate_per_day"]).ToString("0.00");
                txtModel3D.Text = _existing["model_3d_url"].ToString();
                txtIconUrl.Text = _existing["custom_icon_url"].ToString();

                string s = _existing["status"].ToString();
                for (int i = 0; i < cboStatus.Items.Count; i++)
                    if (cboStatus.Items[i].ToString().ToLower() == s.ToLower()) { cboStatus.SelectedIndex = i; break; }
            }

            var btnSave = new Button { Text = isEdit ? "Save Changes" : "Add Vehicle", Size = new Size(180, 44), Location = new Point((this.Width - 180) / 2, y), FlatStyle = FlatStyle.Flat, BackColor = ThemeManager.CurrentPrimary, ForeColor = Color.White, Font = new Font("Segoe UI", 11F, FontStyle.Bold), Cursor = Cursors.Hand };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += OnSave;
            this.Controls.Add(btnSave);
        }

        private void OnSave(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtBrand.Text) || string.IsNullOrWhiteSpace(txtPlate.Text))
            { MessageBox.Show("Brand, Model, and Plate are required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            if (!decimal.TryParse(txtRate.Text, out decimal rate))
            { MessageBox.Show("Invalid daily rate.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            string finalIconUrl = txtIconUrl.Text.Trim();
            if (finalIconUrl.Contains("\\") && File.Exists(finalIconUrl))
            {
                try
                {
                    string assetsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets");
                    if (!Directory.Exists(assetsFolder)) Directory.CreateDirectory(assetsFolder);

                    string extension = Path.GetExtension(finalIconUrl);
                    string newFileName = "vehicle_" + DateTime.Now.Ticks + extension;
                    string destinationPath = Path.Combine(assetsFolder, newFileName);

                    File.Copy(finalIconUrl, destinationPath, true);
                    finalIconUrl = "http://appassets/" + newFileName;
                }
                catch { }
            }

            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();

                if (_existing == null)
                {
                    var cmd = new MySqlCommand(@"INSERT INTO vehicles (brand, model, plate_no, type, rate_per_day, status, model_3d_url, photo_url)
                                                 VALUES (@brand, @model, @plate, @type, @rate, @status, @url, @icon)", conn);
                    AddParams(cmd, rate, finalIconUrl);
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    var cmd = new MySqlCommand(@"UPDATE vehicles SET brand=@brand, model=@model, plate_no=@plate, type=@type,
                                                 rate_per_day=@rate, status=@status, model_3d_url=@url, photo_url=@icon
                                                 WHERE vehicle_id=@id", conn);
                    AddParams(cmd, rate, finalIconUrl);
                    cmd.Parameters.AddWithValue("@id", _existing["vehicle_id"]);
                    cmd.ExecuteNonQuery();
                }

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex) { MessageBox.Show("DB Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void AddParams(MySqlCommand cmd, decimal rate, string iconUrl)
        {
            cmd.Parameters.AddWithValue("@brand", txtBrand.Text.Trim());
            cmd.Parameters.AddWithValue("@model", txtModel.Text.Trim());
            cmd.Parameters.AddWithValue("@plate", txtPlate.Text.Trim());
            cmd.Parameters.AddWithValue("@type", txtType.Text.Trim());
            cmd.Parameters.AddWithValue("@rate", rate);
            cmd.Parameters.AddWithValue("@status", cboStatus.SelectedItem?.ToString().ToLower() ?? "available");
            cmd.Parameters.AddWithValue("@url", txtModel3D.Text.Trim());
            cmd.Parameters.AddWithValue("@icon", iconUrl);
        }

        private TextBox AddField(string label, int x, ref int y, int w)
        {
            this.Controls.Add(new Label { Text = label, Font = new Font("Segoe UI", 9F), ForeColor = ThemeManager.CurrentSubText, AutoSize = true, Location = new Point(20, y + 6), BackColor = Color.Transparent });
            var tb = new TextBox { Size = new Size(w, 30), Location = new Point(x, y), Font = new Font("Segoe UI", 10F), BackColor = ThemeManager.CurrentCard, ForeColor = ThemeManager.CurrentText, BorderStyle = BorderStyle.FixedSingle };
            this.Controls.Add(tb);
            y += 44;
            return tb;
        }
    }
}