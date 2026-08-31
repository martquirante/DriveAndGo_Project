#nullable disable
using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using DriveAndGo_Admin.Helpers;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DriveAndGo_Admin
{
    /// <summary>
    /// Lightweight Pure WebView2 Host Control for ChatOverlay.
    /// Manages SignalR HubConnection & System Tray notifications while hosting the
    /// React 18 + Tailwind Glassmorphism UI in WebView2.
    /// Preserves 100% feature parity with backend APIs & database synchronization.
    /// </summary>
    public class ChatOverlayPanel : UserControl
    {
        private WebView2 _webView;
        private Panel _loadingPanel;
        private Label _loadingLabel;
        private bool _isInitialized = false;
        private HubConnection _hubConnection;
        private readonly NotifyIcon _notifyIcon;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action<bool> OnToggleFullscreenRequested { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action<string> OnSetLayoutModeRequested { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action<string> OnNavigateToAccountRequested { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action<int> OnUnreadCountChanged { get; set; }

        public ChatOverlayPanel()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = ThemeManager.CurrentBackground;

            BuildLoadingScreen();

            _notifyIcon = new NotifyIcon
            {
                Text = "DriveAndGo Admin",
                Icon = SystemIcons.Information,
                Visible = false
            };

            this.HandleCreated += async (s, e) =>
            {
                await InitializeWebViewAsync();
                await InitializeSignalRAsync();
            };

            ThemeManager.ThemeChanged += ThemeChanged_Handler;
            this.Disposed += (s, e) =>
            {
                ThemeManager.ThemeChanged -= ThemeChanged_Handler;
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
                try { _hubConnection?.DisposeAsync().AsTask().Wait(); } catch { }
            };
        }

        private void ThemeChanged_Handler(object sender, EventArgs e)
        {
            this.BackColor = ThemeManager.CurrentBackground;
            if (_webView != null) _webView.DefaultBackgroundColor = ThemeManager.CurrentBackground;
            if (this.IsHandleCreated && !this.IsDisposed)
            {
                ApplyTheme();
            }
        }

        private void BuildLoadingScreen()
        {
            _loadingPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ThemeManager.CurrentBackground
            };
            _loadingLabel = new Label
            {
                Text = "Loading Drive&Go Dispatch Chat…",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(234, 88, 12),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            _loadingPanel.Controls.Add(_loadingLabel);
            _loadingPanel.Resize += (s, e) =>
                _loadingLabel.Location = new Point(
                    (_loadingPanel.Width - _loadingLabel.Width) / 2,
                    (_loadingPanel.Height - _loadingLabel.Height) / 2);

            this.Controls.Add(_loadingPanel);
        }

        private async Task InitializeWebViewAsync()
        {
            if (_isInitialized) return;

            try
            {
                string htmlPath = Helpers.WebAssetHelper.GetWebAssetPath("ChatOverlay.html", "chat");
                string chatFolder = Path.GetDirectoryName(htmlPath) ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "panels", "chat");
                string webAssetsFolder = Path.GetDirectoryName(Path.GetDirectoryName(chatFolder)) ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets");

                _webView = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = ThemeManager.CurrentBackground };
                this.Controls.Add(_webView);
                _webView.BringToFront();

                var userDataFolder = Path.Combine(Path.GetTempPath(), "DriveAndGo_ChatWV2");
                var options = new Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions
                {
                    AdditionalBrowserArguments = "--use-fake-ui-for-media-stream --unsafely-treat-insecure-origin-as-secure=http://chatassets.local --enable-media-stream"
                };
                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
                await _webView.EnsureCoreWebView2Async(env);

                // Configure browser settings
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

                // Explicitly allow native Chromium context menu ("Copy image", "Save image as...", "Copy link address")
                _webView.CoreWebView2.ContextMenuRequested += (sender, args) =>
                {
                    args.Handled = false;
                };

                // Auto-allow Microphone permission for voice recording
                _webView.CoreWebView2.PermissionRequested += (sender, args) =>
                {
                    if (args.PermissionKind == Microsoft.Web.WebView2.Core.CoreWebView2PermissionKind.Microphone)
                    {
                        args.State = Microsoft.Web.WebView2.Core.CoreWebView2PermissionState.Allow;
                    }
                };

                // Virtual Folder Mapping for clean local HTTP origin (http://chatassets.local/)
                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "chatassets.local",
                    webAssetsFolder,
                    CoreWebView2HostResourceAccessKind.Allow);

                string apiBase = ApiService.BaseUrl.TrimEnd('/');
                string token = SessionManager.Token ?? string.Empty;
                string currentTheme = ThemeManager.IsDarkMode ? "dark" : "light";

                string adminAvatarBase64 = string.Empty;
                if (SessionManager.CustomAvatar != null)
                {
                    try
                    {
                        using (var ms = new MemoryStream())
                        {
                            SessionManager.CustomAvatar.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                            adminAvatarBase64 = "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
                        }
                    }
                    catch { }
                }

                // Pre-inject configuration variables before DOM loads
                await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                    $"window.API_BASE_URL = '{apiBase}'; " +
                    $"window.AUTH_TOKEN = '{token}'; " +
                    $"window.ADMIN_AVATAR = '{adminAvatarBase64}'; " +
                    $"document.documentElement.setAttribute('data-theme', '{currentTheme}');");

                _webView.CoreWebView2.NewWindowRequested += (sender, args) =>
                {
                    args.Handled = true;
                    if (!string.IsNullOrWhiteSpace(args.Uri))
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = args.Uri,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ChatOverlayPanel] NewWindowRequested error: {ex.Message}");
                        }
                    }
                };

                _webView.CoreWebView2.NavigationStarting += (sender, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.Uri) &&
                        !args.Uri.StartsWith("http://chatassets.local", StringComparison.OrdinalIgnoreCase) &&
                        !args.Uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase) &&
                        !args.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase))
                    {
                        args.Cancel = true;
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = args.Uri,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ChatOverlayPanel] NavigationStarting error: {ex.Message}");
                        }
                    }
                };

                _webView.CoreWebView2.NavigationCompleted += (sender, args) =>
                {
                    if (_loadingPanel != null) _loadingPanel.Visible = false;
                    ApplyTheme();
                };

                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                // Navigate via virtual host HTTP origin so Babel standalone and React can fetch JSX files without CORS/file:// restrictions
                _webView.CoreWebView2.Navigate("http://chatassets.local/panels/chat/ChatOverlay.html?v=" + DateTime.UtcNow.Ticks);
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatOverlayPanel] WebView2 initialization failed: {ex.Message}");
                if (_loadingLabel != null)
                {
                    _loadingLabel.Text = "Failed to load Chat Interface.";
                    _loadingLabel.ForeColor = Color.Red;
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  SIGNALR HUB CONNECTION & REAL-TIME WEB MESSAGE FORWARDING
        // ════════════════════════════════════════════════════════════════════════
        private async Task InitializeSignalRAsync()
        {
            try
            {
                string baseUrl = ApiService.BaseUrl.Replace("/api", "").TrimEnd('/');
                string hubUrl = baseUrl + "/hubs/admin";

                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(hubUrl)
                    .WithAutomaticReconnect()
                    .Build();

                _hubConnection.On<string, string, string, string, string>(
                    "ReceiveChatMessage",
                    (senderId, receiverId, body, timestamp, messageId) =>
                {
                    if (this.IsDisposed || !this.IsHandleCreated) return;
                    this.BeginInvoke((MethodInvoker)(() =>
                    {
                        PostWebMessage(new
                        {
                            event_name = "ReceiveChatMessage",
                            senderId,
                            receiverId,
                            body,
                            timestamp,
                            messageId
                        });

                        bool isAi = senderId == "@Drive&Go AI" || senderId == "Drive&Go AI" || senderId == "ai_copilot" || (!string.IsNullOrEmpty(senderId) && senderId.Contains("AI"));

                        if (isAi)
                        {
                            NotificationSoundHelper.PlayAiResponseSound();
                            ShowBalloonNotification("Drive&Go AI Response", body);
                        }
                        else if (senderId != "admin")
                        {
                            NotificationSoundHelper.PlayChatReceiveSound();
                            ShowBalloonNotification($"Message from {senderId}", body);
                        }
                    }));
                });

                _hubConnection.On<string, string, string, string>(
                    "MessageStatusChanged",
                    (messageId, status, senderId, receiverId) =>
                {
                    if (this.IsDisposed || !this.IsHandleCreated) return;
                    this.BeginInvoke((MethodInvoker)(() =>
                    {
                        PostWebMessage(new
                        {
                            event_name = "MessageStatusChanged",
                            messageId,
                            status,
                            senderId,
                            receiverId
                        });
                    }));
                });

                _hubConnection.On<string, string, string, string>("MessageEdited", (msgId, newText, history, recId) =>
                {
                    if (this.IsDisposed || !this.IsHandleCreated) return;
                    this.BeginInvoke((MethodInvoker)(() =>
                    {
                        PostWebMessage(new
                        {
                            event_name = "MessageEdited",
                            messageId = msgId,
                            newText,
                            history,
                            receiverId = recId
                        });
                    }));
                });

                _hubConnection.On<string, string>("MessageUnsent", (msgId, recId) =>
                {
                    if (this.IsDisposed || !this.IsHandleCreated) return;
                    this.BeginInvoke((MethodInvoker)(() =>
                    {
                        PostWebMessage(new
                        {
                            event_name = "MessageUnsent",
                            messageId = msgId,
                            receiverId = recId
                        });
                    }));
                });

                _hubConnection.On<string, string, string>("MessageReactionChanged", (msgId, rx, recId) =>
                {
                    if (this.IsDisposed || !this.IsHandleCreated) return;
                    this.BeginInvoke((MethodInvoker)(() =>
                    {
                        PostWebMessage(new
                        {
                            event_name = "MessageReactionChanged",
                            messageId = msgId,
                            reactions = rx,
                            receiverId = recId
                        });
                    }));
                });

                await _hubConnection.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatOverlayPanel] SignalR connection warning: {ex.Message}");
            }
        }

        private void PostWebMessage(object messageObj)
        {
            if (_webView?.CoreWebView2 != null && _isInitialized)
            {
                try
                {
                    string json = JsonSerializer.Serialize(messageObj);
                    _webView.CoreWebView2.PostWebMessageAsJson(json);
                }
                catch { }
            }
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string rawJson = e.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(rawJson)) rawJson = e.WebMessageAsJson;
                if (string.IsNullOrEmpty(rawJson)) return;

                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                string action = null;
                if (root.TryGetProperty("action", out var actProp))
                    action = actProp.GetString();
                else if (root.TryGetProperty("ACTION", out actProp))
                    action = actProp.GetString();
                else if (root.TryGetProperty("type", out var typeProp))
                    action = typeProp.GetString();
                else if (root.TryGetProperty("TYPE", out typeProp))
                    action = typeProp.GetString();

                switch (action)
                {
                    case "open_external_url":
                    case "openUrl":
                    case "open_url":
                        if (root.TryGetProperty("url", out var urlProp))
                        {
                            string extUrl = urlProp.GetString();
                            if (!string.IsNullOrWhiteSpace(extUrl))
                            {
                                try
                                {
                                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                    {
                                        FileName = extUrl,
                                        UseShellExecute = true
                                    });
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[ChatOverlayPanel] Error opening external URL: {ex.Message}");
                                }
                            }
                        }
                        break;
                    case "toggleFullscreen":
                    case "TOGGLE_FULLSCREEN":
                        bool isFullscreen = (root.TryGetProperty("isFullscreen", out var fsProp) && fsProp.GetBoolean()) ||
                                           (root.TryGetProperty("enabled", out var enProp) && enProp.GetBoolean());
                        this.BeginInvoke((MethodInvoker)(() => OnToggleFullscreenRequested?.Invoke(isFullscreen)));
                        break;

                    case "setLayoutMode":
                    case "SET_LAYOUT_MODE":
                        if (root.TryGetProperty("mode", out var modeProp))
                        {
                            string layoutMode = modeProp.GetString();
                            this.BeginInvoke((MethodInvoker)(() => OnSetLayoutModeRequested?.Invoke(layoutMode)));
                        }
                        break;

                    case "navigateToAccount":
                    case "NAVIGATE_TO_ACCOUNT":
                        string custId = root.TryGetProperty("customerId", out var cIdProp) ? cIdProp.GetString() : "";
                        this.BeginInvoke((System.Windows.Forms.MethodInvoker)(() => OnNavigateToAccountRequested?.Invoke(custId)));
                        break;

                    case "updateUnreadCount":
                    case "UPDATE_UNREAD_COUNT":
                        if (root.TryGetProperty("count", out var countProp))
                        {
                            int count = countProp.GetInt32();
                            this.BeginInvoke((MethodInvoker)(() => OnUnreadCountChanged?.Invoke(count)));
                        }
                        break;

                    case "showNotification":
                        if (root.TryGetProperty("title", out var titleProp) && root.TryGetProperty("message", out var msgProp))
                        {
                            ShowBalloonNotification(titleProp.GetString(), msgProp.GetString());
                        }
                        break;

                    case "playAiSound":
                    case "PLAY_AI_SOUND":
                        NotificationSoundHelper.PlayAiResponseSound();
                        break;

                    case "playChatSound":
                    case "PLAY_CHAT_SOUND":
                        NotificationSoundHelper.PlayChatReceiveSound();
                        break;

                    case "log":
                        if (root.TryGetProperty("message", out var logProp))
                        {
                            Console.WriteLine($"[ChatOverlay JS]: {logProp.GetString()}");
                        }
                        break;

                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatOverlayPanel] Error processing WebMessage: {ex.Message}");
            }
        }

        public void ApplyTheme()
        {
            this.BackColor = ThemeManager.CurrentBackground;

            if (_webView?.CoreWebView2 != null && _isInitialized)
            {
                string themeStr = ThemeManager.IsDarkMode ? "dark" : "light";
                _webView.CoreWebView2.ExecuteScriptAsync(
                    $"document.documentElement.setAttribute('data-theme', '{themeStr}'); " +
                    $"if (window.setChatTheme) window.setChatTheme('{themeStr}');");
            }
        }

        private void ShowBalloonNotification(string title, string message)
        {
            try
            {
                if (_notifyIcon == null || this.IsDisposed) return;
                string tipText = message?.Length > 100 ? message.Substring(0, 97) + "..." : (message ?? "");

                _notifyIcon.Visible = true;
                _notifyIcon.ShowBalloonTip(3000, title, tipText, ToolTipIcon.Info);

                var hideTimer = new System.Windows.Forms.Timer { Interval = 4000 };
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
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _webView?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
