#nullable disable
using DriveAndGo_Admin.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DriveAndGo_Admin.Panels
{
    public class SplitPaymentsPanel : UserControl
    {
        private Panel _headerPanel;
        private Label _titleLabel;
        private Label _subtitleLabel;

        private Panel _topBar;
        private Label _lblRentalId;
        private TextBox _txtRentalId;
        private Button _btnLoadRental;

        private Panel _rentalInfoPanel;
        private Label _lblCustomerName;
        private Label _lblTotalAmount;
        private Label _lblRentalStatus;

        private Panel _splitWorkspace;
        private Panel _leftPanel;
        private Panel _rightPanel;

        // Left Table
        private Label _lblSplitStatus;
        private DataGridView _gridSplits;

        // Right Add Member Form
        private Label _lblAddShare;
        private Label _lblEmail;
        private TextBox _txtEmail;
        private Label _lblAmount;
        private TextBox _txtAmount;
        private Button _btnAddShare;

        // Bottom Actions
        private Panel _actionPanel;
        private Label _lblSumSplit;
        private Button _btnSendSplit;

        private int _currentRentalId = -1;
        private decimal _rentalTotal = 0;
        private List<SplitShareItem> _localShares = new();

        public SplitPaymentsPanel()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.Transparent;
            BuildUI();
            ThemeManager.ThemeChanged += (s, e) => ApplyTheme();
            ApplyTheme();
        }

        private void BuildUI()
        {
            // ── Header Panel ────────────────────────────────────────────────
            _headerPanel = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = Color.Transparent };
            _titleLabel = new Label { Text = "🤝  Barkada Mode — Split Payments", Font = new Font("Segoe UI", 20F, FontStyle.Bold), AutoSize = true, Location = new Point(0, 8) };
            _subtitleLabel = new Label { Text = "Divide rental costs among multiple co-renters and track payment confirmation", Font = new Font("Segoe UI", 10F), AutoSize = true, Location = new Point(2, 42) };
            _headerPanel.Controls.Add(_titleLabel);
            _headerPanel.Controls.Add(_subtitleLabel);

            // ── Top Bar (Load Rental ID) ────────────────────────────────────
            _topBar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.Transparent };
            _lblRentalId = new Label { Text = "Enter Rental ID:", Font = new Font("Segoe UI", 10F, FontStyle.Bold), Location = new Point(0, 15), AutoSize = true };
            _txtRentalId = new TextBox { Location = new Point(120, 12), Width = 120, Font = new Font("Segoe UI", 10F) };
            _btnLoadRental = new Button { Text = "🔍 Load Rental", Location = new Point(260, 10), Size = new Size(130, 30), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand };
            _btnLoadRental.Click += async (s, e) => await LoadRentalAsync();

            _topBar.Controls.Add(_lblRentalId);
            _topBar.Controls.Add(_txtRentalId);
            _topBar.Controls.Add(_btnLoadRental);

            // ── Rental Info Card ───────────────────────────────────────────
            _rentalInfoPanel = new Panel { Dock = DockStyle.Top, Height = 75, BackColor = Color.Transparent };
            _lblCustomerName = new Label { Text = "Customer: ---", Font = new Font("Segoe UI", 11F, FontStyle.Bold), Location = new Point(15, 12), AutoSize = true };
            _lblTotalAmount = new Label { Text = "Total Amount: ₱0.00", Font = new Font("Segoe UI", 11F, FontStyle.Bold), Location = new Point(15, 40), AutoSize = true };
            _lblRentalStatus = new Label { Text = "Status: ---", Font = new Font("Segoe UI", 11F, FontStyle.Bold), Location = new Point(400, 12), AutoSize = true };
            
            _rentalInfoPanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 5, _rentalInfoPanel.Width - 1, _rentalInfoPanel.Height - 10);
                using (var pen = new Pen(ThemeManager.CurrentBorder, 1))
                {
                    e.Graphics.DrawRectangle(pen, rect);
                }
            };
            _rentalInfoPanel.Controls.Add(_lblCustomerName);
            _rentalInfoPanel.Controls.Add(_lblTotalAmount);
            _rentalInfoPanel.Controls.Add(_lblRentalStatus);

            // ── Workspace ──────────────────────────────────────────────────
            _splitWorkspace = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 15, 0, 10), BackColor = Color.Transparent };
            
            _leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _rightPanel = new Panel { Dock = DockStyle.Right, Width = 320, BackColor = Color.Transparent, Padding = new Padding(15, 0, 0, 0) };

            _splitWorkspace.Controls.Add(_leftPanel);
            _splitWorkspace.Controls.Add(_rightPanel);

            // ── Left: Split List ───────────────────────────────────────────
            _lblSplitStatus = new Label { Text = "Current Split Shares & Payment Status", Font = new Font("Segoe UI", 12F, FontStyle.Bold), Dock = DockStyle.Top, Height = 30 };
            _gridSplits = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(30, 41, 59),
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(51, 65, 85)
            };
            _gridSplits.Columns.Add("email", "Email Address");
            _gridSplits.Columns.Add("shareAmount", "Share Amount");
            _gridSplits.Columns.Add("paymentStatus", "Payment Status");

            _gridSplits.Columns["shareAmount"].DefaultCellStyle.Format = "₱#,##0.00";
            _gridSplits.RowsRemoved += (s, e) => CalculateSplitSum();

            _leftPanel.Controls.Add(_gridSplits);
            _leftPanel.Controls.Add(_lblSplitStatus);

            // ── Right: Add Share Form ──────────────────────────────────────
            _lblAddShare = new Label { Text = "➕ Add Co-Renter Share", Font = new Font("Segoe UI", 12F, FontStyle.Bold), Location = new Point(15, 0), Height = 30, AutoSize = true };
            _lblEmail = new Label { Text = "Co-Renter Email:", Font = new Font("Segoe UI", 9F), Location = new Point(15, 45), AutoSize = true };
            _txtEmail = new TextBox { Location = new Point(15, 68), Width = 280, Font = new Font("Segoe UI", 10F) };
            _lblAmount = new Label { Text = "Share Amount (₱):", Font = new Font("Segoe UI", 9F), Location = new Point(15, 110), AutoSize = true };
            _txtAmount = new TextBox { Location = new Point(15, 133), Width = 280, Font = new Font("Segoe UI", 10F) };
            
            _btnAddShare = new Button { Text = "Add Share to List", Location = new Point(15, 180), Size = new Size(280, 36), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand, BackColor = Color.FromArgb(59, 130, 246), ForeColor = Color.White };
            _btnAddShare.FlatAppearance.BorderSize = 0;
            _btnAddShare.Click += btnAddShare_Click;

            _rightPanel.Controls.Add(_lblAddShare);
            _rightPanel.Controls.Add(_lblEmail);
            _rightPanel.Controls.Add(_txtEmail);
            _rightPanel.Controls.Add(_lblAmount);
            _rightPanel.Controls.Add(_txtAmount);
            _rightPanel.Controls.Add(_btnAddShare);

            // ── Bottom Action Panel ─────────────────────────────────────────
            _actionPanel = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Color.Transparent };
            _lblSumSplit = new Label { Text = "Total Split Allocated: ₱0.00 / ₱0.00 (0%)", Font = new Font("Segoe UI", 11F, FontStyle.Bold), Location = new Point(5, 20), AutoSize = true };
            _btnSendSplit = new Button { Text = "🚀 Initialize Split Payments", Location = new Point(500, 10), Size = new Size(250, 40), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Cursor = Cursors.Hand, BackColor = Color.FromArgb(16, 185, 129), ForeColor = Color.White, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            _btnSendSplit.FlatAppearance.BorderSize = 0;
            _btnSendSplit.Click += btnSendSplit_Click;

            _actionPanel.Controls.Add(_lblSumSplit);
            _actionPanel.Controls.Add(_btnSendSplit);

            this.Controls.Add(_splitWorkspace);
            this.Controls.Add(_actionPanel);
            this.Controls.Add(_rentalInfoPanel);
            this.Controls.Add(_topBar);
            this.Controls.Add(_headerPanel);

            // Adjust layout on resize
            this.Resize += (s, e) =>
            {
                if (_btnSendSplit != null)
                {
                    _btnSendSplit.Left = this.Width - 270;
                }
            };
        }

        private void ApplyTheme()
        {
            _titleLabel.ForeColor = ThemeManager.CurrentText;
            _subtitleLabel.ForeColor = ThemeManager.CurrentSubText;

            _lblRentalId.ForeColor = ThemeManager.CurrentText;
            _txtRentalId.BackColor = ThemeManager.CurrentCard;
            _txtRentalId.ForeColor = ThemeManager.CurrentText;
            _btnLoadRental.BackColor = ThemeManager.CurrentCard;
            _btnLoadRental.ForeColor = ThemeManager.CurrentText;
            _btnLoadRental.FlatAppearance.BorderColor = ThemeManager.CurrentBorder;

            _lblCustomerName.ForeColor = ThemeManager.CurrentText;
            _lblTotalAmount.ForeColor = ThemeManager.CurrentText;
            _lblRentalStatus.ForeColor = ThemeManager.CurrentText;

            _lblSplitStatus.ForeColor = ThemeManager.CurrentText;
            
            // Modern grid style
            bool dk = ThemeManager.IsDarkMode;
            _gridSplits.BackgroundColor = ThemeManager.CurrentBackground;
            _gridSplits.GridColor = ThemeManager.CurrentBorder;
            _gridSplits.BorderStyle = BorderStyle.None;
            _gridSplits.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            _gridSplits.RowHeadersVisible = false;
            _gridSplits.EnableHeadersVisualStyles = false;
            _gridSplits.RowTemplate.Height = 38;
            _gridSplits.Font = new Font("Segoe UI", 9.5F);

            _gridSplits.DefaultCellStyle.BackColor = dk ? ThemeManager.CurrentCard : Color.White;
            _gridSplits.DefaultCellStyle.ForeColor = ThemeManager.CurrentText;
            _gridSplits.DefaultCellStyle.SelectionBackColor = dk ? Color.FromArgb(32, 255, 90, 31) : Color.FromArgb(255, 240, 230);
            _gridSplits.DefaultCellStyle.SelectionForeColor = Color.FromArgb(255, 90, 31);

            _gridSplits.ColumnHeadersDefaultCellStyle.BackColor = dk ? Color.FromArgb(8, 8, 16) : Color.FromArgb(235, 236, 245);
            _gridSplits.ColumnHeadersDefaultCellStyle.ForeColor = ThemeManager.CurrentSubText;
            _gridSplits.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _gridSplits.ColumnHeadersHeight = 36;

            _gridSplits.AlternatingRowsDefaultCellStyle.BackColor = dk 
                ? Color.FromArgb(20, 20, 34) 
                : Color.FromArgb(250, 250, 255);

            _lblAddShare.ForeColor = ThemeManager.CurrentText;
            _lblEmail.ForeColor = ThemeManager.CurrentText;
            _txtEmail.BackColor = ThemeManager.CurrentCard;
            _txtEmail.ForeColor = ThemeManager.CurrentText;
            _lblAmount.ForeColor = ThemeManager.CurrentText;
            _txtAmount.BackColor = ThemeManager.CurrentCard;
            _txtAmount.ForeColor = ThemeManager.CurrentText;

            _lblSumSplit.ForeColor = ThemeManager.CurrentText;

            // Apply round regions to action buttons
            foreach (var btn in new Button[] { _btnLoadRental, _btnAddShare, _btnSendSplit })
            {
                if (btn != null)
                {
                    if (btn == _btnLoadRental)
                    {
                        btn.BackColor = ThemeManager.CurrentCard;
                        btn.ForeColor = ThemeManager.CurrentText;
                        btn.FlatAppearance.BorderColor = ThemeManager.CurrentBorder;
                    }
                    SetRound(btn, 6);
                }
            }
        }

        private async Task LoadRentalAsync()
        {
            if (!int.TryParse(_txtRentalId.Text.Trim(), out int rid))
            {
                MessageBox.Show("Please enter a numeric Rental ID.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Clear state
                _gridSplits.Rows.Clear();
                _localShares.Clear();

                // 1. Get rentals detail
                var res = await ApiService.GetAsync($"rentals");
                if (!res.Success) return;

                var rentals = JsonDocument.Parse(res.Body).RootElement;
                JsonElement? found = null;
                foreach (var r in rentals.EnumerateArray())
                {
                    if (r.GetProperty("rentalId").GetInt32() == rid)
                    {
                        found = r;
                        break;
                    }
                }

                if (!found.HasValue)
                {
                    MessageBox.Show("Rental not found.", "Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _currentRentalId = rid;
                string customer = found.Value.GetProperty("customerName").GetString();
                _rentalTotal = found.Value.GetProperty("totalAmount").GetDecimal();
                string status = found.Value.GetProperty("status").GetString();

                _lblCustomerName.Text = $"Customer: {customer}";
                _lblTotalAmount.Text = $"Total Amount: ₱{_rentalTotal:N2}";
                _lblRentalStatus.Text = $"Status: {status.ToUpper()}";

                // 2. Load splits from backend
                var splitRes = await ApiService.GetAsync($"rentals/{rid}/split");
                if (splitRes.Success)
                {
                    var splits = JsonDocument.Parse(splitRes.Body).RootElement;
                    foreach (var s in splits.EnumerateArray())
                    {
                        string email = s.GetProperty("email").GetString();
                        decimal amt = s.GetProperty("shareAmount").GetDecimal();
                        string pStatus = s.GetProperty("paymentStatus").GetString();
                        
                        _gridSplits.Rows.Add(email, amt, pStatus);
                    }
                }

                CalculateSplitSum();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load rental details: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnAddShare_Click(object sender, EventArgs e)
        {
            if (_currentRentalId < 0)
            {
                MessageBox.Show("Please load a rental first.", "Action Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string email = _txtEmail.Text.Trim();
            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
            {
                MessageBox.Show("Please enter a valid co-renter email.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!decimal.TryParse(_txtAmount.Text.Trim(), out decimal amt) || amt <= 0)
            {
                MessageBox.Show("Please enter a valid share amount.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Check if email already exists
            foreach (DataGridViewRow row in _gridSplits.Rows)
            {
                if (row.Cells[0].Value?.ToString() == email)
                {
                    MessageBox.Show("This co-renter is already added.", "Duplicate Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            // Add to grid
            _gridSplits.Rows.Add(email, amt, "pending");
            
            _txtEmail.Clear();
            _txtAmount.Clear();

            CalculateSplitSum();
        }

        private void CalculateSplitSum()
        {
            decimal sum = 0;
            foreach (DataGridViewRow row in _gridSplits.Rows)
            {
                if (row.Cells[1].Value != null)
                {
                    sum += Convert.ToDecimal(row.Cells[1].Value);
                }
            }

            double percent = _rentalTotal > 0 ? (double)(sum / _rentalTotal) * 100 : 0;
            _lblSumSplit.Text = $"Total Split Allocated: ₱{sum:N2} / ₱{_rentalTotal:N2} ({percent:N1}%)";
            
            // Adjust label color if it exceeds or matches total
            if (sum > _rentalTotal) _lblSumSplit.ForeColor = Color.FromArgb(239, 68, 68); // Red
            else if (sum == _rentalTotal) _lblSumSplit.ForeColor = Color.FromArgb(34, 197, 94); // Green
            else _lblSumSplit.ForeColor = ThemeManager.CurrentText;
        }

        private async void btnSendSplit_Click(object sender, EventArgs e)
        {
            if (_currentRentalId < 0) return;

            decimal sum = 0;
            var sharesList = new List<object>();

            foreach (DataGridViewRow row in _gridSplits.Rows)
            {
                string email = row.Cells[0].Value?.ToString();
                decimal amt = Convert.ToDecimal(row.Cells[1].Value);
                string pStatus = row.Cells[2].Value?.ToString();

                sum += amt;
                
                // Only post pending/new splits
                if (pStatus == "pending")
                {
                    sharesList.Add(new { email, amount = amt });
                }
            }

            if (sum > _rentalTotal)
            {
                MessageBox.Show("The total split shares exceed the rental total amount.", "Calculation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (sharesList.Count == 0)
            {
                MessageBox.Show("No new pending split shares to save.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var payload = new { shares = sharesList };
            var res = await ApiService.PostAsync($"rentals/{_currentRentalId}/split", payload);

            if (res.Success)
            {
                MessageBox.Show("Split payments initialized! Notification emails sent to co-renters.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await LoadRentalAsync(); // Reload
            }
            else
            {
                MessageBox.Show($"Failed to initialize split payments: {res.Error}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

        private class SplitShareItem
        {
            public string Email { get; set; }
            public decimal Amount { get; set; }
        }
    }
}
