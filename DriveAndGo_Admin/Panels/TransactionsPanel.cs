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

        private Button btnExportPDF;

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
            cboMethod.Items.AddRange(new object[] { "All Methods", "cash", "gcash", "card" });
            cboMethod.SelectedIndex = 0;
            cboMethod.SelectedIndexChanged += (s, e) => FilterGrid();

            cboStatus = new ComboBox { Size = new Size(130, 30), Location = new Point(366, 60), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White, ForeColor = ColText };
            cboStatus.Items.AddRange(new object[] { "All Status", "confirmed", "pending", "refunded" });
            cboStatus.SelectedIndex = 0;
            cboStatus.SelectedIndexChanged += (s, e) => FilterGrid();

            var btnRefresh = CreateBtn("⟳ Reload", ColSub, 506, 58, 90);
            btnRefresh.Click += (s, e) => LoadFromDB();

            topBar.Controls.Add(lblTitle);
            topBar.Controls.Add(lblStats);
            topBar.Controls.Add(txtSearch);
            topBar.Controls.Add(cboMethod);
            topBar.Controls.Add(cboStatus);
            topBar.Controls.Add(btnRefresh);

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
                        t.status, 
                        t.paid_at
                    FROM transactions t
                    LEFT JOIN rentals r ON t.rental_id = r.rental_id
                    LEFT JOIN users u ON r.customer_id = u.user_id
                    LEFT JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                    ORDER BY t.paid_at DESC", conn);

                using var adapter = new MySqlDataAdapter(cmd);
                adapter.Fill(_data);

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
                        row["method"]?.ToString().ToUpper() ?? "UNKNOWN",
                        row["status"]?.ToString() ?? "pending",
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
                dgvTransactions.Columns[4].Width = 80;
                dgvTransactions.Columns[5].Width = 90;
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

            foreach (DataRow row in _data.Rows)
            {
                string st = row["status"]?.ToString().ToLower();
                if (st == "confirmed" || st == "paid")
                {
                    totalRevenue += row["amount"] != DBNull.Value ? Convert.ToDecimal(row["amount"]) : 0;
                }
            }

            if (lblStats.InvokeRequired)
            {
                lblStats.Invoke(new Action(() => lblStats.Text = $"{totalTxs} Transactions  ·  Total Revenue: ₱{totalRevenue:N2}"));
            }
            else
            {
                lblStats.Text = $"{totalTxs} Transactions  ·  Total Revenue: ₱{totalRevenue:N2}";
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
            string method = _selectedRow["method"]?.ToString().ToUpper();
            string status = _selectedRow["status"]?.ToString().ToUpper();
            DateTime date = _selectedRow["paid_at"] != DBNull.Value ? Convert.ToDateTime(_selectedRow["paid_at"]) : DateTime.Now;

            lblReceiptContent.Text = $@"
DRIVE & GO VEHICLE RENTALS
San Jose del Monte, Bulacan
----------------------------------------
OFFICIAL RECEIPT
----------------------------------------

Transaction ID : TX-{id:D5}
Date           : {date:MMM dd, yyyy HH:mm}
Rental Ref     : {rentalId}

Customer       : {customer}
Vehicle Plate  : {vehicle}

Payment Method : {method}
Payment Status : {status}

----------------------------------------
TOTAL PAID     : ₱ {amount:N2}
----------------------------------------

Thank you for choosing Drive & Go!
Drive safely!
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
                using var writer = new PdfWriter(path);
                using var pdf = new PdfDocument(writer);
                using var doc = new Document(pdf);

                PdfFont fontBold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                PdfFont fontNormal = PdfFontFactory.CreateFont(StandardFonts.COURIER); // Resibo look

                var orange = new iText.Kernel.Colors.DeviceRgb(230, 81, 0);
                var dark = new iText.Kernel.Colors.DeviceRgb(20, 20, 40);
                var gray = new iText.Kernel.Colors.DeviceRgb(100, 100, 130);

                // LOGO Header
                var headerTable = new iText.Layout.Element.Table(new float[] { 1, 3 }).UseAllAvailableWidth();
                Cell logoCell = new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(TextAlignment.LEFT);
                string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "logo.png");
                if (File.Exists(logoPath))
                {
                    var imgData = ImageDataFactory.Create(logoPath);
                    var logo = new iText.Layout.Element.Image(imgData).SetHeight(50).SetAutoScale(true);
                    logoCell.Add(logo);
                }

                Cell textCell = new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT);
                textCell.Add(new Paragraph("OFFICIAL RECEIPT").SetFontColor(orange).SetFontSize(20).SetFont(fontBold));
                textCell.Add(new Paragraph("Drive & Go Vehicle Rental Services").SetFontColor(gray).SetFontSize(10));

                headerTable.AddCell(logoCell);
                headerTable.AddCell(textCell);
                doc.Add(headerTable);
                doc.Add(new Paragraph("\n───────────────────────────────────────────────────────────────").SetFontColor(gray));

                // Receipt Content
                string customer = _selectedRow["customer_name"]?.ToString();
                string rentalId = _selectedRow["rental_id"] != DBNull.Value ? $"DG-{Convert.ToInt32(_selectedRow["rental_id"]):D5}" : "N/A";
                string plate = _selectedRow["plate_no"]?.ToString();
                decimal amount = _selectedRow["amount"] != DBNull.Value ? Convert.ToDecimal(_selectedRow["amount"]) : 0;
                string method = _selectedRow["method"]?.ToString().ToUpper();
                string status = _selectedRow["status"]?.ToString().ToUpper();
                DateTime date = _selectedRow["paid_at"] != DBNull.Value ? Convert.ToDateTime(_selectedRow["paid_at"]) : DateTime.Now;

                var body = new Paragraph()
                    .SetFont(fontNormal)
                    .SetFontSize(12)
                    .SetFontColor(dark)
                    .Add($"\nTransaction ID : TX-{_selectedTxId:D5}")
                    .Add($"\nDate           : {date:MMM dd, yyyy hh:mm tt}")
                    .Add($"\nRental Ref     : {rentalId}")
                    .Add($"\n\nCustomer       : {customer}")
                    .Add($"\nVehicle Plate  : {plate}")
                    .Add($"\nPayment Method : {method}")
                    .Add($"\nPayment Status : {status}")
                    .Add("\n\n───────────────────────────────────────────────────────────────\n")
                    .Add(new Text($"TOTAL PAID     : PHP {amount:N2}").SetFont(fontBold).SetFontSize(14))
                    .Add("\n───────────────────────────────────────────────────────────────\n\n")
                    .Add("Thank you for choosing Drive & Go!");

                doc.Add(body);

                MessageBox.Show("Receipt saved successfully!\n" + path, "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("PDF Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                WinColor c = val.ToLower() == "confirmed" || val.ToLower() == "paid" ? ColGreen : val.ToLower() == "refunded" ? ColRed : ColYellow;

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