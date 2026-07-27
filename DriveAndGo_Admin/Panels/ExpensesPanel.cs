#nullable disable
using DriveAndGo_Admin.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinColor = System.Drawing.Color;

namespace DriveAndGo_Admin.Panels
{
    public class ExpensesPanel : UserControl
    {
        // ── Theme colors ──
        private WinColor ColBg => ThemeManager.CurrentBackground;
        private WinColor ColCard => ThemeManager.CurrentCard;
        private WinColor ColText => ThemeManager.CurrentText;
        private WinColor ColSub => ThemeManager.CurrentSubText;
        private WinColor ColBorder => ThemeManager.CurrentBorder;
        private WinColor ColAccent = WinColor.FromArgb(255, 90, 31);
        private WinColor ColGreen = WinColor.FromArgb(34, 197, 94);
        private WinColor ColBlue = WinColor.FromArgb(59, 130, 246);
        private WinColor ColRed = WinColor.FromArgb(239, 68, 68);

        // ── Tabs ──
        private TabControl tabControl;
        private TabPage tabExpensesLog;
        private TabPage tabMaintenance;
        private TabPage tabFuel;

        // ── Tab 1: Expenses Log Controls ──
        private Panel _headerPanel;
        private Label _titleLabel;
        private Label _subtitleLabel;
        private Button _btnAddExpense;
        private Button _btnScanReceipt;
        private Panel _statsPanel;
        private Label _lblTotalSpent;
        private Label _lblFuelSpent;
        private Label _lblTollSpent;
        private Label _lblMaintSpent;
        private FlowLayoutPanel _filterPanel;
        private Label _lblFilterVehicle;
        private ComboBox _cmbVehicle;
        private Label _lblFilterCategory;
        private ComboBox _cmbCategory;
        private Button _btnRefresh;
        private DataGridView _grid;
        private List<ExpenseItem> _expenses = new();

        // ── Tab 2: Maintenance Controls ──
        private SplitContainer splitMaint;
        private DataGridView dgvMaint;
        private ComboBox cboMaintVehicle;
        private TextBox txtMaintDesc;
        private TextBox txtMaintCost;
        private ComboBox cboMaintStatus;
        private DateTimePicker dtMaintScheduled;
        private Button btnSaveMaint;
        private Button btnCompleteMaint;
        private Button btnDeleteMaint;
        private DataTable dtMaintData = new DataTable();
        private int selectedMaintId = -1;

        // ── Tab 3: Fuel Controls ──
        private SplitContainer splitFuel;
        private DataGridView dgvFuel;
        private ComboBox cboFuelVehicle;
        private TextBox txtFuelQty;
        private TextBox txtFuelCost;
        private TextBox txtFuelOdo;
        private Button btnSaveFuel;
        private Button btnDeleteFuel;
        private DataTable dtFuelData = new DataTable();
        private int selectedFuelId = -1;

        // Shared vehicle list state
        private List<KeyValuePair<int, string>> vehicleList = new List<KeyValuePair<int, string>>();

        public ExpensesPanel()
        {
            this.Dock = DockStyle.Fill;
            this.DoubleBuffered = true;
            this.BackColor = ColBg;
            ThemeManager.ThemeChanged += (s, e) => ApplyTheme();

            BuildUI();
            this.Load += async (s, e) =>
            {
                await LoadVehiclesList();
                await LoadExpensesAsync();
                await RefreshMaintenance();
                await RefreshFuel();
            };
        }

        private void BuildUI()
        {
            // Tab Control Setup
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F),
                Padding = new Point(12, 4)
            };

            tabExpensesLog = new TabPage { Text = "💰  Expenses Log & OCR" };
            tabMaintenance = new TabPage { Text = "🔧  Service & Maintenance" };
            tabFuel = new TabPage { Text = "⛽  Fuel Logging" };

            tabControl.TabPages.AddRange(new TabPage[] { tabExpensesLog, tabMaintenance, tabFuel });
            this.Controls.Add(tabControl);

