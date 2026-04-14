#nullable disable
using DriveAndGo_Admin.Helpers;
using System;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MySql.Data.MySqlClient;

namespace DriveAndGo_Admin.Panels
{
    public class DashboardPanel : UserControl
    {
        // ── Dynamic theme colors (reads from ThemeManager) ──
        private Color ColBg => ThemeManager.CurrentBackground;
        private Color ColCard => ThemeManager.CurrentCard;
        private Color ColText => ThemeManager.CurrentText;
        private Color ColSub => ThemeManager.CurrentSubText;
        private Color ColBorder => ThemeManager.CurrentBorder;
        private Color ColAccent => ThemeManager.CurrentPrimary;

        // ── Fixed accent colors ──
        private readonly Color ColBlue = Color.FromArgb(59, 130, 246);
        private readonly Color ColGreen = Color.FromArgb(34, 197, 94);
        private readonly Color ColPurple = Color.FromArgb(168, 85, 247);
        private readonly Color ColRed = Color.FromArgb(239, 68, 68);
        private readonly Color ColYellow = Color.FromArgb(234, 179, 8);

        private readonly string _connStr =
            "Server=localhost;Database=vehicle_rental_db;Uid=root;Pwd=;";

        // ── Stat values ──
        private int _totalVehicles = 0;
        private int _activeRentals = 0;
        private int _availDrivers = 0;
        private decimal _todayRevenue = 0;
        private int _pendingBookings = 0;
        private int _pendingPayments = 0;
        private int _overdueRentals = 0;
        private int _openIssues = 0;

        // ── Layout ──
        private Panel _scrollContainer;
        private Panel[] _statCards = new Panel[8];
        private Panel _bookingsCard;
        private Panel _quickStatsCard;
        private Panel _fleetCard;
        private Panel _pendingCard;

        // ── Animation ──
        private System.Windows.Forms.Timer _entranceTimer;
        private float[] _cardAlpha;
        private float[] _cardOffsetY;
        private int _cardsDone = 0;

        // ── Hover lift per card ──
        private class HoverState
        {
            public float Lift = 0f;
            public bool Hovered = false;
            public System.Windows.Forms.Timer Timer;
        }

        public DashboardPanel()
        {
            Dock = DockStyle.Fill;
            DoubleBuffered = true;
            BackColor = ColBg;

            Resize += (s, e) => RelayoutAll();
            ThemeManager.ThemeChanged += ThemeChanged_Handler;

            LoadStatsFromDB();
            BuildScrollContainer();
            BuildUI();
            StartEntranceAnimation();
        }

        private void ThemeChanged_Handler(object sender, EventArgs e)
        {
            try
            {
                BackColor = ColBg;
                if (_scrollContainer != null)
                {
                    var scrollPos = new Point(
                        Math.Abs(_scrollContainer.AutoScrollPosition.X),
                        Math.Abs(_scrollContainer.AutoScrollPosition.Y));

                    _scrollContainer.BackColor = ColBg;
                    _scrollContainer.Controls.Clear();
                    _statCards = new Panel[8];

                    LoadStatsFromDB();
                    BuildUI();
                    StartEntranceAnimation();

                    _scrollContainer.AutoScrollPosition = scrollPos;
                }
            }
            catch { }
        }

        // ══════════════════════════════════════════════
        //  SCROLL CONTAINER
        // ══════════════════════════════════════════════
        private void BuildScrollContainer()
        {
            _scrollContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = ColBg
            };

            SetDoubleBuffer(_scrollContainer);
            Controls.Add(_scrollContainer);
        }

