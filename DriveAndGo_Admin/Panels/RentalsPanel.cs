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
        // ── Theme colors ──────────────────────────────────────────────────
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
        private readonly WinColor ColOrange = WinColor.FromArgb(249, 115, 22);

        private readonly string _connStr = "Server=localhost;Database=vehicle_rental_db;Uid=root;Pwd=;";

        // Path to the GCash QR image in WebAssets
        private static string GcashQrPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "gcash_qr.png");

        // ── UI controls ───────────────────────────────────────────────────
        private SplitContainer splitContainer;
        private Panel topBar;
        private DataGridView dgvRentals;
        private Panel rightPanel, pnlContract, pnlStatusBanner, pnlActionBar;
        private Label lblContractTitle, lblContractBody, lblStatusBanner, lblStats;
        private FlowLayoutPanel pnlInfoCards;

        // Action buttons
        private Button btnApprove, btnReject, btnComplete,
                       btnConfirmPayment, btnExportPDF, btnWalkIn,
                       btnCancel, btnExtensions;

        private TextBox txtSearch;
        private ComboBox cboStatus, cboPayment;

        // ── State ──────────────────────────────────────────────────────────
        private DataTable _data = new DataTable();
        private int _selectedId = -1;
        private DataRow _selectedRow = null;

        // ══════════════════════════════════════════════════════════════════
        public RentalsPanel()
        {
            Dock = DockStyle.Fill;
            DoubleBuffered = true;
            BackColor = ColBg;
            ThemeManager.ThemeChanged += OnThemeChanged;

            BuildUI();
            Load += (s, e) => LoadFromDB();
        }

        // ── BUILD UI ──────────────────────────────────────────────────────
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
            Controls.Add(splitContainer);
        }

        // ── LEFT PANEL ────────────────────────────────────────────────────
        private void BuildLeftPanel()
        {
            topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 116,
                BackColor = WinColor.Transparent,
                Padding = new Padding(16, 12, 16, 8)
            };

            var lblTitle = new Label
            {
                Text = "Rental Bookings",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = ColText,
                AutoSize = true,
                Location = new Point(16, 12),
                BackColor = WinColor.Transparent
            };

            lblStats = new Label
            {
                Text = "Loading...",
                Font = new Font("Segoe UI", 9F),
                ForeColor = ColSub,
                AutoSize = true,
                Location = new Point(18, 40),
                BackColor = WinColor.Transparent
            };

            txtSearch = new TextBox
            {
                Size = new Size(170, 30),
                Location = new Point(16, 72),
                Font = new Font("Segoe UI", 10F),
                BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White,
                ForeColor = ColText,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "🔍 Search customer / vehicle..."
            };
            txtSearch.TextChanged += (s, e) => FilterGrid();

            cboStatus = new ComboBox
            {
                Size = new Size(105, 30),
                Location = new Point(196, 72),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F),
                BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White,
                ForeColor = ColText
            };
            cboStatus.Items.AddRange(new object[]
                { "All Status","pending","approved","completed","rejected","active","cancelled" });
            cboStatus.SelectedIndex = 0;
            cboStatus.SelectedIndexChanged += (s, e) => FilterGrid();

            cboPayment = new ComboBox
            {
                Size = new Size(95, 30),
                Location = new Point(311, 72),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F),
                BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White,
                ForeColor = ColText
            };
            cboPayment.Items.AddRange(new object[] { "All Payment", "unpaid", "paid", "refunded" });
            cboPayment.SelectedIndex = 0;
            cboPayment.SelectedIndexChanged += (s, e) => FilterGrid();

            var btnRefresh = CreateBtn("⟳", ColSub, 416, 72, 40);
            btnRefresh.Font = new Font("Segoe UI", 13F);
            btnRefresh.Click += (s, e) => LoadFromDB();

            btnWalkIn = CreateBtn("＋ Walk-In", ColAccent, 466, 72, 120);
            btnWalkIn.Click += OnWalkInRental;

            topBar.Controls.AddRange(new Control[]
                { lblTitle, lblStats, txtSearch, cboStatus, cboPayment, btnRefresh, btnWalkIn });

            dgvRentals = new DataGridView { Dock = DockStyle.Fill };
            StyleGrid(dgvRentals);
            dgvRentals.SelectionChanged += OnRowSelected;
            dgvRentals.CellPainting += OnCellPainting;

            splitContainer.Panel1.Controls.Add(dgvRentals);
            splitContainer.Panel1.Controls.Add(topBar);
        }

        // ── RIGHT PANEL ───────────────────────────────────────────────────
        private void BuildRightPanel()
        {
            rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(12, 12, 20) : WinColor.FromArgb(240, 241, 248),
                Padding = new Padding(16)
            };

            // Status banner
            pnlStatusBanner = new Panel
            {
                Size = new Size(420, 32),
                Location = new Point(16, 16),
                BackColor = WinColor.FromArgb(30, ColYellow),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Width = rightPanel.Width - 32
            };
            lblStatusBanner = new Label
            {
                Text = "Select a rental to view details",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ColYellow,
                AutoSize = true,
                Location = new Point(12, 8),
                BackColor = WinColor.Transparent
            };
            pnlStatusBanner.Controls.Add(lblStatusBanner);
            pnlStatusBanner.Paint += (s, e) =>
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, pnlStatusBanner.Width - 1, pnlStatusBanner.Height - 1);
                using var path = RoundRect(rect, 6);
                g.FillPath(new SolidBrush(WinColor.FromArgb(25, lblStatusBanner.ForeColor)), path);
                g.DrawPath(new Pen(WinColor.FromArgb(80, lblStatusBanner.ForeColor), 1), path);
            };

            // Info cards
            pnlInfoCards = new FlowLayoutPanel
            {
                Location = new Point(16, 56),
                BackColor = WinColor.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Width = rightPanel.Width - 32,
                Height = 144,
                AutoScroll = false,
                WrapContents = true
            };

            // Contract panel
            pnlContract = new Panel
            {
                BackColor = WinColor.White,
                Location = new Point(16, 206),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Width = rightPanel.Width - 32,
                Height = rightPanel.Height - 276,
                AutoScroll = true
            };
            pnlContract.Paint += OnContractPaint;

            lblContractTitle = new Label
            {
                Text = "VEHICLE RENTAL AGREEMENT",
                Font = new Font("Georgia", 13F, FontStyle.Bold),
                ForeColor = WinColor.FromArgb(20, 20, 40),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 20),
                BackColor = WinColor.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Width = pnlContract.Width
            };
            lblContractBody = new Label
            {
                Text = "Select a booking to view the contract.",
                Font = new Font("Segoe UI", 10F),
                ForeColor = WinColor.FromArgb(30, 30, 50),
                Location = new Point(30, 60),
                AutoSize = true,
                BackColor = WinColor.Transparent
            };

            pnlContract.Controls.Add(lblContractTitle);
            pnlContract.Controls.Add(lblContractBody);

            // Action bar
            pnlActionBar = new Panel
            {
                Height = 52,
                Dock = DockStyle.Bottom,
                BackColor = WinColor.Transparent
            };
            pnlActionBar.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(ColBorder, 1), 0, 0, pnlActionBar.Width, 0);

            // ── Create action buttons ─────────────────────────────────────
            btnApprove = CreateBtn("✔ Approve", ColGreen, 0, 8, 100);
            btnReject = CreateBtn("✖ Reject", ColRed, 106, 8, 84);
            btnCancel = CreateBtn("⊗ Cancel", ColOrange, 196, 8, 84);
            btnComplete = CreateBtn("✓ Complete", ColTeal, 286, 8, 100);
            btnExtensions = CreateBtn("📋 Extensions", ColPurple, 392, 8, 110);
            btnConfirmPayment = CreateBtn("💳 Paid", ColGreen, 508, 8, 80);
            btnExportPDF = CreateBtn("📄 PDF", ColAccent, 594, 8, 70);

            btnApprove.Click += (s, e) => UpdateStatus("approved");
            btnReject.Click += (s, e) => UpdateStatus("rejected");
            btnCancel.Click += (s, e) => UpdateStatus("cancelled");
            btnComplete.Click += (s, e) => UpdateStatus("completed");
            btnExtensions.Click += OnManageExtensions;
            btnConfirmPayment.Click += OnConfirmPayment;
            btnExportPDF.Click += OnExportPDF;

            pnlActionBar.Controls.AddRange(new Control[]
                { btnApprove, btnReject, btnCancel, btnComplete,
                  btnExtensions, btnConfirmPayment, btnExportPDF });

            rightPanel.Controls.Add(pnlStatusBanner);
            rightPanel.Controls.Add(pnlInfoCards);
            rightPanel.Controls.Add(pnlContract);
            rightPanel.Controls.Add(pnlActionBar);
            splitContainer.Panel2.Controls.Add(rightPanel);
        }

        // ── Theme change ──────────────────────────────────────────────────
        private void OnThemeChanged(object s, EventArgs e)
        {
            BackColor = ColBg;
            splitContainer.Panel1.BackColor = ColBg;
            splitContainer.Panel2.BackColor = ColBg;
            splitContainer.BackColor = ColBorder;
            rightPanel.BackColor = ThemeManager.IsDarkMode
                ? WinColor.FromArgb(12, 12, 20) : WinColor.FromArgb(240, 241, 248);
            topBar.BackColor = ThemeManager.IsDarkMode ? ColBg : WinColor.FromArgb(250, 250, 255);
            txtSearch.BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White;
            txtSearch.ForeColor = ColText;
            cboStatus.BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White;
            cboStatus.ForeColor = ColText;
            cboPayment.BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White;
            cboPayment.ForeColor = ColText;
            foreach (Control c in topBar.Controls) if (c is Label l) l.ForeColor = ColText;
            if (_selectedRow != null) BuildInfoCards(_selectedRow);
            StyleGrid(dgvRentals);
            Invalidate(true);
        }

        // ══ DATABASE LOAD ══════════════════════════════════════════════════
        private void LoadFromDB()
        {
            _data = new DataTable();
            try
            {
                AdminDataHelper.ReconcilePaidRentalTransactions(_connStr);

                using var conn = new MySqlConnection(_connStr);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT
                        r.rental_id,
                        r.customer_id,
                        u.full_name          AS customer_name,
                        r.vehicle_id,
                        CONCAT(v.brand,' ',v.model) AS vehicle_name,
                        v.plate_no           AS plate_number,
                        r.driver_id,
                        COALESCE(d.full_name,'No Driver') AS driver_name,
                        r.start_date,
                        r.end_date,
                        r.destination,
                        r.status,
                        r.total_amount,
                        r.payment_method,
                        r.payment_status,
                        r.qr_code,
                        r.created_at,
                        DATEDIFF(r.end_date, CURDATE()) AS days_remaining,
                        (SELECT COUNT(*) FROM extensions e
                          WHERE e.rental_id=r.rental_id
                            AND LOWER(COALESCE(e.status,''))='pending')  AS pending_extensions,
                        (SELECT COUNT(*) FROM issues i
                          WHERE i.rental_id=r.rental_id
                            AND LOWER(COALESCE(i.status,''))<>'resolved') AS open_issues,
                        (SELECT COUNT(*) FROM messages m
                          WHERE m.rental_id=r.rental_id)                 AS unread_messages
                    FROM rentals r
                    JOIN  users    u ON r.customer_id = u.user_id
                    JOIN  vehicles v ON r.vehicle_id  = v.vehicle_id
                    LEFT JOIN users d ON r.driver_id  = d.user_id
                    ORDER BY r.created_at DESC", conn);

                using var adapter = new MySqlDataAdapter(cmd);
                adapter.Fill(_data);

                RefreshGrid(_data);
                UpdateStats();
            }
            catch (Exception ex)
            {
                RefreshGrid(new DataTable());
                MessageBox.Show($"DB Error:\n{ex.Message}", "Database Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ══ GRID ═══════════════════════════════════════════════════════════
        private void RefreshGrid(DataTable dt)
        {
            if (dgvRentals == null) return;
            if (dgvRentals.InvokeRequired) { dgvRentals.Invoke(new Action(() => RefreshGrid(dt))); return; }

            dgvRentals.DataSource = null;
            dgvRentals.Columns.Clear();

            var display = new DataTable();
            display.Columns.Add("ID", typeof(int));
            display.Columns.Add("Customer", typeof(string));
            display.Columns.Add("Vehicle", typeof(string));
            display.Columns.Add("Dates", typeof(string));
            display.Columns.Add("Amount", typeof(string));
            display.Columns.Add("Payment", typeof(string));
            display.Columns.Add("Status", typeof(string));
            display.Columns.Add("Return", typeof(string));

            if (dt?.Rows.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    DateTime sd = row["start_date"] != DBNull.Value ? Convert.ToDateTime(row["start_date"]) : DateTime.Now;
                    DateTime ed = row["end_date"] != DBNull.Value ? Convert.ToDateTime(row["end_date"]) : DateTime.Now;
                    int rem = row["days_remaining"] != DBNull.Value ? Convert.ToInt32(row["days_remaining"]) : 0;

                    string returnLabel = rem < 0 ? $"⚠ {Math.Abs(rem)}d OVERDUE"
                                       : rem == 0 ? "⚠ Due TODAY"
                                       : rem <= 2 ? $"⏰ {rem}d left"
                                       : $"{rem}d left";

                    string payLabel = row["payment_status"] != DBNull.Value &&
                                      row["payment_status"].ToString().ToLower() == "paid"
                                      ? "✓ Paid" : "⚡ Unpaid";

                    display.Rows.Add(
                        row["rental_id"],
                        row["customer_name"]?.ToString() ?? "Unknown",
                        row["vehicle_name"]?.ToString() ?? "Unknown",
                        $"{sd:MMM dd} → {ed:MMM dd}",
                        "₱" + (row["total_amount"] != DBNull.Value
                            ? Convert.ToDecimal(row["total_amount"]).ToString("N0") : "0"),
                        payLabel,
                        row["status"]?.ToString() ?? "pending",
                        returnLabel);
                }
            }

            dgvRentals.DataSource = display;

            if (dgvRentals.Columns.Count >= 8)
            {
                dgvRentals.Columns[0].Width = 44;
                dgvRentals.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                dgvRentals.Columns[2].Width = 180;
                dgvRentals.Columns[3].Width = 130;
                dgvRentals.Columns[4].Width = 100;
                dgvRentals.Columns[5].Width = 95;
                dgvRentals.Columns[6].Width = 95;
                dgvRentals.Columns[7].Width = 110;
            }
        }

        private void FilterGrid()
        {
            if (_data == null) return;
            string status = cboStatus?.SelectedItem?.ToString() ?? "All Status";
            string payment = cboPayment?.SelectedItem?.ToString() ?? "All Payment";
            string search = txtSearch?.Text?.Trim().ToLower() ?? "";

            var filtered = _data.Clone();
            foreach (DataRow row in _data.Rows)
            {
                string rowStatus = row["status"]?.ToString().ToLower() ?? "";
                string rowPayment = row["payment_status"]?.ToString().ToLower() ?? "";

                bool ok = true;
                if (status != "All Status" && rowStatus != status.ToLower()) ok = false;
                if (payment != "All Payment" && rowPayment != payment.ToLower()) ok = false;

                if (!string.IsNullOrEmpty(search))
                {
                    string cN = row["customer_name"]?.ToString().ToLower() ?? "";
                    string vN = row["vehicle_name"]?.ToString().ToLower() ?? "";
                    string ds = row["destination"]?.ToString().ToLower() ?? "";
                    if (!cN.Contains(search) && !vN.Contains(search) && !ds.Contains(search))
                        ok = false;
                }
                if (ok) filtered.ImportRow(row);
            }
            RefreshGrid(filtered);
        }

        private void UpdateStats()
        {
            if (_data == null) return;
            int total = 0, pending = 0, approved = 0, overdue = 0, unpaid = 0;
            foreach (DataRow row in _data.Rows)
            {
                total++;
                string st = row["status"]?.ToString().ToLower() ?? "";
                string pay = row["payment_status"]?.ToString().ToLower() ?? "";
                int rem = row["days_remaining"] != DBNull.Value ? Convert.ToInt32(row["days_remaining"]) : 0;
                if (st == "pending") pending++;
                if (st == "approved" || st == "active") approved++;
                if (rem < 0 && (st == "approved" || st == "active")) overdue++;
                if (pay == "unpaid") unpaid++;
            }
            void Set() => lblStats.Text =
                $"{total} total  ·  {pending} pending  ·  {approved} active  ·  {overdue} overdue  ·  {unpaid} unpaid";
            if (lblStats.InvokeRequired) lblStats.Invoke((Action)Set); else Set();
        }

        // ══ ROW SELECTED ════════════════════════════════════════════════════
        private void OnRowSelected(object sender, EventArgs e)
        {
            if (dgvRentals?.SelectedRows.Count == 0) return;
            var cv = dgvRentals.SelectedRows[0].Cells[0].Value;
            if (cv == null || cv == DBNull.Value) return;

            int id = Convert.ToInt32(cv);
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

            string status = r["status"]?.ToString().ToLower() ?? "unknown";
            string payment = r["payment_status"]?.ToString().ToLower() ?? "unknown";
            string payMethod = r["payment_method"]?.ToString().ToLower() ?? "";
            int rem = r["days_remaining"] != DBNull.Value ? Convert.ToInt32(r["days_remaining"]) : 0;
            int pendExt = r["pending_extensions"] != DBNull.Value ? Convert.ToInt32(r["pending_extensions"]) : 0;
            int openIss = r["open_issues"] != DBNull.Value ? Convert.ToInt32(r["open_issues"]) : 0;
            int unreadMsg = r["unread_messages"] != DBNull.Value ? Convert.ToInt32(r["unread_messages"]) : 0;

            WinColor bannerColor =
                status == "approved" || status == "active" ? ColGreen :
                status == "pending" ? ColYellow :
                status == "completed" ? ColBlue :
                status == "rejected" || status == "cancelled" ? ColRed : ColSub;

            lblStatusBanner.Text =
                $"● {status.ToUpper()}  |  Payment: {payment.ToUpper()}  |  " +
                (rem >= 0 ? $"Return in {rem} day(s)" : $"⚠ OVERDUE by {Math.Abs(rem)} day(s)") +
                $"  |  Ops: {pendExt} ext · {openIss} issue · {unreadMsg} msg";
            lblStatusBanner.ForeColor = bannerColor;
            pnlStatusBanner.Invalidate();

            string sDate = r["start_date"] != DBNull.Value ? Convert.ToDateTime(r["start_date"]).ToString("MMM dd") : "—";
            string eDate = r["end_date"] != DBNull.Value ? Convert.ToDateTime(r["end_date"]).ToString("MMM dd, yyyy") : "—";
            string totalAmt = r["total_amount"] != DBNull.Value ? Convert.ToDecimal(r["total_amount"]).ToString("N2") : "0.00";
            string activity = $"{pendExt} ext pending  ·  {openIss} issues\n{unreadMsg} messages";

            var cards = new[]
            {
                ("👤 Customer",    r["customer_name"]?.ToString() ?? "Unknown",                                    ColBlue),
                ("🚗 Vehicle",     (r["vehicle_name"]?.ToString()??"—") + "\n" + (r["plate_number"]?.ToString()??"—"), ColAccent),
                ("🧑 Driver",      r["driver_name"]?.ToString() ?? "No Driver",                                    ColPurple),
                ("📍 Destination", r["destination"]?.ToString()  ?? "—",                                           ColTeal),
                ("💰 Amount",      $"₱{totalAmt}\n{payMethod.ToUpper()}",                                          ColGreen),
                ("📅 Activity",    $"{sDate} → {eDate}\n{activity}",                                              openIss > 0 ? ColRed : ColYellow)
            };

            int cw = Math.Max(150, (pnlInfoCards.Width - 24) / 3 - 10);
            foreach (var (label, value, color) in cards)
            {
                var card = new Panel
                {
                    Size = new Size(cw, 62),
                    Margin = new Padding(0, 0, 10, 10),
                    BackColor = WinColor.Transparent
                };
                card.Paint += (s, ev) =>
                {
                    var g = ev.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = RoundRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 6);
                    g.FillPath(new SolidBrush(ThemeManager.IsDarkMode
                        ? WinColor.FromArgb(22, 22, 35) : WinColor.White), path);
                    g.FillRectangle(new SolidBrush(color), 0, card.Height - 3, card.Width, 3);
                    g.DrawPath(new Pen(WinColor.FromArgb(25, color), 1), path);
                };
                card.Controls.Add(new Label { Text = label, Font = new Font("Segoe UI", 7F, FontStyle.Bold), ForeColor = color, AutoSize = true, Location = new Point(8, 6), BackColor = WinColor.Transparent });
                card.Controls.Add(new Label { Text = value, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = ColText, AutoSize = false, Size = new Size(cw - 12, 34), Location = new Point(8, 22), BackColor = WinColor.Transparent });
                pnlInfoCards.Controls.Add(card);
            }

            // ── GCash QR banner when payment is GCash + unpaid ────────────
            if (payMethod == "gcash" && payment == "unpaid")
            {
                var qrBanner = new Panel
                {
                    Width = pnlInfoCards.Width,
                    Height = 36,
                    Margin = new Padding(0, 4, 0, 0),
                    BackColor = WinColor.Transparent,
                    Cursor = Cursors.Hand
                };
                qrBanner.Paint += (s, ev) =>
                {
                    ev.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = RoundRect(new Rectangle(0, 0, qrBanner.Width - 1, qrBanner.Height - 1), 6);
                    ev.Graphics.FillPath(new SolidBrush(WinColor.FromArgb(30, WinColor.FromArgb(0, 134, 58))), path);
                    ev.Graphics.DrawPath(new Pen(WinColor.FromArgb(0, 134, 58), 1), path);
                };
                var qrLabel = new Label
                {
                    Text = "📲  View GCash QR Code — tap to show payment QR",
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    ForeColor = WinColor.FromArgb(0, 180, 80),
                    AutoSize = true,
                    Location = new Point(14, 9),
                    BackColor = WinColor.Transparent,
                    Cursor = Cursors.Hand
                };
                qrBanner.Controls.Add(qrLabel);
                qrBanner.Click += (s, e) => ShowGcashQrDialog(r);
                qrLabel.Click += (s, e) => ShowGcashQrDialog(r);
                pnlInfoCards.Controls.Add(qrBanner);
            }
        }

        // ── GCash QR Dialog ───────────────────────────────────────────────
        private void ShowGcashQrDialog(DataRow r)
        {
            string customer = r["customer_name"]?.ToString() ?? "Customer";
            decimal amount = r["total_amount"] != DBNull.Value ? Convert.ToDecimal(r["total_amount"]) : 0;
            int rentalId = r["rental_id"] != DBNull.Value ? Convert.ToInt32(r["rental_id"]) : 0;

            using var dlg = new Form
            {
                Text = "GCash Payment QR",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                Size = new Size(400, 560),
                BackColor = WinColor.FromArgb(18, 18, 28),
                Font = new Font("Segoe UI", 9.5F)
            };

            dlg.Controls.Add(new Label
            {
                Text = "GCash Payment",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = WinColor.FromArgb(0, 200, 90),
                AutoSize = true,
                Location = new Point(125, 18),
                BackColor = WinColor.Transparent
            });

            dlg.Controls.Add(new Label
            {
                Text = $"₱{amount:N2}",
                Font = new Font("Segoe UI", 22F, FontStyle.Bold),
                ForeColor = WinColor.White,
                AutoSize = true,
                Location = new Point(120, 52),
                BackColor = WinColor.Transparent
            });

            dlg.Controls.Add(new Label
            {
                Text = $"Rental #DG-{rentalId:D5}  ·  {customer}",
                Font = new Font("Segoe UI", 9F),
                ForeColor = WinColor.FromArgb(160, 160, 180),
                AutoSize = true,
                Location = new Point(20, 96),
                BackColor = WinColor.Transparent
            });

            var pic = new PictureBox
            {
                Size = new Size(300, 300),
                Location = new Point(48, 120),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = WinColor.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            try
            {
                string qrPath = GcashQrPath;

                if (File.Exists(qrPath))
                {
                    using var fs = new FileStream(qrPath, FileMode.Open, FileAccess.Read);
                    pic.Image = System.Drawing.Image.FromStream(fs);
                }
                else
                {
                    var bmp = new Bitmap(300, 300);
                    using var g = Graphics.FromImage(bmp);
                    g.Clear(WinColor.White);

                    using var fmt = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };

                    g.DrawString(
                        "📲\n\nGCash QR\n\nFile not found\n\nPlace:\nWebAssets\\gcash_qr.png",
                        new Font("Segoe UI", 11F),
                        Brushes.Gray,
                        new RectangleF(0, 0, 300, 300),
                        fmt
                    );

                    pic.Image = bmp;
                }
            }
            catch
            {
                var bmp = new Bitmap(300, 300);
                using var g = Graphics.FromImage(bmp);
                g.Clear(WinColor.White);

                using var fmt = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                g.DrawString(
                    "GCash QR could not be loaded.",
                    new Font("Segoe UI", 11F),
                    Brushes.Gray,
                    new RectangleF(0, 0, 300, 300),
                    fmt
                );

                pic.Image = bmp;
            }

            dlg.Controls.Add(pic);

            dlg.Controls.Add(new Label
            {
                Text = "Ask customer to scan with GCash app",
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = WinColor.FromArgb(140, 140, 160),
                AutoSize = true,
                Location = new Point(68, 430),
                BackColor = WinColor.Transparent
            });

            var btnConfirm = new Button
            {
                Text = "✔  Confirm Payment Received",
                Size = new Size(300, 40),
                Location = new Point(48, 470),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = WinColor.FromArgb(0, 134, 58),
                ForeColor = WinColor.White,
                Cursor = Cursors.Hand
            };
            btnConfirm.FlatAppearance.BorderSize = 0;
            btnConfirm.Click += (s, e) =>
            {
                dlg.DialogResult = DialogResult.OK;
                dlg.Close();
            };
            dlg.Controls.Add(btnConfirm);

            if (dlg.ShowDialog() == DialogResult.OK)
                ConfirmPaymentDirect();
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

            lblContractBody.Text =
                "Agreement No.: DG-" + rentalId.ToString("D5") + "\n" +
                "Date Prepared: " + created + "\n\n" +
                "────────────────────────────────────────────────────────────\n\n" +
                "LESSOR:  Drive & Go Vehicle Rental Services\n" +
                "LESSEE:  " + customer + "\n\n" +
                "────────────────────────────────────────────────────────────\n" +
                "SECTION 1 — VEHICLE DETAILS\n" +
                "────────────────────────────────────────────────────────────\n\n" +
                "   Vehicle         : " + vehicle + "\n" +
                "   Plate Number    : " + plate + "\n" +
                "   Assigned Driver : " + driver + "\n\n" +
                "────────────────────────────────────────────────────────────\n" +
                "SECTION 2 — RENTAL PERIOD & DESTINATION\n" +
                "────────────────────────────────────────────────────────────\n\n" +
                "   Start Date    : " + start + "\n" +
                "   Return Date   : " + end + "\n" +
                "   Destination   : " + dest + "\n\n" +
                "────────────────────────────────────────────────────────────\n" +
                "SECTION 3 — PAYMENT\n" +
                "────────────────────────────────────────────────────────────\n\n" +
                "   Total Amount  : " + amount + "\n" +
                "   Method        : " + method + "\n" +
                "   Payment Status: " + payStatus + "\n" +
                "   Booking Status: " + status + "\n\n" +
                "────────────────────────────────────────────────────────────\n" +
                "SECTION 4 — TERMS & CONDITIONS\n" +
                "────────────────────────────────────────────────────────────\n\n" +
                "1. GPS TRACKING — Vehicle is equipped with GPS tracking.\n" +
                "2. VEHICLE CONDITION — Return in same condition as released.\n" +
                "3. TRAFFIC VIOLATIONS — Customer is liable for all violations.\n" +
                "4. FUEL POLICY — Return with agreed fuel level.\n" +
                "5. EXTENSIONS — Require prior admin approval.\n" +
                "6. RESTRICTED USE — Illegal use is prohibited.\n" +
                "7. CANCELLATION — Last-minute cancellations may be non-refundable.\n\n" +
                "By proceeding, the customer agrees to all stated terms.\n\n\n" +
                "___________________________    ___________________________\n" +
                " Authorized Admin Signature         Customer Signature";

            pnlContract.Invalidate();
        }

        private void UpdateActionButtons(DataRow r)
        {
            string status = r["status"]?.ToString().ToLower() ?? "";
            string payment = r["payment_status"]?.ToString().ToLower() ?? "";
            int pendExt = r["pending_extensions"] != DBNull.Value ? Convert.ToInt32(r["pending_extensions"]) : 0;

            btnApprove.Enabled = status == "pending";
            btnReject.Enabled = status == "pending";
            btnCancel.Enabled = status == "approved" || status == "active" || status == "pending";
            btnComplete.Enabled = status == "approved" || status == "active";
            btnExtensions.Enabled = pendExt > 0;
            btnExtensions.BackColor = pendExt > 0
                ? WinColor.FromArgb(40, ColPurple) : WinColor.FromArgb(20, ColSub);
            btnExtensions.ForeColor = pendExt > 0 ? ColPurple : ColSub;
            btnConfirmPayment.Enabled = payment == "unpaid";
            btnExportPDF.Enabled = true;
        }

        // ══ STATUS ACTIONS ══════════════════════════════════════════════════
        private void UpdateStatus(string newStatus)
        {
            if (_selectedId < 0) return;
            string label = newStatus switch
            {
                "approved" => "Approve",
                "rejected" => "Reject",
                "completed" => "Complete",
                "cancelled" => "Cancel",
                _ => newStatus
            };

            if (MessageBox.Show($"{label} this rental?", "Confirm",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();

                new MySqlCommand($"UPDATE rentals SET status=@s WHERE rental_id=@id", conn)
                    .Also(c => { c.Parameters.AddWithValue("@s", newStatus); c.Parameters.AddWithValue("@id", _selectedId); c.ExecuteNonQuery(); });

                if (_selectedRow?["vehicle_id"] != DBNull.Value)
                {
                    int vid = Convert.ToInt32(_selectedRow["vehicle_id"]);
                    string vs = newStatus == "approved" ? "in-use" : "available";
                    if (newStatus == "approved" || newStatus == "completed" ||
                        newStatus == "rejected" || newStatus == "cancelled")
                        new MySqlCommand("UPDATE vehicles SET status=@s WHERE vehicle_id=@vid", conn)
                            .Also(c => { c.Parameters.AddWithValue("@s", vs); c.Parameters.AddWithValue("@vid", vid); c.ExecuteNonQuery(); });
                }

                MessageBox.Show($"Rental {newStatus} successfully!", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadFromDB();
            }
            catch (Exception ex) { MessageBox.Show("DB Error: " + ex.Message); }
        }

        // ══ CONFIRM PAYMENT ══════════════════════════════════════════════════
        // Called from the "💳 Paid" button
        private void OnConfirmPayment(object s, EventArgs e)
        {
            if (_selectedId < 0 || _selectedRow == null) return;

            // If GCash — show the QR dialog first
            string payMethod = _selectedRow["payment_method"]?.ToString().ToLower() ?? "";
            if (payMethod == "gcash")
            {
                ShowGcashQrDialog(_selectedRow);
                return;
            }

            if (MessageBox.Show("Confirm payment for this rental?", "Confirm Payment",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            ConfirmPaymentDirect();
        }

        // Core payment confirmation — updates rentals + inserts verified transaction
        private void ConfirmPaymentDirect()
        {
            if (_selectedId < 0 || _selectedRow == null) return;
            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();

                // 1. Mark rental as paid
                new MySqlCommand("UPDATE rentals SET payment_status='paid' WHERE rental_id=@id", conn)
                    .Also(c => { c.Parameters.AddWithValue("@id", _selectedId); c.ExecuteNonQuery(); });

                // 2. Insert or update transaction with status = 'verified'
                if (TableExists(conn, "transactions"))
                {
                    // Check if record already exists
                    var check = new MySqlCommand(
                        "SELECT transaction_id FROM transactions WHERE rental_id=@id LIMIT 1", conn);
                    check.Parameters.AddWithValue("@id", _selectedId);
                    var existing = check.ExecuteScalar();

                    if (existing != null)
                    {
                        // Update existing NULL/pending record → verified
                        new MySqlCommand(
                            "UPDATE transactions SET status='verified' WHERE rental_id=@id", conn)
                            .Also(c => { c.Parameters.AddWithValue("@id", _selectedId); c.ExecuteNonQuery(); });
                    }
                    else
                    {
                        // Insert fresh verified transaction
                        decimal amt = _selectedRow["total_amount"] != DBNull.Value
                            ? Convert.ToDecimal(_selectedRow["total_amount"]) : 0;
                        string method = _selectedRow["payment_method"]?.ToString() ?? "cash";

                        var ins = new MySqlCommand(@"
                            INSERT INTO transactions
                                (rental_id, amount, type, method, status, paid_at)
                            VALUES
                                (@rid, @amt, 'payment', @method, 'verified', NOW())", conn);
                        ins.Parameters.AddWithValue("@rid", _selectedId);
                        ins.Parameters.AddWithValue("@amt", amt);
                        ins.Parameters.AddWithValue("@method", method);
                        ins.ExecuteNonQuery();
                    }
                }

                // 3. Show receipt
                ShowPaymentReceipt(_selectedRow);

                LoadFromDB();
            }
            catch (Exception ex) { MessageBox.Show("DB Error: " + ex.Message); }
        }

        // ── Payment receipt dialog ────────────────────────────────────────
        private void ShowPaymentReceipt(DataRow r)
        {
            string customer = r["customer_name"]?.ToString() ?? "Customer";
            string vehicle = r["vehicle_name"]?.ToString() ?? "Vehicle";
            decimal amount = r["total_amount"] != DBNull.Value ? Convert.ToDecimal(r["total_amount"]) : 0;
            string method = r["payment_method"]?.ToString().ToUpper() ?? "—";
            int rentalId = r["rental_id"] != DBNull.Value ? Convert.ToInt32(r["rental_id"]) : 0;

            using var dlg = new Form
            {
                Text = "Payment Receipt",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                Size = new Size(380, 340),
                BackColor = WinColor.FromArgb(18, 18, 28),
                Font = new Font("Segoe UI", 9.5F)
            };

            string receipt =
                $"  ✅  PAYMENT CONFIRMED\n\n" +
                $"  Agreement : DG-{rentalId:D5}\n" +
                $"  Customer  : {customer}\n" +
                $"  Vehicle   : {vehicle}\n" +
                $"  Amount    : ₱{amount:N2}\n" +
                $"  Method    : {method}\n" +
                $"  Date/Time : {DateTime.Now:MMM dd, yyyy hh:mm tt}\n" +
                $"  Status    : VERIFIED ✓\n\n" +
                $"  Thank you for your payment!";

            dlg.Controls.Add(new Label
            {
                Text = receipt,
                Font = new Font("Consolas", 10F),
                ForeColor = WinColor.FromArgb(0, 220, 100),
                AutoSize = false,
                Size = new Size(340, 220),
                Location = new Point(16, 16),
                BackColor = WinColor.Transparent
            });

            var btnOk = new Button
            {
                Text = "✔  OK",
                Size = new Size(150, 38),
                Location = new Point(100, 258),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = WinColor.FromArgb(0, 134, 58),
                ForeColor = WinColor.White,
                Cursor = Cursors.Hand
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) => dlg.Close();
            dlg.Controls.Add(btnOk);
            dlg.ShowDialog();
        }

        // ══ EXTENSION MANAGEMENT ════════════════════════════════════════════
        private void OnManageExtensions(object s, EventArgs e)
        {
            if (_selectedId < 0) return;

            DataTable ext = new DataTable();
            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();
                var cmd = new MySqlCommand(@"
                    SELECT extension_id, added_days, added_fee, status, requested_at
                    FROM extensions
                    WHERE rental_id = @rid
                    ORDER BY requested_at DESC", conn);
                cmd.Parameters.AddWithValue("@rid", _selectedId);
                new MySqlDataAdapter(cmd).Fill(ext);
            }
            catch (Exception ex) { MessageBox.Show("DB Error: " + ex.Message); return; }

            if (ext.Rows.Count == 0)
            { MessageBox.Show("No extension requests for this rental.", "Extensions", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

            using var dlg = new Form
            {
                Text = $"Extensions — Rental #DG-{_selectedId:D5}",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                Size = new Size(560, 400),
                BackColor = WinColor.FromArgb(18, 18, 28),
                Font = new Font("Segoe UI", 9.5F)
            };

            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = WinColor.FromArgb(18, 18, 28),
                ForeColor = WinColor.Gainsboro,
                Font = new Font("Segoe UI", 9F),
                RowTemplate = { Height = 36 }
            };
            dgv.DefaultCellStyle.BackColor = WinColor.FromArgb(26, 26, 38);
            dgv.DefaultCellStyle.ForeColor = WinColor.Gainsboro;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = WinColor.FromArgb(10, 10, 20);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = WinColor.Silver;

            var display = new DataTable();
            display.Columns.Add("ID", typeof(int));
            display.Columns.Add("Added Days", typeof(int));
            display.Columns.Add("Added Fee", typeof(string));
            display.Columns.Add("Status", typeof(string));
            display.Columns.Add("Requested At", typeof(string));

            foreach (DataRow row in ext.Rows)
                display.Rows.Add(
                    row["extension_id"],
                    row["added_days"],
                    "₱" + (row["added_fee"] != DBNull.Value ? Convert.ToDecimal(row["added_fee"]).ToString("N2") : "0.00"),
                    row["status"]?.ToString() ?? "—",
                    row["requested_at"] != DBNull.Value
                        ? Convert.ToDateTime(row["requested_at"]).ToString("MMM dd, yyyy HH:mm") : "—");

            dgv.DataSource = display;

            var btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = WinColor.FromArgb(12, 12, 22) };

            var btnApproveExt = new Button
            {
                Text = "✔ Approve Selected",
                Size = new Size(160, 36),
                Location = new Point(12, 8),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = WinColor.FromArgb(20, ColGreen),
                ForeColor = ColGreen,
                Cursor = Cursors.Hand
            };
            btnApproveExt.FlatAppearance.BorderColor = ColGreen;
            btnApproveExt.FlatAppearance.BorderSize = 1;
            btnApproveExt.Click += (bs, be) =>
            {
                if (dgv.SelectedRows.Count == 0) return;
                int extId = Convert.ToInt32(dgv.SelectedRows[0].Cells[0].Value);
                UpdateExtensionStatus(extId, "approved");
                dlg.Close(); LoadFromDB();
            };

            var btnRejectExt = new Button
            {
                Text = "✖ Reject Selected",
                Size = new Size(150, 36),
                Location = new Point(180, 8),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = WinColor.FromArgb(20, ColRed),
                ForeColor = ColRed,
                Cursor = Cursors.Hand
            };
            btnRejectExt.FlatAppearance.BorderColor = ColRed;
            btnRejectExt.FlatAppearance.BorderSize = 1;
            btnRejectExt.Click += (bs, be) =>
            {
                if (dgv.SelectedRows.Count == 0) return;
                int extId = Convert.ToInt32(dgv.SelectedRows[0].Cells[0].Value);
                UpdateExtensionStatus(extId, "rejected");
                dlg.Close(); LoadFromDB();
            };

            btnPanel.Controls.AddRange(new Control[] { btnApproveExt, btnRejectExt });
            dlg.Controls.Add(dgv);
            dlg.Controls.Add(btnPanel);
            dlg.ShowDialog();
        }

        private void UpdateExtensionStatus(int extensionId, string newStatus)
        {
            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();
                var cmd = new MySqlCommand(
                    "UPDATE extensions SET status=@s WHERE extension_id=@id", conn);
                cmd.Parameters.AddWithValue("@s", newStatus);
                cmd.Parameters.AddWithValue("@id", extensionId);
                cmd.ExecuteNonQuery();

                // If approved → extend the rental end_date
                if (newStatus == "approved")
                {
                    var getExt = new MySqlCommand(
                        "SELECT rental_id, added_days FROM extensions WHERE extension_id=@id", conn);
                    getExt.Parameters.AddWithValue("@id", extensionId);
                    using var reader = getExt.ExecuteReader();
                    if (reader.Read())
                    {
                        int rentalId = reader.GetInt32("rental_id");
                        int addedDays = reader.GetInt32("added_days");
                        reader.Close();

                        var updRental = new MySqlCommand(
                            "UPDATE rentals SET end_date = DATE_ADD(end_date, INTERVAL @days DAY) WHERE rental_id=@rid",
                            conn);
                        updRental.Parameters.AddWithValue("@days", addedDays);
                        updRental.Parameters.AddWithValue("@rid", rentalId);
                        updRental.ExecuteNonQuery();
                    }
                }
                MessageBox.Show($"Extension {newStatus}!", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show("DB Error: " + ex.Message); }
        }

        // ══ WALK-IN RENTAL ══════════════════════════════════════════════════
        private void OnWalkInRental(object sender, EventArgs e)
        {
            using var dlg = new WalkInRentalForm(_connStr);
            if (dlg.ShowDialog() == DialogResult.OK) LoadFromDB();
        }

        // ══ PDF EXPORT ══════════════════════════════════════════════════════
        private void OnExportPDF(object s, EventArgs e)
        {
            if (_selectedId < 0 || _selectedRow == null)
            { MessageBox.Show("Select a rental first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

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
                Directory.CreateDirectory(Path.GetDirectoryName(dlg.FileName) ?? ".");

                using var writer = new PdfWriter(dlg.FileName, new WriterProperties().SetCompressionLevel(9));
                using var pdf = new PdfDocument(writer);
                using var doc = new Document(pdf);

                PdfFont fontBold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                PdfFont fontNormal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

                var orange = new iText.Kernel.Colors.DeviceRgb(230, 81, 0);
                var dark = new iText.Kernel.Colors.DeviceRgb(20, 20, 40);
                var gray = new iText.Kernel.Colors.DeviceRgb(100, 100, 130);
                var white = iText.Kernel.Colors.ColorConstants.WHITE;
                var lightBg = new iText.Kernel.Colors.DeviceRgb(248, 248, 252);

                // Header table
                var headerTable = new iText.Layout.Element.Table(new float[] { 1, 4 }).UseAllAvailableWidth();
                string logoPath = ExportBrandingHelper.ResolveLogoPath();

                var logoCell = new Cell()
                    .SetBackgroundColor(new iText.Kernel.Colors.DeviceRgb(255, 241, 232))
                    .SetPadding(12).SetBorder(iText.Layout.Borders.Border.NO_BORDER)
                    .SetVerticalAlignment(VerticalAlignment.MIDDLE);
                if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
                    logoCell.Add(new iText.Layout.Element.Image(ImageDataFactory.Create(logoPath)).SetHeight(44).SetAutoScale(true));
                else
                    logoCell.Add(new Paragraph("DG").SetFontColor(orange).SetFontSize(22).SetFont(fontBold));

                var headerCell = new Cell()
                    .SetBackgroundColor(orange).SetPadding(16).SetBorder(iText.Layout.Borders.Border.NO_BORDER);
                headerCell.Add(new Paragraph("DRIVE & GO").SetFontColor(white).SetFontSize(24).SetFont(fontBold));
                headerCell.Add(new Paragraph("Vehicle Rental Services")
                    .SetFontColor(new iText.Kernel.Colors.DeviceRgb(255, 200, 160)).SetFontSize(12).SetFont(fontNormal));

                headerTable.AddCell(logoCell);
                headerTable.AddCell(headerCell);
                doc.Add(headerTable);
                doc.Add(new Paragraph(" "));

                doc.Add(new Paragraph("VEHICLE RENTAL AGREEMENT")
                    .SetFontSize(16).SetFont(fontBold).SetFontColor(dark).SetTextAlignment(TextAlignment.CENTER));

                DateTime createdAt = r["created_at"] != DBNull.Value ? Convert.ToDateTime(r["created_at"]) : DateTime.Now;
                doc.Add(new Paragraph($"Agreement No.: DG-{_selectedId:D5}  |  Date: {createdAt:MMMM dd, yyyy}")
                    .SetFontSize(9).SetFontColor(gray).SetFont(fontNormal).SetTextAlignment(TextAlignment.CENTER));
                doc.Add(new Paragraph(" "));

                // Info table
                var infoTable = new iText.Layout.Element.Table(new float[] { 1, 1 }).UseAllAvailableWidth().SetMarginBottom(12);
                void AddRow(string lbl, string val)
                {
                    infoTable.AddCell(new Cell().SetBackgroundColor(lightBg).SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPadding(8)
                        .Add(new Paragraph(lbl).SetFontSize(9).SetFontColor(gray).SetFont(fontNormal)));
                    infoTable.AddCell(new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPadding(8)
                        .Add(new Paragraph(val).SetFontSize(10).SetFont(fontBold).SetFontColor(dark)));
                }

                AddRow("Customer Name", r["customer_name"]?.ToString() ?? "Unknown");
                AddRow("Vehicle", (r["vehicle_name"]?.ToString() ?? "—") + " — " + (r["plate_number"]?.ToString() ?? "—"));
                AddRow("Assigned Driver", r["driver_name"]?.ToString() ?? "No Driver");
                AddRow("Destination", r["destination"]?.ToString() ?? "Not specified");
                AddRow("Rental Period",
                    (r["start_date"] != DBNull.Value ? Convert.ToDateTime(r["start_date"]).ToString("MMM dd, yyyy") : "—") +
                    " to " +
                    (r["end_date"] != DBNull.Value ? Convert.ToDateTime(r["end_date"]).ToString("MMM dd, yyyy") : "—"));
                AddRow("Total Amount", "PHP " + (r["total_amount"] != DBNull.Value ? Convert.ToDecimal(r["total_amount"]).ToString("N2") : "0.00"));
                AddRow("Payment Status", (r["payment_status"]?.ToString().ToUpper() ?? "—") + " (" + (r["payment_method"]?.ToString().ToUpper() ?? "—") + ")");
                AddRow("Booking Status", r["status"]?.ToString().ToUpper() ?? "—");
                doc.Add(infoTable);

                doc.Add(new Paragraph("TERMS & CONDITIONS").SetFontSize(11).SetFont(fontBold).SetFontColor(orange));
                var terms = new[]
                {
                    "GPS TRACKING — Vehicle is equipped with live GPS tracking.",
                    "VEHICLE CONDITION — Return in same condition as released.",
                    "TRAFFIC VIOLATIONS — Customer assumes full responsibility.",
                    "FUEL POLICY — Return with agreed fuel level.",
                    "RENTAL EXTENSIONS — Require prior admin approval.",
                    "RESTRICTED USAGE — Illegal use is strictly prohibited.",
                    "CANCELLATION POLICY — Last-minute cancellations may be non-refundable."
                };
                foreach (var t in terms)
                    doc.Add(new Paragraph("• " + t).SetFontSize(9f).SetFontColor(dark).SetMarginBottom(6).SetFont(fontNormal));

                doc.Add(new Paragraph("By proceeding, the customer agrees to all stated terms.")
                    .SetFontSize(9f).SetFontColor(dark).SetMarginTop(10).SetFont(fontNormal));
                doc.Add(new Paragraph(" ").SetMarginTop(20));

                var sigTable = new iText.Layout.Element.Table(new float[] { 1, 1 }).UseAllAvailableWidth();
                sigTable.AddCell(new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPaddingTop(30)
                    .Add(new Paragraph("____________________________\nAuthorized Admin Signature")
                        .SetFontSize(9).SetFontColor(gray).SetFont(fontNormal)));
                sigTable.AddCell(new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPaddingTop(30)
                    .Add(new Paragraph("____________________________\nCustomer Signature / App Confirmation")
                        .SetFontSize(9).SetFontColor(gray).SetTextAlignment(TextAlignment.RIGHT).SetFont(fontNormal)));
                doc.Add(sigTable);

                doc.Add(new Paragraph("Drive & Go Vehicle Rental Services  |  Generated: " + DateTime.Now.ToString("MMM dd, yyyy hh:mm tt"))
                    .SetFontSize(8).SetFontColor(gray).SetTextAlignment(TextAlignment.CENTER).SetMarginTop(20).SetFont(fontNormal));

                MessageBox.Show("PDF contract saved!\n" + dlg.FileName, "Export Complete",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = dlg.FileName, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show("PDF Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // ══ PAINT ═══════════════════════════════════════════════════════════
        private void OnContractPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(new SolidBrush(ColAccent), 0, 0, pnlContract.Width, 5);
            g.DrawRectangle(new Pen(WinColor.FromArgb(40, 0, 0, 0), 1), 0, 0, pnlContract.Width - 1, pnlContract.Height - 1);
        }

        private void OnCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.Value == null || e.Value == DBNull.Value) return;

            if (e.ColumnIndex == 6) // Status
            {
                e.Handled = true; e.PaintBackground(e.ClipBounds, true);
                string val = e.Value.ToString().ToLower();
                WinColor c = val == "approved" ? ColGreen :
                             val == "pending" ? ColYellow :
                             val == "completed" ? ColBlue :
                             val == "cancelled" ? ColOrange :
                             val == "rejected" ? ColRed : ColSub;
                DrawBadge(e.Graphics, e.CellBounds, val, c);
            }
            else if (e.ColumnIndex == 5) // Payment
            {
                e.Handled = true; e.PaintBackground(e.ClipBounds, true);
                string val = e.Value.ToString();
                DrawBadge(e.Graphics, e.CellBounds, val, val.Contains("Paid") ? ColGreen : ColYellow);
            }
            else if (e.ColumnIndex == 7) // Return
            {
                e.Handled = true; e.PaintBackground(e.ClipBounds, true);
                string val = e.Value?.ToString() ?? "";
                WinColor c = val.Contains("OVERDUE") || val.Contains("TODAY") ? ColRed
                           : val.Contains("⏰") ? ColYellow : ColSub;
                TextRenderer.DrawText(e.Graphics, val, new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    e.CellBounds, c, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            }
        }

        private void DrawBadge(Graphics g, Rectangle bounds, string text, WinColor c)
        {
            var rect = new Rectangle(bounds.X + 6, bounds.Y + 9, bounds.Width - 12, bounds.Height - 18);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundRect(rect, 8);
            g.FillPath(new SolidBrush(WinColor.FromArgb(30, c)), path);
            g.DrawPath(new Pen(c, 1), path);
            TextRenderer.DrawText(g, text, new Font("Segoe UI", 8F, FontStyle.Bold), rect, c,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
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
            try { using var c = new MySqlCommand($"SHOW TABLES LIKE '{t}'", conn); return c.ExecuteScalar() != null; }
            catch { return false; }
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
                BackColor = WinColor.FromArgb(20, color),
                ForeColor = color
            };
            btn.FlatAppearance.BorderColor = color;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = WinColor.FromArgb(45, color);
            return btn;
        }

        private GraphicsPath RoundRect(Rectangle b, int r)
        {
            int d = r * 2;
            var arc = new Rectangle(b.Location, new Size(d, d));
            var path = new GraphicsPath();
            path.AddArc(arc, 180, 90); arc.X = b.Right - d;
            path.AddArc(arc, 270, 90); arc.Y = b.Bottom - d;
            path.AddArc(arc, 0, 90); arc.X = b.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }

        // ══ WALK-IN RENTAL FORM ══════════════════════════════════════════════
        public class WalkInRentalForm : Form
        {
            private readonly string _connStr;

            private TextBox txtCustomerName, txtEmail, txtPhone, txtDestination;
            private ComboBox cboVehicle, cboDriver, cboPaymentMethod;
            private DateTimePicker dtStart, dtEnd;
            private CheckBox chkWithDriver;
            private Label lblAmount;
            private Button btnSave;

            private DataTable _vehicles = new DataTable();
            private DataTable _drivers = new DataTable();

            public WalkInRentalForm(string connStr) { _connStr = connStr; BuildUI(); LoadLookupData(); ComputeAmount(); }

            private void BuildUI()
            {
                Text = "Walk-In Rental Form";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false; MinimizeBox = false;
                Size = new Size(560, 620);
                BackColor = Color.FromArgb(18, 18, 28);
                Font = new Font("Segoe UI", 9.5F);

                int lx = 24, vx = 180, y = 24, w = 320;

                Controls.Add(MakeLabel("Customer Name:", lx, y + 6)); txtCustomerName = MakeText(vx, y, w); Controls.Add(txtCustomerName); y += 42;
                Controls.Add(MakeLabel("Email:", lx, y + 6)); txtEmail = MakeText(vx, y, w); Controls.Add(txtEmail); y += 42;
                Controls.Add(MakeLabel("Phone:", lx, y + 6)); txtPhone = MakeText(vx, y, w); Controls.Add(txtPhone); y += 42;
                Controls.Add(MakeLabel("Vehicle:", lx, y + 6)); cboVehicle = MakeCombo(vx, y, w); Controls.Add(cboVehicle); y += 42;

                chkWithDriver = new CheckBox { Text = "With Driver", Location = new Point(vx, y), ForeColor = Color.White, BackColor = Color.Transparent, AutoSize = true };
                chkWithDriver.CheckedChanged += (s, e) => { cboDriver.Enabled = chkWithDriver.Checked; ComputeAmount(); };
                Controls.Add(chkWithDriver); y += 30;

                Controls.Add(MakeLabel("Driver:", lx, y + 6)); cboDriver = MakeCombo(vx, y, w); cboDriver.Enabled = false; Controls.Add(cboDriver); y += 42;

                Controls.Add(MakeLabel("Start Date:", lx, y + 6));
                dtStart = new DateTimePicker { Location = new Point(vx, y), Size = new Size(w, 30), Format = DateTimePickerFormat.Short };
                dtStart.ValueChanged += (s, e) => ComputeAmount(); Controls.Add(dtStart); y += 42;

                Controls.Add(MakeLabel("End Date:", lx, y + 6));
                dtEnd = new DateTimePicker { Location = new Point(vx, y), Size = new Size(w, 30), Format = DateTimePickerFormat.Short };
                dtEnd.ValueChanged += (s, e) => ComputeAmount(); Controls.Add(dtEnd); y += 42;

                Controls.Add(MakeLabel("Destination:", lx, y + 6)); txtDestination = MakeText(vx, y, w); Controls.Add(txtDestination); y += 42;

                Controls.Add(MakeLabel("Payment Method:", lx, y + 6));
                cboPaymentMethod = MakeCombo(vx, y, w);
                cboPaymentMethod.Items.AddRange(new object[] { "cash", "gcash", "maya", "bank_transfer" });
                cboPaymentMethod.SelectedIndex = 0; Controls.Add(cboPaymentMethod); y += 42;

                Controls.Add(MakeLabel("Total Amount:", lx, y + 6));
                lblAmount = new Label { Location = new Point(vx, y), Size = new Size(w, 30), Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.Orange, BackColor = Color.Transparent, Text = "₱0.00" };
                Controls.Add(lblAmount); y += 60;

                btnSave = new Button { Text = "Save Walk-In Rental", Size = new Size(180, 40), Location = new Point((Width - 180) / 2 - 8, y), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(230, 81, 0), ForeColor = Color.White };
                btnSave.Click += OnSave; Controls.Add(btnSave);
            }

            private Label MakeLabel(string t, int x, int y) => new Label { Text = t, Location = new Point(x, y), AutoSize = true, ForeColor = Color.Gainsboro, BackColor = Color.Transparent };
            private TextBox MakeText(int x, int y, int w) => new TextBox { Location = new Point(x, y), Size = new Size(w, 30), BackColor = Color.FromArgb(28, 28, 40), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            private ComboBox MakeCombo(int x, int y, int w) => new ComboBox { Location = new Point(x, y), Size = new Size(w, 30), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(28, 28, 40), ForeColor = Color.White };

            private void LoadLookupData()
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();

                using (var da = new MySqlDataAdapter(@"
                    SELECT vehicle_id, CONCAT(brand,' ',model,' [',plate_no,']') AS display_name,
                           rate_per_day, rate_with_driver
                    FROM vehicles WHERE LOWER(status)='available' ORDER BY brand,model", conn))
                    da.Fill(_vehicles);

                cboVehicle.DataSource = _vehicles;
                cboVehicle.DisplayMember = "display_name";
                cboVehicle.ValueMember = "vehicle_id";
                cboVehicle.SelectedIndexChanged += (s, e) => ComputeAmount();

                using (var da = new MySqlDataAdapter(@"
                    SELECT d.driver_id, u.full_name FROM drivers d
                    JOIN users u ON d.user_id=u.user_id
                    WHERE LOWER(COALESCE(d.status,'active'))='active'
                    ORDER BY u.full_name", conn))
                    da.Fill(_drivers);

                cboDriver.DataSource = _drivers;
                cboDriver.DisplayMember = "full_name";
                cboDriver.ValueMember = "driver_id";
            }

            private void ComputeAmount()
            {
                if (_vehicles.Rows.Count == 0 || cboVehicle.SelectedIndex < 0) { lblAmount.Text = "₱0.00"; return; }
                var row = ((DataRowView)cboVehicle.SelectedItem).Row;
                decimal baseRate = row["rate_per_day"] != DBNull.Value ? Convert.ToDecimal(row["rate_per_day"]) : 0;
                decimal driverRate = row["rate_with_driver"] != DBNull.Value ? Convert.ToDecimal(row["rate_with_driver"]) : baseRate;
                int days = Math.Max(1, (dtEnd.Value.Date - dtStart.Value.Date).Days + 1);
                lblAmount.Text = "₱" + (days * (chkWithDriver.Checked ? driverRate : baseRate)).ToString("N2");
            }

            private void OnSave(object sender, EventArgs e)
            {
                if (string.IsNullOrWhiteSpace(txtCustomerName.Text) || string.IsNullOrWhiteSpace(txtPhone.Text) || cboVehicle.SelectedIndex < 0)
                { MessageBox.Show("Customer name, phone, and vehicle are required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (dtEnd.Value.Date < dtStart.Value.Date)
                { MessageBox.Show("End date must not be earlier than start date.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

                using var conn = new MySqlConnection(_connStr);
                conn.Open();
                using var tx = conn.BeginTransaction();
                try
                {
                    // Find or create customer
                    int customerId;
                    var chk = new MySqlCommand("SELECT user_id FROM users WHERE full_name=@n AND phone=@p LIMIT 1", conn, tx);
                    chk.Parameters.AddWithValue("@n", txtCustomerName.Text.Trim());
                    chk.Parameters.AddWithValue("@p", txtPhone.Text.Trim());
                    var existing = chk.ExecuteScalar();

                    if (existing != null)
                        customerId = Convert.ToInt32(existing);
                    else
                    {
                        var ins = new MySqlCommand(@"
                            INSERT INTO users(full_name,email,password_hash,phone,role,created_at)
                            VALUES(@n,@e,@h,@p,'customer',NOW()); SELECT LAST_INSERT_ID();", conn, tx);
                        ins.Parameters.AddWithValue("@n", txtCustomerName.Text.Trim());
                        ins.Parameters.AddWithValue("@e", string.IsNullOrWhiteSpace(txtEmail.Text) ? DBNull.Value : (object)txtEmail.Text.Trim());
                        ins.Parameters.AddWithValue("@h", "walkin-no-login");
                        ins.Parameters.AddWithValue("@p", txtPhone.Text.Trim());
                        customerId = Convert.ToInt32(ins.ExecuteScalar());
                    }

                    var vRow = ((DataRowView)cboVehicle.SelectedItem).Row;
                    int vehicleId = Convert.ToInt32(vRow["vehicle_id"]);
                    int? driverId = chkWithDriver.Checked && cboDriver.SelectedIndex >= 0
                        ? Convert.ToInt32(cboDriver.SelectedValue) : (int?)null;
                    decimal total = decimal.Parse(lblAmount.Text.Replace("₱", "").Replace(",", ""));

                    var insR = new MySqlCommand(@"
                        INSERT INTO rentals(customer_id,vehicle_id,driver_id,start_date,end_date,
                            destination,status,total_amount,payment_method,payment_status,qr_code,created_at)
                        VALUES(@cid,@vid,@did,@sd,@ed,@dest,'approved',@amt,@pm,'unpaid',NULL,NOW())", conn, tx);
                    insR.Parameters.AddWithValue("@cid", customerId);
                    insR.Parameters.AddWithValue("@vid", vehicleId);
                    insR.Parameters.AddWithValue("@did", driverId.HasValue ? (object)driverId.Value : DBNull.Value);
                    insR.Parameters.AddWithValue("@sd", dtStart.Value.Date);
                    insR.Parameters.AddWithValue("@ed", dtEnd.Value.Date);
                    insR.Parameters.AddWithValue("@dest", string.IsNullOrWhiteSpace(txtDestination.Text) ? DBNull.Value : (object)txtDestination.Text.Trim());
                    insR.Parameters.AddWithValue("@amt", total);
                    insR.Parameters.AddWithValue("@pm", cboPaymentMethod.SelectedItem?.ToString() ?? "cash");
                    insR.ExecuteNonQuery();

                    new MySqlCommand("UPDATE vehicles SET status='in-use' WHERE vehicle_id=@vid", conn, tx)
                        .Also(c => { c.Parameters.AddWithValue("@vid", vehicleId); c.ExecuteNonQuery(); });

                    tx.Commit();
                    MessageBox.Show("Walk-in rental saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (Exception ex) { tx.Rollback(); MessageBox.Show("Save error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
    }

    // ── Extension method helper ───────────────────────────────────────────
    internal static class MySqlCommandExtensions
    {
        public static MySqlCommand Also(this MySqlCommand cmd, Action<MySqlCommand> configure)
        { configure(cmd); return cmd; }
    }
}