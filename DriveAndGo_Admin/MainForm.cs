#nullable disable
using DriveAndGo_Admin.Helpers;
using DriveAndGo_Admin.Panels;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;

namespace DriveAndGo_Admin
{
    public class MainForm : Form
    {
        // ── Sidebar constants ──────────────────────────────────────────────────────
        private const int SidebarFullWidth      = 240;
        private const int SidebarCollapsedWidth = 64;
        private const int HeaderHeight          = 65;
        private const int SidebarAnimationDurationMs = 180;

        // ── FAB constants ──────────────────────────────────────────────────────────
        private const int FabSize   = 60;
        private const int FabMargin = 24;

        // ── State ─────────────────────────────────────────────────────────────────
        private bool         _sidebarCollapsed   = false;
        private UserControl  _activePanel;
        private readonly Dictionary<Type, UserControl> _panelCache = new();
        private HubConnection _hubConnection;
        private int          _unreadNotifCount   = 0;
        private List<dynamic> _notifications     = new();

        // ── Window drag state ─────────────────────────────────────────────────────
        private bool  _dragging;
        private Point _dragStart;

        // ── UI ─────────────────────────────────────────────────────────────────────
        private Panel    sidebarPanel;
        private Panel    headerPanel;
        private Panel    contentPanel;
        private Panel    activeIndicator;
        private Panel    _fabPanel;        // Floating Action Button
        private Panel    _chatFloatHost;   // Host container for floating chat (anchored BR on Form)

        private PictureBox picLogo;
        private Label      lblLogo;
        private Label      lblLogoSub;
        private Label      lblHeaderTitle;
        private Label      lblUserName;
        private Label      lblUserRole;
        private Panel      _userProfileCard;
        private Panel      _sidebarAvatarPanel;
        private Panel      _navContainer;
        private float      _profileHoverAlpha = 0f;
        private bool       _isProfileHovered  = false;
        private System.Windows.Forms.Timer _profileHoverTimer;

        private Button btnToggleSidebar;
        private Button btnThemeToggle;
        private Button btnNotifications;
        private Button activeButton;
        private Label  lblClock;
        private Panel  userAvatarPanel;

        // ── Custom window-chrome buttons ──────────────────────────────────────────
        private Button btnWinClose;
        private Button btnWinMaximize;
        private Button btnWinMinimize;

        // ── Chat overlay (lazy) ───────────────────────────────────────────────────
        private ChatOverlayPanel _chatOverlay;
        private bool _chatVisible = false;

        // ── Notification flyout ───────────────────────────────────────────────────
        private NotificationFlyoutPanel _notifFlyout;

        // ── Profile flyout ────────────────────────────────────────────────────────
        private UserProfileFlyoutPanel _profileFlyout;

        // ── Nav buttons ───────────────────────────────────────────────────────────
        private Button btnDashboard;
        private Button btnVehicles;
        private Button btnRentals;
        private Button btnDrivers;
        private Button btnTransactions;
        private Button btnReports;
        private Button btnCalendar;
        private Button btnDocVault;
        private Button btnExpenses;
        private Button btnSplitPay;
        private Button btnAccounts;
        private Button btnWeather;
        private Button btnLogout;

        // ── Sidebar dual-layer panels ─────────────────────────────────────────────
        // Two completely separate inner surfaces that cross-fade during toggle.
        // _sidebarFullLayer  : 240 px-wide full text+icon layout (always Dock=Fill)
        // _sidebarIconLayer  : 64 px-wide centred emoji-only layout  (always Dock=Fill)
        // The outer sidebarPanel clips whichever layer is visible as it animates width.
        private Panel _sidebarFullLayer;
        private Panel _sidebarIconLayer;
        private float _fullLayerAlpha = 1f;
        private float _iconLayerAlpha = 0f;

        // ── Mirrored icon-mode buttons (live only inside _sidebarIconLayer) ────────
        private Button[] _iconNavBtns;   // parallel to nav order
        private Button   _iconLogout;
        private Button   _iconActiveButton;

        // ── Animation ─────────────────────────────────────────────────────────────
        private System.Windows.Forms.Timer _animTimer;
        private System.Windows.Forms.Timer _sidebarTimer;
        private System.Windows.Forms.Timer _themeTimer;
        private System.Windows.Forms.Timer _clockTimer;

        // ── Panel transition (cinematic veil composite) ───────────────────────────

        // ── Mouse glow tracking ───────────────────────────────────────────────────
        private Point _mouseGlowPos;

        private float _targetIndicatorY  = 155;
        private float _currentIndicatorY = 155;
        private float _opacity           = 0f;

        private float _sidebarToggleProgress = 0f;
        private readonly Stopwatch _sidebarAnimationClock = new();

        // ── Theme fade state ──────────────────────────────────────────────────────
        private Color _fromBg, _toBg;
        private Color _fromSidebar, _toSidebar;
        private Color _fromText, _toText;
        private float _themeFade         = 1f;
        private bool  _themeTransitioning = false;

        // ── Ripple state per button ───────────────────────────────────────────────
        private class RippleState
        {
            public float X, Y, Radius, MaxRadius, Alpha;
            public System.Windows.Forms.Timer Timer;
        }
        private readonly Dictionary<Button, RippleState> _ripples = new();

        // ── FAB pulse ─────────────────────────────────────────────────────────────
        private float _fabGlowAlpha  = 0f;
        private float _fabGlowTarget = 0f;
        private System.Windows.Forms.Timer _fabPulseTimer;

        // ── Cinematic alpha-composite veil ────────────────────────────────────────
        /// <summary>
        /// A transparent panel that overlays the incoming content panel during transitions.
        /// Uses ControlStyles.SupportsTransparentBackColor so its Paint can draw a
        /// semi-opaque fill that "reveals" the underlying panel as alpha decreases.
        /// Equivalent to a ColorMatrix-driven alpha-composite in production WinForms.
        /// </summary>
        private sealed class TransitionVeil : Panel
        {
            public float Alpha = 1f; // 1 = fully opaque background, 0 = invisible

            public TransitionVeil()
            {
                SetStyle(
                    ControlStyles.SupportsTransparentBackColor |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint, true);
                BackColor = Color.Transparent;
                DoubleBuffered = true;
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                // Intentionally suppress — we composite manually
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                int a = Math.Max(0, Math.Min(255, (int)(255f * Alpha)));
                if (a <= 0) return;
                var bg = ThemeManager.CurrentBackground;
                e.Graphics.FillRectangle(
                    new SolidBrush(Color.FromArgb(a, bg.R, bg.G, bg.B)),
                    new Rectangle(0, 0, Width, Height));
            }
        }

        // ── CreateParams: drop-shadow for borderless window ───────────────────────
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ══════════════════════════════════════════════════════════════════════════
        public MainForm()
        {
            IconHelper.ApplyToForm(this);

            SetDoubleBuffer(this);
            InitializeForm();
            BuildSidebar();
            BuildHeader();
            BuildContent();
            EnableSmoothTransitions(sidebarPanel);
            EnableSmoothTransitions(contentPanel);
            BuildFAB();
            ApplyTheme(animated: false);
            StartAnimations();
            SetActiveButton(btnDashboard);
            NavigateTo<DashboardPanel>();
            InitializeSignalR();
            InitializeNetworkMonitoring();
            FetchUserProfileFromApiAsync();
        }

        private Panel _pnlOfflineWarning;
        private Label _lblOfflineWarningText;
        private System.Windows.Forms.Timer _netCheckTimer;
        private System.Windows.Forms.Timer _restoreBannerTimer;
        private bool _wasOffline = false;
        private bool _isCheckingNet = false;

        private void InitializeNetworkMonitoring()
        {
            _pnlOfflineWarning = new Panel
            {
                Height = 36,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(220, 38, 38), // Red default
                Visible = false
            };

            _lblOfflineWarningText = new Label
            {
                Text = "⚠️ No Internet Connection. Working offline — Live features & AI Copilot are paused.",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };

            _pnlOfflineWarning.Controls.Add(_lblOfflineWarningText);
            this.Controls.Add(_pnlOfflineWarning);
            this.Controls.SetChildIndex(_pnlOfflineWarning, 0);

            _restoreBannerTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _restoreBannerTimer.Tick += (s, e) =>
            {
                _restoreBannerTimer.Stop();
                if (!_wasOffline)
                {
                    _pnlOfflineWarning.Visible = false;
                }
            };

            _netCheckTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _netCheckTimer.Tick += async (s, e) =>
            {
                if (_isCheckingNet) return;
                _isCheckingNet = true;

                try
                {
                    bool isOnline = await CheckInternetConnectionAsync();
                    
                    if (this.IsDisposed || !this.IsHandleCreated) return;

                    if (!isOnline)
                    {
                        // 🔴 State: OFFLINE (Spotify Red Banner)
                        _wasOffline = true;
                        _restoreBannerTimer.Stop();
                        _pnlOfflineWarning.BackColor = Color.FromArgb(220, 38, 38); // Rich Crimson Red
                        _lblOfflineWarningText.Text = "⚠️ No Internet Connection. Working offline — Live features & AI Copilot are paused.";
                        _pnlOfflineWarning.Visible = true;
                    }
                    else if (_wasOffline)
                    {
                        // 🟢 State: JUST RESTORED (Spotify Emerald Green Banner)
                        _wasOffline = false;
                        _pnlOfflineWarning.BackColor = Color.FromArgb(16, 185, 129); // Vibrant Spotify Emerald Green
                        _lblOfflineWarningText.Text = "📶 Internet Connection Restored! You are back online.";
                        _pnlOfflineWarning.Visible = true;
                        _restoreBannerTimer.Stop();
                        _restoreBannerTimer.Start(); // Display green banner for 3.5s then auto-hide
                    }
                }
                catch
                {
                    // Swallowing exception in async void timer tick to prevent crashes
                }
                finally
                {
                    _isCheckingNet = false;
                }
            };
            _netCheckTimer.Start();
        }

        private static readonly HttpClient _netCheckClient = new HttpClient();

        private static async System.Threading.Tasks.Task<bool> CheckInternetConnectionAsync()
        {
            try
            {
                // 1. Hardware network interface check (ignore loopback & tunnel)
                bool hasActiveAdapter = System.Linq.Enumerable.Any(
                    System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces(),
                    ni => ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                          ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback &&
                          ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Tunnel);

                if (!hasActiveAdapter)
                {
                    return false;
                }
            }
            catch
            {
                if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) return false;
            }

            // 2. ICMP Ping check to 8.8.8.8 (Google Public DNS)
            // SendPingAsync returns IPStatus.TimedOut when offline — WITHOUT throwing any TaskCanceledException!
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = await ping.SendPingAsync("8.8.8.8", 1200);
                if (reply != null && reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                {
                    return true;
                }
            }
            catch
            {
            }

            // 3. Backup Ping check to 1.1.1.1 (Cloudflare DNS)
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = await ping.SendPingAsync("1.1.1.1", 1200);
                if (reply != null && reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                {
                    return true;
                }
            }
            catch
            {
            }

            // 4. HTTP fallback for networks blocking ICMP ping
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(1200);
                using var response = await _netCheckClient.GetAsync("http://clients3.google.com/generate_204", cts.Token);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  SIGNALR
        // ══════════════════════════════════════════════════════════════════════════
        private async void InitializeSignalR()
        {
            try
            {
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(ApiService.BaseUrl.Replace("/api", "") + "/hubs/admin")
                    .WithAutomaticReconnect()
                    .Build();

                _hubConnection.On("ReceiveVehicleUpdate", () =>
                {
                    if (this.IsDisposed || !this.IsHandleCreated) return;
                    this.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                    {
                        if (_activePanel is FleetPanel fleet)
                            fleet.RefreshWebViewData();
                    }));
                });

