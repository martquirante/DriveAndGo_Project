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
        private RichTextBox _txtInput;
        private Label       _lblInputPlaceholder;
        private bool        _isFormattingInputMention = false;
        private Button      _btnSend;

        private Panel   _pnlLinkPreview;
        private PictureBox _pbLinkPreviewThumb;
        private Label   _lblLinkPreviewTitle;
        private Label   _lblLinkPreviewDesc;
        private Button  _btnDismissLinkPreview;
        private System.Windows.Forms.Timer _linkDebounceTimer;
        private bool    _isLinkPreviewDismissed = false;
        private string  _lastPreviewUrl = "";

        private Panel   _pnlMentionPopup;
        private Label   _lblMentionItem;

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
        private readonly HashSet<string> _renderedAiMessageKeys = new HashSet<string>();

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

                _webView.CoreWebView2.WebMessageReceived += (s, e) =>
                {
                    try
                    {
                        string rawJson = e.TryGetWebMessageAsString();
                        if (string.IsNullOrWhiteSpace(rawJson)) rawJson = e.WebMessageAsJson;
                        if (string.IsNullOrWhiteSpace(rawJson)) return;

                        using var doc = System.Text.Json.JsonDocument.Parse(rawJson);
                        if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            using var innerDoc = System.Text.Json.JsonDocument.Parse(doc.RootElement.GetString()!);
                            if (innerDoc.RootElement.TryGetProperty("action", out var act) && act.GetString() == "TOGGLE_FULLSCREEN")
                            {
                                bool isFull = innerDoc.RootElement.TryGetProperty("enabled", out var en) && en.GetBoolean();
                                this.BeginInvoke(new Action(() => OnToggleFullscreenRequested?.Invoke(isFull)));
                            }
                        }
                        else if (doc.RootElement.TryGetProperty("action", out var act) && act.GetString() == "TOGGLE_FULLSCREEN")
                        {
                            bool isFull = doc.RootElement.TryGetProperty("enabled", out var en) && en.GetBoolean();
                            this.BeginInvoke(new Action(() => OnToggleFullscreenRequested?.Invoke(isFull)));
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ChatOverlayPanel] WebMessageReceived parse error: {ex.Message}");
                    }
                };

                _webView.CoreWebView2.NewWindowRequested += (s, e) =>
                {
                    e.Handled = true;
                    if (!string.IsNullOrWhiteSpace(e.Uri))
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri) { UseShellExecute = true });
                        }
                        catch { }
                    }
                };

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

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Action<bool> OnToggleFullscreenRequested { get; set; }

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

                string rawName = _lblConvName?.Text;
                string init = _activeConvIsGroup ? "G"
                    : (!string.IsNullOrWhiteSpace(rawName) && rawName != "Select a conversation"
                        ? rawName.Trim()[0].ToString().ToUpper() : "D");
                if (string.IsNullOrEmpty(init)) init = "D";

                using var font = new Font("Segoe UI Emoji", 14F, FontStyle.Bold);
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
            inputBar.Height = 62;
            inputBar.Dock   = DockStyle.Bottom;
            inputBar.BackColor = ThemeManager.CurrentSidebar;
            inputBar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillRectangle(new SolidBrush(ThemeManager.CurrentSidebar), inputBar.ClientRectangle);

                var inputR = new Rectangle(14, 8, inputBar.Width - 76, inputBar.Height - 16);
                int radius = Math.Min(20, inputR.Height / 2);
                using var path = RR(inputR, radius);
                g.FillPath(new SolidBrush(ThemeManager.CurrentInputBg), path);
                g.DrawPath(new Pen(ThemeManager.CurrentBorder, 1f), path);
            };

            // Vector Media Button 🖼 (Sharp GDI+ Vector Paint)
            var btnMedia = new Button
            {
                Size = new Size(32, 32),
                Location = new Point(22, 15),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btnMedia.FlatAppearance.BorderSize = 0;
            btnMedia.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                bool hover = btnMedia.ClientRectangle.Contains(btnMedia.PointToClient(Cursor.Position));
                using var pen = new Pen(hover ? ThemeManager.CurrentPrimary : Color.FromArgb(148, 163, 184), 1.8f);

                // Draw Vector Frame (rounded box)
                var rect = new Rectangle(5, 6, 21, 17);
                using var path = RR(rect, 3);
                g.DrawPath(pen, path);
                // Sun dot
                g.DrawEllipse(pen, 9, 9, 3, 3);
                // Mountain slope lines
                g.DrawLine(pen, 7, 21, 12, 14);
                g.DrawLine(pen, 12, 14, 16, 21);
                g.DrawLine(pen, 15, 21, 18, 17);
                g.DrawLine(pen, 18, 17, 22, 21);
            };
            btnMedia.MouseEnter += (s, e) => btnMedia.Invalidate();
            btnMedia.MouseLeave += (s, e) => btnMedia.Invalidate();
            btnMedia.Click += (s, e) =>
            {
                using var ofd = new OpenFileDialog
                {
                    Title = "Select Photo or Video to Send",
                    Filter = "Media Files (*.jpg;*.png;*.mp4;*.webm)|*.jpg;*.jpeg;*.png;*.gif;*.mp4;*.webm"
                };
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    UploadAndSendMedia(ofd.FileName);
                }
            };
            inputBar.Controls.Add(btnMedia);

            // Vector Voice Note Button 🎤 (Sharp GDI+ Vector Paint)
            var btnMic = new Button
            {
                Size = new Size(32, 32),
                Location = new Point(58, 15),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btnMic.FlatAppearance.BorderSize = 0;
            btnMic.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                bool hover = btnMic.ClientRectangle.Contains(btnMic.PointToClient(Cursor.Position));
                using var pen = new Pen(hover ? ThemeManager.CurrentPrimary : Color.FromArgb(148, 163, 184), 1.8f);

                // Mic capsule
                var capsule = new Rectangle(12, 5, 8, 13);
                using var capPath = RR(capsule, 4);
                g.DrawPath(pen, capPath);
                // Arch cup
                g.DrawArc(pen, 8, 9, 16, 12, 0, 180);
                // Stand & Base
                g.DrawLine(pen, 16, 21, 16, 26);
                g.DrawLine(pen, 11, 26, 21, 26);
            };
            btnMic.MouseEnter += (s, e) => btnMic.Invalidate();
            btnMic.MouseLeave += (s, e) => btnMic.Invalidate();
            btnMic.Click += (s, e) => StartVoiceRecordingBar(inputBar);
            inputBar.Controls.Add(btnMic);

            _txtInput = new RichTextBox
            {
                Multiline       = true,
                AcceptsTab      = false,
                BorderStyle     = BorderStyle.None,
                BackColor       = ThemeManager.CurrentInputBg,
                ForeColor       = ThemeManager.CurrentText,
                Font            = new Font("Segoe UI", 10.5F),
                ScrollBars      = RichTextBoxScrollBars.None,
                WordWrap        = true,
                DetectUrls      = false,
                Size            = new Size(inputBar.Width - 162, 28),
                Location        = new Point(96, 14),
                Anchor          = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
            };

            _lblInputPlaceholder = new Label
            {
                Text      = "Type a message...",
                ForeColor = ThemeManager.CurrentSubText,
                Font      = new Font("Segoe UI", 10.5F),
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(98, 14),
                Cursor    = Cursors.IBeam
            };
            _lblInputPlaceholder.Click += (s, e) => _txtInput.Focus();
            inputBar.Controls.Add(_lblInputPlaceholder);

            _linkDebounceTimer = new System.Windows.Forms.Timer { Interval = 300 };
            _linkDebounceTimer.Tick += (s, e) =>
            {
                _linkDebounceTimer.Stop();
                CheckForInputLinkPreview();
            };

            _txtInput.TextChanged += (s, e) =>
            {
                _lblInputPlaceholder.Visible = string.IsNullOrEmpty(_txtInput.Text);
                _linkDebounceTimer.Stop();
                _linkDebounceTimer.Start();
                CheckForMentionPopup();
                HighlightInputMentions();

                // Calculate required text height dynamically
                using (var g = _txtInput.CreateGraphics())
                {
                    SizeF sz = g.MeasureString(_txtInput.Text + " ", _txtInput.Font, _txtInput.Width);
                    int reqTxtH = Math.Min(80, Math.Max(28, (int)sz.Height + 4));
                    if (_txtInput.Height != reqTxtH)
                    {
                        _txtInput.Height = reqTxtH;
                        inputBar.Height  = reqTxtH + 28;
                        if (_btnSend != null)
                            _btnSend.Location = new Point(inputBar.Width - 54, (inputBar.Height - 40) / 2);
                        inputBar.Invalidate();
                    }
                }
            };
            _txtInput.KeyDown += (s, e) =>
            {
                if ((e.KeyCode == Keys.Tab || e.KeyCode == Keys.Enter) && _pnlMentionPopup != null && _pnlMentionPopup.Visible)
                {
                    e.SuppressKeyPress = true;
                    ApplyMentionAutocomplete();
                    return;
                }
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true;
                    SendMessage();
                    _txtInput.Height = 28;
                    inputBar.Height  = 62;
                    if (_btnSend != null)
                        _btnSend.Location = new Point(inputBar.Width - 54, 11);
                    inputBar.Invalidate();
                }
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

            // ── Mention Autocomplete Popup Panel ──
            _pnlMentionPopup = new Panel
            {
                Height    = 42,
                Dock      = DockStyle.Bottom,
                BackColor = Color.FromArgb(30, 41, 59),
                Visible   = false,
                Padding   = new Padding(8, 6, 8, 6),
                Cursor    = Cursors.Hand
            };
            EnableDB(_pnlMentionPopup);

            _lblMentionItem = new Label
            {
                Text      = "✨  @Drive&Go AI   (In-Chat Assistant)",
                Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor    = Cursors.Hand
            };
            _pnlMentionPopup.Controls.Add(_lblMentionItem);
            _pnlMentionPopup.Click += (s, e) => ApplyMentionAutocomplete();
            _lblMentionItem.Click  += (s, e) => ApplyMentionAutocomplete();
            _rightPane.Controls.Add(_pnlMentionPopup);

            // ── Live Link Preview Header Bar (positioned directly above inputBar) ──
            _pnlLinkPreview = new Panel
            {
                Height    = 56,
                Dock      = DockStyle.Bottom,
                BackColor = Color.FromArgb(28, 30, 46),
                Visible   = false,
                Padding   = new Padding(8)
            };
            EnableDB(_pnlLinkPreview);

            _pbLinkPreviewThumb = new PictureBox
            {
                Size      = new Size(42, 42),
                Location  = new Point(8, 7),
                SizeMode  = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(15, 16, 26)
            };
            _pnlLinkPreview.Controls.Add(_pbLinkPreviewThumb);

            _lblLinkPreviewTitle = new Label
            {
                Font         = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor    = Color.FromArgb(241, 245, 249),
                Location     = new Point(56, 6),
                Size         = new Size(inputBar.Width - 110, 18),
                Anchor       = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                AutoEllipsis = true
            };
            _pnlLinkPreview.Controls.Add(_lblLinkPreviewTitle);

            _lblLinkPreviewDesc = new Label
            {
                Font         = new Font("Segoe UI", 8F),
                ForeColor    = Color.FromArgb(148, 163, 184),
                Location     = new Point(56, 26),
                Size         = new Size(inputBar.Width - 110, 16),
                Anchor       = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                AutoEllipsis = true
            };
            _pnlLinkPreview.Controls.Add(_lblLinkPreviewDesc);

            _btnDismissLinkPreview = new Button
            {
                Text      = "✕",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(24, 24),
                Location  = new Point(_pnlLinkPreview.Width - 30, 6),
                Anchor    = AnchorStyles.Right | AnchorStyles.Top,
                Cursor    = Cursors.Hand
            };
            _btnDismissLinkPreview.FlatAppearance.BorderSize = 0;
            _btnDismissLinkPreview.Click += (s, e) =>
            {
                _isLinkPreviewDismissed = true;
                _pnlLinkPreview.Visible = false;
            };
            _pnlLinkPreview.Controls.Add(_btnDismissLinkPreview);

            _rightPane.Controls.Add(_pnlLinkPreview);
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
                               || (_activeConvIsGroup && receiverId == _activeConvId)
                               || (senderId == "@Drive&Go AI" && receiverId == _activeConvId)))
                        {
                            if (senderId == "@Drive&Go AI")
                            {
                                TryAddAiMessageOnce(body, messageId, dt);
                            }
                            else
                            {
                                AddMessage(body, false, dt, MessageDeliveryState.Delivered);
                                if (_flowMessages.Controls.Count > 0)
                                    _flowMessages.ScrollControlIntoView(_flowMessages.Controls[_flowMessages.Controls.Count - 1]);
                                _ = MarkConversationSeenAsync(senderId);
                            }
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

                        // Read media fields (try both camelCase and PascalCase)
                        string mType = item.TryGetProperty("mediaType",  out var mtP) ? mtP.GetString()
                                     : item.TryGetProperty("MediaType",  out var mtP2) ? mtP2.GetString() : null;
                        string mUrl  = item.TryGetProperty("mediaUrl",   out var muP) ? muP.GetString()
                                     : item.TryGetProperty("MediaUrl",   out var muP2) ? muP2.GetString() : null;

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
                        bool isUnsent  = item.TryGetProperty("isUnsent",  out var uProp2) && uProp2.GetBoolean();
                        string rx      = item.TryGetProperty("reactions",  out var rProp) ? (rProp.GetString() ?? "{}") : "{}";

                        AddMessage(body, isMine, ts, historyState, bId, isUnsent, isEdited, rx, mType, mUrl);
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

                string safeName = string.IsNullOrWhiteSpace(conv.Name) ? "Chat" : conv.Name.Trim();
                string safeRole = string.IsNullOrWhiteSpace(conv.Role) ? "User" : conv.Role.Trim();
                string init     = ExtractInitialLetter(safeName, conv.IsGroup);

                using var initFont = new Font("Segoe UI Emoji", 13F, FontStyle.Bold);
                using var fmt      = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(init, initFont, Brushes.White, new RectangleF(av.X, av.Y, av.Width, av.Height), fmt);

                // ── Name ───────────
                using var nameFont = new Font("Segoe UI Emoji", 10F, FontStyle.Bold);
                using var nameFmt  = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                
                string formattedTime = FormatDisplayTime(conv.Time);
                float rightPadding = !string.IsNullOrWhiteSpace(formattedTime) ? 75f : 14f;
                var nameRect = new RectangleF(68, 12, Math.Max(40, card.Width - 68 - rightPadding), 18);
                Color nameColor = hasUnread ? Color.FromArgb(255, 255, 255) : ThemeManager.CurrentText;
                g.DrawString(safeName, nameFont, new SolidBrush(nameColor), nameRect, nameFmt);

                using var roleFont = new Font("Segoe UI Emoji", 7.5F);
                var roleText = "[" + safeRole.ToUpper() + "]";
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

                using (var font = new Font("Segoe UI Emoji", 32F, FontStyle.Bold))
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

                string safeName = string.IsNullOrWhiteSpace(conv.Name) ? "Chat" : conv.Name.Trim();
                string safeRole = string.IsNullOrWhiteSpace(conv.Role) ? "User" : conv.Role.Trim();
                string init     = ExtractInitialLetter(safeName, conv.IsGroup);

                using (var initFont = new Font("Segoe UI Emoji", 22F, FontStyle.Bold))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(init, initFont, Brushes.White, new RectangleF(avRect.X, avRect.Y, avRect.Width, avRect.Height), fmt);
                }

                using (var nameFont = new Font("Segoe UI Emoji", 12.5F, FontStyle.Bold))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center })
                {
                    g.DrawString(safeName, nameFont, new SolidBrush(ThemeManager.CurrentText), new PointF(cx, 88), fmt);
                }

                using (var subFont = new Font("Segoe UI Emoji", 9F))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center })
                {
                    g.DrawString($"{safeRole.ToUpper()} • DriveAndGo Network", subFont, new SolidBrush(roleColor), new PointF(cx, 115), fmt);
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
        private void AddMessage(string text, bool isMine, DateTime time, MessageDeliveryState state = MessageDeliveryState.Delivered, string bubbleId = null, bool isUnsent = false, bool isEdited = false, string reactionsJson = "{}", string mediaType = null, string mediaUrl = null, string mediaMetadata = null)
        {
            // ── Horizontal inset: keeps bubbles away from the absolute container edges ──
            const int hInset = 20;  // pixels of breathing room on each side
            int maxW = Math.Max(200, _flowMessages.ClientSize.Width - 120);
            int padH = 12, padV = 9;

            if (string.IsNullOrWhiteSpace(text)) text = " ";

            if (isUnsent)
                text = isMine ? "You unsent a message" : "This message was unsent";

            // ── Media bubble rendering (image / video / audio) ──────────────────
            bool hasMedia = (!string.IsNullOrEmpty(mediaType) || !string.IsNullOrEmpty(mediaUrl))
                            && mediaType != "file";
            if (hasMedia)
            {
                AddMediaMessage(text, isMine, time, state, bubbleId, mediaType, mediaUrl, mediaMetadata);
                return;
            }

            SizeF sz;
            using (var g = this.CreateGraphics())
            using (var font = isUnsent ? new Font("Segoe UI Emoji", 10.5F, FontStyle.Italic) : new Font("Segoe UI Emoji", 10.5F))
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
                    AddMenuItem(ctx, "↪  Forward",          ()=> ForwardMessageAction(bubbleId));
                    if (isMine && !hasMedia)
                        AddMenuItem(ctx, "✏️  Edit text",     ()=> EditMessageAction(bubbleId, text));
                    AddMenuItem(ctx, "🗑  Remove for you", ()=> RemoveMessageAction(bubbleId));
                    AddMenuItem(ctx, "😊  React",           ()=> ReactMessageAction(bubbleId));
                    if (isMine)
                        AddMenuItem(ctx, "↩  Unsend",       ()=> UnsendMessageAction(bubbleId), isDestructive: true);
                }
                return ctx;
            }

            var stateHolder = new[] { state };
            if (bubbleId != null)
                _bubbleRegistry[bubbleId] = (row, stateHolder);

            // ── Link Preview Rich Card Attachment ────────────────────────────
            var urlMatch = System.Text.RegularExpressions.Regex.Match(text, @"(https?://[^\s]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (urlMatch.Success && !isUnsent)
            {
                string foundUrl = urlMatch.Groups[1].Value;
                int cardW = Math.Min(320, row.Width - 40);
                int cardH = 210;

                var pnlCard = new Panel
                {
                    Size      = new Size(cardW, cardH),
                    Location  = new Point(isMine ? row.Width - cardW - hInset : hInset, bubbleH + 6),
                    BackColor = Color.FromArgb(28, 30, 46),
                    Cursor    = Cursors.Hand,
                    Visible   = false
                };
                EnableDB(pnlCard);
                pnlCard.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, cardW, cardH, 14, 14));

                var pbCover = new PictureBox
                {
                    Size      = new Size(cardW, 130),
                    Location  = new Point(0, 0),
                    SizeMode  = PictureBoxSizeMode.Zoom,
                    BackColor = Color.FromArgb(15, 16, 26)
                };
                pnlCard.Controls.Add(pbCover);

                var lblTitle = new Label
                {
                    Font         = new Font("Segoe UI", 9F, FontStyle.Bold),
                    ForeColor    = Color.FromArgb(241, 245, 249),
                    Location     = new Point(10, 134),
                    Size         = new Size(cardW - 20, 20),
                    AutoEllipsis = true
                };
                pnlCard.Controls.Add(lblTitle);

                var lblDesc = new Label
                {
                    Font         = new Font("Segoe UI", 8F),
                    ForeColor    = Color.FromArgb(148, 163, 184),
                    Location     = new Point(10, 156),
                    Size         = new Size(cardW - 20, 30),
                    AutoEllipsis = true
                };
                pnlCard.Controls.Add(lblDesc);

                var lblDomain = new Label
                {
                    Font         = new Font("Segoe UI", 7.5F),
                    ForeColor    = Color.FromArgb(56, 189, 248),
                    Location     = new Point(10, 188),
                    Size         = new Size(cardW - 20, 16),
                    AutoEllipsis = true
                };
                pnlCard.Controls.Add(lblDomain);

                Action openUrl = () =>
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(foundUrl) { UseShellExecute = true });
                    }
                    catch { }
                };

                pnlCard.Click   += (s, e) => openUrl();
                pbCover.Click   += (s, e) => openUrl();
                lblTitle.Click  += (s, e) => openUrl();
                lblDesc.Click   += (s, e) => openUrl();
                lblDomain.Click += (s, e) => openUrl();

                row.Controls.Add(pnlCard);

                // Fetch metadata asynchronously
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var res = await ApiService.GetAsync($"media/link-preview?url={Uri.EscapeDataString(foundUrl)}");
                        if (res.Success && !string.IsNullOrEmpty(res.Body) && !this.IsDisposed)
                        {
                            var linkDto = System.Text.Json.JsonSerializer.Deserialize<LinkPreviewDto>(res.Body,
                                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                            if (linkDto != null)
                            {
                                this.BeginInvoke((Action)(() =>
                                {
                                    if (this.IsDisposed || pnlCard.IsDisposed) return;
                                    lblTitle.Text  = !string.IsNullOrEmpty(linkDto.Title) ? linkDto.Title : linkDto.Domain;
                                    lblDesc.Text   = !string.IsNullOrEmpty(linkDto.Description) ? linkDto.Description : (!string.IsNullOrEmpty(linkDto.SiteName) ? linkDto.SiteName : linkDto.Domain);
                                    lblDomain.Text = linkDto.Domain;

                                    if (!string.IsNullOrEmpty(linkDto.Image))
                                    {
                                        try { pbCover.LoadAsync(linkDto.Image); } catch { }
                                    }

                                    row.Height += cardH + 4;
                                    pnlCard.Visible = true;
                                    pnlCard.BringToFront();
                                }));
                            }
                        }
                    }
                    catch { }
                });
            }

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
                    return;
                }
                if (urlMatch.Success && !isUnsent)
                {
                    int bx = isMine ? row.Width - bubbleW - hInset : hInset;
                    var br = new Rectangle(bx, 4, bubbleW, bubbleH);
                    if (br.Contains(e.Location) || br.Contains(mousePt))
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(urlMatch.Groups[1].Value) { UseShellExecute = true });
                            return;
                        }
                        catch { }
                    }
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
                    DrawMessageTextWithMentions(g, text, font,
                        new RectangleF(bx + padH, by + padV, bubbleW - padH * 2, bubbleH),
                        isMine: false, isUnsent: true);
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
                    using var font = new Font("Segoe UI Emoji", 10.5F);
                    DrawMessageTextWithMentions(g, text, font,
                        new RectangleF(bx + padH, by + padV, bubbleW - padH * 2, bubbleH),
                        isMine: true, isUnsent: false);
                }
                else
                {
                    using var path = RR(br, 14);
                    using var bg   = new SolidBrush(ThemeManager.CurrentCard);
                    using var pen  = (bubbleId != null && bubbleId.StartsWith("ai_")) 
                                     ? new Pen(Color.FromArgb(56, 189, 248), 1.5f) 
                                     : new Pen(ThemeManager.CurrentBorder, 1f);
                    g.FillPath(bg, path);
                    g.DrawPath(pen, path);
                    using var font = new Font("Segoe UI Emoji", 10.5F);
                    DrawMessageTextWithMentions(g, text, font,
                        new RectangleF(bx + padH, by + padV, bubbleW - padH * 2, bubbleH),
                        isMine: false, isUnsent: false);
                }

                // Show Drive&Go AI sender tag above bubble ONLY for AI responses
                if (bubbleId != null && bubbleId.StartsWith("ai_") && by >= 16)
                {
                    using var mfont = new Font("Segoe UI", 7.5F, FontStyle.Bold);
                    using var mbrush = new SolidBrush(Color.FromArgb(56, 189, 248)); // Bright Cyan / Messenger Blue
                    g.DrawString("✨ Drive&Go AI", mfont, mbrush, new PointF(bx + 4, by - 14));
                }

                if (isEdited && !isUnsent)
                {
                    using var efont = new Font("Segoe UI", 7.5F, FontStyle.Bold);
                    using var ebrush = new SolidBrush(Color.FromArgb(56, 189, 248)); // Messenger Blue
                    // Draw "Edited" above the top of the bubble
                    float editX = isMine ? bx + bubbleW - 42 : bx + 4;
                    g.DrawString("Edited", efont, ebrush, new PointF(editX, Math.Max(2, by - 14)));
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

        /// <summary>
        /// Renders an image, video, or audio message as a real visual bubble
        /// (PictureBox for images, process-launch for video, HTML5 audio player for audio).
        /// </summary>
        private void AddMediaMessage(string caption, bool isMine, DateTime time, MessageDeliveryState state, string bubbleId, string mediaType, string mediaUrl, string mediaMetadata = null)
        {
            // Static files are served from the server ROOT (no /api prefix)
            // e.g.  ApiService.BaseUrl = "http://localhost:5233/api"
            //  -->  serverRoot           = "http://localhost:5233"
            string serverRoot = ApiService.BaseUrl.TrimEnd('/')
                                    .Replace("/api", "", StringComparison.OrdinalIgnoreCase)
                                    .TrimEnd('/');
            string fullUrl = (mediaUrl ?? "").StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? mediaUrl
                : serverRoot + mediaUrl;

            // ── Row container ─────────────────────────────────────────────────
            int rowH   = mediaType == "image" ? 220 : mediaType == "video" ? 190 : 62;
            int bubbleW = Math.Min(260, _flowMessages.ClientSize.Width - 80);

            var row = new Panel
            {
                Width     = _flowMessages.ClientSize.Width - 24,
                Height    = rowH + 28,
                BackColor = Color.Transparent,
                Margin    = new Padding(0, 4, 0, 4)
            };
            EnableDB(row);

            int bx = isMine ? (row.Width - bubbleW - 10) : 10;

            // ── Wrapper with rounded region ───────────────────────────────────
            var wrapper = new Panel
            {
                Size      = new Size(bubbleW, rowH),
                Location  = new Point(bx, 2),
                BackColor = Color.FromArgb(25, 25, 30)
            };
            wrapper.Region = System.Drawing.Region.FromHrgn(
                CreateRoundRectRgn(0, 0, wrapper.Width, wrapper.Height, 16, 16));
            EnableDB(wrapper);

            // ─────────────────────────────────────────────────────────────────
            if (mediaType == "image")
            {
                var pb = new PictureBox
                {
                    Dock      = DockStyle.Fill,
                    SizeMode  = PictureBoxSizeMode.Zoom,
                    BackColor = Color.FromArgb(20, 20, 26),
                    Cursor    = Cursors.Hand
                };

                // Download image bytes on background thread then marshal back to UI thread
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                        var bytes = await client.GetByteArrayAsync(fullUrl);
                        using var ms = new MemoryStream(bytes);
                        var bmp = new Bitmap(ms);
                        if (!pb.IsDisposed)
                            pb.BeginInvoke((Action)(() =>
                            {
                                if (!pb.IsDisposed) pb.Image = bmp;
                            }));
                    }
                    catch { /* show broken image silently */ }
                });

                pb.Click += (s, e) => ShowMediaFullscreen(fullUrl);
                wrapper.Controls.Add(pb);
            }
            else if (mediaType == "video")
            {
                var pnl = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(12, 12, 18) };
                EnableDB(pnl);

                pnl.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.FromArgb(18, 18, 24));

                    int cx = pnl.Width / 2, cy = pnl.Height / 2;
                    // Orange glow circle
                    using var circleBrush = new SolidBrush(Color.FromArgb(200, 234, 88, 12));
                    g.FillEllipse(circleBrush, cx - 30, cy - 30, 60, 60);
                    // White play triangle
                    using var playBrush = new SolidBrush(Color.White);
                    g.FillPolygon(playBrush, new PointF[] { new(cx - 10, cy - 16), new(cx + 18, cy), new(cx - 10, cy + 16) });
                    // Label
                    using var font = new Font("Segoe UI", 9F);
                    using var textBrush = new SolidBrush(Color.FromArgb(160, 255, 255, 255));
                    string lbl = "📹 Video — click to play";
                    var sz = g.MeasureString(lbl, font);
                    g.DrawString(lbl, font, textBrush, (pnl.Width - sz.Width) / 2f, pnl.Height - 24f);
                };
                pnl.Click += (s, e) => ShowMediaFullscreen(fullUrl);
                pnl.Cursor = Cursors.Hand;
                wrapper.Controls.Add(pnl);
            }
            else if (mediaType == "audio")
            {
                rowH = 50;
                row.Height = 78;
                wrapper.Size      = new Size(bubbleW, 50);
                wrapper.BackColor = isMine ? Color.FromArgb(234, 88, 12) : Color.FromArgb(30, 41, 59); // Drive&Go Orange vs Dark Slate
                wrapper.Region    = System.Drawing.Region.FromHrgn(
                    CreateRoundRectRgn(0, 0, wrapper.Width, 50, 22, 22));
                EnableDB(wrapper);

                var tbl = new TableLayoutPanel
                {
                    Dock        = DockStyle.Fill,
                    ColumnCount = 3,
                    RowCount    = 1,
                    BackColor   = Color.Transparent,
                    Padding     = new Padding(4, 2, 8, 2)
                };
                tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38)); // Play btn
                tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Waveform
                tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42)); // Duration
                tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

                // ── Equalizer Waveform Canvas (Uses real recorded mic data if available) ──
                var pnlWave = new Panel
                {
                    Dock      = DockStyle.Fill,
                    BackColor = Color.Transparent,
                    Margin    = new Padding(2, 4, 2, 4)
                };
                EnableDB(pnlWave);

                int[] samplePattern = null;
                if (!string.IsNullOrEmpty(mediaMetadata))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(mediaMetadata);
                        if (doc.RootElement.TryGetProperty("waveform", out var wfElem) && wfElem.ValueKind == JsonValueKind.Array)
                        {
                            var list = new List<int>();
                            foreach (var item in wfElem.EnumerateArray()) list.Add(item.GetInt32());
                            if (list.Count > 0) samplePattern = list.ToArray();
                        }
                    }
                    catch { }
                }
                if (samplePattern == null || samplePattern.Length == 0)
                {
                    samplePattern = new int[] { 8, 14, 10, 20, 14, 24, 10, 18, 24, 10, 20, 14, 22, 8, 16, 20, 12, 18, 10, 14 };
                }
                tbl.Controls.Add(pnlWave, 1, 0);

                // ── Play/Pause Circle Button ──
                var btnPlay = new Button
                {
                    Dock      = DockStyle.Fill,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.White,
                    ForeColor = isMine ? Color.FromArgb(234, 88, 12) : Color.FromArgb(30, 41, 59),
                    Text      = "▶",
                    Font      = new Font("Segoe UI Symbol", 10F, FontStyle.Bold),
                    Cursor    = Cursors.Hand,
                    Margin    = new Padding(4, 7, 4, 7)
                };
                btnPlay.FlatAppearance.BorderSize = 0;
                tbl.Controls.Add(btnPlay, 0, 0);

                // ── Duration Label ──
                string durStr = caption.Contains("Voice Note")
                    ? caption.Replace("[Voice Note ", "").Replace("]", "").Trim()
                    : "0:05";

                int totalDurationSecs = 5;
                if (!string.IsNullOrEmpty(durStr) && durStr.Contains(":"))
                {
                    var parts = durStr.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int mm) && int.TryParse(parts[1], out int ss))
                    {
                        totalDurationSecs = Math.Max(1, mm * 60 + ss);
                    }
                }

                var lblDur = new Label
                {
                    Text      = string.IsNullOrWhiteSpace(durStr) ? "0:05" : durStr,
                    Font      = new Font("Segoe UI", 8F, FontStyle.Bold),
                    ForeColor = Color.White,
                    Dock      = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    Margin    = new Padding(0, 4, 2, 4)
                };
                tbl.Controls.Add(lblDur, 2, 0);

                float playbackProgress = 0f;
                System.Windows.Forms.Timer playTimer = null;
                bool playing = false;

                Action stopThisBubble = () =>
                {
                    playing = false;
                    playbackProgress = 0f;
                    playTimer?.Stop();
                    playTimer?.Dispose();
                    playTimer = null;
                    if (!btnPlay.IsDisposed) btnPlay.Text = "▶";
                    if (!lblDur.IsDisposed) lblDur.Text = durStr;
                    if (!pnlWave.IsDisposed) pnlWave.Invalidate();
                };

                btnPlay.Click += (s, e) =>
                {
                    if (playing)
                    {
                        StopCurrentAudioPlayback();
                        return;
                    }

                    // Stop any other currently playing voice note in the app!
                    StopCurrentAudioPlayback();

                    playing = true;
                    btnPlay.Text = "⏸";
                    playbackProgress = 0f;

                    string alias = "voice_" + Guid.NewGuid().ToString("N");
                    _currentPlayingAlias = alias;
                    _stopCurrentPlaybackAction = stopThisBubble;

                    int elapsedMs = 0;
                    int totalMs = totalDurationSecs * 1000;

                    playTimer = new System.Windows.Forms.Timer { Interval = 100 };
                    playTimer.Tick += (st, et) =>
                    {
                        elapsedMs += 100;
                        playbackProgress = Math.Min(1.0f, (float)elapsedMs / totalMs);

                        int curSec = Math.Min(totalDurationSecs, elapsedMs / 1000);
                        if (!lblDur.IsDisposed)
                            lblDur.Text = $"{curSec / 60}:{curSec % 60:D2}";

                        if (!pnlWave.IsDisposed)
                            pnlWave.Invalidate();

                        if (elapsedMs >= totalMs)
                        {
                            StopCurrentAudioPlayback();
                        }
                    };
                    playTimer.Start();

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            string localPath = mediaUrl;
                            string cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DriveAndGo", "AudioCache");
                            Directory.CreateDirectory(cacheDir);

                            if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
                            {
                                string fileName = !string.IsNullOrEmpty(fullUrl) ? Path.GetFileName(fullUrl) : $"{alias}.wav";
                                string cachedPath = Path.Combine(cacheDir, fileName);

                                if (File.Exists(cachedPath))
                                {
                                    localPath = cachedPath;
                                }
                                else if (fullUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                                {
                                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                                    var bytes = await client.GetByteArrayAsync(fullUrl);
                                    await File.WriteAllBytesAsync(cachedPath, bytes);
                                    localPath = cachedPath;
                                }
                            }

                            if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
                            {
                                using var sp = new System.Media.SoundPlayer(localPath);
                                sp.Play();
                            }
                            else if (!string.IsNullOrEmpty(localPath))
                            {
                                mciSendString($"open \"{localPath}\" alias {alias}", null, 0, IntPtr.Zero);
                                mciSendString($"play {alias}", null, 0, IntPtr.Zero);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Audio play error: " + ex.Message);
                        }
                    });
                };

                // ── Paint Waveform with Progress Highlight ──
                pnlWave.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    int midY = pnlWave.Height / 2;
                    int spacing = 5;
                    int startX = 4;

                    using var activeBrush = new SolidBrush(Color.White);
                    using var mutedBrush  = new SolidBrush(Color.FromArgb(140, 255, 255, 255));

                    for (int i = 0; i < samplePattern.Length; i++)
                    {
                        int x = startX + (i * spacing);
                        if (x > pnlWave.Width - 4) break;
                        int h = samplePattern[i];

                        float barRatio = (float)i / Math.Max(1, samplePattern.Length - 1);
                        Brush b = (playing && barRatio <= playbackProgress) ? activeBrush : mutedBrush;

                        g.FillRectangle(b, new Rectangle(x, midY - h / 2, 3, Math.Max(3, h)));
                    }
                };

                wrapper.Controls.Add(tbl);
            }

            row.Controls.Add(wrapper);

            // Register bubble for status updates
            var stateHolder = new MessageDeliveryState[] { state };
            if (!string.IsNullOrEmpty(bubbleId))
            {
                row.Tag = bubbleId;
                _bubbleRegistry[bubbleId] = (row, stateHolder);
            }

            // ── Mouse tracking & Hover Action Bar for Media ──────────────────
            bool rowHovered   = false;
            bool smileHovered = false;
            bool dotsHovered  = false;
            Rectangle smileRect = Rectangle.Empty;
            Rectangle dotsRect  = Rectangle.Empty;

            ContextMenuStrip BuildMediaContextMenu()
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
                if (bubbleId != null)
                {
                    AddMenuItem(ctx, "↪  Forward",          ()=> ForwardMessageAction(bubbleId));
                    AddMenuItem(ctx, "🗑  Remove for you", ()=> RemoveMessageAction(bubbleId));
                    AddMenuItem(ctx, "😊  React",           ()=> ReactMessageAction(bubbleId));
                    if (isMine)
                        AddMenuItem(ctx, "↩  Unsend",       ()=> UnsendMessageAction(bubbleId), isDestructive: true);
                }
                return ctx;
            }

            row.MouseEnter += (s, e) => { rowHovered = true;  row.Invalidate(); };
            row.MouseLeave += (s, e) =>
            {
                rowHovered   = false;
                smileHovered = false;
                dotsHovered  = false;
                row.Cursor   = Cursors.Default;
                row.Invalidate();
            };
            row.MouseMove += (s, e) =>
            {
                int scrollOffsetY = Math.Abs(this.AutoScrollPosition.Y) + Math.Abs(_flowMessages?.AutoScrollPosition.Y ?? 0);
                Point mousePt = scrollOffsetY > 0 ? new Point(e.X, e.Y + scrollOffsetY) : e.Location;

                bool newSmile = smileRect != Rectangle.Empty && (smileRect.Contains(e.Location) || smileRect.Contains(mousePt));
                bool newDots  = dotsRect  != Rectangle.Empty && (dotsRect.Contains(e.Location) || dotsRect.Contains(mousePt));

                if (newSmile != smileHovered || newDots != dotsHovered)
                {
                    smileHovered = newSmile;
                    dotsHovered  = newDots;
                    row.Cursor   = (newSmile || newDots) ? Cursors.Hand : Cursors.Default;
                    row.Invalidate();
                }
            };
            row.MouseClick += (s, e) =>
            {
                int scrollOffsetY = Math.Abs(this.AutoScrollPosition.Y) + Math.Abs(_flowMessages?.AutoScrollPosition.Y ?? 0);
                Point mousePt = scrollOffsetY > 0 ? new Point(e.X, e.Y + scrollOffsetY) : e.Location;

                if (smileRect != Rectangle.Empty && (smileRect.Contains(e.Location) || smileRect.Contains(mousePt)))
                {
                    ReactMessageAction(bubbleId);
                    return;
                }
                if (dotsRect != Rectangle.Empty && (dotsRect.Contains(e.Location) || dotsRect.Contains(mousePt)))
                {
                    var ctx = BuildMediaContextMenu();
                    ctx.Show(row, e.Location);
                    return;
                }
            };

            // Status and Timestamp below the media bubble
            row.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                if (rowHovered)
                {
                    int iconSize = 24;
                    int iconY = 2 + (rowH / 2) - (iconSize / 2);

                    if (isMine)
                    {
                        dotsRect  = new Rectangle(bx - iconSize - 4,     iconY, iconSize, iconSize);
                        smileRect = new Rectangle(bx - iconSize * 2 - 8, iconY, iconSize, iconSize);
                    }
                    else
                    {
                        smileRect = new Rectangle(bx + bubbleW + 4,            iconY, iconSize, iconSize);
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

                string ts = time.ToString("h:mm tt");
                using var tFont = new Font("Segoe UI", 8F);
                using var metaColor = new SolidBrush(ThemeManager.IsDarkMode ? Color.FromArgb(140, 200, 200, 200) : Color.FromArgb(140, 80, 80, 80));

                float metaY = 2 + rowH + 4;

                if (isMine)
                {
                    string statusLabel = stateHolder[0] switch
                    {
                        MessageDeliveryState.Sending   => "Sending",
                        MessageDeliveryState.Sent      => "Sent",
                        MessageDeliveryState.Delivered => "Delivered",
                        MessageDeliveryState.Seen      => "Seen",
                        _                              => ""
                    };
                    using var lblFont = new Font("Segoe UI", 7.5F);

                    int iconSize = 14;
                    var iconRect = new Rectangle(bx + bubbleW - iconSize, (int)metaY + 1, iconSize, iconSize);
                    DrawDeliveryIcon(g, iconRect, stateHolder[0]);

                    var lblSz = g.MeasureString(statusLabel, lblFont);
                    float lblX = iconRect.X - lblSz.Width - 3;
                    Color lblColor = stateHolder[0] == MessageDeliveryState.Seen
                        ? Color.FromArgb(200, ThemeManager.CurrentPrimary)
                        : (ThemeManager.IsDarkMode ? Color.FromArgb(140, 200, 200, 200) : Color.FromArgb(140, 80, 80, 80));
                    if (!string.IsNullOrEmpty(statusLabel))
                        g.DrawString(statusLabel, lblFont, new SolidBrush(lblColor), new PointF(lblX, metaY + 1));

                    var tsSz = g.MeasureString(ts, tFont);
                    float tsX = bx;
                    g.DrawString(ts, tFont, metaColor, new PointF(tsX, metaY));
                }
                else
                {
                    g.DrawString(ts, tFont, metaColor, new PointF(bx, metaY));
                }
            };

            _flowMessages.Controls.Add(row);
        }

        private void ShowMediaFullscreen(string url)
        {
            var mediaForm = new Form
            {
                Text            = "Drive&Go — Media Viewer",
                StartPosition   = FormStartPosition.CenterParent,
                Size            = new Size(960, 680),
                MinimumSize     = new Size(500, 400),
                BackColor       = Color.FromArgb(10, 12, 20),
                ShowIcon        = false,
                FormBorderStyle = FormBorderStyle.None,
                KeyPreview      = true
            };

            // Rounded corners for the modal form
            mediaForm.Shown += (s, e) =>
            {
                if (mediaForm.Width > 0 && mediaForm.Height > 0)
                {
                    mediaForm.Region = System.Drawing.Region.FromHrgn(
                        CreateRoundRectRgn(0, 0, mediaForm.Width, mediaForm.Height, 16, 16));
                }
            };
            mediaForm.SizeChanged += (s, e) =>
            {
                if (mediaForm.Width > 0 && mediaForm.Height > 0)
                {
                    mediaForm.Region = System.Drawing.Region.FromHrgn(
                        CreateRoundRectRgn(0, 0, mediaForm.Width, mediaForm.Height, 16, 16));
                }
            };

            mediaForm.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) mediaForm.Close(); };

            // ── Top Header Toolbar (Dark Glass Panel with Title & Action Buttons) ──
            var pnlHeader = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 48,
                BackColor = Color.FromArgb(18, 19, 32),
                Padding   = new Padding(16, 0, 16, 0)
            };
            EnableDB(pnlHeader);

            // Allow dragging window by header panel
            pnlHeader.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(mediaForm.Handle, 0xA1, 0x2, 0);
                }
            };

            var lblTitle = new Label
            {
                Text      = "🖼  Media Viewer",
                Font      = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(241, 245, 249),
                AutoSize  = true,
                Location  = new Point(16, 13),
                BackColor = Color.Transparent
            };
            pnlHeader.Controls.Add(lblTitle);

            // Action Buttons Panel (Copy, Save, Close) on the right
            var pnlActions = new FlowLayoutPanel
            {
                Dock         = DockStyle.Right,
                AutoSize     = true,
                FlowDirection= FlowDirection.LeftToRight,
                BackColor    = Color.Transparent,
                Padding      = new Padding(0, 7, 0, 0)
            };

            // Copy Button
            var btnCopy = new Button
            {
                Text      = "📋 Copy Image",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(234, 88, 12), // Drive&Go Signature Orange
                FlatStyle = FlatStyle.Flat,
                Height    = 34,
                AutoSize  = true,
                Cursor    = Cursors.Hand,
                Margin    = new Padding(0, 0, 8, 0)
            };
            btnCopy.FlatAppearance.BorderSize = 0;
            btnCopy.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, 120, 34, 12, 12));

            // Save / Download Button
            var btnSave = new Button
            {
                Text      = "💾 Save",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(226, 232, 240),
                BackColor = Color.FromArgb(45, 55, 72),
                FlatStyle = FlatStyle.Flat,
                Height    = 34,
                AutoSize  = true,
                Cursor    = Cursors.Hand,
                Margin    = new Padding(0, 0, 8, 0)
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, 80, 34, 12, 12));

            // Close Button
            var btnClose = new Button
            {
                Text      = "✕",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(220, 38, 38),
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(34, 34),
                Cursor    = Cursors.Hand,
                Margin    = new Padding(0, 0, 0, 0)
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, 34, 34, 34, 34));
            btnClose.Click += (s, e) => mediaForm.Close();

            pnlActions.Controls.Add(btnCopy);
            pnlActions.Controls.Add(btnSave);
            pnlActions.Controls.Add(btnClose);
            pnlHeader.Controls.Add(pnlActions);

            // Floating Toast Notification Panel ("Copied image to clipboard! 📋")
            var pnlToast = new Panel
            {
                Size      = new Size(240, 38),
                BackColor = Color.FromArgb(234, 88, 12),
                Visible   = false,
                Anchor    = AnchorStyles.Bottom
            };
            pnlToast.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, 240, 38, 18, 18));
            var lblToast = new Label
            {
                Text      = "Copied image to clipboard! 📋",
                Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            pnlToast.Controls.Add(lblToast);

            System.Windows.Forms.Timer toastTimer = new System.Windows.Forms.Timer { Interval = 2500 };
            toastTimer.Tick += (st, et) =>
            {
                toastTimer.Stop();
                pnlToast.Visible = false;
            };

            Action showToast = () =>
            {
                pnlToast.Location = new Point((mediaForm.ClientSize.Width - pnlToast.Width) / 2, mediaForm.ClientSize.Height - 60);
                pnlToast.Visible = true;
                pnlToast.BringToFront();
                toastTimer.Stop();
                toastTimer.Start();
            };

            string ext = Path.GetExtension(url).ToLowerInvariant();
            bool isVideo = ext == ".mp4" || ext == ".mov" || ext == ".avi" || url.Contains("video");

            var pnlBody = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(10, 12, 20)
            };

            if (!isVideo)
            {
                var pb = new PictureBox
                {
                    Dock      = DockStyle.Fill,
                    SizeMode  = PictureBoxSizeMode.Zoom,
                    BackColor = Color.FromArgb(10, 12, 20),
                    Cursor    = Cursors.Hand
                };

                // Tooltip explaining click to copy
                var toolTip = new ToolTip();
                toolTip.SetToolTip(pb, "Click anywhere on image to copy to clipboard");

                Action copyAction = () =>
                {
                    if (pb.Image != null)
                    {
                        try
                        {
                            Clipboard.SetImage(pb.Image);
                            showToast();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Could not copy image to clipboard: " + ex.Message, "Copy Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                };

                // Copy on Image Click
                pb.Click += (s, e) => copyAction();
                btnCopy.Click += (s, e) => copyAction();

                // Save action
                btnSave.Click += (s, e) =>
                {
                    if (pb.Image != null)
                    {
                        using var sfd = new SaveFileDialog
                        {
                            Filter   = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap Image|*.bmp",
                            FileName = $"DriveAndGo_Media_{DateTime.Now:yyyyMMdd_HHmmss}.png"
                        };
                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            pb.Image.Save(sfd.FileName);
                            MessageBox.Show("Image saved successfully!", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                };

                // Context menu on right click
                var cms = new ContextMenuStrip();
                var itemCopy = cms.Items.Add("📋 Copy Image");
                itemCopy.Click += (s, e) => copyAction();
                var itemSave = cms.Items.Add("💾 Save Image As...");
                itemSave.Click += (s, e) => btnSave.PerformClick();
                pb.ContextMenuStrip = cms;

                pnlBody.Controls.Add(pb);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                        var bytes = await client.GetByteArrayAsync(url);
                        using var ms = new MemoryStream(bytes);
                        var bmp = new Bitmap(ms);
                        if (!pb.IsDisposed)
                            pb.BeginInvoke((Action)(() => { if (!pb.IsDisposed) pb.Image = bmp; }));
                    }
                    catch { }
                });
            }
            else
            {
                btnCopy.Visible = false; // Copy image button not needed for video
                var webView = new Microsoft.Web.WebView2.WinForms.WebView2
                {
                    Dock = DockStyle.Fill
                };
                pnlBody.Controls.Add(webView);

                mediaForm.Shown += async (s, e) =>
                {
                    try
                    {
                        await webView.EnsureCoreWebView2Async();
                        string html = $@"
                        <html>
                          <body style='margin:0;background:#0a0c12;display:flex;align-items:center;justify-content:center;height:100vh;'>
                            <video src='{url}' controls autoplay style='max-width:100%;max-height:100%;border-radius:12px;'></video>
                          </body>
                        </html>";
                        webView.CoreWebView2.NavigateToString(html);
                    }
                    catch { }
                };
            }

            mediaForm.Controls.Add(pnlBody);
            mediaForm.Controls.Add(pnlHeader);
            mediaForm.Controls.Add(pnlToast);

            pnlToast.Location = new Point((mediaForm.ClientSize.Width - pnlToast.Width) / 2, mediaForm.ClientSize.Height - 60);

            mediaForm.ShowDialog(this);
        }


        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

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
            if (_pnlLinkPreview != null) _pnlLinkPreview.Visible = false;
            _isLinkPreviewDismissed = false;
            _lastPreviewUrl = "";

            string bubbleId = Guid.NewGuid().ToString();
            _lastSentBubbleId = bubbleId;
            DateTime now = DateTime.Now;

            AddMessage(text, true, now, MessageDeliveryState.Sending, bubbleId);
            if (_flowMessages.Controls.Count > 0)
                _flowMessages.ScrollControlIntoView(_flowMessages.Controls[_flowMessages.Controls.Count - 1]);

            if (text.Contains("@Drive&Go AI", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("@DriveAndGo AI", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("@AI", StringComparison.OrdinalIgnoreCase))
            {
                TriggerInChatAiMention(text, _activeConvId, _activeConvIsGroup);
            }

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

        private async void UploadAndSendMedia(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || _activeConvId == null) return;
            
            string tempBubbleId = Guid.NewGuid().ToString();
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            string tempMType = (ext == ".jpg" || ext == ".png" || ext == ".jpeg" || ext == ".gif" || ext == ".webp") ? "image"
                             : (ext == ".mp4" || ext == ".mov" || ext == ".avi") ? "video" : "file";

            // 1. Optimistic bubble: show immediately with "Sending" (•) state
            AddMessage(tempMType == "image" ? "[Photo]" : tempMType == "video" ? "[Video]" : "[File]",
                       true, DateTime.Now, MessageDeliveryState.Sending, tempBubbleId, false, false, "{}", tempMType, filePath);
            if (_flowMessages.Controls.Count > 0)
                _flowMessages.ScrollControlIntoView(_flowMessages.Controls[_flowMessages.Controls.Count - 1]);

            try
            {
                using var client = new HttpClient();
                using var content = new MultipartFormDataContent();
                using var stream = File.OpenRead(filePath);
                content.Add(new StreamContent(stream), "file", Path.GetFileName(filePath));

                var apiBase = ApiService.BaseUrl.TrimEnd('/');
                var res = await client.PostAsync($"{apiBase}/messages/upload", content);
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    string url = root.TryGetProperty("Url", out var uPropU) ? uPropU.GetString()
                               : root.TryGetProperty("url", out var uPropL) ? uPropL.GetString() : null;
                    string mType = root.TryGetProperty("MediaType", out var mPropU2) ? mPropU2.GetString()
                                 : root.TryGetProperty("mediaType", out var mPropL2) ? mPropL2.GetString() : tempMType;

                    var payload = new
                    {
                        senderId = "admin",
                        receiverId = _activeConvId,
                        messageBody = mType == "image" ? "[Photo]" : mType == "video" ? "[Video]" : "[File]",
                        isGroupChat = _activeConvIsGroup,
                        senderName = SessionManager.FullName,
                        mediaType = mType,
                        mediaUrl = url
                    };

                    var sendRes = await ApiService.PostAsync("messages", payload);
                    if (sendRes.Success)
                    {
                        // 2. Sent: update state to Sent (✓)
                        UpdateBubbleState(tempBubbleId, MessageDeliveryState.Sent);

                        if (!string.IsNullOrWhiteSpace(sendRes.Body))
                        {
                            try
                            {
                                using var doc2 = JsonDocument.Parse(sendRes.Body);
                                int realId = 0;
                                if (doc2.RootElement.TryGetProperty("messageId", out var mid1)) realId = mid1.GetInt32();
                                else if (doc2.RootElement.TryGetProperty("MessageId", out var mid2)) realId = mid2.GetInt32();

                                if (realId > 0 && _bubbleRegistry.TryGetValue(tempBubbleId, out var entry))
                                {
                                    entry.row.Tag = realId.ToString();
                                    _bubbleRegistry[tempBubbleId] = entry;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Media Upload Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Panel _pnlVoiceRecorderBar;
        private Label _lblVoiceTimer;
        private System.Windows.Forms.Timer _voiceTimer;
        private System.Windows.Forms.Timer _voiceWaveTimer;
        private int _voiceSecs = 0;
        private RealAudioMicrophoneTracker _micTracker;
        private List<int> _waveHeights = new List<int>();

        private void StartVoiceRecordingBar(Panel parentInputBar)
        {
            if (_pnlVoiceRecorderBar != null)
                StopVoiceRecordingBar(parentInputBar);

            _voiceSecs = 0;
            _waveHeights.Clear();

            // ── Root container: Dock = Fill over the inputBar ─────────────────
            _pnlVoiceRecorderBar = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = ThemeManager.CurrentSidebar,
                Padding   = new Padding(6, 6, 6, 6)
            };

            var tbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 3,
                RowCount    = 1,
                BackColor   = Color.Transparent
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));   // Col0: Circular Trash btn (40px)
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));   // Col1: Orange pill
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));   // Col2: Circular Send btn (40px)
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // ── Col 0: Trash / Cancel Button (Perfect 1:1 Circle) ───────────
            var btnTrash = new Button
            {
                Size      = new Size(34, 34),
                Anchor    = AnchorStyles.None,
                Margin    = new Padding(1, 0, 1, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(239, 68, 68),
                ForeColor = Color.White,
                Text      = "🗑",
                Font      = new Font("Segoe UI Emoji", 9.5F, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            btnTrash.FlatAppearance.BorderSize = 0;
            btnTrash.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, 34, 34, 34, 34));
            btnTrash.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, 34, 34);
                using var grad = new LinearGradientBrush(r,
                    Color.FromArgb(239, 68, 68), Color.FromArgb(185, 28, 28),
                    LinearGradientMode.Vertical);
                g.FillEllipse(grad, r);
                TextRenderer.DrawText(g, "🗑", btnTrash.Font, r, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            btnTrash.Click += (s, e) => StopVoiceRecordingBar(parentInputBar);
            tbl.Controls.Add(btnTrash, 0, 0);

            // ── Col 1: Orange Gradient Pill (Waveform + Timer) ───────────────
            var pnlOrangePill = new Panel
            {
                Dock      = DockStyle.Fill,
                Margin    = new Padding(3, 4, 3, 4),
                BackColor = Color.Transparent
            };
            EnableDB(pnlOrangePill);

            pnlOrangePill.SizeChanged += (s, e) =>
            {
                if (pnlOrangePill.Width > 0 && pnlOrangePill.Height > 0)
                {
                    int radius = Math.Min(pnlOrangePill.Height / 2, 18);
                    pnlOrangePill.Region = System.Drawing.Region.FromHrgn(
                        CreateRoundRectRgn(0, 0, pnlOrangePill.Width, pnlOrangePill.Height, radius * 2, radius * 2));
                }
            };

            pnlOrangePill.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var grad = new LinearGradientBrush(
                    pnlOrangePill.ClientRectangle,
                    Color.FromArgb(234, 88, 12), Color.FromArgb(249, 115, 22),
                    LinearGradientMode.Horizontal);
                int radius = Math.Min(pnlOrangePill.Height / 2, 18);
                using var path = RR(pnlOrangePill.ClientRectangle, radius);
                g.FillPath(grad, path);
            };

            // Inner flex row: ⏸ icon | waveform | 0:00
            var innerTbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 3,
                RowCount    = 1,
                BackColor   = Color.Transparent,
                Padding     = new Padding(4, 0, 8, 0)
            };
            innerTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32)); // ⏸ btn
            innerTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // waveform
            innerTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44)); // 0:00
            innerTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // ⏸ pause / recording pulse button (Perfect 1:1 Circle White Pill)
            var btnPause = new Button
            {
                Size      = new Size(26, 26),
                Anchor    = AnchorStyles.None,
                Margin    = new Padding(2, 0, 2, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(234, 88, 12),
                Text      = "⏹",
                Font      = new Font("Segoe UI Symbol", 8F, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            btnPause.FlatAppearance.BorderSize = 0;
            btnPause.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, 26, 26, 26, 26));
            btnPause.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, 26, 26);
                using var bgBrush = new SolidBrush(Color.White);
                g.FillEllipse(bgBrush, r);
                TextRenderer.DrawText(g, "⏹", btnPause.Font, r, Color.FromArgb(234, 88, 12),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            innerTbl.Controls.Add(btnPause, 0, 0);

            // Waveform canvas — REAL mic amplitude bars with rounded capsules
            var pnlWaveform = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin    = new Padding(2, 4, 2, 4)
            };
            EnableDB(pnlWaveform);

            pnlWaveform.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int spacing = 5, barWidth = 3, midY = pnlWaveform.Height / 2;
                
                int maxVisibleBars = (pnlWaveform.Width - 10) / spacing;
                int count = Math.Min(_waveHeights.Count, maxVisibleBars);
                int startIndex = Math.Max(0, _waveHeights.Count - maxVisibleBars);

                for (int i = 0; i < count; i++)
                {
                    int h = _waveHeights[startIndex + i];
                    int x = pnlWaveform.Width - 10 - ((count - 1 - i) * spacing);
                    int barH = Math.Max(4, h);
                    
                    var barRect = new Rectangle(x, midY - barH / 2, barWidth, barH);
                    using var path = RR(barRect, 2);
                    g.FillPath(Brushes.White, path);
                }
            };
            innerTbl.Controls.Add(pnlWaveform, 1, 0);

            // Timer label
            _lblVoiceTimer = new Label
            {
                Text      = "0:00",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Margin    = new Padding(0, 2, 2, 2)
            };
            innerTbl.Controls.Add(_lblVoiceTimer, 2, 0);
            pnlOrangePill.Controls.Add(innerTbl);
            tbl.Controls.Add(pnlOrangePill, 1, 0);

            // ── Col 2: Send Voice Button (Perfect 1:1 Circle) ───────────────
            var btnSendVoice = new Button
            {
                Size      = new Size(34, 34),
                Anchor    = AnchorStyles.None,
                Margin    = new Padding(1, 0, 1, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(234, 88, 12),
                ForeColor = Color.White,
                Text      = "➤",
                Font      = new Font("Segoe UI Symbol", 9.5F, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            btnSendVoice.FlatAppearance.BorderSize = 0;
            btnSendVoice.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, 34, 34, 34, 34));
            btnSendVoice.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, 34, 34);
                using var grad = new LinearGradientBrush(r,
                    Color.FromArgb(234, 88, 12), Color.FromArgb(249, 115, 22),
                    LinearGradientMode.Vertical);
                g.FillEllipse(grad, r);
                TextRenderer.DrawText(g, "➤", btnSendVoice.Font, r, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            btnSendVoice.Click += (s, e) =>
            {
                string durationStr = _lblVoiceTimer?.Text ?? "0:05";
                UploadAndSendVoiceNoteCustom(durationStr, parentInputBar);
            };
            tbl.Controls.Add(btnSendVoice, 2, 0);

            _pnlVoiceRecorderBar.Controls.Add(tbl);

            // ── 1-second counter ─────────────────────────────────────────────
            _voiceTimer?.Stop();
            _voiceTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _voiceTimer.Tick += (s, e) =>
            {
                _voiceSecs++;
                int m = _voiceSecs / 60, sec = _voiceSecs % 60;
                if (_lblVoiceTimer != null && !_lblVoiceTimer.IsDisposed)
                    _lblVoiceTimer.Text = $"{m}:{sec:D2}";
            };
            _voiceTimer.Start();

            // ── REAL HARDWARE MIC TRACKER (WinMM WaveIn RMS) ─────────────────
            _micTracker?.Dispose();
            _micTracker = new RealAudioMicrophoneTracker();
            _micTracker.Start();

            _voiceWaveTimer?.Stop();
            _voiceWaveTimer = new System.Windows.Forms.Timer { Interval = 40 }; // Faster tick for smoother live response
            _voiceWaveTimer.Tick += (s, e) =>
            {
                float amp = _micTracker?.CurrentAmplitude ?? 0f;
                int maxBars = 150; // Max history length
                
                if (amp <= 0.005f) {
                    _waveHeights.Add(3); // Silence
                } else {
                    int h = (int)(amp * 160f); // Scale amplitude significantly to match visualizer
                    _waveHeights.Add(Math.Max(3, Math.Min(50, h)));
                }

                if (_waveHeights.Count > maxBars) {
                    _waveHeights.RemoveAt(0);
                }

                if (pnlWaveform != null && !pnlWaveform.IsDisposed)
                    pnlWaveform.Invalidate();
            };
            _voiceWaveTimer.Start();

            parentInputBar.Controls.Add(_pnlVoiceRecorderBar);
            _pnlVoiceRecorderBar.BringToFront();
        }

        private void StopVoiceRecordingBar(Panel parentInputBar)
        {
            _voiceTimer?.Stop();
            _voiceWaveTimer?.Stop();
            _micTracker?.Stop();
            _micTracker?.Dispose();
            _micTracker = null;

            if (_pnlVoiceRecorderBar != null)
            {
                if (parentInputBar != null && parentInputBar.Controls.Contains(_pnlVoiceRecorderBar))
                {
                    parentInputBar.Controls.Remove(_pnlVoiceRecorderBar);
                }
                _pnlVoiceRecorderBar.Dispose();
                _pnlVoiceRecorderBar = null;
            }
        }

        [System.Runtime.InteropServices.DllImport("winmm.dll", EntryPoint = "mciSendStringA", CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
        private static extern int mciSendString(string command, System.Text.StringBuilder buffer, int bufferSize, IntPtr hwndCallback);

        private static string _currentPlayingAlias = null;
        private static Action _stopCurrentPlaybackAction = null;

        private static void StopCurrentAudioPlayback()
        {
            if (_stopCurrentPlaybackAction != null)
            {
                var stop = _stopCurrentPlaybackAction;
                _stopCurrentPlaybackAction = null;
                try { stop.Invoke(); } catch { }
            }
            if (!string.IsNullOrEmpty(_currentPlayingAlias))
            {
                try
                {
                    mciSendString($"stop {_currentPlayingAlias}", null, 0, IntPtr.Zero);
                    mciSendString($"close {_currentPlayingAlias}", null, 0, IntPtr.Zero);
                }
                catch { }
                _currentPlayingAlias = null;
            }
        }

        private async void UploadAndSendVoiceNoteCustom(string durationStr, Panel parentInputBar = null)
        {
            if (_activeConvId == null) return;
            
            string cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DriveAndGo", "AudioCache");
            Directory.CreateDirectory(cacheFolder);
            string voiceFilePath = Path.Combine(cacheFolder, $"voice_{Guid.NewGuid():N}.wav");

            bool hasSavedFile = _micTracker?.SaveWav(voiceFilePath) ?? false;
            int[] recordedWaveform = GetNormalizedWaveform(20);
            string metadataJson = JsonSerializer.Serialize(new { waveform = recordedWaveform, duration = durationStr });

            // Now stop the recorder UI & mic tracker
            if (parentInputBar != null)
                StopVoiceRecordingBar(parentInputBar);

            string tempBubbleId = Guid.NewGuid().ToString();

            // Optimistic bubble: show immediately with "Sending" (•) state
            AddMessage($"[Voice Note {durationStr}]", true, DateTime.Now, MessageDeliveryState.Sending, tempBubbleId, false, false, "{}", "audio", voiceFilePath, metadataJson);
            if (_flowMessages.Controls.Count > 0)
                _flowMessages.ScrollControlIntoView(_flowMessages.Controls[_flowMessages.Controls.Count - 1]);

            try
            {

                string uploadedUrl = $"/uploads/chat/voice_{Guid.NewGuid():N}.wav";
                if (hasSavedFile && File.Exists(voiceFilePath))
                {
                    try
                    {
                        using var client = new HttpClient();
                        using var content = new MultipartFormDataContent();
                        await using var fs = File.OpenRead(voiceFilePath);
                        var fileContent = new StreamContent(fs);
                        fileContent.Headers.ContentType =
                            new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                        content.Add(fileContent, "file", Path.GetFileName(voiceFilePath));

                        string apiBase = ApiService.BaseUrl.TrimEnd('/');
                        var res = await client.PostAsync($"{apiBase}/messages/upload", content);
                        if (res.IsSuccessStatusCode)
                        {
                            var json = await res.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("url", out var u) || root.TryGetProperty("Url", out u))
                                uploadedUrl = u.GetString() ?? uploadedUrl;
                        }
                    }
                    catch { }
                }

                var payload = new
                {
                    senderId   = "admin",
                    receiverId = _activeConvId,
                    messageBody = $"[Voice Note {durationStr}]",
                    isGroupChat = _activeConvIsGroup,
                    senderName  = SessionManager.FullName,
                    mediaType   = "audio",
                    mediaUrl    = uploadedUrl,
                    mediaMetadata = metadataJson
                };

                var postRes = await ApiService.PostAsync("messages", payload);
                if (postRes.Success)
                {
                    // 2. Sent: update state to Sent (✓)
                    UpdateBubbleState(tempBubbleId, MessageDeliveryState.Sent);

                    if (!string.IsNullOrWhiteSpace(postRes.Body))
                    {
                        try
                        {
                            using var doc2 = JsonDocument.Parse(postRes.Body);
                            int realId = 0;
                            if (doc2.RootElement.TryGetProperty("messageId", out var mid1)) realId = mid1.GetInt32();
                            else if (doc2.RootElement.TryGetProperty("MessageId", out var mid2)) realId = mid2.GetInt32();

                            if (realId > 0 && _bubbleRegistry.TryGetValue(tempBubbleId, out var entry))
                            {
                                entry.row.Tag = realId.ToString();
                                _bubbleRegistry[tempBubbleId] = entry;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Voice Note Error: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private int[] GetNormalizedWaveform(int targetCount)
        {
            int[] fallback = new int[] { 6, 16, 10, 24, 14, 26, 12, 18, 24, 8, 22, 14, 20, 8, 16, 22, 10, 18, 12, 16 };
            if (_waveHeights == null || _waveHeights.Count == 0)
                return fallback;

            int maxAmp = 0;
            foreach (var h in _waveHeights) if (h > maxAmp) maxAmp = h;

            int[] result = new int[targetCount];
            float step = (float)_waveHeights.Count / targetCount;
            for (int i = 0; i < targetCount; i++)
            {
                int idx = Math.Min(_waveHeights.Count - 1, (int)(i * step));
                int val = _waveHeights[idx];
                
                if (maxAmp > 5)
                {
                    float norm = (float)val / maxAmp;
                    result[i] = Math.Max(4, (int)(norm * 24f));
                }
                else
                {
                    result[i] = fallback[i % fallback.Length];
                }
            }
            return result;
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

        private static string ExtractInitialLetter(string name, bool isGroup)
        {
            if (isGroup) return "G";
            if (string.IsNullOrWhiteSpace(name)) return "C";

            string trimmed = name.Trim();
            foreach (char ch in trimmed)
            {
                if (char.IsLetterOrDigit(ch))
                    return char.ToUpperInvariant(ch).ToString();
            }
            foreach (char ch in trimmed)
            {
                if (!char.IsWhiteSpace(ch))
                    return ch.ToString();
            }
            return "C";
        }

        private void SetRoundRegion(Control c, int r)
        {
            c.Region = new Region(RR(new Rectangle(0, 0, c.Width, c.Height), r));
        }

        private void HighlightInputMentions()
        {
            if (_txtInput == null || _isFormattingInputMention || !_txtInput.IsHandleCreated) return;
            _isFormattingInputMention = true;

            int origSelStart = _txtInput.SelectionStart;
            int origSelLen   = _txtInput.SelectionLength;

            SendMessage(_txtInput.Handle, 0x000B, 0, 0);

            try
            {
                _txtInput.SelectAll();
                _txtInput.SelectionColor     = ThemeManager.CurrentText;
                _txtInput.SelectionFont      = new Font("Segoe UI", 10.5F, FontStyle.Regular);
                _txtInput.SelectionBackColor = _txtInput.BackColor;

                string txt = _txtInput.Text;
                if (!string.IsNullOrEmpty(txt))
                {
                    var regex = new System.Text.RegularExpressions.Regex(@"(@Drive&Go AI|@DriveAndGo AI|@[a-zA-Z0-9_&]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    var matches = regex.Matches(txt);

                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        _txtInput.Select(m.Index, m.Length);
                        _txtInput.SelectionColor     = Color.FromArgb(56, 189, 248); // #38bdf8 - Bright Cyan/Blue
                        _txtInput.SelectionFont      = new Font("Segoe UI", 10.5F, FontStyle.Bold);
                        _txtInput.SelectionBackColor = _txtInput.BackColor; // Clean background, no solid block
                    }
                }

                _txtInput.Select(origSelStart, origSelLen);
                _txtInput.SelectionColor     = ThemeManager.CurrentText;
                _txtInput.SelectionFont      = new Font("Segoe UI", 10.5F, FontStyle.Regular);
                _txtInput.SelectionBackColor = _txtInput.BackColor;
            }
            catch { }
            finally
            {
                SendMessage(_txtInput.Handle, 0x000B, 1, 0);
                _txtInput.Invalidate();
                _isFormattingInputMention = false;
            }
        }

        private void DrawMessageTextWithMentions(Graphics g, string text, Font normalFont, RectangleF bounds, bool isMine, bool isUnsent)
        {
            if (string.IsNullOrEmpty(text)) return;

            try
            {
                var mentionRegex = new System.Text.RegularExpressions.Regex(@"(@Drive&Go AI|@DriveAndGo AI|@[a-zA-Z0-9_&]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!mentionRegex.IsMatch(text))
                {
                    using var brush = isUnsent 
                        ? new SolidBrush(ThemeManager.CurrentSubText)
                        : isMine ? (Brush)Brushes.White : new SolidBrush(ThemeManager.CurrentText);
                    g.DrawString(text, normalFont, brush, bounds.Location);
                    return;
                }

                using var boldFont   = new Font(normalFont.FontFamily, normalFont.Size, FontStyle.Bold);
                using var italicFont = new Font(normalFont.FontFamily, normalFont.Size, FontStyle.Italic);

                Color defaultTextColor = isUnsent ? ThemeManager.CurrentSubText : (isMine ? Color.White : ThemeManager.CurrentText);
                Color mentionTextColor = Color.FromArgb(56, 189, 248); // #38bdf8 - Bright Cyan/Blue Font (Bold)
                Color mentionBgColor   = isMine ? Color.FromArgb(190, 15, 23, 42) : Color.FromArgb(45, 56, 189, 248); // Dark navy pill on orange bubble, soft blue pill on card

                string[] lines = text.Replace("\r\n", "\n").Split('\n');
                float curY = bounds.Y;
                float lineHeight = normalFont.GetHeight(g) + 2f;
                TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

                foreach (string line in lines)
                {
                    if (curY + lineHeight > bounds.Bottom + lineHeight) break;

                    var tokens = TokenizeMessageLine(line, mentionRegex);
                    float curX = bounds.X;

                    foreach (var token in tokens)
                    {
                        Font tokenFont = token.IsMention ? boldFont : (isUnsent ? italicFont : normalFont);
                        Size sz = TextRenderer.MeasureText(g, token.Text, tokenFont, new Size(1000, 100), flags);
                        int tokenW = sz.Width;

                        if (curX + tokenW > bounds.Right && curX > bounds.X)
                        {
                            curX = bounds.X;
                            curY += lineHeight;
                            if (curY + lineHeight > bounds.Bottom + lineHeight) break;
                        }

                        if (token.IsMention)
                        {
                            var pillRect = new Rectangle((int)curX - 2, (int)curY, tokenW + 4, (int)lineHeight - 1);
                            using var pillPath = RR(pillRect, 5);
                            using var pillBrush = new SolidBrush(mentionBgColor);
                            g.FillPath(pillBrush, pillPath);

                            TextRenderer.DrawText(g, token.Text, boldFont, new Point((int)curX, (int)curY), mentionTextColor, flags);
                        }
                        else
                        {
                            TextRenderer.DrawText(g, token.Text, tokenFont, new Point((int)curX, (int)curY), defaultTextColor, flags);
                        }

                        curX += tokenW;
                    }

                    curY += lineHeight;
                }
            }
            catch
            {
                try
                {
                    using var fallbackBrush = isMine ? (Brush)Brushes.White : new SolidBrush(ThemeManager.CurrentText);
                    g.DrawString(text ?? "", normalFont, fallbackBrush, bounds.Location);
                }
                catch { }
            }
        }

        private struct MentionTextToken
        {
            public string Text;
            public bool IsMention;
        }

        private static List<MentionTextToken> TokenizeMessageLine(string line, System.Text.RegularExpressions.Regex mentionRegex)
        {
            var list = new List<MentionTextToken>();
            int lastIdx = 0;
            var matches = mentionRegex.Matches(line);

            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                if (m.Index > lastIdx)
                {
                    string preceding = line.Substring(lastIdx, m.Index - lastIdx);
                    AddWordsAndSpaces(list, preceding, false);
                }

                list.Add(new MentionTextToken { Text = m.Value, IsMention = true });
                lastIdx = m.Index + m.Length;
            }

            if (lastIdx < line.Length)
            {
                string trailing = line.Substring(lastIdx);
                AddWordsAndSpaces(list, trailing, false);
            }

            return list;
        }

        private static void AddWordsAndSpaces(List<MentionTextToken> list, string text, bool isMention)
        {
            var parts = System.Text.RegularExpressions.Regex.Split(text, @"(\s+)");
            foreach (var p in parts)
            {
                if (string.IsNullOrEmpty(p)) continue;
                list.Add(new MentionTextToken { Text = p, IsMention = isMention });
            }
        }

        private void CheckForMentionPopup()
        {
            if (_txtInput == null || _pnlMentionPopup == null) return;
            string txt = _txtInput.Text;
            int sel = _txtInput.SelectionStart;
            if (sel > 0 && txt.Length >= sel && (txt[sel - 1] == '@' || (sel >= 2 && txt.Substring(sel - 2, 2).Equals("@D", StringComparison.OrdinalIgnoreCase))))
            {
                _pnlMentionPopup.Visible = true;
                _pnlMentionPopup.BringToFront();
            }
            else
            {
                _pnlMentionPopup.Visible = false;
            }
        }

        private void ApplyMentionAutocomplete()
        {
            if (_txtInput == null) return;
            string txt = _txtInput.Text;
            int lastAt = txt.LastIndexOf('@');
            if (lastAt >= 0)
            {
                _txtInput.Text = txt.Substring(0, lastAt) + "@Drive&Go AI ";
            }
            else
            {
                _txtInput.Text = "@Drive&Go AI ";
            }
            _txtInput.SelectionStart = _txtInput.Text.Length;
            if (_pnlMentionPopup != null) _pnlMentionPopup.Visible = false;
        }

        private void TryAddAiMessageOnce(string body, string messageId, DateTime dt)
        {
            if (string.IsNullOrWhiteSpace(body)) return;
            string key = !string.IsNullOrWhiteSpace(messageId) && messageId != "0" ? messageId : body.Trim();
            if (_renderedAiMessageKeys.Contains(key)) return;
            _renderedAiMessageKeys.Add(key);

            AddMessage(body, false, dt, MessageDeliveryState.Delivered, bubbleId: "ai_" + Guid.NewGuid().ToString("N"));
            if (_flowMessages.Controls.Count > 0)
                _flowMessages.ScrollControlIntoView(_flowMessages.Controls[_flowMessages.Controls.Count - 1]);
        }

        private void TriggerInChatAiMention(string userPrompt, string conversationId, bool isGroupChat)
        {
            if (string.IsNullOrWhiteSpace(conversationId)) return;

            var pnlTyping = new Panel
            {
                Width     = Math.Max(200, _flowMessages.ClientSize.Width - 24),
                Height    = 32,
                BackColor = Color.Transparent,
                Margin    = new Padding(0, 4, 0, 4)
            };
            var lblTyping = new Label
            {
                Text      = "✨ @Drive&Go AI is typing...",
                Font      = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.FromArgb(56, 189, 248),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlTyping.Controls.Add(lblTyping);
            _flowMessages.Controls.Add(pnlTyping);
            _flowMessages.ScrollControlIntoView(pnlTyping);

            _ = Task.Run(async () =>
            {
                string aiReplyText = null;
                string resMsgId = null;
                try
                {
                    var req = new
                    {
                        conversationId = conversationId,
                        senderId = "admin",
                        userPrompt = userPrompt,
                        isGroupChat = isGroupChat
                    };
                    var res = await ApiService.PostAsync("messages/mention-ai", req);
                    if (res.Success && !string.IsNullOrWhiteSpace(res.Body))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(res.Body);
                        if (doc.RootElement.TryGetProperty("messageBody", out var mbElem))
                        {
                            aiReplyText = mbElem.GetString();
                        }
                        if (doc.RootElement.TryGetProperty("messageId", out var midElem))
                        {
                            resMsgId = midElem.GetInt32().ToString();
                        }
                    }
                }
                catch { }
                finally
                {
                    this.BeginInvoke((Action)(() =>
                    {
                        if (!this.IsDisposed && pnlTyping.Parent != null)
                        {
                            _flowMessages.Controls.Remove(pnlTyping);
                            pnlTyping.Dispose();
                        }
                        if (!this.IsDisposed && !string.IsNullOrWhiteSpace(aiReplyText) && _activeConvId == conversationId)
                        {
                            TryAddAiMessageOnce(aiReplyText, resMsgId, DateTime.Now);
                        }
                    }));
                }
            });
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

        private void CheckForInputLinkPreview()
        {
            if (_txtInput == null || _pnlLinkPreview == null) return;
            string text = _txtInput.Text;
            var match = System.Text.RegularExpressions.Regex.Match(text, @"(https?://[^\s]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                _pnlLinkPreview.Visible = false;
                _isLinkPreviewDismissed = false;
                _lastPreviewUrl = "";
                return;
            }

            string url = match.Groups[1].Value;
            if (url == _lastPreviewUrl || _isLinkPreviewDismissed) return;

            _lastPreviewUrl = url;

            _ = Task.Run(async () =>
            {
                try
                {
                    var res = await ApiService.GetAsync($"media/link-preview?url={Uri.EscapeDataString(url)}");
                    if (res.Success && !string.IsNullOrEmpty(res.Body) && !this.IsDisposed)
                    {
                        var linkDto = System.Text.Json.JsonSerializer.Deserialize<LinkPreviewDto>(res.Body,
                            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (linkDto != null)
                        {
                            this.BeginInvoke((Action)(() =>
                            {
                                if (this.IsDisposed || _isLinkPreviewDismissed) return;
                                _lblLinkPreviewTitle.Text = !string.IsNullOrEmpty(linkDto.Title) ? linkDto.Title : linkDto.Domain;
                                _lblLinkPreviewDesc.Text  = !string.IsNullOrEmpty(linkDto.Description) ? linkDto.Description : (!string.IsNullOrEmpty(linkDto.SiteName) ? linkDto.SiteName : linkDto.Domain);

                                if (!string.IsNullOrEmpty(linkDto.Image))
                                {
                                    try { _pbLinkPreviewThumb.LoadAsync(linkDto.Image); } catch { }
                                }
                                else
                                {
                                    _pbLinkPreviewThumb.Image = null;
                                }

                                _pnlLinkPreview.Visible = true;
                                _pnlLinkPreview.BringToFront();
                            }));
                        }
                    }
                }
                catch { }
            });
        }

        public class LinkPreviewDto
        {
            public string Url { get; set; } = "";
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
            public string Domain { get; set; } = "";
            public string Image { get; set; } = "";
            public string SiteName { get; set; } = "";
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

        private void EditMessageAction(string bubbleId, string currentText)
        {
            using var inputForm = new Form
            {
                Text            = "Drive&Go — Edit Message",
                Size            = new Size(440, 220),
                StartPosition   = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox     = false,
                MinimizeBox     = false,
                BackColor       = Color.FromArgb(20, 22, 34),
                ForeColor       = Color.White
            };

            var lbl = new Label { Text = "✏️ Edit your message:", Left = 16, Top = 14, AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(241, 245, 249) };
            var txt = new TextBox { Text = currentText, Left = 16, Top = 42, Width = 390, Multiline = true, Height = 75, Font = new Font("Segoe UI", 10F), BackColor = Color.FromArgb(12, 14, 22), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            
            var btnSave = new Button { Text = "Save Changes", DialogResult = DialogResult.OK, Left = 246, Top = 130, Width = 105, Height = 34, BackColor = Color.FromArgb(234, 88, 12), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, 105, 34, 10, 10));

            var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 357, Top = 130, Width = 50, Height = 34, BackColor = Color.FromArgb(50, 52, 68), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9F) };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, 50, 34, 10, 10));

            inputForm.Controls.Add(lbl);
            inputForm.Controls.Add(txt);
            inputForm.Controls.Add(btnSave);
            inputForm.Controls.Add(btnCancel);
            inputForm.AcceptButton = btnSave;
            inputForm.CancelButton = btnCancel;

            if (inputForm.ShowDialog(this) == DialogResult.OK)
            {
                string editedText = txt.Text.Trim();
                if (!string.IsNullOrEmpty(editedText) && editedText != currentText)
                {
                    _ = ExecuteMessageEdit(bubbleId, editedText);
                }
            }
        }

        private async Task ExecuteMessageEdit(string bubbleId, string newText)
        {
            try
            {
                await ApiService.PostAsync($"messages/{bubbleId}/edit", new { body = newText, text = newText });
                if (!string.IsNullOrEmpty(_activeConvId))
                {
                    if (this.InvokeRequired)
                        this.Invoke((Action)(() => LoadMessagesFromApi(_activeConvId)));
                    else
                        LoadMessagesFromApi(_activeConvId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExecuteMessageEdit] Error: {ex.Message}");
            }
        }

        private void ShowEditHistoryModal(string historyJson)
        {
            var historyForm = new Form
            {
                Text            = "Drive&Go — Edit History",
                StartPosition   = FormStartPosition.CenterParent,
                Size            = new Size(440, 340),
                BackColor       = Color.FromArgb(20, 22, 34),
                ShowIcon        = false,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox     = false,
                MinimizeBox     = false
            };

            var lblTitle = new Label { Text = "✏️ Edit History", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.White, Left = 16, Top = 14, AutoSize = true };
            
            var pnlList = new FlowLayoutPanel
            {
                Left          = 16,
                Top           = 46,
                Width         = 390,
                Height        = 220,
                AutoScroll    = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents  = false,
                BackColor     = Color.FromArgb(12, 14, 22),
                Padding       = new Padding(8)
            };

            List<string> entries = new List<string>();
            if (!string.IsNullOrEmpty(historyJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(historyJson);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            if (el.ValueKind == JsonValueKind.String)
                                entries.Add(el.GetString());
                            else if (el.TryGetProperty("text", out var t))
                                entries.Add(t.GetString());
                            else if (el.TryGetProperty("body", out var b))
                                entries.Add(b.GetString());
                        }
                    }
                }
                catch { }
            }

            if (entries.Count == 0)
            {
                pnlList.Controls.Add(new Label { Text = "No previous versions recorded.", ForeColor = Color.FromArgb(148, 163, 184), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Italic), Padding = new Padding(8) });
            }
            else
            {
                foreach (var item in entries)
                {
                    var pnlItem = new Panel { Width = 350, Height = 56, BackColor = Color.FromArgb(28, 30, 46), Margin = new Padding(0, 0, 0, 8), Padding = new Padding(10) };
                    pnlItem.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, 350, 56, 12, 12));
                    var lblText = new Label { Text = item, ForeColor = Color.FromArgb(241, 245, 249), Font = new Font("Segoe UI", 9.5F), Dock = DockStyle.Fill };
                    pnlItem.Controls.Add(lblText);
                    pnlList.Controls.Add(pnlItem);
                }
            }

            var btnClose = new Button { Text = "Close", DialogResult = DialogResult.OK, Left = 326, Top = 274, Width = 80, Height = 32, BackColor = Color.FromArgb(234, 88, 12), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, 80, 32, 10, 10));

            historyForm.Controls.Add(lblTitle);
            historyForm.Controls.Add(pnlList);
            historyForm.Controls.Add(btnClose);
            historyForm.ShowDialog(this);
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

    /// <summary>
    /// Real Audio Microphone Tracker — Captures physical PCM audio samples from Windows soundcard
    /// via WinMM P/Invoke. Computes real Root Mean Square (RMS) volume amplitude.
    /// Returns EXACTLY 0.0f on room silence!
    /// </summary>
    public class RealAudioMicrophoneTracker : IDisposable
    {
        private IntPtr _waveIn = IntPtr.Zero;
        private bool _isRecording = false;
        private float _currentAmplitude = 0f;

        // PCM sample accumulator for WAV file export
        private readonly System.IO.MemoryStream _pcmStream = new();
        private readonly object _pcmLock = new();

        // WinMM P/Invoke
        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        private static extern int waveInOpen(out IntPtr phwi, int uDeviceID, ref WAVEFORMATEX pwfx, WaveInProc dwCallback, IntPtr dwInstance, int fdwOpen);
        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        private static extern int waveInStart(IntPtr hwi);
        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        private static extern int waveInStop(IntPtr hwi);
        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        private static extern int waveInClose(IntPtr hwi);
        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        private static extern int waveInAddBuffer(IntPtr hwi, ref WAVEHDR pwh, int cbwh);
        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        private static extern int waveInPrepareHeader(IntPtr hwi, ref WAVEHDR pwh, int cbwh);
        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        private static extern int waveInUnprepareHeader(IntPtr hwi, ref WAVEHDR pwh, int cbwh);

        private delegate void WaveInProc(IntPtr hwi, int uMsg, IntPtr dwInstance, ref WAVEHDR dwParam1, IntPtr dwParam2);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint   nSamplesPerSec;
            public uint   nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct WAVEHDR
        {
            public IntPtr lpData;
            public uint   dwBufferLength;
            public uint   dwBytesRecorded;
            public IntPtr dwUser;
            public uint   dwFlags;
            public uint   dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        private const int SampleRate    = 16000;
        private const int BitsPerSample = 16;
        private const int Channels      = 1;

        private WaveInProc _callback;
        private WAVEHDR[]  _hdrs = new WAVEHDR[2];
        private byte[][]   _buffers = new byte[2][];
        private System.Runtime.InteropServices.GCHandle[] _bufferHandles = new System.Runtime.InteropServices.GCHandle[2];

        public float CurrentAmplitude => _currentAmplitude;

        public void Start()
        {
            if (_isRecording) return;
            lock (_pcmLock) { _pcmStream.SetLength(0); }
            try
            {
                var format = new WAVEFORMATEX
                {
                    wFormatTag     = 1, // PCM
                    nChannels      = Channels,
                    nSamplesPerSec = SampleRate,
                    wBitsPerSample = BitsPerSample,
                    nBlockAlign    = (ushort)(Channels * BitsPerSample / 8),
                    nAvgBytesPerSec= (uint)(SampleRate * Channels * BitsPerSample / 8),
                    cbSize         = 0
                };

                _callback = new WaveInProc(OnWaveInProc);
                int res = waveInOpen(out _waveIn, -1, ref format, _callback, IntPtr.Zero, 0x00030000);
                if (res == 0 && _waveIn != IntPtr.Zero)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        _buffers[i] = new byte[3200]; // 100ms @ 16kHz 16-bit mono
                        _bufferHandles[i] = System.Runtime.InteropServices.GCHandle.Alloc(
                            _buffers[i], System.Runtime.InteropServices.GCHandleType.Pinned);
                        _hdrs[i] = new WAVEHDR
                        {
                            lpData         = _bufferHandles[i].AddrOfPinnedObject(),
                            dwBufferLength = (uint)_buffers[i].Length
                        };
                        waveInPrepareHeader(_waveIn, ref _hdrs[i], System.Runtime.InteropServices.Marshal.SizeOf(_hdrs[i]));
                        waveInAddBuffer   (_waveIn, ref _hdrs[i], System.Runtime.InteropServices.Marshal.SizeOf(_hdrs[i]));
                    }
                    waveInStart(_waveIn);
                    _isRecording = true;
                }
            }
            catch { }
        }

        private void OnWaveInProc(IntPtr hwi, int uMsg, IntPtr dwInstance, ref WAVEHDR dwParam1, IntPtr dwParam2)
        {
            try
            {
                if (uMsg == 0x3C0 && dwParam1.dwBytesRecorded > 0 && dwParam1.lpData != IntPtr.Zero) // WIM_DATA
                {
                    int count = (int)dwParam1.dwBytesRecorded;
                    byte[] tempBuf = new byte[count];
                    System.Runtime.InteropServices.Marshal.Copy(dwParam1.lpData, tempBuf, 0, count);

                    // ── Amplitude (RMS) ───────────────────────────────────────────
                    long sum = 0;
                    int samples = count / 2;
                    for (int i = 0; i < samples; i++)
                    {
                        short s = (short)(tempBuf[i * 2] | (tempBuf[i * 2 + 1] << 8));
                        sum += (long)s * s;
                    }
                    float rms = (float)Math.Sqrt((double)sum / Math.Max(1, samples));
                    _currentAmplitude = Math.Min(1.0f, rms / 2500.0f);
                    if (_currentAmplitude < 0.04f) _currentAmplitude = 0f;

                    // ── Accumulate PCM for WAV file ───────────────────────────────
                    lock (_pcmLock)
                        _pcmStream.Write(tempBuf, 0, count);

                    if (_isRecording && _waveIn != IntPtr.Zero)
                        waveInAddBuffer(_waveIn, ref dwParam1,
                            System.Runtime.InteropServices.Marshal.SizeOf(dwParam1));
                }
            }
            catch { }
        }

        /// <summary>
        /// Writes the accumulated PCM samples to a valid WAV file.
        /// Returns true if the file was written with audio data, false otherwise.
        /// </summary>
        public bool SaveWav(string filePath)
        {
            byte[] pcm;
            lock (_pcmLock)
                pcm = _pcmStream.ToArray();

            if (pcm.Length == 0) return false;

            using var fs  = new FileStream(filePath, FileMode.Create);
            using var bw  = new System.IO.BinaryWriter(fs);

            int byteRate  = SampleRate * Channels * BitsPerSample / 8;
            int blockAlign= Channels * BitsPerSample / 8;

            // RIFF header
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + pcm.Length);                              // ChunkSize
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            // fmt  sub-chunk
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);                                           // SubChunk1Size (PCM)
            bw.Write((ushort)1);                                    // AudioFormat   (PCM)
            bw.Write((ushort)Channels);
            bw.Write(SampleRate);
            bw.Write(byteRate);
            bw.Write((ushort)blockAlign);
            bw.Write((ushort)BitsPerSample);
            // data sub-chunk
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(pcm.Length);
            bw.Write(pcm);
            return true;
        }

        public void Stop()
        {
            if (!_isRecording) return;
            _isRecording = false;
            try
            {
                if (_waveIn != IntPtr.Zero)
                {
                    waveInStop(_waveIn);
                    for (int i = 0; i < 2; i++)
                    {
                        try
                        {
                            waveInUnprepareHeader(_waveIn, ref _hdrs[i], System.Runtime.InteropServices.Marshal.SizeOf(_hdrs[i]));
                        }
                        catch { }
                    }
                    waveInClose(_waveIn);
                    _waveIn = IntPtr.Zero;
                }
                for (int i = 0; i < 2; i++)
                {
                    if (_bufferHandles[i].IsAllocated)
                        _bufferHandles[i].Free();
                }
            }
            catch { }
            _currentAmplitude = 0f;
        }

        public void Dispose()
        {
            Stop();
            _pcmStream.Dispose();
        }
    }

}  // end namespace DriveAndGo_Admin
