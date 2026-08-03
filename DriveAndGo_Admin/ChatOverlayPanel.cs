#nullable disable
using DriveAndGo_Admin.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DriveAndGo_Admin
{
    public enum MessageDeliveryState
    {
        Sending,   // Blue / hollow slate dashed circle
        Sent,      // Blue circle outline + thin checkmark
        Delivered, // Solid filled blue circle + white checkmark
        Seen       // 16x16 circular-cropped gradient initials avatar
    }

    /// <summary>
    /// Full-screen / Floating chat overlay panel.
    /// Fully synchronized with ThemeManager (Light/Dark Mode).
    /// Left split: conversation list (drivers, customers, group chats).
    /// Right split: active message thread with Messenger-style aesthetics & delivery states.
    /// </summary>
    public class ChatOverlayPanel : UserControl
    {
        // ── Layout ───────────────────────────────────────────────────────────────
        private Panel _leftPane;     // Conversation list
        private Panel _rightPane;    // Message thread
        private Panel _divider;
        private WebView2 _webView;
        private bool _isInitialized = false;

        // ── Left pane controls ───────────────────────────────────────────────────
        private Label   _lblChats;
        private TextBox _txtSearch;
        private Panel   _convListPanel;

        // ── Right pane controls ──────────────────────────────────────────────────
        private Panel   _headerBar;
        private Label   _lblConvName;
        private Label   _lblConvStatus;
        private Button  _btnToggleExpand;
        private Panel   _pnlWelcomeState;
        private FlowLayoutPanel _flowMessages;
        private TextBox _txtInput;
        private Button  _btnSend;

        // ── Data & State ─────────────────────────────────────────────────────────
        private string _activeConvId = null;
        private bool   _activeConvIsGroup = false;
        private bool   _isChatFullscreen = false;
        private Rectangle _normalBounds;

        private readonly List<ConvItem> _conversations = new();
        private readonly List<ConvItem> _filteredConversations = new();
        private HubConnection _hubConnection;

        private readonly Dictionary<string, (Panel row, MessageDeliveryState[] stateHolder)> _bubbleRegistry = new();
        private string _lastSentBubbleId;

        // ── System Tray Notification Icon ────────────────────────────────────────
        // Shown when a new message arrives in a non-active conversation.
        // Disposed explicitly to prevent ghost icons in the system tray.
        private readonly NotifyIcon _notifyIcon;

        // ── Hover-icon hitbox tracking ────────────────────────────────────────
        // Hitboxes are tracked as local variables captured in each row's closure.
        // This dictionary is reserved for future cross-row coordination.

        private struct ConvItem
        {
            public string Id, Name, LastMessage, Time, Role;
            public int    UnreadCount;
            public bool   IsGroup;
        }

        // ── Dynamic Theme Managers Colors ────────────────────────────────────────
        private static Color BgDark   => ThemeManager.CurrentBackground;
        private static Color PanelBg  => ThemeManager.CurrentSidebar;
        private static Color CardBg   => ThemeManager.CurrentCard;
        private static Color Border   => ThemeManager.CurrentBorder;
        private static Color TextMain => ThemeManager.CurrentText;
        private static Color TextSub  => ThemeManager.CurrentSubText;
        private static Color Orange   => ThemeManager.CurrentPrimary;
        private static Color InputBg  => ThemeManager.CurrentInputBg;

        // ── Win32 Native Scrollbar Suppressor ────────────────────────────────────
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);
        private const int SB_HORZ = 0;
        private const int SB_VERT = 1;

        private static void HideNativeScrollBars(Control ctrl)
        {
            if (ctrl != null && ctrl.IsHandleCreated)
            {
                try
                {
                    ShowScrollBar(ctrl.Handle, SB_VERT, false);
                    ShowScrollBar(ctrl.Handle, SB_HORZ, false);
                }
                catch { }
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════════════════
        public ChatOverlayPanel()
        {
            EnableDB(this);
            this.SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint  |
                ControlStyles.UserPaint, true);
            this.BackColor = ThemeManager.CurrentBackground;
            this.AutoScroll = false;
            this.MouseEnter += (s, e) => this.Focus();
            this.Paint    += OnBackgroundPaint;

            ThemeManager.ThemeChanged += (s, e) => ApplyTheme();

            // ── Initialize system tray notification icon ──────────────────────
            _notifyIcon = new NotifyIcon
            {
                Text    = "DriveAndGo Admin",
                Icon    = SystemIcons.Information,
                Visible = false   // Only made visible when a balloon tip is shown
            };

            InitializeWebView2();
            BuildLayout();
            InitializeSignalR();
            LoadConversationsFromApi();
        }


        // ── Background: radial glow ──────────────────────────────────────────────
        private void OnBackgroundPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(this.AutoScrollPosition.X, this.AutoScrollPosition.Y);
            g.FillRectangle(new SolidBrush(ThemeManager.CurrentBackground), this.ClientRectangle);

            int cx = this.Width / 2;
            try
            {
                using var gb = new PathGradientBrush(new Point[]
                {
                    new(cx - 400, -20), new(cx + 400, -20),
                    new(cx + 400, 140), new(cx - 400, 140)
                })
                {
                    CenterColor    = ThemeManager.RadialGlowColor,
                    SurroundColors = new[] { Color.Transparent }
                };
                g.FillEllipse(gb, cx - 400, -30, 800, 170);
            }
            catch { }
        }

        private async void InitializeWebView2()
        {
            try
            {
                _webView = new WebView2
                {
                    Dock = DockStyle.Fill,
                    Visible = false
                };

                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DriveAndGo", "WebView2ChatData");

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await _webView.EnsureCoreWebView2Async(env);

                string webAssetsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets");

                if (Directory.Exists(webAssetsFolder))
                {
                    _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        "appassets",
                        webAssetsFolder,
                        CoreWebView2HostResourceAccessKind.Allow);
                }

                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

                _webView.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    _isInitialized = true;
                };

                _webView.CoreWebView2.Navigate("https://appassets/ChatOverlay.html");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChatOverlayPanel] WebView2 init error: {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  THEME REFRESHER
        // ════════════════════════════════════════════════════════════════════════
        public void ApplyTheme()
        {
            this.BackColor           = ThemeManager.CurrentBackground;
            if (_leftPane != null)       _leftPane.BackColor      = ThemeManager.CurrentSidebar;
            if (_convListPanel != null)  _convListPanel.BackColor  = ThemeManager.CurrentSidebar;
            if (_rightPane != null)      _rightPane.BackColor     = ThemeManager.CurrentBackground;
            if (_flowMessages != null)   _flowMessages.BackColor  = ThemeManager.CurrentBackground;
            if (_headerBar != null)      _headerBar.BackColor     = ThemeManager.CurrentSidebar;
            if (_divider != null)        _divider.BackColor       = ThemeManager.CurrentBorder;

            if (_webView != null && _isInitialized)
            {
                _webView.CoreWebView2.ExecuteScriptAsync($"document.documentElement.setAttribute('data-theme', '{ (ThemeManager.IsDarkMode ? "dark" : "light") }');");
            }

            if (_txtSearch != null)
            {
                _txtSearch.BackColor = ThemeManager.CurrentInputBg;
                _txtSearch.ForeColor = ThemeManager.CurrentText;
            }

            if (_txtInput != null)
            {
                _txtInput.BackColor = ThemeManager.CurrentInputBg;
                _txtInput.ForeColor = ThemeManager.CurrentText;
            }

            if (_lblChats != null)       _lblChats.ForeColor       = ThemeManager.CurrentText;
            if (_lblConvName != null)    _lblConvName.ForeColor    = ThemeManager.CurrentText;
            if (_lblConvStatus != null)  _lblConvStatus.ForeColor  = ThemeManager.CurrentSubText;

            if (_btnToggleExpand != null)
            {
                _btnToggleExpand.ForeColor = ThemeManager.CurrentText;
                _btnToggleExpand.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(20, 255, 255, 255) : Color.FromArgb(20, 0, 0, 0);
            }

            RefreshConvList();
            if (_activeConvId == null)
            {
                ShowWelcomeScreen();
            }
            else
            {
                LoadMessagesFromApi(_activeConvId);
            }

            this.Invalidate(true);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  LAYOUT & UI BUILD
        // ════════════════════════════════════════════════════════════════════════
        private void BuildLayout()
        {
            _divider = new Panel { Width = 1, Dock = DockStyle.None, BackColor = ThemeManager.CurrentBorder };

            _leftPane = new Panel();
            EnableDB(_leftPane);
            _leftPane.BackColor = ThemeManager.CurrentSidebar;
            _leftPane.Dock      = DockStyle.None;
            _leftPane.Paint    += (s, e) => PaintLeftPane(e);

            _rightPane = new Panel();
            EnableDB(_rightPane);
            _rightPane.BackColor = ThemeManager.CurrentBackground;
            _rightPane.Dock      = DockStyle.None;

            this.Controls.Add(_leftPane);
            this.Controls.Add(_divider);
            this.Controls.Add(_rightPane);

            this.Resize += (s, e) => DoLayout();

            BuildLeftPane();
            BuildRightPane();
        }

        private void DoLayout()
        {
            int leftW = Math.Max(260, Math.Min(360, (int)(this.Width * 0.32)));
            _leftPane.SetBounds(0, 0, leftW, this.Height);
            _divider.SetBounds(leftW, 0, 1, this.Height);
            _rightPane.SetBounds(leftW + 1, 0, this.Width - leftW - 1, this.Height);
            UpdateScrollBounds();
        }

        private void BuildLeftPane()
        {
            var header = new Panel();
            EnableDB(header);
            header.Height = 64;
            header.Dock   = DockStyle.Top;
            header.BackColor = Color.Transparent;
            header.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillRectangle(new SolidBrush(ThemeManager.CurrentSidebar), header.ClientRectangle);
            };

            _lblChats = new Label
            {
                Text      = "Messages",
                Font      = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentText,
                AutoSize  = true,
                Location  = new Point(18, 18),
                BackColor = Color.Transparent
            };
            header.Controls.Add(_lblChats);

            var btnCompose = new Button
            {
                Text      = "+",
                Font      = new Font("Segoe UI", 16F, FontStyle.Bold),
                Size      = new Size(32, 32),
                Location  = new Point(header.Width - 44, 16),
                FlatStyle = FlatStyle.Flat,
                ForeColor = ThemeManager.CurrentPrimary,
                BackColor = Color.FromArgb(20, ThemeManager.CurrentPrimary.R, ThemeManager.CurrentPrimary.G, ThemeManager.CurrentPrimary.B),
                Cursor    = Cursors.Hand,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            btnCompose.FlatAppearance.BorderSize = 0;
            SetRoundRegion(btnCompose, 16);
            btnCompose.Click += (s, e) =>
            {
                _txtSearch?.Focus();
                _txtSearch?.SelectAll();
            };
            header.Controls.Add(btnCompose);
            _leftPane.Controls.Add(header);

            var searchWrap = new Panel();
            EnableDB(searchWrap);
            searchWrap.Height = 52;
            searchWrap.Dock   = DockStyle.Top;
            searchWrap.BackColor = ThemeManager.CurrentSidebar;
            searchWrap.Padding = new Padding(14, 8, 14, 8);
            searchWrap.Paint  += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillRectangle(new SolidBrush(ThemeManager.CurrentSidebar), searchWrap.ClientRectangle);
                var r = new Rectangle(10, 6, searchWrap.Width - 20, 36);
                using var path = RR(r, 18);
                g.FillPath(new SolidBrush(ThemeManager.CurrentInputBg), path);
                g.DrawPath(new Pen(ThemeManager.CurrentBorder, 1f), path);

                using var iconPen = new Pen(ThemeManager.CurrentSubText, 1.8f);
                iconPen.StartCap = LineCap.Round;
                iconPen.EndCap   = LineCap.Round;
                g.DrawEllipse(iconPen, 20, 16, 11, 11);
                g.DrawLine(iconPen, 29, 25, 34, 30);
            };

            _txtSearch = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor   = ThemeManager.CurrentInputBg,
                ForeColor   = ThemeManager.CurrentText,
                Font        = new Font("Segoe UI", 10F),
                PlaceholderText = "Search conversations...",
                Size        = new Size(searchWrap.Width - 60, 28),
                Location    = new Point(40, 14),
                Anchor      = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            _txtSearch.TextChanged += (s, e) => RefreshConvList();
            searchWrap.Controls.Add(_txtSearch);

            _convListPanel = new DarkScrollPanel();
            EnableDB(_convListPanel);
            _convListPanel.Dock       = DockStyle.Fill;
            _convListPanel.BackColor  = ThemeManager.CurrentSidebar;
            _convListPanel.AutoScroll = true;
            _convListPanel.Resize    += (s, e) =>
            {
                int cardW = Math.Max(100, _convListPanel.ClientSize.Width - 4);
                _convListPanel.SuspendLayout();
                foreach (Control ctrl in _convListPanel.Controls)
                {
                    if (ctrl is Panel p)
                    {
                        p.Width = cardW;
                        p.Location = new Point(2, p.Location.Y);
                    }
                }
                _convListPanel.ResumeLayout();
            };

            _convListPanel.ControlAdded   += (s, e) => HideNativeScrollBars(_convListPanel);
            _convListPanel.ControlRemoved += (s, e) => HideNativeScrollBars(_convListPanel);
            _convListPanel.Resize         += (s, e) => HideNativeScrollBars(_convListPanel);
            _convListPanel.Paint          += (s, e) => HideNativeScrollBars(_convListPanel);
            _convListPanel.Scroll         += (s, e) => HideNativeScrollBars(_convListPanel);
            _convListPanel.MouseWheel     += (s, e) => HideNativeScrollBars(_convListPanel);

            _leftPane.Controls.Add(_convListPanel);
            _leftPane.Controls.Add(searchWrap);
            _leftPane.Controls.Add(header);

            searchWrap.SendToBack();
            header.SendToBack();
            _convListPanel.BringToFront();
        }

        private void PaintLeftPane(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.FillRectangle(new SolidBrush(ThemeManager.CurrentSidebar), _leftPane.ClientRectangle);
        }

        private void BuildRightPane()
        {
            _headerBar = new Panel();
            EnableDB(_headerBar);
            _headerBar.Height = 65;
            _headerBar.Dock   = DockStyle.Top;
            _headerBar.BackColor = ThemeManager.CurrentSidebar;
            _headerBar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillRectangle(new SolidBrush(ThemeManager.CurrentSidebar), _headerBar.ClientRectangle);
            };

            var avatar = new Panel();
            EnableDB(avatar);
            avatar.Size      = new Size(42, 42);
            avatar.Location  = new Point(16, 12);
            avatar.BackColor = Color.Transparent;
            avatar.Paint    += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, 41, 41);

                Color grad1 = _activeConvIsGroup ? Color.FromArgb(34, 197, 94) : ThemeManager.CurrentPrimary;
                Color grad2 = _activeConvIsGroup ? Color.FromArgb(16, 185, 129) : ThemeManager.CurrentPrimaryGlow;

                using var grad = new LinearGradientBrush(r, grad1, grad2, LinearGradientMode.ForwardDiagonal);
                g.FillEllipse(grad, r);

                string init = _activeConvIsGroup ? "G"
                    : (!string.IsNullOrEmpty(_lblConvName?.Text) && _lblConvName.Text != "Select a conversation"
                        ? _lblConvName.Text[0].ToString().ToUpper() : "⚡");
                using var font = new Font("Segoe UI", 14F, FontStyle.Bold);
                using var fmt  = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(init, font, Brushes.White, new RectangleF(0, 0, 42, 42), fmt);
            };
            _headerBar.Controls.Add(avatar);

            _lblConvName = new Label
            {
                Text      = "DriveAndGo Hubs",
                Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentText,
                AutoSize  = true,
                Location  = new Point(68, 13),
                BackColor = Color.Transparent
            };
            _headerBar.Controls.Add(_lblConvName);

            _lblConvStatus = new Label
            {
                Text      = "Real-time Messaging Hub",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = ThemeManager.CurrentSubText,
                AutoSize  = true,
                Location  = new Point(68, 36),
                BackColor = Color.Transparent
            };
            _headerBar.Controls.Add(_lblConvStatus);

            // ── Fullscreen Toggle Button ──
            _btnToggleExpand = new Button
            {
                Text      = "🗖",
                Font      = new Font("Segoe UI Symbol", 12F, FontStyle.Bold),
                Size      = new Size(36, 36),
                Location  = new Point(_headerBar.Width - 48, 14),
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(20, 255, 255, 255) : Color.FromArgb(20, 0, 0, 0),
                ForeColor = ThemeManager.CurrentText,
                Cursor    = Cursors.Hand,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnToggleExpand.FlatAppearance.BorderSize = 0;
            SetRoundRegion(_btnToggleExpand, 10);
            _btnToggleExpand.Click += (s, e) =>
            {
                if (this.Parent is Panel hostPanel && hostPanel.Parent is Form parentForm)
                {
                    _isChatFullscreen = !_isChatFullscreen;
                    if (_isChatFullscreen)
                    {
                        _normalBounds = hostPanel.Bounds;
                        hostPanel.Bounds = parentForm.ClientRectangle;
                        hostPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                    }
                    else
                    {
                        hostPanel.Bounds = _normalBounds;
                        hostPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                    }
                    _btnToggleExpand.Text = _isChatFullscreen ? "🗗" : "🗖";
                    hostPanel.BringToFront();
                }
            };
            _headerBar.Controls.Add(_btnToggleExpand);
            _rightPane.Controls.Add(_headerBar);

            // ── Input Bar ──
            var inputBar = new Panel();
            EnableDB(inputBar);
            inputBar.Height = 72;
            inputBar.Dock   = DockStyle.Bottom;
            inputBar.BackColor = ThemeManager.CurrentSidebar;
            inputBar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillRectangle(new SolidBrush(ThemeManager.CurrentSidebar), inputBar.ClientRectangle);

                var inputR = new Rectangle(14, 12, inputBar.Width - 76, 46);
                using var path = RR(inputR, 22);
                g.FillPath(new SolidBrush(ThemeManager.CurrentInputBg), path);
                g.DrawPath(new Pen(ThemeManager.CurrentBorder, 1f), path);
            };

            _txtInput = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor   = ThemeManager.CurrentInputBg,
                ForeColor   = ThemeManager.CurrentText,
                Font        = new Font("Segoe UI", 10.5F),
                PlaceholderText = "Type a message...",
                Size        = new Size(inputBar.Width - 96, 28),
                Location    = new Point(28, 21),
                Anchor      = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
            };
            _txtInput.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                { e.SuppressKeyPress = true; SendMessage(); }
            };
            inputBar.Controls.Add(_txtInput);

            _btnSend = new Button
            {
                Text      = "➤",
                Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
                Size      = new Size(40, 40),
                Location  = new Point(inputBar.Width - 54, 15),
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeManager.CurrentPrimary,
                ForeColor = Color.White,
                Cursor    = Cursors.Hand,
                Anchor    = AnchorStyles.Right | AnchorStyles.Top
            };
            _btnSend.FlatAppearance.BorderSize = 0;
            SetRoundRegion(_btnSend, 20);
            _btnSend.Click += (s, e) => SendMessage();
            inputBar.Controls.Add(_btnSend);
            _rightPane.Controls.Add(inputBar);

            // ── Messages Stream / Welcome State ──
            _flowMessages = new DarkScrollFlowLayoutPanel
            {
                FlowDirection   = FlowDirection.TopDown,
                WrapContents    = false,
                AutoScroll      = true,
                Dock            = DockStyle.Fill,
                BackColor       = ThemeManager.CurrentBackground,
                // px-8 equivalent: 32px horizontal padding so bubbles never hug the screen edge
                Padding         = new Padding(16, 8, 16, 12)
            };
            EnableDB(_flowMessages);
            _flowMessages.MouseEnter     += (s, e) => { _flowMessages.Focus(); HideNativeScrollBars(_flowMessages); };
            _flowMessages.ControlAdded   += (s, e) => HideNativeScrollBars(_flowMessages);
            _flowMessages.ControlRemoved += (s, e) => HideNativeScrollBars(_flowMessages);
            _flowMessages.Paint          += (s, e) => HideNativeScrollBars(_flowMessages);
            _flowMessages.Scroll         += (s, e) => HideNativeScrollBars(_flowMessages);
            _flowMessages.MouseWheel     += (s, e) => HideNativeScrollBars(_flowMessages);

            _pnlWelcomeState = _flowMessages;

            _flowMessages.Resize += (s, e) =>
            {
                int targetW = Math.Max(100, _flowMessages.ClientSize.Width - 24);
                _flowMessages.SuspendLayout();
                foreach (Control ctrl in _flowMessages.Controls)
                {
                    if (ctrl is Panel p) p.Width = targetW;
                }
                _flowMessages.ResumeLayout();
                UpdateScrollBounds();
            };
            _rightPane.Controls.Add(_flowMessages);
            _rightPane.Controls.Add(_webView);

            inputBar.SendToBack();
            _headerBar.SendToBack();
            _flowMessages.BringToFront();
            if (_webView != null) _webView.BringToFront();

            ShowWelcomeScreen();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  DATABASE & REAL-TIME LOGIC
        // ════════════════════════════════════════════════════════════════════════
        private async void InitializeSignalR()
        {
            try
            {
                string baseUrl = ApiService.BaseUrl.Replace("/api", "").TrimEnd('/');
                string hubUrl = baseUrl + "/hubs/admin";
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(hubUrl)
                    .WithAutomaticReconnect()
                    .Build();

                // ── ReceiveChatMessage: now includes messageId (5th arg) ──────────────
                _hubConnection.On<string, string, string, string, string>(
                    "ReceiveChatMessage",
                    (senderId, receiverId, body, timestamp, messageId) =>
                {
                    if (this.IsDisposed || !this.IsHandleCreated) return;
                    this.Invoke((System.Windows.Forms.MethodInvoker)(async () =>
                    {
                        UpdateConversationLastMsg(senderId, receiverId, body, timestamp);

                        DateTime dt = DateTime.Now;
                        if (DateTime.TryParse(timestamp, out var parsedDt))
                            dt = parsedDt.ToLocalTime();

                        // ── Case 1: Message arrives in the currently open thread ──────
                        // User is already looking at it → render and send seen immediately.
                        if (_activeConvId != null
                            && senderId != "admin"
                            && ((senderId == _activeConvId && receiverId == "admin")
                               || (_activeConvIsGroup && receiverId == _activeConvId)))
                        {
                            AddMessage(body, false, dt, MessageDeliveryState.Delivered);
                            if (_flowMessages.Controls.Count > 0)
                                _flowMessages.ScrollControlIntoView(
                                    _flowMessages.Controls[_flowMessages.Controls.Count - 1]);

                            // ACK seen (not just delivered) — user is actively watching
                            _ = MarkConversationSeenAsync(senderId);
                        }
                        // ── Case 2: Background message — user is in a different chat ──
                        else if (senderId != "admin")
                        {
                            // Bump conversation to top of the list (move to index 1, after AI Copilot)
                            string contactId = senderId;
                            int foundIdx = _conversations.FindIndex(c => c.Id == contactId);
                            if (foundIdx > 1)
                            {
                                var bumped = _conversations[foundIdx];
                                _conversations.RemoveAt(foundIdx);
                                _conversations.Insert(1, bumped);
                            }

                            // Show Windows balloon tip notification
                            string senderName = _conversations.Find(c => c.Id == contactId).Name;
                            if (string.IsNullOrEmpty(senderName)) senderName = senderId;
                            ShowBalloonNotification($"New message from {senderName}", body);

                            // ACK delivered (not seen — user hasn't opened the thread)
                            if (int.TryParse(messageId, out int mid) && mid > 0)
                                _ = AckDeliveredAsync(mid);
                        }
                    }));
                });

                // ── MessageStatusChanged: real delivery/seen push from backend ───────
                _hubConnection.On<string, string, string, string>(
                    "MessageStatusChanged",
                    (messageId, status, senderId, receiverId) =>
                {
                    if (this.IsDisposed || !this.IsHandleCreated) return;
                    // Only update bubbles for messages WE sent
                    if (senderId != "admin") return;

                    var newState = status switch
                    {
                        "delivered" => MessageDeliveryState.Delivered,
                        "seen"      => MessageDeliveryState.Seen,
                        "sent"      => MessageDeliveryState.Sent,
                        _           => MessageDeliveryState.Sent
                    };

                    // Find the matching registered bubble by messageId tag
                    this.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                    {
                        // Update any bubble whose tag matches the messageId
                        foreach (var kvp in _bubbleRegistry)
                        {
                            var (row, stateHolder) = kvp.Value;
                            if (row?.Tag?.ToString() == messageId)
                            {
                                stateHolder[0] = newState;
                                _bubbleRegistry[kvp.Key] = (row, stateHolder);
                                if (row != null && !row.IsDisposed)
                                    row.Invalidate();
                                break;
                            }
                        }
                        // Also update last sent bubble if not found by tag (legacy path)
                        if (_lastSentBubbleId != null)
                            UpdateBubbleState(_lastSentBubbleId, newState);
                    }));
                });

                _hubConnection.On<string, string, string, string>("MessageEdited", (msgId, newText, history, recId) =>
                {
                    if (recId == _activeConvId || recId == "admin")
                        Invoke((System.Windows.Forms.MethodInvoker)(() => LoadMessagesFromApi(_activeConvId)));
                });

                _hubConnection.On<string, string>("MessageUnsent", (msgId, recId) =>
                {
                    if (recId == _activeConvId || recId == "admin")
                        Invoke((System.Windows.Forms.MethodInvoker)(() => LoadMessagesFromApi(_activeConvId)));
                });

                _hubConnection.On<string, string, string>("MessageReactionChanged", (msgId, rx, recId) =>
                {
                    if (recId == _activeConvId || recId == "admin")
                        Invoke((System.Windows.Forms.MethodInvoker)(() => LoadMessagesFromApi(_activeConvId)));
                });

                await _hubConnection.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ChatOverlayPanel] SignalR connection notice: " + ex.Message);
            }
        }

        private static string CleanMessagePreview(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Trim();
            if (text.StartsWith("{"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.TryGetProperty("text", out var tProp))
                    {
                        var s = tProp.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) return s;
                    }
                    if (doc.RootElement.TryGetProperty("message", out var mProp))
                    {
                        var s = mProp.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) return s;
                    }
                }
                catch { }
            }
            return text;
        }

        private void UpdateConversationLastMsg(string senderId, string receiverId, string body, string timestamp)
        {
            string contactId = senderId == "admin" ? receiverId : senderId;
            DateTime dt = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(timestamp) && DateTime.TryParse(timestamp, out var parsedDt))
                dt = parsedDt;

            string cleanBody = CleanMessagePreview(body);
            string isoTime = dt.ToString("o");

            bool found = false;
            for (int i = 0; i < _conversations.Count; i++)
            {
                var conv = _conversations[i];
                if (conv.Id == contactId)
                {
                    conv.LastMessage = cleanBody;
                    conv.Time = isoTime;
                    if (_activeConvId != contactId && senderId != "admin")
                    {
                        conv.UnreadCount++;
                    }
                    _conversations[i] = conv;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                LoadConversationsFromApi();
            }
            else
            {
                RefreshConvList();
            }
        }

        private async void LoadConversationsFromApi()
        {
            try
            {
                var res = await ApiService.GetAsync("messages/conversations?userId=admin");
                if (res.Success && !string.IsNullOrWhiteSpace(res.Body))
                {
                    var root = JsonDocument.Parse(res.Body).RootElement;
                    _conversations.Clear();

                    _conversations.Add(new ConvItem
                    {
                        Id = "ai_copilot",
                        Name = "Drive\u0026Go AI",
                        Role = "AI COPILOT",
                        LastMessage = "Omniscient AI Intelligence",
                        Time = "",
                        UnreadCount = 0,
                        IsGroup = false
                    });

                    foreach (var item in root.EnumerateArray())
                    {
                        string id = item.GetProperty("id").GetString();
                        if (string.Equals(id, "ai_copilot", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string name    = item.GetProperty("name").GetString();
                        string role    = item.TryGetProperty("role",        out var rProp) ? rProp.GetString()  : "Customer";
                        string lastMsg = item.TryGetProperty("lastMessage", out var mProp) ? mProp.GetString()  : "";
                        lastMsg = CleanMessagePreview(lastMsg);
                        string time    = item.TryGetProperty("time",        out var tProp) ? tProp.GetString()  : "";
                        int    unread  = item.TryGetProperty("unreadCount", out var uProp) ? uProp.GetInt32()  : 0;
                        bool   isGroup = role == "Group" || id.StartsWith("gc") || id.StartsWith("g");

                        // Keep raw ISO timestamp in Time so we can sort by it
                        _conversations.Add(new ConvItem
                        {
                            Id          = id,
                            Name        = name,
                            Role        = role,
                            LastMessage = lastMsg,
                            Time        = time,   // raw ISO string for sorting
                            UnreadCount = unread,
                            IsGroup     = isGroup
                        });
                    }

                    RefreshConvList();

                    // Auto-open the most recently active real conversation on first load
                    if (_activeConvId == null)
                    {
                        var mostRecent = _conversations
                            .Where(c => c.Id != "ai_copilot" &&
                                        !string.IsNullOrWhiteSpace(c.LastMessage) &&
                                        c.LastMessage != "No messages yet")
                            .OrderByDescending(c =>
                            {
                                return DateTime.TryParse(c.Time, out var dt) ? dt : DateTime.MinValue;
                            })
                            .FirstOrDefault();

                        if (!string.IsNullOrEmpty(mostRecent.Id))
                            OpenConversation(mostRecent);
                        else if (_conversations.Count > 0)
                            OpenConversation(_conversations[0]); // fallback to AI Copilot
                    }
                }
                else
                {
                    _conversations.Clear();
                    RefreshConvList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to load conversations: " + ex.Message);
                _conversations.Clear();
                RefreshConvList();
            }
        }

        private async void LoadMessagesFromApi(string contactId)
        {
            _flowMessages.Controls.Clear();

            var activeConv = _conversations.Find(c => c.Id == contactId);
            if (!string.IsNullOrEmpty(activeConv.Id))
                AddMessengerContactHeader(activeConv);

            try
            {
                var res = await ApiService.GetAsync($"messages?senderId=admin&receiverId={contactId}");
                if (res.Success && !string.IsNullOrWhiteSpace(res.Body))
                {
                    var root = JsonDocument.Parse(res.Body).RootElement;
                    int count = 0;
                    DateTime? lastTime = null;
                    foreach (var item in root.EnumerateArray())
                    {
                        string sId  = item.GetProperty("senderId").GetString();
                        string body = item.GetProperty("messageBody").GetString();
                        DateTime ts = item.GetProperty("timestamp").GetDateTime().ToLocalTime();

                        if (lastTime == null || ts.Date != lastTime.Value.Date || (ts - lastTime.Value).TotalHours > 1)
                        {
                            AddDateSeparator(ts);
                            lastTime = ts;
                        }

                        string rawStatus = item.TryGetProperty("deliveryStatus", out var dsProp)
                            ? (dsProp.GetString() ?? "sent") : "sent";
                        var historyState = rawStatus switch
                        {
                            "seen"      => MessageDeliveryState.Seen,
                            "delivered" => MessageDeliveryState.Delivered,
                            "sending"   => MessageDeliveryState.Sending,
                            _           => MessageDeliveryState.Sent
                        };

                        bool isMine  = sId == "admin";
                        string bId   = item.GetProperty("messageId").GetInt32().ToString();
                        bool isEdited  = item.TryGetProperty("isEdited",  out var eProp) && eProp.GetBoolean();
                        bool isUnsent  = item.TryGetProperty("isUnsent",  out var uProp) && uProp.GetBoolean();
                        string rx      = item.TryGetProperty("reactions",  out var rProp) ? (rProp.GetString() ?? "{}") : "{}";

                        AddMessage(body, isMine, ts, historyState, bId, isUnsent, isEdited, rx);
                        count++;
                    }

                    // Deferred multi-pass scroll — ensures viewport is positioned at absolute bottom after rendering
                    if (count > 0)
                    {
                        this.BeginInvoke((Action)(async () =>
                        {
                            _flowMessages.ScrollControlIntoView(
                                _flowMessages.Controls[_flowMessages.Controls.Count - 1]);
                            ScrollToBottom();
                            await Task.Delay(80);
                            ScrollToBottom();
                            await Task.Delay(250);
                            ScrollToBottom();
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to load messages: " + ex.Message);
            }
        }

        private void FilterConversations(string query)
        {
            RefreshConvList();
        }

        private static DateTime ParseConvTime(string timeStr)
        {
            if (string.IsNullOrWhiteSpace(timeStr)) return DateTime.MinValue;
            if (DateTime.TryParse(timeStr, out var dt)) return dt;
            return DateTime.MinValue;
        }

        private static string FormatDisplayTime(string timeStr)
        {
            if (string.IsNullOrWhiteSpace(timeStr)) return "";
            if (DateTime.TryParse(timeStr, out var dt))
            {
                dt = dt.ToLocalTime();
                if (dt.Date == DateTime.Today)
                    return dt.ToString("h:mm tt");
                if (dt.Date == DateTime.Today.AddDays(-1))
                    return "Yesterday";
                if ((DateTime.Today - dt.Date).TotalDays < 7)
                    return dt.ToString("ddd");
                return dt.ToString("MMM d");
            }
            return timeStr;
        }

        private void RefreshConvList()
        {
            _convListPanel.AutoScrollPosition = new Point(0, 0);
            _convListPanel.Controls.Clear();

            string query      = _txtSearch?.Text?.Trim().ToLower() ?? "";
            bool   isSearching = !string.IsNullOrEmpty(query);

            // ── Pinned #1: AI Copilot ───────────────────────────────────────────────
            var aiEntry = _conversations.Where(c => c.Id == "ai_copilot").ToList();

            // ── Pinned #2: Group Chats (Drivers Community GC, etc.) ────────────────
            var groupConvs = _conversations
                .Where(c => c.Id != "ai_copilot" && (c.IsGroup || c.Role == "Group" || c.Id.StartsWith("gc") || c.Id.StartsWith("g")))
                .OrderByDescending(c => c.UnreadCount > 0)
                .ThenByDescending(c => ParseConvTime(c.Time))
                .ToList();

            // ── Section 3: Established Customer / User Conversations ──────────────
            // (Sorted by Unread first, then Most Recent Chat timestamp descending)
            var customerConvs = _conversations
                .Where(c => c.Id != "ai_copilot" && !(c.IsGroup || c.Role == "Group" || c.Id.StartsWith("gc") || c.Id.StartsWith("g")))
                .OrderByDescending(c => c.UnreadCount > 0)
                .ThenByDescending(c => ParseConvTime(c.Time))
                .ToList();

            var sortedConvs = aiEntry.Concat(groupConvs).Concat(customerConvs).ToList();

            int y      = 6;
            int cardW  = Math.Max(100, _convListPanel.ClientSize.Width - 4);
            int countVisible = 0;

            foreach (var conv in sortedConvs)
            {
                bool hasMessages = !string.IsNullOrWhiteSpace(conv.LastMessage) &&
                                   conv.LastMessage != "No messages yet" &&
                                   conv.LastMessage != "Tap to start conversation" &&
                                   conv.LastMessage != "Group Chat Channel";

                // Group chats & AI Copilot are pinned; individual customers are shown if established or searching
                if (!isSearching && !hasMessages && conv.Id != "ai_copilot" && !conv.IsGroup && conv.Role != "Group")
                    continue;

                if (isSearching)
                {
                    bool matches = conv.Name.ToLower().Contains(query) ||
                                   conv.Role.ToLower().Contains(query)  ||
                                   conv.LastMessage.ToLower().Contains(query);
                    if (!matches) continue;
                }

                var card = BuildConvCard(conv);
                card.Width = cardW;
                card.Location = new Point(2, y);
                _convListPanel.Controls.Add(card);
                y += card.Height + 1;
                countVisible++;
            }

            if (countVisible == 0)
            {
                var lblEmpty = new Label
                {
                    Text      = isSearching
                        ? "No conversations found"
                        : "No active chats yet.\r\nClick '+' or search to start a chat.",
                    Font      = new Font("Segoe UI", 9.5F),
                    ForeColor = ThemeManager.CurrentSubText,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock      = DockStyle.Fill
                };
                _convListPanel.Controls.Add(lblEmpty);
            }
            else
            {
                _convListPanel.AutoScrollPosition = new Point(0, 0);
            }
        }

        private Panel BuildConvCard(ConvItem conv)
        {
            var card = new Panel();
            EnableDB(card);
            card.Width    = Math.Max(100, _convListPanel.ClientSize.Width - 4);
            card.Height   = 70;
            card.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            card.Cursor   = Cursors.Hand;
            card.BackColor = Color.Transparent;

            Color roleColor = conv.Role == "Driver"   ? Color.FromArgb(59, 130, 246)
                            : conv.Role == "Customer" ? Color.FromArgb(168, 85, 247)
                            : conv.Role == "Group"    ? Color.FromArgb(34, 197, 94)
                            : ThemeManager.CurrentPrimary;

            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                bool isActive = _activeConvId == conv.Id;
                bool hasUnread = conv.UnreadCount > 0;
                var r = new Rectangle(6, 2, card.Width - 12, card.Height - 4);
                using var path = RR(r, 10);

                // Active: smooth background highlight only — no full outline border
                Color bgAlpha = isActive
                    ? (ThemeManager.IsDarkMode
                        ? Color.FromArgb(55, 255, 255, 255)   // subtle white glow in dark mode
                        : Color.FromArgb(45, 0, 0, 0))        // subtle dark tint in light mode
                    : (hasUnread
                        ? (ThemeManager.IsDarkMode ? Color.FromArgb(40, 234, 88, 12) : Color.FromArgb(25, 234, 88, 12))
                        : (ThemeManager.IsDarkMode ? Color.FromArgb(8, 255, 255, 255) : Color.FromArgb(12, 0, 0, 0)));

                using var bg = new SolidBrush(bgAlpha);
                g.FillPath(bg, path);

                // Unread conversations keep a subtle orange outline — active does NOT get a full border
                if (!isActive && hasUnread)
                {
                    using var pen = new Pen(Color.FromArgb(160, 234, 88, 12), 1f);
                    g.DrawPath(pen, path);
                }

                // Left accent bar — always visible, thicker and more prominent when active
                int accentW = isActive ? 4 : 3;
                Color accentColor = isActive ? ThemeManager.CurrentPrimary : roleColor;
                g.FillRectangle(new SolidBrush(accentColor), 6, 4, accentW, card.Height - 8);

                var av = new Rectangle(18, 14, 40, 40);
                using var avGrad = new LinearGradientBrush(av, roleColor,
                    Color.FromArgb(Math.Max(0, roleColor.R - 40),
                                   Math.Max(0, roleColor.G - 40),
                                   Math.Max(0, roleColor.B - 40)),
                    LinearGradientMode.ForwardDiagonal);
                g.FillEllipse(avGrad, av);

                string init = conv.IsGroup ? "G" : (conv.Name.Length > 0 ? conv.Name[0].ToString().ToUpper() : "?");
                using var initFont = new Font("Segoe UI", 13F, FontStyle.Bold);
                using var fmt      = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(init, initFont, Brushes.White, new RectangleF(av.X, av.Y, av.Width, av.Height), fmt);

                // ── Name ───────────
                using var nameFont = new Font("Segoe UI", 10F, FontStyle.Bold);
                using var nameFmt  = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                
                string formattedTime = FormatDisplayTime(conv.Time);
                float rightPadding = !string.IsNullOrWhiteSpace(formattedTime) ? 75f : 14f;
                var nameRect = new RectangleF(68, 12, Math.Max(40, card.Width - 68 - rightPadding), 18);
                Color nameColor = hasUnread ? Color.FromArgb(255, 255, 255) : ThemeManager.CurrentText;
                g.DrawString(conv.Name, nameFont, new SolidBrush(nameColor), nameRect, nameFmt);

                using var roleFont = new Font("Segoe UI", 7.5F);
                var roleText = "[" + conv.Role.ToUpper() + "]";
                g.DrawString(roleText, roleFont, new SolidBrush(roleColor), new PointF(68, 30));

                // ── Last message (bold + vibrant if unread, muted if read) ───────────
                Font msgFont = hasUnread ? new Font("Segoe UI", 9F, FontStyle.Bold) : new Font("Segoe UI", 9F);
                Color msgColor = hasUnread
                    ? (ThemeManager.IsDarkMode ? Color.FromArgb(254, 215, 170) : Color.FromArgb(194, 65, 12))
                    : ThemeManager.CurrentSubText;

                string lastMsg = CleanMessagePreview(conv.LastMessage);
                float msgRightPadding = hasUnread ? 34f : 14f;
                var msgRect = new RectangleF(68, 46, Math.Max(40, card.Width - 68 - msgRightPadding), 18);
                using var msgFmt = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                g.DrawString(lastMsg, msgFont, new SolidBrush(msgColor), msgRect, msgFmt);
                msgFont.Dispose();

                // ── Time Stamp ───────────
                if (!string.IsNullOrWhiteSpace(formattedTime))
                {
                    using var timeFont = new Font("Segoe UI", 7.5F, hasUnread ? FontStyle.Bold : FontStyle.Regular);
                    using var timeFmt = new StringFormat { Alignment = StringAlignment.Far };
                    Color timeColor = hasUnread ? Color.FromArgb(249, 115, 22) : ThemeManager.CurrentSubText;
                    g.DrawString(formattedTime, timeFont, new SolidBrush(timeColor), new RectangleF(card.Width - 75, 12, 65, 16), timeFmt);
                }

                // ── Unread Badge Pill ───────────
                if (hasUnread)
                {
                    var badge = new Rectangle(card.Width - 32, card.Height - 26, 24, 18);
                    using var badgeBrush = new SolidBrush(Color.FromArgb(234, 88, 12));
                    using var badgePath = RR(badge, 9);
                    g.FillPath(badgeBrush, badgePath);
                    using var badgeFont = new Font("Segoe UI", 8F, FontStyle.Bold);
                    g.DrawString(conv.UnreadCount.ToString(), badgeFont, Brushes.White,
                        new RectangleF(badge.X, badge.Y, badge.Width, badge.Height), fmt);
                }

                g.DrawLine(new Pen(ThemeManager.CurrentBorder, 1f),
                    18, card.Height - 1, card.Width - 18, card.Height - 1);
            };

            card.Click += (s, e) => OpenConversation(conv);
            return card;
        }

        private void OpenConversation(ConvItem conv)
        {
            _activeConvId = conv.Id;
            _activeConvIsGroup = conv.IsGroup;

            _lblConvName.Text   = conv.Name;
            _lblConvStatus.Text = conv.IsGroup ? "Group Chat • Active" : $"{conv.Role} • Online";
            _headerBar.Invalidate();

            if (conv.Id == "ai_copilot")
            {
                if (_webView != null) 
                {
                    _webView.Visible = true;
                    if (_isInitialized)
                    {
                        _webView.CoreWebView2.ExecuteScriptAsync($"document.documentElement.setAttribute('data-theme', '{ (ThemeManager.IsDarkMode ? "dark" : "light") }');");
                    }
                }
                _flowMessages.Visible = false;
                if (_txtInput?.Parent != null) _txtInput.Parent.Visible = false;
                if (_btnSend != null) _btnSend.Visible = false;
            }
            else
            {
                if (_webView != null) _webView.Visible = false;
                _flowMessages.Visible = true;
                if (_txtInput?.Parent != null) _txtInput.Parent.Visible = true;
                if (_btnSend != null) _btnSend.Visible = true;
            }

            for (int i = 0; i < _conversations.Count; i++)
            {
                if (_conversations[i].Id == conv.Id)
                {
                    var c = _conversations[i];
                    c.UnreadCount = 0;
                    _conversations[i] = c;
                    break;
                }
            }

            RefreshConvList();

            if (conv.Id != "ai_copilot")
            {
                LoadMessagesFromApi(conv.Id);
                // Mark all incoming messages in this thread as Seen
                // (admin opened the chat → recipient side sees "Seen")
                _ = MarkConversationSeenAsync(conv.Id);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  MESSENGER-STYLE PROFILE WELCOME CONTAINER
        // ════════════════════════════════════════════════════════════════════════
        private void ShowWelcomeScreen()
        {
            _flowMessages.Controls.Clear();
            _lblConvName.Text   = "DriveAndGo Hubs";
            _lblConvStatus.Text = "Real-time Messaging Hub";

            var container = new Panel
            {
                Width     = Math.Max(280, _flowMessages.ClientSize.Width - 40),
                Height    = 460,
                BackColor = Color.Transparent,
                Margin    = new Padding(10, 20, 10, 20)
            };
            EnableDB(container);

            container.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int cx = container.Width / 2;

                int avR = 90;
                var avRect = new Rectangle(cx - avR / 2, 25, avR, avR);
                using (var avGrad = new LinearGradientBrush(avRect, ThemeManager.CurrentPrimary, ThemeManager.CurrentPrimaryGlow, LinearGradientMode.ForwardDiagonal))
                {
                    g.FillEllipse(avGrad, avRect);
                }
                using (var pen = new Pen(ThemeManager.CurrentBorder, 3f))
                {
                    g.DrawEllipse(pen, avRect.X - 4, avRect.Y - 4, avR + 8, avR + 8);
                }

                using (var font = new Font("Segoe UI", 32F, FontStyle.Bold))
                using (var fmt  = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("⚡", font, Brushes.White, new RectangleF(avRect.X, avRect.Y, avRect.Width, avRect.Height), fmt);
                }

                string title = "DriveAndGo Messaging Hub";
                float titleFontSize = container.Width < 340 ? 12F : 14.5F;
                using (var titleFont = new Font("Segoe UI", titleFontSize, FontStyle.Bold))
                {
                    SizeF titleSize = g.MeasureString(title, titleFont);
                    float totalTitleW = titleSize.Width + 22;
                    float titleX = Math.Max(10f, (container.Width - totalTitleW) / 2f);
                    g.DrawString(title, titleFont, new SolidBrush(ThemeManager.CurrentText), new PointF(titleX, 130));

                    int badgeX = (int)(titleX + titleSize.Width + 4);
                    var vBadge = new Rectangle(badgeX, 134, 16, 16);
                    g.FillEllipse(Brushes.DodgerBlue, vBadge);
                    DrawCheckmark(g, vBadge, Color.White, 1.2f);
                }

                using (var subFont = new Font("Segoe UI", 9F))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center })
                {
                    g.DrawString("Business chats and driver communications", subFont, new SolidBrush(ThemeManager.CurrentSubText), new PointF(cx, 162), fmt);
                }

                int cardW = Math.Min(300, Math.Max(220, container.Width - 32));
                var cardRect = new Rectangle((container.Width - cardW) / 2, 200, cardW, 95);
                using (var path = RR(cardRect, 14))
                {
                    using var cardBg  = new SolidBrush(ThemeManager.CurrentCard);
                    using var cardPen = new Pen(ThemeManager.CurrentBorder, 1f);
                    g.FillPath(cardBg, path);
                    g.DrawPath(cardPen, path);
                }

                using (var bodyFont = new Font("Segoe UI", 9F))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("Select a conversation from the left menu to start messaging drivers, customers, or groups.", bodyFont, new SolidBrush(ThemeManager.CurrentText), new RectangleF(cardRect.X + 12, cardRect.Y + 8, cardRect.Width - 24, cardRect.Height - 16), fmt);
                }

                using (var lockFont = new Font("Segoe UI", 8.5F))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center })
                {
                    g.DrawString("🔒 End-to-end encrypted dispatch network", lockFont, new SolidBrush(ThemeManager.CurrentSubText), new PointF(cx, 310), fmt);
                }
            };

            var btnStart = new Button
            {
                Text      = "Select Conversation",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                Size      = new Size(180, 40),
                Location  = new Point((container.Width - 180) / 2, 355),
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeManager.CurrentPrimary,
                ForeColor = Color.White,
                Cursor    = Cursors.Hand
            };
            btnStart.FlatAppearance.BorderSize = 0;
            SetRoundRegion(btnStart, 20);
            btnStart.Click += (s, e) =>
            {
                if (_conversations.Count > 0)
                {
                    OpenConversation(_conversations[0]);
                }
                else
                {
                    _txtSearch?.Focus();
                }
            };
            container.Controls.Add(btnStart);

            container.Resize += (s, e) =>
            {
                btnStart.Location = new Point((container.Width - btnStart.Width) / 2, 355);
            };

            _flowMessages.Controls.Add(container);
            UpdateScrollBounds();
        }

        private void AddMessengerContactHeader(ConvItem conv)
        {
            // ── Top spacer: ensures the avatar is never clipped under the sticky header bar ──
            var spacer = new Panel
            {
                Width     = Math.Max(280, _flowMessages.ClientSize.Width - 40),
                Height    = 12,
                BackColor = Color.Transparent,
                Margin    = new Padding(0)
            };
            _flowMessages.Controls.Add(spacer);

            var headerCard = new Panel
            {
                Width     = Math.Max(280, _flowMessages.ClientSize.Width - 40),
                Height    = 170,
                BackColor = Color.Transparent,
                Margin    = new Padding(10, 4, 10, 10)
            };
            EnableDB(headerCard);

            Color roleColor = conv.Role == "Driver"   ? Color.FromArgb(59, 130, 246)
                            : conv.Role == "Customer" ? Color.FromArgb(168, 85, 247)
                            : conv.Role == "Group"    ? Color.FromArgb(34, 197, 94)
                            : ThemeManager.CurrentPrimary;

            headerCard.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int cx = headerCard.Width / 2;

                var avRect = new Rectangle(cx - 35, 10, 70, 70);
                using (var grad = new LinearGradientBrush(avRect, roleColor, Color.FromArgb(Math.Max(0, roleColor.R - 50), Math.Max(0, roleColor.G - 50), Math.Max(0, roleColor.B - 50)), LinearGradientMode.ForwardDiagonal))
                {
                    g.FillEllipse(grad, avRect);
                }

                string init = conv.IsGroup ? "G" : (conv.Name.Length > 0 ? conv.Name[0].ToString().ToUpper() : "?");
                using (var initFont = new Font("Segoe UI", 22F, FontStyle.Bold))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(init, initFont, Brushes.White, new RectangleF(avRect.X, avRect.Y, avRect.Width, avRect.Height), fmt);
                }

                using (var nameFont = new Font("Segoe UI", 12.5F, FontStyle.Bold))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center })
                {
                    g.DrawString(conv.Name, nameFont, new SolidBrush(ThemeManager.CurrentText), new PointF(cx, 88), fmt);
                }

                using (var subFont = new Font("Segoe UI", 9F))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center })
                {
                    g.DrawString($"{conv.Role.ToUpper()} • DriveAndGo Network", subFont, new SolidBrush(roleColor), new PointF(cx, 115), fmt);
                    g.DrawString("You're connected on DriveAndGo Dispatch", subFont, new SolidBrush(ThemeManager.CurrentSubText), new PointF(cx, 135), fmt);
                }
            };

            _flowMessages.Controls.Add(headerCard);
            UpdateScrollBounds();
        }

        private static string FormatMessengerDateDivider(DateTime ts)
        {
            DateTime now = DateTime.Now;
            if (ts.Date == now.Date)
                return "Today";
            if (ts.Date == now.Date.AddDays(-1))
                return "Yesterday";
            if ((now.Date - ts.Date).TotalDays < 7)
                return ts.ToString("dddd"); // e.g. "Monday", "Tuesday", "Sunday"
            if (ts.Year == now.Year)
                return ts.ToString("MMMM d"); // e.g. "July 13"
            return ts.ToString("MMMM d, yyyy"); // e.g. "July 13, 2025"
        }

        private void AddDateSeparator(DateTime ts)
        {
            var pnl = new Panel { Width = _flowMessages.ClientSize.Width - 24, Height = 30, Margin = new Padding(0, 6, 0, 6), BackColor = Color.Transparent };
            EnableDB(pnl);
            pnl.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                string text = FormatMessengerDateDivider(ts);
                using var font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                using var brush = new SolidBrush(ThemeManager.IsDarkMode ? Color.FromArgb(160, 255, 255, 255) : Color.FromArgb(140, 0, 0, 0));
                var size = g.MeasureString(text, font);
                g.DrawString(text, font, brush, new PointF((pnl.Width - size.Width) / 2, (pnl.Height - size.Height) / 2));
            };
            _flowMessages.Controls.Add(pnl);
            UpdateScrollBounds();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  MESSAGE BUBBLES & MESSENGER DELIVERY STATUS ICONS
        // ════════════════════════════════════════════════════════════════════════
        private void AddMessage(string text, bool isMine, DateTime time, MessageDeliveryState state = MessageDeliveryState.Delivered, string bubbleId = null, bool isUnsent = false, bool isEdited = false, string reactionsJson = "{}")
        {
            // ── Horizontal inset: keeps bubbles away from the absolute container edges ──
            const int hInset = 20;  // pixels of breathing room on each side
            int maxW = Math.Max(200, _flowMessages.ClientSize.Width - 120);
            int padH = 12, padV = 9;

            if (isUnsent)
                text = isMine ? "You unsent a message" : "This message was unsent";

            SizeF sz;
            using (var g = this.CreateGraphics())
            using (var font = isUnsent ? new Font("Segoe UI", 10.5F, FontStyle.Italic) : new Font("Segoe UI", 10.5F))
                sz = g.MeasureString(text, font, maxW - padH * 2);

            int bubbleW = Math.Min((int)sz.Width + padH * 2 + 16, maxW);
            int bubbleH = Math.Max((int)sz.Height + padV * 2 + 4, 38);

            var row = new Panel();
            EnableDB(row);
            // Leave hInset on each side so bubbles never touch the scroll container walls
            row.Width     = _flowMessages.ClientSize.Width - 24;
            row.Height    = bubbleH + 32;  // extra bottom room for timestamp + delivery icon
            row.BackColor = Color.Transparent;
            row.Margin    = new Padding(0, 3, 0, 3);

            // ── Per-row hover & hitbox state ─────────────────────────────────
            bool rowHovered      = false;
            bool smileHovered    = false;
            bool dotsHovered     = false;
            bool rxPillHovered   = false;
            Rectangle smileRect  = Rectangle.Empty;
            Rectangle dotsRect   = Rectangle.Empty;
            Rectangle rxPillRect = Rectangle.Empty;

            // ── Dark-themed ContextMenuStrip ──────────────────────────────────
            ContextMenuStrip BuildContextMenu()
            {
                var ctx = new ContextMenuStrip
                {
                    ShowImageMargin  = false,
                    ShowCheckMargin  = false,
                    BackColor        = Color.FromArgb(36, 37, 38),
                    ForeColor        = Color.FromArgb(228, 230, 235),
                    Font             = new Font("Segoe UI", 10F, FontStyle.Regular),
                    RenderMode       = ToolStripRenderMode.Professional,
                    Renderer         = new MessengerMenuRenderer()
                };
                if (!isUnsent && bubbleId != null)
                {
                    AddMenuItem(ctx, "↪  Forward",      ()=> ForwardMessageAction(bubbleId));
                    AddMenuItem(ctx, "🗑  Remove for you", ()=> RemoveMessageAction(bubbleId));
                    AddMenuItem(ctx, "😊  React",        ()=> ReactMessageAction(bubbleId));
                    if (isMine)
                        AddMenuItem(ctx, "↩  Unsend", ()=> UnsendMessageAction(bubbleId), isDestructive: true);
                }
                return ctx;
            }

            var stateHolder = new[] { state };
            if (bubbleId != null)
                _bubbleRegistry[bubbleId] = (row, stateHolder);

            // ── Mouse events: hover tracking + precise hitbox cursor ─────────
            row.MouseEnter += (s, e) => { rowHovered = true;  row.Invalidate(); };
            row.MouseLeave += (s, e) =>
            {
                rowHovered    = false;
                smileHovered  = false;
                dotsHovered   = false;
                rxPillHovered = false;
                row.Cursor    = Cursors.Default;
                row.Invalidate();
            };
            row.MouseMove += (s, e) =>
            {
                if (isUnsent) return;
                int scrollOffsetY = Math.Abs(this.AutoScrollPosition.Y) + Math.Abs(_flowMessages?.AutoScrollPosition.Y ?? 0);
                Point mousePt = scrollOffsetY > 0 ? new Point(e.X, e.Y + scrollOffsetY) : e.Location;

                bool newSmile  = smileRect  != Rectangle.Empty && (smileRect.Contains(e.Location) || smileRect.Contains(mousePt));
                bool newDots   = dotsRect   != Rectangle.Empty && (dotsRect.Contains(e.Location) || dotsRect.Contains(mousePt));
                bool newRxPill = rxPillRect != Rectangle.Empty && (rxPillRect.Contains(e.Location) || rxPillRect.Contains(mousePt));

                if (newSmile != smileHovered || newDots != dotsHovered || newRxPill != rxPillHovered)
                {
                    smileHovered  = newSmile;
                    dotsHovered   = newDots;
                    rxPillHovered = newRxPill;
                    row.Cursor    = (newSmile || newDots || newRxPill) ? Cursors.Hand : Cursors.Default;
                    row.Invalidate();
                }
            };
            row.MouseClick += (s, e) =>
            {
                if (isUnsent) return;
                int scrollOffsetY = Math.Abs(this.AutoScrollPosition.Y) + Math.Abs(_flowMessages?.AutoScrollPosition.Y ?? 0);
                Point mousePt = scrollOffsetY > 0 ? new Point(e.X, e.Y + scrollOffsetY) : e.Location;

                if (smileRect != Rectangle.Empty && (smileRect.Contains(e.Location) || smileRect.Contains(mousePt)))
                {
                    ReactMessageAction(bubbleId);
                    return;
                }
                if (dotsRect != Rectangle.Empty && (dotsRect.Contains(e.Location) || dotsRect.Contains(mousePt)))
                {
                    var ctx = BuildContextMenu();
                    ctx.Show(row, e.Location);
                    return;
                }
                if (rxPillRect != Rectangle.Empty && (rxPillRect.Contains(e.Location) || rxPillRect.Contains(mousePt)))
                {
                    ShowReactionDetails(reactionsJson, bubbleId);
                }
            };

            row.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode     = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // hInset (captured from outer scope) keeps bubbles away from the absolute row edges
                int bx = isMine ? row.Width - bubbleW - hInset : hInset;
                int by = 4;
                var br = new Rectangle(bx, by, bubbleW, bubbleH);

                // ── Bubble body ───────────────────────────────────────────────
                if (isUnsent)
                {
                    using var path  = RR(br, 14);
                    using var bg    = new SolidBrush(ThemeManager.CurrentBackground);
                    using var pen   = new Pen(ThemeManager.CurrentBorder, 1f) { DashStyle = DashStyle.Dash };
                    g.FillPath(bg, path);
                    g.DrawPath(pen, path);
                    using var font  = new Font("Segoe UI", 10.5F, FontStyle.Italic);
                    g.DrawString(text, font, new SolidBrush(ThemeManager.CurrentSubText),
                        new RectangleF(bx + padH, by + padV, bubbleW - padH * 2, bubbleH));
                }
                else if (isMine)
                {
                    using var grad = new LinearGradientBrush(br,
                        ThemeManager.CurrentPrimary, ThemeManager.CurrentPrimaryDark,
                        LinearGradientMode.Vertical);
                    using var path = RR(br, 14);
                    g.FillPath(grad, path);
                    var shineR = new Rectangle(br.X + 2, br.Y + 2, br.Width - 4, br.Height / 2);
                    if (!shineR.IsEmpty)
                    {
                        using var sp    = RR(shineR, 12);
                        using var shine = new LinearGradientBrush(shineR,
                            Color.FromArgb(40, 255, 255, 255), Color.FromArgb(0, 255, 255, 255),
                            LinearGradientMode.Vertical);
                        g.FillPath(shine, sp);
                    }
                    using var font = new Font("Segoe UI", 10.5F);
                    g.DrawString(text, font, Brushes.White,
                        new RectangleF(bx + padH, by + padV, bubbleW - padH * 2, bubbleH));
                }
                else
                {
                    using var path = RR(br, 14);
                    using var bg   = new SolidBrush(ThemeManager.CurrentCard);
                    using var pen  = new Pen(ThemeManager.CurrentBorder, 1f);
                    g.FillPath(bg, path);
                    g.DrawPath(pen, path);
                    using var font = new Font("Segoe UI", 10.5F);
                    g.DrawString(text, font, new SolidBrush(ThemeManager.CurrentText),
                        new RectangleF(bx + padH, by + padV, bubbleW - padH * 2, bubbleH));
                }

                if (isEdited && !isUnsent)
                {
                    using var efont = new Font("Segoe UI", 7F);
                    // Align "(edited)" to the right edge of the bubble for sent, left for received
                    float editX = isMine ? bx + bubbleW - 44 : bx + 2;
                    g.DrawString("(edited)", efont, new SolidBrush(ThemeManager.CurrentSubText),
                        new PointF(editX, by + bubbleH + 2));
                }
                
                // ── Reaction pill (clickable) ────────────────────────────────
                var rxDict = new Dictionary<string, string>();
                if (!string.IsNullOrWhiteSpace(reactionsJson) && reactionsJson != "{}")
                    try { rxDict = JsonSerializer.Deserialize<Dictionary<string, string>>(reactionsJson); } catch {}

                rxPillRect = Rectangle.Empty;
                if (rxDict != null && rxDict.Count > 0 && !isUnsent)
                {
                    string emoji = "";
                    foreach (var kvp in rxDict) { emoji = kvp.Value; break; }
                    string rxText  = rxDict.Count > 1 ? $"{emoji} {rxDict.Count}" : emoji;
                    using var rxFont = new Font("Segoe UI Emoji", 9F);
                    var rxSz = g.MeasureString(rxText, rxFont);
                    var rxRect = new Rectangle(
                        bx + bubbleW - (int)rxSz.Width - 14,
                        by + bubbleH - 10,
                        (int)rxSz.Width + 12, (int)rxSz.Height + 4);
                    rxPillRect = rxRect;   // update hitbox

                    Color pillBg = rxPillHovered
                        ? Color.FromArgb(80, ThemeManager.CurrentPrimary)
                        : ThemeManager.CurrentCard;
                    using var rxBg   = new SolidBrush(pillBg);
                    using var rxPen  = new Pen(ThemeManager.CurrentBorder, 1f);
                    using var rxPath = RR(rxRect, 10);
                    g.FillPath(rxBg,  rxPath);
                    g.DrawPath(rxPen, rxPath);
                    g.DrawString(rxText, rxFont, new SolidBrush(ThemeManager.CurrentText),
                        new PointF(rxRect.X + 4, rxRect.Y + 1));
                }

                // ── Hover action icons (Smile + 3-dots) ───────────────────────
                if (rowHovered && !isUnsent)
                {
                    int iconSize = 24;
                    int iconY    = by + bubbleH / 2 - iconSize / 2;

                    if (isMine)
                    {
                        // Smile left of bubble, Dots further left
                        dotsRect  = new Rectangle(bx - iconSize - 4,        iconY, iconSize, iconSize);
                        smileRect = new Rectangle(bx - iconSize * 2 - 8,    iconY, iconSize, iconSize);
                    }
                    else
                    {
                        // Smile right of bubble, Dots further right
                        smileRect = new Rectangle(bx + bubbleW + 4,          iconY, iconSize, iconSize);
                        dotsRect  = new Rectangle(bx + bubbleW + iconSize + 8, iconY, iconSize, iconSize);
                    }

                    DrawHoverIcon(g, smileRect, smileHovered, isSmile: true);
                    DrawHoverIcon(g, dotsRect,  dotsHovered,  isSmile: false);
                }
                else
                {
                    smileRect = Rectangle.Empty;
                    dotsRect  = Rectangle.Empty;
                }

                // ── Timestamp + delivery state ────────────────────────────────
                // Both are rendered flush with the edge of the bubble (not the row edge)
                // so they stay properly aligned in both fullscreen and split view.
                using var tFont  = new Font("Segoe UI", 7.5F);
                using var metaColor = new SolidBrush(ThemeManager.IsDarkMode
                    ? Color.FromArgb(130, 200, 200, 200)
                    : Color.FromArgb(130, 80, 80, 80));
                string ts = time.ToString("h:mm tt");

                if (isMine && !isUnsent)
                {
                    string statusLabel = stateHolder[0] switch
                    {
                        MessageDeliveryState.Sending   => "Sending",
                        MessageDeliveryState.Sent      => "Sent",
                        MessageDeliveryState.Delivered => "Delivered",
                        MessageDeliveryState.Seen      => "Seen",
                        _                              => ""
                    };
                    using var lblFont = new Font("Segoe UI", 7F);

                    // Metadata baseline: sits 4px below the bubble bottom, right-aligned to bubble right edge
                    float metaY    = by + bubbleH + 5;
                    int   iconSize = 14;

                    // Delivery icon — right-aligned to bubble right edge
                    var iconRect = new Rectangle(bx + bubbleW - iconSize, (int)metaY + 1, iconSize, iconSize);
                    DrawDeliveryIcon(g, iconRect, stateHolder[0]);

                    // Status label — sits left of the icon, with a 3px gap
                    var lblSz   = g.MeasureString(statusLabel, lblFont);
                    float lblX  = iconRect.X - lblSz.Width - 3;
                    Color lblColor = stateHolder[0] == MessageDeliveryState.Seen
                        ? Color.FromArgb(200, ThemeManager.CurrentPrimary)
                        : (ThemeManager.IsDarkMode ? Color.FromArgb(140, 200, 200, 200) : Color.FromArgb(140, 80, 80, 80));
                    if (!string.IsNullOrEmpty(statusLabel))
                        g.DrawString(statusLabel, lblFont, new SolidBrush(lblColor), new PointF(lblX, metaY + 1));

                    // Timestamp — left of the status label, flush with bubble left
                    var tsSz = g.MeasureString(ts, tFont);
                    float tsX = bx;   // left-aligned to bubble left edge
                    g.DrawString(ts, tFont, metaColor, new PointF(tsX, metaY));
                }
                else if (!isMine)
                {
                    // Timestamp below the received bubble, left-aligned to bubble left edge
                    float metaY = by + bubbleH + 5;
                    g.DrawString(ts, tFont, metaColor, new PointF(bx, metaY));
                }
            };  // end row.Paint

            _flowMessages.Controls.Add(row);
            ScrollToBottom();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  VIRTUAL SCROLLING & BOUNDS CALCULATION
        // ════════════════════════════════════════════════════════════════════════
        public void UpdateScrollBounds()
        {
            int totalHeight = 0;
            if (_flowMessages != null && _flowMessages.Controls.Count > 0)
            {
                foreach (Control ctrl in _flowMessages.Controls)
                {
                    if (ctrl != null && ctrl.Visible)
                        totalHeight += ctrl.Height + ctrl.Margin.Top + ctrl.Margin.Bottom;
                }
                totalHeight += _flowMessages.Padding.Top + _flowMessages.Padding.Bottom;
                _flowMessages.AutoScrollMinSize = new Size(0, totalHeight);
            }
            HideNativeScrollBars(_flowMessages);
        }

        private void ScrollToBottom()
        {
            UpdateScrollBounds();
            if (_flowMessages != null && _flowMessages.Controls.Count > 0)
            {
                var lastControl = _flowMessages.Controls[_flowMessages.Controls.Count - 1];
                _flowMessages.ScrollControlIntoView(lastControl);

                int maxScroll = Math.Max(0, _flowMessages.VerticalScroll.Maximum - _flowMessages.ClientSize.Height);
                _flowMessages.AutoScrollPosition = new Point(0, maxScroll);
                _flowMessages.Invalidate();
            }
        }

        // ── Draw one hover action icon (Smile or 3-dots) ─────────────────────
        private void DrawHoverIcon(Graphics g, Rectangle r, bool hovered, bool isSmile)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color bgColor = hovered
                ? Color.FromArgb(60, 255, 255, 255)
                : Color.FromArgb(25, 255, 255, 255);

            using var bgBrush = new SolidBrush(bgColor);
            using var bgPath  = new GraphicsPath();
            bgPath.AddEllipse(r);
            g.FillPath(bgBrush, bgPath);

            using var iconPen = new Pen(ThemeManager.CurrentSubText, 1.4f)
            {
                StartCap = LineCap.Round,
                EndCap   = LineCap.Round
            };

            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            float rs = r.Width * 0.3f;

            if (isSmile)
            {
                // Smiley circle
                var innerR = new RectangleF(cx - rs, cy - rs, rs * 2, rs * 2);
                g.DrawEllipse(iconPen, innerR);
                // Eyes
                g.FillEllipse(new SolidBrush(ThemeManager.CurrentSubText),
                    cx - rs * 0.4f - 1, cy - rs * 0.25f, 2.5f, 2.5f);
                g.FillEllipse(new SolidBrush(ThemeManager.CurrentSubText),
                    cx + rs * 0.15f,    cy - rs * 0.25f, 2.5f, 2.5f);
                // Smile arc
                var smileArc = new RectangleF(cx - rs * 0.5f, cy, rs, rs * 0.6f);
                g.DrawArc(iconPen, smileArc, 10, 160);
            }
            else
            {
                // Three horizontal dots
                float dotR = 2f;
                for (int i = -1; i <= 1; i++)
                    g.FillEllipse(new SolidBrush(ThemeManager.CurrentSubText),
                        cx + i * (dotR * 2.4f) - dotR, cy - dotR, dotR * 2, dotR * 2);
            }
        }

        private void DrawDeliveryIcon(Graphics g, Rectangle rect, MessageDeliveryState state)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            switch (state)
            {
                case MessageDeliveryState.Sending:
                    using (var pen = new Pen(Color.FromArgb(100, 140, 200), 1.5f))
                    {
                        pen.DashStyle   = DashStyle.Custom;
                        pen.DashPattern = new float[] { 3f, 2f };
                        g.DrawEllipse(pen, rect);
                    }
                    break;

                case MessageDeliveryState.Sent:
                    using (var pen = new Pen(Color.FromArgb(59, 130, 246), 1.5f))
                    {
                        g.DrawEllipse(pen, rect);
                    }
                    DrawCheckmark(g, rect, Color.FromArgb(59, 130, 246), 1.2f);
                    break;

                case MessageDeliveryState.Delivered:
                    using (var bgBrush = new SolidBrush(Color.FromArgb(59, 130, 246)))
                    {
                        g.FillEllipse(bgBrush, rect);
                    }
                    DrawCheckmark(g, rect, Color.White, 1.4f);
                    break;

                case MessageDeliveryState.Seen:
                    var sq = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
                    using (var clipPath = new GraphicsPath())
                    {
                        clipPath.AddEllipse(sq);
                        g.SetClip(clipPath);
                        using (var grad = new LinearGradientBrush(
                            sq, ThemeManager.CurrentPrimary, ThemeManager.CurrentPrimaryDark,
                            LinearGradientMode.ForwardDiagonal))
                        {
                            g.FillRectangle(grad, sq);
                        }

                        string init = !string.IsNullOrEmpty(_lblConvName?.Text) && _lblConvName.Text.Length > 0
                            ? _lblConvName.Text[0].ToString().ToUpper() : "U";
                        using (var font = new Font("Segoe UI", 7F, FontStyle.Bold))
                        using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        {
                            g.DrawString(init, font, Brushes.White, new RectangleF(sq.X, sq.Y, sq.Width, sq.Height), fmt);
                        }
                        g.ResetClip();
                    }
                    break;
            }
        }

        private void DrawCheckmark(Graphics g, Rectangle rect, Color color, float thickness)
        {
            float cx = rect.X + rect.Width / 2f;
            float cy = rect.Y + rect.Height / 2f;
            float s = rect.Width * 0.22f;
            using var pen = new Pen(color, thickness)
            {
                StartCap = LineCap.Round,
                EndCap   = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            g.DrawLines(pen, new PointF[]
            {
                new(cx - s * 1.1f, cy),
                new(cx - s * 0.2f, cy + s * 0.85f),
                new(cx + s * 1.1f, cy - s * 0.85f)
            });
        }

        private void UpdateBubbleState(string bubbleId, MessageDeliveryState newState)
        {
            if (string.IsNullOrEmpty(bubbleId) || !_bubbleRegistry.ContainsKey(bubbleId)) return;
            var (row, stateHolder) = _bubbleRegistry[bubbleId];
            stateHolder[0] = newState;
            _bubbleRegistry[bubbleId] = (row, stateHolder);
            if (row != null && !row.IsDisposed)
            {
                if (row.InvokeRequired)
                    row.BeginInvoke((System.Windows.Forms.MethodInvoker)(() => row.Invalidate()));
                else
                    row.Invalidate();
            }
        }

        private async void SendMessage()
        {
            string text = _txtInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(text) || _activeConvId == null) return;
            _txtInput.Clear();

            string bubbleId = Guid.NewGuid().ToString();
            _lastSentBubbleId = bubbleId;
            DateTime now = DateTime.Now;

            AddMessage(text, true, now, MessageDeliveryState.Sending, bubbleId);
            if (_flowMessages.Controls.Count > 0)
                _flowMessages.ScrollControlIntoView(_flowMessages.Controls[_flowMessages.Controls.Count - 1]);

            try
            {
                var payload = new
                {
                    senderId    = "admin",
                    receiverId  = _activeConvId,
                    messageBody = text,
                    isGroupChat = _activeConvIsGroup
                };
                var res = await ApiService.PostAsync("messages", payload);
                if (res.Success)
                {
                    // ── Sent: message is in the DB ──────────────────────────────────
                    UpdateBubbleState(bubbleId, MessageDeliveryState.Sent);

                    // Tag the bubble row with the real DB messageId so
                    // MessageStatusChanged can find it by tag later
                    if (!string.IsNullOrWhiteSpace(res.Body))
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(res.Body);
                            if (doc.RootElement.TryGetProperty("messageId", out var midElem))
                            {
                                string msgId = midElem.GetInt32().ToString();
                                if (_bubbleRegistry.TryGetValue(bubbleId, out var entry))
                                {
                                    entry.row.Tag = msgId;
                                    _bubbleRegistry[bubbleId] = entry;
                                }
                            }
                        }
                        catch { /* non-critical */ }
                    }
                    // Delivered / Seen will now come from real SignalR MessageStatusChanged events
                }
                else
                {
                    MessageBox.Show("Failed to send message: " + res.Error, "Messaging Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Messaging Exception: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  DELIVERY STATUS HELPERS
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fire-and-forget: tells the backend this message was delivered to us.
        /// Called as soon as we receive a message via SignalR.
        /// </summary>
        private async Task AckDeliveredAsync(int messageId)
        {
            try
            {
                await ApiService.PostAsync($"messages/{messageId}/delivered", new { });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatOverlayPanel] AckDelivered failed for {messageId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when admin opens a conversation thread.
        /// Uses the new bulk /thread/{contactId}/seen endpoint to mark all messages
        /// from that contact as 'seen' in a single DB query.
        /// Backend fires SignalR MessageStatusChanged("seen") back to the sender.
        /// </summary>
        private async Task MarkConversationSeenAsync(string contactId)
        {
            try
            {
                await ApiService.PostAsync(
                    $"messages/thread/{contactId}/seen",
                    new { viewerId = "admin" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatOverlayPanel] MarkThreadSeen failed for {contactId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Displays a Windows system-tray balloon tip notification.
        /// The icon is made briefly visible, the tip is shown, then the icon
        /// hides itself again after the standard balloon timeout.
        /// </summary>
        private void ShowBalloonNotification(string title, string body)
        {
            try
            {
                if (_notifyIcon == null || this.IsDisposed) return;

                // Truncate long messages so the balloon doesn't overflow
                string tipText = body?.Length > 100 ? body.Substring(0, 97) + "..." : (body ?? "");

                _notifyIcon.Visible = true;
                _notifyIcon.ShowBalloonTip(
                    timeout : 4000,
                    tipTitle: title,
                    tipText : tipText,
                    tipIcon : ToolTipIcon.Info);

                // Auto-hide the icon after the balloon expires
                // (prevents persistent ghost icon in system tray)
                var hideTimer = new System.Windows.Forms.Timer { Interval = 5000 };
                hideTimer.Tick += (s, e) =>
                {
                    if (_notifyIcon != null)
                    {
                        try { _notifyIcon.Visible = false; } catch { }
                    }
                    hideTimer.Stop();
                    hideTimer.Dispose();
                };
                hideTimer.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatOverlayPanel] ShowBalloonNotification failed: {ex.Message}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
                try { _hubConnection?.DisposeAsync().AsTask().Wait(); } catch { }
            }
            base.Dispose(disposing);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════════
        private GraphicsPath RR(Rectangle r, int radius)
        {
            int d = radius * 2;
            var arc = new Rectangle(r.Location, new Size(d, d));
            var path = new GraphicsPath();
            path.AddArc(arc, 180, 90); arc.X = r.Right - d;
            path.AddArc(arc, 270, 90); arc.Y = r.Bottom - d;
            path.AddArc(arc, 0, 90);   arc.X = r.Left;
            path.AddArc(arc, 90, 90);  path.CloseFigure();
            return path;
        }

        private void SetRoundRegion(Control c, int r)
        {
            c.Region = new Region(RR(new Rectangle(0, 0, c.Width, c.Height), r));
        }

        private static void EnableDB(Control c)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, c, new object[] { true });
        }
        private async void ForwardMessageAction(string bubbleId)
        {
            using var dlg = new ForwardMessageDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedContactId != null)
            {
                int.TryParse(bubbleId, out int origId);
                var req = new { OriginalMessageId = origId, SenderId = "admin", NewReceiverId = dlg.SelectedContactId };
                await ApiService.PostAsync("messages/forward", req);
            }
        }

        private async void RemoveMessageAction(string bubbleId)
        {
            using var dlg = new RemoveConfirmationDialog();
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            var req = new { UserId = "admin" };
            var res = await ApiService.PostAsync($"messages/{bubbleId}/remove", req);
            if (res.Success)
            {
                if (_bubbleRegistry.ContainsKey(bubbleId))
                {
                    _flowMessages.Controls.Remove(_bubbleRegistry[bubbleId].row);
                    _bubbleRegistry.Remove(bubbleId);
                }
            }
        }

        private async void ReactMessageAction(string bubbleId)
        {
            // Open emoji picker via a small context menu
            var ctx = new ContextMenuStrip
            {
                ShowImageMargin = false,
                ShowCheckMargin = false,
                BackColor       = Color.FromArgb(36, 37, 38),
                ForeColor       = Color.FromArgb(228, 230, 235),
                Font            = new Font("Segoe UI Emoji", 13F),
                RenderMode      = ToolStripRenderMode.Professional,
                Renderer        = new MessengerMenuRenderer()
            };

            foreach (var emoji in new[] { "👍", "❤️", "😂", "😮", "😢", "😡" })
            {
                string captured = emoji;
                var item = new ToolStripMenuItem(captured)
                {
                    Font = new Font("Segoe UI Emoji", 14F)
                };
                item.Click += async (s, e) =>
                {
                    var req = new { UserId = "admin", Emoji = captured };
                    await ApiService.PostAsync($"messages/{bubbleId}/react", req);
                };
                ctx.Items.Add(item);
            }

            // Show inline below the flow panel
            if (_flowMessages.Controls.Count > 0)
                ctx.Show(_flowMessages, _flowMessages.PointToClient(Cursor.Position));
        }

        private async void UnsendMessageAction(string bubbleId)
        {
            await ApiService.DeleteAsync($"messages/{bubbleId}/unsend");
        }

        /// <summary>Opens the ReactionDetailsDialog for a given reactions JSON string.
        /// If the current user clicks their own reaction row the reaction is removed via the API.</summary>
        private async void ShowReactionDetails(string reactionsJson, string messageId)
        {
            var rxDict = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(reactionsJson) && reactionsJson != "{}")
                try { rxDict = JsonSerializer.Deserialize<Dictionary<string, string>>(reactionsJson); } catch {}

            if (rxDict.Count == 0) return;

            // "admin" is the logged-in user ID in this context
            const string currentUserId = "admin";

            using var dlg = new ReactionDetailsDialog(rxDict, currentUserId);
            dlg.ShowDialog(this);

            // ── Undo Reaction ─────────────────────────────────────────────────
            // If the user clicked their own reaction row, delete it via the API.
            // SignalR's MessageReactionChanged event will propagate the UI update automatically.
            if (dlg.RemoveMyReaction && !string.IsNullOrWhiteSpace(messageId))
            {
                try
                {
                    await ApiService.PostAsync($"messages/{messageId}/react", new { UserId = currentUserId, Emoji = "" });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ShowReactionDetails] Remove reaction error: {ex.Message}");
                }
            }
        }

        /// <summary>Helper: adds a styled item to a dark ContextMenuStrip.</summary>
        private static void AddMenuItem(ContextMenuStrip ctx, string text, Action action, bool isDestructive = false)
        {
            var item = new ToolStripMenuItem(text)
            {
                ForeColor = isDestructive ? Color.FromArgb(255, 107, 107) : Color.FromArgb(228, 230, 235),
                BackColor = Color.FromArgb(36, 37, 38),
                Font      = new Font("Segoe UI", 10F)
            };
            item.Click += (s, e) => action();
            ctx.Items.Add(item);
        }
    }  // end class ChatOverlayPanel

    // ══════════════════════════════════════════════════════════════════════════
    //  MESSENGER DARK CONTEXT MENU RENDERER
    //  Eliminates the default white Windows chrome on ContextMenuStrip.
    // ══════════════════════════════════════════════════════════════════════════
    internal sealed class MessengerMenuRenderer : ToolStripProfessionalRenderer
    {
        private static readonly Color BgColor      = Color.FromArgb(36, 37, 38);
        private static readonly Color HoverBg      = Color.FromArgb(58, 59, 60);
        private static readonly Color BorderColor  = Color.FromArgb(70, 255, 255, 255);
        private static readonly Color SeparatorCol = Color.FromArgb(50, 255, 255, 255);

        public MessengerMenuRenderer() : base(new MessengerColorTable()) { }

        // ── Whole drop-down background ────────────────────────────────────────
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using var path = RR(r, 10);
            g.FillPath(new SolidBrush(BgColor), path);
            g.DrawPath(new Pen(BorderColor, 1f), path);
        }

        // ── Drop-down border (suppress default) ──────────────────────────────
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { /* suppressed */ }

        // ── Image margin strip (left gutter) — hide it ────────────────────────
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { /* suppressed */ }

        // ── Item background + hover highlight ────────────────────────────────
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var r = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);

            if (e.Item.Selected)
            {
                using var path = RR(r, 8);
                g.FillPath(new SolidBrush(HoverBg), path);
            }
        }

        // ── Item text ─────────────────────────────────────────────────────────
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.ForeColor != Color.Empty ? e.Item.ForeColor : Color.FromArgb(228, 230, 235);
            e.TextFont  = e.Item.Font ?? new Font("Segoe UI", 10F);
            base.OnRenderItemText(e);
        }

        // ── Separator ─────────────────────────────────────────────────────────
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            e.Graphics.DrawLine(new Pen(SeparatorCol, 1f),
                8, e.Item.Height / 2,
                e.Item.Width - 8, e.Item.Height / 2);
        }

        // ── Arrow (sub-menu indicator) ────────────────────────────────────────
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Color.FromArgb(176, 179, 184);
            base.OnRenderArrow(e);
        }

        private static System.Drawing.Drawing2D.GraphicsPath RR(Rectangle r, int radius)
        {
            int d = radius * 2;
            var arc  = new Rectangle(r.Location, new Size(d, d));
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(arc, 180, 90); arc.X = r.Right - d;
            path.AddArc(arc, 270, 90); arc.Y = r.Bottom - d;
            path.AddArc(arc, 0,   90); arc.X = r.Left;
            path.AddArc(arc, 90,  90); path.CloseFigure();
            return path;
        }
    }

    internal sealed class MessengerColorTable : ProfessionalColorTable
    {
        private static readonly Color Bg     = Color.FromArgb(36, 37, 38);
        private static readonly Color Hover  = Color.FromArgb(58, 59, 60);
        private static readonly Color Border = Color.FromArgb(70, 255, 255, 255);

        public override Color MenuItemSelected           => Hover;
        public override Color MenuItemBorder             => Border;
        public override Color MenuBorder                 => Border;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd   => Hover;
        public override Color MenuItemPressedGradientBegin  => Hover;
        public override Color MenuItemPressedGradientEnd    => Hover;
        public override Color ToolStripDropDownBackground   => Bg;
        public override Color ImageMarginGradientBegin      => Bg;
        public override Color ImageMarginGradientMiddle     => Bg;
        public override Color ImageMarginGradientEnd        => Bg;
        public override Color SeparatorDark                 => Color.FromArgb(50, 255, 255, 255);
        public override Color SeparatorLight                => Color.FromArgb(50, 255, 255, 255);
    }

    internal class DarkScrollFlowLayoutPanel : FlowLayoutPanel
    {
        private bool _isHovered = false;
        private bool _isDragging = false;
        private int  _dragStartY = 0;
        private int  _dragStartScrollY = 0;

        public DarkScrollFlowLayoutPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer |
                           ControlStyles.AllPaintingInWmPaint |
                           ControlStyles.UserPaint, true);
            this.UpdateStyles();

            this.MouseEnter += (s, e) => { _isHovered = true;  this.Invalidate(); };
            this.MouseLeave += (s, e) => { _isHovered = false; _isDragging = false; this.Invalidate(); };
            this.MouseDown  += OnThumbMouseDown;
            this.MouseMove  += OnThumbMouseMove;
            this.MouseUp    += (s, e) => { _isDragging = false; };
            this.Scroll     += (s, e) => this.Invalidate();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x000F || m.Msg == 0x0085 || m.Msg == 0x0005 || m.Msg == 0x0115 || m.Msg == 0x0114)
            {
                if (this.IsHandleCreated)
                {
                    ShowScrollBar(this.Handle, 1, false);
                    ShowScrollBar(this.Handle, 0, false);
                }
            }
        }

        private Rectangle GetThumbRectangle()
        {
            int clientH  = this.ClientSize.Height;
            int displayH = this.DisplayRectangle.Height;
            if (displayH <= clientH || clientH <= 0) return Rectangle.Empty;

            int thumbH   = Math.Max(28, (clientH * clientH) / displayH);
            int scrollY  = -this.AutoScrollPosition.Y;
            int maxScrollY = displayH - clientH;
            int thumbY   = (maxScrollY > 0) ? (scrollY * (clientH - thumbH)) / maxScrollY : 0;

            return new Rectangle(this.Width - 8, thumbY + 3, 5, thumbH - 6);
        }

        private void OnThumbMouseDown(object sender, MouseEventArgs e)
        {
            var thumb = GetThumbRectangle();
            if (thumb != Rectangle.Empty && e.X >= this.Width - 14)
            {
                _isDragging = true;
                _dragStartY = e.Y;
                _dragStartScrollY = -this.AutoScrollPosition.Y;
            }
        }

        private void OnThumbMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                int clientH  = this.ClientSize.Height;
                int displayH = this.DisplayRectangle.Height;
                int maxScrollY = displayH - clientH;
                int thumbH   = Math.Max(28, (clientH * clientH) / displayH);
                int trackH   = clientH - thumbH;

                if (trackH > 0)
                {
                    int deltaY = e.Y - _dragStartY;
                    int newScrollY = _dragStartScrollY + (deltaY * maxScrollY) / trackH;
                    newScrollY = Math.Max(0, Math.Min(maxScrollY, newScrollY));
                    this.AutoScrollPosition = new Point(0, newScrollY);
                    this.Invalidate();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var thumb = GetThumbRectangle();
            if (thumb != Rectangle.Empty)
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                Color thumbColor = (_isDragging || _isHovered)
                    ? Color.FromArgb(200, ThemeManager.CurrentPrimary)
                    : Color.FromArgb(65, 255, 255, 255);

                using var brush = new SolidBrush(thumbColor);
                int r = 2;
                int d = r * 2;
                var path = new System.Drawing.Drawing2D.GraphicsPath();
                var arc = new Rectangle(thumb.Location, new Size(d, d));
                path.AddArc(arc, 180, 90); arc.X = thumb.Right - d;
                path.AddArc(arc, 270, 90); arc.Y = thumb.Bottom - d;
                path.AddArc(arc, 0,   90); arc.X = thumb.Left;
                path.AddArc(arc, 90,  90); path.CloseFigure();

                g.FillPath(brush, path);
            }
        }
    }

    internal class DarkScrollPanel : Panel
    {
        private bool _isHovered = false;
        private bool _isDragging = false;
        private int  _dragStartY = 0;
        private int  _dragStartScrollY = 0;

        public DarkScrollPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer |
                           ControlStyles.AllPaintingInWmPaint |
                           ControlStyles.UserPaint, true);
            this.UpdateStyles();

            this.MouseEnter += (s, e) => { _isHovered = true;  this.Invalidate(); };
            this.MouseLeave += (s, e) => { _isHovered = false; _isDragging = false; this.Invalidate(); };
            this.MouseDown  += OnThumbMouseDown;
            this.MouseMove  += OnThumbMouseMove;
            this.MouseUp    += (s, e) => { _isDragging = false; };
            this.Scroll     += (s, e) => this.Invalidate();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x000F || m.Msg == 0x0085 || m.Msg == 0x0005 || m.Msg == 0x0115 || m.Msg == 0x0114)
            {
                if (this.IsHandleCreated)
                {
                    ShowScrollBar(this.Handle, 1, false);
                    ShowScrollBar(this.Handle, 0, false);
                }
            }
        }

        private Rectangle GetThumbRectangle()
        {
            int clientH  = this.ClientSize.Height;
            int displayH = this.DisplayRectangle.Height;
            if (displayH <= clientH || clientH <= 0) return Rectangle.Empty;

            int thumbH   = Math.Max(28, (clientH * clientH) / displayH);
            int scrollY  = -this.AutoScrollPosition.Y;
            int maxScrollY = displayH - clientH;
            int thumbY   = (maxScrollY > 0) ? (scrollY * (clientH - thumbH)) / maxScrollY : 0;

            return new Rectangle(this.Width - 8, thumbY + 3, 5, thumbH - 6);
        }

        private void OnThumbMouseDown(object sender, MouseEventArgs e)
        {
            var thumb = GetThumbRectangle();
            if (thumb != Rectangle.Empty && e.X >= this.Width - 14)
            {
                _isDragging = true;
                _dragStartY = e.Y;
                _dragStartScrollY = -this.AutoScrollPosition.Y;
            }
        }

        private void OnThumbMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                int clientH  = this.ClientSize.Height;
                int displayH = this.DisplayRectangle.Height;
                int maxScrollY = displayH - clientH;
                int thumbH   = Math.Max(28, (clientH * clientH) / displayH);
                int trackH   = clientH - thumbH;

                if (trackH > 0)
                {
                    int deltaY = e.Y - _dragStartY;
                    int newScrollY = _dragStartScrollY + (deltaY * maxScrollY) / trackH;
                    newScrollY = Math.Max(0, Math.Min(maxScrollY, newScrollY));
                    this.AutoScrollPosition = new Point(0, newScrollY);
                    this.Invalidate();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var thumb = GetThumbRectangle();
            if (thumb != Rectangle.Empty)
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                Color thumbColor = (_isDragging || _isHovered)
                    ? Color.FromArgb(200, ThemeManager.CurrentPrimary)
                    : Color.FromArgb(65, 255, 255, 255);

                using var brush = new SolidBrush(thumbColor);
                int r = 2;
                int d = r * 2;
                var path = new System.Drawing.Drawing2D.GraphicsPath();
                var arc = new Rectangle(thumb.Location, new Size(d, d));
                path.AddArc(arc, 180, 90); arc.X = thumb.Right - d;
                path.AddArc(arc, 270, 90); arc.Y = thumb.Bottom - d;
                path.AddArc(arc, 0,   90); arc.X = thumb.Left;
                path.AddArc(arc, 90,  90); path.CloseFigure();

                g.FillPath(brush, path);
            }
        }
    }

}  // end namespace DriveAndGo_Admin
