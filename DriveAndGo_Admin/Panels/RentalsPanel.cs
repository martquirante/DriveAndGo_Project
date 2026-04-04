#nullable disable
using DriveAndGo_Admin.Helpers;
using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

// iText7 PDF Libraries
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Font;
using iText.IO.Font.Constants;
using iText.IO.Image;

using Button = System.Windows.Forms.Button;
using ComboBox = System.Windows.Forms.ComboBox;
using TextBox = System.Windows.Forms.TextBox;
using WinColor = System.Drawing.Color;

namespace DriveAndGo_Admin.Panels
{
    public class RentalsPanel : UserControl
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
        private readonly WinColor ColPurple = WinColor.FromArgb(168, 85, 247);
        private readonly WinColor ColTeal = WinColor.FromArgb(20, 184, 166);

        private readonly string _connStr = "Server=localhost;Database=vehicle_rental_db;Uid=root;Pwd=;";

        // ── UI ──
        private SplitContainer splitContainer;
        private Panel topBar;
        private DataGridView dgvRentals;
        private Panel rightPanel;
        private Panel pnlContract;

        private Label lblContractTitle;
        private Label lblContractBody;
        private Panel pnlStatusBanner;
        private Label lblStatusBanner;
        private FlowLayoutPanel pnlInfoCards;
        private Panel pnlActionBar;

        private Button btnApprove, btnReject, btnComplete, btnConfirmPayment, btnExportPDF;
        private TextBox txtSearch;
        private ComboBox cboStatus, cboPayment;
        private Label lblStats;

        // ── State ──
        private DataTable _data = new DataTable();
        private int _selectedId = -1;
        private DataRow _selectedRow = null;

        public RentalsPanel()
        {
            this.Dock = DockStyle.Fill;
            this.DoubleBuffered = true;
            this.BackColor = ColBg;
            ThemeManager.ThemeChanged += OnThemeChanged;

            BuildUI();

            // Safe DB load after UI construction
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

            BuildLeftPanel();
            BuildRightPanel();
            this.Controls.Add(splitContainer);
        }

        // ── LEFT PANEL ──
        private void BuildLeftPanel()
        {
            topBar = new Panel { Dock = DockStyle.Top, Height = 116, BackColor = WinColor.Transparent, Padding = new Padding(16, 12, 16, 8) };

            var lblTitle = new Label { Text = "Rental Bookings", Font = new Font("Segoe UI", 18F, FontStyle.Bold), ForeColor = ColText, AutoSize = true, Location = new Point(16, 12), BackColor = WinColor.Transparent };
            lblStats = new Label { Text = "Loading...", Font = new Font("Segoe UI", 9F), ForeColor = ColSub, AutoSize = true, Location = new Point(18, 40), BackColor = WinColor.Transparent };

            txtSearch = new TextBox { Size = new Size(200, 30), Location = new Point(16, 72), Font = new Font("Segoe UI", 10F), BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White, ForeColor = ColText, BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "🔍 Search customer / vehicle..." };
            txtSearch.TextChanged += (s, e) => FilterGrid();

            cboStatus = new ComboBox { Size = new Size(120, 30), Location = new Point(226, 72), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White, ForeColor = ColText };
            cboStatus.Items.AddRange(new object[] { "All Status", "pending", "approved", "completed", "rejected" });
            cboStatus.SelectedIndex = 0;
            cboStatus.SelectedIndexChanged += (s, e) => FilterGrid();

            cboPayment = new ComboBox { Size = new Size(110, 30), Location = new Point(356, 72), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White, ForeColor = ColText };
            cboPayment.Items.AddRange(new object[] { "All Payment", "unpaid", "paid" });
            cboPayment.SelectedIndex = 0;
            cboPayment.SelectedIndexChanged += (s, e) => FilterGrid();

            var btnRefresh = CreateBtn("⟳", ColSub, 476, 72, 36);
            btnRefresh.Font = new Font("Segoe UI", 13F);
            btnRefresh.Click += (s, e) => LoadFromDB();

            topBar.Controls.Add(lblTitle); topBar.Controls.Add(lblStats); topBar.Controls.Add(txtSearch);
            topBar.Controls.Add(cboStatus); topBar.Controls.Add(cboPayment); topBar.Controls.Add(btnRefresh);

            dgvRentals = new DataGridView { Dock = DockStyle.Fill };
            StyleGrid(dgvRentals);
            dgvRentals.SelectionChanged += OnRowSelected;
            dgvRentals.CellPainting += OnCellPainting;

            splitContainer.Panel1.Controls.Add(dgvRentals);
            splitContainer.Panel1.Controls.Add(topBar);
        }

        // ── RIGHT PANEL — Contract Viewer ──
        private void BuildRightPanel()
        {
            rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(12, 12, 20) : WinColor.FromArgb(240, 241, 248), Padding = new Padding(16) };

