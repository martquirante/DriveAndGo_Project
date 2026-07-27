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

        // ── Left pane controls ───────────────────────────────────────────────────
        private Label   _lblChats;
        private TextBox _txtSearch;
        private Panel   _convListPanel;

        // ── Right pane controls ──────────────────────────────────────────────────
        private Panel   _headerBar;
        private Label   _lblConvName;
        private Label   _lblConvStatus;
        private Button  _btnToggleExpand;
        private Panel   _messagesPanel;
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
            this.Paint    += OnBackgroundPaint;

            ThemeManager.ThemeChanged += (s, e) => ApplyTheme();

            BuildLayout();
            InitializeSignalR();
            LoadConversationsFromApi();
        }

        // ── Background: radial glow ──────────────────────────────────────────────
        private void OnBackgroundPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
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

            _convListPanel = new Panel();
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

            // Explicit Z-order addition to guarantee top-to-bottom layout hierarchy
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

            // ── Messages Stream ──
            _flowMessages = new FlowLayoutPanel
            {
                Dock            = DockStyle.Fill,
                FlowDirection   = FlowDirection.TopDown,
                WrapContents    = false,
                AutoScroll      = true,
                BackColor       = ThemeManager.CurrentBackground,
                Padding         = new Padding(12, 8, 12, 8)
            };
            EnableDB(_flowMessages);
            _flowMessages.Resize += (s, e) =>
            {
                int targetW = Math.Max(100, _flowMessages.ClientSize.Width - 24);
                _flowMessages.SuspendLayout();
                foreach (Control ctrl in _flowMessages.Controls)
                {
                    if (ctrl is Panel p) p.Width = targetW;
                }
                _flowMessages.ResumeLayout();
            };
            _rightPane.Controls.Add(_flowMessages);

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

                _hubConnection.On<string, string, string, string>("ReceiveChatMessage", (senderId, receiverId, body, timestamp) =>
                {
                    if (this.IsDisposed || !this.IsHandleCreated) return;
                    this.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                    {
                        UpdateConversationLastMsg(senderId, receiverId, body, timestamp);

                        DateTime dt = DateTime.Now;
                        if (DateTime.TryParse(timestamp, out var parsedDt))
                        {
                            dt = parsedDt.ToLocalTime();
                        }

                        if (_activeConvId != null)
                        {
                            if (senderId != "admin" && 
                                ((senderId == _activeConvId && receiverId == "admin") ||
                                 (_activeConvIsGroup && receiverId == _activeConvId)))
                            {
                                AddMessage(body, false, dt, MessageDeliveryState.Delivered);
                                if (_flowMessages.Controls.Count > 0)
                                    _flowMessages.ScrollControlIntoView(_flowMessages.Controls[_flowMessages.Controls.Count - 1]);

                                if (_lastSentBubbleId != null)
                                    UpdateBubbleState(_lastSentBubbleId, MessageDeliveryState.Seen);
                            }
                            else if (senderId == "admin")
                            {
                                if (_lastSentBubbleId != null)
                                {
                                    UpdateBubbleState(_lastSentBubbleId, MessageDeliveryState.Delivered);
                                }
                            }
                        }
                    }));
                });

                await _hubConnection.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ChatOverlayPanel] SignalR connection notice: " + ex.Message);
            }
        }

        private void UpdateConversationLastMsg(string senderId, string receiverId, string body, string timestamp)
        {
            string contactId = senderId == "admin" ? receiverId : senderId;
            DateTime dt = DateTime.Now;
            DateTime.TryParse(timestamp, out dt);

            bool found = false;
            for (int i = 0; i < _conversations.Count; i++)
            {
                var conv = _conversations[i];
                if (conv.Id == contactId)
                {
                    conv.LastMessage = body;
                    conv.Time = dt.ToLocalTime().ToString("h:mm tt");
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
                    foreach (var item in root.EnumerateArray())
                    {
                        string id = item.GetProperty("id").GetString();
                        string name = item.GetProperty("name").GetString();
                        string role = item.TryGetProperty("role", out var rProp) ? rProp.GetString() : "Customer";
                        string lastMsg = item.TryGetProperty("lastMessage", out var mProp) ? mProp.GetString() : "";
                        string time = item.TryGetProperty("time", out var tProp) ? tProp.GetString() : "";
                        if (DateTime.TryParse(time, out var tParsed))
                        {
                            time = tParsed.ToLocalTime().ToString("h:mm tt");
                        }
                        int unread = item.TryGetProperty("unreadCount", out var uProp) ? uProp.GetInt32() : 0;
                        bool isGroup = role == "Group" || id.StartsWith("gc") || id.StartsWith("g");

                        _conversations.Add(new ConvItem
                        {
                            Id = id,
                            Name = name,
                            Role = role,
                            LastMessage = lastMsg,
                            Time = time,
                            UnreadCount = unread,
                            IsGroup = isGroup
                        });
                    }
                    RefreshConvList();
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
            {
                AddMessengerContactHeader(activeConv);
            }

            try
            {
                var res = await ApiService.GetAsync($"messages?senderId=admin&receiverId={contactId}");
                if (res.Success && !string.IsNullOrWhiteSpace(res.Body))
                {
                    var root = JsonDocument.Parse(res.Body).RootElement;
                    int count = 0;
                    foreach (var item in root.EnumerateArray())
                    {
                        string sId = item.GetProperty("senderId").GetString();
                        string body = item.GetProperty("messageBody").GetString();
                        DateTime ts = item.GetProperty("timestamp").GetDateTime().ToLocalTime();
                        
                        bool isMine = sId == "admin";
                        AddMessage(body, isMine, ts, MessageDeliveryState.Delivered);
                        count++;
                    }
                    if (count > 0 && _flowMessages.Controls.Count > 0)
                    {
                        _flowMessages.ScrollControlIntoView(_flowMessages.Controls[_flowMessages.Controls.Count - 1]);
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

        private void RefreshConvList()
        {
            _convListPanel.Controls.Clear();

            string query = _txtSearch?.Text?.Trim().ToLower() ?? "";
            bool isSearching = !string.IsNullOrEmpty(query);

            int y = 6;
            int cardW = Math.Max(100, _convListPanel.ClientSize.Width - 4);
            int countVisible = 0;

            foreach (var conv in _conversations)
            {
                bool hasMessages = !string.IsNullOrWhiteSpace(conv.LastMessage) &&
                                   conv.LastMessage != "No messages yet" &&
                                   conv.LastMessage != "Tap to start conversation" &&
                                   conv.LastMessage != "Group Chat Channel";

                if (!isSearching && !hasMessages)
                {
                    continue;
                }

                if (isSearching)
                {
                    bool matches = conv.Name.ToLower().Contains(query) ||
                                   conv.Role.ToLower().Contains(query) ||
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
                    Text      = isSearching ? "No conversations found" : "No active chats yet.\r\nClick '+' or search to start a chat.",
                    Font      = new Font("Segoe UI", 9.5F),
                    ForeColor = ThemeManager.CurrentSubText,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock      = DockStyle.Fill
                };
                _convListPanel.Controls.Add(lblEmpty);
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
                var r = new Rectangle(6, 2, card.Width - 12, card.Height - 4);
                using var path = RR(r, 10);

                Color bgAlpha = isActive
                    ? ThemeManager.NavActiveBg
                    : (ThemeManager.IsDarkMode ? Color.FromArgb(8, 255, 255, 255) : Color.FromArgb(12, 0, 0, 0));

                using var bg = new SolidBrush(bgAlpha);
                g.FillPath(bg, path);

                if (isActive)
                {
                    using var pen = new Pen(ThemeManager.CurrentPrimary, 1f);
                    g.DrawPath(pen, path);
                }

                g.FillRectangle(new SolidBrush(roleColor), 6, 2, 3, card.Height - 4);

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

                using var nameFont = new Font("Segoe UI", 10F, FontStyle.Bold);
                g.DrawString(conv.Name, nameFont, new SolidBrush(ThemeManager.CurrentText), new PointF(68, 12));

                using var roleFont = new Font("Segoe UI", 7.5F);
                var roleText = "[" + conv.Role.ToUpper() + "]";
                g.DrawString(roleText, roleFont, new SolidBrush(roleColor), new PointF(68, 30));

                using var msgFont = new Font("Segoe UI", 9F);
                string lastMsg = conv.LastMessage;
                if (lastMsg?.Length > 30) lastMsg = lastMsg.Substring(0, 30) + "...";
                g.DrawString(lastMsg, msgFont, new SolidBrush(ThemeManager.CurrentSubText), new PointF(68, 46));

                using var timeFont = new Font("Segoe UI", 7.5F);
                using var timeFmt = new StringFormat { Alignment = StringAlignment.Far };
                g.DrawString(conv.Time, timeFont, new SolidBrush(ThemeManager.CurrentSubText), new RectangleF(card.Width - 75, 12, 65, 16), timeFmt);

                if (conv.UnreadCount > 0)
                {
                    var badge = new Rectangle(card.Width - 30, card.Height - 26, 22, 18);
                    g.FillEllipse(new SolidBrush(ThemeManager.CurrentPrimary), badge);
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
            LoadMessagesFromApi(conv.Id);
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
                Width     = Math.Max(280, _flowMessages.Width - 40),
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

                using (var titleFont = new Font("Segoe UI", 15F, FontStyle.Bold))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center })
                {
                    g.DrawString("DriveAndGo Messaging Hub", titleFont, new SolidBrush(ThemeManager.CurrentText), new PointF(cx, 130), fmt);
                }

                int badgeX = cx + 124;
                var vBadge = new Rectangle(badgeX, 134, 18, 18);
                g.FillEllipse(Brushes.DodgerBlue, vBadge);
                DrawCheckmark(g, vBadge, Color.White, 1.3f);

                using (var subFont = new Font("Segoe UI", 9.5F))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center })
                {
                    g.DrawString("Business chats and driver communications", subFont, new SolidBrush(ThemeManager.CurrentSubText), new PointF(cx, 162), fmt);
                }

                var cardRect = new Rectangle(cx - 160, 200, 320, 100);
                using (var path = RR(cardRect, 16))
                {
                    using var cardBg  = new SolidBrush(ThemeManager.CurrentCard);
                    using var cardPen = new Pen(ThemeManager.CurrentBorder, 1f);
                    g.FillPath(cardBg, path);
                    g.DrawPath(cardPen, path);
                }

                using (var bodyFont = new Font("Segoe UI", 9.5F))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center })
                {
                    g.DrawString("Select a conversation from the left menu\r\nto start messaging drivers, customers, or groups.", bodyFont, new SolidBrush(ThemeManager.CurrentText), new RectangleF(cardRect.X + 10, cardRect.Y + 24, cardRect.Width - 20, cardRect.Height - 40), fmt);
                }

                using (var lockFont = new Font("Segoe UI", 8.5F))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center })
                {
                    g.DrawString("🔒 End-to-end encrypted dispatch network", lockFont, new SolidBrush(ThemeManager.CurrentSubText), new PointF(cx, 320), fmt);
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

            _flowMessages.Controls.Add(container);
        }

        private void AddMessengerContactHeader(ConvItem conv)
        {
            var headerCard = new Panel
            {
                Width     = Math.Max(280, _flowMessages.Width - 40),
                Height    = 170,
                BackColor = Color.Transparent,
                Margin    = new Padding(10, 10, 10, 10)
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
        }

        // ════════════════════════════════════════════════════════════════════════
        //  MESSAGE BUBBLES & MESSENGER DELIVERY STATUS ICONS
        // ════════════════════════════════════════════════════════════════════════
        private void AddMessage(string text, bool isMine, DateTime time, MessageDeliveryState state = MessageDeliveryState.Delivered, string bubbleId = null)
        {
            int maxW = Math.Max(200, _flowMessages.Width - 80);
            int padH = 10, padV = 8;

            SizeF sz;
            using (var g = this.CreateGraphics())
            using (var font = new Font("Segoe UI", 10.5F))
                sz = g.MeasureString(text, font, maxW - padH * 2);

            int bubbleW = (int)sz.Width + padH * 2 + 16;
            int bubbleH = (int)sz.Height + padV * 2 + 4;

            bubbleW = Math.Min(bubbleW, maxW);
            bubbleH = Math.Max(bubbleH, 36);

            var row = new Panel();
            EnableDB(row);
            row.Width     = _flowMessages.Width - 24;
            row.Height    = bubbleH + 24;
            row.BackColor = Color.Transparent;
            row.Margin    = new Padding(0, 3, 0, 3);

            var stateHolder = new[] { state };
            if (isMine && bubbleId != null)
            {
                _bubbleRegistry[bubbleId] = (row, stateHolder);
            }

            row.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int bx = isMine ? row.Width - bubbleW - 8 : 8;
                int by = 4;
                var br = new Rectangle(bx, by, bubbleW, bubbleH);

                if (isMine)
                {
                    using var grad = new LinearGradientBrush(br, ThemeManager.CurrentPrimary, ThemeManager.CurrentPrimaryDark, LinearGradientMode.Vertical);
                    using var path = RR(br, 14);
                    g.FillPath(grad, path);

                    var shineR = new Rectangle(br.X + 2, br.Y + 2, br.Width - 4, br.Height / 2);
                    if (!shineR.IsEmpty)
                    {
                        using var shinePath = RR(shineR, 12);
                        using var shine = new LinearGradientBrush(shineR, Color.FromArgb(40, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), LinearGradientMode.Vertical);
                        g.FillPath(shine, shinePath);
                    }

                    using var font = new Font("Segoe UI", 10.5F);
                    using var fmt  = new StringFormat { Alignment = StringAlignment.Near };
                    g.DrawString(text, font, Brushes.White, new RectangleF(bx + padH, by + padV, bubbleW - padH * 2, bubbleH), fmt);
                }
                else
                {
                    using var path = RR(br, 14);
                    using var bg   = new SolidBrush(ThemeManager.CurrentCard);
                    using var pen  = new Pen(ThemeManager.CurrentBorder, 1f);
                    g.FillPath(bg, path);
                    g.DrawPath(pen, path);

                    using var font = new Font("Segoe UI", 10.5F);
                    using var fmt  = new StringFormat { Alignment = StringAlignment.Near };
                    g.DrawString(text, font, new SolidBrush(ThemeManager.CurrentText), new RectangleF(bx + padH, by + padV, bubbleW - padH * 2, bubbleH), fmt);
                }

                using var tFont = new Font("Segoe UI", 7.5F);
                string ts = time.ToString("h:mm tt");
                
                if (isMine)
                {
                    g.DrawString(ts, tFont, new SolidBrush(ThemeManager.CurrentSubText), new PointF(br.X - 54, br.Bottom - 14));
                    var iconRect = new Rectangle(bx + bubbleW - 18, by + bubbleH + 2, 16, 16);
                    DrawDeliveryIcon(g, iconRect, stateHolder[0]);
                }
                else
                {
                    g.DrawString(ts, tFont, new SolidBrush(ThemeManager.CurrentSubText), new PointF(br.Right + 6, br.Bottom - 14));
                }
            };

            _flowMessages.Controls.Add(row);
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
                    senderId = "admin",
                    receiverId = _activeConvId,
                    messageBody = text,
                    isGroupChat = _activeConvIsGroup
                };
                var res = await ApiService.PostAsync("messages", payload);
                if (res.Success)
                {
                    UpdateBubbleState(bubbleId, MessageDeliveryState.Sent);
                    await Task.Delay(350);
                    UpdateBubbleState(bubbleId, MessageDeliveryState.Delivered);
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
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
    }
}
