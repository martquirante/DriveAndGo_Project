#nullable disable
using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
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
using DriveAndGo_Admin.Helpers;

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
        private Label lblTotalRev, lblPaidRentals, lblPending;
        private Label lblInsight;

        private Panel pnlMainContent;
        private Panel pnlChartContainer;
        private DataGridView dgvReport;

        // ── State ──
        private DataTable _reportData = new DataTable();
        private string _currentPeriod = "Monthly"; // Daily, Weekly, Monthly, Yearly

        public ReportsPanel()
        {
            Dock = DockStyle.Fill;
            DoubleBuffered = true;
            BackColor = ColBg;
            ThemeManager.ThemeChanged += OnThemeChanged;

            BuildUI();
            Load += (s, e) => LoadReportData();
        }

        // ══ BUILD UI ══
        private void BuildUI()
        {
            topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = WinColor.Transparent,
                Padding = new Padding(16, 12, 16, 8)
            };

            var lblTitle = new Label
            {
                Text = "Sales & Analytics Report",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = ColText,
                AutoSize = true,
                Location = new Point(16, 12),
                BackColor = WinColor.Transparent
            };

            cboPeriod = new ComboBox
            {
                Size = new Size(150, 30),
                Location = new Point(16, 56),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F),
                BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White,
                ForeColor = ColText
            };
            cboPeriod.Items.AddRange(new object[] { "Daily", "Weekly", "Monthly", "Yearly" });
            cboPeriod.SelectedIndex = 2;
            cboPeriod.SelectedIndexChanged += (s, e) =>
            {
                _currentPeriod = cboPeriod.SelectedItem?.ToString() ?? "Monthly";
                LoadReportData();
            };

            btnExportPDF = CreateBtn("📄 Export PDF", ColRed, 180, 54, 130);
            btnExportCSV = CreateBtn("📊 Export Excel", ColGreen, 320, 54, 150);

            lblInsight = new Label
            {
                Text = "Loading analytics…",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = ColSub,
                AutoSize = true,
                Location = new Point(500, 60),
                BackColor = WinColor.Transparent
            };

            btnExportPDF.Click += OnExportPDF;
            btnExportCSV.Click += OnExportCSV;

            topBar.Controls.Add(lblTitle);
            topBar.Controls.Add(cboPeriod);
            topBar.Controls.Add(btnExportPDF);
            topBar.Controls.Add(btnExportCSV);
            topBar.Controls.Add(lblInsight);

            pnlMainContent = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16)
            };

            Panel pnlStats = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = WinColor.Transparent
            };

            lblTotalRev = CreateStatCard(pnlStats, "TOTAL REVENUE", "₱ 0.00", ColGreen, 0);
            lblPaidRentals = CreateStatCard(pnlStats, "PAID RENTALS", "0", ColBlue, 1);
            lblPending = CreateStatCard(pnlStats, "PENDING PAYMENTS", "₱ 0.00", ColYellow, 2);

            pnlChartContainer = new Panel
            {
                Dock = DockStyle.Top,
                Height = 300,
                BackColor = WinColor.Transparent
            };
            pnlChartContainer.Paint += DrawCustomBarChart;

            dgvReport = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 16, 0, 0)
            };
            StyleGrid(dgvReport);

            pnlMainContent.Controls.Add(dgvReport);
            pnlMainContent.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 16 });
            pnlMainContent.Controls.Add(pnlChartContainer);
            pnlMainContent.Controls.Add(pnlStats);

            Controls.Add(pnlMainContent);
            Controls.Add(topBar);
        }

        private Label CreateStatCard(Panel parent, string title, string value, WinColor accent, int index)
        {
            Panel card = new Panel
            {
                Size = new Size(250, 80),
                Location = new Point(index * 266, 10),
                BackColor = WinColor.Transparent
            };

            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 8);
                g.FillPath(new SolidBrush(ThemeManager.IsDarkMode ? WinColor.FromArgb(22, 22, 35) : WinColor.White), path);
                g.FillRectangle(new SolidBrush(accent), 0, card.Height - 4, card.Width, 4);
                g.DrawPath(new Pen(ColBorder, 1), path);
            };

            Label lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = ColSub,
                AutoSize = true,
                Location = new Point(12, 12),
                BackColor = WinColor.Transparent
            };

            Label lblVal = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = ColText,
                AutoSize = false,
                Size = new Size(226, 30),
                Location = new Point(12, 32),
                BackColor = WinColor.Transparent
            };

            card.Controls.Add(lblTitle);
            card.Controls.Add(lblVal);
            parent.Controls.Add(card);

            return lblVal;
        }

        // ══ THEME SYNC ══
        private void OnThemeChanged(object s, EventArgs e)
        {
            BackColor = ColBg;
            topBar.BackColor = ThemeManager.IsDarkMode ? ColBg : WinColor.FromArgb(250, 250, 255);
            cboPeriod.BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.White;
            cboPeriod.ForeColor = ColText;

            foreach (Control c in topBar.Controls)
            {
                if (c is Label l)
                    l.ForeColor = (l == lblInsight) ? ColSub : ColText;
            }

            foreach (Control p in pnlMainContent.Controls)
            {
                if (p is Panel pnl)
                    pnl.Invalidate(true);
            }

            StyleGrid(dgvReport);
            Invalidate(true);
        }

        // ══ DATABASE LOGIC ══
        private void LoadReportData()
        {
            _reportData = new DataTable();

            string groupExpr;
            string labelExpr;

            // Mas accurate kung booking/payment timeline ay based sa created_at.
            // Fallback sa start_date kung null ang created_at.
            string baseDateExpr = "COALESCE(created_at, start_date)";

            switch (_currentPeriod)
            {
                case "Daily":
                    groupExpr = $"DATE({baseDateExpr})";
                    labelExpr = $"DATE_FORMAT({baseDateExpr}, '%b %d, %Y')";
                    break;

                case "Weekly":
                    groupExpr = $"YEARWEEK({baseDateExpr}, 1)";
                    labelExpr = $"CONCAT('Week ', WEEK({baseDateExpr}, 1), ', ', YEAR({baseDateExpr}))";
                    break;

                case "Monthly":
                    groupExpr = $"DATE_FORMAT({baseDateExpr}, '%Y-%m')";
                    labelExpr = $"DATE_FORMAT({baseDateExpr}, '%M %Y')";
                    break;

                default:
                    groupExpr = $"YEAR({baseDateExpr})";
                    labelExpr = $"YEAR({baseDateExpr})";
                    break;
            }

            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();

                string query = $@"
                    SELECT
                        {groupExpr} AS group_key,
                        {labelExpr} AS period_label,
                        COUNT(*) AS total_rentals,
                        SUM(CASE WHEN LOWER(TRIM(COALESCE(payment_status,''))) = 'paid' THEN 1 ELSE 0 END) AS paid_rentals,
                        COALESCE(SUM(CASE WHEN LOWER(TRIM(COALESCE(payment_status,''))) = 'paid' THEN COALESCE(total_amount,0) ELSE 0 END), 0) AS total_revenue,
                        COALESCE(AVG(CASE WHEN LOWER(TRIM(COALESCE(payment_status,''))) = 'paid' THEN COALESCE(total_amount,0) END), 0) AS avg_ticket,
                        COALESCE(SUM(CASE WHEN LOWER(TRIM(COALESCE(payment_status,''))) <> 'paid' THEN COALESCE(total_amount,0) ELSE 0 END), 0) AS pending_amount
                    FROM rentals
                    GROUP BY group_key, period_label
                    ORDER BY group_key DESC
                    LIMIT 30;";

                using var adapter = new MySqlDataAdapter(new MySqlCommand(query, conn));
                adapter.Fill(_reportData);

                EnsureReportSchema();
                UpdateDashboard();

                int rowCount = _reportData.Rows.Count;
                lblInsight.Text = rowCount > 0
                    ? $"{_currentPeriod} analytics loaded from rentals table ({rowCount} period(s))."
                    : $"No {_currentPeriod.ToLower()} analytics available yet from rentals.";
            }
            catch (Exception ex)
            {
                BuildEmptyReportTable();
                UpdateDashboard();
                lblInsight.Text = "Analytics load failed. Check the database connection and rentals table state.";
                MessageBox.Show("Could not load analytics.\n" + ex.Message, "Reports Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void EnsureReportSchema()
        {
            if (!_reportData.Columns.Contains("period_label")) _reportData.Columns.Add("period_label", typeof(string));
            if (!_reportData.Columns.Contains("total_rentals")) _reportData.Columns.Add("total_rentals", typeof(int));
            if (!_reportData.Columns.Contains("paid_rentals")) _reportData.Columns.Add("paid_rentals", typeof(int));
            if (!_reportData.Columns.Contains("total_revenue")) _reportData.Columns.Add("total_revenue", typeof(decimal));
            if (!_reportData.Columns.Contains("avg_ticket")) _reportData.Columns.Add("avg_ticket", typeof(decimal));
            if (!_reportData.Columns.Contains("pending_amount")) _reportData.Columns.Add("pending_amount", typeof(decimal));
        }

        private void BuildEmptyReportTable()
        {
            _reportData = new DataTable();
            _reportData.Columns.Add("period_label", typeof(string));
            _reportData.Columns.Add("total_rentals", typeof(int));
            _reportData.Columns.Add("paid_rentals", typeof(int));
            _reportData.Columns.Add("total_revenue", typeof(decimal));
            _reportData.Columns.Add("avg_ticket", typeof(decimal));
            _reportData.Columns.Add("pending_amount", typeof(decimal));
        }

        private void UpdateDashboard()
        {
            dgvReport.DataSource = null;
            dgvReport.Columns.Clear();

            var display = new DataTable();
            display.Columns.Add("Period", typeof(string));
            display.Columns.Add("Total Rentals", typeof(int));
            display.Columns.Add("Paid Rentals", typeof(int));
            display.Columns.Add("Avg Ticket", typeof(string));
            display.Columns.Add("Revenue", typeof(string));
            display.Columns.Add("Pending", typeof(string));

            decimal grandTotalRev = 0;
            int grandPaidRentals = 0;
            decimal grandTotalPending = 0;

            foreach (DataRow row in _reportData.Rows)
            {
                decimal rev = row["total_revenue"] != DBNull.Value ? Convert.ToDecimal(row["total_revenue"]) : 0;
                decimal pen = row["pending_amount"] != DBNull.Value ? Convert.ToDecimal(row["pending_amount"]) : 0;
                int totalRentals = row["total_rentals"] != DBNull.Value ? Convert.ToInt32(row["total_rentals"]) : 0;
                int paidRentals = row["paid_rentals"] != DBNull.Value ? Convert.ToInt32(row["paid_rentals"]) : 0;
                decimal avgTicket = row["avg_ticket"] != DBNull.Value ? Convert.ToDecimal(row["avg_ticket"]) : 0;

                grandTotalRev += rev;
                grandPaidRentals += paidRentals;
                grandTotalPending += pen;

                display.Rows.Add(
                    row["period_label"]?.ToString() ?? "",
                    totalRentals,
                    paidRentals,
                    $"₱ {avgTicket:N2}",
                    $"₱ {rev:N2}",
                    $"₱ {pen:N2}"
                );
            }

            dgvReport.DataSource = display;

            if (dgvReport.Columns.Count >= 6)
            {
                dgvReport.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                dgvReport.Columns[1].Width = 120;
                dgvReport.Columns[2].Width = 120;
                dgvReport.Columns[3].Width = 130;
                dgvReport.Columns[4].Width = 160;
                dgvReport.Columns[5].Width = 160;
            }

            lblTotalRev.Text = $"₱ {grandTotalRev:N2}";
            lblPaidRentals.Text = grandPaidRentals.ToString();
            lblPending.Text = $"₱ {grandTotalPending:N2}";

            pnlChartContainer.Invalidate();
        }

        // ══ CUSTOM CHART DRAWING ══
        private void DrawCustomBarChart(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, pnlChartContainer.Width - 1, pnlChartContainer.Height - 1);
            using var path = RoundRect(rect, 12);
            g.FillPath(new SolidBrush(ThemeManager.IsDarkMode ? WinColor.FromArgb(22, 22, 35) : WinColor.White), path);
            g.DrawPath(new Pen(ColBorder, 1), path);

            if (_reportData.Rows.Count == 0)
            {
                TextRenderer.DrawText(
                    g,
                    "No data available for the selected period.",
                    new Font("Segoe UI", 10F),
                    rect,
                    ColSub,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                );
                return;
            }

            g.DrawString($"Revenue Trend ({_currentPeriod})", new Font("Segoe UI", 12F, FontStyle.Bold), new SolidBrush(ColText), new PointF(20, 15));

            int paddingL = 60, paddingR = 20, paddingT = 60, paddingB = 40;
            int chartW = pnlChartContainer.Width - paddingL - paddingR;
            int chartH = pnlChartContainer.Height - paddingT - paddingB;

            var rows = _reportData.AsEnumerable().Reverse().ToArray();

            decimal maxVal = rows
                .Select(r => r["total_revenue"] != DBNull.Value ? Convert.ToDecimal(r["total_revenue"]) : 0m)
                .DefaultIfEmpty(0m)
                .Max();

            if (maxVal <= 0)
                maxVal = 1000;

            double exponent = Math.Floor(Math.Log10((double)maxVal));
            double fraction = (double)maxVal / Math.Pow(10, exponent);
            double niceFraction = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
            decimal niceMax = (decimal)(niceFraction * Math.Pow(10, exponent));

            using Pen gridPen = new Pen(
                ThemeManager.IsDarkMode ? WinColor.FromArgb(40, 40, 55) : WinColor.FromArgb(230, 230, 240), 1)
            { DashStyle = DashStyle.Dash };

            using SolidBrush textBrush = new SolidBrush(ColSub);
            using SolidBrush valueBrush = new SolidBrush(ColText);
            using Font labelFont = new Font("Segoe UI", 8F);

            for (int i = 0; i <= 4; i++)
            {
                float y = paddingT + chartH - (chartH * (i / 4f));
                decimal yVal = niceMax * (i / 4m);
                string yStr = yVal >= 1000 ? (yVal / 1000).ToString("0.#") + "k" : yVal.ToString("0");

                g.DrawLine(gridPen, paddingL, y, paddingL + chartW, y);
                g.DrawString(yStr, labelFont, textBrush, new PointF(paddingL - 40, y - 6));
            }

            int barCount = rows.Length;
            if (barCount == 0) return;

            float barSpace = chartW / (float)barCount;
            float barWidth = Math.Min(barSpace * 0.6f, 60);

            for (int i = 0; i < barCount; i++)
            {
                var r = rows[i];
                decimal val = r["total_revenue"] != DBNull.Value ? Convert.ToDecimal(r["total_revenue"]) : 0;

                float h = niceMax > 0 ? (float)(val / niceMax) * chartH : 0;
                float x = paddingL + (i * barSpace) + (barSpace - barWidth) / 2;
                float y = paddingT + chartH - h;

                RectangleF barRect = new RectangleF(x, y, barWidth, h);

                if (h > 0)
                {
                    int rVal = Math.Min(255, ColAccent.R + 40);
                    int gVal = Math.Min(255, ColAccent.G + 40);
                    int bVal = Math.Min(255, ColAccent.B + 40);

                    using LinearGradientBrush brush = new LinearGradientBrush(
                        barRect,
                        ColAccent,
                        WinColor.FromArgb(255, rVal, gVal, bVal),
                        LinearGradientMode.Vertical
                    );

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

                    if (barCount <= 15)
                    {
                        string vStr = val >= 1000 ? (val / 1000).ToString("0.#") + "k" : val.ToString("0");
                        var size = g.MeasureString(vStr, labelFont);
                        g.DrawString(vStr, labelFont, valueBrush, new PointF(x + (barWidth - size.Width) / 2, y - 16));
                    }
                }

                int labelStep = Math.Max(1, barCount / 6);
                if (barCount <= 12 || i % labelStep == 0)
                {
                    string xLbl = r["period_label"]?.ToString() ?? "";
                    if (xLbl.Length > 10) xLbl = xLbl.Substring(0, 10) + "..";
                    var xSize = g.MeasureString(xLbl, labelFont);
                    g.DrawString(xLbl, labelFont, textBrush, new PointF(x + (barWidth - xSize.Width) / 2, paddingT + chartH + 8));
                }
            }
        }

        // ══ EXPORT TO EXCEL-FRIENDLY XLS ══
        private void OnExportCSV(object s, EventArgs e)
        {
            if (_reportData == null || _reportData.Rows.Count == 0)
            {
                MessageBox.Show("No data to export.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "Excel files (*.xls)|*.xls",
                FileName = $"DriveAndGo_Sales_Report_{_currentPeriod}_{DateTime.Now:yyyyMMdd}.xls"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, BuildExcelReportHtml(), Encoding.UTF8);
                    MessageBox.Show("Excel export successful!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Export error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private string BuildExcelReportHtml()
        {
            decimal totalRevenue = 0;
            decimal totalPending = 0;
            int totalPaidRentals = 0;

            foreach (DataRow row in _reportData.Rows)
            {
                totalRevenue += row["total_revenue"] != DBNull.Value ? Convert.ToDecimal(row["total_revenue"]) : 0;
                totalPending += row["pending_amount"] != DBNull.Value ? Convert.ToDecimal(row["pending_amount"]) : 0;
                totalPaidRentals += row["paid_rentals"] != DBNull.Value ? Convert.ToInt32(row["paid_rentals"]) : 0;
            }

            string logoDataUri = ExportBrandingHelper.GetLogoDataUri();
            var html = new StringBuilder();

            html.AppendLine("<html xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:x=\"urn:schemas-microsoft-com:office:excel\">");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset=\"utf-8\" />");
            html.AppendLine("<meta name=\"ProgId\" content=\"Excel.Sheet\" />");
            html.AppendLine("<style>");
            html.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;background:#f5f6fb;color:#1f2330;margin:24px;}");
            html.AppendLine("table{border-collapse:collapse;width:100%;}");
            html.AppendLine(".header td{border:none;vertical-align:middle;}");
            html.AppendLine(".brand{background:#e65100;color:#fff;padding:16px 18px;}");
            html.AppendLine(".brand-title{font-size:24px;font-weight:700;letter-spacing:.3px;}");
            html.AppendLine(".brand-sub{font-size:12px;color:#ffd0b0;}");
            html.AppendLine(".section-title{font-size:18px;font-weight:700;margin:18px 0 6px 0;}");
            html.AppendLine(".meta{font-size:11px;color:#667085;margin-bottom:14px;}");
            html.AppendLine(".summary{margin:10px 0 18px 0;}");
            html.AppendLine(".summary td{padding:12px;border:1px solid #e6e8f0;background:#fff;}");
            html.AppendLine(".summary-label{font-size:11px;color:#667085;font-weight:600;text-transform:uppercase;}");
            html.AppendLine(".summary-value{font-size:18px;color:#1f2330;font-weight:700;}");
            html.AppendLine(".report th{background:#1f2330;color:#fff;padding:10px;border:1px solid #d9dbe7;font-size:11px;text-transform:uppercase;}");
            html.AppendLine(".report td{padding:9px;border:1px solid #d9dbe7;font-size:11px;background:#fff;}");
            html.AppendLine(".report tr:nth-child(even) td{background:#fafbff;}");
            html.AppendLine(".num{text-align:right;}");
            html.AppendLine(".center{text-align:center;}");
            html.AppendLine(".footer{margin-top:18px;font-size:10px;color:#667085;text-align:center;}");
            html.AppendLine("</style>");
            html.AppendLine("</head><body>");

            html.AppendLine("<table class=\"header\">");
            html.AppendLine("<tr>");
            html.AppendLine("<td style=\"width:92px;padding-right:12px;\">");

            if (!string.IsNullOrWhiteSpace(logoDataUri))
            {
                html.Append("<img src=\"")
                    .Append(logoDataUri)
                    .AppendLine("\" alt=\"Drive & Go Logo\" style=\"width:72px;height:auto;display:block;\" />");
            }
            else
            {
                html.AppendLine("<div style=\"width:72px;height:72px;line-height:72px;text-align:center;background:#fff3eb;border:1px solid #ffd3b6;color:#e65100;font-weight:700;font-size:22px;\">DG</div>");
            }

            html.AppendLine("</td>");
            html.AppendLine("<td class=\"brand\">");
            html.AppendLine("<div class=\"brand-title\">DRIVE &amp; GO</div>");
            html.AppendLine("<div class=\"brand-sub\">Sales &amp; Analytics Report</div>");
            html.AppendLine("<div class=\"brand-sub\">San Jose del Monte, Bulacan</div>");
            html.AppendLine("</td>");
            html.AppendLine("</tr>");
            html.AppendLine("</table>");

            html.Append("<div class=\"section-title\">")
                .Append(ExportBrandingHelper.EscapeHtml(_currentPeriod))
                .AppendLine(" Sales Report</div>");

            html.Append("<div class=\"meta\">Generated on ")
                .Append(ExportBrandingHelper.EscapeHtml(DateTime.Now.ToString("MMMM dd, yyyy hh:mm tt")))
                .AppendLine("</div>");

            html.AppendLine("<table class=\"summary\">");
            html.AppendLine("<tr>");
            html.AppendLine($"<td><div class=\"summary-label\">Total Revenue</div><div class=\"summary-value\">PHP {totalRevenue:N2}</div></td>");
            html.AppendLine($"<td><div class=\"summary-label\">Paid Rentals</div><div class=\"summary-value\">{totalPaidRentals:N0}</div></td>");
            html.AppendLine($"<td><div class=\"summary-label\">Pending Amount</div><div class=\"summary-value\">PHP {totalPending:N2}</div></td>");
            html.AppendLine("</tr>");
            html.AppendLine("</table>");

            html.AppendLine("<table class=\"report\">");
            html.AppendLine("<tr>");
            html.AppendLine("<th>Period</th>");
            html.AppendLine("<th>Total Rentals</th>");
            html.AppendLine("<th>Paid Rentals</th>");
            html.AppendLine("<th>Average Ticket</th>");
            html.AppendLine("<th>Revenue (PHP)</th>");
            html.AppendLine("<th>Pending (PHP)</th>");
            html.AppendLine("</tr>");

            foreach (DataRow row in _reportData.Rows)
            {
                decimal avgTicket = row["avg_ticket"] != DBNull.Value ? Convert.ToDecimal(row["avg_ticket"]) : 0;
                decimal revenue = row["total_revenue"] != DBNull.Value ? Convert.ToDecimal(row["total_revenue"]) : 0;
                decimal pending = row["pending_amount"] != DBNull.Value ? Convert.ToDecimal(row["pending_amount"]) : 0;

                html.AppendLine("<tr>");
                ExportBrandingHelper.AppendExcelCell(html, row["period_label"]?.ToString() ?? string.Empty, string.Empty);
                ExportBrandingHelper.AppendExcelCell(html, row["total_rentals"]?.ToString() ?? "0", "center");
                ExportBrandingHelper.AppendExcelCell(html, row["paid_rentals"]?.ToString() ?? "0", "center");
                ExportBrandingHelper.AppendExcelCell(html, avgTicket.ToString("N2"), "num");
                ExportBrandingHelper.AppendExcelCell(html, revenue.ToString("N2"), "num");
                ExportBrandingHelper.AppendExcelCell(html, pending.ToString("N2"), "num");
                html.AppendLine("</tr>");
            }

            html.AppendLine("</table>");
            html.AppendLine("<div class=\"footer\">Drive &amp; Go System Report Export</div>");
            html.AppendLine("</body></html>");

            return html.ToString();
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
                var writerProps = new WriterProperties();
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

                var headerTable = new iText.Layout.Element.Table(new float[] { 1, 5 }).UseAllAvailableWidth();

                Cell logoCell = new Cell()
                    .SetBackgroundColor(orange)
                    .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetPaddingTop(10);

                string logoPath = ExportBrandingHelper.ResolveLogoPath();

                if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
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

                doc.Add(new Paragraph($"{_currentPeriod.ToUpper()} SALES REPORT")
                    .SetFontSize(16).SetFont(fontBold).SetFontColor(dark)
                    .SetTextAlignment(TextAlignment.CENTER));

                doc.Add(new Paragraph($"Generated on: {DateTime.Now:MMMM dd, yyyy hh:mm tt}")
                    .SetFontSize(9).SetFontColor(gray).SetFont(fontNormal)
                    .SetTextAlignment(TextAlignment.CENTER));

                doc.Add(new Paragraph(" "));

                var dataTable = new iText.Layout.Element.Table(new float[] { 3, 2, 2, 2, 3, 3 })
                    .UseAllAvailableWidth()
                    .SetMarginBottom(12);

                string[] headers = { "Period", "Total Rentals", "Paid Rentals", "Avg Ticket", "Revenue (PHP)", "Pending (PHP)" };
                foreach (var h in headers)
                {
                    dataTable.AddHeaderCell(
                        new Cell()
                            .SetBackgroundColor(dark)
                            .SetFontColor(white)
                            .SetPadding(6)
                            .Add(new Paragraph(h).SetFont(fontBold).SetFontSize(10))
                    );
                }

                decimal totalRev = 0;
                decimal totalPending = 0;
                int totalPaidRentals = 0;

                foreach (DataRow row in _reportData.Rows)
                {
                    decimal rev = row["total_revenue"] != DBNull.Value ? Convert.ToDecimal(row["total_revenue"]) : 0;
                    decimal avgTicket = row["avg_ticket"] != DBNull.Value ? Convert.ToDecimal(row["avg_ticket"]) : 0;
                    decimal pending = row["pending_amount"] != DBNull.Value ? Convert.ToDecimal(row["pending_amount"]) : 0;
                    totalRev += rev;
                    totalPending += pending;
                    totalPaidRentals += row["paid_rentals"] != DBNull.Value ? Convert.ToInt32(row["paid_rentals"]) : 0;

                    dataTable.AddCell(new Cell().SetPadding(5).Add(new Paragraph(row["period_label"]?.ToString() ?? "").SetFont(fontNormal).SetFontSize(9)));
                    dataTable.AddCell(new Cell().SetPadding(5).SetTextAlignment(TextAlignment.CENTER).Add(new Paragraph(row["total_rentals"]?.ToString() ?? "0").SetFont(fontNormal).SetFontSize(9)));
                    dataTable.AddCell(new Cell().SetPadding(5).SetTextAlignment(TextAlignment.CENTER).Add(new Paragraph(row["paid_rentals"]?.ToString() ?? "0").SetFont(fontNormal).SetFontSize(9)));
                    dataTable.AddCell(new Cell().SetPadding(5).SetTextAlignment(TextAlignment.RIGHT).Add(new Paragraph(avgTicket.ToString("N2")).SetFont(fontNormal).SetFontSize(9)));
                    dataTable.AddCell(new Cell().SetPadding(5).SetTextAlignment(TextAlignment.RIGHT).Add(new Paragraph(rev.ToString("N2")).SetFont(fontNormal).SetFontSize(9)));
                    dataTable.AddCell(new Cell().SetPadding(5).SetTextAlignment(TextAlignment.RIGHT).Add(new Paragraph(pending.ToString("N2")).SetFont(fontNormal).SetFontSize(9).SetFontColor(new iText.Kernel.Colors.DeviceRgb(200, 50, 50))));
                }

                doc.Add(dataTable);

                doc.Add(new Paragraph($"GRAND TOTAL REVENUE: PHP {totalRev:N2}")
                    .SetFont(fontBold)
                    .SetFontSize(14)
                    .SetFontColor(orange)
                    .SetTextAlignment(TextAlignment.RIGHT)
                    .SetMarginTop(10));

                doc.Add(new Paragraph($"TOTAL PAID RENTALS: {totalPaidRentals:N0}    |    TOTAL PENDING AMOUNT: PHP {totalPending:N2}")
                    .SetFont(fontNormal)
                    .SetFontSize(10)
                    .SetFontColor(gray)
                    .SetTextAlignment(TextAlignment.RIGHT)
                    .SetMarginTop(4));

                doc.Add(new Paragraph("Drive & Go System - Confidential Report")
                    .SetFontSize(8)
                    .SetFont(fontNormal)
                    .SetFontColor(gray)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginTop(30));

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

        protected override void Dispose(bool disposing)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }
    }
}