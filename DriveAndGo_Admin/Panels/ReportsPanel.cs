#nullable disable
using DriveAndGo_Admin.Helpers;
using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
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
using WinColor = System.Drawing.Color;
using TextAlignment = iText.Layout.Properties.TextAlignment;

namespace DriveAndGo_Admin.Panels
{
    public class ReportsPanel : UserControl
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

        private readonly string _connStr = "Server=localhost;Database=vehicle_rental_db;Uid=root;Pwd=;";

        // ── UI ──
        private Panel topBar;
        private ComboBox cboPeriod;
        private Button btnExportPDF, btnExportCSV;
        private Label lblTotalRev, lblTotalTx, lblPending;

        private Panel pnlMainContent;
        private Panel pnlChartContainer;
        private DataGridView dgvReport;

        // ── State ──
        private DataTable _reportData = new DataTable();
        private string _currentPeriod = "Monthly"; // Daily, Weekly, Monthly, Yearly

        public ReportsPanel()
        {
            this.Dock = DockStyle.Fill;
            this.DoubleBuffered = true;
            this.BackColor = ColBg;
            ThemeManager.ThemeChanged += OnThemeChanged;

            BuildUI();
            this.Load += (s, e) => LoadReportData();
        }

        // ══ BUILD UI ══
        private void BuildUI()
        {
            // ── TOP BAR ──
            topBar = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = WinColor.Transparent, Padding = new Padding(16, 12, 16, 8) };

            var lblTitle = new Label { Text = "Sales & Analytics Report", Font = new Font("Segoe UI", 18F, FontStyle.Bold), ForeColor = ColText, AutoSize = true, Location = new Point(16, 12), BackColor = WinColor.Transparent };

