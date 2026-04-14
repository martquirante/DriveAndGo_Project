#nullable disable
using DriveAndGo_Admin.Helpers;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

// iText7 PDF Libraries
using iText.Kernel.Colors;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Draw;
using iText.Layout;
using iText.Layout.Borders;
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
    public class TransactionsPanel : UserControl
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

        private readonly string _connStr = "Server=localhost;Database=vehicle_rental_db;Uid=root;Pwd=;";

        // ── UI ──
        private SplitContainer splitContainer;
        private Panel topBar;
        private DataGridView dgvTransactions;

        // Receipt Viewer (Right Panel)
        private Panel rightPanel;
        private Panel pnlReceipt;
        private Label lblReceiptContent;

        private TextBox txtSearch;
        private ComboBox cboMethod, cboStatus;
        private Label lblStats;

        private Button btnExportPDF, btnReconcile;

        // ── State ──
        private DataTable _data = new DataTable();
        private int _selectedTxId = -1;
        private DataRow _selectedRow = null;

        public TransactionsPanel()
        {
            this.Dock = DockStyle.Fill;
            this.DoubleBuffered = true;
            this.BackColor = ColBg;
            ThemeManager.ThemeChanged += OnThemeChanged;

            BuildUI();

            // Load DB after UI is built
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
            splitContainer.SplitterMoved += (s, e) => { dgvTransactions?.Invalidate(); };

            BuildLeftPanel();
            BuildRightPanel();

            this.Controls.Add(splitContainer);
        }

        // ── LEFT PANEL (Table & Controls) ──
        private void BuildLeftPanel()
        {
            // Pinalaki natin yung height para di mag-overlap
            topBar = new Panel { Dock = DockStyle.Top, Height = 110, BackColor = WinColor.Transparent, Padding = new Padding(16, 12, 16, 8) };

            var lblTitle = new Label { Text = "Transaction History", Font = new Font("Segoe UI", 18F, FontStyle.Bold), ForeColor = ColText, AutoSize = true, Location = new Point(16, 12), BackColor = WinColor.Transparent };

            // Inusog natin sa kanan ng Title ang Stats para malinis
            lblStats = new Label { Text = "Loading...", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = ColSub, AutoSize = true, Location = new Point(300, 22), BackColor = WinColor.Transparent };

            txtSearch = new TextBox { Size = new Size(200, 30), Location = new Point(16, 60), Font = new Font("Segoe UI", 10F), BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White, ForeColor = ColText, BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "🔍 Search customer..." };
            txtSearch.TextChanged += (s, e) => FilterGrid();

            cboMethod = new ComboBox { Size = new Size(130, 30), Location = new Point(226, 60), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White, ForeColor = ColText };
            cboMethod.Items.AddRange(new object[] { "All Methods", "cash", "gcash", "maya", "bank", "card" });
            cboMethod.SelectedIndex = 0;
            cboMethod.SelectedIndexChanged += (s, e) => FilterGrid();

            cboStatus = new ComboBox { Size = new Size(130, 30), Location = new Point(366, 60), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White, ForeColor = ColText };
            cboStatus.Items.AddRange(new object[] { "All Status", "confirmed", "paid", "verified", "pending", "rejected", "refunded" });
            cboStatus.SelectedIndex = 0;
            cboStatus.SelectedIndexChanged += (s, e) => FilterGrid();

            var btnRefresh = CreateBtn("⟳ Reload", ColSub, 506, 58, 90);
            btnRefresh.Click += (s, e) => LoadFromDB();
            btnReconcile = CreateBtn("🛠 Repair Logs", ColAccent, 602, 58, 120);
            btnReconcile.Click += (s, e) => ReconcilePaymentLogs();

            topBar.Controls.Add(lblTitle);
            topBar.Controls.Add(lblStats);
            topBar.Controls.Add(txtSearch);
            topBar.Controls.Add(cboMethod);
            topBar.Controls.Add(cboStatus);
            topBar.Controls.Add(btnRefresh);
            topBar.Controls.Add(btnReconcile);

            dgvTransactions = new DataGridView { Dock = DockStyle.Fill };
            StyleGrid(dgvTransactions);
            dgvTransactions.SelectionChanged += OnRowSelected;
            dgvTransactions.CellPainting += OnCellPainting;

            splitContainer.Panel1.Controls.Add(dgvTransactions);
            splitContainer.Panel1.Controls.Add(topBar);
        }

        // ── RIGHT PANEL (Receipt Viewer) ──
        private void BuildRightPanel()
        {
            rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(12, 12, 20) : WinColor.FromArgb(240, 241, 248), Padding = new Padding(20) };

            // Receipt Paper UI
            pnlReceipt = new Panel
            {
                BackColor = WinColor.White,
                Location = new Point(20, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Height = rightPanel.Height - 100
            };

            pnlReceipt.Paint += (s, e) => {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillRectangle(new SolidBrush(pnlReceipt.BackColor), 0, 0, pnlReceipt.Width, pnlReceipt.Height);
                g.DrawRectangle(new Pen(ColBorder, 1), 0, 0, pnlReceipt.Width - 1, pnlReceipt.Height - 1);

                // Orange Top stripe
                g.FillRectangle(new SolidBrush(ColAccent), 0, 0, pnlReceipt.Width, 6);
            };

            lblReceiptContent = new Label
            {
                Text = "\n\n\nSelect a transaction to view receipt details.",
                Font = new Font("Consolas", 11F),
                ForeColor = WinColor.FromArgb(40, 40, 40),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopCenter,
                BackColor = WinColor.Transparent
            };
            pnlReceipt.Controls.Add(lblReceiptContent);

            // Action Buttons Panel
            Panel pnlActions = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = WinColor.Transparent };
            btnExportPDF = CreateBtn("📄 Download PDF Receipt", ColBlue, 0, 12, 200);
            btnExportPDF.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnExportPDF.Location = new Point(rightPanel.Width - 220, 12);
            btnExportPDF.Enabled = false;
            btnExportPDF.Click += OnExportPDF;

            pnlActions.Controls.Add(btnExportPDF);

            rightPanel.Controls.Add(pnlReceipt);
            rightPanel.Controls.Add(pnlActions);

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
            cboMethod.BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White;
            cboMethod.ForeColor = ColText;
            cboStatus.BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White;
            cboStatus.ForeColor = ColText;

            foreach (Control c in topBar.Controls) { if (c is Label l) l.ForeColor = ColText; }

            StyleGrid(dgvTransactions);
            this.Invalidate(true);
        }

        // ══ PURE DATABASE LOAD ══
        private void LoadFromDB()
        {
            try
            {
                AdminDataHelper.ReconcilePaidRentalTransactions(_connStr);

                _data = new DataTable();
                using var conn = new MySqlConnection(_connStr);
                conn.Open();

                // Ginamit natin ang LEFT JOIN para sure na may lilitaw na data kahit kulang
                var cmd = new MySqlCommand(@"
                    SELECT 
                        t.transaction_id, 
                        t.rental_id, 
                        COALESCE(u.full_name, 'Unknown Customer') AS customer_name,
                        COALESCE(v.plate_no, 'Unknown Vehicle') AS plate_no,
                        t.amount, 
                        t.type, 
                        t.method, 
                        t.proof_url,
                        t.status, 
                        t.paid_at,
                        r.payment_status
                    FROM transactions t
                    LEFT JOIN rentals r ON t.rental_id = r.rental_id
                    LEFT JOIN users u ON r.customer_id = u.user_id
                    LEFT JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                    WHERE LOWER(COALESCE(t.status, '')) <> 'duplicate'
                    ORDER BY t.paid_at DESC", conn);

                using var adapter = new MySqlDataAdapter(cmd);
                adapter.Fill(_data);
                _data = DeduplicateTransactions(_data);

                RefreshGrid(_data);
                UpdateStats();
            }
            catch (Exception ex)
            {
                RefreshGrid(new DataTable());
                MessageBox.Show($"Could not load transactions.\n{ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RefreshGrid(DataTable dt)
        {
            if (dgvTransactions == null) return;
            if (dgvTransactions.InvokeRequired) { dgvTransactions.Invoke(new Action(() => RefreshGrid(dt))); return; }

            dgvTransactions.DataSource = null;
            dgvTransactions.Columns.Clear();

            var display = new DataTable();
            display.Columns.Add("Tx ID", typeof(string));
            display.Columns.Add("Rental ID", typeof(string));
            display.Columns.Add("Customer Name", typeof(string));
            display.Columns.Add("Amount", typeof(string));
            display.Columns.Add("Method", typeof(string));
            display.Columns.Add("Status", typeof(string));
            display.Columns.Add("Date", typeof(string));

            if (dt != null && dt.Rows.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    decimal amount = row["amount"] != DBNull.Value ? Convert.ToDecimal(row["amount"]) : 0;
                    DateTime date = row["paid_at"] != DBNull.Value ? Convert.ToDateTime(row["paid_at"]) : DateTime.Now;

                    display.Rows.Add(
                        $"TX-{row["transaction_id"]:D5}",
                        $"DG-{row["rental_id"]:D5}",
                        row["customer_name"]?.ToString() ?? "Unknown",
                        $"₱{amount:N2}",
                        ExportBrandingHelper.GetDisplayMethod(row["method"]?.ToString()),
                        GetDisplayStatus(row),
                        date.ToString("MMM dd, yyyy HH:mm")
                    );
                }
            }

            dgvTransactions.DataSource = display;

            if (dgvTransactions.Columns.Count >= 7)
            {
                dgvTransactions.Columns[0].Width = 80;
                dgvTransactions.Columns[1].Width = 80;
                dgvTransactions.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                dgvTransactions.Columns[3].Width = 100;
                dgvTransactions.Columns[4].Width = 100;
                dgvTransactions.Columns[5].Width = 120;
                dgvTransactions.Columns[6].Width = 130;
            }
        }

        private void FilterGrid()
        {
            if (_data == null || cboMethod == null || cboStatus == null || txtSearch == null) return;

            string method = cboMethod.SelectedItem?.ToString().ToLower() ?? "all methods";
            string status = cboStatus.SelectedItem?.ToString().ToLower() ?? "all status";
            string search = txtSearch.Text?.Trim().ToLower() ?? "";

            var filtered = _data.Clone();
            foreach (DataRow row in _data.Rows)
            {
                bool ok = true;
                string rowMethod = row["method"] != DBNull.Value ? row["method"].ToString().ToLower() : "";
                string rowStatus = row["status"] != DBNull.Value ? row["status"].ToString().ToLower() : "";

                if (method != "all methods" && rowMethod != method) ok = false;
                if (status != "all status" && rowStatus != status) ok = false;

                if (!string.IsNullOrEmpty(search))
                {
                    string cName = row["customer_name"] != DBNull.Value ? row["customer_name"].ToString().ToLower() : "";
                    string txId = row["transaction_id"].ToString();

                    if (!cName.Contains(search) && !txId.Contains(search)) ok = false;
                }

                if (ok) filtered.ImportRow(row);
            }
            RefreshGrid(filtered);
        }

        private void UpdateStats()
        {
            if (_data == null) return;

            decimal totalRevenue = 0;
            int totalTxs = _data.Rows.Count;
            int pendingCount = 0;
            int repairedNeeded = 0;

            foreach (DataRow row in _data.Rows)
            {
                string st = row["status"]?.ToString().ToLower() ?? "";
                if (st == "confirmed" || st == "paid" || st == "verified")
                {
                    totalRevenue += row["amount"] != DBNull.Value ? Convert.ToDecimal(row["amount"]) : 0;
                }
                if (st == "pending") pendingCount++;
            }

            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();
                using var cmd = new MySqlCommand(@"
                    SELECT COUNT(*)
                    FROM rentals r
                    LEFT JOIN transactions t
                      ON t.rental_id = r.rental_id
                     AND (
                        CASE
                            WHEN LOWER(COALESCE(t.type,'')) IN ('', 'rental') THEN 'payment'
                            ELSE LOWER(COALESCE(t.type,''))
                        END
                     ) = 'payment'
                     AND LOWER(COALESCE(t.status,'')) NOT IN ('rejected','refunded','duplicate')
                    WHERE LOWER(COALESCE(r.payment_status,'')) = 'paid'
                      AND t.transaction_id IS NULL", conn);
                repairedNeeded = Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch { }

            if (lblStats.InvokeRequired)
            {
                lblStats.Invoke(new Action(() => lblStats.Text = $"{totalTxs} transactions  ·  ₱{totalRevenue:N2} revenue  ·  {pendingCount} pending  ·  {repairedNeeded} payment logs need repair"));
            }
            else
            {
                lblStats.Text = $"{totalTxs} transactions  ·  ₱{totalRevenue:N2} revenue  ·  {pendingCount} pending  ·  {repairedNeeded} payment logs need repair";
            }
        }

        private void ReconcilePaymentLogs()
        {
            try
            {
                int repaired = AdminDataHelper.ReconcilePaidRentalTransactions(_connStr);
                LoadFromDB();
                MessageBox.Show(
                    repaired > 0
                        ? $"Repaired {repaired} payment log issue(s)."
                        : "No payment log issues were found.",
                    "Payment Log Repair",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not repair payment logs.\n" + ex.Message, "Repair Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ══ ROW SELECTED → Generate Receipt ══
        private void OnRowSelected(object sender, EventArgs e)
        {
            if (dgvTransactions == null || dgvTransactions.SelectedRows.Count == 0) return;

            var cellVal = dgvTransactions.SelectedRows[0].Cells[0].Value;
            if (cellVal == null || cellVal == DBNull.Value) return;

            string txIdString = cellVal.ToString();
            if (string.IsNullOrEmpty(txIdString)) return;

            // Safe parsing ng string na may kasamang "TX-"
            if (!int.TryParse(txIdString.Replace("TX-", ""), out int id)) return;

            _selectedTxId = id;

            var rows = _data.Select($"transaction_id = {id}");
            if (rows.Length == 0) return;

            _selectedRow = rows[0];

            string customer = _selectedRow["customer_name"]?.ToString();
            string rentalId = _selectedRow["rental_id"] != DBNull.Value ? $"DG-{Convert.ToInt32(_selectedRow["rental_id"]):D5}" : "N/A";
            string vehicle = _selectedRow["plate_no"]?.ToString();
            decimal amount = _selectedRow["amount"] != DBNull.Value ? Convert.ToDecimal(_selectedRow["amount"]) : 0;
            string method = ExportBrandingHelper.GetDisplayMethod(_selectedRow["method"]?.ToString()).ToUpperInvariant();
            string status = GetDisplayStatus(_selectedRow).ToUpperInvariant();
            DateTime date = _selectedRow["paid_at"] != DBNull.Value ? Convert.ToDateTime(_selectedRow["paid_at"]) : DateTime.Now;

            lblReceiptContent.Text = $@"
DRIVE & GO VEHICLE RENTALS
San Jose del Monte, Bulacan
----------------------------------------
OFFICIAL RECEIPT
----------------------------------------

Receipt No.    : TX-{id:D5}
Issued On      : {date:MMM dd, yyyy hh:mm tt}
Rental Ref     : {rentalId}
Branch         : San Jose del Monte, Bulacan

Customer       : {customer}
Vehicle Plate  : {vehicle}

Payment Method : {method}
Payment Status : {status}
Remarks        : Auto-generated payment receipt

----------------------------------------
TOTAL PAID     : ₱ {amount:N2}
----------------------------------------

Please keep this receipt for your records.
Thank you for choosing Drive & Go.
";
            btnExportPDF.Enabled = true;
        }

        // ══ PDF EXPORT via iText7 ══
        private void OnExportPDF(object s, EventArgs e)
        {
            if (_selectedTxId < 0 || _selectedRow == null) return;

            using var dlg = new SaveFileDialog
            {
                Title = "Save Receipt",
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = $"DriveAndGo_Receipt_TX-{_selectedTxId:D5}.pdf"
            };

            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                string path = dlg.FileName;
                var writerProps = new WriterProperties();
                writerProps.SetCompressionLevel(9);

                using var writer = new PdfWriter(path, writerProps);
                using var pdf = new PdfDocument(writer);
                using var doc = new Document(pdf);
                doc.SetMargins(28, 28, 24, 28);

                PdfFont fontBold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                PdfFont fontNormal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

                var orange = new DeviceRgb(230, 81, 0);
                var dark = new DeviceRgb(20, 20, 40);
                var gray = new DeviceRgb(100, 100, 130);
                var softGray = new DeviceRgb(244, 245, 249);
                var softOrange = new DeviceRgb(255, 241, 232);
                var white = ColorConstants.WHITE;

                string customer = _selectedRow["customer_name"]?.ToString();
                string rentalId = _selectedRow["rental_id"] != DBNull.Value ? $"DG-{Convert.ToInt32(_selectedRow["rental_id"]):D5}" : "N/A";
                string plate = _selectedRow["plate_no"]?.ToString();
                decimal amount = _selectedRow["amount"] != DBNull.Value ? Convert.ToDecimal(_selectedRow["amount"]) : 0;
                string method = ExportBrandingHelper.GetDisplayMethod(_selectedRow["method"]?.ToString());
                string status = GetDisplayStatus(_selectedRow);
                DateTime date = _selectedRow["paid_at"] != DBNull.Value ? Convert.ToDateTime(_selectedRow["paid_at"]) : DateTime.Now;
                string logoPath = ExportBrandingHelper.ResolveLogoPath();

                var headerTable = new Table(new float[] { 1.1f, 3.4f }).UseAllAvailableWidth();

                Cell logoCell = new Cell()
                    .SetBorder(Border.NO_BORDER)
                    .SetBackgroundColor(softOrange)
                    .SetPadding(12)
                    .SetTextAlignment(TextAlignment.LEFT)
                    .SetVerticalAlignment(VerticalAlignment.MIDDLE);

                if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
                {
                    var imgData = ImageDataFactory.Create(logoPath);
                    logoCell.Add(new iText.Layout.Element.Image(imgData).SetHeight(42).SetAutoScale(true));
                }
                else
                {
                    logoCell.Add(new Paragraph("DG")
                        .SetFont(fontBold)
                        .SetFontSize(22)
                        .SetFontColor(orange));
                }

                Cell textCell = new Cell()
                    .SetBorder(Border.NO_BORDER)
                    .SetPadding(12)
                    .SetBackgroundColor(orange)
                    .SetTextAlignment(TextAlignment.RIGHT);

                textCell.Add(new Paragraph("OFFICIAL RECEIPT")
                    .SetFont(fontBold)
                    .SetFontSize(18)
                    .SetFontColor(white));
                textCell.Add(new Paragraph("Drive & Go Vehicle Rentals")
                    .SetFont(fontNormal)
                    .SetFontSize(11)
                    .SetFontColor(new DeviceRgb(255, 220, 196)));
                textCell.Add(new Paragraph("San Jose del Monte, Bulacan")
                    .SetFont(fontNormal)
                    .SetFontSize(9)
                    .SetFontColor(new DeviceRgb(255, 220, 196)));

                headerTable.AddCell(logoCell);
                headerTable.AddCell(textCell);
                doc.Add(headerTable);
                doc.Add(new Paragraph(" "));

                var metaTable = new Table(new float[] { 1f, 1f }).UseAllAvailableWidth();
                metaTable.AddCell(CreateReceiptMetaCell("Receipt No.", $"TX-{_selectedTxId:D5}", fontBold, fontNormal, softGray, dark, gray));
                metaTable.AddCell(CreateReceiptMetaCell("Issued On", date.ToString("MMMM dd, yyyy hh:mm tt"), fontBold, fontNormal, softGray, dark, gray));
                metaTable.AddCell(CreateReceiptMetaCell("Rental Ref", rentalId, fontBold, fontNormal, softGray, dark, gray));
                metaTable.AddCell(CreateReceiptMetaCell("Payment Status", status, fontBold, fontNormal, softGray, dark, gray));
                doc.Add(metaTable);
                doc.Add(new Paragraph(" "));

                var detailsTable = new Table(new float[] { 1.2f, 2.4f }).UseAllAvailableWidth();
                AddReceiptDetail(detailsTable, "Customer", customer ?? "Unknown Customer", fontBold, fontNormal, softGray, dark, gray);
                AddReceiptDetail(detailsTable, "Vehicle Plate", plate ?? "Unknown Vehicle", fontBold, fontNormal, softGray, dark, gray);
                AddReceiptDetail(detailsTable, "Payment Method", method, fontBold, fontNormal, softGray, dark, gray);
                AddReceiptDetail(detailsTable, "Remarks", "Auto-generated payment receipt from the Drive & Go admin portal.", fontBold, fontNormal, softGray, dark, gray);
                doc.Add(detailsTable);
                doc.Add(new Paragraph(" "));

                var totalBox = new Table(1).UseAllAvailableWidth();
                totalBox.AddCell(new Cell()
                    .SetBorder(Border.NO_BORDER)
                    .SetBackgroundColor(softOrange)
                    .SetPadding(14)
                    .Add(new Paragraph("TOTAL PAID")
                        .SetFont(fontBold)
                        .SetFontSize(10)
                        .SetFontColor(gray))
                    .Add(new Paragraph($"PHP {amount:N2}")
                        .SetFont(fontBold)
                        .SetFontSize(22)
                        .SetFontColor(dark)));
                doc.Add(totalBox);
                doc.Add(new Paragraph(" "));

                doc.Add(new LineSeparator(new SolidLine(1f)).SetStrokeColor(new DeviceRgb(220, 223, 232)));
                doc.Add(new Paragraph("Please keep this receipt for your records. This document serves as proof of payment captured by the system.")
                    .SetFont(fontNormal)
                    .SetFontSize(9)
                    .SetFontColor(gray)
                    .SetMarginTop(10)
                    .SetMarginBottom(2));
                doc.Add(new Paragraph("Thank you for choosing Drive & Go. Drive safely.")
                    .SetFont(fontBold)
                    .SetFontSize(9)
                    .SetFontColor(dark));

                MessageBox.Show("Receipt saved successfully!\n" + path, "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("PDF Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private DataTable DeduplicateTransactions(DataTable source)
        {
            if (source == null || source.Rows.Count == 0)
                return source;

            var deduped = source.Clone();
            foreach (var group in source.AsEnumerable().GroupBy(BuildTransactionKey, StringComparer.OrdinalIgnoreCase))
            {
                DataRow keeper = group
                    .OrderBy(row => GetStatusRank(NormalizeStatus(row["status"]?.ToString())))
                    .ThenBy(row => string.IsNullOrWhiteSpace(row["type"]?.ToString()) ? 1 : 0)
                    .ThenByDescending(row => row["paid_at"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(row["paid_at"], CultureInfo.InvariantCulture))
                    .ThenByDescending(row => Convert.ToInt32(row["transaction_id"], CultureInfo.InvariantCulture))
                    .First();

                deduped.ImportRow(keeper);
            }

            return deduped;
        }

        private string BuildTransactionKey(DataRow row)
        {
            decimal amount = row["amount"] != DBNull.Value ? Convert.ToDecimal(row["amount"], CultureInfo.InvariantCulture) : 0;
            return string.Join("|",
                row["rental_id"] == DBNull.Value ? "0" : Convert.ToInt32(row["rental_id"], CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
                NormalizeTransactionType(row["type"]?.ToString()),
                amount.ToString("0.##", CultureInfo.InvariantCulture),
                NormalizeMethod(row["method"]?.ToString()),
                NormalizeProof(row["proof_url"]?.ToString()));
        }

        private string GetDisplayStatus(DataRow row)
        {
            string fallback = GetFallbackStatus(row);
            return ExportBrandingHelper.GetDisplayStatus(row["status"]?.ToString(), fallback);
        }

        private static string GetFallbackStatus(DataRow row)
        {
            string paymentStatus = row.Table.Columns.Contains("payment_status")
                ? NormalizeStatus(row["payment_status"]?.ToString())
                : string.Empty;

            return paymentStatus == "paid" ? "Confirmed" : "Pending Review";
        }

        private static string NormalizeTransactionType(string type)
        {
            string normalized = type?.Trim().ToLowerInvariant() ?? "payment";
            return normalized switch
            {
                "" => "payment",
                "rental" => "payment",
                _ => normalized
            };
        }

        private static string NormalizeMethod(string method) =>
            string.IsNullOrWhiteSpace(method) ? "cash" : method.Trim().ToLowerInvariant();

        private static string NormalizeProof(string proofUrl) =>
            string.IsNullOrWhiteSpace(proofUrl) ? string.Empty : proofUrl.Trim();

        private static string NormalizeStatus(string status) =>
            string.IsNullOrWhiteSpace(status) ? string.Empty : status.Trim().ToLowerInvariant();

        private static int GetStatusRank(string status) => status switch
        {
            "paid" => 0,
            "confirmed" => 1,
            "verified" => 2,
            "pending" => 3,
            "" => 4,
            _ => 5
        };

        private static Cell CreateReceiptMetaCell(
            string label,
            string value,
            PdfFont fontBold,
            PdfFont fontNormal,
            DeviceRgb background,
            DeviceRgb dark,
            DeviceRgb gray)
        {
            return new Cell()
                .SetBorder(Border.NO_BORDER)
                .SetBackgroundColor(background)
                .SetPadding(10)
                .Add(new Paragraph(label)
                    .SetFont(fontNormal)
                    .SetFontSize(8)
                    .SetFontColor(gray)
                    .SetMarginBottom(2))
                .Add(new Paragraph(value)
                    .SetFont(fontBold)
                    .SetFontSize(11)
                    .SetFontColor(dark)
                    .SetMarginTop(0));
        }

        private static void AddReceiptDetail(
            Table table,
            string label,
            string value,
            PdfFont fontBold,
            PdfFont fontNormal,
            DeviceRgb softGray,
            DeviceRgb dark,
            DeviceRgb gray)
        {
            table.AddCell(new Cell()
                .SetBorder(Border.NO_BORDER)
                .SetBackgroundColor(softGray)
                .SetPadding(10)
                .Add(new Paragraph(label)
                    .SetFont(fontNormal)
                    .SetFontSize(8)
                    .SetFontColor(gray)));

            table.AddCell(new Cell()
                .SetBorder(Border.NO_BORDER)
                .SetPadding(10)
                .Add(new Paragraph(value)
                    .SetFont(fontBold)
                    .SetFontSize(10)
                    .SetFontColor(dark)));
        }

        // ══ GRID DESIGN ══
        private void OnCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.Value == null || e.Value == DBNull.Value) return;

            // Status column is 5
            if (e.ColumnIndex == 5)
            {
                e.Handled = true;
                e.PaintBackground(e.ClipBounds, true);

                string val = e.Value.ToString();
                string normalized = val.ToLowerInvariant();
                WinColor c = normalized is "confirmed" or "paid" or "verified"
                    ? ColGreen
                    : normalized is "refunded" or "rejected"
                        ? ColRed
                        : ColYellow;

                var rect = new Rectangle(e.CellBounds.X + 6, e.CellBounds.Y + 9, e.CellBounds.Width - 12, e.CellBounds.Height - 18);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundRect(rect, 8);
                e.Graphics.FillPath(new SolidBrush(WinColor.FromArgb(30, c)), path);
                e.Graphics.DrawPath(new Pen(c, 1), path);
                TextRenderer.DrawText(e.Graphics, val.ToUpper(), new Font("Segoe UI", 8F, FontStyle.Bold), rect, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            // Method column is 4
            if (e.ColumnIndex == 4)
            {
                e.Handled = true;
                e.PaintBackground(e.ClipBounds, true);
                string val = e.Value.ToString();
                WinColor c = val.ToLower() == "gcash" ? ColBlue : val.ToLower() == "card" ? ColPurple : ColGreen;

                var rect = new Rectangle(e.CellBounds.X + 6, e.CellBounds.Y + 9, e.CellBounds.Width - 12, e.CellBounds.Height - 18);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundRect(rect, 8);
                e.Graphics.FillPath(new SolidBrush(WinColor.FromArgb(30, c)), path);
                e.Graphics.DrawPath(new Pen(c, 1), path);
                TextRenderer.DrawText(e.Graphics, val.ToUpper(), new Font("Segoe UI", 8F, FontStyle.Bold), rect, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
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

        protected override void Dispose(bool disposing)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }
    }
}