            pnlStatusBanner = new Panel { Size = new Size(420, 32), Location = new Point(16, 16), BackColor = WinColor.FromArgb(30, ColYellow), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Width = rightPanel.Width - 32 };
            lblStatusBanner = new Label { Text = "Select a rental to view details", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = ColYellow, AutoSize = true, Location = new Point(12, 8), BackColor = WinColor.Transparent };
            pnlStatusBanner.Controls.Add(lblStatusBanner);
            pnlStatusBanner.Paint += (s, e) => {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, pnlStatusBanner.Width - 1, pnlStatusBanner.Height - 1);
                using var path = RoundRect(rect, 6);
                g.FillPath(new SolidBrush(WinColor.FromArgb(25, lblStatusBanner.ForeColor)), path);
                g.DrawPath(new Pen(WinColor.FromArgb(80, lblStatusBanner.ForeColor), 1), path);
            };

            pnlInfoCards = new FlowLayoutPanel { Location = new Point(16, 56), BackColor = WinColor.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Width = rightPanel.Width - 32, Height = 80, AutoScroll = false, WrapContents = false };

            pnlContract = new Panel { BackColor = WinColor.White, Location = new Point(16, 140), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, Width = rightPanel.Width - 32, Height = rightPanel.Height - 210, AutoScroll = true };
            pnlContract.Paint += OnContractPaint;

            lblContractTitle = new Label { Text = "VEHICLE RENTAL AGREEMENT", Font = new Font("Georgia", 13F, FontStyle.Bold), ForeColor = WinColor.FromArgb(20, 20, 40), AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Location = new Point(0, 20), BackColor = WinColor.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Width = pnlContract.Width };
            lblContractBody = new Label { Text = "Select a booking to view the contract.", Font = new Font("Segoe UI", 10F), ForeColor = WinColor.FromArgb(30, 30, 50), Location = new Point(30, 60), AutoSize = true, BackColor = WinColor.Transparent };

            pnlContract.Controls.Add(lblContractTitle); pnlContract.Controls.Add(lblContractBody);

            pnlActionBar = new Panel { Height = 52, Dock = DockStyle.Bottom, BackColor = WinColor.Transparent };
            pnlActionBar.Paint += (s, e) => { e.Graphics.DrawLine(new Pen(ColBorder, 1), 0, 0, pnlActionBar.Width, 0); };

            btnApprove = CreateBtn("✔ Approve", ColGreen, 0, 8, 110);
            btnReject = CreateBtn("✖ Reject", ColRed, 116, 8, 90);
            btnComplete = CreateBtn("✓ Complete", ColTeal, 212, 8, 110);
            btnConfirmPayment = CreateBtn("💳 Paid", ColPurple, 328, 8, 80);
            btnExportPDF = CreateBtn("📄 PDF Contract", ColAccent, 414, 8, 130);

            btnApprove.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnReject.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnComplete.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnConfirmPayment.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnExportPDF.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            int rx = pnlActionBar.Width;
            btnExportPDF.Location = new Point(rx - 130 - 16, 8);
            btnConfirmPayment.Location = new Point(btnExportPDF.Left - 80 - 6, 8);
            btnComplete.Location = new Point(btnConfirmPayment.Left - 110 - 6, 8);
            btnReject.Location = new Point(btnComplete.Left - 90 - 6, 8);
            btnApprove.Location = new Point(btnReject.Left - 110 - 6, 8);

            btnApprove.Click += (s, e) => UpdateStatus("approved");
            btnReject.Click += (s, e) => UpdateStatus("rejected");
            btnComplete.Click += (s, e) => UpdateStatus("completed");
            btnConfirmPayment.Click += OnConfirmPayment;
            btnExportPDF.Click += OnExportPDF;

            pnlActionBar.Controls.Add(btnApprove); pnlActionBar.Controls.Add(btnReject); pnlActionBar.Controls.Add(btnComplete); pnlActionBar.Controls.Add(btnConfirmPayment); pnlActionBar.Controls.Add(btnExportPDF);

            rightPanel.Controls.Add(pnlStatusBanner); rightPanel.Controls.Add(pnlInfoCards); rightPanel.Controls.Add(pnlContract); rightPanel.Controls.Add(pnlActionBar);
            splitContainer.Panel2.Controls.Add(rightPanel);
        }

        private void OnThemeChanged(object s, EventArgs e)
        {
            this.BackColor = ColBg;
            splitContainer.Panel1.BackColor = ColBg;
            splitContainer.Panel2.BackColor = ColBg;
            splitContainer.BackColor = ColBorder;
            rightPanel.BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(12, 12, 20) : WinColor.FromArgb(240, 241, 248);

            topBar.BackColor = ThemeManager.IsDarkMode ? ColBg : WinColor.FromArgb(250, 250, 255);
            txtSearch.BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White;
            txtSearch.ForeColor = ColText;
            cboStatus.BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White;
            cboStatus.ForeColor = ColText;
            cboPayment.BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White;
            cboPayment.ForeColor = ColText;

            foreach (Control c in topBar.Controls) { if (c is Label l) l.ForeColor = ColText; }

            if (_selectedRow != null) BuildInfoCards(_selectedRow);

            StyleGrid(dgvRentals);
            this.Invalidate(true);
        }

        // ══ PURE DATABASE LOAD ══
        private void LoadFromDB()
        {
            _data = new DataTable();
            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT 
                        r.rental_id, r.customer_id, u.full_name AS customer_name,
                        r.vehicle_id, CONCAT(v.brand,' ',v.model) AS vehicle_name, v.plate_no AS plate_number,
                        r.driver_id, COALESCE(d.full_name,'No Driver') AS driver_name,
                        r.start_date, r.end_date, r.destination, r.status, r.total_amount,
                        r.payment_method, r.payment_status, r.qr_code, r.created_at,
                        DATEDIFF(r.end_date, CURDATE()) AS days_remaining
                    FROM rentals r
                    JOIN users u ON r.customer_id = u.user_id
                    JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                    LEFT JOIN users d ON r.driver_id = d.user_id
                    ORDER BY r.created_at DESC", conn);

                using var adapter = new MySqlDataAdapter(cmd);
                adapter.Fill(_data);

                RefreshGrid(_data);
                UpdateStats();
            }
            catch (Exception ex)
            {
                RefreshGrid(new DataTable());
                MessageBox.Show($"DB Error Details:\n{ex.Message}", "Database Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ══ GRID ══
        private void RefreshGrid(DataTable dt)
        {
            if (dgvRentals == null) return;
            if (dgvRentals.InvokeRequired) { dgvRentals.Invoke(new Action(() => RefreshGrid(dt))); return; }

            dgvRentals.DataSource = null; dgvRentals.Columns.Clear();
            var display = new DataTable();
            display.Columns.Add("ID", typeof(int)); display.Columns.Add("Customer", typeof(string)); display.Columns.Add("Vehicle", typeof(string));
            display.Columns.Add("Dates", typeof(string)); display.Columns.Add("Amount", typeof(string)); display.Columns.Add("Payment", typeof(string));
            display.Columns.Add("Status", typeof(string)); display.Columns.Add("Return", typeof(string));

            if (dt != null && dt.Rows.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    DateTime s = row["start_date"] != DBNull.Value ? Convert.ToDateTime(row["start_date"]) : DateTime.Now;
                    DateTime e = row["end_date"] != DBNull.Value ? Convert.ToDateTime(row["end_date"]) : DateTime.Now;
                    int rem = row["days_remaining"] != DBNull.Value ? Convert.ToInt32(row["days_remaining"]) : 0;

                    string returnLabel = rem < 0 ? $"⚠ {Math.Abs(rem)}d OVERDUE" : rem == 0 ? "⚠ Due TODAY" : rem <= 2 ? $"⏰ {rem}d left" : $"{rem}d left";
                    string payLabel = row["payment_status"] != DBNull.Value && row["payment_status"].ToString() == "paid" ? "✓ Paid" : "⚡ Unpaid";

                    display.Rows.Add(
                        row["rental_id"],
                        row["customer_name"]?.ToString() ?? "Unknown",
                        $"{row["vehicle_name"]}",
                        $"{s:MMM dd} → {e:MMM dd}",
                        "₱" + (row["total_amount"] != DBNull.Value ? Convert.ToDecimal(row["total_amount"]).ToString("N0") : "0"),
                        payLabel,
                        row["status"]?.ToString() ?? "pending",
                        returnLabel
                    );
                }
            }

            dgvRentals.DataSource = display;

            if (dgvRentals.Columns.Count >= 8)
            {
                dgvRentals.Columns[0].Width = 36; dgvRentals.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; dgvRentals.Columns[2].Width = 120;
                dgvRentals.Columns[3].Width = 130; dgvRentals.Columns[4].Width = 80; dgvRentals.Columns[5].Width = 80; dgvRentals.Columns[6].Width = 90; dgvRentals.Columns[7].Width = 100;
            }
        }

        private void FilterGrid()
        {
            if (_data == null || cboStatus == null || cboPayment == null || txtSearch == null) return;

            string status = cboStatus.SelectedItem?.ToString() ?? "All Status";
            string payment = cboPayment.SelectedItem?.ToString() ?? "All Payment";
            string search = txtSearch.Text?.Trim().ToLower() ?? "";

            var filtered = _data.Clone();
            foreach (DataRow row in _data.Rows)
            {
                bool ok = true;
                string rowStatus = row["status"] != DBNull.Value ? row["status"].ToString().ToLower() : "";
                string rowPayment = row["payment_status"] != DBNull.Value ? row["payment_status"].ToString().ToLower() : "";

                if (status != "All Status" && rowStatus != status.ToLower()) ok = false;
                if (payment != "All Payment" && rowPayment != payment.ToLower()) ok = false;

                if (!string.IsNullOrEmpty(search))
                {
                    string cName = row["customer_name"] != DBNull.Value ? row["customer_name"].ToString().ToLower() : "";
                    string vName = row["vehicle_name"] != DBNull.Value ? row["vehicle_name"].ToString().ToLower() : "";
                    string dest = row["destination"] != DBNull.Value ? row["destination"].ToString().ToLower() : "";

                    if (!cName.Contains(search) && !vName.Contains(search) && !dest.Contains(search)) ok = false;
                }

                if (ok) filtered.ImportRow(row);
            }
            RefreshGrid(filtered);
        }

        private void UpdateStats()
        {
            if (_data == null) return;

            int total = _data.Rows.Count;
            int pending = 0, approved = 0, overdue = 0, unpaid = 0;

            foreach (DataRow row in _data.Rows)
            {
                string st = row["status"] != DBNull.Value ? row["status"].ToString().ToLower() : "";
                if (st == "pending") pending++;
                if (st == "approved") approved++;

                int rem = row["days_remaining"] != DBNull.Value ? Convert.ToInt32(row["days_remaining"]) : 0;
                if (rem < 0 && st == "approved") overdue++;

                string payStatus = row["payment_status"] != DBNull.Value ? row["payment_status"].ToString().ToLower() : "";
                if (payStatus == "unpaid") unpaid++;
            }

            if (lblStats.InvokeRequired) lblStats.Invoke(new Action(() => UpdateStatsLabel(total, pending, approved, overdue, unpaid)));
            else UpdateStatsLabel(total, pending, approved, overdue, unpaid);
        }

        private void UpdateStatsLabel(int total, int pending, int approved, int overdue, int unpaid)
        {
            lblStats.Text = $"{total} total  ·  {pending} pending  ·  {approved} active  ·  {overdue} overdue  ·  {unpaid} unpaid";
        }

        // ══ ROW SELECTED → build contract ══
        private void OnRowSelected(object sender, EventArgs e)
        {
            if (dgvRentals == null || dgvRentals.SelectedRows.Count == 0) return;

            var cellVal = dgvRentals.SelectedRows[0].Cells[0].Value;
            if (cellVal == null || cellVal == DBNull.Value) return;

            int id = Convert.ToInt32(cellVal);
            _selectedId = id;

            var rows = _data.Select($"rental_id = {id}");
            if (rows.Length == 0) return;
            _selectedRow = rows[0];

            BuildInfoCards(_selectedRow);
            BuildContractText(_selectedRow);
            UpdateActionButtons(_selectedRow);
        }

        private void BuildInfoCards(DataRow r)
        {
            pnlInfoCards.Controls.Clear();
            string status = r["status"] != DBNull.Value ? r["status"].ToString().ToLower() : "unknown";
            string payment = r["payment_status"] != DBNull.Value ? r["payment_status"].ToString().ToLower() : "unknown";
            int rem = r["days_remaining"] != DBNull.Value ? Convert.ToInt32(r["days_remaining"]) : 0;

            WinColor bannerColor = status == "approved" ? ColGreen : status == "pending" ? ColYellow : status == "completed" ? ColBlue : status == "rejected" ? ColRed : ColSub;
            lblStatusBanner.Text = $"● {status.ToUpper()}  |  Payment: {payment.ToUpper()}  |  " + (rem >= 0 ? $"Return in {rem} day(s)" : $"⚠ OVERDUE by {Math.Abs(rem)} day(s)");
            lblStatusBanner.ForeColor = bannerColor;
            pnlStatusBanner.Invalidate();

            string sDate = r["start_date"] != DBNull.Value ? Convert.ToDateTime(r["start_date"]).ToString("MMM dd") : "—";
            string eDate = r["end_date"] != DBNull.Value ? Convert.ToDateTime(r["end_date"]).ToString("MMM dd, yyyy") : "—";
            string totalAmt = r["total_amount"] != DBNull.Value ? Convert.ToDecimal(r["total_amount"]).ToString("N2") : "0.00";
            string payMethod = r["payment_method"] != DBNull.Value ? r["payment_method"].ToString().ToUpper() : "—";

            var cards = new[] {
                ("👤 Customer", r["customer_name"]?.ToString() ?? "Unknown", ColBlue),
                ("🚗 Vehicle", (r["vehicle_name"]?.ToString() ?? "—") + "\n" + (r["plate_number"]?.ToString() ?? "—"), ColAccent),
                ("📍 Destination", r["destination"]?.ToString() ?? "—", ColTeal),
                ("💰 Amount", $"₱{totalAmt}\n{payMethod}", ColGreen),
                ("📅 Dates", $"{sDate} →\n{eDate}", ColYellow)
            };

            int cw = (pnlInfoCards.Width - (10 * cards.Length)) / cards.Length;

            foreach (var (label, value, color) in cards)
            {
                var card = new Panel { Size = new Size(cw, 65), Margin = new Padding(0, 0, 10, 0), BackColor = WinColor.Transparent };
                card.Paint += (s, ev) => {
                    var g = ev.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = RoundRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 6);
                    g.FillPath(new SolidBrush(ThemeManager.IsDarkMode ? WinColor.FromArgb(22, 22, 35) : WinColor.White), path);
                    g.FillRectangle(new SolidBrush(color), 0, card.Height - 3, card.Width, 3);
                    g.DrawPath(new Pen(WinColor.FromArgb(25, color), 1), path);
                };

                card.Controls.Add(new Label { Text = label, Font = new Font("Segoe UI", 7F, FontStyle.Bold), ForeColor = color, AutoSize = true, Location = new Point(8, 6), BackColor = WinColor.Transparent });
                card.Controls.Add(new Label { Text = value, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = ColText, AutoSize = false, Size = new Size(cw - 12, 36), Location = new Point(8, 22), BackColor = WinColor.Transparent });
                pnlInfoCards.Controls.Add(card);
            }
        }

        private void BuildContractText(DataRow r)
        {
            string customer = r["customer_name"]?.ToString() ?? "Unknown";
            string vehicle = r["vehicle_name"]?.ToString() ?? "Unknown";
            string plate = r["plate_number"]?.ToString() ?? "Unknown";
            string driver = r["driver_name"]?.ToString() ?? "No Driver";
            string dest = r["destination"]?.ToString() ?? "Not specified";
            string start = r["start_date"] != DBNull.Value ? Convert.ToDateTime(r["start_date"]).ToString("MMMM dd, yyyy") : "—";
            string end = r["end_date"] != DBNull.Value ? Convert.ToDateTime(r["end_date"]).ToString("MMMM dd, yyyy") : "—";
            string amount = r["total_amount"] != DBNull.Value ? "₱" + Convert.ToDecimal(r["total_amount"]).ToString("N2") : "₱0.00";
            string method = r["payment_method"]?.ToString().ToUpper() ?? "—";
            string payStatus = r["payment_status"]?.ToString().ToUpper() ?? "—";
            string status = r["status"]?.ToString().ToUpper() ?? "—";
            string created = r["created_at"] != DBNull.Value ? Convert.ToDateTime(r["created_at"]).ToString("MMMM dd, yyyy hh:mm tt") : "—";
            int rentalId = r["rental_id"] != DBNull.Value ? Convert.ToInt32(r["rental_id"]) : 0;

            string contract =
                "Agreement No.: DG-" + rentalId.ToString("D5") + "\n" +
                "Date Prepared: " + created + "\n\n" +
                "────────────────────────────────────────────────────────────\n\n" +
                "This Vehicle Rental Agreement is entered into by and between:\n\n" +
                "LESSOR:  Drive & Go Vehicle Rental Services\n" +
                "         (Hereinafter referred to as 'the Company')\n\n" +
                "LESSEE:  " + customer + "\n" +
                "         (Hereinafter referred to as 'the Customer')\n\n" +
                "────────────────────────────────────────────────────────────\n" +
                "SECTION 1 — VEHICLE DETAILS\n" +
                "────────────────────────────────────────────────────────────\n\n" +
                "   Vehicle: " + vehicle + "\n" +
                "   Plate Number  : " + plate + "\n" +
                "   Assigned Driver: " + driver + "\n\n" +
                "────────────────────────────────────────────────────────────\n" +
                "SECTION 2 — RENTAL PERIOD & DESTINATION\n" +
                "────────────────────────────────────────────────────────────\n\n" +
                "   Start Date    : " + start + "\n" +
                "   Return Date   : " + end + "\n" +
                "   Destination   : " + dest + "\n\n" +
                "   The Customer agrees to return the vehicle on or before\n" +
                "   the Return Date. Late returns are subject to additional\n" +
                "   charges of ₱500.00 per hour beyond the agreed period.\n\n" +
                "────────────────────────────────────────────────────────────\n" +
                "SECTION 3 — PAYMENT & SUBMITTED REQUIREMENTS\n" +
                "────────────────────────────────────────────────────────────\n\n" +
                "   Total Amount  : " + amount + "\n" +
                "   Payment Method: " + method + "\n" +
                "   Payment Status: " + payStatus + "\n" +
                "   Booking Status: " + status + "\n\n" +
                "   DOCUMENTS VERIFIED VIA APP:\n" +
                "   [✓] Valid Professional Driver's License\n" +
                "   [✓] Government Issued ID\n" +
                "   [✓] Proof of Billing\n\n" +
                "────────────────────────────────────────────────────────────\n" +
                "SECTION 4 — COMPREHENSIVE TERMS & CONDITIONS\n" +
                "────────────────────────────────────────────────────────────\n\n" +
                "   1. GPS TRACKING — The Customer acknowledges and agrees\n" +
                "      that the rented vehicle is equipped with a live GPS \n" +
                "      tracking system for security and monitoring purposes. \n" +
                "      Disabling, removing, or tampering with the GPS system \n" +
                "      is strictly prohibited and constitutes a breach of \n" +
                "      contract. Such actions will result in immediate \n" +
                "      termination of the rental, forfeiture of any deposit, \n" +
                "      and possible legal action.\n\n" +
                "   2. CONDITION OF VEHICLE — The vehicle must be returned in \n" +
                "      the same condition as it was when released. Any damage, \n" +
                "      scratch, dent, or mechanical issue caused by negligence \n" +
                "      during the rental period will be charged fully to the \n" +
                "      Customer. The Company reserves the right to withhold \n" +
                "      deposits pending damage assessment.\n\n" +
                "   3. TRAFFIC VIOLATIONS — The Customer assumes full \n" +
                "      responsibility for all traffic violations, towing \n" +
                "      fees, impound fees, and any other penalties incurred \n" +
                "      during the rental period. The Company is not liable \n" +
                "      for the Customer's actions on the road.\n\n" +
                "   4. FUEL POLICY — The vehicle must be returned with the \n" +
                "      exact same fuel level as recorded upon release. Any \n" +
                "      fuel shortfall will be charged to the Customer based \n" +
                "      on the current premium market rates plus a refuelling \n" +
                "      service charge.\n\n" +
                "   5. RENTAL EXTENSIONS — If the Customer wishes to extend \n" +
                "      the rental period, they must notify and secure approval \n" +
                "      from the Company at least two (2) hours before the \n" +
                "      original return time. Unapproved extensions will incur \n" +
                "      a penalty fee of ₱500.00 per hour of delay.\n\n" +
                "   6. RESTRICTED USAGE — The rented vehicle must NOT be used \n" +
                "      for any illegal activities, racing, off-road driving, \n" +
                "      or transporting hazardous materials. Only the declared \n" +
                "      and authorized driver/s are allowed to operate the \n" +
                "      vehicle.\n\n" +
                "   7. CANCELLATION POLICY — Cancellations made within 24 \n" +
                "      hours of the scheduled start date are strictly non-\n" +
                "      refundable to cover administrative and opportunity \n" +
                "      costs.\n\n" +
                "────────────────────────────────────────────────────────────\n\n" +
                "By proceeding with this rental transaction and signing below, \n" +
                "the Customer hereby acknowledges that they have read, \n" +
                "understood, and agreed to be fully bound by all the terms \n" +
                "and conditions stated in this agreement.\n\n\n\n\n" +
                "___________________________    ___________________________\n" +
                " Authorized Admin Signature     Customer Signature / App\n" +
                "                                      Confirmation";

            lblContractBody.Text = contract;
            pnlContract.Invalidate();
        }

        private void UpdateActionButtons(DataRow r)
        {
            string status = r["status"] != DBNull.Value ? r["status"].ToString().ToLower() : "";
            string payment = r["payment_status"] != DBNull.Value ? r["payment_status"].ToString().ToLower() : "";

            btnApprove.Enabled = status == "pending";
            btnReject.Enabled = status == "pending";
            btnComplete.Enabled = status == "approved";
            btnConfirmPayment.Enabled = payment == "unpaid";
            btnExportPDF.Enabled = true;
        }

        // ══ ACTIONS ══
        private void UpdateStatus(string newStatus)
        {
            if (_selectedId < 0) return;

            string label = newStatus == "approved" ? "Approve" : newStatus == "rejected" ? "Reject" : "Complete";
            if (MessageBox.Show($"{label} this rental?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();

                var cmd = new MySqlCommand("UPDATE rentals SET status = @s WHERE rental_id = @id", conn);
                cmd.Parameters.AddWithValue("@s", newStatus);
                cmd.Parameters.AddWithValue("@id", _selectedId);
                cmd.ExecuteNonQuery();

                if (newStatus == "approved" && _selectedRow != null && _selectedRow["vehicle_id"] != DBNull.Value)
                {
                    var vc = new MySqlCommand("UPDATE vehicles SET status = 'Rented' WHERE vehicle_id = @vid", conn);
                    vc.Parameters.AddWithValue("@vid", _selectedRow["vehicle_id"]);
                    vc.ExecuteNonQuery();
                }

                if (newStatus == "completed" && _selectedRow != null && _selectedRow["vehicle_id"] != DBNull.Value)
                {
                    var vc = new MySqlCommand("UPDATE vehicles SET status = 'Available' WHERE vehicle_id = @vid", conn);
                    vc.Parameters.AddWithValue("@vid", _selectedRow["vehicle_id"]);
                    vc.ExecuteNonQuery();
                }

                MessageBox.Show($"Rental successfully {newStatus}!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadFromDB();
            }
            catch (Exception ex)
            {
                MessageBox.Show("DB Error: " + ex.Message);
            }
        }

        private void OnConfirmPayment(object s, EventArgs e)
        {
            if (_selectedId < 0 || _selectedRow == null) return;
            if (MessageBox.Show("Confirm payment for this rental?", "Confirm Payment", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();
                var cmd = new MySqlCommand("UPDATE rentals SET payment_status = 'paid' WHERE rental_id = @id", conn);
                cmd.Parameters.AddWithValue("@id", _selectedId);
                cmd.ExecuteNonQuery();

                if (TableExists(conn, "transactions"))
                {
                    var tx = new MySqlCommand(@"INSERT INTO transactions (rental_id, amount, type, method, status, paid_at)
                                                SELECT rental_id, total_amount, 'payment', payment_method, 'confirmed', NOW()
                                                FROM rentals WHERE rental_id = @id", conn);
                    tx.Parameters.AddWithValue("@id", _selectedId);
                    tx.ExecuteNonQuery();
                }

                MessageBox.Show("Payment confirmed! Transaction recorded.", "Payment Confirmed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadFromDB();
            }
            catch (Exception ex) { MessageBox.Show("DB Error: " + ex.Message); }
        }

        // ══ PDF EXPORT via iText7 ══
        private void OnExportPDF(object s, EventArgs e)
        {
            if (_selectedId < 0 || _selectedRow == null)
            {
                MessageBox.Show("Select a rental first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title = "Save Rental Contract",
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = $"DriveAndGo_Contract_{_selectedId}.pdf"
            };

            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                var r = _selectedRow;
                string path = dlg.FileName;

                // Validate path and ensure directory exists
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                // Create PDF with WriterProperties to avoid serialization issues
                var writerProps = new iText.Kernel.Pdf.WriterProperties();
                writerProps.SetCompressionLevel(9);
                
                using var writer = new PdfWriter(path, writerProps);
                using var pdf = new PdfDocument(writer);
                using var doc = new Document(pdf);

                PdfFont fontBold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                PdfFont fontNormal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

                var orange = new iText.Kernel.Colors.DeviceRgb(230, 81, 0);
                var dark = new iText.Kernel.Colors.DeviceRgb(20, 20, 40);
                var gray = new iText.Kernel.Colors.DeviceRgb(100, 100, 130);
                var white = iText.Kernel.Colors.ColorConstants.WHITE;
                var lightBg = new iText.Kernel.Colors.DeviceRgb(248, 248, 252);

                // ── HEADER ──
                var headerTable = new iText.Layout.Element.Table(1).UseAllAvailableWidth();
                var headerCell = new Cell()
                    .SetBackgroundColor(orange)
                    .SetPadding(16)
                    .SetBorder(iText.Layout.Borders.Border.NO_BORDER);

                headerCell.Add(new Paragraph("DRIVE & GO")
                    .SetFontColor(white).SetFontSize(24).SetFont(fontBold));
                headerCell.Add(new Paragraph("Vehicle Rental Services")
                    .SetFontColor(new iText.Kernel.Colors.DeviceRgb(255, 200, 160)).SetFontSize(12).SetFont(fontNormal));

                headerTable.AddCell(headerCell);
                doc.Add(headerTable);
                doc.Add(new Paragraph(" "));

                // Title
                doc.Add(new Paragraph("VEHICLE RENTAL AGREEMENT")
                    .SetFontSize(16).SetFont(fontBold).SetFontColor(dark)
                    .SetTextAlignment(TextAlignment.CENTER));

                DateTime createdAt = r["created_at"] != DBNull.Value ? Convert.ToDateTime(r["created_at"]) : DateTime.Now;
                doc.Add(new Paragraph(
                        $"Agreement No.: DG-{_selectedId:D5}  |  " +
                        $"Date: {createdAt:MMMM dd, yyyy}")
                    .SetFontSize(9).SetFontColor(gray).SetFont(fontNormal)
                    .SetTextAlignment(TextAlignment.CENTER));
                doc.Add(new Paragraph(" "));

                // Info table
                var infoTable = new iText.Layout.Element.Table(new float[] { 1, 1 })
                    .UseAllAvailableWidth().SetMarginBottom(12);

                void AddInfoRow(string lbl, string val)
                {
                    infoTable.AddCell(new Cell().SetBackgroundColor(lightBg).SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPadding(8).Add(new Paragraph(lbl).SetFontSize(9).SetFontColor(gray).SetFont(fontNormal)));
                    infoTable.AddCell(new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPadding(8).Add(new Paragraph(val).SetFontSize(10).SetFont(fontBold).SetFontColor(dark)));
                }

                string customer = r["customer_name"]?.ToString() ?? "Unknown";
                string vehicle = r["vehicle_name"]?.ToString() ?? "Unknown";
                string plate = r["plate_number"]?.ToString() ?? "Unknown";
                string driver = r["driver_name"]?.ToString() ?? "No Driver";
                string dest = r["destination"]?.ToString() ?? "Not specified";
                string start = r["start_date"] != DBNull.Value ? Convert.ToDateTime(r["start_date"]).ToString("MMM dd, yyyy") : "—";
                string end = r["end_date"] != DBNull.Value ? Convert.ToDateTime(r["end_date"]).ToString("MMM dd, yyyy") : "—";
                string amount = r["total_amount"] != DBNull.Value ? Convert.ToDecimal(r["total_amount"]).ToString("N2") : "0.00";
                string method = r["payment_method"]?.ToString().ToUpper() ?? "—";
                string payStatus = r["payment_status"]?.ToString().ToUpper() ?? "—";

                AddInfoRow("Customer Name", customer);
                AddInfoRow("Vehicle", vehicle + " — " + plate);
                AddInfoRow("Assigned Driver", driver);
                AddInfoRow("Destination", dest);
                AddInfoRow("Rental Period", start + " to " + end);
                AddInfoRow("Total Amount", "PHP " + amount);
                AddInfoRow("Payment Status", payStatus + " (" + method + ")");
                AddInfoRow("Submitted Documents", "Driver's License [VERIFIED]\nValid Govt ID [VERIFIED]\nProof of Billing [VERIFIED]");

                doc.Add(infoTable);

                // Extended Terms
                doc.Add(new Paragraph("COMPREHENSIVE TERMS & CONDITIONS").SetFontSize(11).SetFont(fontBold).SetFontColor(orange));
                var terms = new[] {
                    "GPS TRACKING — The Customer acknowledges and agrees that the rented vehicle is equipped with a live GPS tracking system for security and monitoring purposes. Disabling, removing, or tampering with the GPS system is strictly prohibited and constitutes a breach of contract. Such actions will result in immediate termination of the rental, forfeiture of any deposit, and possible legal action.",
                    "CONDITION OF VEHICLE — The vehicle must be returned in the same condition as it was when released. Any damage, scratch, dent, or mechanical issue caused by negligence during the rental period will be charged fully to the Customer. The Company reserves the right to withhold deposits pending damage assessment.",
                    "TRAFFIC VIOLATIONS — The Customer assumes full responsibility for all traffic violations, towing fees, impound fees, and any other penalties incurred during the rental period. The Company is not liable for the Customer's actions on the road.",
                    "FUEL POLICY — The vehicle must be returned with the exact same fuel level as recorded upon release. Any fuel shortfall will be charged to the Customer based on the current premium market rates plus a refuelling service charge.",
                    "RENTAL EXTENSIONS — If the Customer wishes to extend the rental period, they must notify and secure approval from the Company at least two (2) hours before the original return time. Unapproved extensions will incur a penalty fee of ₱500.00 per hour of delay.",
                    "RESTRICTED USAGE — The rented vehicle must NOT be used for any illegal activities, racing, off-road driving, or transporting hazardous materials. Only the declared and authorized driver/s are allowed to operate the vehicle.",
                    "CANCELLATION POLICY — Cancellations made within 24 hours of the scheduled start date are strictly non-refundable to cover administrative and opportunity costs."
                };

                foreach (var t in terms) 
                    doc.Add(new Paragraph("• " + t).SetFontSize(9f).SetFontColor(dark).SetMarginBottom(6).SetFont(fontNormal));

                doc.Add(new Paragraph("By proceeding with this rental transaction and signing below, the Customer hereby acknowledges that they have read, understood, and agreed to be fully bound by all the terms and conditions stated in this agreement.")
                    .SetFontSize(9f).SetFontColor(dark).SetMarginTop(10).SetFont(fontNormal));

                doc.Add(new Paragraph(" ").SetMarginTop(20));

                // Signatures
                var sigTable = new iText.Layout.Element.Table(new float[] { 1, 1 }).UseAllAvailableWidth();
                sigTable.AddCell(new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPaddingTop(30).Add(new Paragraph("____________________________\nAuthorized Admin Signature").SetFontSize(9).SetFontColor(gray).SetFont(fontNormal)));
                sigTable.AddCell(new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPaddingTop(30).Add(new Paragraph("____________________________\nCustomer Signature / App Confirmation").SetFontSize(9).SetFontColor(gray).SetTextAlignment(TextAlignment.RIGHT).SetFont(fontNormal)));
                doc.Add(sigTable);

                // Footer
                doc.Add(new Paragraph("Drive & Go Vehicle Rental Services  |  Generated: " + DateTime.Now.ToString("MMM dd, yyyy hh:mm tt")).SetFontSize(8).SetFontColor(gray).SetTextAlignment(TextAlignment.CENTER).SetMarginTop(20).SetFont(fontNormal));

                MessageBox.Show("PDF contract saved successfully!\n" + path, "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("PDF Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ══ PAINT HANDLERS ══
        private void OnContractPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(new SolidBrush(ColAccent), 0, 0, pnlContract.Width, 5);
            using var pen = new Pen(WinColor.FromArgb(40, 0, 0, 0), 1);
            g.DrawRectangle(pen, 0, 0, pnlContract.Width - 1, pnlContract.Height - 1);
        }

        private void OnCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.Value == null || e.Value == DBNull.Value) return;

            if (e.ColumnIndex == 8) // Status
            {
                e.Handled = true; e.PaintBackground(e.ClipBounds, true);
                string val = e.Value.ToString();
                WinColor c = val == "approved" ? ColGreen : val == "pending" ? ColYellow : val == "completed" ? ColBlue : val == "rejected" ? ColRed : ColSub;
                DrawBadge(e.Graphics, e.CellBounds, val, c);
            }
            if (e.ColumnIndex == 7) // Payment
            {
                e.Handled = true; e.PaintBackground(e.ClipBounds, true);
                string val = e.Value.ToString();
                WinColor c = val.Contains("Paid") ? ColGreen : ColYellow;
                DrawBadge(e.Graphics, e.CellBounds, val, c);
            }
            if (e.ColumnIndex == 9) // Return Date Warning
            {
                e.Handled = true; e.PaintBackground(e.ClipBounds, true);
                string val = e.Value?.ToString() ?? "";
                WinColor c = val.Contains("OVERDUE") || val.Contains("TODAY") ? ColRed : val.Contains("⏰") ? ColYellow : ColSub;
                TextRenderer.DrawText(e.Graphics, val, new Font("Segoe UI", 8.5F, FontStyle.Bold), e.CellBounds, c, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            }
        }

        private void DrawBadge(Graphics g, Rectangle bounds, string text, WinColor c)
        {
            var rect = new Rectangle(bounds.X + 6, bounds.Y + 9, bounds.Width - 12, bounds.Height - 18);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundRect(rect, 8);
            g.FillPath(new SolidBrush(WinColor.FromArgb(30, c)), path);
            g.DrawPath(new Pen(c, 1), path);
            TextRenderer.DrawText(g, text, new Font("Segoe UI", 8F, FontStyle.Bold), rect, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
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
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

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

        private bool TableExists(MySqlConnection conn, string t)
        {
            try { using var c = new MySqlCommand($"SHOW TABLES LIKE '{t}'", conn); return c.ExecuteScalar() != null; } catch { return false; }
        }

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

        protected override void Dispose(bool disposing)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }
    }
}