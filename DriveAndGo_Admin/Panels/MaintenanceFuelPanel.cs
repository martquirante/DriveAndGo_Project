using DriveAndGo_Admin.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinColor = System.Drawing.Color;

namespace DriveAndGo_Admin.Panels
{
    public class MaintenanceFuelPanel : UserControl
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

        // ── UI Controls ──
        private TabControl tabControl;
        private TabPage tabMaintenance;
        private TabPage tabFuel;

        // Maintenance Tab Controls
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

        // Fuel Tab Controls
        private SplitContainer splitFuel;
        private DataGridView dgvFuel;
        private ComboBox cboFuelVehicle;
        private TextBox txtFuelQty;
        private TextBox txtFuelCost;
        private TextBox txtFuelOdo;
        private Button btnSaveFuel;
        private Button btnDeleteFuel;

        // Data State
        private DataTable dtMaintData = new DataTable();
        private DataTable dtFuelData = new DataTable();
        private List<KeyValuePair<int, string>> vehicleList = new List<KeyValuePair<int, string>>();
        private int selectedMaintId = -1;
        private int selectedFuelId = -1;

        public MaintenanceFuelPanel()
        {
            this.Dock = DockStyle.Fill;
            this.DoubleBuffered = true;
            this.BackColor = ColBg;
            ThemeManager.ThemeChanged += (s, e) => ApplyTheme();

            BuildUI();
            this.Load += async (s, e) =>
            {
                await LoadVehicles();
                await RefreshMaintenance();
                await RefreshFuel();
            };
        }