        // ══════════════════════════════════════════════
        //  LOAD DATA
        // ══════════════════════════════════════════════
        private void LoadStatsFromDB()
        {
            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();

                T Query<T>(string sql)
                {
                    using var cmd = new MySqlCommand(sql, conn);
                    var result = cmd.ExecuteScalar();
                    if (result == null || result == DBNull.Value) return default(T);
                    return (T)Convert.ChangeType(result, typeof(T));
                }

                // Vehicles
                _totalVehicles = Query<int>("SELECT COUNT(*) FROM vehicles");

                // Rentals status
                _activeRentals = Query<int>(@"
                    SELECT COUNT(*)
                    FROM rentals
                    WHERE LOWER(TRIM(COALESCE(status,''))) IN ('approved','active')");

                _pendingBookings = Query<int>(@"
                    SELECT COUNT(*)
                    FROM rentals
                    WHERE LOWER(TRIM(COALESCE(status,''))) = 'pending'");

                _overdueRentals = Query<int>(@"
                    SELECT COUNT(*)
                    FROM rentals
                    WHERE LOWER(TRIM(COALESCE(status,''))) IN ('approved','active')
                      AND end_date IS NOT NULL
                      AND DATE(end_date) < CURDATE()");

                // Drivers
                _availDrivers = Query<int>(@"
                    SELECT COUNT(*)
                    FROM drivers
                    WHERE LOWER(TRIM(COALESCE(status,''))) IN ('active')");

                // Payments from rentals table
                _pendingPayments = Query<int>(@"
                    SELECT COUNT(*)
                    FROM rentals
                    WHERE LOWER(TRIM(COALESCE(payment_status,''))) <> 'paid'");

                _todayRevenue = Query<decimal>(@"
                    SELECT COALESCE(SUM(total_amount), 0)
                    FROM rentals
                    WHERE LOWER(TRIM(COALESCE(payment_status,''))) = 'paid'
                      AND DATE(COALESCE(created_at, start_date)) = CURDATE()");

                // Issues
                _openIssues = Query<int>(@"
                    SELECT COUNT(*)
                    FROM issues
                    WHERE LOWER(TRIM(COALESCE(status,''))) <> 'resolved'");
            }
            catch (Exception ex)
            {
                Console.WriteLine("DB: " + ex.Message);
            }
        }

        // ══════════════════════════════════════════════
        //  BUILD UI
        // ══════════════════════════════════════════════
        private void BuildUI()
        {
            _scrollContainer.Controls.Clear();

            BuildHeader();
            BuildStatCards();
            BuildRecentBookings();
            BuildQuickStats();
            BuildVehicleStatus();
            BuildPendingActions();

            RelayoutAll();
        }

        // ══════════════════════════════════════════════
        //  RESPONSIVE RELAYOUT
        // ══════════════════════════════════════════════
        private void RelayoutAll()
        {
            int W = _scrollContainer.ClientSize.Width;
            if (W < 10) return;

            int pad = 24;
            int gap = 16;
            int usable = W - pad * 2;

            int minCard = 160;
            int cols = Math.Max(1, Math.Min(6, usable / (minCard + gap)));
            int cardW = (usable - gap * (cols - 1)) / cols;
            int cardH = 118;

            int statTop = 110;

            for (int i = 0; i < _statCards.Length; i++)
            {
                if (_statCards[i] == null) continue;
                int col = i % cols;
                int row = i / cols;
                int x = pad + col * (cardW + gap);
                int y = statTop + row * (cardH + gap);
                _statCards[i].Location = new Point(x, y);
                _statCards[i].Width = cardW;
            }

            int rows2Start = statTop + (((_statCards.Length - 1) / cols) + 1) * (cardH + gap) + gap;

            bool wide = W >= 900;
            int bkW = wide ? (int)(usable * 0.68) : usable;
            int qsW = wide ? usable - bkW - gap : usable;
            int row2H = 456;

            if (_bookingsCard != null)
            {
                _bookingsCard.Location = new Point(pad, rows2Start);
                _bookingsCard.Size = new Size(bkW, row2H);

                foreach (Control c in _bookingsCard.Controls)
                {
                    if (c is DataGridView dgv)
                    {
                        dgv.Size = new Size(bkW - 40, row2H - 52);
                        dgv.Location = new Point(20, 52);
                    }
                }
            }

            if (_quickStatsCard != null)
            {
                _quickStatsCard.Location = wide
                    ? new Point(pad + bkW + gap, rows2Start)
                    : new Point(pad, rows2Start + row2H + gap);

                _quickStatsCard.Size = new Size(qsW, row2H);

                foreach (Control c in _quickStatsCard.Controls)
                {
                    if (c is Panel row && row.Tag?.ToString() == "qsrow")
                        row.Width = qsW - 40;
                }
            }

            int row3Start = rows2Start + row2H + gap + (wide ? 0 : row2H + gap);
            int row3H = 200;
            int halfW = (usable - gap) / 2;

            if (_fleetCard != null)
            {
                _fleetCard.Location = new Point(pad, row3Start);
                _fleetCard.Size = new Size(wide ? halfW : usable, row3H);
            }

            if (_pendingCard != null)
            {
                _pendingCard.Location = wide
                    ? new Point(pad + halfW + gap, row3Start)
                    : new Point(pad, row3Start + row3H + gap);

                _pendingCard.Size = new Size(wide ? halfW : usable, row3H);
            }

            int bottom = row3Start + row3H + (wide ? 0 : row3H + gap) + 40;
            _scrollContainer.AutoScrollMinSize = new Size(0, bottom);
            _scrollContainer.Invalidate(true);
        }

        // ══════════════════════════════════════════════
        //  HEADER
        // ══════════════════════════════════════════════
        private void BuildHeader()
        {
            var pnl = new Panel
            {
                Location = new Point(24, 16),
                Size = new Size(_scrollContainer.Width - 48, 80),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            var lblTitle = new Label
            {
                Text = "Dashboard Overview",
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = ColText,
                AutoSize = true,
                Location = new Point(0, 2),
                BackColor = Color.Transparent
            };

            var lblSub = new Label
            {
                Text = "Welcome back! Here's your fleet and revenue summary for today.",
                Font = new Font("Segoe UI", 11F),
                ForeColor = ColSub,
                AutoSize = true,
                Location = new Point(2, 38),
                BackColor = Color.Transparent
            };

            var lblDate = new Label
            {
                Text = DateTime.Now.ToString("dddd, MMMM dd, yyyy"),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ColAccent,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            var btnRefresh = new Button
            {
                Text = "⟳  Refresh",
                Size = new Size(110, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = ColAccent,
                Font = new Font("Segoe UI", 10F),
                Cursor = Cursors.Hand
            };

            btnRefresh.FlatAppearance.BorderColor = ColAccent;
            btnRefresh.FlatAppearance.BorderSize = 1;
            btnRefresh.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, ColAccent);

            pnl.Resize += (s, e) =>
            {
                btnRefresh.Location = new Point(Math.Max(8, pnl.ClientSize.Width - btnRefresh.Width - 10), 7);
                lblDate.Location = new Point(Math.Max(8, btnRefresh.Left - lblDate.Width - 12), 15);
            };

            btnRefresh.Click += (s, e) =>
            {
                lblDate.Text = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
                LoadStatsFromDB();
                _scrollContainer.Controls.Clear();
                _statCards = new Panel[8];
                BackColor = ColBg;
                _scrollContainer.BackColor = ColBg;
                BuildUI();
                StartEntranceAnimation();
            };

            pnl.Controls.Add(lblTitle);
            pnl.Controls.Add(lblSub);
            pnl.Controls.Add(lblDate);
            pnl.Controls.Add(btnRefresh);
            _scrollContainer.Controls.Add(pnl);
        }

        // ══════════════════════════════════════════════
        //  STAT CARDS
        // ══════════════════════════════════════════════
        private void BuildStatCards()
        {
            var cards = new[]
            {
                ("Total Fleet",       "🚗", _totalVehicles.ToString(),          "All vehicles",       ColBlue),
                ("Active Rentals",    "🔑", _activeRentals.ToString(),          "Currently active",   ColGreen),
                ("Avail. Drivers",    "👤", _availDrivers.ToString(),           "Ready to deploy",    ColPurple),
                ("Today's Revenue",   "₱",  "₱" + _todayRevenue.ToString("N2"), "Paid rentals only",  ColAccent),
                ("Pending Bookings",  "📋", _pendingBookings.ToString(),        "Needs approval",     ColRed),
                ("Pending Payments",  "💳", _pendingPayments.ToString(),        "Unpaid rentals",     ColYellow),
                ("Overdue Rentals",   "⏰", _overdueRentals.ToString(),         "Needs follow-up",    ColRed),
                ("Open Issues",       "🛠", _openIssues.ToString(),             "Reported incidents", ColYellow),
            };

            _cardAlpha = new float[cards.Length];
            _cardOffsetY = new float[cards.Length];
            for (int i = 0; i < cards.Length; i++) _cardOffsetY[i] = 28f;

            for (int i = 0; i < cards.Length; i++)
            {
                var (title, icon, value, sub, color) = cards[i];
                var card = CreateStatCard(title, icon, value, sub, 190, 118, color, i);
                _statCards[i] = card;
                _scrollContainer.Controls.Add(card);
            }
        }

        private Panel CreateStatCard(string title, string icon, string value, string sub, int w, int h, Color accentColor, int idx)
        {
            var card = new Panel
            {
                Size = new Size(w, h),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            SetDoubleBuffer(card);

            var hs = new HoverState();
            hs.Timer = new System.Windows.Forms.Timer { Interval = 12 };
            hs.Timer.Tick += (s, e) =>
            {
                float target = hs.Hovered ? 6f : 0f;
                float diff = target - hs.Lift;
                if (Math.Abs(diff) < 0.2f) { hs.Lift = target; hs.Timer.Stop(); }
                else hs.Lift += diff * 0.28f;
                card.Invalidate();
            };
            card.MouseEnter += (s, e) => { hs.Hovered = true; hs.Timer.Start(); };
            card.MouseLeave += (s, e) => { hs.Hovered = false; hs.Timer.Start(); };

            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                float alpha = _cardAlpha[idx];
                float offset = _cardOffsetY[idx];

                int drawY = (int)(offset * (1f - alpha));
                var drawR = new Rectangle(0, drawY, card.Width - 2, h - 2 - drawY);
                if (drawR.Height < 10) return;

                var path = GetRoundedRect(drawR, 14);

                if (hs.Lift > 0.5f)
                {
                    var shadowR = new Rectangle(drawR.X + 4, drawR.Y + (int)hs.Lift + 6, drawR.Width - 4, drawR.Height - 4);
                    using var shadowPath = GetRoundedRect(shadowR, 14);
                    using var shadowBr = new PathGradientBrush(shadowPath);
                    shadowBr.CenterColor = Color.FromArgb(60, 0, 0, 0);
                    shadowBr.SurroundColors = new[] { Color.Transparent };
                    g.FillPath(shadowBr, shadowPath);
                }

                var state = g.Save();
                g.TranslateTransform(0, -hs.Lift);

                bool dark = ThemeManager.IsDarkMode;
                Color c1 = dark ? Color.FromArgb(32, 32, 48) : Color.FromArgb(255, 255, 255);
                Color c2 = dark ? Color.FromArgb(20, 20, 32) : Color.FromArgb(245, 245, 252);
                using var bg = new LinearGradientBrush(drawR, c1, c2, LinearGradientMode.Vertical);
                g.FillPath(bg, path);

                g.FillRectangle(new SolidBrush(accentColor), drawR.X, drawR.Y + drawR.Height / 4, 3, drawR.Height / 2);

                using var hiPen = new Pen(Color.FromArgb(dark ? 20 : 60, 255, 255, 255), 1f);
                g.DrawLine(hiPen, drawR.X + 14, drawR.Y + 1, drawR.Right - 14, drawR.Y + 1);

                using var borderPen = new Pen(Color.FromArgb(dark ? 30 : 180, ThemeManager.CurrentBorder), 1f);
                g.DrawPath(borderPen, path);

                using var glowPath = new GraphicsPath();
                glowPath.AddEllipse(drawR.X - 10, drawR.Y - 10, 100, 80);
                using var glowBr = new PathGradientBrush(glowPath);
                glowBr.CenterColor = Color.FromArgb((int)(12 * alpha), accentColor);
                glowBr.SurroundColors = new[] { Color.Transparent };
                g.FillPath(glowBr, glowPath);

                g.Restore(state);
            };

            // Mas ilayo ang icon sa text
            var pnlIcon = new Panel
            {
                Size = new Size(40, 40),
                Location = new Point(w - 54, 12),
                BackColor = Color.Transparent
            };
            SetDoubleBuffer(pnlIcon);
            pnlIcon.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillEllipse(new SolidBrush(Color.FromArgb(40, accentColor)), 0, 0, 40, 40);
                e.Graphics.FillEllipse(new SolidBrush(Color.FromArgb(15, accentColor)), 5, 5, 30, 30);
            };

            var lblIcon = new Label
            {
                Text = icon,
                Font = new Font("Segoe UI Emoji", 13F),
                ForeColor = accentColor,
                AutoSize = true,
                Location = new Point(8, 7),
                BackColor = Color.Transparent
            };
            pnlIcon.Controls.Add(lblIcon);

            // FIX: bigyan ng exact width para hindi sumapaw sa icon
            var lblValue = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 17F, FontStyle.Bold),
                ForeColor = ColText,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Location = new Point(14, 12),
                Size = new Size(w - 78, 34),
                BackColor = Color.Transparent
            };

            var lblTitle2 = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ColSub,
                AutoSize = false,
                Location = new Point(15, 54),
                Size = new Size(w - 30, 18),
                BackColor = Color.Transparent
            };

            var lblSub2 = new Label
            {
                Text = sub,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(ThemeManager.IsDarkMode ? 70 : 140, ColSub),
                AutoSize = false,
                Location = new Point(15, 72),
                Size = new Size(w - 30, 16),
                BackColor = Color.Transparent
            };

            card.Controls.Add(pnlIcon);
            card.Controls.Add(lblValue);
            card.Controls.Add(lblTitle2);
            card.Controls.Add(lblSub2);
            return card;
        }

        // ══════════════════════════════════════════════
        //  RECENT BOOKINGS
        // ══════════════════════════════════════════════
        private void BuildRecentBookings()
        {
            _bookingsCard = CreateCard("Recent Bookings");

            var dgv = new DataGridView();
            dgv.Location = new Point(20, 52);
            dgv.Size = new Size(700, 228);
            dgv.BackgroundColor = ColCard;
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.GridColor = ThemeManager.IsDarkMode ? Color.FromArgb(32, 32, 48) : Color.FromArgb(220, 220, 230);
            dgv.RowHeadersVisible = false;
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.ReadOnly = true;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.Font = new Font("Segoe UI", 9F);
            dgv.EnableHeadersVisualStyles = false;
            dgv.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            bool dark = ThemeManager.IsDarkMode;
            dgv.DefaultCellStyle.BackColor = dark ? Color.FromArgb(22, 22, 35) : Color.White;
            dgv.DefaultCellStyle.ForeColor = ColText;
            dgv.DefaultCellStyle.SelectionBackColor = dark ? Color.FromArgb(40, 40, 58) : Color.FromArgb(230, 240, 255);
            dgv.DefaultCellStyle.SelectionForeColor = ColAccent;
            dgv.DefaultCellStyle.Padding = new Padding(6, 4, 6, 4);

            dgv.ColumnHeadersDefaultCellStyle.BackColor = dark ? Color.FromArgb(18, 18, 28) : Color.FromArgb(245, 245, 250);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = ColSub;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(6);
            dgv.ColumnHeadersHeight = 36;
            dgv.RowTemplate.Height = 36;

            dgv.CellFormatting += (s, e) =>
            {
                if (dgv.Columns.Count > 4 && e.ColumnIndex == 5 && e.Value != null)
                {
                    e.CellStyle.ForeColor = e.Value.ToString()!.ToLower() switch
                    {
                        "approved" => ColGreen,
                        "pending" => ColYellow,
                        "completed" => ColBlue,
                        "rejected" => ColRed,
                        "active" => ColAccent,
                        _ => ColText
                    };
                    e.CellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                }
            };

            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();
                var cmd = new MySqlCommand(@"
                    SELECT r.rental_id AS '#',
                           u.full_name AS 'Customer',
                           CONCAT(v.brand,' ',v.model) AS 'Vehicle',
                           r.start_date AS 'Start',
                           r.end_date AS 'End',
                           r.status AS 'Status',
                           CONCAT('₱',FORMAT(r.total_amount,2)) AS 'Amount'
                    FROM rentals r
                    JOIN users u ON r.customer_id = u.user_id
                    JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                    ORDER BY COALESCE(r.created_at, r.start_date) DESC
                    LIMIT 12", conn);
                using var adapter = new MySqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);
                dgv.DataSource = dt;
            }
            catch (Exception ex)
            {
                AddErrorLabel(_bookingsCard, ex.Message);
            }

            _bookingsCard.Controls.Add(dgv);
            _scrollContainer.Controls.Add(_bookingsCard);
        }

        // ══════════════════════════════════════════════
        //  QUICK STATS
        // ══════════════════════════════════════════════
        private void BuildQuickStats()
        {
            _quickStatsCard = CreateCard("Quick Stats");

            decimal monthRevenue = 0;
            int totalUsers = 0, totalRatings = 0, dueToday = 0, overdue = 0, pendingExtensions = 0, openIssues = 0;
            decimal avgRating = 0;
            string topDriverName = "No driver ratings yet";
            decimal topDriverRating = 0;

            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();

                T Q<T>(string sql)
                {
                    using var c = new MySqlCommand(sql, conn);
                    var res = c.ExecuteScalar();
                    return res == DBNull.Value || res == null ? default(T) : (T)Convert.ChangeType(res, typeof(T));
                }

                monthRevenue = Q<decimal>(@"
                    SELECT COALESCE(SUM(total_amount), 0)
                    FROM rentals
                    WHERE LOWER(TRIM(COALESCE(payment_status, ''))) = 'paid'
                      AND MONTH(COALESCE(created_at, start_date)) = MONTH(CURDATE())
                      AND YEAR(COALESCE(created_at, start_date)) = YEAR(CURDATE())");

                totalUsers = Q<int>(@"
                    SELECT COUNT(*)
                    FROM users
                    WHERE LOWER(TRIM(COALESCE(role, ''))) = 'customer'");

                totalRatings = Q<int>(@"
                    SELECT COUNT(*)
                    FROM ratings");

                avgRating = Q<decimal>(@"
                    SELECT COALESCE(AVG(vehicle_score), 0)
                    FROM ratings");

                dueToday = Q<int>(@"
                    SELECT COUNT(*)
                    FROM rentals
                    WHERE LOWER(TRIM(COALESCE(status, ''))) IN ('approved', 'active')
                      AND end_date IS NOT NULL
                      AND DATE(end_date) = CURDATE()");

                overdue = Q<int>(@"
                    SELECT COUNT(*)
                    FROM rentals
                    WHERE LOWER(TRIM(COALESCE(status, ''))) IN ('approved', 'active')
                      AND end_date IS NOT NULL
                      AND DATE(end_date) < CURDATE()");

                pendingExtensions = Q<int>(@"
                    SELECT COUNT(*)
                    FROM extensions
                    WHERE LOWER(TRIM(COALESCE(status, ''))) = 'pending'");

                openIssues = Q<int>(@"
                    SELECT COUNT(*)
                    FROM issues
                    WHERE LOWER(TRIM(COALESCE(status, ''))) <> 'resolved'");

                using var topDriverCmd = new MySqlCommand(@"
                    SELECT u.full_name, ROUND(AVG(r.driver_score), 1) AS avg_rating
                    FROM ratings r
                    JOIN drivers d ON r.driver_id = d.driver_id
                    JOIN users u ON d.user_id = u.user_id
                    WHERE r.driver_score IS NOT NULL
                    GROUP BY u.user_id, u.full_name
                    ORDER BY avg_rating DESC, COUNT(*) DESC
                    LIMIT 1", conn);

                using var topDriverReader = topDriverCmd.ExecuteReader();
                if (topDriverReader.Read())
                {
                    topDriverName = topDriverReader["full_name"]?.ToString() ?? topDriverName;
                    topDriverRating = topDriverReader["avg_rating"] == DBNull.Value ? 0 : Convert.ToDecimal(topDriverReader["avg_rating"]);
                }
            }
            catch
            {
            }

            var items = new[]
            {
                ("Monthly Revenue",   "₱" + monthRevenue.ToString("N2"), ColAccent),
                ("Total Customers",   totalUsers.ToString(), ColBlue),
                ("Total Reviews",     totalRatings.ToString(), ColPurple),
                ("Avg. Rating",       avgRating.ToString("0.0") + " / 5.0", ColGreen),
                ("Due Today / Overdue", $"{dueToday} due  ·  {overdue} overdue", overdue > 0 ? ColRed : ColYellow),
                ("Ops Queue",         $"{pendingExtensions} extensions  ·  {openIssues} issues", (pendingExtensions + openIssues) > 0 ? ColYellow : ColGreen),
                ("Top Driver",        topDriverRating > 0 ? $"{topDriverName}  ·  {topDriverRating:0.0}★" : topDriverName, ColBlue),
            };

            int itemY = 56;
            foreach (var (label, val, color) in items)
            {
                var row = new Panel
                {
                    Size = new Size(280, 46),
                    Location = new Point(20, itemY),
                    BackColor = Color.Transparent,
                    Tag = "qsrow"
                };
                SetDoubleBuffer(row);

                row.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    var rect = new Rectangle(0, 0, row.Width - 1, row.Height - 1);
                    var path = GetRoundedRect(rect, 8);
                    bool d = ThemeManager.IsDarkMode;
                    g.FillPath(new SolidBrush(d ? Color.FromArgb(20, 20, 32) : Color.FromArgb(248, 248, 255)), path);
                    g.FillRectangle(new SolidBrush(color), 0, 10, 3, 26);
                    g.DrawPath(new Pen(Color.FromArgb(d ? 25 : 180, ThemeManager.CurrentBorder), 0.5f), path);
                };

                var lblKey = new Label
                {
                    Text = label,
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = ColSub,
                    AutoSize = true,
                    Location = new Point(14, 5),
                    BackColor = Color.Transparent
                };

                var lblVal = new Label
                {
                    Text = val,
                    Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                    ForeColor = color,
                    AutoSize = true,
                    Location = new Point(14, 22),
                    BackColor = Color.Transparent
                };

                row.Controls.Add(lblKey);
                row.Controls.Add(lblVal);
                _quickStatsCard.Controls.Add(row);
                itemY += 56;
            }

            _scrollContainer.Controls.Add(_quickStatsCard);
        }

        // ══════════════════════════════════════════════
        //  VEHICLE STATUS
        // ══════════════════════════════════════════════
        private void BuildVehicleStatus()
        {
            _fleetCard = CreateCard("Fleet Status");

            int available = 0, rented = 0, maintenance = 0, retired = 0;
            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();

                T Q<T>(string sql)
                {
                    using var c = new MySqlCommand(sql, conn);
                    var res = c.ExecuteScalar();
                    return res == DBNull.Value || res == null ? default(T) : (T)Convert.ChangeType(res, typeof(T));
                }

                available = Q<int>("SELECT COUNT(*) FROM vehicles WHERE LOWER(TRIM(COALESCE(status,'')))='available'");
                rented = Q<int>("SELECT COUNT(*) FROM vehicles WHERE LOWER(TRIM(COALESCE(status,''))) IN ('rented','in-use')");
                maintenance = Q<int>("SELECT COUNT(*) FROM vehicles WHERE LOWER(TRIM(COALESCE(status,'')))='maintenance'");
                retired = Q<int>("SELECT COUNT(*) FROM vehicles WHERE LOWER(TRIM(COALESCE(status,'')))='retired'");
            }
            catch { }

            int total = Math.Max(available + rented + maintenance + retired, 1);
            var statuses = new[]
            {
                ("Available",   available,   ColGreen),
                ("Rented",      rented,      ColBlue),
                ("Maintenance", maintenance, ColYellow),
                ("Retired",     retired,     ColRed),
            };

            int barY = 52;
            foreach (var (label, count, color) in statuses)
            {
                float pct = (float)count / total;

                var lblLabel = new Label { Text = label, Font = new Font("Segoe UI", 9F), ForeColor = ColSub, AutoSize = true, Location = new Point(20, barY), BackColor = Color.Transparent };
                var lblCount = new Label { Text = count + " units", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = color, AutoSize = true, BackColor = Color.Transparent };
                lblCount.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                lblCount.Location = new Point(_fleetCard.Width - 90, barY);

                var track = new Panel { Size = new Size(_fleetCard.Width - 50, 8), Location = new Point(20, barY + 20), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                track.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(30, 30, 45) : Color.FromArgb(220, 220, 235);
                SetDoubleBuffer(track);

                float _fillPct = 0f;
                var fill = new Panel { Size = new Size(4, 8), Location = new Point(0, 0), BackColor = color };
                SetDoubleBuffer(fill);
                fill.Paint += (s, e) =>
                {
                    using var brush = new LinearGradientBrush(fill.ClientRectangle, Color.FromArgb(180, color), color, LinearGradientMode.Horizontal);
                    e.Graphics.FillRectangle(brush, fill.ClientRectangle);
                };

                track.Controls.Add(fill);
                _fleetCard.Controls.Add(lblLabel);
                _fleetCard.Controls.Add(lblCount);
                _fleetCard.Controls.Add(track);

                var barTimer = new System.Windows.Forms.Timer { Interval = 14 };
                barTimer.Tick += (s, e) =>
                {
                    _fillPct += 0.055f;
                    if (_fillPct >= pct) { _fillPct = pct; barTimer.Stop(); barTimer.Dispose(); }
                    int fillW = (int)(track.Width * _fillPct);
                    fill.Width = Math.Max(fillW, 4);
                    fill.Invalidate();
                };
                barTimer.Start();

                barY += 36;
            }

            _scrollContainer.Controls.Add(_fleetCard);
        }

        // ══════════════════════════════════════════════
        //  PENDING ACTIONS
        // ══════════════════════════════════════════════
        private void BuildPendingActions()
        {
            _pendingCard = CreateCard("Admin Action Feed");

            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT *
                    FROM
                    (
                        SELECT 'booking' AS action_type,
                               CONCAT(u.full_name,' → ',v.brand,' ',v.model) AS description,
                               r.total_amount AS action_amount,
                               COALESCE(r.created_at, r.start_date) AS action_time,
                               r.status AS action_status
                        FROM rentals r
                        JOIN users u ON r.customer_id = u.user_id
                        JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                        WHERE LOWER(TRIM(COALESCE(r.status,''))) = 'pending'

                        UNION ALL

                        SELECT 'payment' AS action_type,
                               CONCAT(u.full_name,' unpaid for ',v.brand,' ',v.model) AS description,
                               r.total_amount AS action_amount,
                               COALESCE(r.created_at, r.start_date) AS action_time,
                               r.payment_status AS action_status
                        FROM rentals r
                        JOIN users u ON r.customer_id = u.user_id
                        JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                        WHERE LOWER(TRIM(COALESCE(r.payment_status,''))) <> 'paid'

                        UNION ALL

                        SELECT 'extension' AS action_type,
                               CONCAT(u.full_name,' requested +',e.added_days,' day(s) on ',v.brand,' ',v.model) AS description,
                               e.added_fee AS action_amount,
                               e.requested_at AS action_time,
                               e.status AS action_status
                        FROM extensions e
                        JOIN rentals r ON e.rental_id = r.rental_id
                        JOIN users u ON r.customer_id = u.user_id
                        JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                        WHERE LOWER(TRIM(COALESCE(e.status,''))) = 'pending'

                        UNION ALL

                        SELECT 'issue' AS action_type,
                               CONCAT(COALESCE(i.issue_type,'Issue'),' · ',u.full_name,' · ',v.brand,' ',v.model) AS description,
                               NULL AS action_amount,
                               i.reported_at AS action_time,
                               i.status AS action_status
                        FROM issues i
                        JOIN users u ON i.reporter_id = u.user_id
                        JOIN rentals r ON i.rental_id = r.rental_id
                        JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                        WHERE LOWER(TRIM(COALESCE(i.status,''))) <> 'resolved'

                        UNION ALL

                        SELECT 'overdue' AS action_type,
                               CONCAT(u.full_name,' overdue on ',v.brand,' ',v.model) AS description,
                               r.total_amount AS action_amount,
                               r.end_date AS action_time,
                               r.status AS action_status
                        FROM rentals r
                        JOIN users u ON r.customer_id = u.user_id
                        JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                        WHERE LOWER(TRIM(COALESCE(r.status,''))) IN ('approved','active')
                          AND r.end_date IS NOT NULL
                          AND DATE(r.end_date) < CURDATE()
                    ) feed
                    ORDER BY action_time DESC
                    LIMIT 5", conn);

                using var reader = cmd.ExecuteReader();
                int itemY = 50;

                while (reader.Read())
                {
                    string actionType = reader["action_type"]?.ToString()?.ToLower() ?? "booking";
                    var desc = reader["description"].ToString() ?? "";
                    var dt2 = Convert.ToDateTime(reader["action_time"]);
                    decimal? amount = reader["action_amount"] == DBNull.Value ? null : Convert.ToDecimal(reader["action_amount"]);

                    Color actionColor = actionType switch
                    {
                        "issue" => ColRed,
                        "extension" => ColYellow,
                        "overdue" => ColRed,
                        "payment" => ColYellow,
                        _ => ColAccent
                    };

                    string actionLabel = actionType switch
                    {
                        "issue" => "Needs attention",
                        "extension" => "Extension fee",
                        "overdue" => "Overdue amount",
                        "payment" => "Unpaid rental",
                        _ => "Booking amount"
                    };

                    var row = new Panel { Size = new Size(500, 38), Location = new Point(20, itemY), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                    SetDoubleBuffer(row);
                    row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(ColBorder, 1), 0, row.Height - 1, row.Width, row.Height - 1);

                    var dot = new Panel { Size = new Size(8, 8), Location = new Point(0, 15), BackColor = Color.Transparent };
                    dot.Paint += (s, e) =>
                    {
                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        e.Graphics.FillEllipse(new SolidBrush(actionColor), 0, 0, 7, 7);
                    };

                    var lblDesc = new Label { Text = desc, Font = new Font("Segoe UI", 9F), ForeColor = ColText, AutoSize = false, Size = new Size(340, 18), Location = new Point(16, 4), BackColor = Color.Transparent };
                    var lblTime = new Label { Text = dt2.ToString("MMM dd, hh:mm tt"), Font = new Font("Segoe UI", 8F), ForeColor = ColSub, AutoSize = true, Location = new Point(16, 20), BackColor = Color.Transparent };

                    var lblAmt = new Label
                    {
                        Text = amount.HasValue ? "₱" + amount.Value.ToString("N2") : actionLabel,
                        Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                        ForeColor = amount.HasValue ? ColAccent : actionColor,
                        AutoSize = true,
                        Location = new Point(390, 10),
                        BackColor = Color.Transparent,
                        Anchor = AnchorStyles.Top | AnchorStyles.Right
                    };

                    row.Controls.Add(dot);
                    row.Controls.Add(lblDesc);
                    row.Controls.Add(lblTime);
                    row.Controls.Add(lblAmt);
                    _pendingCard.Controls.Add(row);
                    itemY += 44;
                }

                if (itemY == 50)
                {
                    var lbl = new Label { Text = "✓  No urgent admin actions", Font = new Font("Segoe UI", 11F), ForeColor = ColGreen, AutoSize = true, Location = new Point(20, 76), BackColor = Color.Transparent };
                    _pendingCard.Controls.Add(lbl);
                }
            }
            catch (Exception ex)
            {
                AddErrorLabel(_pendingCard, ex.Message);
            }

            _scrollContainer.Controls.Add(_pendingCard);
        }

        // ══════════════════════════════════════════════
        //  ENTRANCE ANIMATION
        // ══════════════════════════════════════════════
        private void StartEntranceAnimation()
        {
            if (_cardAlpha == null) return;

            for (int i = 0; i < _cardAlpha.Length; i++)
            {
                _cardAlpha[i] = 0f;
                _cardOffsetY[i] = 28f;
            }

            _cardsDone = 0;
            _entranceTimer = new System.Windows.Forms.Timer { Interval = 14 };
            _entranceTimer.Tick += (s, e) =>
            {
                bool allDone = true;
                for (int i = 0; i < _cardAlpha.Length; i++)
                {
                    if (_cardsDone < i) { allDone = false; continue; }

                    _cardAlpha[i] += 0.06f;
                    _cardOffsetY[i] *= 0.78f;

                    if (_cardAlpha[i] >= 1f)
                    {
                        _cardAlpha[i] = 1f;
                        _cardOffsetY[i] = 0f;
                        if (_cardsDone == i) _cardsDone++;
                    }
                    else
                    {
                        allDone = false;
                    }

                    _statCards[i]?.Invalidate();
                }

                if (allDone)
                {
                    _entranceTimer.Stop();
                    _entranceTimer.Dispose();
                }
            };
            _entranceTimer.Start();
        }

        // ══════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════
        private Panel CreateCard(string title)
        {
            var pnl = new Panel { BackColor = Color.Transparent };
            SetDoubleBuffer(pnl);

            pnl.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int w = pnl.Width, h = pnl.Height;
                var rect = new Rectangle(0, 0, w - 1, h - 1);
                var path = GetRoundedRect(rect, 14);

                bool dark = ThemeManager.IsDarkMode;
                Color c1 = dark ? Color.FromArgb(28, 28, 42) : Color.White;
                Color c2 = dark ? Color.FromArgb(16, 16, 26) : Color.FromArgb(248, 248, 255);
                using var bg = new LinearGradientBrush(rect, c1, c2, LinearGradientMode.Vertical);
                g.FillPath(bg, path);

                pnl.Region = new Region(path);

                g.DrawLine(new Pen(Color.FromArgb(dark ? 18 : 80, 255, 255, 255), 1f), 14, 1, w - 14, 1);
                g.DrawPath(new Pen(Color.FromArgb(dark ? 30 : 180, ThemeManager.CurrentBorder), 0.8f), path);
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = ColText,
                AutoSize = true,
                Location = new Point(20, 14),
                BackColor = Color.Transparent
            };

            var accent = new Panel { Size = new Size(36, 3), Location = new Point(20, 36), BackColor = ColAccent };

            pnl.Controls.Add(lblTitle);
            pnl.Controls.Add(accent);
            return pnl;
        }

        private void AddErrorLabel(Panel parent, string msg)
        {
            parent.Controls.Add(new Label
            {
                Text = "⚠  " + msg,
                ForeColor = ColRed,
                Font = new Font("Segoe UI", 9F),
                AutoSize = true,
                Location = new Point(20, 80),
                BackColor = Color.Transparent
            });
        }

        private static void SetDoubleBuffer(Control c)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic,
                null, c, new object[] { true });
        }

        private GraphicsPath GetRoundedRect(Rectangle b, int r)
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
            ThemeManager.ThemeChanged -= ThemeChanged_Handler;
            _entranceTimer?.Dispose();
            base.Dispose(disposing);
        }
    }
}