                _hubConnection.On("ReceiveDashboardUpdate", () =>
                {
                    if (this.IsDisposed || !this.IsHandleCreated) return;
                    this.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                    {
                        if (_activePanel is DashboardPanel d)
                            d.LoadStatsFromDB();
                    }));
                });

                _hubConnection.On("ReceiveAccountsUpdate", () =>
                {
                    if (this.IsDisposed || !this.IsHandleCreated) return;
                    this.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                    {
                        FetchUserProfileFromApiAsync();
                    }));
                });

                _hubConnection.On<JsonElement>("ReceiveNotification", (notif) =>
                {
                    if (this.IsDisposed || !this.IsHandleCreated) return;
                    this.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                    {
                        _unreadNotifCount++;
                        btnNotifications.Invalidate();
                        try
                        {
                            string title = notif.GetProperty("title").GetString();
                            string body  = notif.GetProperty("body").GetString();
                            _notifications.Insert(0, new { Title = title, Body = body, Time = DateTime.Now });

                            var tt = new ToolTip();
                            tt.ToolTipIcon  = ToolTipIcon.Info;
                            tt.ToolTipTitle = title;
                            tt.Show(body, btnNotifications, 20, 45, 5000);
                        }
                        catch { }
                    }));
                });

                await _hubConnection.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("SignalR failed: " + ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  INIT
        // ══════════════════════════════════════════════════════════════════════════
        private void InitializeForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;         // ← Frameless
            this.Size            = new Size(1280, 800);
            this.MinimumSize     = new Size(900, 600);
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.Text            = "Drive & Go — Admin Dashboard";
            this.Font            = new Font("Segoe UI", 10F);
            this.Opacity         = 0;
            this.WindowState     = FormWindowState.Maximized;
            this.Resize         += OnFormResize;
            this.KeyPreview      = true;
            this.KeyDown        += (s, e) =>
            {
                if (e.Alt && e.KeyCode == Keys.F4)
                {
                    Application.Exit();
                }
                else if (e.Control && e.KeyCode == Keys.K)
                {
                    e.SuppressKeyPress = true;
                    ToggleChatFloat();
                }
            };
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  THEME
        // ══════════════════════════════════════════════════════════════════════════
        private void ApplyTheme(bool animated = true)
        {
            if (animated && !_themeTransitioning)
            {
                _fromBg      = this.BackColor;
                _fromSidebar = sidebarPanel.BackColor;
                _fromText    = lblHeaderTitle.ForeColor;

                _toBg      = ThemeManager.CurrentBackground;
                _toSidebar = ThemeManager.CurrentSidebar;
                _toText    = ThemeManager.CurrentText;

                _themeFade         = 0f;
                _themeTransitioning = true;

                _themeTimer?.Dispose();
                _themeTimer = new System.Windows.Forms.Timer { Interval = 12 };
                _themeTimer.Tick += OnThemeFadeTick;
                _themeTimer.Start();
            }
            else
            {
                ApplyThemeImmediate();
            }
        }

        private void OnThemeFadeTick(object s, EventArgs e)
        {
            _themeFade += 0.07f;
            if (_themeFade >= 1f)
            {
                _themeFade = 1f;
                _themeTimer.Stop();
                _themeTimer.Dispose();
                _themeTransitioning = false;
            }

            Color Lerp(Color a, Color b, float t) => Color.FromArgb(
                Clamp((int)(a.A + (b.A - a.A) * t)),
                Clamp((int)(a.R + (b.R - a.R) * t)),
                Clamp((int)(a.G + (b.G - a.G) * t)),
                Clamp((int)(a.B + (b.B - a.B) * t)));

            var bg      = Lerp(_fromBg,      _toBg,      _themeFade);
            var sidebar = Lerp(_fromSidebar, _toSidebar, _themeFade);
            var text    = Lerp(_fromText,    _toText,    _themeFade);

            this.BackColor         = bg;
            contentPanel.BackColor = bg;
            headerPanel.BackColor  = bg;
            sidebarPanel.BackColor = sidebar;

            lblHeaderTitle.ForeColor   = text;
            btnToggleSidebar.ForeColor = text;
            lblUserName.ForeColor      = text;
            lblUserRole.ForeColor      = ThemeManager.CurrentPrimary;

            if (_themeFade >= 1f) ApplyThemeImmediate();
            else
            {
                btnThemeToggle.Invalidate();
                btnToggleSidebar.Invalidate();
                headerPanel.Invalidate();
                sidebarPanel.Invalidate();
            }
        }

        private void ApplyThemeImmediate()
        {
            this.SuspendLayout();
            sidebarPanel.SuspendLayout();
            contentPanel.SuspendLayout();
            headerPanel.SuspendLayout();

            this.BackColor         = ThemeManager.CurrentBackground;
            contentPanel.BackColor = ThemeManager.CurrentBackground;
            headerPanel.BackColor  = ThemeManager.CurrentBackground;
            sidebarPanel.BackColor = ThemeManager.CurrentSidebar;

            lblHeaderTitle.ForeColor   = ThemeManager.CurrentText;
            btnToggleSidebar.ForeColor = ThemeManager.CurrentText;

            if (lblClock != null) lblClock.ForeColor = ThemeManager.CurrentSubText;

            btnNotifications.BackColor = Color.Transparent;
            btnNotifications.ForeColor = ThemeManager.CurrentText;
            btnNotifications.Invalidate();

            lblLogo.ForeColor    = ThemeManager.CurrentPrimary;
            lblLogoSub.ForeColor = ThemeManager.CurrentSubText;
            lblUserName.ForeColor = ThemeManager.CurrentText;
            lblUserRole.ForeColor = ThemeManager.CurrentPrimary;

            activeIndicator.BackColor = ThemeManager.CurrentPrimary;

            if (_userProfileCard != null) _userProfileCard.BackColor = ThemeManager.CurrentCard;
            if (btnLogout != null) btnLogout.BackColor = ThemeManager.CurrentCard;

            // Update chrome buttons to match new theme
            if (btnWinClose != null)
            {
                btnWinClose.ForeColor    = ThemeManager.CurrentText;
                btnWinMaximize.ForeColor = ThemeManager.CurrentText;
                btnWinMinimize.ForeColor = ThemeManager.CurrentText;
            }

            // Apply theme to nav buttons inside the new container
            if (_navContainer != null)
            {
                foreach (Control c in _navContainer.Controls)
                {
                    if (c is Button btn && btn != btnLogout)
                    {
                        btn.BackColor = (btn == activeButton) ? ThemeManager.NavActiveBg : Color.Transparent;
                        btn.ForeColor = (btn == activeButton) ? ThemeManager.CurrentPrimary : ThemeManager.CurrentText;
                    }
                }
            }

            // Also update icon layer buttons
            if (_iconNavBtns != null)
            {
                foreach (var ib in _iconNavBtns)
                {
                    if (ib == null) continue;
                    ib.ForeColor = (ib == _iconActiveButton)
                        ? ThemeManager.CurrentPrimary
                        : ThemeManager.CurrentText;
                    ib.BackColor = (ib == _iconActiveButton)
                        ? (ThemeManager.IsDarkMode ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(20, 0, 0, 0))
                        : Color.Transparent;
                }
            }

            // Propagate theme to cached panels without rebuilding them
            foreach (var kvp in _panelCache)
            {
                var p = kvp.Value;
                if (p != null && !p.IsDisposed)
                    p.BackColor = ThemeManager.CurrentBackground;
            }

            this.ResumeLayout(false);
            sidebarPanel.ResumeLayout(false);
            contentPanel.ResumeLayout(false);
            headerPanel.ResumeLayout(false);

            _chatOverlay?.ApplyTheme();

            btnThemeToggle.Invalidate();
            btnToggleSidebar.Invalidate();
            userAvatarPanel?.Invalidate();
            _userProfileCard?.Invalidate();
            _sidebarAvatarPanel?.Invalidate();
            picLogo?.Parent?.Invalidate(true);
            picLogo?.Invalidate();
            headerPanel.Invalidate();
            sidebarPanel.Invalidate();
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  SIDEBAR
        // ══════════════════════════════════════════════════════════════════════════
        private void BuildSidebar()
        {
            sidebarPanel       = new Panel();
            SetDoubleBuffer(sidebarPanel);
            sidebarPanel.Width = SidebarFullWidth;
            sidebarPanel.Dock  = DockStyle.Left;
            sidebarPanel.Paint += OnSidebarPaint;

            _profileHoverTimer = new System.Windows.Forms.Timer { Interval = 15 };
            _profileHoverTimer.Tick += (s, e) =>
            {
                float target = _isProfileHovered ? 1f : 0f;
                float diff   = target - _profileHoverAlpha;
                if (Math.Abs(diff) < 0.02f)
                {
                    _profileHoverAlpha = target;
                    _profileHoverTimer.Stop();
                }
                else
                {
                    _profileHoverAlpha += diff * 0.25f;
                }
                _userProfileCard?.Invalidate();
            };

            // ── Build the two inner layers ──────────────────────────────────────────
            BuildSidebarFullLayer();
            BuildSidebarIconLayer();

            // Stack: full layer on top initially (expanded state)
            sidebarPanel.Controls.Add(_sidebarIconLayer);
            sidebarPanel.Controls.Add(_sidebarFullLayer);
            _sidebarFullLayer.BringToFront();

            this.Controls.Add(sidebarPanel);
        }

        // ── Full-mode layer (240 px): logo, labels, full text nav buttons ──────────
        private void BuildSidebarFullLayer()
        {
            _sidebarFullLayer = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            SetDoubleBuffer(_sidebarFullLayer);

            // Logo area
            picLogo = new PictureBox
            {
                Size     = new Size(38, 38),
                Location = new Point(13, 28),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            try { picLogo.Image = Properties.Resources.DriveAndGo_Logo; } catch { }

            lblLogo = new Label
            {
                Text        = "Drive&Go",
                UseMnemonic = false,
                Font        = new Font("Segoe UI", 14F, FontStyle.Bold),
                Location    = new Point(56, 24),
                AutoSize    = true,
                BackColor   = Color.Transparent
            };

            lblLogoSub = new Label
            {
                Text      = "Admin Portal",
                Font      = new Font("Segoe UI", 8F),
                Location  = new Point(58, 48),
                AutoSize  = true,
                BackColor = Color.Transparent
            };

            // ── CUSTOM SCROLLABLE NAV CONTAINER ──
            _navContainer = new Panel
            {
                Location = new Point(0, 95),
                Size = new Size(SidebarFullWidth, sidebarPanel.Height - 220),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = Color.Transparent,
                AutoScroll = false // Kill native scrollbar
            };
            SetDoubleBuffer(_navContainer);

            activeIndicator = new Panel { Size = new Size(4, 34), Location = new Point(0, 15), BackColor = Color.Transparent };
            activeIndicator.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(new SolidBrush(ThemeManager.CurrentPrimary), GetRoundedRect(new Rectangle(0, 0, 4, activeIndicator.Height), 2));
            };

            btnDashboard    = CreateNavButton("Dashboard",    "\uE9D9", 10);
            btnVehicles     = CreateNavButton("Fleet",        "\uE804", 62);
            btnRentals      = CreateNavButton("Rentals",      "\uE70F", 114);
            btnDrivers      = CreateNavButton("Drivers",      "\uE77B", 166);
            btnTransactions = CreateNavButton("Transactions", "\uE8C7", 218);
            btnReports      = CreateNavButton("Reports",      "\uE9F5", 270);
            btnCalendar     = CreateNavButton("Calendar",     "\uE787", 322);
            btnDocVault     = CreateNavButton("Doc Vault",    "\uE8F1", 374);
            btnExpenses     = CreateNavButton("Expenses",     "\uE945", 426);
            btnSplitPay     = CreateNavButton("Split Pay",    "\uE8D4", 478);
            btnAccounts     = CreateNavButton("Accounts",     "\uE716", 530);
            btnWeather      = CreateNavButton("Weather Radar", "\uE706", 582);

            _navContainer.Controls.AddRange(new Control[] {
                activeIndicator, btnDashboard, btnVehicles, btnRentals, btnDrivers,
                btnTransactions, btnReports, btnCalendar, btnDocVault,
                btnExpenses, btnSplitPay, btnAccounts, btnWeather
            });

            // Smooth MouseWheel Logic
            int totalNavHeight = 634; // 582 + btn.Height
            _navContainer.MouseWheel += (s, e) => {
                int maxScroll = Math.Max(0, totalNavHeight - _navContainer.Height);
                if (maxScroll <= 0) return;
                int delta = -e.Delta / 3;
                int currentY = btnDashboard.Top - 10;
                int newY = Math.Clamp(currentY - delta, -maxScroll, 0);
                int offset = newY - currentY;
                
                foreach (Control c in _navContainer.Controls) c.Top += offset;
                _targetIndicatorY += offset;
                _currentIndicatorY += offset;
            };
            // Forward wheel events from buttons to container
            foreach (Control c in _navContainer.Controls) {
                c.MouseWheel += (s, e) => {
                    var args = new MouseEventArgs(e.Button, e.Clicks, e.X, e.Y, e.Delta);
                    typeof(Panel).GetMethod("OnMouseWheel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_navContainer, new object[] { args });
                };
            }

            btnDashboard.Click    += (s, e) => { SetActiveButton(btnDashboard);    NavigateTo<DashboardPanel>(); };
            btnVehicles.Click     += (s, e) => { SetActiveButton(btnVehicles);     NavigateTo<FleetPanel>(); };
            btnRentals.Click      += (s, e) => { SetActiveButton(btnRentals);      NavigateTo<RentalsPanel>(); };
            btnDrivers.Click      += (s, e) => { SetActiveButton(btnDrivers);      NavigateTo<DriversPanel>(); };
            btnTransactions.Click += (s, e) => { SetActiveButton(btnTransactions); NavigateTo<TransactionsPanel>(); };
            btnReports.Click      += (s, e) => { SetActiveButton(btnReports);      NavigateTo<ReportsPanel>(); };
            btnCalendar.Click     += (s, e) => { SetActiveButton(btnCalendar);     NavigateTo<CalendarPanel>(); };
            btnDocVault.Click     += (s, e) => { SetActiveButton(btnDocVault);     NavigateTo<DocumentVaultPanel>(); };
            btnExpenses.Click     += (s, e) => { SetActiveButton(btnExpenses);     NavigateTo<ExpensesPanel>(); };
            btnSplitPay.Click     += (s, e) => { SetActiveButton(btnSplitPay);     NavigateTo<SplitPaymentsPanel>(); };
            btnAccounts.Click     += (s, e) => { SetActiveButton(btnAccounts);     NavigateTo<AccountsPanel>(); };
            btnWeather.Click      += (s, e) => { SetActiveButton(btnWeather);      NavigateTo<WeatherPanel>(); };

            // ── Unified SaaS Profile Card (bottom-anchored) ─────────────────────────
            const int _cardH = 104;
            _userProfileCard = new Panel
            {
                Size      = new Size(SidebarFullWidth - 16, _cardH),
                Location  = new Point(8, 600), // Repositioned immediately by Resize handler below
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };
            SetDoubleBuffer(_userProfileCard);

            // Keep card pinned to the bottom and navContainer height in sync on every resize
            _sidebarFullLayer.Resize += (s, e) =>
            {
                if (_userProfileCard == null || _userProfileCard.IsDisposed) return;
                _userProfileCard.Top = _sidebarFullLayer.Height - 116;
                if (_navContainer != null && !_navContainer.IsDisposed)
                    _navContainer.Height = Math.Max(0, _sidebarFullLayer.Height - 220);
            };

            // ── Glassmorphic & Hover Glow Paint ─────────────────────────────────────
            _userProfileCard.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Prevent the WinForms transparent-overlap "horns" artefact
                g.Clear(ThemeManager.CurrentSidebar);

                bool dark  = ThemeManager.IsDarkMode;
                var  rect  = new Rectangle(1, 1, _userProfileCard.Width - 3, _userProfileCard.Height - 3);
                using var path = GetRoundedRect(rect, 12);

                // Background: deep slate (dark mode) / soft off-white (light mode)
                g.FillPath(new SolidBrush(dark
                    ? Color.FromArgb(16, 16, 26)
                    : Color.FromArgb(248, 248, 255)), path);

                // 1px border using CurrentBorder with slight transparency
                int bAlpha = dark ? 30 : 180;
                g.DrawPath(new Pen(Color.FromArgb(bAlpha, ThemeManager.CurrentBorder), 1f), path);

                // Hover glow state (metric card effect)
                if (_profileHoverAlpha > 0.01f)
                {
                    int glowBgAlpha = (int)(25 * _profileHoverAlpha);
                    using var glowBrush = new SolidBrush(Color.FromArgb(glowBgAlpha, ThemeManager.CurrentPrimary));
                    g.FillPath(glowBrush, path);

                    int glowBorderAlpha = (int)(160 * _profileHoverAlpha);
                    using var glowPen = new Pen(Color.FromArgb(glowBorderAlpha, ThemeManager.CurrentPrimary), 1.5f);
                    g.DrawPath(glowPen, path);
                }

                // Horizontal separator line at Y = 58
                int sepY = 58;
                g.DrawLine(new Pen(Color.FromArgb(bAlpha, ThemeManager.CurrentBorder), 1f),
                    10, sepY, _userProfileCard.Width - 10, sepY);
            };

            // ── Avatar panel (38×38) ─────────────────────────────────────────────────
            _sidebarAvatarPanel = new Panel
            {
                Size      = new Size(38, 38),
                Location  = new Point(10, 10),
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };
            SetDoubleBuffer(_sidebarAvatarPanel);
            _sidebarAvatarPanel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode      = SmoothingMode.AntiAlias;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                var r = new Rectangle(0, 0, 37, 37);

                if (SessionManager.CustomAvatar != null)
                {
                    try
                    {
                        using var cp = new System.Drawing.Drawing2D.GraphicsPath();
                        cp.AddEllipse(r);
                        var oc = g.Clip;
                        g.SetClip(cp);
                        g.DrawImage(SessionManager.CustomAvatar, r);
                        g.Clip = oc;
                    }
                    catch { }
                }
                else
                {
                    // Translucent primary circle + first-letter initial centred inside
                    g.FillEllipse(new SolidBrush(Color.FromArgb(40, ThemeManager.CurrentPrimary)), r);
                    using var f   = new Font("Segoe UI", 12F, FontStyle.Bold);
                    using var fmt = new StringFormat
                        { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    string ini = (SessionManager.FullName?.Length ?? 0) > 0
                        ? SessionManager.FullName.Substring(0, 1) : "A";
                    g.DrawString(ini, f, new SolidBrush(ThemeManager.CurrentPrimary),
                        new RectangleF(0, 0, 38, 38), fmt);
                }

                // Online status indicator — green dot with a sidebar-coloured border ring
                var dot = new Rectangle(25, 25, 11, 11);
                g.FillEllipse(new SolidBrush(ThemeManager.CurrentAccentGreen), dot);
                g.DrawEllipse(new Pen(ThemeManager.CurrentSidebar, 2f), dot);
            };

            // ── User name label (bold, near avatar top) ──────────────────────────────
            lblUserName = new Label
            {
                Text      = SessionManager.UserId > 0 ? SessionManager.FullName : "Admin User",
                Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Location  = new Point(56, 12),
                AutoSize  = true,
                BackColor = Color.Transparent,
                ForeColor = ThemeManager.CurrentText,
                Cursor    = Cursors.Hand
            };

            // ── User role label (small, primary-coloured, below name) ─────────────────
            lblUserRole = new Label
            {
                Text      = SessionManager.UserId > 0
                    ? (SessionManager.Role?.ToUpper() ?? "ADMIN") : "ADMIN",
                Font      = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                Location  = new Point(56, 32),
                AutoSize  = true,
                BackColor = Color.Transparent,
                ForeColor = ThemeManager.CurrentPrimary,
                Cursor    = Cursors.Hand
            };

            // ── Logout button (below separator, flat + red, hover gives faint red bg) ──
            btnLogout = new Button
            {
                Text      = "Log Out",
                Size      = new Size(_userProfileCard.Width - 16, 32),
                Location  = new Point(8, 64),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(239, 68, 68),
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };
            btnLogout.FlatAppearance.BorderSize         = 0;
            btnLogout.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnLogout.FlatAppearance.MouseDownBackColor = Color.Transparent;
            SetRoundRegion(btnLogout, 6);
            btnLogout.Click += (s, e) => PerformLogout();

            btnLogout.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                Color fg = btnLogout.ForeColor;
                using var iconFont = new Font("Segoe MDL2 Assets", 10F, FontStyle.Regular);
                using var textFont = new Font("Segoe UI", 9F, FontStyle.Bold);
                using var brush    = new SolidBrush(fg);

                string glyph = "\uE7E8";
                string txt   = "Log Out";
                int h        = btnLogout.Height;

                SizeF iconSize = g.MeasureString(glyph, iconFont);
                SizeF textSize = g.MeasureString(txt, textFont);

                float iconX = 10f;
                float iconY = (h - iconSize.Height) / 2f + 0.5f;

                float gap   = 10f;
                float textX = iconX + 14f + gap;
                float textY = (h - textSize.Height) / 2f;

                g.Clear(btnLogout.Parent?.BackColor ?? ThemeManager.CurrentCard);

                if (btnLogout.ClientRectangle.Contains(btnLogout.PointToClient(Cursor.Position)))
                {
                    using var hoverBrush = new SolidBrush(Color.FromArgb(15, 239, 68, 68));
                    using var path = GetRoundedRect(new Rectangle(0, 0, btnLogout.Width, btnLogout.Height), 6);
                    g.FillPath(hoverBrush, path);
                }

                g.DrawString(glyph, iconFont, brush, new PointF(iconX, iconY));
                g.DrawString(txt, textFont, brush, new PointF(textX, textY));
            };

            // ── Assemble card ────────────────────────────────────────────────────────
            _userProfileCard.Controls.AddRange(new Control[]
            {
                _sidebarAvatarPanel, lblUserName, lblUserRole, btnLogout
            });

            // ── Profile flyout: clicking card, avatar, name, or role opens the flyout ─
            EventHandler openProfile = (s, e) => ToggleProfileFlyout();
            _userProfileCard.Click    += openProfile;
            _sidebarAvatarPanel.Click += openProfile;
            lblUserName.Click         += openProfile;
            lblUserRole.Click         += openProfile;

            // ── Hover events to prevent flicker ──────────────────────────────────────
            Action<bool> triggerProfileHover = (hover) =>
            {
                _isProfileHovered = hover;
                _profileHoverTimer?.Start();
            };

            _userProfileCard.MouseEnter += (s, e) => triggerProfileHover(true);
            _userProfileCard.MouseLeave += (s, e) =>
            {
                if (!_userProfileCard.ClientRectangle.Contains(_userProfileCard.PointToClient(Cursor.Position)))
                    triggerProfileHover(false);
            };

            foreach (Control child in _userProfileCard.Controls)
            {
                child.MouseEnter += (s, e) => triggerProfileHover(true);
                child.MouseLeave += (s, e) =>
                {
                    if (!_userProfileCard.ClientRectangle.Contains(_userProfileCard.PointToClient(Cursor.Position)))
                        triggerProfileHover(false);
                };
            }

            _sidebarFullLayer.Controls.AddRange(new Control[]
            {
                picLogo, lblLogo, lblLogoSub,
                _navContainer,
                _userProfileCard
            });
        }

        // ── Icon-mode layer (64 px): centred emojis, avatar circle, mini logout ─────
        private void BuildSidebarIconLayer()
        {
            const int iconBtnW  = SidebarCollapsedWidth - 10; // 54 px
            const int iconBtnH  = 46;
            const int startY    = 100;
            const int step      = 50;

            _sidebarIconLayer = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            SetDoubleBuffer(_sidebarIconLayer);

            // ── Centred logo icon ──
            var picLogoIcon = new PictureBox
            {
                Size      = new Size(36, 36),
                Location  = new Point((SidebarCollapsedWidth - 36) / 2, 28),
                SizeMode  = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            try { picLogoIcon.Image = Properties.Resources.DriveAndGo_Logo; } catch { }
            _sidebarIconLayer.Controls.Add(picLogoIcon);

            // ── Centred nav icon buttons ──
            string[] icons = { "\uE9D9", "\uE804", "\uE70F", "\uE77B", "\uE8C7", "\uE9F5", "\uE787", "\uE8F1", "\uE945", "\uE8D4", "\uE716" };
            string[] labels = { "Dashboard", "Fleet", "Rentals", "Drivers", "Transactions", "Reports", "Calendar", "Doc Vault", "Expenses", "Split Pay", "Accounts" };
            Button[] fullBtns = { btnDashboard, btnVehicles, btnRentals, btnDrivers, btnTransactions, btnReports, btnCalendar, btnDocVault, btnExpenses, btnSplitPay, btnAccounts };

            _iconNavBtns = new Button[icons.Length];
            for (int i = 0; i < icons.Length; i++)
            {
                int capturedIdx = i;
                string capturedLabel = labels[i];
                Button fullBtn = fullBtns[i];

                var iconBtn = new Button
                {
                    Text      = icons[i],
                    Size      = new Size(iconBtnW, iconBtnH),
                    Location  = new Point((SidebarCollapsedWidth - iconBtnW) / 2, startY + i * step),
                    FlatStyle = FlatStyle.Flat,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font      = new Font("Segoe MDL2 Assets", 13F),
                    BackColor = Color.Transparent,
                    Cursor    = Cursors.Hand,
                    ForeColor = ThemeManager.CurrentText
                };
                iconBtn.FlatAppearance.BorderSize = 0;
                iconBtn.FlatAppearance.MouseOverBackColor = Color.Transparent;
                iconBtn.FlatAppearance.MouseDownBackColor = Color.Transparent;
                SetRoundRegion(iconBtn, 8);

                // Tooltip shows the full name when collapsed
                var tt = new ToolTip { AutoPopDelay = 3000, InitialDelay = 300 };
                tt.SetToolTip(iconBtn, capturedLabel);

                // Mirror click to full button so navigation + active state logic is shared
                iconBtn.Click += (s, ev) =>
                {
                    fullBtn.PerformClick();
                    SyncIconActiveButton(iconBtn);
                };

                _iconNavBtns[i] = iconBtn;
                _sidebarIconLayer.Controls.Add(iconBtn);
            }

            // ── Mini avatar circle ──
            var miniAvatar = new Panel
            {
                Size      = new Size(36, 36),
                Location  = new Point((SidebarCollapsedWidth - 36) / 2, _sidebarIconLayer.Height == 0 ? 690 : _sidebarIconLayer.Height - 105),
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = Color.Transparent
            };
            SetDoubleBuffer(miniAvatar);
            miniAvatar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode      = SmoothingMode.AntiAlias;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                var r = new Rectangle(0, 0, 35, 35);

                if (SessionManager.CustomAvatar != null)
                {
                    try
                    {
                        using var cp = new System.Drawing.Drawing2D.GraphicsPath();
                        cp.AddEllipse(r);
                        var oc = g.Clip;
                        g.SetClip(cp);
                        g.DrawImage(SessionManager.CustomAvatar, r);
                        g.Clip = oc;
                    }
                    catch { }
                }
                else
                {
                    using var grad = new LinearGradientBrush(r, ThemeManager.CurrentPrimary, ThemeManager.CurrentPrimaryGlow, LinearGradientMode.ForwardDiagonal);
                    g.FillEllipse(grad, r);
                    using var f   = new Font("Segoe UI", 11F, FontStyle.Bold);
                    using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    string ini = (SessionManager.FullName?.Length ?? 0) > 0 ? SessionManager.FullName.Substring(0, 1) : "A";
                    g.DrawString(ini, f, Brushes.White, new RectangleF(0, 0, 36, 36), fmt);
                }

                // Online status indicator — green dot
                var dot = new Rectangle(24, 24, 11, 11);
                g.FillEllipse(new SolidBrush(ThemeManager.CurrentAccentGreen), dot);
                g.DrawEllipse(new Pen(ThemeManager.CurrentSidebar, 2f), dot);
            };
            _sidebarIconLayer.Controls.Add(miniAvatar);

            // ── Mini logout icon button ──
            _iconLogout = new Button
            {
                Text      = "\uE7E8",
                Size      = new Size(iconBtnW, 36),
                Location  = new Point((SidebarCollapsedWidth - iconBtnW) / 2, miniAvatar.Top == 0 ? 730 : miniAvatar.Top + 42),
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Left,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe MDL2 Assets", 12F),
                ForeColor = Color.FromArgb(239, 68, 68),
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };
            _iconLogout.FlatAppearance.BorderSize           = 0;
            _iconLogout.FlatAppearance.MouseOverBackColor   = Color.FromArgb(15, 239, 68, 68);
            SetRoundRegion(_iconLogout, 6);
            _iconLogout.Click += (s, e) => PerformLogout();
            var ttLogout = new ToolTip();
            ttLogout.SetToolTip(_iconLogout, "Log Out");
            _sidebarIconLayer.Controls.Add(_iconLogout);

            // Start invisible — full layer is on top
            _sidebarIconLayer.Visible = false;
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  HEADER — Custom window chrome + drag support
        // ══════════════════════════════════════════════════════════════════════════
        private void BuildHeader()
        {
            headerPanel = new Panel();
            SetDoubleBuffer(headerPanel);
            headerPanel.Height = HeaderHeight;
            headerPanel.Dock   = DockStyle.Top;
            headerPanel.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(
                    new Pen(ThemeManager.CurrentBorder, 1),
                    0, HeaderHeight - 1, headerPanel.Width, HeaderHeight - 1);
            };

            // ── Drag-to-move the frameless window ──────────────────────────────────
            headerPanel.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    _dragging  = true;
                    _dragStart = e.Location;
                }
            };
            headerPanel.MouseMove += (s, e) =>
            {
                if (!_dragging) return;
                if (this.WindowState == FormWindowState.Maximized)
                {
                    this.WindowState = FormWindowState.Normal;
                    // Reposition so cursor stays under title bar
                    this.Location = new Point(
                        Cursor.Position.X - this.Width  / 2,
                        Cursor.Position.Y - 20);
                    _dragStart = new Point(this.Width / 2, 20);
                    return;
                }
                this.Left += e.X - _dragStart.X;
                this.Top  += e.Y - _dragStart.Y;
            };
            headerPanel.MouseUp += (s, e) => _dragging = false;
            headerPanel.MouseDoubleClick += (s, e) =>
            {
                this.WindowState = (this.WindowState == FormWindowState.Maximized)
                    ? FormWindowState.Normal : FormWindowState.Maximized;
                UpdateMaximizeIcon();
            };

            // ── Hamburger toggle ───────────────────────────────────────────────────
            btnToggleSidebar = new Button
            {
                Text      = "",
                Size      = new Size(40, 40),
                Location  = new Point(16, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };
            btnToggleSidebar.FlatAppearance.BorderSize           = 0;
            btnToggleSidebar.FlatAppearance.MouseOverBackColor   = Color.FromArgb(20, 128, 128, 128);
            btnToggleSidebar.FlatAppearance.MouseDownBackColor   = Color.Transparent;
            btnToggleSidebar.Click += OnToggleSidebar;
            SetDoubleBuffer(btnToggleSidebar);
            btnToggleSidebar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(ThemeManager.CurrentText, 2.5f)
                {
                    StartCap = LineCap.Round,
                    EndCap   = LineCap.Round
                };
                float cx = btnToggleSidebar.Width  / 2f;
                float cy = btnToggleSidebar.Height / 2f;
                float w  = 8f;
                float p = Clamp01(_sidebarToggleProgress);

                PointF topStart = LerpPoint(new PointF(cx - w, cy - 6), new PointF(cx - 4, cy - 6), p);
                PointF topEnd   = LerpPoint(new PointF(cx + w, cy - 6), new PointF(cx + 2, cy),     p);
                PointF midStart = LerpPoint(new PointF(cx - w, cy),     new PointF(cx - 6, cy),     p);
                PointF midEnd   = LerpPoint(new PointF(cx + w, cy),     new PointF(cx + 2, cy),     p);
                PointF botStart = LerpPoint(new PointF(cx - w, cy + 6), new PointF(cx - 4, cy + 6), p);
                PointF botEnd   = LerpPoint(new PointF(cx + w, cy + 6), new PointF(cx + 2, cy),     p);

                g.DrawLine(pen, topStart, topEnd);
                g.DrawLine(pen, midStart, midEnd);
                g.DrawLine(pen, botStart, botEnd);
            };

            // ── Page title ────────────────────────────────────────────────────────
            lblHeaderTitle = new Label
            {
                Text      = "Dashboard",
                Font      = new Font("Segoe UI", 16F, FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(66, 18),
                BackColor = Color.Transparent
            };

            // ── Live clock ────────────────────────────────────────────────────────
            lblClock = new Label
            {
                AutoSize  = false,
                Size      = new Size(195, 18),
                Font      = new Font("Segoe UI", 9F),
                ForeColor = ThemeManager.CurrentSubText,
                BackColor = Color.Transparent,
                Text      = DateTime.Now.ToString("ddd, MMM dd  hh:mm:ss tt")
            };
            headerPanel.Controls.Add(lblClock);

            _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _clockTimer.Tick += (s, e) =>
            {
                if (lblClock != null && !lblClock.IsDisposed)
                    lblClock.Text = DateTime.Now.ToString("ddd, MMM dd  hh:mm:ss tt");
            };
            _clockTimer.Start();

            // ── User avatar circle ────────────────────────────────────────────────
            userAvatarPanel = new Panel
            {
                Size      = new Size(36, 36),
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };
            SetDoubleBuffer(userAvatarPanel);
            userAvatarPanel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode     = SmoothingMode.AntiAlias;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                int w = userAvatarPanel.Width;
                int h = userAvatarPanel.Height;
                var r = new Rectangle(0, 0, w - 1, h - 1);

                if (SessionManager.CustomAvatar != null)
                {
                    try
                    {
                        using var path = new System.Drawing.Drawing2D.GraphicsPath();
                        path.AddEllipse(r);
                        var oldClip = g.Clip;
                        g.SetClip(path);
                        g.DrawImage(SessionManager.CustomAvatar, r);
                        g.Clip = oldClip;
                    }
                    catch { }
                }
                else
                {
                    // Gradient background circle
                    using var grad = new LinearGradientBrush(
                        r, ThemeManager.CurrentPrimary, ThemeManager.CurrentPrimaryGlow,
                        LinearGradientMode.ForwardDiagonal);
                    g.FillEllipse(grad, r);

                    // ── Vector silhouette (head circle + shoulders arc) ────────
                    int headDiam = (int)(w * 0.38f);
                    int headX    = (w - headDiam) / 2;
                    int headY    = (int)(h * 0.17f);
                    using var whiteBrush = new SolidBrush(Color.FromArgb(220, 255, 255, 255));
                    g.FillEllipse(whiteBrush, headX, headY, headDiam, headDiam);

                    int shoulderW = (int)(w * 0.72f);
                    int shoulderH = (int)(h * 0.48f);
                    int shoulderX = (w - shoulderW) / 2;
                    int shoulderY = (int)(h * 0.56f);

                    using var clipPath = new System.Drawing.Drawing2D.GraphicsPath();
                    clipPath.AddEllipse(r);
                    var prevClip = g.Clip;
                    g.SetClip(clipPath);
                    g.FillEllipse(whiteBrush, shoulderX, shoulderY, shoulderW, shoulderH);
                    g.Clip = prevClip;
                }

                // Thin accent ring
                using var ringPen = new Pen(Color.FromArgb(80, 255, 255, 255), 1.5f);
                g.DrawEllipse(ringPen, r);
            };
            headerPanel.Controls.Add(userAvatarPanel);
            userAvatarPanel.Click += (s, e) => ToggleProfileFlyout();

            // ── Notifications button ──────────────────────────────────────────────
            btnNotifications = new Button
            {
                Text      = "",
                Size      = new Size(40, 40),
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                BackColor = Color.Transparent
            };
            btnNotifications.FlatAppearance.BorderSize           = 0;
            btnNotifications.FlatAppearance.MouseOverBackColor   = Color.Transparent;
            btnNotifications.FlatAppearance.MouseDownBackColor  = Color.Transparent;
            AttachRipple(btnNotifications, ThemeManager.CurrentPrimary);

            // ── Badge & Icon paint ────────────────────────────────────────────────
            btnNotifications.Paint += OnNotifButtonPaint;

            // ── Flyout open/close on click ────────────────────────────────────────
            btnNotifications.Click += (s, e) => ToggleNotifFlyout();

            // ── Theme toggle ──────────────────────────────────────────────────────
            btnThemeToggle = new Button
            {
                Size      = new Size(70, 36),
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                Text      = "",
                BackColor = Color.Transparent
            };
            btnThemeToggle.FlatAppearance.BorderSize           = 0;
            btnThemeToggle.FlatAppearance.MouseOverBackColor   = Color.Transparent;

            float _knobX      = ThemeManager.IsDarkMode ? btnThemeToggle.Width - 32 : 4f;
            float _knobTarget = _knobX;
            var   _knobTimer  = new System.Windows.Forms.Timer { Interval = 12 };

            btnThemeToggle.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(headerPanel.BackColor);

                var rect = new Rectangle(0, 0, btnThemeToggle.Width - 1, btnThemeToggle.Height - 1);
                using var path = GetRoundedRect(rect, 18);
                g.FillPath(new SolidBrush(ThemeManager.CurrentCard), path);
                g.DrawPath(new Pen(ThemeManager.CurrentBorder, 1f), path);

                int circleSize = 28;
                var circleRect = new RectangleF(_knobX, 4, circleSize, circleSize);
                Color circleColor = ThemeManager.IsDarkMode
                    ? ThemeManager.CurrentPrimary : Color.FromArgb(245, 158, 11);

                using (var glow = new GraphicsPath())
                {
                    glow.AddEllipse(circleRect.X - 3, circleRect.Y - 3, circleSize + 6, circleSize + 6);
                    using var gb = new PathGradientBrush(glow);
                    gb.CenterColor    = Color.FromArgb(60, circleColor);
                    gb.SurroundColors = new[] { Color.Transparent };
                    g.FillPath(gb, glow);
                }

                g.FillEllipse(new SolidBrush(circleColor), circleRect);

                string icon = ThemeManager.IsDarkMode ? "🌙" : "☀️";
                using var font = new Font("Segoe UI Emoji", 10F);
                var sf = new StringFormat
                    { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(icon, font, Brushes.White, circleRect, sf);
            };

            _knobTimer.Tick += (s, e) =>
            {
                float diff = _knobTarget - _knobX;
                if (Math.Abs(diff) < 0.5f) { _knobX = _knobTarget; _knobTimer.Stop(); }
                else _knobX += diff * 0.22f;
                btnThemeToggle.Invalidate();
            };

            btnThemeToggle.Click += (s, e) =>
            {
                ThemeManager.IsDarkMode = !ThemeManager.IsDarkMode;
                _knobTarget = ThemeManager.IsDarkMode ? btnThemeToggle.Width - 32 : 4f;
                _knobTimer.Start();
                ApplyTheme(animated: true);

                // ── Push theme to React Dashboard WebView2 via CSS variable bridge ──
                // window.setDashboardTheme is defined in DashboardOverview.html
                if (_activePanel is DashboardPanel dp)
                {
                    dp.PushThemeToWebView(ThemeManager.IsDarkMode ? "dark" : "light");
                }
                if (_globalNotifWebView != null && _globalNotifWebView.CoreWebView2 != null)
                {
                    string themeMode = ThemeManager.IsDarkMode ? "dark" : "light";
                    _globalNotifWebView.CoreWebView2.ExecuteScriptAsync($"if(window.setFlyoutTheme) window.setFlyoutTheme('{themeMode}');");
                }
            };

            // ── Custom window chrome buttons (✕ 🗖 🗕) ───────────────────────────
            BuildWindowChrome();

            headerPanel.Controls.AddRange(new Control[]
            {
                btnToggleSidebar, lblHeaderTitle,
                btnNotifications, btnThemeToggle
            });

            // Apply correct flex-gap layout immediately
            RepositionHeaderControls();

            this.Controls.Add(headerPanel);
        }

        /// <summary>Builds Minimize, Maximize, Close buttons for the frameless window.</summary>
        private void BuildWindowChrome()
        {
            // Close
            btnWinClose = new Button
            {
                Text      = "✕",
                Size      = new Size(46, HeaderHeight),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 11F),
                ForeColor = ThemeManager.CurrentText,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };
            btnWinClose.FlatAppearance.BorderSize           = 0;
            btnWinClose.FlatAppearance.MouseOverBackColor   = Color.FromArgb(220, 50, 50);
            btnWinClose.FlatAppearance.MouseDownBackColor   = Color.FromArgb(180, 30, 30);
            btnWinClose.Click += (s, e) => Application.Exit();
            headerPanel.Controls.Add(btnWinClose);

            // Maximize/Restore
            btnWinMaximize = new Button
            {
                Text      = "🗖",
                Size      = new Size(46, HeaderHeight),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI Emoji", 11F),
                ForeColor = ThemeManager.CurrentText,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };
            btnWinMaximize.FlatAppearance.BorderSize           = 0;
            btnWinMaximize.FlatAppearance.MouseOverBackColor   = Color.FromArgb(30, 128, 128, 128);
            btnWinMaximize.Click += (s, e) =>
            {
                this.WindowState = (this.WindowState == FormWindowState.Maximized)
                    ? FormWindowState.Normal : FormWindowState.Maximized;
                UpdateMaximizeIcon();
            };
            headerPanel.Controls.Add(btnWinMaximize);

            // Minimize
            btnWinMinimize = new Button
            {
                Text      = "🗕",
                Size      = new Size(46, HeaderHeight),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI Emoji", 11F),
                ForeColor = ThemeManager.CurrentText,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };
            btnWinMinimize.FlatAppearance.BorderSize           = 0;
            btnWinMinimize.FlatAppearance.MouseOverBackColor   = Color.FromArgb(30, 128, 128, 128);
            btnWinMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            headerPanel.Controls.Add(btnWinMinimize);
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  HEADER LAYOUT — Flex-gap right-to-left positioning
        // ══════════════════════════════════════════════════════════════════════════
        private void RepositionHeaderControls()
        {
            if (headerPanel == null) return;
            int W = headerPanel.Width;
            int vCenter(int h) => (HeaderHeight - h) / 2;

            // Zone 1 – Chrome (right edge, full height for OS feel)
            btnWinClose?.SetBounds(W - 46, 0, 46, HeaderHeight);
            btnWinMaximize?.SetBounds(W - 92, 0, 46, HeaderHeight);
            btnWinMinimize?.SetBounds(W - 138, 0, 46, HeaderHeight);

            // Zone 2 – User avatar (gap=16 from chrome)
            int avatarX = W - 138 - 16 - 36;
            userAvatarPanel?.SetBounds(avatarX, vCenter(36), 36, 36);

            // Zone 3 – Notification bell (gap=20 from avatar)
            int bellX = avatarX - 20 - 40;
            btnNotifications?.SetBounds(bellX, vCenter(40), 40, 40);

            // Zone 4 – Theme toggle (gap=10 from bell)
            int themeX = bellX - 10 - 70;
            btnThemeToggle?.SetBounds(themeX, vCenter(36), 70, 36);

            // Zone 5 – Clock (gap=20 from theme toggle)
            if (lblClock != null)
                lblClock.SetBounds(themeX - 20 - 195, vCenter(18), 195, 18);
        }

        private void UpdateMaximizeIcon()
        {
            if (btnWinMaximize == null || btnWinMaximize.IsDisposed) return;
            btnWinMaximize.Text = (this.WindowState == FormWindowState.Maximized) ? "🗗" : "🗖";
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  CONTENT
        // ══════════════════════════════════════════════════════════════════════════
        private void BuildContent()
        {
            contentPanel = new Panel();
            SetDoubleBuffer(contentPanel);
            contentPanel.AutoScroll = false;
            contentPanel.Dock       = DockStyle.Fill;
            contentPanel.Padding    = new Padding(0);

            contentPanel.MouseMove += (s, e) =>
            {
                _mouseGlowPos     = e.Location;
            };

            this.Controls.Add(contentPanel);
            this.Controls.SetChildIndex(contentPanel, 0);
            this.Controls.SetChildIndex(sidebarPanel, 1);
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  FLOATING ACTION BUTTON  (bottom-right of Form)
        // ══════════════════════════════════════════════════════════════════════════
        private void BuildFAB()
        {
            _fabPanel = new Panel
            {
                Size      = new Size(FabSize, FabSize),
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };
            _fabPanel.Location = new Point(
                this.ClientSize.Width  - FabSize - FabMargin,
                this.ClientSize.Height - FabSize - FabMargin);

            SetDoubleBuffer(_fabPanel);

            _fabPanel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // ── Outer neon glow ring (pulses) ──
                int glow = (int)(22 + 16 * _fabGlowAlpha);
                using (var glowPath = new GraphicsPath())
                {
                    glowPath.AddEllipse(-6, -6, FabSize + 12, FabSize + 12);
                    using var gb = new PathGradientBrush(glowPath);
                    gb.CenterColor    = Color.FromArgb(glow, 255, 90, 31);
                    gb.SurroundColors = new[] { Color.Transparent };
                    g.FillPath(gb, glowPath);
                }

                // ── Drop shadow ──
                var shadowR = new Rectangle(4, 6, FabSize - 2, FabSize - 2);
                using (var sp = new GraphicsPath()) {
                    sp.AddEllipse(shadowR);
                    using var sb2 = new PathGradientBrush(sp);
                    sb2.CenterColor    = Color.FromArgb(80, 0, 0, 0);
                    sb2.SurroundColors = new[] { Color.Transparent };
                    g.FillPath(sb2, sp);
                }

                // ── Orange gradient circle ──
                var r = new Rectangle(0, 0, FabSize - 1, FabSize - 1);
                using var grad = new LinearGradientBrush(
                    r, Color.FromArgb(255, 100, 40), Color.FromArgb(200, 55, 0),
                    LinearGradientMode.ForwardDiagonal);
                g.FillEllipse(grad, r);

                // ── Inner shine ──
                var shineR = new Rectangle(4, 4, FabSize - 8, (FabSize - 8) / 2);
                using var shinePath = new GraphicsPath();
                shinePath.AddEllipse(shineR);
                using var shineBrush = new LinearGradientBrush(
                    shineR, Color.FromArgb(55, 255, 255, 255), Color.Transparent,
                    LinearGradientMode.Vertical);
                g.FillPath(shineBrush, shinePath);

                // ── Chat icon ──
                string icon = _chatVisible ? "✕" : "💬";
                using var iconFont = new Font("Segoe UI Emoji", _chatVisible ? 16F : 18F, FontStyle.Bold);
                using var fmt      = new StringFormat
                    { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(icon, iconFont, Brushes.White,
                    new RectangleF(0, 0, FabSize, FabSize), fmt);
            };

            // ── Hover glow ──
            _fabPanel.MouseEnter += (s, e) => { _fabGlowTarget = 1f; _fabPulseTimer?.Start(); };
            _fabPanel.MouseLeave += (s, e) => { _fabGlowTarget = 0f; _fabPulseTimer?.Start(); };
            _fabPanel.Click      += (s, e) => ToggleChatFloat();

            // Circular hit region
            using (var circlePath = new GraphicsPath())
            {
                circlePath.AddEllipse(0, 0, FabSize, FabSize);
                _fabPanel.Region = new Region(circlePath);
            }

            this.Controls.Add(_fabPanel);
            _fabPanel.BringToFront();
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  FLOATING CHAT PANEL  (560 × 700, slides up from bottom-right)
        // ══════════════════════════════════════════════════════════════════════════
        private void ToggleChatFloat()
        {
            if (!_chatVisible)
            {
                // ── Show ──
                _chatVisible = true;
                _fabPanel?.Invalidate();

                if (_chatOverlay == null || _chatOverlay.IsDisposed)
                {
                    _chatOverlay = new ChatOverlayPanel();
                }

                _chatOverlay.OnToggleFullscreenRequested = (isFullscreen) =>
                {
                    if (_chatFloatHost == null || _chatFloatHost.IsDisposed) return;
                    if (isFullscreen)
                    {
                        int padding = 20;
                        _chatFloatHost.SetBounds(padding, padding, Math.Max(600, this.ClientSize.Width - (padding * 2)), Math.Max(500, this.ClientSize.Height - (padding * 2)));
                        _chatFloatHost.BringToFront();
                    }
                    else
                    {
                        int floatW = 540;
                        int floatH = 680;
                        int initialX = Math.Max(20, this.ClientSize.Width - floatW - FabMargin);
                        int targetY = Math.Max(20, this.ClientSize.Height - floatH - FabMargin - FabSize - 12);
                        _chatFloatHost.SetBounds(initialX, targetY, floatW, floatH);
                        _chatFloatHost.BringToFront();
                    }
                };

                _chatOverlay.OnSetLayoutModeRequested = (mode) =>
                {
                    if (_chatFloatHost == null || _chatFloatHost.IsDisposed) return;
                    int padding = 20;
                    if (mode == "fullscreen")
                    {
                        _chatFloatHost.SetBounds(padding, padding, Math.Max(600, this.ClientSize.Width - (padding * 2)), Math.Max(500, this.ClientSize.Height - (padding * 2)));
                    }
                    else if (mode == "split")
                    {
                        int splitWidth = Math.Max(480, (this.ClientSize.Width / 2) - padding);
                        int initialX = this.ClientSize.Width - splitWidth - padding;
                        int targetY = padding;
                        int targetHeight = Math.Max(500, this.ClientSize.Height - (padding * 2));
                        _chatFloatHost.SetBounds(initialX, targetY, splitWidth, targetHeight);
                    }
                    else // "floating"
                    {
                        int floatW = 540;
                        int floatH = 680;
                        int initialX = Math.Max(padding, this.ClientSize.Width - floatW - FabMargin);
                        int targetY = Math.Max(padding, this.ClientSize.Height - floatH - FabMargin - FabSize - 12);
                        _chatFloatHost.SetBounds(initialX, targetY, floatW, floatH);
                    }
                    _chatFloatHost.BringToFront();
                };

                _chatOverlay.OnNavigateToAccountRequested = (customerId) =>
                {
                    this.BeginInvoke((System.Windows.Forms.MethodInvoker)(() =>
                    {
                        if (btnAccounts != null && !btnAccounts.IsDisposed)
                        {
                            SetActiveButton(btnAccounts);
                            NavigateTo<AccountsPanel>();
                        }
                    }));
                };

                if (_chatFloatHost == null || _chatFloatHost.IsDisposed)
                {
                    _chatFloatHost = new Panel
                    {
                        Size      = new Size(560, 700),
                        BackColor = Color.Transparent
                    };
                    SetDoubleBuffer(_chatFloatHost);

                    // Paint a subtle rounded shadow border around the host
                    _chatFloatHost.Paint += (s, e) =>
                    {
                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        using var pen = new Pen(Color.FromArgb(30, 255, 255, 255), 1f);
                        using var path = GetRoundedRect(new Rectangle(0, 0, _chatFloatHost.Width - 1, _chatFloatHost.Height - 1), 16);
                        e.Graphics.DrawPath(pen, path);
                    };

                    _chatOverlay.Dock = DockStyle.Fill;
                    _chatFloatHost.Controls.Add(_chatOverlay);
                }

                _chatFloatHost.Visible = true;
                _chatOverlay.Visible   = true;

                if (!this.Controls.Contains(_chatFloatHost))
                {
                    int initialX = this.ClientSize.Width - 560 - FabMargin;
                    int initialY = this.ClientSize.Height; // start below viewport
                    _chatFloatHost.Location = new Point(initialX, initialY);
                    this.Controls.Add(_chatFloatHost);
                }

                _chatFloatHost.BringToFront();
                _fabPanel?.BringToFront();

                _chatFloatHost?.Focus();
                _chatOverlay?.Focus();

                // ── Slide-up animation (ease-out-expo lerp) ──
                int targetY = this.ClientSize.Height - 700 - FabMargin - FabSize - 12;
                var t = new System.Windows.Forms.Timer { Interval = 12 };
                t.Tick += (s, e) =>
                {
                    if (_chatFloatHost == null || _chatFloatHost.IsDisposed) { t.Stop(); t.Dispose(); return; }
                    float diff = targetY - _chatFloatHost.Top;
                    if (Math.Abs(diff) < 1f)
                    {
                        _chatFloatHost.Top = targetY;
                        t.Stop(); t.Dispose();
                        _chatFloatHost?.Focus();
                        _chatOverlay?.Focus();
                    }
                    else
                    {
                        int step = (int)(diff * 0.28f);
                        _chatFloatHost.Top += step != 0 ? step : (diff > 0 ? 1 : -1);
                    }
                };
                t.Start();
            }
            else
            {
                // ── Hide (ease-out-expo lerp) ──
                _chatVisible = false;
                _fabPanel?.Invalidate();

                if (_chatFloatHost == null || _chatFloatHost.IsDisposed) return;
                var host    = _chatFloatHost;
                int targetY = this.ClientSize.Height + 20;
                var t = new System.Windows.Forms.Timer { Interval = 12 };
                t.Tick += (s, e) =>
                {
                    if (host == null || host.IsDisposed) { t.Stop(); t.Dispose(); return; }
                    float diff = targetY - host.Top;
                    if (Math.Abs(diff) < 2f || host.Top >= targetY)
                    {
                        // Keep host & WebView2 ALIVE in background so AI processing continues!
                        host.Visible = false;
                        t.Stop(); t.Dispose();
                    }
                    else
                    {
                        int step = (int)(diff * 0.28f);
                        host.Top += step != 0 ? step : 2;
                    }
                };
                t.Start();
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  LOAD PANEL  [REMOVED — legacy method, no callers. Use NavigateTo<T>().]
        // ══════════════════════════════════════════════════════════════════════════
        [Obsolete("No-op stub. Use NavigateTo<T>() for all navigation.", error: true)]
        public void LoadPanel(UserControl _) { /* Intentionally empty — use NavigateTo<T>() */ }

        // ══════════════════════════════════════════════════════════════════════════
        //  NAVIGATE TO — Panel Cache engine (zero re-instantiation navigation)
        //  Panels are created once, hidden/shown on navigation — no veil, no timer.
        //  Old ghost panels are completely eliminated: outgoing is hidden and sent
        //  to back before the incoming panel is shown.
        // ══════════════════════════════════════════════════════════════════════════
        // ── Navigation History ────────────────────────────────────────────────────
        private readonly Stack<Type> _backStack = new();
        private readonly Stack<Type> _forwardStack = new();
        private bool _isNavigatingFromHistory = false;

        private void NavigateTo<T>() where T : UserControl, new() => NavigateToType(typeof(T));

        private void NavigateToType(Type panelType)
        {
            if (_activePanel != null && _activePanel.GetType() == panelType) return;

            if (_chatVisible) ToggleChatFloat();

            // ── History Tracking ────────────────────────────────────────────────
            if (!_isNavigatingFromHistory && _activePanel != null)
            {
                _backStack.Push(_activePanel.GetType());
                _forwardStack.Clear();
            }

            // ── Hide outgoing (keep alive in cache) ──────────────────────────────
            var outgoing = _activePanel;
            outgoing?.Hide();
            outgoing?.SendToBack();  // prevent any ghost paint bleeding through

            // ── Get or create panel from cache ───────────────────────────────────
            if (!_panelCache.TryGetValue(panelType, out UserControl incoming) || incoming.IsDisposed)
            {
                incoming = (UserControl)Activator.CreateInstance(panelType);
                incoming.BackColor = ThemeManager.CurrentBackground;
                SetDoubleBuffer(incoming);
                _panelCache[panelType] = incoming;
            }

            _activePanel = incoming;

            // ── RENDERING FIX: Set Size and Dock = Fill BEFORE adding & BringToFront ──
            incoming.Size = contentPanel.ClientSize;
            incoming.Dock = DockStyle.Fill;

            if (!contentPanel.Controls.Contains(incoming))
            {
                contentPanel.Controls.Add(incoming);
            }

            incoming.Show();
            incoming.BringToFront();

            // ── Lightweight data refresh without rebuilding DOM ──────────────────
            if (incoming is DashboardPanel dp)
            {
                dp.LoadStatsFromDB();
                dp.RefreshWebViewData();
                dp.PushThemeToWebView(ThemeManager.IsDarkMode ? "dark" : "light");
            }
            else if (incoming is FleetPanel fp) fp.RefreshWebViewData();

            SyncNavButtonState(panelType);
        }

        private void NavigateBack()
        {
            if (_backStack.Count == 0) return;
            if (_activePanel != null) _forwardStack.Push(_activePanel.GetType());
            _isNavigatingFromHistory = true;
            Type prev = _backStack.Pop();
            NavigateToType(prev);
            _isNavigatingFromHistory = false;
        }

        private void NavigateForward()
        {
            if (_forwardStack.Count == 0) return;
            if (_activePanel != null) _backStack.Push(_activePanel.GetType());
            _isNavigatingFromHistory = true;
            Type next = _forwardStack.Pop();
            NavigateToType(next);
            _isNavigatingFromHistory = false;
        }

        private void SyncNavButtonState(Type panelType)
        {
            // Selectively update the active sidebar button based on the displayed panel type
            if (panelType == typeof(DashboardPanel)) { SetActiveButton(btnDashboard); }
            else if (panelType == typeof(FleetPanel)) { SetActiveButton(btnVehicles); }
            else if (panelType == typeof(RentalsPanel)) { SetActiveButton(btnRentals); }
            else if (panelType == typeof(DriversPanel)) { SetActiveButton(btnDrivers); }
            else if (panelType == typeof(TransactionsPanel)) { SetActiveButton(btnTransactions); }
            else if (panelType == typeof(ReportsPanel)) { SetActiveButton(btnReports); }
            else if (panelType == typeof(CalendarPanel)) { SetActiveButton(btnCalendar); }
            else if (panelType == typeof(DocumentVaultPanel)) { SetActiveButton(btnDocVault); }
            else if (panelType == typeof(ExpensesPanel)) { SetActiveButton(btnExpenses); }
            else if (panelType == typeof(SplitPaymentsPanel)) { SetActiveButton(btnSplitPay); }
            else if (panelType == typeof(AccountsPanel)) { SetActiveButton(btnAccounts); }
            else if (panelType == typeof(WeatherPanel)) { SetActiveButton(btnWeather); }
        }

        private void OnToggleSidebar(object sender, EventArgs e)
        {
            _sidebarCollapsed = !_sidebarCollapsed;
            int startWidth  = sidebarPanel.Width;
            int targetWidth = _sidebarCollapsed ? SidebarCollapsedWidth : SidebarFullWidth;

            // ── Cross-fade: show the incoming layer before animating ────────────────
            // This is the key: both layers are always at their correct final layout.
            // We never resize their children. We just fade between them.
            bool collapsing = _sidebarCollapsed;

            // Prepare layers
            if (collapsing)
            {
                // Going to icon mode: make icon layer visible underneath, then fade full layer out
                _sidebarIconLayer.Visible = true;
                _sidebarFullLayer.BringToFront();
                _fullLayerAlpha = 1f;
                _iconLayerAlpha = 1f;
            }
            else
            {
                // Going to full mode: full layer is underneath, fade icon layer out
                _sidebarFullLayer.Visible = true;
                _sidebarIconLayer.BringToFront();
                _iconLayerAlpha = 1f;
                _fullLayerAlpha = 1f;
            }

            // ── Width animation + cross-fade driven by a single Stopwatch timer ─────
            _sidebarTimer?.Stop();
            _sidebarTimer?.Dispose();
            _sidebarAnimationClock.Restart();

            _sidebarTimer = new System.Windows.Forms.Timer { Interval = 1 };
            _sidebarTimer.Tick += (s, e2) =>
            {
                if (sidebarPanel == null || sidebarPanel.IsDisposed)
                { _sidebarTimer.Stop(); return; }

                double elapsed = _sidebarAnimationClock.Elapsed.TotalMilliseconds;
                double t       = Math.Min(1.0, elapsed / SidebarAnimationDurationMs);
                double eased   = 1.0 - Math.Pow(1.0 - t, 3);  // ease-out cubic

                // ── Width slide ──────────────────────────────────────────────────────
                int currentWidth = (int)Math.Round(startWidth + (targetWidth - startWidth) * eased);
                if (sidebarPanel.Width != currentWidth)
                    sidebarPanel.Width = currentWidth;

                // ── Hamburger morph ──────────────────────────────────────────────────
                _sidebarToggleProgress = collapsing ? (float)eased : (float)(1.0 - eased);
                btnToggleSidebar?.Invalidate();

                // ── Cross-fade alpha (outgoing fades in first half, incoming fades in second half) ──
                float fade = (float)eased;

                if (collapsing)
                {
                    // Full layer fades out (alpha 1 → 0)
                    _fullLayerAlpha = Math.Max(0f, 1f - fade * 2f);  // done by t=0.5
                    SetLayerOpacity(_sidebarFullLayer, _fullLayerAlpha);

                    // Icon layer fades in (alpha 0 → 1) starting from middle
                    _iconLayerAlpha = Math.Min(1f, (fade - 0.3f) / 0.7f);
                    SetLayerOpacity(_sidebarIconLayer, Math.Max(0f, _iconLayerAlpha));
                }
                else
                {
                    // Icon layer fades out
                    _iconLayerAlpha = Math.Max(0f, 1f - fade * 2f);
                    SetLayerOpacity(_sidebarIconLayer, _iconLayerAlpha);

                    // Full layer fades in
                    _fullLayerAlpha = Math.Min(1f, (fade - 0.3f) / 0.7f);
                    SetLayerOpacity(_sidebarFullLayer, Math.Max(0f, _fullLayerAlpha));
                }

                if (t >= 1.0)
                {
                    _sidebarTimer.Stop();
                    _sidebarAnimationClock.Stop();

                    sidebarPanel.Width = targetWidth;

                    // Snap layers to final state
                    if (collapsing)
                    {
                        _sidebarFullLayer.Visible = false;
                        SetLayerOpacity(_sidebarFullLayer, 1f);  // reset for next time
                        _sidebarIconLayer.Visible = true;
                        SetLayerOpacity(_sidebarIconLayer, 1f);
                        _sidebarIconLayer.BringToFront();
                    }
                    else
                    {
                        _sidebarIconLayer.Visible = false;
                        SetLayerOpacity(_sidebarIconLayer, 1f);
                        _sidebarFullLayer.Visible = true;
                        SetLayerOpacity(_sidebarFullLayer, 1f);
                        _sidebarFullLayer.BringToFront();
                    }

                    _sidebarToggleProgress = collapsing ? 1f : 0f;
                    btnToggleSidebar?.Invalidate();

                    // Keep active state correct on the newly-visible layer
                    if (activeButton != null) SetActiveButton(activeButton);
                    // Sync profile card layout to the new collapsed/expanded state
                    SetSidebarUIState(collapsing);
                }
            };
            _sidebarTimer.Start();
        }

        // ── Apply a fake opacity to a panel by color-tinting its BackColor alpha ────
        // WinForms panels don't natively support opacity, so we toggle Visible at the
        // boundary values and use intermediate visibility illusion via the alpha state.
        private void SetLayerOpacity(Panel layer, float alpha)
        {
            if (layer == null || layer.IsDisposed) return;
            // For WinForms we can't set true opacity on a panel without a Form host.
            // Instead we drive visibility with threshold gating — the cross-fade effect
            // is primarily delivered by the width animation + instant layer swap.
            // We use Visible toggling at the midpoint to create a crisp crosscut.
            if (alpha <= 0.01f)
                layer.Visible = false;
            else
                layer.Visible = true;
        }

        private void SyncIconActiveButton(Button iconBtn)
        {
            // Highlight the matching icon button in the icon layer
            if (_iconActiveButton != null)
            {
                _iconActiveButton.BackColor = Color.Transparent;
                _iconActiveButton.ForeColor = ThemeManager.CurrentText;
            }
            iconBtn.BackColor = ThemeManager.IsDarkMode
                ? Color.FromArgb(40, 255, 255, 255)
                : Color.FromArgb(20, 0, 0, 0);
            iconBtn.ForeColor = ThemeManager.CurrentPrimary;
            _iconActiveButton = iconBtn;
        }

        private void SetSidebarUIState(bool isCollapsed)
        {
            if (_userProfileCard == null || _userProfileCard.IsDisposed) return;

            if (isCollapsed)
            {
                // ── Collapsed state: shrink card, hide labels, centre avatar, emoji-only logout
                int collapsedW = SidebarCollapsedWidth - 10;
                _userProfileCard.Width = collapsedW;

                lblUserName?.Hide();
                lblUserRole?.Hide();

                if (btnLogout != null)
                {
                    btnLogout.Text      = "\uE7E8";
                    btnLogout.Font      = new Font("Segoe MDL2 Assets", 11F);
                    btnLogout.TextAlign = ContentAlignment.MiddleCenter;
                    btnLogout.Padding   = new Padding(0);
                    btnLogout.Width     = collapsedW - 16;
                    btnLogout.Left      = 8;
                }

                // Recentre avatar horizontally in the shrunken card at Y=10
                if (_sidebarAvatarPanel != null)
                    _sidebarAvatarPanel.Location = new Point((collapsedW - 38) / 2, 10);
            }
            else
            {
                // ── Expanded state: restore full card layout
                int expandedW = SidebarFullWidth - 16;
                _userProfileCard.Width = expandedW;

                lblUserName?.Show();
                lblUserRole?.Show();

                if (btnLogout != null)
                {
                    btnLogout.Text      = "Log Out";
                    btnLogout.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
                    btnLogout.TextAlign = ContentAlignment.MiddleLeft;
                    btnLogout.Padding   = new Padding(10, 0, 0, 0);
                    btnLogout.Width     = expandedW - 16;
                    btnLogout.Left      = 8;
                }

                // Restore avatar to X=10, Y=10
                if (_sidebarAvatarPanel != null)
                    _sidebarAvatarPanel.Location = new Point(10, 10);
            }

            _userProfileCard.Invalidate();
        }

        private void RefreshNavButtonRegions()
        {
            var buttons = new[] { btnDashboard, btnVehicles, btnRentals, btnDrivers,
                btnTransactions, btnReports, btnCalendar, btnDocVault,
                btnExpenses, btnSplitPay, btnAccounts, btnLogout };
            foreach (var b in buttons) 
                if (b != null) SetRoundRegion(b, 8);
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  RESPONSIVE
        // ══════════════════════════════════════════════════════════════════════════
        private void OnFormResize(object sender, EventArgs e)
        {
            // Auto-collapse sidebar on narrow windows
            if (this.Width < 1050 && !_sidebarCollapsed)       OnToggleSidebar(null, null);
            else if (this.Width >= 1050 && _sidebarCollapsed)  OnToggleSidebar(null, null);

            // Reposition all right-side header controls using the flex-gap layout
            RepositionHeaderControls();

            // Reposition FAB
            if (_fabPanel != null)
                _fabPanel.Location = new Point(
                    this.ClientSize.Width  - FabSize - FabMargin,
                    this.ClientSize.Height - FabSize - FabMargin);

            // Reposition floating chat
            if (_chatFloatHost != null && !_chatFloatHost.IsDisposed && _chatVisible)
                _chatFloatHost.Location = new Point(
                    this.ClientSize.Width  - 560 - FabMargin,
                    this.ClientSize.Height - 700 - FabMargin - FabSize - 12);

            // Reposition notification & profile flyouts if open
            _notifFlyout?.Reanchor(btnNotifications);
            _profileFlyout?.Reanchor(userAvatarPanel);

            UpdateMaximizeIcon();
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  NAV ACTIVE STATE
        // ══════════════════════════════════════════════════════════════════════════
        private void SetActiveButton(Button btn)
        {
            var prevActive = activeButton;
            activeButton = btn;

            if (prevActive != null)
            {
                prevActive.Invalidate();
            }

            if (btn != null)
            {
                btn.Invalidate();
            }

            string raw = btn?.Text?.Trim() ?? "";
            if (lblHeaderTitle != null && !string.IsNullOrWhiteSpace(raw))
                lblHeaderTitle.Text = raw;

            _targetIndicatorY = btn.Top + (btn.Height / 2) - (activeIndicator?.Height / 2 ?? 17);
            if (_animTimer != null && !_animTimer.Enabled) _animTimer.Start();

            // Sync active highlight in the icon layer as well
            if (_iconNavBtns != null)
            {
                Button[] fullBtns = { btnDashboard, btnVehicles, btnRentals, btnDrivers,
                    btnTransactions, btnReports, btnCalendar, btnDocVault,
                    btnExpenses, btnSplitPay, btnAccounts };
                for (int i = 0; i < fullBtns.Length; i++)
                {
                    if (fullBtns[i] == btn && i < _iconNavBtns.Length)
                    {
                        SyncIconActiveButton(_iconNavBtns[i]);
                        break;
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  RIPPLE
        // ══════════════════════════════════════════════════════════════════════════
        private void AttachRipple(Button btn, Color rippleColor)
        {
            btn.MouseDown += (s, e) =>
            {
                if (_ripples.TryGetValue(btn, out var old))
                {
                    old.Timer?.Stop(); old.Timer?.Dispose();
                    _ripples.Remove(btn);
                }

                float maxR = (float)Math.Sqrt(btn.Width * btn.Width + btn.Height * btn.Height) * 0.7f;
                var rs = new RippleState
                {
                    X = e.X, Y = e.Y, Radius = 0,
                    MaxRadius = maxR, Alpha = 180,
                    Timer = new System.Windows.Forms.Timer { Interval = 13 }
                };
                _ripples[btn] = rs;
                rs.Timer.Tick += (ts, te) =>
                {
                    rs.Radius += maxR * 0.1f;
                    rs.Alpha   = (int)(180 * (1f - rs.Radius / rs.MaxRadius));
                    if (rs.Radius >= rs.MaxRadius || rs.Alpha <= 0)
                    {
                        rs.Timer.Stop(); rs.Timer.Dispose();
                        _ripples.Remove(btn);
                    }
                    btn.Invalidate();
                };
                rs.Timer.Start();
                btn.Invalidate();
            };

            btn.Paint += (s, e) =>
            {
                if (!_ripples.TryGetValue(btn, out var rs)) return;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int alpha = Clamp((int)rs.Alpha);
                using var br = new SolidBrush(Color.FromArgb(alpha, rippleColor));
                e.Graphics.FillEllipse(br,
                    rs.X - rs.Radius, rs.Y - rs.Radius,
                    rs.Radius * 2, rs.Radius * 2);
            };
        }

        private void AttachHoverGlow(Button btn)
        {
            float _glowAlpha = 0f;
            bool  _hovering  = false;
            var glowTimer    = new System.Windows.Forms.Timer { Interval = 12 };

            glowTimer.Tick += (s, e) =>
            {
                float target = _hovering ? 1f : 0f;
                float diff   = target - _glowAlpha;
                if (Math.Abs(diff) < 0.02f) { _glowAlpha = target; glowTimer.Stop(); }
                else _glowAlpha += diff * 0.25f;
                btn.Invalidate();
            };

            btn.MouseEnter += (s, e) => { _hovering = true;  glowTimer.Start(); };
            btn.MouseLeave += (s, e) => { _hovering = false; glowTimer.Start(); };

            btn.Paint += (s, e) =>
            {
                if (_glowAlpha <= 0.01f) return;
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int alpha = (int)(30 * _glowAlpha);
                using var path = GetRoundedRect(new Rectangle(0, 0, btn.Width, btn.Height), 8);
                using var gb   = new PathGradientBrush(path);
                gb.CenterColor    = Color.FromArgb(alpha * 2, ThemeManager.CurrentPrimary);
                gb.SurroundColors = new[] { Color.Transparent };
                g.FillPath(gb, path);
            };
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  ANIMATIONS
        // ══════════════════════════════════════════════════════════════════════════
        private void StartAnimations()
        {
            var navBtns = new[] { btnDashboard, btnVehicles, btnRentals, btnDrivers,
                btnTransactions, btnReports, btnCalendar, btnDocVault,
                btnExpenses, btnSplitPay, btnAccounts };

            foreach (var b in navBtns)
            {
                AttachHoverGlow(b);
                AttachRipple(b, ThemeManager.CurrentPrimary);
            }
            AttachRipple(btnLogout,       Color.FromArgb(239, 68, 68));
            AttachRipple(btnNotifications, ThemeManager.CurrentPrimary);

            // ── FAB pulse timer ──
            _fabPulseTimer = new System.Windows.Forms.Timer { Interval = 14 };
            _fabPulseTimer.Tick += (s, e) =>
            {
                float diff = _fabGlowTarget - _fabGlowAlpha;
                if (Math.Abs(diff) < 0.02f)
                {
                    _fabGlowAlpha = _fabGlowTarget;
                    _fabPulseTimer.Stop();
                }
                else _fabGlowAlpha += diff * 0.18f;
                _fabPanel?.Invalidate();
            };

            // ── Main animation tick ──
            _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animTimer.Tick += (s, e) =>
            {
                bool animActive = false;

                // Form fade-in
                if (_opacity < 1f)
                {
                    _opacity  += 0.08f;
                    this.Opacity = Math.Min(_opacity, 1f);
                    animActive = true;
                }

                // Active indicator slide (smooth cubic-bezier equivalent)
                float diff = _targetIndicatorY - _currentIndicatorY;
                if (Math.Abs(diff) > 0.3f)
                {
                    _currentIndicatorY += diff * 0.25f;
                    activeIndicator.Top  = (int)_currentIndicatorY;
                    activeIndicator.Invalidate();
                    animActive = true;
                }
                else
                {
                    _currentIndicatorY = _targetIndicatorY;
                    activeIndicator.Top = (int)_currentIndicatorY;
                }

                if (!animActive)
                {
                    _animTimer.Stop();
                }
            };
            _animTimer.Start();
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  PAINT — Sidebar (glassmorphism + rim light upgrade)
        // ══════════════════════════════════════════════════════════════════════════
        private void OnSidebarPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // ── Full gradient fill ──
                using var bgGrad = new LinearGradientBrush(
                new Rectangle(0, 0, sidebarPanel.Width, sidebarPanel.Height),
                ThemeManager.SidebarGradientTop,
                ThemeManager.SidebarGradientBot,
                LinearGradientMode.Vertical);
            g.FillRectangle(bgGrad, sidebarPanel.ClientRectangle);

            // ── Right border ──
            g.DrawLine(new Pen(ThemeManager.CurrentBorder, 1),
                sidebarPanel.Width - 1, 0,
                sidebarPanel.Width - 1, sidebarPanel.Height);

            // ── Glassmorphic right-edge rim light (vertical gradient strip) ──
            var rimRect = new Rectangle(sidebarPanel.Width - 3, 0, 2, sidebarPanel.Height);
            using var rimBrush = new LinearGradientBrush(
                rimRect,
                Color.FromArgb(ThemeManager.IsDarkMode ? 14 : 40, 255, 255, 255),
                Color.Transparent,
                LinearGradientMode.Vertical);
            g.FillRectangle(rimBrush, rimRect);

            if (ThemeManager.IsDarkMode)
            {
                // ── Top ambient logo glow ──
                try
                {
                    using var gb = new PathGradientBrush(new Point[]
                    {
                        new(0, 0), new(sidebarPanel.Width, 0),
                        new(sidebarPanel.Width, 110), new(0, 110)
                    })
                    {
                        CenterColor    = Color.FromArgb(ThemeManager.SidebarGlowAlpha, 255, 90, 31),
                        SurroundColors = new[] { Color.Transparent }
                    };
                    g.FillEllipse(gb, -20, -30, sidebarPanel.Width + 40, 160);
                }
                catch { }

                // ── Top shimmer sheen ──
                var shimmerRect = new Rectangle(0, 0, sidebarPanel.Width, 100);
                using var shimmerBrush = new LinearGradientBrush(shimmerRect,
                    Color.FromArgb(10, 255, 255, 255), Color.Transparent,
                    LinearGradientMode.Vertical);
                g.FillRectangle(shimmerBrush, shimmerRect);

                // ── Bottom fade vignette ──
                int fH = 80;
                var fadeRect = new Rectangle(0, sidebarPanel.Height - fH, sidebarPanel.Width, fH);
                using var fadeBrush = new LinearGradientBrush(fadeRect,
                    Color.Transparent, Color.FromArgb(20, 0, 0, 0),
                    LinearGradientMode.Vertical);
                g.FillRectangle(fadeBrush, fadeRect);
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════════
        private Button CreateNavButton(string text, string iconGlyph, int y)
        {
            bool isHovered = false;
            var btn = new Button
            {
                Text      = text,
                Tag       = iconGlyph,
                Size      = new Size(SidebarFullWidth - 20, 46),
                Location  = new Point(10, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left
            };
            btn.FlatAppearance.BorderSize           = 0;
            btn.FlatAppearance.MouseOverBackColor   = Color.Transparent;
            btn.FlatAppearance.MouseDownBackColor   = Color.Transparent;
            SetRoundRegion(btn, 8);

            btn.MouseEnter += (s, e) => { isHovered = true;  btn.Invalidate(); };
            btn.MouseLeave += (s, e) => { isHovered = false; btn.Invalidate(); };

            btn.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                bool isActive = (activeButton == btn);

                // Erase native WinForms text by filling background
                Color sidebarBg = ThemeManager.CurrentSidebar;
                g.Clear(sidebarBg);

                // Paint rounded card background for active / hover state
                if (isActive)
                {
                    Color activeBg = ThemeManager.IsDarkMode
                        ? Color.FromArgb(28, 255, 255, 255)
                        : Color.FromArgb(18, 0, 0, 0);
                    using var b = new SolidBrush(activeBg);
                    using var path = GetRoundedRect(new Rectangle(0, 0, btn.Width, btn.Height), 8);
                    g.FillPath(b, path);
                }
                else if (isHovered)
                {
                    Color hoverBg = ThemeManager.IsDarkMode
                        ? Color.FromArgb(14, 255, 255, 255)
                        : Color.FromArgb(10, 0, 0, 0);
                    using var b = new SolidBrush(hoverBg);
                    using var path = GetRoundedRect(new Rectangle(0, 0, btn.Width, btn.Height), 8);
                    g.FillPath(b, path);
                }

                // Determine text & icon color
                Color fg = isActive
                    ? ThemeManager.CurrentPrimary
                    : (isHovered ? ThemeManager.CurrentText : Color.FromArgb(195, ThemeManager.CurrentText));

                using var iconFont = new Font("Segoe MDL2 Assets", 12F, FontStyle.Regular);
                using var textFont = new Font("Segoe UI", 10.5F, isActive ? FontStyle.Bold : FontStyle.Regular);
                using var brush    = new SolidBrush(fg);

                int h = btn.Height; // 46px

                // Measure strings for exact vertical centering
                SizeF iconSize = g.MeasureString(iconGlyph, iconFont);
                SizeF textSize = g.MeasureString(text, textFont);

                float iconX = 18f;
                float iconY = (h - iconSize.Height) / 2f + 0.5f;

                float gap   = 14f;
                float textX = iconX + 18f + gap; // 50px
                float textY = (h - textSize.Height) / 2f;

                // Draw Icon & Text cleanly aligned
                g.DrawString(iconGlyph, iconFont, brush, new PointF(iconX, iconY));
                g.DrawString(text, textFont, brush, new PointF(textX, textY));
            };

            return btn;
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  NOTIFICATION Flyout & Badge Rendering
        // ══════════════════════════════════════════════════════════════════════════
        private void OnNotifButtonPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int w = btnNotifications.Width;
            int h = btnNotifications.Height;

            // 1. Crisp anti-aliased circular container background & subtle border
            bool isDark = ThemeManager.IsDarkMode;
            var circleRect = new Rectangle(2, 2, w - 5, h - 5);

            using (var bgBrush = new SolidBrush(isDark ? Color.FromArgb(22, 255, 255, 255) : Color.FromArgb(12, 0, 0, 0)))
            {
                g.FillEllipse(bgBrush, circleRect);
            }
            using (var borderPen = new Pen(isDark ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(25, 0, 0, 0), 1f))
            {
                g.DrawEllipse(borderPen, circleRect);
            }

            // 2. Vector Bell Icon (Crisp, centered, perfectly proportional)
            DrawVectorBell(g, w / 2f, h / 2f + 0.5f, 17f, ThemeManager.CurrentText);

            // 3. Floating Notification Badge Pill / Circle (Zero clipping, crystal clear centering)
            if (_unreadNotifCount > 0)
            {
                string txt = _unreadNotifCount > 99 ? "99+" : _unreadNotifCount.ToString();
                using var font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
                var textSize = g.MeasureString(txt, font);

                int badgeH = 17;
                int badgeW = _unreadNotifCount <= 9 ? 17 : Math.Max(17, (int)textSize.Width + 5);
                int badgeX = w - badgeW - 1;
                int badgeY = 1;
                var badgeRect = new Rectangle(badgeX, badgeY, badgeW, badgeH);

                // Subtle red glow
                using (var glowBrush = new SolidBrush(Color.FromArgb(45, 239, 68, 68)))
                {
                    if (badgeW == badgeH)
                    {
                        g.FillEllipse(glowBrush, badgeX - 1, badgeY - 1, badgeW + 2, badgeH + 2);
                    }
                    else
                    {
                        using var glowPath = GetRoundedRect(new Rectangle(badgeX - 1, badgeY - 1, badgeW + 2, badgeH + 2), (badgeH + 2) / 2);
                        g.FillPath(glowBrush, glowPath);
                    }
                }

                // Clean cutout border ring (using headerPanel.BackColor to create crisp layered depth)
                using (var ringPen = new Pen(headerPanel.BackColor, 2f))
                {
                    if (badgeW == badgeH)
                    {
                        g.DrawEllipse(ringPen, badgeRect);
                    }
                    else
                    {
                        using var ringPath = GetRoundedRect(badgeRect, badgeH / 2);
                        g.DrawPath(ringPen, ringPath);
                    }
                }

                // Vibrant solid red badge fill
                using (var bgBrush = new SolidBrush(Color.FromArgb(239, 68, 68)))
                {
                    if (badgeW == badgeH)
                    {
                        g.FillEllipse(bgBrush, badgeRect);
                    }
                    else
                    {
                        using var badgePath = GetRoundedRect(badgeRect, badgeH / 2);
                        g.FillPath(bgBrush, badgePath);
                    }
                }

                // Crisp centered text
                using var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                var textRect = new RectangleF(badgeX, badgeY - 0.5f, badgeW, badgeH);
                g.DrawString(txt, font, Brushes.White, textRect, sf);
            }
        }

        private void DrawVectorBell(Graphics g, float cx, float cy, float size, Color color)
        {
            using var pen = new Pen(color, 1.8f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };

            float s = size / 20f;
            using var path = new GraphicsPath();

            // Top dome arc
            path.AddArc(cx - 4.5f * s, cy - 7f * s, 9f * s, 9f * s, 180, 180);
            // Right flare curve
            path.AddBezier(
                new PointF(cx + 4.5f * s, cy - 2.5f * s),
                new PointF(cx + 4.5f * s, cy + 2f * s),
                new PointF(cx + 7f * s, cy + 3.5f * s),
                new PointF(cx + 7f * s, cy + 5f * s)
            );
            // Bottom line across flare
            path.AddLine(cx + 7f * s, cy + 5f * s, cx - 7f * s, cy + 5f * s);
            // Left flare curve
            path.AddBezier(
                new PointF(cx - 7f * s, cy + 5f * s),
                new PointF(cx - 7f * s, cy + 3.5f * s),
                new PointF(cx - 4.5f * s, cy + 2f * s),
                new PointF(cx - 4.5f * s, cy - 2.5f * s)
            );
            path.CloseFigure();
            g.DrawPath(pen, path);

            // Bottom clapper arc
            using var clapperPen = new Pen(color, 1.8f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawArc(clapperPen, cx - 2f * s, cy + 5f * s, 4f * s, 3.5f * s, 0, 180);
        }

        public void PushNotification(string title, string body)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => PushNotification(title, body)));
                return;
            }

            _unreadNotifCount++;
            btnNotifications.Invalidate();
            _notifications.Insert(0, new { Title = title, Body = body, Time = DateTime.Now });

            if (_notifFlyout != null && !_notifFlyout.IsDisposed)
            {
                _notifFlyout.RebuildList();
            }
        }

        private Panel _globalNotifHostPanel;
        private Microsoft.Web.WebView2.WinForms.WebView2 _globalNotifWebView;
        private bool _isNotifFlyoutVisible = false;

        private async void ToggleNotifFlyout()
        {
            _unreadNotifCount = 0;
            btnNotifications?.Invalidate();

            if (_notifications.Count == 0)
            {
                _notifications.Add(new { title = "España Blvd Flood Hazard Alert", body = "1 Active Vehicle in España Blvd Flood Zone (Tire-Deep: 25-35 cm).", time = "Just now", unread = true });
                _notifications.Add(new { title = "PAGASA Monsoon Weather Advisory", body = "PAGASA Yellow Advisory Target: Rental Garage Hub (SJDM / Metro Manila).", time = "5m ago", unread = true });
                _notifications.Add(new { title = "Vehicle Decommission Audit", body = "Vehicle Toyota Vios (XWK-9492) removed from active fleet. Reason: Sold.", time = "1h ago", unread = false });
            }

            if (_isNotifFlyoutVisible && _globalNotifHostPanel != null)
            {
                _globalNotifHostPanel.Hide();
                _isNotifFlyoutVisible = false;
                return;
            }

            if (_globalNotifHostPanel == null || _globalNotifHostPanel.IsDisposed)
            {
                _globalNotifHostPanel = new Panel
                {
                    Size = new Size(360, 360),
                    BackColor = Color.Transparent,
                    Visible = false
                };

                Point screenPt = btnNotifications.PointToScreen(new Point(0, 0));
                Point parentPt = this.PointToClient(screenPt);
                _globalNotifHostPanel.Location = new Point(parentPt.X + btnNotifications.Width - 360, parentPt.Y + btnNotifications.Height + 6);

                _globalNotifWebView = new Microsoft.Web.WebView2.WinForms.WebView2 { 
                    Dock = DockStyle.Fill,
                    DefaultBackgroundColor = System.Drawing.Color.FromArgb(0, 0, 0, 0)
                };
                _globalNotifHostPanel.Controls.Add(_globalNotifWebView);
                this.Controls.Add(_globalNotifHostPanel);

                try
                {
                    var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DriveAndGo_Notif_Cache"));
                    await _globalNotifWebView.EnsureCoreWebView2Async(env);

                    _globalNotifWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                    _globalNotifWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                    _globalNotifWebView.CoreWebView2.WebMessageReceived += (s, args) =>
                    {
                        try
                        {
                            string msg = args.TryGetWebMessageAsString();
                            if (msg.StartsWith("notification_clicked:"))
                            {
                                string json = msg.Substring("notification_clicked:".Length);
                                using var doc = JsonDocument.Parse(json);
                                var root = doc.RootElement;
                                string title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                                string body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

                                _globalNotifHostPanel.Hide();
                                _isNotifFlyoutVisible = false;

                                HandleNotificationClick(title, body);
                            }
                            else if (msg == "notifications_cleared")
                            {
                                _notifications.Clear();
                                _unreadNotifCount = 0;
                                btnNotifications?.Invalidate();
                            }
                            else if (msg.StartsWith("resize_flyout:"))
                            {
                                if (int.TryParse(msg.Substring("resize_flyout:".Length), out int h))
                                {
                                    int clampedH = Math.Min(400, Math.Max(100, h + 10));
                                    this.BeginInvoke((Action)(() =>
                                    {
                                        if (_globalNotifHostPanel != null && !_globalNotifHostPanel.IsDisposed)
                                        {
                                            _globalNotifHostPanel.Height = clampedH;
                                        }
                                    }));
                                }
                            }
                        }
                        catch { }
                    };

                    string htmlPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "NotificationsFlyout.html");
                    _globalNotifWebView.CoreWebView2.Navigate("file:///" + htmlPath.Replace('\\', '/') + "?v=" + DateTime.UtcNow.Ticks);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[NotifFlyout] Init error: " + ex.Message);
                }
            }

            if (_globalNotifWebView != null && _globalNotifWebView.CoreWebView2 != null)
            {
                try
                {
                    var json = JsonSerializer.Serialize(_notifications);
                    string themeMode = ThemeManager.IsDarkMode ? "dark" : "light";
                    await _globalNotifWebView.CoreWebView2.ExecuteScriptAsync($"if(window.setNotifications) window.setNotifications({json}); if(window.setFlyoutTheme) window.setFlyoutTheme('{themeMode}');");
                }
                catch { }
            }

            _globalNotifHostPanel.BringToFront();
            _globalNotifHostPanel.Show();
            _isNotifFlyoutVisible = true;
        }

        private void ToggleProfileFlyout()
        {
            if (_profileFlyout == null || _profileFlyout.IsDisposed)
            {
                _profileFlyout = new UserProfileFlyoutPanel(this, userAvatarPanel);
                this.Controls.Add(_profileFlyout);
                _profileFlyout.BringToFront();
                _profileFlyout.StartEntrance();
            }
            else
            {
                _profileFlyout.StartDismissal();
            }
        }

        public void RefreshHeaderUserInfo()
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            if (this.InvokeRequired)
            {
                this.Invoke((Action)(() => RefreshHeaderUserInfo()));
                return;
            }

            if (lblUserName != null)
            {
                lblUserName.Text = !string.IsNullOrWhiteSpace(SessionManager.FullName) ? SessionManager.FullName : "Admin";
            }
            if (lblUserRole != null)
            {
                lblUserRole.Text = !string.IsNullOrWhiteSpace(SessionManager.Role) ? SessionManager.Role.ToUpper() : "ADMIN";
            }
            userAvatarPanel?.Invalidate();
            _sidebarAvatarPanel?.Invalidate();
        }

        public async void FetchUserProfileFromApiAsync()
        {
            try
            {
                int uid = SessionManager.UserId;
                string queryUrl = uid > 0 ? $"users/{uid}" : "admin/accounts?role=admin";

                var res = await ApiService.GetAsync(queryUrl);
                if (res.Success && !string.IsNullOrWhiteSpace(res.Body))
                {
                    using var doc = JsonDocument.Parse(res.Body);
                    JsonElement userElem;
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        if (doc.RootElement.GetArrayLength() > 0)
                            userElem = doc.RootElement[0];
                        else
                            return;
                    }
                    else
                    {
                        userElem = doc.RootElement;
                    }

                    if (userElem.TryGetProperty("userId", out var uidProp) && uidProp.GetInt32() > 0)
                    {
                        SessionManager.UserId = uidProp.GetInt32();
                    }
                    if (userElem.TryGetProperty("fullName", out var fn) && !string.IsNullOrWhiteSpace(fn.GetString()))
                    {
                        SessionManager.FullName = fn.GetString();
                    }
                    if (userElem.TryGetProperty("email", out var em) && !string.IsNullOrWhiteSpace(em.GetString()))
                    {
                        SessionManager.Email = em.GetString();
                    }
                    if (userElem.TryGetProperty("role", out var rl) && !string.IsNullOrWhiteSpace(rl.GetString()))
                    {
                        SessionManager.Role = rl.GetString();
                    }

                    string photoStr = null;
                    if (userElem.TryGetProperty("avatarBase64", out var av) && !string.IsNullOrWhiteSpace(av.GetString()))
                    {
                        photoStr = av.GetString();
                    }
                    else if (userElem.TryGetProperty("idPhotoUrl", out var ip) && !string.IsNullOrWhiteSpace(ip.GetString()))
                    {
                        photoStr = ip.GetString();
                    }
                    else if (userElem.TryGetProperty("photoUrl", out var pu) && !string.IsNullOrWhiteSpace(pu.GetString()))
                    {
                        photoStr = pu.GetString();
                    }

                    if (!string.IsNullOrWhiteSpace(photoStr))
                    {
                        await SessionManager.SetAvatarFromRawAsync(photoStr);
                    }
                }
            }
            catch { }

            RefreshHeaderUserInfo();
        }

        public void PerformLogout()
        {
            var r = ModernDialog.Show(
                this,
                "Are you sure you want to log out of the Drive&Go System?",
                "Confirm Logout",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (r == DialogResult.Yes)
            {
                SessionManager.Clear();
                new LoginForm().Show();
                this.Hide();
            }
        }

        public void HandleNotificationClick(string title, string body)
        {
            // Close notifications panel flyout
            ToggleNotifFlyout();

            string combined = $"{title} {body}".ToLower();

            if (combined.Contains("weather") || combined.Contains("pagasa") || combined.Contains("monsoon") || combined.Contains("flood") || combined.Contains("rain"))
            {
                // 1. Weather / Flood Advisory -> Navigate to Fleet Overview Panel and trigger Weather & PAGASA Radar Analytics Modal!
                btnVehicles?.PerformClick();
                var fp = contentPanel.Controls.OfType<FleetPanel>().FirstOrDefault();
                if (fp != null)
                {
                    fp.ExecuteScriptAsync("window.showWeatherAdvisoryFromNotif && window.showWeatherAdvisoryFromNotif();");
                }
            }
            else if (combined.Contains("decommission") || combined.Contains("audit") || combined.Contains("removed"))
            {
                // 2. Vehicle Decommission Audit -> Navigate to Fleet Overview Panel
                btnVehicles?.PerformClick();
            }
            else if (combined.Contains("rental") || combined.Contains("overdue") || combined.Contains("payment") || combined.Contains("refund") || combined.Contains("invoice"))
            {
                // 3. Rentals / Overdue -> Navigate to Rentals Panel
                btnRentals?.PerformClick();
            }
            else if (combined.Contains("account") || combined.Contains("user"))
            {
                // 4. Accounts -> Navigate to Accounts Panel
                btnAccounts?.PerformClick();
            }
            else
            {
                btnVehicles?.PerformClick();
            }
        }

        // ── Custom flyout panel ──
        public class NotificationFlyoutPanel : Panel
        {
            private readonly MainForm _parent;
            private readonly List<dynamic> _notifs;
            private readonly Action _onCleared;
            private float _alpha = 0f;
            private float _yOffset = -15f;
            private System.Windows.Forms.Timer _animTimer;

            public NotificationFlyoutPanel(MainForm parent, List<dynamic> notifs, Action onCleared)
            {
                _parent = parent;
                _notifs = notifs;
                _onCleared = onCleared;

                this.Size = new Size(300, 360);
                this.BackColor = Color.Transparent;
                
                SetDoubleBuffer(this);
                Reanchor(parent.btnNotifications);

                ThemeManager.ThemeChanged += (s, e) =>
                {
                    if (!this.IsDisposed && this.IsHandleCreated)
                    {
                        BuildList();
                        this.Invalidate();
                    }
                };

                BuildList();
            }

            public void Reanchor(Control anchor)
            {
                if (anchor == null || _parent == null || anchor.IsDisposed) return;
                Point screenPt = anchor.PointToScreen(new Point(0, 0));
                Point parentPt = _parent.PointToClient(screenPt);
                this.Location = new Point(parentPt.X + anchor.Width - this.Width, parentPt.Y + anchor.Height + 6 + (int)_yOffset);
            }

            public void RebuildList()
            {
                BuildList();
            }

            private void BuildList()
            {
                this.Controls.Clear();

                var btnClear = new Button
                {
                    Text = "Clear all",
                    Size = new Size(280, 32),
                    Location = new Point(10, this.Height - 42),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = ThemeManager.CurrentPrimary,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                };
                btnClear.FlatAppearance.BorderSize = 0;
                btnClear.FlatAppearance.MouseOverBackColor = Color.FromArgb(15, ThemeManager.CurrentPrimary);
                btnClear.Click += (s, e) =>
                {
                    _notifs.Clear();
                    _onCleared?.Invoke();
                    BuildList();
                    this.Invalidate();
                };
                this.Controls.Add(btnClear);

                // ── Custom smooth-scroll container (no native white scrollbar) ──
                var container = new FlowLayoutPanel
                {
                    Location      = new Point(10, 40),
                    Size          = new Size(280, this.Height - 90),
                    AutoScroll    = false,           // kills the native white scrollbar
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents  = false,
                    BackColor     = Color.Transparent
                };

                if (_notifs.Count == 0)
                {
                    var lblEmpty = new Label
                    {
                        Text      = "No notifications",
                        Font      = new Font("Segoe UI", 9.5F, FontStyle.Italic),
                        ForeColor = ThemeManager.CurrentSubText,
                        Size      = new Size(280, 100),
                        TextAlign = ContentAlignment.MiddleCenter,
                        BackColor = Color.Transparent
                    };
                    container.Controls.Add(lblEmpty);
                }
                else
                {
                    int count = Math.Min(10, _notifs.Count);
                    for (int i = 0; i < count; i++)
                    {
                        var n      = _notifs[i];
                        bool isUnread = i < _parent._unreadNotifCount;
                        var item   = new NotificationItemControl(n.Title, n.Body, n.Time, isUnread);
                        item.Click += (s, e) =>
                        {
                            _parent.HandleNotificationClick(n.Title, n.Body);
                        };
                        container.Controls.Add(item);
                    }
                }
                this.Controls.Add(container);

                // ── Manual MouseWheel smooth scroll (replaces native scrollbar) ──
                // ContentHeight = sum of children heights
                int itemHeight = 56;  // NotificationItemControl.Size.Height
                this.MouseWheel += (s, e) =>
                {
                    if (container.IsDisposed) return;
                    int totalH   = container.Controls.Count * itemHeight;
                    int viewH    = container.Height;
                    int maxScroll = Math.Max(0, totalH - viewH);
                    int delta     = -e.Delta / 6;   // wheel delta → pixel amount
                    int newY      = Math.Clamp(container.Location.Y - delta - 40, -maxScroll, 0);
                    container.Location = new Point(container.Location.X, 40 + newY);
                };
                // Forward wheel events that land on child controls up to the flyout panel
                foreach (Control child in container.Controls)
                {
                    child.MouseWheel += (s, e) =>
                    {
                        if (container.IsDisposed) return;
                        int totalH   = container.Controls.Count * itemHeight;
                        int viewH    = container.Height;
                        int maxScroll = Math.Max(0, totalH - viewH);
                        int delta     = -e.Delta / 6;
                        int newY      = Math.Clamp(container.Location.Y - delta - 40, -maxScroll, 0);
                        container.Location = new Point(container.Location.X, 40 + newY);
                    };
                }

                var lblTitle = new Label
                {
                    Text = "Notifications",
                    Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                    ForeColor = ThemeManager.CurrentText,
                    Location = new Point(16, 12),
                    AutoSize = true,
                    BackColor = Color.Transparent
                };
                this.Controls.Add(lblTitle);
            }

            public void StartEntrance()
            {
                _alpha   = 0.04f;
                _yOffset = -10f;   // mirror: translateY(-10px) from top-right origin
                _animTimer?.Stop();
                // 10ms tick → ~200ms to 99% completion at 0.30 lerp factor (ease-out curve)
                _animTimer = new System.Windows.Forms.Timer { Interval = 10 };
                _animTimer.Tick += (s, e) =>
                {
                    float alphaDiff = 1f - _alpha;
                    float yDiff     = 0f - _yOffset;

                    if (alphaDiff < 0.015f && Math.Abs(yDiff) < 0.3f)
                    {
                        _alpha   = 1f;
                        _yOffset = 0f;
                        _animTimer.Stop();
                    }
                    else
                    {
                        _alpha   += alphaDiff * 0.30f;   // fast ease-out (matches CSS 200ms ease-out)
                        _yOffset += yDiff     * 0.30f;
                    }
                    Reanchor(_parent.btnNotifications);
                    this.Invalidate();
                };
                _animTimer.Start();
            }

            public void StartDismissal()
            {
                _animTimer?.Stop();
                _animTimer = new System.Windows.Forms.Timer { Interval = 10 };
                _animTimer.Tick += (s, e) =>
                {
                    _alpha   -= _alpha   * 0.32f;   // snappy exit
                    _yOffset -= 1.4f;               // slide up as it fades
                    if (_alpha <= 0.04f)
                    {
                        _alpha = 0f;
                        _animTimer.Stop();
                        if (!_parent.IsDisposed && _parent.IsHandleCreated)
                        {
                            _parent.Controls.Remove(this);
                        }
                        this.Dispose();
                    }
                    else
                    {
                        Reanchor(_parent.btnNotifications);
                        this.Invalidate();
                    }
                };
                _animTimer.Start();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int w = this.Width, h = this.Height;
                var rect = new Rectangle(0, 0, w - 1, h - 1);
                using var path = _parent.GetRoundedRect(rect, 14);

                bool dark = ThemeManager.IsDarkMode;
                Color bgBase = dark ? Color.FromArgb(18, 18, 34) : Color.FromArgb(255, 255, 255);
                int bgAlpha = (int)(250 * _alpha);
                Color bgColor = Color.FromArgb(bgAlpha, bgBase);

                using (var brush = new SolidBrush(bgColor))
                {
                    g.FillPath(brush, path);
                }

                Color borderBase = dark ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(218, 220, 240);
                int borderAlpha = (int)(borderBase.A * _alpha);
                using (var pen = new Pen(Color.FromArgb(borderAlpha, borderBase), 1.2f))
                {
                    g.DrawPath(pen, path);
                }
            }

            protected override void Dispose(bool disposing)
            {
                _animTimer?.Dispose();
                base.Dispose(disposing);
            }
        }

        // ── Custom notification item row ──
        public class NotificationItemControl : Panel
        {
            private readonly string _title;
            private readonly string _body;
            private readonly DateTime _time;
            private bool _isHovered;
            private readonly bool _isUnread;
            private float _hoverScale = 0f;
            private System.Windows.Forms.Timer _hoverTimer;

            public NotificationItemControl(string title, string body, DateTime time, bool isUnread)
            {
                _title = CleanEmoji(title);
                _body = CleanEmoji(body);
                _time = time;
                _isUnread = isUnread;
                this.Size = new Size(280, 56);
                this.Cursor = Cursors.Hand;
                
                SetDoubleBuffer(this);
                
                this.MouseEnter += (s, e) => StartHover(true);
                this.MouseLeave += (s, e) => StartHover(false);
                this.MouseClick += (s, e) => this.OnClick(e);
            }

            private static string CleanEmoji(string input)
            {
                if (string.IsNullOrWhiteSpace(input)) return "";
                return input.Trim();
            }

            private void StartHover(bool hover)
            {
                _isHovered = hover;
                _hoverTimer?.Stop();
                _hoverTimer = new System.Windows.Forms.Timer { Interval = 14 };
                _hoverTimer.Tick += (s, e) =>
                {
                    float target = _isHovered ? 1f : 0f;
                    float diff = target - _hoverScale;
                    if (Math.Abs(diff) < 0.05f)
                    {
                        _hoverScale = target;
                        _hoverTimer.Stop();
                    }
                    else
                    {
                        _hoverScale += diff * 0.25f;
                    }
                    this.Invalidate();
                };
                _hoverTimer.Start();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int w = this.Width, h = this.Height;
                if (_hoverScale > 0)
                {
                    int alpha = (int)(18 * _hoverScale);
                    using var brush = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255));
                    g.FillRectangle(brush, 0, 0, w, h);
                }

                // Styled Category Dot Indicator
                Color categoryDotColor = Color.FromArgb(59, 130, 246); // default blue
                string lowTitle = _title.ToLower();
                string lowBody = _body.ToLower();

                if (lowTitle.Contains("weather") || lowTitle.Contains("pagasa") || lowTitle.Contains("monsoon") || lowBody.Contains("rain"))
                {
                    categoryDotColor = Color.FromArgb(245, 158, 11); // Amber
                }
                else if (lowTitle.Contains("flood") || lowTitle.Contains("hazard"))
                {
                    categoryDotColor = Color.FromArgb(6, 182, 212); // Cyan
                }
                else if (lowTitle.Contains("decommission") || lowTitle.Contains("audit") || lowTitle.Contains("emergency"))
                {
                    categoryDotColor = Color.FromArgb(239, 68, 68); // Red
                }

                using (var dotBrush = new SolidBrush(categoryDotColor))
                {
                    g.FillEllipse(dotBrush, 12, 15, 8, 8);
                }

                Color textCol = ThemeManager.CurrentText;
                Color subCol = ThemeManager.CurrentSubText;

                // Draw Ago Timestamp on right first to calculate exact remaining space for Title
                var diff = DateTime.Now - _time;
                string ago = diff.TotalMinutes < 1 ? "Just now" :
                             diff.TotalMinutes < 60 ? $"{(int)diff.TotalMinutes}m ago" :
                             diff.TotalHours < 24 ? $"{(int)diff.TotalHours}h ago" :
                             _time.ToString("MMM dd");
                             
                using var fontTime = new Font("Segoe UI", 7.5F);
                SizeF agoSize = g.MeasureString(ago, fontTime);
                float agoX = w - agoSize.Width - 10;
                g.DrawString(ago, fontTime, new SolidBrush(Color.FromArgb(140, subCol)), new PointF(agoX, 9));

                // Draw Title with ellipsis trimming (guarantees NO text overlap with timestamp)
                using var fontTitle = new Font("Segoe UI", 9F, FontStyle.Bold);
                using var sfTitle = new StringFormat
                {
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                float maxTitleWidth = Math.Max(50f, agoX - 28f - 6f);
                g.DrawString(_title, fontTitle, new SolidBrush(textCol), new RectangleF(28, 7, maxTitleWidth, 18), sfTitle);

                // Draw Body
                using var fontBody = new Font("Segoe UI", 8.5F);
                using var sfBody = new StringFormat
                {
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                g.DrawString(_body, fontBody, new SolidBrush(subCol), new RectangleF(28, 26, w - 38, 18), sfBody);

                using var pen = new Pen(Color.FromArgb(15, ThemeManager.CurrentBorder), 1);
                g.DrawLine(pen, 10, h - 1, w - 10, h - 1);
            }

            protected override void Dispose(bool disposing)
            {
                _hoverTimer?.Dispose();
                base.Dispose(disposing);
            }
        }

        private void SetRoundRegion(Control ctrl, int radius)
        {
            ctrl.Region = new Region(GetRoundedRect(
                new Rectangle(0, 0, ctrl.Width, ctrl.Height), radius));
        }

        private GraphicsPath GetRoundedRect(Rectangle b, int r)
        {
            int d   = r * 2;
            var arc = new Rectangle(b.Location, new Size(d, d));
            var path = new GraphicsPath();
            path.AddArc(arc, 180, 90); arc.X = b.Right - d;
            path.AddArc(arc, 270, 90); arc.Y = b.Bottom - d;
            path.AddArc(arc, 0,   90); arc.X = b.Left;
            path.AddArc(arc, 90,  90);
            path.CloseFigure();
            return path;
        }

        private static int Clamp(int v, int min = 0, int max = 255)
            => v < min ? min : v > max ? max : v;

        private static float Clamp01(float value)
            => value < 0f ? 0f : value > 1f ? 1f : value;

        private static float EaseOutCubic(float t)
        {
            t = 1f - Clamp01(t);
            return 1f - (t * t * t);
        }

        private static PointF LerpPoint(PointF from, PointF to, float t)
            => new PointF(
                from.X + ((to.X - from.X) * t),
                from.Y + ((to.Y - from.Y) * t));

        private void EnableSmoothTransitions(Control control)
        {
            if (control == null) return;

            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, control, new object[] { true });
        }

        private static void SetDoubleBuffer(Control c)
        {
            if (c == null) return;

            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, c, new object[] { true });
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════════════════════
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animTimer?.Dispose();
                _sidebarTimer?.Dispose();
                _themeTimer?.Dispose();
                _clockTimer?.Dispose();
                _fabPulseTimer?.Dispose();
                try { _chatOverlay?.Dispose(); }   catch { }
                try { _chatFloatHost?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
        // ══════════════════════════════════════════════════════════════════════════
        //  GLOBAL KEYBOARD NAVIGATION ENGINE
        // ══════════════════════════════════════════════════════════════════════════
        private readonly Type[] _sidebarNavTypes = new Type[]
        {
            typeof(DashboardPanel),
            typeof(FleetPanel),
            typeof(RentalsPanel),
            typeof(DriversPanel),
            typeof(TransactionsPanel),
            typeof(ReportsPanel),
            typeof(CalendarPanel),
            typeof(DocumentVaultPanel),
            typeof(ExpensesPanel),
            typeof(SplitPaymentsPanel),
            typeof(AccountsPanel)
        };

        private void NavigateNextSidebarTab()
        {
            if (_activePanel == null) { NavigateTo<DashboardPanel>(); return; }
            int idx = Array.IndexOf(_sidebarNavTypes, _activePanel.GetType());
            if (idx < 0) idx = 0;
            else idx = (idx + 1) % _sidebarNavTypes.Length;
            NavigateToType(_sidebarNavTypes[idx]);
        }

        private void NavigatePreviousSidebarTab()
        {
            if (_activePanel == null) { NavigateTo<DashboardPanel>(); return; }
            int idx = Array.IndexOf(_sidebarNavTypes, _activePanel.GetType());
            if (idx <= 0) idx = _sidebarNavTypes.Length - 1;
            else idx = idx - 1;
            NavigateToType(_sidebarNavTypes[idx]);
        }

        private void FocusActivePanelSearch()
        {
            if (_activePanel == null)
            {
                ToggleChatFloat();
                return;
            }

            Control searchControl = FindSearchControl(_activePanel);
            if (searchControl != null)
            {
                searchControl.Focus();
                if (searchControl is TextBox tb) tb.SelectAll();
            }
            else
            {
                ToggleChatFloat();
            }
        }

        private Control FindSearchControl(Control parent)
        {
            if (parent == null) return null;
            foreach (Control c in parent.Controls)
            {
                if (c is TextBox && (!string.IsNullOrEmpty(c.Name) && (c.Name.Contains("Search", StringComparison.OrdinalIgnoreCase) || c.Name.Contains("Filter", StringComparison.OrdinalIgnoreCase))))
                    return c;

                var child = FindSearchControl(c);
                if (child != null) return child;
            }
            return null;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Check if user is currently typing in an editable input box
            bool isEditingText = ActiveControl is TextBoxBase || ActiveControl is ComboBox;

            // 1. ESC key: Close flyouts -> Close AI chat -> Navigate Back
            if (keyData == Keys.Escape)
            {
                if (_notifFlyout != null && _notifFlyout.Visible)
                {
                    _notifFlyout.Hide();
                    return true;
                }
                if (_profileFlyout != null && _profileFlyout.Visible)
                {
                    _profileFlyout.Hide();
                    return true;
                }
                if (_chatVisible)
                {
                    ToggleChatFloat();
                    return true;
                }
                if (_backStack.Count > 0)
                {
                    NavigateBack();
                    return true;
                }
            }

            // 2. Navigation Back (Alt + Left or Backspace when not typing)
            if (keyData == (Keys.Alt | Keys.Left) || (!isEditingText && keyData == Keys.Back))
            {
                if (_backStack.Count > 0)
                {
                    NavigateBack();
                    return true;
                }
            }

            // 3. Navigation Forward (Alt + Right)
            if (keyData == (Keys.Alt | Keys.Right))
            {
                if (_forwardStack.Count > 0)
                {
                    NavigateForward();
                    return true;
                }
            }

            // 4. Drive&Go AI Copilot Chat Toggle (F1, Ctrl+Shift+C, Ctrl+Space)
            if (keyData == Keys.F1 || keyData == (Keys.Control | Keys.Shift | Keys.C) || keyData == (Keys.Control | Keys.Space))
            {
                ToggleChatFloat();
                return true;
            }

            // 5. Refresh Active Panel (F5 or Ctrl+R)
            if (keyData == Keys.F5 || keyData == (Keys.Control | Keys.R))
            {
                if (_activePanel != null)
                {
                    Type currentType = _activePanel.GetType();
                    _activePanel = null; // force re-sync
                    NavigateToType(currentType);
                    return true;
                }
            }

            // 6. Global Search Focus (Ctrl+F, F3)
            if (keyData == (Keys.Control | Keys.F) || keyData == Keys.F3)
            {
                FocusActivePanelSearch();
                return true;
            }

            // 7. Toggle Sidebar Collapse (Ctrl+B)
            if (keyData == (Keys.Control | Keys.B))
            {
                OnToggleSidebar(this, EventArgs.Empty);
                return true;
            }

            // 8. Direct Jump to Tabs (Ctrl + 1..9 or Alt + 1..9)
            Keys keyMask = keyData & Keys.KeyCode;
            bool isCtrl = (keyData & Keys.Control) == Keys.Control;
            bool isAlt = (keyData & Keys.Alt) == Keys.Alt;

            if (isCtrl || isAlt)
            {
                switch (keyMask)
                {
                    case Keys.D1: case Keys.NumPad1: NavigateTo<DashboardPanel>(); return true;
                    case Keys.D2: case Keys.NumPad2: NavigateTo<FleetPanel>(); return true;
                    case Keys.D3: case Keys.NumPad3: NavigateTo<RentalsPanel>(); return true;
                    case Keys.D4: case Keys.NumPad4: NavigateTo<DriversPanel>(); return true;
                    case Keys.D5: case Keys.NumPad5: NavigateTo<TransactionsPanel>(); return true;
                    case Keys.D6: case Keys.NumPad6: NavigateTo<ReportsPanel>(); return true;
                    case Keys.D7: case Keys.NumPad7: NavigateTo<CalendarPanel>(); return true;
                    case Keys.D8: case Keys.NumPad8: NavigateTo<DocumentVaultPanel>(); return true;
                    case Keys.D9: case Keys.NumPad9: NavigateTo<ExpensesPanel>(); return true;
                }
            }

            // 9. Cycle Sidebar Tabs (Ctrl + Up / Ctrl + Down or Alt + Up / Alt + Down)
            if ((isCtrl || isAlt) && keyMask == Keys.Up)
            {
                NavigatePreviousSidebarTab();
                return true;
            }
            if ((isCtrl || isAlt) && keyMask == Keys.Down)
            {
                NavigateNextSidebarTab();
                return true;
            }

            // 10. F11 Fullscreen / Maximize Toggle
            if (keyData == Keys.F11)
            {
                if (this.WindowState == FormWindowState.Maximized)
                    this.WindowState = FormWindowState.Normal;
                else
                    this.WindowState = FormWindowState.Maximized;
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private const int WM_XBUTTONUP = 0x020C;
        private const int XBUTTON1 = 0x0001; // Back button
        private const int XBUTTON2 = 0x0002; // Forward button

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_XBUTTONUP)
            {
                int button = (int)(m.WParam.ToInt64() >> 16) & 0xFFFF;
                if (button == XBUTTON1)
                {
                    NavigateBack();
                    m.Result = IntPtr.Zero;
                    return;
                }
                else if (button == XBUTTON2)
                {
                    NavigateForward();
                    m.Result = IntPtr.Zero;
                    return;
                }
            }
            base.WndProc(ref m);
        }
    }
}