        private void BuildUI()
        {
            // Title & Header Area
            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = WinColor.Transparent };
            var lblTitle = new Label
            {
                Text = "🔧  Operations & Maintenance",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = ColText,
                Location = new Point(16, 12),
                AutoSize = true
            };
            var lblSub = new Label
            {
                Text = "Manage vehicle servicing tasks, maintenance schedules, and fuel consumption logs",
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = ColSub,
                Location = new Point(18, 48),
                AutoSize = true
            };
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSub);
            this.Controls.Add(pnlHeader);

            // Tab Control
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F),
                Padding = new Point(12, 4)
            };
            tabMaintenance = new TabPage { Text = "🔧  Service & Maintenance" };
            tabFuel = new TabPage { Text = "⛽  Fuel Logging" };

            tabControl.TabPages.Add(tabMaintenance);
            tabControl.TabPages.Add(tabFuel);
            this.Controls.Add(tabControl);

            BuildMaintenanceTab();
            BuildFuelTab();
            ApplyTheme();
        }

        // ══════════════════════════════════════════════
        //  MAINTENANCE TAB
        // ══════════════════════════════════════════════
        private void BuildMaintenanceTab()
        {
            splitMaint = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 600,
                SplitterWidth = 5
            };
            tabMaintenance.Controls.Add(splitMaint);

            // Left Panel (Grid)
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

                    // Set vehicle combo selection
                    int vId = Convert.ToInt32(row.Cells["vehicleId"].Value);
                    for (int i = 0; i < cboMaintVehicle.Items.Count; i++)
                    {
                        var item = (KeyValuePair<int, string>)cboMaintVehicle.Items[i];
                        if (item.Key == vId)
                        {
                            cboMaintVehicle.SelectedIndex = i;
                            break;
                        }
                    }
                    btnCompleteMaint.Enabled = row.Cells["status"].Value?.ToString().ToLower() != "completed";
                    btnDeleteMaint.Enabled = true;
                }
                else
                {
                    ClearMaintForm();
                }
            };
            splitMaint.Panel1.Controls.Add(dgvMaint);

            // Right Panel (Form)
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
            btnCompleteMaint.Enabled = false;
            y += 46;

            btnDeleteMaint = CreateBtn("Delete Log", ColRed, 16, y, 260);
            btnDeleteMaint.Click += async (s, e) => await DeleteMaintenance();
            btnDeleteMaint.Enabled = false;

            pnlForm.Controls.AddRange(new Control[] {
                lblFormTitle, lblV, cboMaintVehicle, lblD, txtMaintDesc, lblC, txtMaintCost,
                lblS, cboMaintStatus, lblDt, dtMaintScheduled, btnSaveMaint, btnCompleteMaint, btnDeleteMaint
            });
            splitMaint.Panel2.Controls.Add(pnlForm);
        }

        // ══════════════════════════════════════════════
        //  FUEL TAB
        // ══════════════════════════════════════════════
        private void BuildFuelTab()
        {
            splitFuel = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 600,
                SplitterWidth = 5
            };
            tabFuel.Controls.Add(splitFuel);

            // Left Grid
            dgvFuel = new DataGridView { Dock = DockStyle.Fill };
            StyleGrid(dgvFuel);
            dgvFuel.SelectionChanged += (s, e) =>
            {
                if (dgvFuel.SelectedRows.Count > 0)
                {
                    selectedFuelId = Convert.ToInt32(dgvFuel.SelectedRows[0].Cells["fuelLogId"].Value);
                    btnDeleteFuel.Enabled = true;
                }
                else
                {
                    selectedFuelId = -1;
                    btnDeleteFuel.Enabled = false;
                }
            };
            splitFuel.Panel1.Controls.Add(dgvFuel);

            // Right Form
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
            btnDeleteFuel.Enabled = false;

            pnlForm.Controls.AddRange(new Control[] {
                lblFormTitle, lblV, cboFuelVehicle, lblQty, txtFuelQty, lblC, txtFuelCost, lblOdo, txtFuelOdo, btnSaveFuel, btnDeleteFuel
            });
            splitFuel.Panel2.Controls.Add(pnlForm);
        }

        // ══════════════════════════════════════════════
        //  THEME APPLICATION
        // ══════════════════════════════════════════════
        private void ApplyTheme()
        {
            this.BackColor = ColBg;
            tabMaintenance.BackColor = ColBg;
            tabFuel.BackColor = ColBg;

            foreach (TabPage tab in tabControl.TabPages)
            {
                tab.BackColor = ColBg;
            }

            splitMaint.Panel2.BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.FromArgb(245, 245, 250);
            splitFuel.Panel2.BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.FromArgb(245, 245, 250);

            StyleGrid(dgvMaint);
            StyleGrid(dgvFuel);
        }

        // ══════════════════════════════════════════════
        //  DATA OPERATIONS
        // ══════════════════════════════════════════════
        private async Task LoadVehicles()
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
            if (cboMaintVehicle.SelectedIndex < 0 || string.IsNullOrWhiteSpace(txtMaintDesc.Text))
            {
                MessageBox.Show("Please select a vehicle and enter description.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

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
            else
            {
                MessageBox.Show("Save failed: " + res.Body, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task CompleteMaintenance()
        {
            if (selectedMaintId <= 0) return;
            decimal.TryParse(txtMaintCost.Text, out decimal cost);

            var payload = new
            {
                status = "completed",
                cost = cost,
                completedDate = DateTime.UtcNow
            };

            var res = await ApiService.PutAsync($"maintenance/{selectedMaintId}", payload);
            if (res.Success)
            {
                MessageBox.Show("Service marked as completed.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearMaintForm();
                await RefreshMaintenance();
            }
            else
            {
                MessageBox.Show("Update failed: " + res.Body, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            var payload = new
            {
                vehicleId = (int)cboFuelVehicle.SelectedValue,
                fuelQtyLiters = qty,
                cost = cost,
                currentOdometer = odo
            };

            var res = await ApiService.PostAsync("fuel", payload);
            if (res.Success)
            {
                MessageBox.Show("Fuel log saved.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            if (res.Success)
            {
                await RefreshFuel();
            }
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

        // ══════════════════════════════════════════════
        //  GRID & BUTTON UTILS
        // ══════════════════════════════════════════════
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
    }
}