            cboPeriod = new ComboBox { Size = new Size(150, 30), Location = new Point(16, 56), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10F), BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White, ForeColor = ColText };
            cboPeriod.Items.AddRange(new object[] { "Daily", "Weekly", "Monthly", "Yearly" });
            cboPeriod.SelectedIndex = 2; // Default Monthly
            cboPeriod.SelectedIndexChanged += (s, e) => { _currentPeriod = cboPeriod.SelectedItem.ToString(); LoadReportData(); };

            btnExportPDF = CreateBtn("📄 Export PDF", ColRed, 180, 54, 130);
            btnExportCSV = CreateBtn("📊 Export Excel (CSV)", ColGreen, 320, 54, 160);

            btnExportPDF.Click += OnExportPDF;
            btnExportCSV.Click += OnExportCSV;

            topBar.Controls.Add(lblTitle);
            topBar.Controls.Add(cboPeriod);
            topBar.Controls.Add(btnExportPDF);
            topBar.Controls.Add(btnExportCSV);

            // ── MAIN CONTENT (Split between Chart and Table) ──
            pnlMainContent = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };

            // ── STATS SUMMARY CARDS (Top of Main Content) ──
            Panel pnlStats = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = WinColor.Transparent };
            lblTotalRev = CreateStatCard(pnlStats, "TOTAL REVENUE", "₱ 0.00", ColGreen, 0);
            lblTotalTx = CreateStatCard(pnlStats, "SUCCESSFUL TRANSACTIONS", "0", ColBlue, 1);
            lblPending = CreateStatCard(pnlStats, "PENDING PAYMENTS", "0", ColYellow, 2);

            // ── CHART ──
            pnlChartContainer = new Panel { Dock = DockStyle.Top, Height = 300, BackColor = WinColor.Transparent };
            pnlChartContainer.Paint += DrawCustomBarChart;

            // ── GRID ──
            dgvReport = new DataGridView { Dock = DockStyle.Fill, Margin = new Padding(0, 16, 0, 0) };
            StyleGrid(dgvReport);

            pnlMainContent.Controls.Add(dgvReport);

            // Add a spacer between chart and grid
            Panel spacer = new Panel { Dock = DockStyle.Top, Height = 16 };
            pnlMainContent.Controls.Add(spacer);

            pnlMainContent.Controls.Add(pnlChartContainer);
            pnlMainContent.Controls.Add(pnlStats);

            this.Controls.Add(pnlMainContent);
            this.Controls.Add(topBar);
        }

        private Label CreateStatCard(Panel parent, string title, string value, WinColor accent, int index)
        {
            Panel card = new Panel { Size = new Size(250, 80), Location = new Point(index * 266, 10), BackColor = WinColor.Transparent };
            card.Paint += (s, e) => {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 8);
                g.FillPath(new SolidBrush(ThemeManager.IsDarkMode ? WinColor.FromArgb(22, 22, 35) : WinColor.White), path);
                g.FillRectangle(new SolidBrush(accent), 0, card.Height - 4, card.Width, 4);
                g.DrawPath(new Pen(ColBorder, 1), path);
            };

            Label lblTitle = new Label { Text = title, Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = ColSub, AutoSize = true, Location = new Point(12, 12), BackColor = WinColor.Transparent };
            Label lblVal = new Label { Text = value, Font = new Font("Segoe UI", 16F, FontStyle.Bold), ForeColor = ColText, AutoSize = false, Size = new Size(226, 30), Location = new Point(12, 32), BackColor = WinColor.Transparent };

            card.Controls.Add(lblTitle);
            card.Controls.Add(lblVal);
            parent.Controls.Add(card);

            return lblVal;
        }

        // ══ THEME SYNC ══
        private void OnThemeChanged(object s, EventArgs e)
        {
            this.BackColor = ColBg;
            topBar.BackColor = ThemeManager.IsDarkMode ? ColBg : WinColor.FromArgb(250, 250, 255);
            cboPeriod.BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White;
            cboPeriod.ForeColor = ColText;

            foreach (Control c in topBar.Controls) { if (c is Label l) l.ForeColor = ColText; }
            foreach (Control p in pnlMainContent.Controls)
            {
                if (p is Panel pnl) { pnl.Invalidate(true); }
            }

            StyleGrid(dgvReport);
            this.Invalidate(true);
        }

        // ══ DATABASE LOGIC ══
        private void LoadReportData()
        {
            _reportData = new DataTable();

            // Format format date grouping based on selected period
            string dateGroupFormat = "";
            string displayFormat = "";

            switch (_currentPeriod)
            {
                case "Daily":
                    dateGroupFormat = "DATE(paid_at)";
                    displayFormat = "DATE_FORMAT(paid_at, '%b %d, %Y')";
                    break;
                case "Weekly":
                    dateGroupFormat = "YEARWEEK(paid_at)";
                    displayFormat = "CONCAT('Week ', WEEK(paid_at), ', ', YEAR(paid_at))";
                    break;
                case "Monthly":
                    dateGroupFormat = "DATE_FORMAT(paid_at, '%Y-%m')";
                    displayFormat = "DATE_FORMAT(paid_at, '%M %Y')";
                    break;
                case "Yearly":
                    dateGroupFormat = "YEAR(paid_at)";
                    displayFormat = "YEAR(paid_at)";
                    break;
            }

            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();

                string query = $@"
                    SELECT 
                        {dateGroupFormat} AS group_key,
                        {displayFormat} AS period_label,
                        COUNT(transaction_id) AS total_transactions,
                        SUM(CASE WHEN status = 'confirmed' OR status = 'paid' THEN amount ELSE 0 END) AS total_revenue,
                        SUM(CASE WHEN status = 'pending' THEN amount ELSE 0 END) AS pending_amount
                    FROM transactions
                    GROUP BY group_key, period_label
                    ORDER BY group_key DESC
                    LIMIT 30"; // Limit to last 30 periods for performance

                using var adapter = new MySqlDataAdapter(new MySqlCommand(query, conn));
                adapter.Fill(_reportData);

                UpdateDashboard();
            }
            catch (Exception ex)
            {
                LoadDummyData(); // Load dummy data if DB fails so UI can be tested
            }
        }

        private void LoadDummyData()
        {
            _reportData.Clear();
            _reportData.Columns.Add("period_label", typeof(string));
            _reportData.Columns.Add("total_transactions", typeof(int));
            _reportData.Columns.Add("total_revenue", typeof(decimal));
            _reportData.Columns.Add("pending_amount", typeof(decimal));

            if (_currentPeriod == "Daily")
            {
                for (int i = 0; i < 7; i++)
                    _reportData.Rows.Add(DateTime.Now.AddDays(-i).ToString("MMM dd, yyyy"), new Random(i).Next(2, 10), new Random(i).Next(1500, 8000), new Random(i).Next(0, 1000));
            }
            else if (_currentPeriod == "Monthly")
            {
                for (int i = 0; i < 6; i++)
                    _reportData.Rows.Add(DateTime.Now.AddMonths(-i).ToString("MMMM yyyy"), new Random(i).Next(20, 50), new Random(i).Next(25000, 80000), new Random(i).Next(1000, 5000));
            }

            UpdateDashboard();
        }

        private void UpdateDashboard()
        {
            // 1. Update Grid
            dgvReport.DataSource = null;
            dgvReport.Columns.Clear();

            var display = new DataTable();
            display.Columns.Add("Period", typeof(string));
            display.Columns.Add("Transactions", typeof(int));
            display.Columns.Add("Revenue", typeof(string));
            display.Columns.Add("Pending", typeof(string));

            decimal grandTotalRev = 0;
            int grandTotalTx = 0;
            decimal grandTotalPending = 0;

            foreach (DataRow row in _reportData.Rows)
            {
                decimal rev = row["total_revenue"] != DBNull.Value ? Convert.ToDecimal(row["total_revenue"]) : 0;
                decimal pen = row["pending_amount"] != DBNull.Value ? Convert.ToDecimal(row["pending_amount"]) : 0;
                int txs = row["total_transactions"] != DBNull.Value ? Convert.ToInt32(row["total_transactions"]) : 0;

                grandTotalRev += rev;
                grandTotalTx += txs;
                grandTotalPending += pen;

                display.Rows.Add(
                    row["period_label"].ToString(),
                    txs,
                    $"₱ {rev:N2}",
                    $"₱ {pen:N2}"
                );
            }

            dgvReport.DataSource = display;

            if (dgvReport.Columns.Count >= 4)
            {
                dgvReport.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                dgvReport.Columns[1].Width = 150;
                dgvReport.Columns[2].Width = 200;
                dgvReport.Columns[3].Width = 200;
            }

            // 2. Update Stats Cards
            lblTotalRev.Text = $"₱ {grandTotalRev:N2}";
            lblTotalTx.Text = grandTotalTx.ToString();
            lblPending.Text = $"₱ {grandTotalPending:N2}";

            // 3. Trigger Chart Redraw
            pnlChartContainer.Invalidate();
        }

        // ══ CUSTOM CHART DRAWING (Modern Bar Chart) ══
        private void DrawCustomBarChart(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background
            var rect = new Rectangle(0, 0, pnlChartContainer.Width - 1, pnlChartContainer.Height - 1);
            using var path = RoundRect(rect, 12);
            g.FillPath(new SolidBrush(ThemeManager.IsDarkMode ? WinColor.FromArgb(22, 22, 35) : WinColor.White), path);
            g.DrawPath(new Pen(ColBorder, 1), path);

            if (_reportData.Rows.Count == 0)
            {
                TextRenderer.DrawText(g, "No data available for the selected period.", new Font("Segoe UI", 10F), rect, ColSub, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            // Title
            g.DrawString($"Revenue Trend ({_currentPeriod})", new Font("Segoe UI", 12F, FontStyle.Bold), new SolidBrush(ColText), new PointF(20, 15));

            // Setup Chart Bounds
            int paddingL = 60, paddingR = 20, paddingT = 60, paddingB = 40;
            int chartW = pnlChartContainer.Width - paddingL - paddingR;
            int chartH = pnlChartContainer.Height - paddingT - paddingB;

            // Find Max Value for Y-Axis Scale
            decimal maxVal = 0;
            var rows = _reportData.AsEnumerable().Reverse().ToArray(); // Chronological order (old to new)

            foreach (var row in rows)
            {
                decimal v = row["total_revenue"] != DBNull.Value ? Convert.ToDecimal(row["total_revenue"]) : 0;
                if (v > maxVal) maxVal = v;
            }

            if (maxVal == 0) maxVal = 1000; // default if 0

            // Round up maxVal to nearest beautiful number (e.g. 4300 -> 5000)
            double exponent = Math.Floor(Math.Log10((double)maxVal));
            double fraction = (double)maxVal / Math.Pow(10, exponent);
            double niceFraction = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
            decimal niceMax = (decimal)(niceFraction * Math.Pow(10, exponent));

            // Draw Y-Axis lines (4 grid lines)
            using Pen gridPen = new Pen(ThemeManager.IsDarkMode ? WinColor.FromArgb(40, 40, 55) : WinColor.FromArgb(230, 230, 240), 1) { DashStyle = DashStyle.Dash };
            using SolidBrush textBrush = new SolidBrush(ColSub);
            Font labelFont = new Font("Segoe UI", 8F);

            for (int i = 0; i <= 4; i++)
            {
                float y = paddingT + chartH - (chartH * (i / 4f));
                decimal yVal = niceMax * (i / 4m);
                string yStr = yVal >= 1000 ? (yVal / 1000).ToString("0.#") + "k" : yVal.ToString("0");

                g.DrawLine(gridPen, paddingL, y, paddingL + chartW, y);
                g.DrawString(yStr, labelFont, textBrush, new PointF(paddingL - 40, y - 6));
            }

            // Draw Bars
            int barCount = rows.Length;
            float barSpace = chartW / (float)barCount;
            float barWidth = barSpace * 0.6f;
            if (barWidth > 60) barWidth = 60; // Max width cap

            for (int i = 0; i < barCount; i++)
            {
                var r = rows[i];
                decimal val = r["total_revenue"] != DBNull.Value ? Convert.ToDecimal(r["total_revenue"]) : 0;

                float h = (float)(val / niceMax) * chartH;
                float x = paddingL + (i * barSpace) + (barSpace - barWidth) / 2;
                float y = paddingT + chartH - h;

                // Create Gradient Bar
                RectangleF barRect = new RectangleF(x, y, barWidth, h);
                if (h > 0)
                {
                    using LinearGradientBrush brush = new LinearGradientBrush(barRect, ColAccent, WinColor.FromArgb(255, ColAccent.R + 40 > 255 ? 255 : ColAccent.R + 40, ColAccent.G + 40, ColAccent.B), LinearGradientMode.Vertical);

                    // Rounded top corners for bars
                    using GraphicsPath barPath = new GraphicsPath();
                    int rad = 4;
                    if (h > rad)
                    {
                        barPath.AddArc(x, y, rad * 2, rad * 2, 180, 90);
                        barPath.AddArc(x + barWidth - rad * 2, y, rad * 2, rad * 2, 270, 90);
                        barPath.AddLine(x + barWidth, y + rad, x + barWidth, y + h);
                        barPath.AddLine(x, y + h, x, y + rad);
                        barPath.CloseFigure();
                        g.FillPath(brush, barPath);
                    }
                    else
                    {
                        g.FillRectangle(brush, barRect);
                    }

                    // Hover tooltip simulation (just draw value on top if space allows)
                    if (barCount <= 15)
                    {
                        string vStr = val >= 1000 ? (val / 1000).ToString("0.#") + "k" : val.ToString("0");
                        var size = g.MeasureString(vStr, labelFont);
                        g.DrawString(vStr, labelFont, new SolidBrush(ColText), new PointF(x + (barWidth - size.Width) / 2, y - 16));
                    }
                }

                // X-Axis Label (Show only a few if too many bars)
                if (barCount <= 12 || i % (barCount / 6) == 0)
                {
                    string xLbl = r["period_label"].ToString();
                    if (xLbl.Length > 8) xLbl = xLbl.Substring(0, 8) + "..";
                    var xSize = g.MeasureString(xLbl, labelFont);
                    g.DrawString(xLbl, labelFont, textBrush, new PointF(x + (barWidth - xSize.Width) / 2, paddingT + chartH + 8));
                }
            }
        }

        // ══ EXPORT TO CSV (EXCEL) ══
        private void OnExportCSV(object s, EventArgs e)
        {
            if (_reportData == null || _reportData.Rows.Count == 0)
            {
                MessageBox.Show("No data to export.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using SaveFileDialog sfd = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = $"Sales_Report_{_currentPeriod}_{DateTime.Now:yyyyMMdd}.csv" };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    using StreamWriter sw = new StreamWriter(sfd.FileName);
                    sw.WriteLine("Period,Total Transactions,Total Revenue,Pending Amount");

                    foreach (DataRow row in _reportData.Rows)
                    {
                        sw.WriteLine($"{row["period_label"]},{row["total_transactions"]},{row["total_revenue"]},{row["pending_amount"]}");
                    }

                    MessageBox.Show("Excel/CSV export successful!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { MessageBox.Show("Export error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        // ══ EXPORT TO PDF via iText7 ══
        private void OnExportPDF(object s, EventArgs e)
        {
            if (_reportData == null || _reportData.Rows.Count == 0)
            {
                MessageBox.Show("No data to export.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title = "Save Sales Report",
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = $"DriveAndGo_Sales_Report_{_currentPeriod}_{DateTime.Now:yyyyMMdd}.pdf"
            };

            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                string path = dlg.FileName;
                using var writer = new PdfWriter(path);
                using var pdf = new PdfDocument(writer);
                using var doc = new Document(pdf);

                PdfFont fontBold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                PdfFont fontNormal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

                var orange = new iText.Kernel.Colors.DeviceRgb(230, 81, 0);
                var dark = new iText.Kernel.Colors.DeviceRgb(20, 20, 40);
                var gray = new iText.Kernel.Colors.DeviceRgb(100, 100, 130);
                var white = iText.Kernel.Colors.ColorConstants.WHITE;
                var lightBg = new iText.Kernel.Colors.DeviceRgb(248, 248, 252);

                // ── HEADER MAY LOGO ──
                var headerTable = new iText.Layout.Element.Table(new float[] { 1, 5 }).UseAllAvailableWidth();

                Cell logoCell = new Cell()
                    .SetBackgroundColor(orange)
                    .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetPaddingTop(10);

                string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "logo.png");

                if (File.Exists(logoPath))
                {
                    var imgData = ImageDataFactory.Create(logoPath);
                    var logo = new iText.Layout.Element.Image(imgData).SetHeight(40).SetAutoScale(true);
                    logoCell.Add(logo);
                }
                else
                {
                    logoCell.Add(new Paragraph("DG").SetFontColor(white).SetFontSize(26).SetFont(fontBold));
                }

                Cell textCell = new Cell()
                    .SetBackgroundColor(orange)
                    .SetPadding(12)
                    .SetBorder(iText.Layout.Borders.Border.NO_BORDER);

                textCell.Add(new Paragraph("DRIVE & GO")
                    .SetFontColor(white).SetFontSize(24).SetFont(fontBold));
                textCell.Add(new Paragraph("Sales & Analytics Report")
                    .SetFontColor(new iText.Kernel.Colors.DeviceRgb(255, 200, 160)).SetFontSize(12).SetFont(fontNormal));

                headerTable.AddCell(logoCell);
                headerTable.AddCell(textCell);
                doc.Add(headerTable);
                doc.Add(new Paragraph(" "));

                // Title
                doc.Add(new Paragraph($"{_currentPeriod.ToUpper()} SALES REPORT")
                    .SetFontSize(16).SetFont(fontBold).SetFontColor(dark)
                    .SetTextAlignment(TextAlignment.CENTER));
                doc.Add(new Paragraph($"Generated on: {DateTime.Now:MMMM dd, yyyy hh:mm tt}")
                    .SetFontSize(9).SetFontColor(gray).SetFont(fontNormal)
                    .SetTextAlignment(TextAlignment.CENTER));
                doc.Add(new Paragraph(" "));

                // Data Table
                var dataTable = new iText.Layout.Element.Table(new float[] { 3, 2, 3, 3 })
                    .UseAllAvailableWidth().SetMarginBottom(12);

                // Table Headers
                string[] headers = { "Period", "Transactions", "Revenue (PHP)", "Pending (PHP)" };
                foreach (var h in headers)
                {
                    dataTable.AddHeaderCell(new Cell().SetBackgroundColor(dark).SetFontColor(white).SetPadding(6).Add(new Paragraph(h).SetFont(fontBold).SetFontSize(10)));
                }

                // Table Rows
                decimal totalRev = 0;
                foreach (DataRow row in _reportData.Rows)
                {
                    decimal rev = row["total_revenue"] != DBNull.Value ? Convert.ToDecimal(row["total_revenue"]) : 0;
                    totalRev += rev;

                    dataTable.AddCell(new Cell().SetPadding(5).Add(new Paragraph(row["period_label"].ToString()).SetFont(fontNormal).SetFontSize(9)));
                    dataTable.AddCell(new Cell().SetPadding(5).SetTextAlignment(TextAlignment.CENTER).Add(new Paragraph(row["total_transactions"].ToString()).SetFont(fontNormal).SetFontSize(9)));
                    dataTable.AddCell(new Cell().SetPadding(5).SetTextAlignment(TextAlignment.RIGHT).Add(new Paragraph(rev.ToString("N2")).SetFont(fontNormal).SetFontSize(9)));
                    dataTable.AddCell(new Cell().SetPadding(5).SetTextAlignment(TextAlignment.RIGHT).Add(new Paragraph(Convert.ToDecimal(row["pending_amount"]).ToString("N2")).SetFont(fontNormal).SetFontSize(9).SetFontColor(new iText.Kernel.Colors.DeviceRgb(200, 50, 50))));
                }
                doc.Add(dataTable);

                // Summary
                doc.Add(new Paragraph($"GRAND TOTAL REVENUE: PHP {totalRev:N2}")
                    .SetFont(fontBold).SetFontSize(14).SetFontColor(orange)
                    .SetTextAlignment(TextAlignment.RIGHT).SetMarginTop(10));

                // Footer
                doc.Add(new Paragraph("Drive & Go System - Confidential Report")
                    .SetFontSize(8).SetFont(fontNormal).SetFontColor(gray)
                    .SetTextAlignment(TextAlignment.CENTER).SetMarginTop(30));

                MessageBox.Show("PDF Report saved successfully!\n" + path, "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("PDF Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ══ UI STYLING ══
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
            dgv.RowTemplate.Height = 40;
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