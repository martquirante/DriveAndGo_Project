#nullable disable
using DriveAndGo_Admin.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DriveAndGo_Admin.Panels
{
    public class ExpensesPanel : UserControl
    {
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

        public ExpensesPanel()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.Transparent;
            BuildUI();
            ThemeManager.ThemeChanged += (s, e) => ApplyTheme();
            ApplyTheme();
            _ = LoadExpensesAsync();
        }

        private void BuildUI()
        {
            // ── Header Panel ────────────────────────────────────────────────
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.Transparent
            };

            _titleLabel = new Label
            {
                Text = "💰  Vehicle Expenses",
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(0, 8),
                BackColor = Color.Transparent
            };

            _subtitleLabel = new Label
            {
                Text = "Track fuel, toll fees, maintenance costs, and scan receipts using AI OCR",
                Font = new Font("Segoe UI", 10F),
                AutoSize = true,
                Location = new Point(2, 42),
                BackColor = Color.Transparent
            };

            _btnAddExpense = new Button
            {
                Text = "➕  Log Expense",
                Size = new Size(140, 36),
                Location = new Point(500, 20),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(59, 130, 246),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnAddExpense.FlatAppearance.BorderSize = 0;
            _btnAddExpense.Click += (s, e) => ShowAddExpenseDialog();

            _btnScanReceipt = new Button
            {
                Text = "📷  Scan Receipt (OCR)",
                Size = new Size(170, 36),
                Location = new Point(650, 20),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnScanReceipt.FlatAppearance.BorderSize = 0;
            _btnScanReceipt.Click += btnScanReceipt_Click;

            _headerPanel.Controls.Add(_titleLabel);
            _headerPanel.Controls.Add(_subtitleLabel);
            _headerPanel.Controls.Add(_btnAddExpense);
            _headerPanel.Controls.Add(_btnScanReceipt);

            // ── Stats Panel ─────────────────────────────────────────────────
            _statsPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                Padding = new Padding(0, 5, 0, 5),
                BackColor = Color.Transparent
            };

            _lblTotalSpent = CreateStatCard("Total Spent", "₱0.00", 0);
            _lblFuelSpent = CreateStatCard("Fuel", "₱0.00", 185);
            _lblTollSpent = CreateStatCard("Tolls", "₱0.00", 370);
            _lblMaintSpent = CreateStatCard("Maintenance", "₱0.00", 555);

            _statsPanel.Controls.Add(_lblTotalSpent);
            _statsPanel.Controls.Add(_lblFuelSpent);
            _statsPanel.Controls.Add(_lblTollSpent);
            _statsPanel.Controls.Add(_lblMaintSpent);

            // ── Filter Panel ────────────────────────────────────────────────
            _filterPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 50,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 10, 0, 10),
                BackColor = Color.Transparent
            };

            _lblFilterVehicle = new Label { Text = "Vehicle:", Font = new Font("Segoe UI", 9F), AutoSize = true, Margin = new Padding(0, 6, 5, 0) };
            _cmbVehicle = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbVehicle.Items.Add("All Vehicles");
            _cmbVehicle.SelectedIndex = 0;
            _cmbVehicle.SelectedIndexChanged += (s, e) => FilterExpenses();

            _lblFilterCategory = new Label { Text = "Category:", Font = new Font("Segoe UI", 9F), AutoSize = true, Margin = new Padding(20, 6, 5, 0) };
            _cmbCategory = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbCategory.Items.AddRange(new object[] { "All", "fuel", "toll", "maintenance", "parking", "other" });
            _cmbCategory.SelectedIndex = 0;
            _cmbCategory.SelectedIndexChanged += (s, e) => FilterExpenses();

            _btnRefresh = new Button
            {
                Text = "🔄 Refresh",
                Size = new Size(90, 26),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(20, 0, 0, 0)
            };
            _btnRefresh.Click += async (s, e) => await LoadExpensesAsync();

            _filterPanel.Controls.Add(_lblFilterVehicle);
            _filterPanel.Controls.Add(_cmbVehicle);
            _filterPanel.Controls.Add(_lblFilterCategory);
            _filterPanel.Controls.Add(_cmbCategory);
            _filterPanel.Controls.Add(_btnRefresh);

            // ── Grid Panel ──────────────────────────────────────────────────
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(30, 41, 59),
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(51, 65, 85)
            };

            // Custom columns
            _grid.Columns.Add("expenseId", "ID");
            _grid.Columns.Add("vehicleId", "Vehicle ID");
            _grid.Columns.Add("category", "Category");
            _grid.Columns.Add("amount", "Amount");
            _grid.Columns.Add("createdAt", "Date Logged");

            _grid.Columns["expenseId"].Width = 60;
            _grid.Columns["amount"].DefaultCellStyle.Format = "₱#,##0.00";

            this.Controls.Add(_grid);
            this.Controls.Add(_filterPanel);
            this.Controls.Add(_statsPanel);
            this.Controls.Add(_headerPanel);
            
            // Adjust layout on resize
            this.Resize += (s, e) =>
            {
                if (_btnAddExpense != null)
                {
                    _btnAddExpense.Left = this.Width - 360;
                    _btnScanReceipt.Left = this.Width - 200;
                }
            };
        }

        private Label CreateStatCard(string title, string initialValue, int x)
        {
            var card = new Label
            {
                Size = new Size(170, 70),
                Location = new Point(x, 5),
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(12, 10, 12, 10)
            };

            // Use Paint event to draw nice card borders and round edges
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using (var pen = new Pen(ThemeManager.CurrentBorder, 1))
                {
                    e.Graphics.DrawRectangle(pen, rect);
                }
                
                // Draw text elements inside the card
                using (var titleFont = new Font("Segoe UI", 9F, FontStyle.Bold))
                using (var valFont = new Font("Segoe UI", 13F, FontStyle.Bold))
                using (var brushTitle = new SolidBrush(ThemeManager.CurrentSubText))
                using (var brushVal = new SolidBrush(ThemeManager.CurrentText))
                {
                    e.Graphics.DrawString(title, titleFont, brushTitle, 10, 8);
                    e.Graphics.DrawString(card.Tag?.ToString() ?? initialValue, valFont, brushVal, 10, 30);
                }
            };

            card.Tag = initialValue;
            SetRound(card, 10);
            return card;
        }

        private void UpdateStatCard(Label card, string value)
        {
            card.Tag = value;
            card.Invalidate();
        }

        private void ApplyTheme()
        {
            _titleLabel.ForeColor = ThemeManager.CurrentText;
            _subtitleLabel.ForeColor = ThemeManager.CurrentSubText;
            
            _lblFilterVehicle.ForeColor = ThemeManager.CurrentText;
            _lblFilterCategory.ForeColor = ThemeManager.CurrentText;
            
            _cmbVehicle.BackColor = ThemeManager.CurrentCard;
            _cmbVehicle.ForeColor = ThemeManager.CurrentText;
            _cmbCategory.BackColor = ThemeManager.CurrentCard;
            _cmbCategory.ForeColor = ThemeManager.CurrentText;
            
            _btnRefresh.BackColor = ThemeManager.CurrentCard;
            _btnRefresh.ForeColor = ThemeManager.CurrentText;
            _btnRefresh.FlatAppearance.BorderColor = ThemeManager.CurrentBorder;

            _grid.BackgroundColor = ThemeManager.CurrentBackground;
            _grid.GridColor = ThemeManager.CurrentBorder;
            _grid.DefaultCellStyle.BackColor = ThemeManager.CurrentCard;
            _grid.DefaultCellStyle.ForeColor = ThemeManager.CurrentText;
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(59, 130, 246);
            _grid.DefaultCellStyle.SelectionForeColor = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = ThemeManager.CurrentBackground;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = ThemeManager.CurrentText;

            _lblTotalSpent.BackColor = ThemeManager.CurrentCard;
            _lblFuelSpent.BackColor = ThemeManager.CurrentCard;
            _lblTollSpent.BackColor = ThemeManager.CurrentCard;
            _lblMaintSpent.BackColor = ThemeManager.CurrentCard;

            _lblTotalSpent.Invalidate();
            _lblFuelSpent.Invalidate();
            _lblTollSpent.Invalidate();
            _lblMaintSpent.Invalidate();
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
                    
                    var vehicleList = new HashSet<string>();

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

                        if (exp.VehicleId.HasValue)
                        {
                            vehicleList.Add(exp.VehicleId.Value.ToString());
                        }
                    }

                    // Populate vehicles combo
                    _cmbVehicle.Items.Clear();
                    _cmbVehicle.Items.Add("All Vehicles");
                    foreach (var v in vehicleList)
                    {
                        _cmbVehicle.Items.Add($"Vehicle #{v}");
                    }
                    _cmbVehicle.SelectedIndex = 0;

                    FilterExpenses();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load expenses: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void FilterExpenses()
        {
            _grid.Rows.Clear();
            decimal total = 0, fuel = 0, toll = 0, maint = 0;

            string selVehicle = _cmbVehicle.SelectedItem?.ToString() ?? "All Vehicles";
            string selCat = _cmbCategory.SelectedItem?.ToString() ?? "All";

            foreach (var exp in _expenses)
            {
                // Check vehicle filter
                if (selVehicle != "All Vehicles")
                {
                    string vidStr = exp.VehicleId.HasValue ? $"Vehicle #{exp.VehicleId.Value}" : "";
                    if (vidStr != selVehicle) continue;
                }

                // Check category filter
                if (selCat != "All" && exp.Category.ToLower() != selCat.ToLower()) continue;

                // Add to Grid
                _grid.Rows.Add(exp.ExpenseId, exp.VehicleId.HasValue ? exp.VehicleId.Value.ToString() : "N/A", exp.Category, exp.Amount, exp.CreatedAt);

                // Update totals
                total += exp.Amount;
                if (exp.Category.ToLower() == "fuel") fuel += exp.Amount;
                else if (exp.Category.ToLower() == "toll") toll += exp.Amount;
                else if (exp.Category.ToLower() == "maintenance") maint += exp.Amount;
            }

            // Update stats cards text
            UpdateStatCard(_lblTotalSpent, $"₱{total:N2}");
            UpdateStatCard(_lblFuelSpent, $"₱{fuel:N2}");
            UpdateStatCard(_lblTollSpent, $"₱{toll:N2}");
            UpdateStatCard(_lblMaintSpent, $"₱{maint:N2}");
        }

        private async void btnScanReceipt_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog();
            ofd.Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif";
            ofd.Title = "Select Receipt Image for OCR Processing";
            if (ofd.ShowDialog() != DialogResult.OK) return;

            int vehicleId = 1; // Default fallback

            // Try to extract from ComboBox if a specific vehicle is selected
            string selVehicle = _cmbVehicle.SelectedItem?.ToString() ?? "";
            if (selVehicle.StartsWith("Vehicle #"))
            {
                int.TryParse(selVehicle.Replace("Vehicle #", ""), out vehicleId);
            }

            try
            {
                using var client = new HttpClient();
                // Determine API BaseUrl
                string baseUrl = ApiService.BaseUrl;
                if (baseUrl.EndsWith("/api")) baseUrl = baseUrl.Substring(0, baseUrl.Length - 4); // Strip trailing api if it's the root URL
                if (!baseUrl.EndsWith("/")) baseUrl += "/";

                client.BaseAddress = new Uri(baseUrl);
                
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(vehicleId.ToString()), "vehicleId");
                
                var fileBytes = File.ReadAllBytes(ofd.FileName);
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                form.Add(fileContent, "receiptImage", Path.GetFileName(ofd.FileName));

                var response = await client.PostAsync("api/expenses/ocr", form);
                if (!response.IsSuccessStatusCode)
                {
                    var errBody = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"OCR failed with status {response.StatusCode}: {errBody}", "OCR Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var body = await response.Content.ReadAsStringAsync();
                var result = JsonDocument.Parse(body).RootElement;
                var amount = result.GetProperty("amount").GetDecimal();
                var category = result.GetProperty("category").GetString();
                var detected = result.GetProperty("detectedText").GetString();

                MessageBox.Show($"✅ OCR Complete!\n\nDetected Text: {detected}\nAmount parsed: ₱{amount:N2}\nCategory classified: {category}",
                    "AI Receipt Scanner Results", MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadExpensesAsync(); // Refresh grid and totals
            }
            catch (Exception ex)
            {
                MessageBox.Show($"OCR Request failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

            var btnSave = new Button
            {
                Text = "Save Expense",
                Location = new Point(60, 210),
                Size = new Size(110, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(59, 130, 246),
                ForeColor = Color.White
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(190, 210),
                Size = new Size(110, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeManager.CurrentCard,
                ForeColor = ThemeManager.CurrentText
            };

            btnCancel.Click += (s, e) => dialog.Close();
            btnSave.Click += async (s, e) =>
            {
                if (!decimal.TryParse(txtAmt.Text, out decimal amt))
                {
                    MessageBox.Show("Please enter a valid amount.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                int? vid = null;
                if (!string.IsNullOrEmpty(txtVid.Text))
                {
                    if (int.TryParse(txtVid.Text, out int v)) vid = v;
                    else
                    {
                        MessageBox.Show("Vehicle ID must be a number.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

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
                    MessageBox.Show("Expense logged successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    dialog.Close();
                    await LoadExpensesAsync();
                }
                else
                {
                    MessageBox.Show($"Failed to save: {result.Error}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            dialog.Controls.Add(lblVid); dialog.Controls.Add(txtVid);
            dialog.Controls.Add(lblAmt); dialog.Controls.Add(txtAmt);
            dialog.Controls.Add(lblCat); dialog.Controls.Add(cmbCat);
            dialog.Controls.Add(lblUrl); dialog.Controls.Add(txtUrl);
            dialog.Controls.Add(btnSave); dialog.Controls.Add(btnCancel);

            dialog.ShowDialog();
        }

        private static void SetRound(Control ctrl, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(0, 0, d, d, 180, 90);
            path.AddArc(ctrl.Width - d, 0, d, d, 270, 90);
            path.AddArc(ctrl.Width - d, ctrl.Height - d, d, d, 0, 90);
            path.AddArc(0, ctrl.Height - d, d, d, 90, 90);
            path.CloseFigure();
            ctrl.Region = new Region(path);
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