            BuildExpensesTab();
            BuildMaintenanceTab();
            BuildFuelTab();
            ApplyTheme();
        }

        // ══════════════════════════════════════════════
        //  TAB 1: EXPENSES LOG
        // ══════════════════════════════════════════════
        private void BuildExpensesTab()
        {
            // Header Panel
            _headerPanel = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = Color.Transparent };
            _titleLabel = new Label { Text = "💰  Vehicle Expenses & AI OCR Scanner", Font = new Font("Segoe UI", 18F, FontStyle.Bold), AutoSize = true, Location = new Point(16, 8) };
            _subtitleLabel = new Label { Text = "Track overall expenses, tolls, servicing bills, and scan billing receipts using AI OCR", Font = new Font("Segoe UI", 9.5F), AutoSize = true, Location = new Point(18, 44) };
            
            _btnAddExpense = CreateBtn("➕  Log Expense", ColAccent, 0, 20, 140);
            _btnAddExpense.Click += (s, e) => ShowAddExpenseDialog();
            _btnScanReceipt = CreateBtn("📷  Scan Receipt (OCR)", ColGreen, 0, 20, 170);
            _btnScanReceipt.Click += btnScanReceipt_Click;

            _headerPanel.Controls.AddRange(new Control[] { _titleLabel, _subtitleLabel, _btnAddExpense, _btnScanReceipt });

            // Stats
            _statsPanel = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = Color.Transparent };
            _lblTotalSpent = CreateStatCard("Total Spent", "₱0.00", 16);
            _lblFuelSpent = CreateStatCard("Fuel", "₱0.00", 200);
            _lblTollSpent = CreateStatCard("Tolls", "₱0.00", 385);
            _lblMaintSpent = CreateStatCard("Maintenance", "₱0.00", 570);
            _statsPanel.Controls.AddRange(new Control[] { _lblTotalSpent, _lblFuelSpent, _lblTollSpent, _lblMaintSpent });

            // Filters
            _filterPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(16, 10, 0, 10), BackColor = Color.Transparent };
            _lblFilterVehicle = new Label { Text = "Vehicle:", Font = new Font("Segoe UI", 9F), AutoSize = true, Margin = new Padding(0, 6, 5, 0) };
            _cmbVehicle = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbVehicle.SelectedIndexChanged += (s, e) => FilterExpenses();
            
            _lblFilterCategory = new Label { Text = "Category:", Font = new Font("Segoe UI", 9F), AutoSize = true, Margin = new Padding(20, 6, 5, 0) };
            _cmbCategory = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbCategory.Items.AddRange(new object[] { "All", "fuel", "toll", "maintenance", "parking", "other" });
            _cmbCategory.SelectedIndex = 0;
            _cmbCategory.SelectedIndexChanged += (s, e) => FilterExpenses();

            _btnRefresh = CreateBtn("🔄 Refresh", ColAccent, 0, 0, 100);
            _btnRefresh.Size = new Size(100, 28);
            _btnRefresh.Click += async (s, e) => await LoadExpensesAsync();

            _filterPanel.Controls.AddRange(new Control[] { _lblFilterVehicle, _cmbVehicle, _lblFilterCategory, _cmbCategory, _btnRefresh });

            // Grid
            _grid = new DataGridView { Dock = DockStyle.Fill };
            StyleGrid(_grid);
            _grid.Columns.Add("expenseId", "ID");
            _grid.Columns.Add("vehicleId", "Vehicle ID");
            _grid.Columns.Add("category", "Category");
            _grid.Columns.Add("amount", "Amount");
            _grid.Columns.Add("createdAt", "Date Logged");
            _grid.Columns["expenseId"].Width = 60;
            _grid.Columns["amount"].DefaultCellStyle.Format = "₱#,##0.00";

            var innerLogLayout = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            innerLogLayout.Controls.Add(_grid);
            innerLogLayout.Controls.Add(_filterPanel);
            innerLogLayout.Controls.Add(_statsPanel);
            innerLogLayout.Controls.Add(_headerPanel);
            tabExpensesLog.Controls.Add(innerLogLayout);

            // Resize alignment
            tabExpensesLog.Resize += (s, e) =>
            {
                if (_btnAddExpense != null)
                {
                    _btnAddExpense.Left = tabExpensesLog.Width - 330;
                    _btnScanReceipt.Left = tabExpensesLog.Width - 180;
                }
            };
        }

        // ══════════════════════════════════════════════
        //  TAB 2: MAINTENANCE SCHEDULER
        // ══════════════════════════════════════════════
        private void BuildMaintenanceTab()
        {
            splitMaint = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 600, SplitterWidth = 5 };
            tabMaintenance.Controls.Add(splitMaint);

            dgvMaint = new DataGridView { Dock = DockStyle.Fill };
            StyleGrid(dgvMaint);
            dgvMaint.SelectionChanged += (s, e) =>
            {
                if (dgvMaint.SelectedRows.Count > 0)
                {
                    var row = dgvMaint.SelectedRows[0];
                    selectedMaintId = Convert.ToInt32(row.Cells["maintenanceId"].Value);
                    txtMaintDesc.Text = row.Cells["description"].Value?.ToString();
                    txtMaintCost.Text = row.Cells["cost"].Value?.ToString();
                    cboMaintStatus.SelectedItem = row.Cells["status"].Value?.ToString();
                    dtMaintScheduled.Value = Convert.ToDateTime(row.Cells["scheduledDate"].Value);

                    int vId = Convert.ToInt32(row.Cells["vehicleId"].Value);
                    for (int i = 0; i < cboMaintVehicle.Items.Count; i++)
                    {
                        var item = (KeyValuePair<int, string>)cboMaintVehicle.Items[i];
                        if (item.Key == vId) { cboMaintVehicle.SelectedIndex = i; break; }
                    }
                    btnCompleteMaint.Enabled = row.Cells["status"].Value?.ToString().ToLower() != "completed";
                    btnDeleteMaint.Enabled = true;
                }
                else { ClearMaintForm(); }
            };
            splitMaint.Panel1.Controls.Add(dgvMaint);

            var pnlForm = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
            int y = 16;
            var lblFormTitle = new Label { Text = "Schedule Servicing", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = ColText, Location = new Point(16, y), AutoSize = true };
            y += 36;
            var lblV = new Label { Text = "Select Vehicle:", Location = new Point(16, y), AutoSize = true, ForeColor = ColSub };
            cboMaintVehicle = new ComboBox { Location = new Point(16, y + 20), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            y += 56;
            var lblD = new Label { Text = "Task/Service Description:", Location = new Point(16, y), AutoSize = true, ForeColor = ColSub };
            txtMaintDesc = new TextBox { Location = new Point(16, y + 20), Width = 260, Multiline = true, Height = 60 };
            y += 92;
            var lblC = new Label { Text = "Estimated Cost (PHP):", Location = new Point(16, y), AutoSize = true, ForeColor = ColSub };
            txtMaintCost = new TextBox { Location = new Point(16, y + 20), Width = 260, Text = "0.00" };
            y += 56;
            var lblS = new Label { Text = "Service Status:", Location = new Point(16, y), AutoSize = true, ForeColor = ColSub };
            cboMaintStatus = new ComboBox { Location = new Point(16, y + 20), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            cboMaintStatus.Items.AddRange(new object[] { "scheduled", "active", "completed" });
            cboMaintStatus.SelectedIndex = 0;
            y += 56;
            var lblDt = new Label { Text = "Scheduled Date:", Location = new Point(16, y), AutoSize = true, ForeColor = ColSub };
            dtMaintScheduled = new DateTimePicker { Location = new Point(16, y + 20), Width = 260, Format = DateTimePickerFormat.Short };
            y += 66;

            btnSaveMaint = CreateBtn("Save Schedule", ColAccent, 16, y, 125);
            btnSaveMaint.Click += async (s, e) => await SaveMaintenance();
            btnCompleteMaint = CreateBtn("Complete Service", ColGreen, 151, y, 125);
            btnCompleteMaint.Click += async (s, e) => await CompleteMaintenance();
            y += 46;
            btnDeleteMaint = CreateBtn("Delete Log", ColRed, 16, y, 260);
            btnDeleteMaint.Click += async (s, e) => await DeleteMaintenance();

            pnlForm.Controls.AddRange(new Control[] {
                lblFormTitle, lblV, cboMaintVehicle, lblD, txtMaintDesc, lblC, txtMaintCost,
                lblS, cboMaintStatus, lblDt, dtMaintScheduled, btnSaveMaint, btnCompleteMaint, btnDeleteMaint
            });
            splitMaint.Panel2.Controls.Add(pnlForm);
        }

        // ══════════════════════════════════════════════
        //  TAB 3: FUEL LOGGING
        // ══════════════════════════════════════════════
        private void BuildFuelTab()
        {
            splitFuel = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 600, SplitterWidth = 5 };
            tabFuel.Controls.Add(splitFuel);

            dgvFuel = new DataGridView { Dock = DockStyle.Fill };
            StyleGrid(dgvFuel);
            dgvFuel.SelectionChanged += (s, e) =>
            {
                if (dgvFuel.SelectedRows.Count > 0)
                {
                    selectedFuelId = Convert.ToInt32(dgvFuel.SelectedRows[0].Cells["fuelLogId"].Value);
                    btnDeleteFuel.Enabled = true;
                }
                else { selectedFuelId = -1; btnDeleteFuel.Enabled = false; }
            };
            splitFuel.Panel1.Controls.Add(dgvFuel);

            var pnlForm = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
            int y = 16;
            var lblFormTitle = new Label { Text = "Log Fuel Purchase", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = ColText, Location = new Point(16, y), AutoSize = true };
            y += 36;
            var lblV = new Label { Text = "Select Vehicle:", Location = new Point(16, y), AutoSize = true, ForeColor = ColSub };
            cboFuelVehicle = new ComboBox { Location = new Point(16, y + 20), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            y += 56;
            var lblQty = new Label { Text = "Quantity in Liters:", Location = new Point(16, y), AutoSize = true, ForeColor = ColSub };
            txtFuelQty = new TextBox { Location = new Point(16, y + 20), Width = 260, Text = "0.00" };
            y += 56;
            var lblC = new Label { Text = "Total Cost (PHP):", Location = new Point(16, y), AutoSize = true, ForeColor = ColSub };
            txtFuelCost = new TextBox { Location = new Point(16, y + 20), Width = 260, Text = "0.00" };
            y += 56;
            var lblOdo = new Label { Text = "Current Odometer (km):", Location = new Point(16, y), AutoSize = true, ForeColor = ColSub };
            txtFuelOdo = new TextBox { Location = new Point(16, y + 20), Width = 260, Text = "0.00" };
            y += 66;

            btnSaveFuel = CreateBtn("Save Fuel Log", ColAccent, 16, y, 260);
            btnSaveFuel.Click += async (s, e) => await SaveFuel();
            y += 46;
            btnDeleteFuel = CreateBtn("Delete Fuel Log", ColRed, 16, y, 260);
            btnDeleteFuel.Click += async (s, e) => await DeleteFuel();

            pnlForm.Controls.AddRange(new Control[] {
                lblFormTitle, lblV, cboFuelVehicle, lblQty, txtFuelQty, lblC, txtFuelCost, lblOdo, txtFuelOdo, btnSaveFuel, btnDeleteFuel
            });
            splitFuel.Panel2.Controls.Add(pnlForm);
        }

        // ══════════════════════════════════════════════
        //  THEMING
        // ══════════════════════════════════════════════
        private void ApplyTheme()
        {
            if (_titleLabel == null) return;

            // Apply base control backgrounds to support dark/light mode switches
            this.BackColor = ColBg;
            tabControl.BackColor = ColBg;
            tabExpensesLog.BackColor = ColBg;
            tabMaintenance.BackColor = ColBg;
            tabFuel.BackColor = ColBg;

            splitMaint.Panel1.BackColor = ColBg;
            splitFuel.Panel1.BackColor = ColBg;

            foreach (TabPage tab in tabControl.TabPages)
            {
                tab.BackColor = ColBg;
            }

            _titleLabel.ForeColor = ThemeManager.CurrentText;
            _subtitleLabel.ForeColor = ThemeManager.CurrentSubText;
            _lblFilterVehicle.ForeColor = ThemeManager.CurrentText;
            _lblFilterCategory.ForeColor = ThemeManager.CurrentText;

            _cmbVehicle.BackColor = ThemeManager.CurrentCard;
            _cmbVehicle.ForeColor = ThemeManager.CurrentText;
            _cmbCategory.BackColor = ThemeManager.CurrentCard;
            _cmbCategory.ForeColor = ThemeManager.CurrentText;

            splitMaint.Panel2.BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.FromArgb(245, 245, 250);
            splitFuel.Panel2.BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.FromArgb(245, 245, 250);

            _lblTotalSpent.BackColor = ThemeManager.CurrentCard;
            _lblFuelSpent.BackColor = ThemeManager.CurrentCard;
            _lblTollSpent.BackColor = ThemeManager.CurrentCard;
            _lblMaintSpent.BackColor = ThemeManager.CurrentCard;

            _lblTotalSpent.Invalidate();
            _lblFuelSpent.Invalidate();
            _lblTollSpent.Invalidate();
            _lblMaintSpent.Invalidate();

            StyleGrid(_grid);
            StyleGrid(dgvMaint);
            StyleGrid(dgvFuel);
        }

        // ══════════════════════════════════════════════
        //  DATA LOADS & REFRESH
        // ══════════════════════════════════════════════
        private async Task LoadVehiclesList()
        {
            try
            {
                var res = await ApiService.GetAsync("vehicles");
                if (res.Success)
                {
                    vehicleList.Clear();
                    using var doc = JsonDocument.Parse(res.Body);
                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        int id = elem.GetProperty("vehicleId").GetInt32();
                        string brand = elem.GetProperty("brand").GetString();
                        string model = elem.GetProperty("model").GetString();
                        string plate = elem.GetProperty("plateNumber").GetString();
                        vehicleList.Add(new KeyValuePair<int, string>(id, $"{brand} {model} [{plate}]"));
                    }

                    cboMaintVehicle.DataSource = new BindingSource(vehicleList, null);
                    cboMaintVehicle.DisplayMember = "Value";
                    cboMaintVehicle.ValueMember = "Key";

                    cboFuelVehicle.DataSource = new BindingSource(vehicleList, null);
                    cboFuelVehicle.DisplayMember = "Value";
                    cboFuelVehicle.ValueMember = "Key";
                }
            }
            catch { }
        }

        private async Task LoadExpensesAsync()
        {
            try
            {
                var result = await ApiService.GetAsync("expenses");
                if (result.Success)
                {
                    var root = JsonDocument.Parse(result.Body).RootElement;
                    _expenses.Clear();
                    var filterVList = new HashSet<string>();

                    foreach (var element in root.EnumerateArray())
                    {
                        var exp = new ExpenseItem
                        {
                            ExpenseId = element.GetProperty("expenseId").GetInt32(),
                            VehicleId = element.GetProperty("vehicleId").ValueKind != JsonValueKind.Null ? element.GetProperty("vehicleId").GetInt32() : (int?)null,
                            Category = element.GetProperty("category").GetString() ?? "other",
                            Amount = element.GetProperty("amount").GetDecimal(),
                            CreatedAt = element.GetProperty("createdAt").GetString() ?? ""
                        };
                        _expenses.Add(exp);

                        if (exp.VehicleId.HasValue) filterVList.Add(exp.VehicleId.Value.ToString());
                    }

                    _cmbVehicle.Items.Clear();
                    _cmbVehicle.Items.Add("All Vehicles");
                    foreach (var v in filterVList)
                    {
                        _cmbVehicle.Items.Add($"Vehicle #{v}");
                    }
                    _cmbVehicle.SelectedIndex = 0;
                    FilterExpenses();
                }
            }
            catch { }
        }

        private void FilterExpenses()
        {
            _grid.Rows.Clear();
            decimal total = 0, fuel = 0, toll = 0, maint = 0;
            string selVehicle = _cmbVehicle.SelectedItem?.ToString() ?? "All Vehicles";
            string selCat = _cmbCategory.SelectedItem?.ToString() ?? "All";

            foreach (var exp in _expenses)
            {
                if (selVehicle != "All Vehicles")
                {
                    string vidStr = exp.VehicleId.HasValue ? $"Vehicle #{exp.VehicleId.Value}" : "";
                    if (vidStr != selVehicle) continue;
                }
                if (selCat != "All" && exp.Category.ToLower() != selCat.ToLower()) continue;

                _grid.Rows.Add(exp.ExpenseId, exp.VehicleId.HasValue ? exp.VehicleId.Value.ToString() : "N/A", exp.Category, exp.Amount, exp.CreatedAt);
                total += exp.Amount;
                if (exp.Category.ToLower() == "fuel") fuel += exp.Amount;
                else if (exp.Category.ToLower() == "toll") toll += exp.Amount;
                else if (exp.Category.ToLower() == "maintenance") maint += exp.Amount;
            }

            UpdateStatCard(_lblTotalSpent, $"₱{total:N2}");
            UpdateStatCard(_lblFuelSpent, $"₱{fuel:N2}");
            UpdateStatCard(_lblTollSpent, $"₱{toll:N2}");
            UpdateStatCard(_lblMaintSpent, $"₱{maint:N2}");
        }

        private async Task RefreshMaintenance()
        {
            try
            {
                var res = await ApiService.GetAsync("maintenance");
                if (res.Success)
                {
                    using var doc = JsonDocument.Parse(res.Body);
                    dtMaintData = new DataTable();
                    dtMaintData.Columns.Add("maintenanceId", typeof(int));
                    dtMaintData.Columns.Add("vehicleId", typeof(int));
                    dtMaintData.Columns.Add("vehicleName", typeof(string));
                    dtMaintData.Columns.Add("plateNo", typeof(string));
                    dtMaintData.Columns.Add("description", typeof(string));
                    dtMaintData.Columns.Add("cost", typeof(decimal));
                    dtMaintData.Columns.Add("status", typeof(string));
                    dtMaintData.Columns.Add("scheduledDate", typeof(DateTime));
                    dtMaintData.Columns.Add("completedDate", typeof(string));

                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        string compDate = elem.TryGetProperty("completedDate", out var cd) && cd.ValueKind != JsonValueKind.Null
                            ? Convert.ToDateTime(cd.GetString()).ToShortDateString() : "—";

                        dtMaintData.Rows.Add(
                            elem.GetProperty("maintenanceId").GetInt32(),
                            elem.GetProperty("vehicleId").GetInt32(),
                            elem.GetProperty("vehicleName").GetString(),
                            elem.GetProperty("plateNo").GetString(),
                            elem.GetProperty("description").GetString(),
                            elem.GetProperty("cost").GetDecimal(),
                            elem.GetProperty("status").GetString(),
                            Convert.ToDateTime(elem.GetProperty("scheduledDate").GetString()),
                            compDate
                        );
                    }

                    dgvMaint.DataSource = dtMaintData;
                    if (dgvMaint.Columns["vehicleId"] != null) dgvMaint.Columns["vehicleId"].Visible = false;
                }
            }
            catch { }
        }

        private async Task RefreshFuel()
        {
            try
            {
                var res = await ApiService.GetAsync("fuel");
                if (res.Success)
                {
                    using var doc = JsonDocument.Parse(res.Body);
                    dtFuelData = new DataTable();
                    dtFuelData.Columns.Add("fuelLogId", typeof(int));
                    dtFuelData.Columns.Add("vehicleName", typeof(string));
                    dtFuelData.Columns.Add("plateNo", typeof(string));
                    dtFuelData.Columns.Add("fuelQtyLiters", typeof(decimal));
                    dtFuelData.Columns.Add("cost", typeof(decimal));
                    dtFuelData.Columns.Add("currentOdometer", typeof(decimal));
                    dtFuelData.Columns.Add("loggedDate", typeof(DateTime));

                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        dtFuelData.Rows.Add(
                            elem.GetProperty("fuelLogId").GetInt32(),
                            elem.GetProperty("vehicleName").GetString(),
                            elem.GetProperty("plateNo").GetString(),
                            elem.GetProperty("fuelQtyLiters").GetDecimal(),
                            elem.GetProperty("cost").GetDecimal(),
                            elem.GetProperty("currentOdometer").GetDecimal(),
                            Convert.ToDateTime(elem.GetProperty("loggedDate").GetString())
                        );
                    }

                    dgvFuel.DataSource = dtFuelData;
                }
            }
            catch { }
        }

        private async Task SaveMaintenance()
        {
            if (cboMaintVehicle.SelectedIndex < 0 || string.IsNullOrWhiteSpace(txtMaintDesc.Text)) return;
            decimal.TryParse(txtMaintCost.Text, out decimal cost);

            var payload = new
            {
                vehicleId = (int)cboMaintVehicle.SelectedValue,
                description = txtMaintDesc.Text.Trim(),
                cost = cost,
                status = cboMaintStatus.SelectedItem?.ToString() ?? "scheduled",
                scheduledDate = dtMaintScheduled.Value.Date
            };

            var res = await ApiService.PostAsync("maintenance", payload);
            if (res.Success)
            {
                MessageBox.Show("Maintenance task saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearMaintForm();
                await RefreshMaintenance();
            }
        }

        private async Task CompleteMaintenance()
        {
            if (selectedMaintId <= 0) return;
            decimal.TryParse(txtMaintCost.Text, out decimal cost);

            var payload = new { status = "completed", cost = cost, completedDate = DateTime.UtcNow };
            var res = await ApiService.PutAsync($"maintenance/{selectedMaintId}", payload);
            if (res.Success)
            {
                ClearMaintForm();
                await RefreshMaintenance();
            }
        }

        private async Task DeleteMaintenance()
        {
            if (selectedMaintId <= 0) return;
            if (MessageBox.Show("Delete this maintenance record?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            var res = await ApiService.DeleteAsync($"maintenance/{selectedMaintId}");
            if (res.Success)
            {
                ClearMaintForm();
                await RefreshMaintenance();
            }
        }

        private async Task SaveFuel()
        {
            if (cboFuelVehicle.SelectedIndex < 0) return;
            decimal.TryParse(txtFuelQty.Text, out decimal qty);
            decimal.TryParse(txtFuelCost.Text, out decimal cost);
            decimal.TryParse(txtFuelOdo.Text, out decimal odo);

            var payload = new { vehicleId = (int)cboFuelVehicle.SelectedValue, fuelQtyLiters = qty, cost = cost, currentOdometer = odo };
            var res = await ApiService.PostAsync("fuel", payload);
            if (res.Success)
            {
                txtFuelQty.Text = "0.00";
                txtFuelCost.Text = "0.00";
                txtFuelOdo.Text = "0.00";
                await RefreshFuel();
            }
        }

        private async Task DeleteFuel()
        {
            if (selectedFuelId <= 0) return;
            if (MessageBox.Show("Delete this fuel log?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            var res = await ApiService.DeleteAsync($"fuel/{selectedFuelId}");
            if (res.Success) await RefreshFuel();
        }

        private void ClearMaintForm()
        {
            selectedMaintId = -1;
            txtMaintDesc.Clear();
            txtMaintCost.Text = "0.00";
            cboMaintStatus.SelectedIndex = 0;
            dtMaintScheduled.Value = DateTime.Now;
            btnCompleteMaint.Enabled = false;
            btnDeleteMaint.Enabled = false;
        }

        // ── Original OCR & dialog methods ──
        private async void btnScanReceipt_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog();
            ofd.Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif";
            if (ofd.ShowDialog() != DialogResult.OK) return;

            int vehicleId = 1;
            string selVehicle = _cmbVehicle.SelectedItem?.ToString() ?? "";
            if (selVehicle.StartsWith("Vehicle #")) int.TryParse(selVehicle.Replace("Vehicle #", ""), out vehicleId);

            try
            {
                using var client = new HttpClient();
                string baseUrl = ApiService.BaseUrl;
                if (baseUrl.EndsWith("/api")) baseUrl = baseUrl.Substring(0, baseUrl.Length - 4);
                if (!baseUrl.EndsWith("/")) baseUrl += "/";
                client.BaseAddress = new Uri(baseUrl);
                
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(vehicleId.ToString()), "vehicleId");
                
                var fileBytes = File.ReadAllBytes(ofd.FileName);
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                form.Add(fileContent, "receiptImage", Path.GetFileName(ofd.FileName));

                var response = await client.PostAsync("api/expenses/ocr", form);
                if (!response.IsSuccessStatusCode) return;

                var body = await response.Content.ReadAsStringAsync();
                var result = JsonDocument.Parse(body).RootElement;
                var amount = result.GetProperty("amount").GetDecimal();
                var category = result.GetProperty("category").GetString();
                var detected = result.GetProperty("detectedText").GetString();

                MessageBox.Show($"✅ OCR Complete!\n\nDetected Text: {detected}\nAmount parsed: ₱{amount:N2}\nCategory classified: {category}",
                    "AI Receipt Scanner", MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadExpensesAsync();
            }
            catch { }
        }

        private void ShowAddExpenseDialog()
        {
            var dialog = new Form
            {
                Text = "Log New Vehicle Expense",
                Size = new Size(380, 320),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = ThemeManager.CurrentBackground,
                ForeColor = ThemeManager.CurrentText
            };

            var lblVid = new Label { Text = "Vehicle ID:", Location = new Point(20, 20), AutoSize = true };
            var txtVid = new TextBox { Location = new Point(140, 20), Width = 200, BackColor = ThemeManager.CurrentCard, ForeColor = ThemeManager.CurrentText };
            var lblAmt = new Label { Text = "Amount (₱):", Location = new Point(20, 60), AutoSize = true };
            var txtAmt = new TextBox { Location = new Point(140, 60), Width = 200, BackColor = ThemeManager.CurrentCard, ForeColor = ThemeManager.CurrentText };
            var lblCat = new Label { Text = "Category:", Location = new Point(20, 100), AutoSize = true };
            var cmbCat = new ComboBox { Location = new Point(140, 100), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = ThemeManager.CurrentCard, ForeColor = ThemeManager.CurrentText };
            cmbCat.Items.AddRange(new object[] { "fuel", "toll", "maintenance", "parking", "other" });
            cmbCat.SelectedIndex = 0;

            var lblUrl = new Label { Text = "Receipt URL:", Location = new Point(20, 140), AutoSize = true };
            var txtUrl = new TextBox { Location = new Point(140, 140), Width = 200, BackColor = ThemeManager.CurrentCard, ForeColor = ThemeManager.CurrentText };

            var btnSave = new Button { Text = "Save Expense", Location = new Point(60, 210), Size = new Size(110, 36), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(59, 130, 246), ForeColor = Color.White };
            var btnCancel = new Button { Text = "Cancel", Location = new Point(190, 210), Size = new Size(110, 36), FlatStyle = FlatStyle.Flat, BackColor = ThemeManager.CurrentCard, ForeColor = ThemeManager.CurrentText };

            btnCancel.Click += (s, e) => dialog.Close();
            btnSave.Click += async (s, e) =>
            {
                if (!decimal.TryParse(txtAmt.Text, out decimal amt)) return;
                int? vid = null;
                if (!string.IsNullOrEmpty(txtVid.Text) && int.TryParse(txtVid.Text, out int v)) vid = v;

                var payload = new
                {
                    vehicleId = vid,
                    amount = amt,
                    category = cmbCat.SelectedItem.ToString(),
                    receiptUrl = string.IsNullOrWhiteSpace(txtUrl.Text) ? null : txtUrl.Text
                };

                var result = await ApiService.PostAsync("expenses", payload);
                if (result.Success)
                {
                    dialog.Close();
                    await LoadExpensesAsync();
                }
            };

            dialog.Controls.AddRange(new Control[] { lblVid, txtVid, lblAmt, txtAmt, lblCat, cmbCat, lblUrl, txtUrl, btnSave, btnCancel });
            dialog.ShowDialog();
        }

        private Label CreateStatCard(string title, string initialValue, int x)
        {
            var card = new Label { Size = new Size(170, 70), Location = new Point(x, 5), BorderStyle = BorderStyle.None, BackColor = Color.Transparent, Padding = new Padding(12, 10, 12, 10) };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using (var path = RoundRect(rect, 10))
                {
                    e.Graphics.FillPath(new SolidBrush(ThemeManager.CurrentCard), path);
                    e.Graphics.DrawPath(new Pen(ThemeManager.CurrentBorder, 1), path);
                }
                using (var titleFont = new Font("Segoe UI", 9F, FontStyle.Bold))
                using (var valFont = new Font("Segoe UI", 12F, FontStyle.Bold))
                using (var brushTitle = new SolidBrush(ThemeManager.CurrentSubText))
                using (var brushVal = new SolidBrush(ThemeManager.CurrentText))
                {
                    e.Graphics.DrawString(title, titleFont, brushTitle, 10, 8);
                    e.Graphics.DrawString(card.Tag?.ToString() ?? initialValue, valFont, brushVal, 10, 30);
                }
            };
            card.Tag = initialValue;
            return card;
        }

        private void UpdateStatCard(Label card, string value)
        {
            card.Tag = value;
            card.Invalidate();
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
            dgv.ReadOnly = true;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.Font = new Font("Segoe UI", 9.5F);
            dgv.RowTemplate.Height = 40;
            dgv.EnableHeadersVisualStyles = false;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            dgv.DefaultCellStyle.BackColor = dk ? ColBg : WinColor.White;
            dgv.DefaultCellStyle.ForeColor = ColText;
            dgv.DefaultCellStyle.SelectionBackColor = dk ? WinColor.FromArgb(32, 255, 90, 31) : WinColor.FromArgb(255, 240, 230);
            dgv.DefaultCellStyle.SelectionForeColor = ColAccent;
            dgv.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);

            dgv.ColumnHeadersDefaultCellStyle.BackColor = dk ? WinColor.FromArgb(8, 8, 16) : WinColor.FromArgb(235, 236, 245);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = ColSub;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgv.ColumnHeadersHeight = 36;

            dgv.AlternatingRowsDefaultCellStyle.BackColor = dk ? WinColor.FromArgb(20, 20, 34) : WinColor.FromArgb(250, 250, 255);
        }

        private Button CreateBtn(string text, WinColor color, int x, int y, int w)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(w, 36),
                Location = new Point(x, y),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BackColor = WinColor.FromArgb(15, color),
                ForeColor = color
            };
            btn.FlatAppearance.BorderColor = color;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = WinColor.FromArgb(35, color);

            btn.HandleCreated += (s, e) =>
            {
                if (btn.IsDisposed) return;
                using var p = RoundRect(new Rectangle(0, 0, btn.Width, btn.Height), 6);
                btn.Region = new Region(p);
            };

            return btn;
        }

        private GraphicsPath RoundRect(Rectangle b, int r)
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
        
        private class ExpenseItem
        {
            public int ExpenseId { get; set; }
            public int? VehicleId { get; set; }
            public string Category { get; set; }
            public decimal Amount { get; set; }
            public string CreatedAt { get; set; }
        }
    }
}
