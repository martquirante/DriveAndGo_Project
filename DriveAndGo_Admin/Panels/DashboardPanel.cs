#nullable disable
using DriveAndGo_Admin.Helpers;
using System.Data;
using System.Drawing.Drawing2D;
using System.Text.Json;

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

        private readonly string _connStr = string.Empty; // No longer used — data comes from DriveAndGo_API

        // ── Skeleton & Number Animation State ──
        private bool _isTableLoading = false;
        private bool _isMetricsLoading = false;
        private float _shimmerProgress = 0f;
        private System.Windows.Forms.Timer _shimmerTimer;

        // ── Stat values (Current Animated & Targets) ──
        private int _totalVehicles = 0;
        private int _activeRentals = 0;
        private int _availDrivers = 0;
        private decimal _todayRevenue = 0;
        private int _pendingBookings = 0;
        private int _pendingPayments = 0;
        private int _overdueRentals = 0;
        private int _openIssues = 0;

        private int _targetVehicles = 0;
        private int _targetRentals = 0;
        private int _targetDrivers = 0;
        private decimal _targetRevenue = 0m;
        private int _targetPendingBookings = 0;
        private int _targetPendingPayments = 0;
        private int _targetOverdueRentals = 0;
        private int _targetOpenIssues = 0;

        // ── Quick Stats values ──
        private int _totalUsers = 0;
        private int _totalReviews = 0;
        private decimal _avgRating = 0;
        private int _dueToday = 0;
        private int _overdue = 0;
        private int _pendingExtensions = 0;
        private string _topDriverName = "No driver ratings yet";
        private decimal _topDriverRating = 0;

        // ── Controls references ──
        private Panel _scrollContainer;
        private Panel[] _statCards = new Panel[8];
        private Label[] _statCardValueLabels = new Label[8];
        private DataGridView _dgvRecentBookings;
        private Panel _canvas3DCard;
        private Panel _bookingsCard;
        private Panel _quickStatsCard;
        private Panel _fleetCard;
        private Panel _pendingCard;

        // ── React WebView2 (DashboardOverview.html) ──
        private Microsoft.Web.WebView2.WinForms.WebView2 _dashWebView;

        // ── Animation ──
        private System.Windows.Forms.Timer _entranceTimer;
        private System.Windows.Forms.Timer _refreshTimer;
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

            _shimmerTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _shimmerTimer.Tick += OnShimmerTimerTick;
            _shimmerTimer.Start();

            BuildScrollContainer();
            BuildUI();
            StartEntranceAnimation();

            _refreshTimer = new System.Windows.Forms.Timer { Interval = 10000 };
            _refreshTimer.Tick += (s, e) => LoadStatsFromDB();
            _refreshTimer.Start();

            this.HandleCreated += (s, e) =>
            {
                LoadStatsFromDB();
                // Build the React-powered Dashboard WebView2 once the handle exists
                BuildWebDashboard();
            };

            this.VisibleChanged += (s, e) =>
            {
                if (this.Visible && this.IsHandleCreated && !this.IsDisposed)
                {
                    LoadStatsFromDB();
                    // Re-inject auth token and trigger React refresh
                    RefreshWebViewData();
                    // Sync CSS variable theme — keeps dark/light in sync without reload
                    PushThemeToWebView(Helpers.ThemeManager.IsDarkMode ? "dark" : "light");
                }
            };
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
        //  REACT DASHBOARD WEBVIEW2
        //  Loads DashboardOverview.html — a self-contained
        //  React 18 + Babel Standalone page with:
        //   • Live data fetch from /api/admin/dashboard/summary
        //   • 3D tilt metric cards
        //   • Staggered fadeInUp animations
        //   • window.forceDashboardRefresh global for C# interop
        // ══════════════════════════════════════════════
        private async void BuildWebDashboard()
        {
            try
            {
                string htmlPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "DashboardOverview.html");

                if (!System.IO.File.Exists(htmlPath))
                {
                    Console.WriteLine("[Dashboard] DashboardOverview.html not found at: " + htmlPath);
                    return;
                }

                // Create the WebView2 control
                _dashWebView = new Microsoft.Web.WebView2.WinForms.WebView2 { Dock = DockStyle.Fill };

                // Insert BEHIND the scroll container so native C# cards still show if needed
                // (but we use DockStyle.Fill — the web panel takes priority via BringToFront)
                Controls.Add(_dashWebView);
                _dashWebView.BringToFront();

                // Initialise the WebView2 environment
                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(
                    null, System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DriveAndGo_DashWV2"));

                await _dashWebView.EnsureCoreWebView2Async(env);

                // ── React → C# message bridge ─────────────────────────────────
                // React calls: window.chrome.webview.postMessage({ action: 'open_ai_insights' })
                // C# receives it here and dispatches to the correct handler.
                _dashWebView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;

                // Harden settings
                _dashWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _dashWebView.CoreWebView2.Settings.AreDevToolsEnabled            = false;
                _dashWebView.CoreWebView2.Settings.IsStatusBarEnabled            = false;

                string token   = Helpers.SessionManager.Token ?? string.Empty;
                string apiBase = Helpers.ApiService.BaseUrl.TrimEnd('/');
                string currentTheme = ThemeManager.IsDarkMode ? "dark" : "light";

                // Inject BEFORE navigation so scripts running on document load have API credentials and data-theme defined immediately
                await _dashWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                    $"window.API_BASE_URL = '{apiBase}'; window.AUTH_TOKEN = '{token}'; document.documentElement.setAttribute('data-theme', '{currentTheme}');");
                await _dashWebView.CoreWebView2.ExecuteScriptAsync(
                    $"window.API_BASE_URL = '{apiBase}'; window.AUTH_TOKEN = '{token}'; document.documentElement.setAttribute('data-theme', '{currentTheme}');");

                // After the page finishes loading, re-verify credentials, sync theme & trigger data fetch
                _dashWebView.CoreWebView2.NavigationCompleted += async (sender, args) =>
                {
                    if (_dashWebView == null || _dashWebView.IsDisposed || _dashWebView.CoreWebView2 == null) return;
                    try
                    {
                        string currentToken   = Helpers.SessionManager.Token ?? string.Empty;
                        string currentApiBase = Helpers.ApiService.BaseUrl.TrimEnd('/');
                        string activeTheme    = ThemeManager.IsDarkMode ? "dark" : "light";

                        // Step 1: Set globals and apply theme immediately
                        await _dashWebView.CoreWebView2.ExecuteScriptAsync(
                            $"window.API_BASE_URL = '{currentApiBase}'; window.AUTH_TOKEN = '{currentToken}';" +
                            $"if(window.setDashboardTheme) window.setDashboardTheme('{activeTheme}');");

                        // Step 2: Trigger React data fetch
                        await _dashWebView.CoreWebView2.ExecuteScriptAsync(
                            "if(window.forceDashboardRefresh) window.forceDashboardRefresh();" +
                            "else if(window.refreshDashboardData) window.refreshDashboardData();");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[Dashboard] Post-navigation script failed: " + ex.Message);
                    }
                };

                // Navigate to the HTML host page
                _dashWebView.CoreWebView2.Navigate("file:///" + htmlPath.Replace('\\', '/'));
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Dashboard] BuildWebDashboard failed: " + ex.Message);
                // Graceful fallback: keep the native WinForms panel visible
                _dashWebView?.Dispose();
                _dashWebView = null;
            }
        }

        // ══════════════════════════════════════════════
        //  LOAD DATA  — via DriveAndGo_API
        // ══════════════════════════════════════════════
        public void RefreshWebViewData()
        {
            if (!this.IsHandleCreated || this.IsDisposed) return;
            this.BeginInvoke((MethodInvoker)(async () =>
            {
                try
                {
                    var wv = _dashWebView;
                    if (wv == null || wv.IsDisposed || wv.CoreWebView2 == null) return;

                    // Re-inject fresh auth token every time the panel becomes visible
                    string token   = SessionManager.JwtToken ?? string.Empty;
                    string apiBase = ApiService.BaseUrl.TrimEnd('/');
                    string initJs  = $"window.API_BASE_URL='{apiBase}'; window.AUTH_TOKEN='{token}';"
                                   + " if(window.forceDashboardRefresh) window.forceDashboardRefresh();"
                                   + " else if(window.refreshDashboardData) window.refreshDashboardData();";
                    await wv.CoreWebView2.ExecuteScriptAsync(initJs);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[Dashboard] WebView2 refresh failed: " + ex.Message);
                }
            }));
        }

        // ══════════════════════════════════════════════════════════════
        //  REACT → C# MESSAGE BRIDGE
        //  React fires: window.chrome.webview.postMessage({ action: 'open_ai_insights' })
        //  This event handler receives it and routes to the correct UI action.
        //  NOTE: WebMessageReceived fires on a background/WebView thread.
        //        Always marshal back to the UI thread via BeginInvoke before
        //        touching any WinForms controls or async dialogs.
        // ══════════════════════════════════════════════════════════════
        private void WebView_WebMessageReceived(
            object sender,
            Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string rawStr = e.TryGetWebMessageAsString();
                string action = string.Empty;

                if (!string.IsNullOrEmpty(rawStr))
                {
                    action = rawStr;
                }
                else
                {
                    try
                    {
                        string json = e.WebMessageAsJson;
                        if (!string.IsNullOrEmpty(json))
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(json);
                            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                                doc.RootElement.TryGetProperty("action", out var actionProp))
                            {
                                action = actionProp.GetString();
                            }
                        }
                    }
                    catch { }
                }

                switch (action.Trim())
                {
                    case "open_ai_insights":
                        // Marshal to UI thread — WebMessageReceived fires off the UI thread
                        if (!this.IsHandleCreated || this.IsDisposed) return;
                        this.BeginInvoke((MethodInvoker)(() =>
                        {
                            try
                            {
                                using var aiForm = new AIBusinessInsightsForm();
                                aiForm.ShowDialog(this.FindForm());
                            }
                            catch (Exception ex)
                            { Console.WriteLine("[Dashboard] AI Insights dialog failed: " + ex.Message); }
                        }));
                        break;

                    default:
                        if (!string.IsNullOrWhiteSpace(action))
                            Console.WriteLine("[Dashboard] Unknown WebView action: " + action);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Dashboard] WebMessageReceived error: " + ex.Message);
            }
        }

        //  Called by MainForm on theme toggle AND on dashboard navigation.
        //  window.setDashboardTheme is defined in DashboardOverview.html:
        //    window.setDashboardTheme = (theme) => {
        //      document.documentElement.setAttribute('data-theme', theme);
        //    };
        // ══════════════════════════════════════════════════════════════
        public void PushThemeToWebView(string theme) // "dark" | "light"
        {
            if (!this.IsHandleCreated || this.IsDisposed) return;
            this.BeginInvoke((MethodInvoker)(async () =>
            {
                try
                {
                    var wv = _dashWebView;
                    if (wv == null || wv.IsDisposed || wv.CoreWebView2 == null) return;

                    // Sanitize: only allow "dark" or "light" to prevent injection
                    string safeTheme = theme == "light" ? "light" : "dark";
                    await wv.CoreWebView2.ExecuteScriptAsync(
                        $"if(window.setDashboardTheme) window.setDashboardTheme('{safeTheme}');");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[Dashboard] PushThemeToWebView failed: " + ex.Message);
                }
            }));
        }

        // ══════════════════════════════════════════════
        //  LIVE DATA FETCHING & ANIMATIONS
        // ══════════════════════════════════════════════
        private void OnShimmerTimerTick(object sender, EventArgs e)
        {
            _shimmerProgress += 0.15f;

            if (_isTableLoading)
            {
                _bookingsCard?.Invalidate();
            }

            if (AnimateStatNumbers())
            {
                UpdateStatCardValues();
            }
        }

        private bool AnimateStatNumbers()
        {
            bool animating = false;
            animating |= StepInt(ref _totalVehicles, _targetVehicles);
            animating |= StepInt(ref _activeRentals, _targetRentals);
            animating |= StepInt(ref _availDrivers, _targetDrivers);
            animating |= StepInt(ref _pendingBookings, _targetPendingBookings);
            animating |= StepInt(ref _pendingPayments, _targetPendingPayments);
            animating |= StepInt(ref _overdueRentals, _targetOverdueRentals);
            animating |= StepInt(ref _openIssues, _targetOpenIssues);
            animating |= StepDecimal(ref _todayRevenue, _targetRevenue);
            return animating;
        }

        private static bool StepInt(ref int current, int target)
        {
            if (current == target) return false;
            int diff = target - current;
            int step = Math.Sign(diff) * Math.Max(1, Math.Abs(diff) / 5);
            current += step;
            if (Math.Sign(target - current) != Math.Sign(diff)) current = target;
            return true;
        }

        private static bool StepDecimal(ref decimal current, decimal target)
        {
            if (current == target) return false;
            decimal diff = target - current;
            decimal step = diff * 0.2m;
            if (Math.Abs(step) < 0.01m) current = target;
            else current += step;
            return true;
        }

        private void UpdateStatCardValues()
        {
            if (_statCardValueLabels == null) return;
            if (_statCardValueLabels.Length > 0 && _statCardValueLabels[0] != null) _statCardValueLabels[0].Text = _totalVehicles.ToString();
            if (_statCardValueLabels.Length > 1 && _statCardValueLabels[1] != null) _statCardValueLabels[1].Text = _activeRentals.ToString();
            if (_statCardValueLabels.Length > 2 && _statCardValueLabels[2] != null) _statCardValueLabels[2].Text = _availDrivers.ToString();
            if (_statCardValueLabels.Length > 3 && _statCardValueLabels[3] != null) _statCardValueLabels[3].Text = $"₱{_todayRevenue:N2}";
            if (_statCardValueLabels.Length > 4 && _statCardValueLabels[4] != null) _statCardValueLabels[4].Text = _pendingBookings.ToString();
            if (_statCardValueLabels.Length > 5 && _statCardValueLabels[5] != null) _statCardValueLabels[5].Text = _pendingPayments.ToString();
            if (_statCardValueLabels.Length > 6 && _statCardValueLabels[6] != null) _statCardValueLabels[6].Text = _overdueRentals.ToString();
            if (_statCardValueLabels.Length > 7 && _statCardValueLabels[7] != null) _statCardValueLabels[7].Text = _openIssues.ToString();
        }

        private void RenderSkeletonTable(Graphics g, int alpha)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool dark = ThemeManager.IsDarkMode;
            Color baseColor = dark ? Color.FromArgb(255, 255, 255) : Color.FromArgb(120, 120, 140);
            using var skeletonBrush = new SolidBrush(Color.FromArgb(alpha, baseColor));
            using var headerBrush = new SolidBrush(Color.FromArgb(Math.Min(255, alpha + 20), baseColor));

            int startX = 20;
            int startY = 52;
            int rowHeight = 36;
            int numRows = 5;

            // Header Skeleton
            int cardW = _bookingsCard != null ? _bookingsCard.Width : 700;
            using (var headerPath = GetRoundedRect(new Rectangle(startX, startY, cardW - 40, 28), 6))
            {
                g.FillPath(headerBrush, headerPath);
            }

            // Dummy Rows
            for (int i = 0; i < numRows; i++)
            {
                int y = startY + 36 + (i * rowHeight);

                // 1. Booking ID (50x15)
                using (var path = GetRoundedRect(new Rectangle(startX + 6, y + 10, 50, 15), 4))
                    g.FillPath(skeletonBrush, path);

                // 2. Customer Name (140x15)
                using (var path = GetRoundedRect(new Rectangle(startX + 80, y + 10, 140, 15), 4))
                    g.FillPath(skeletonBrush, path);

                // 3. Vehicle Name (100x15)
                using (var path = GetRoundedRect(new Rectangle(startX + 250, y + 10, 100, 15), 4))
                    g.FillPath(skeletonBrush, path);

                // 4. Start Date (80x15)
                using (var path = GetRoundedRect(new Rectangle(startX + 370, y + 10, 80, 15), 4))
                    g.FillPath(skeletonBrush, path);

                // 5. Status Pill (70x22, fully rounded)
                using (var path = GetRoundedRect(new Rectangle(startX + 470, y + 6, 70, 22), 11))
                    g.FillPath(skeletonBrush, path);

                // 6. Amount (70x15)
                using (var path = GetRoundedRect(new Rectangle(startX + 560, y + 10, 70, 15), 4))
                    g.FillPath(skeletonBrush, path);
            }
        }

        public void LoadStatsFromDB()
        {
            Task.Run(async () => await LoadDashboardDataAsync());
        }

        public async Task LoadDashboardDataAsync()
        {
            if (!this.IsHandleCreated || this.IsDisposed) return;

            this.Invoke((MethodInvoker)(() =>
            {
                _isTableLoading = true;
                _isMetricsLoading = true;
                _totalVehicles = 0;
                _activeRentals = 0;
                _availDrivers = 0;
                _todayRevenue = 0m;
                _pendingBookings = 0;
                _pendingPayments = 0;
                _overdueRentals = 0;
                _openIssues = 0;
                UpdateStatCardValues();

                if (_dgvRecentBookings != null) _dgvRecentBookings.Visible = false;
                _bookingsCard?.Invalidate(true);
                _bookingsCard?.Update();
                this.Invalidate(true);
            }));

            try
            {
                var summaryTask = ApiService.GetAsync("admin/dashboard/summary");
                var rentalsTask = ApiService.GetAsync("rentals");
                var minDelayTask = Task.Delay(600);

                await Task.WhenAll(summaryTask, rentalsTask, minDelayTask);

                var summaryRes = summaryTask.Result;
                var rentalsRes = rentalsTask.Result;

                int totalVehicles = 0, activeRentals = 0, pendingRentals = 0, totalUsers = 0, totalReviews = 0, dueToday = 0, overdue = 0, pendingExts = 0, openIssues = 0, pendingPayments = 0;
                decimal monthRev = 0m, avgRating = 0m, topDriverRating = 0m;
                string topDriverName = "No driver ratings yet";

                if (summaryRes.Success)
                {
                    using var doc = JsonDocument.Parse(summaryRes.Body);
                    var root = doc.RootElement;

                    totalVehicles = root.TryGetProperty("totalVehicles", out var tv) ? tv.GetInt32() : 0;
                    activeRentals = root.TryGetProperty("activeRentals", out var ar) ? ar.GetInt32() : 0;
                    pendingRentals = root.TryGetProperty("pendingRentals", out var pr) ? pr.GetInt32() : 0;
                    monthRev = root.TryGetProperty("revenueThisMonth", out var mr) ? mr.GetDecimal() : 0m;
                    totalUsers = root.TryGetProperty("totalUsers", out var tu) ? tu.GetInt32() : 0;
                    totalReviews = root.TryGetProperty("totalReviews", out var tr) ? tr.GetInt32() : 0;
                    avgRating = root.TryGetProperty("avgRating", out var avgr) ? avgr.GetDecimal() : 0m;
                    dueToday = root.TryGetProperty("dueToday", out var dt) ? dt.GetInt32() : 0;
                    overdue = root.TryGetProperty("overdue", out var od) ? od.GetInt32() : 0;
                    pendingExts = root.TryGetProperty("pendingExtensions", out var pe) ? pe.GetInt32() : 0;
                    openIssues = root.TryGetProperty("openIssues", out var oi) ? oi.GetInt32() : 0;
                    pendingPayments = root.TryGetProperty("pendingPayments", out var pp) ? pp.GetInt32() : 0;
                    topDriverName = root.TryGetProperty("topDriverName", out var tdn) ? tdn.GetString() : "No driver ratings yet";
                    topDriverRating = root.TryGetProperty("topDriverRating", out var tdr) ? tdr.GetDecimal() : 0m;
                }

                DataTable dtBookings = new DataTable();
                dtBookings.Columns.Add("#", typeof(int));
                dtBookings.Columns.Add("Customer", typeof(string));
                dtBookings.Columns.Add("Vehicle", typeof(string));
                dtBookings.Columns.Add("Start", typeof(string));
                dtBookings.Columns.Add("End", typeof(string));
                dtBookings.Columns.Add("Status", typeof(string));
                dtBookings.Columns.Add("Amount", typeof(string));

                if (rentalsRes.Success)
                {
                    using var doc = JsonDocument.Parse(rentalsRes.Body);
                    int count = 0;
                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        if (count++ >= 12) break;
                        var row = dtBookings.NewRow();
                        row["#"] = elem.TryGetProperty("rentalId", out var rid) ? rid.GetInt32() : 0;
                        row["Customer"] = elem.TryGetProperty("customerName", out var cn) ? cn.GetString() : "";
                        row["Vehicle"] = elem.TryGetProperty("vehicleName", out var vn) ? vn.GetString() : "";
                        row["Start"] = elem.TryGetProperty("startDate", out var sd) && sd.ValueKind != JsonValueKind.Null ? sd.GetDateTime().ToString("MMM dd, yyyy") : "";
                        row["End"] = elem.TryGetProperty("endDate", out var ed) && ed.ValueKind != JsonValueKind.Null ? ed.GetDateTime().ToString("MMM dd, yyyy") : "";
                        row["Status"] = elem.TryGetProperty("status", out var st) ? st.GetString() : "";
                        row["Amount"] = elem.TryGetProperty("totalAmount", out var amt) ? $"₱{amt.GetDecimal():N2}" : "₱0.00";
                        dtBookings.Rows.Add(row);
                    }
                }

                if (!this.IsHandleCreated || this.IsDisposed) return;

                this.BeginInvoke((MethodInvoker)(() =>
                {
                    _targetVehicles = totalVehicles;
                    _targetRentals = activeRentals;
                    _targetDrivers = totalUsers;
                    _targetRevenue = monthRev;
                    _targetPendingBookings = pendingRentals;
                    _targetPendingPayments = pendingPayments;
                    _targetOverdueRentals = overdue;
                    _targetOpenIssues = openIssues;

                    _totalUsers = totalUsers;
                    _totalReviews = totalReviews;
                    _avgRating = avgRating;
                    _dueToday = dueToday;
                    _overdue = overdue;
                    _pendingExtensions = pendingExts;
                    _openIssues = openIssues;
                    _topDriverName = topDriverName;
                    _topDriverRating = topDriverRating;

                    _isTableLoading = false;
                    _isMetricsLoading = false;

                    if (_dgvRecentBookings != null)
                    {
                        _dgvRecentBookings.DataSource = dtBookings;
                        _dgvRecentBookings.Visible = true;
                    }

                    _bookingsCard?.Invalidate();
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Dashboard load error: " + ex.Message);
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    this.BeginInvoke((MethodInvoker)(() =>
                    {
                        _isTableLoading = false;
                        _isMetricsLoading = false;
                        if (_dgvRecentBookings != null) _dgvRecentBookings.Visible = true;
                        _bookingsCard?.Invalidate();
                    }));
                }
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

            int rows2Start = statTop + ((_statCards.Length - 1) / cols + 1) * (cardH + gap) + gap;

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

            var btnAiInsights = new Button
            {
                Text = "💡  AI Insights",
                Size = new Size(130, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = ColAccent,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnAiInsights.FlatAppearance.BorderSize = 0;
            btnAiInsights.Click += async (s, e) => await ShowAiInsightsDialogAsync();

            btnRefresh.FlatAppearance.BorderColor = ColAccent;
            btnRefresh.FlatAppearance.BorderSize = 1;
            btnRefresh.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, ColAccent);

            Action positionControls = () =>
            {
                btnRefresh.Location = new Point(Math.Max(8, pnl.ClientSize.Width - btnRefresh.Width - 10), 7);
                btnAiInsights.Location = new Point(Math.Max(8, btnRefresh.Left - btnAiInsights.Width - 10), 7);
            };
            pnl.Resize += (s, e) => positionControls();
            positionControls();

            btnRefresh.Click += (s, e) =>
            {
                LoadStatsFromDB();
            };

            pnl.Controls.Add(lblTitle);
            pnl.Controls.Add(lblSub);
            pnl.Controls.Add(btnRefresh);
            pnl.Controls.Add(btnAiInsights);
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
                ("Today's Revenue",   "₱",  _todayRevenue.ToString("N2"), "Paid rentals only",  ColAccent),
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
                float target = hs.Hovered ? 8f : 0f;
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

                // Neon accent glow border
                using var neonPen = new Pen(Color.FromArgb((int)(20 * alpha), accentColor), 2f);
                using var neonPath = GetRoundedRect(new Rectangle(drawR.X + 1, drawR.Y + 1, drawR.Width - 2, drawR.Height - 2), 13);
                g.DrawPath(neonPen, neonPath);

                g.Restore(state);
            };

            bool isRev = title.Contains("Revenue");

            // Mas ilayo ang icon sa text (put on far-left for Today's Revenue)
            var pnlIcon = new Panel
            {
                Size = new Size(40, 40),
                Location = isRev ? new Point(14, 12) : new Point(w - 54, 12),
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
                Location = isRev ? new Point(11, 7) : new Point(8, 7),
                BackColor = Color.Transparent
            };
            pnlIcon.Controls.Add(lblIcon);

            // Dynamic Font size based on value length to prevent truncation
            float fontSize = 17F;
            if (isRev)
            {
                using (var g = card.CreateGraphics())
                {
                    int maxW = w - 74; // Leave 2px safety margin
                    while (fontSize > 8.5F)
                    {
                        using (var testFont = new Font("Segoe UI", fontSize, FontStyle.Bold))
                        {
                            var size = g.MeasureString(value, testFont);
                            if (size.Width <= maxW)
                                break;
                        }
                        fontSize -= 0.5F; // Scale down by 0.5pt steps
                    }
                }
            }

            var lblValue = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", fontSize, FontStyle.Bold),
                ForeColor = ColText,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = false, // No more "... " dots!
                Location = isRev ? new Point(58, 12) : new Point(14, 12),
                Size = isRev ? new Size(w - 72, 34) : new Size(w - 78, 34),
                BackColor = Color.Transparent
            };

            if (idx >= 0 && idx < _statCardValueLabels.Length)
            {
                _statCardValueLabels[idx] = lblValue;
            }

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
        //  3D CANVAS
        // ══════════════════════════════════════════════
        private async void Build3DCanvas()
        {
            _canvas3DCard = new Panel { BackColor = Color.Transparent };
            SetDoubleBuffer(_canvas3DCard);

            // Paint: glassmorphic card identical to CreateCard() but for the 3D analytics header
            _canvas3DCard.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int w = _canvas3DCard.Width, h = _canvas3DCard.Height;
                var rect = new Rectangle(0, 0, w - 1, h - 1);
                var path = GetRoundedRect(rect, 14);
                bool dark = ThemeManager.IsDarkMode;
                Color c1 = dark ? Color.FromArgb(20, 14, 28) : Color.White;
                Color c2 = dark ? Color.FromArgb(10, 8, 18) : Color.FromArgb(248, 248, 255);
                using var bg = new LinearGradientBrush(rect, c1, c2, LinearGradientMode.Vertical);
                g.FillPath(bg, path);
                _canvas3DCard.Region = new Region(path);
                // Orange neon glow border
                using var glowPen = new Pen(Color.FromArgb(dark ? 40 : 20, 255, 90, 31), 1.5f);
                g.DrawPath(glowPen, path);
                // Top shimmer
                g.DrawLine(new Pen(Color.FromArgb(dark ? 20 : 70, 255, 255, 255), 1f), 14, 1, w - 14, 1);
                // Title
                using var titleFont = new Font("Segoe UI", 11F, FontStyle.Bold);
                g.DrawString("⬡  Fleet 3D Analytics", titleFont, new SolidBrush(ColAccent), new PointF(18, 14));
                // Orange accent underline
                g.FillRectangle(new SolidBrush(ColAccent), 18, 36, 38, 3);
            };

            // WebView2 container (padding-top 44 to clear the title area)
            var wvContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 44, 0, 0)
            };
            SetDoubleBuffer(wvContainer);
            _canvas3DCard.Controls.Add(wvContainer);
            _scrollContainer.Controls.Add(_canvas3DCard);

            // Load WebView2 asynchronously
            try
            {
                var webView = new Microsoft.Web.WebView2.WinForms.WebView2 { Dock = DockStyle.Fill };
                wvContainer.Controls.Add(webView);
                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(
                    null, System.IO.Path.GetTempPath());
                await webView.EnsureCoreWebView2Async(env);
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                string htmlPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "Dashboard3D.html");
                if (System.IO.File.Exists(htmlPath))
                    webView.CoreWebView2.Navigate("file:///" + htmlPath.Replace('\\', '/'));
            }
            catch
            {
                // Fallback label if WebView2 unavailable
                wvContainer.Controls.Add(new Label
                {
                    Text = "⬡  3D Fleet Analytics — WebView2 required",
                    Font = new Font("Segoe UI", 10F),
                    ForeColor = Color.FromArgb(80, 255, 90, 31),
                    AutoSize = true,
                    Location = new Point(20, 16),
                    BackColor = Color.Transparent
                });
            }
        }

        // ══════════════════════════════════════════════
        //  RECENT BOOKINGS
        // ══════════════════════════════════════════════
        private void BuildRecentBookings()
        {
            _bookingsCard = CreateCard("Recent Bookings");
            _bookingsCard.Paint += (s, e) =>
            {
                if (_isTableLoading)
                {
                    int pulseAlpha = (int)(15 + (Math.Sin(_shimmerProgress * 0.1) + 1) * 20);
                    RenderSkeletonTable(e.Graphics, pulseAlpha);
                }
            };

            var dgv = new DataGridView();
            _dgvRecentBookings = dgv;
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

            _bookingsCard.Controls.Add(dgv);
            _scrollContainer.Controls.Add(_bookingsCard);
        }

        // ══════════════════════════════════════════════
        //  QUICK STATS
        // ══════════════════════════════════════════════
        private void BuildQuickStats()
        {
            _quickStatsCard = CreateCard("Quick Stats");

            var items = new[]
            {
                ("Monthly Revenue",     "₱" + _todayRevenue.ToString("N2"), ColAccent, Math.Min(1f, (float)(_todayRevenue / 500000m))),
                ("Total Customers",     _totalUsers.ToString(), ColBlue, Math.Min(1f, _totalUsers / 500f)),
                ("Total Reviews",       _totalReviews.ToString(), ColPurple, Math.Min(1f, _totalReviews / 200f)),
                ("Avg. Rating",         _avgRating.ToString("0.0") + " / 5.0", ColGreen, (float)_avgRating / 5f),
                ("Due Today / Overdue", $"{_dueToday} due  ·  {_overdue} overdue", _overdue > 0 ? ColRed : ColYellow, Math.Min(1f, (_dueToday + _overdue) / 20f)),
                ("Ops Queue",           $"{_pendingExtensions} extensions  ·  {_openIssues} issues", (_pendingExtensions + _openIssues) > 0 ? ColYellow : ColGreen, Math.Min(1f, (_pendingExtensions + _openIssues) / 10f)),
                ("Top Driver",          _topDriverRating > 0 ? $"{_topDriverName}  ·  {_topDriverRating:0.0}★" : _topDriverName, ColBlue, (float)_topDriverRating / 5f),
            };

            int itemY = 56;
            foreach (var (label, val, color, pct) in items)
            {
                var row = new Panel
                {
                    Size = new Size(280, 58),
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

                // Animated progress fill bar (6px tall, at bottom of row)
                var trackBar = new Panel { Size = new Size(row.Width - 80, 5), Location = new Point(14, row.Height - 10), BackColor = Color.FromArgb(25, color), Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
                var fillBar = new Panel { Size = new Size(4, 5), Location = new Point(0, 0), BackColor = color };
                SetDoubleBuffer(fillBar);
                fillBar.Paint += (s, e) =>
                {
                    using var brush = new LinearGradientBrush(fillBar.ClientRectangle.IsEmpty ? new Rectangle(0, 0, 1, 1) : fillBar.ClientRectangle, Color.FromArgb(160, color), color, LinearGradientMode.Horizontal);
                    e.Graphics.FillRectangle(brush, fillBar.ClientRectangle);
                };
                trackBar.Controls.Add(fillBar);
                row.Controls.Add(trackBar);

                // Badge chip on right edge
                var badge = new Panel { Size = new Size(72, 20), Location = new Point(row.Width - 80, 8), Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Color.Transparent };
                SetDoubleBuffer(badge);
                string badgeText = val.Length > 12 ? val.Substring(0, 12) : val;
                badge.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = GetRoundedRect(new Rectangle(0, 0, badge.Width - 1, badge.Height - 1), 10);
                    e.Graphics.FillPath(new SolidBrush(Color.FromArgb(40, color)), path);
                    e.Graphics.DrawPath(new Pen(Color.FromArgb(60, color), 0.8f), path);
                    using var f = new Font("Segoe UI", 7.5F, FontStyle.Bold);
                    using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(badgeText, f, new SolidBrush(color), new RectangleF(0, 0, badge.Width, badge.Height), fmt);
                };
                row.Controls.Add(badge);

                // Animate the fill bar
                float _prog = 0f;
                var progTimer = new System.Windows.Forms.Timer { Interval = 14 };
                progTimer.Tick += (s, e) =>
                {
                    _prog += 0.055f;
                    if (_prog >= pct) { _prog = pct; progTimer.Stop(); progTimer.Dispose(); }
                    fillBar.Width = Math.Max(4, (int)(trackBar.Width * _prog));
                    fillBar.Invalidate();
                };
                progTimer.Start();

                _quickStatsCard.Controls.Add(row);
                itemY += 68;
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
            available = _totalVehicles > 0 ? _totalVehicles - _activeRentals : 0;
            rented = _activeRentals;
            maintenance = 0;
            retired = 0;

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
            _pendingCard = CreateCard("Operational Telemetry");

            var telemetry = new[]
            {
                ("Pending Bookings", _pendingBookings, _pendingBookings > 0 ? ColRed    : ColGreen, _pendingBookings > 0 ? "ACTION" : "CLEAR"),
                ("Overdue Rentals",  _overdueRentals,  _overdueRentals  > 0 ? ColRed    : ColGreen, _overdueRentals  > 0 ? "FOLLOW UP" : "CLEAR"),
                ("Open Issues",      _openIssues,      _openIssues      > 0 ? ColYellow : ColGreen, _openIssues      > 0 ? "REVIEW" : "OK"),
                ("Pending Payments", _pendingPayments,  _pendingPayments > 0 ? ColYellow : ColGreen, _pendingPayments > 0 ? "PENDING" : "CLEAR"),
            };

            int rowY = 48;
            foreach (var (label, count, color, badge) in telemetry)
            {
                var row = new Panel { Size = new Size(_pendingCard.Width - 40, 32), Location = new Point(20, rowY), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                SetDoubleBuffer(row);

                row.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    var rect2 = new Rectangle(0, 0, row.Width - 1, row.Height - 1);
                    using var path = GetRoundedRect(rect2, 8);
                    bool d = ThemeManager.IsDarkMode;
                    g.FillPath(new SolidBrush(d ? Color.FromArgb(16, 16, 28) : Color.FromArgb(248, 248, 255)), path);
                    g.FillRectangle(new SolidBrush(color), 0, 6, 3, 20);
                    g.DrawPath(new Pen(Color.FromArgb(d ? 22 : 160, ThemeManager.CurrentBorder), 0.5f), path);

                    // Label
                    using var lf = new Font("Segoe UI", 9F);
                    g.DrawString(label, lf, new SolidBrush(ColSub), new PointF(12, 8));

                    // Count
                    using var vf = new Font("Segoe UI", 10F, FontStyle.Bold);
                    string countStr = count.ToString();
                    g.DrawString(countStr, vf, new SolidBrush(color), new PointF(200, 6));

                    // Badge pill
                    var bRect = new Rectangle(row.Width - 78, 6, 72, 20);
                    using var bPath = GetRoundedRect(bRect, 10);
                    g.FillPath(new SolidBrush(Color.FromArgb(35, color)), bPath);
                    g.DrawPath(new Pen(Color.FromArgb(55, color), 0.8f), bPath);
                    using var bf = new Font("Segoe UI", 7.5F, FontStyle.Bold);
                    using var bfmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(badge, bf, new SolidBrush(color), new RectangleF(bRect.X, bRect.Y, bRect.Width, bRect.Height), bfmt);
                };

                _pendingCard.Controls.Add(row);
                rowY += 38;
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

        private async Task ShowAiInsightsDialogAsync()
        {
            var dialog = new Form
            {
                Text = "💡 AI Business Insights & Recommendations",
                Size = new Size(800, 600),
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = ColBg,
                ForeColor = ColText
            };

            // Custom Glassmorphic Title Bar
            var titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(15, ThemeManager.CurrentBorder)
            };

            // Allow dragging
            Point lastClick = Point.Empty;
            titleBar.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) lastClick = e.Location;
            };
            titleBar.MouseMove += (s, e) =>
            {
                if (e.Button == MouseButtons.Left && lastClick != Point.Empty)
                {
                    dialog.Left += e.X - lastClick.X;
                    dialog.Top += e.Y - lastClick.Y;
                }
            };

            var lblTitle = new Label
            {
                Text = "🧠  AI BUSINESS OPERATIONS ADVISOR",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 14),
                BackColor = Color.Transparent
            };

            var btnClose = new Button
            {
                Text = "✕",
                Size = new Size(36, 30),
                Location = new Point(dialog.Width - 50, 10),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 200, 200),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 68, 68);
            btnClose.Click += (s, e) => dialog.Close();

            titleBar.Controls.Add(lblTitle);
            titleBar.Controls.Add(btnClose);
            dialog.Controls.Add(titleBar);

            // Container for WebView2
            var pnlWebView = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), BackColor = Color.Transparent };
            dialog.Controls.Add(pnlWebView);
            pnlWebView.BringToFront();

            // Form border painting
            dialog.Paint += (s, e) =>
            {
                using var pen = new Pen(ColAccent, 2);
                e.Graphics.DrawRectangle(pen, 0, 0, dialog.Width - 1, dialog.Height - 1);
            };

            dialog.Shown += async (s, e) =>
            {
                try
                {
                    var webView = new Microsoft.Web.WebView2.WinForms.WebView2 { Dock = DockStyle.Fill };
                    pnlWebView.Controls.Add(webView);

                    var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, Path.GetTempPath());
                    await webView.EnsureCoreWebView2Async(env);

                    webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                    webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                    string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "AIBusinessInsights.html");
                    if (File.Exists(htmlPath))
                    {
                        webView.CoreWebView2.Navigate("file:///" + htmlPath.Replace('\\', '/'));

                        webView.CoreWebView2.NavigationCompleted += async (sender, args) =>
                        {
                            if (dialog.IsDisposed || !dialog.IsHandleCreated) return;
                            try
                            {
                                var res = await ApiService.GetAsync("admin/dashboard/ai-insights");
                                if (dialog.IsDisposed || !dialog.IsHandleCreated || webView.IsDisposed || webView.CoreWebView2 == null) return;

                                if (res.Success)
                                {
                                    var root = JsonDocument.Parse(res.Body).RootElement;
                                    string content = root.GetProperty("content").GetString();
                                    string source = root.GetProperty("source").GetString();
                                    double occupancy = root.TryGetProperty("occupancy", out var occ) ? occ.GetDouble() : 0;
                                    decimal monthlyRevenue = root.TryGetProperty("monthlyRevenue", out var mr) ? mr.GetDecimal() : 0;
                                    decimal totalRevenue = root.TryGetProperty("totalRevenue", out var tr) ? tr.GetDecimal() : 0;

                                    lblTitle.Text = $"🧠  AI BUSINESS OPERATIONS ADVISOR ({source.ToUpper()})";
                                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
                                    string base64 = Convert.ToBase64String(bytes);
                                    await webView.CoreWebView2.ExecuteScriptAsync($"window.updateInsightsFromBase64('{base64}', false, {occupancy}, '{monthlyRevenue:N2}', '{totalRevenue:N2}', '{source}');");
                                }
                                else
                                {
                                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes("Failed to load AI insights. Live server returned error.");
                                    string base64 = Convert.ToBase64String(bytes);
                                    await webView.CoreWebView2.ExecuteScriptAsync($"window.updateInsightsFromBase64('{base64}', false, 0, '0.00', '0.00', 'Error');");
                                }
                            }
                            catch (Exception ex)
                            {
                                if (dialog.IsDisposed || !dialog.IsHandleCreated || webView.IsDisposed || webView.CoreWebView2 == null) return;
                                byte[] bytes = System.Text.Encoding.UTF8.GetBytes($"Error: {ex.Message}");
                                string base64 = Convert.ToBase64String(bytes);
                                await webView.CoreWebView2.ExecuteScriptAsync($"window.updateInsightsFromBase64('{base64}', false, 0, '0.00', '0.00', 'Error');");
                            }
                        };
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to load AI component: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            dialog.ShowDialog(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= ThemeChanged_Handler;
                _entranceTimer?.Dispose();
                _refreshTimer?.Stop();
                _refreshTimer?.Dispose();
                try { _canvas3DCard?.Dispose(); } catch { }
                try { _dashWebView?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
