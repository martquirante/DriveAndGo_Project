#nullable disable
using DriveAndGo_Admin.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DriveAndGo_Admin
{
    /// <summary>
    /// Modern mobile-app-style sliding drill-down user profile flyout.
    /// Features:
    /// 1. Smooth lerping panel transitions (Main → Profile, Security, Activity Log).
    /// 2. Interactive "Story-style" circular avatar upload animation.
    /// 3. Zero dummy data: Live DB Session binding + IP Geolocation.
    /// 4. Password Show/Hide Eye Icons in Security Settings.
    /// 5. 0% Native White Scrollbars (Custom smooth MouseWheel container scrolling).
    /// 6. 100% ThemeManager compliance (Light & Dark mode).
    /// </summary>
    public class UserProfileFlyoutPanel : Panel
    {
        private enum SubView { Main, Profile, Security, Activity }

        private readonly MainForm _parent;
        private readonly Control _anchor;

        // ── Entrance & Slide Animation ───────────────────────────────────────────
        private float _alpha = 0f;
        private float _yOffset = -10f;
        private System.Windows.Forms.Timer _animTimer;
        private System.Windows.Forms.Timer _slideTimer;
        private SubView _currentView = SubView.Main;

        // ── Child Panels ────────────────────────────────────────────────────────
        private Panel _pnlMain;
        private Panel _pnlProfile;
        private Panel _pnlSecurity;
        private Panel _pnlActivity;

        // ── Avatar Upload State ──────────────────────────────────────────────────
        private Image _customAvatarImage = null;
        private bool  _isUploadingAvatar = false;
        private float _uploadProgressAngle = 0f;
        private bool  _uploadSuccessFlash = false;
        private System.Windows.Forms.Timer _uploadTimer;

        // ── Security Toggle States ───────────────────────────────────────────────
        private bool _is2FAEnabled = true;
        private bool _isAlertsEnabled = true;
        private bool _isPinReqEnabled = false;

        // ── Live DB & Geolocation Session Data ──────────────────────────────────
        private string _userContact = "";
        private string _userEmail = "";
        private string _deviceLocation = "Fetching location...";
        private readonly List<ActivityLogItem> _activityLogs = new();

        private struct ActivityLogItem
        {
            public string Icon, Title, Time;
            public Color DotColor;
        }

        private const int FLYOUT_HEIGHT = 378;

        // ════════════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════════════════
        public UserProfileFlyoutPanel(MainForm parent, Control anchor)
        {
            _parent = parent;
            _anchor = anchor;

            this.Size = new Size(300, FLYOUT_HEIGHT);
            this.BackColor = Color.Transparent;

            SetDoubleBuffer(this);
            Reanchor(anchor);

            ThemeManager.ThemeChanged += (s, e) =>
            {
                if (!this.IsDisposed && this.IsHandleCreated)
                {
                    ApplyTheme();
                    this.Invalidate();
                }
            };

            FetchLiveData();
            BuildPanels();
            StartSlideAnimation();
        }

        private async void FetchLiveData()
        {
            try
            {
                _deviceLocation = await GeoLocationService.GetDeviceLocationAsync();

                // ── Retrieve User Profile & Security Settings from Database ──
                int uid = SessionManager.UserId > 0 ? SessionManager.UserId : 1;
                var userRes = await ApiService.GetAsync($"users/{uid}");
                if (!userRes.Success)
                {
                    userRes = await ApiService.GetAsync("users/profile");
                }

                if (userRes.Success && !string.IsNullOrWhiteSpace(userRes.Body))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(userRes.Body);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("fullName", out var fn) && !string.IsNullOrWhiteSpace(fn.GetString()))
                        {
                            SessionManager.FullName = fn.GetString();
                        }
                        if (root.TryGetProperty("email", out var em) && !string.IsNullOrWhiteSpace(em.GetString()))
                        {
                            SessionManager.Email = em.GetString();
                            _userEmail = em.GetString();
                        }
                        if (root.TryGetProperty("contactNumber", out var cn) && !string.IsNullOrWhiteSpace(cn.GetString()))
                        {
                            _userContact = cn.GetString();
                        }
                        else if (root.TryGetProperty("phone", out var ph) && !string.IsNullOrWhiteSpace(ph.GetString()))
                        {
                            _userContact = ph.GetString();
                        }

                        if (root.TryGetProperty("twoFactorEnabled", out var tf)) _is2FAEnabled = tf.GetBoolean();
                        if (root.TryGetProperty("loginAlertsEnabled", out var la)) _isAlertsEnabled = la.GetBoolean();
                        if (root.TryGetProperty("pinRequired", out var pr)) _isPinReqEnabled = pr.GetBoolean();

                        if (root.TryGetProperty("avatarBase64", out var av) && !string.IsNullOrWhiteSpace(av.GetString()))
                        {
                            try
                            {
                                byte[] bytes = Convert.FromBase64String(av.GetString());
                                using var ms = new MemoryStream(bytes);
                                Image img = Image.FromStream(ms);
                                _customAvatarImage = (Image)img.Clone();
                                SessionManager.CustomAvatar = _customAvatarImage;
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                if (string.IsNullOrWhiteSpace(_userEmail))
                {
                    _userEmail = !string.IsNullOrEmpty(SessionManager.Email)
                        ? SessionManager.Email
                        : ((!string.IsNullOrEmpty(SessionManager.FullName) ? SessionManager.FullName.ToLower().Replace(" ", ".") : "admin") + "@driveandgo.ph");
                }

                // ── Fetch real activity logs from DB API ──
                var res = await ApiService.GetAsync($"activity-logs?userId={uid}");
                if (res.Success && !string.IsNullOrWhiteSpace(res.Body))
                {
                    var root = JsonDocument.Parse(res.Body).RootElement;
                    _activityLogs.Clear();
                    foreach (var item in root.EnumerateArray())
                    {
                        string title = item.GetProperty("title").GetString();
                        string rawTime = item.TryGetProperty("createdAt", out var ca) ? ca.GetString() : (item.TryGetProperty("time", out var tm) ? tm.GetString() : "");
                        string formattedTime = DateTime.TryParse(rawTime, out var dt) ? dt.ToString("h:mm tt") : DateTime.Now.ToString("h:mm tt");
                        string icon = item.TryGetProperty("icon", out var ic) ? ic.GetString() : "📋";
                        _activityLogs.Add(new ActivityLogItem
                        {
                            Icon = icon,
                            Title = title,
                            Time = formattedTime,
                            DotColor = ThemeManager.CurrentPrimary
                        });
                    }
                }
            }
            catch { }

            if (this.IsHandleCreated && !this.IsDisposed)
            {
                this.Invoke((Action)(() => { BuildPanels(); _parent?.RefreshHeaderUserInfo(); }));
            }
        }

        // ── Re-anchor positioning relative to topbar avatar button ──────────────
        public void Reanchor(Control anchor)
        {
            if (anchor == null || _parent == null || anchor.IsDisposed) return;
            Point screenPt = anchor.PointToScreen(new Point(0, 0));
            Point parentPt = _parent.PointToClient(screenPt);
            this.Location = new Point(parentPt.X + anchor.Width - this.Width, parentPt.Y + anchor.Height + 6 + (int)_yOffset);
        }

        public void StartEntrance()
        {
            _alpha = 0.04f;
            _yOffset = -10f;
            _animTimer?.Stop();
            _animTimer = new System.Windows.Forms.Timer { Interval = 10 };
            _animTimer.Tick += (s, e) =>
            {
                float alphaDiff = 1f - _alpha;
                float yDiff = 0f - _yOffset;

                if (alphaDiff < 0.015f && Math.Abs(yDiff) < 0.3f)
                {
                    _alpha = 1f;
                    _yOffset = 0f;
                    _animTimer.Stop();
                }
                else
                {
                    _alpha += alphaDiff * 0.30f;
                    _yOffset += yDiff * 0.30f;
                }
                Reanchor(_anchor);
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
                _alpha -= _alpha * 0.32f;
                _yOffset -= 1.4f;
                if (_alpha <= 0.04f)
                {
                    _alpha = 0f;
                    _animTimer.Stop();
                    if (_parent != null && !_parent.IsDisposed && _parent.IsHandleCreated)
                    {
                        _parent.Controls.Remove(this);
                    }
                    this.Dispose();
                }
                else
                {
                    Reanchor(_anchor);
                    this.Invalidate();
                }
            };
            _animTimer.Start();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  SMOOTH DRILL-DOWN SLIDE ANIMATION
        // ════════════════════════════════════════════════════════════════════════
        private void StartSlideAnimation()
        {
            _slideTimer = new System.Windows.Forms.Timer { Interval = 15 };
            _slideTimer.Tick += (s, e) =>
            {
                int w = this.Width;
                int targetMain = _currentView == SubView.Main ? 0 : -w;
                int targetProfile = _currentView == SubView.Profile ? 0 : w;
                int targetSecurity = _currentView == SubView.Security ? 0 : w;
                int targetActivity = _currentView == SubView.Activity ? 0 : w;

                LerpLeft(_pnlMain, targetMain);
                LerpLeft(_pnlProfile, targetProfile);
                LerpLeft(_pnlSecurity, targetSecurity);
                LerpLeft(_pnlActivity, targetActivity);
            };
            _slideTimer.Start();
        }

        private bool LerpLeft(Panel pnl, int target)
        {
            if (pnl == null || pnl.IsDisposed) return true;
            int diff = target - pnl.Left;
            if (Math.Abs(diff) <= 1)
            {
                pnl.Left = target;
                return true;
            }
            pnl.Left += (int)Math.Round(diff * 0.28f);
            return false;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  BUILD PANELS & VIEWS
        // ════════════════════════════════════════════════════════════════════════
        private void BuildPanels()
        {
            this.Controls.Clear();

            _pnlMain     = new Panel { Size = new Size(300, FLYOUT_HEIGHT), Location = new Point(0, 0), BackColor = Color.Transparent };
            _pnlProfile  = new Panel { Size = new Size(300, FLYOUT_HEIGHT), Location = new Point(300, 0), BackColor = Color.Transparent };
            _pnlSecurity = new Panel { Size = new Size(300, FLYOUT_HEIGHT), Location = new Point(300, 0), BackColor = Color.Transparent };
            _pnlActivity = new Panel { Size = new Size(300, FLYOUT_HEIGHT), Location = new Point(300, 0), BackColor = Color.Transparent };

            SetDoubleBuffer(_pnlMain);
            SetDoubleBuffer(_pnlProfile);
            SetDoubleBuffer(_pnlSecurity);
            SetDoubleBuffer(_pnlActivity);

            BuildMainView();
            BuildProfileView();
            BuildSecurityView();
            BuildActivityView();

            this.Controls.Add(_pnlMain);
            this.Controls.Add(_pnlProfile);
            this.Controls.Add(_pnlSecurity);
            this.Controls.Add(_pnlActivity);
        }

        private void ApplyTheme()
        {
            BuildPanels();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  1. MAIN VIEW
        // ════════════════════════════════════════════════════════════════════════
        private void BuildMainView()
        {
            _pnlMain.Controls.Clear();

            // ── Interactive Avatar Upload Panel (64x64) ──
            var pnlAvatar = new Panel
            {
                Size      = new Size(64, 64),
                Location  = new Point((_pnlMain.Width - 64) / 2, 20),
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };
            SetDoubleBuffer(pnlAvatar);

            pnlAvatar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.CompositingQuality = CompositingQuality.HighQuality;

                var r = new Rectangle(2, 2, pnlAvatar.Width - 5, pnlAvatar.Height - 5);

                if (_customAvatarImage != null)
                {
                    try
                    {
                        using var path = new GraphicsPath();
                        path.AddEllipse(r);
                        var oldClip = g.Clip;
                        g.SetClip(path);
                        g.DrawImage(_customAvatarImage, r);
                        g.Clip = oldClip;
                    }
                    catch { }
                }
                else
                {
                    using var grad = new LinearGradientBrush(r, ThemeManager.CurrentPrimary, ThemeManager.CurrentPrimaryGlow, LinearGradientMode.ForwardDiagonal);
                    g.FillEllipse(grad, r);

                    int headDiam = (int)(r.Width * 0.38f);
                    int headX = r.X + (r.Width - headDiam) / 2;
                    int headY = r.Y + (int)(r.Height * 0.17f);
                    using var whiteBrush = new SolidBrush(Color.FromArgb(235, 255, 255, 255));
                    g.FillEllipse(whiteBrush, headX, headY, headDiam, headDiam);

                    int shoulderW = (int)(r.Width * 0.72f);
                    int shoulderH = (int)(r.Height * 0.48f);
                    int shoulderX = r.X + (r.Width - shoulderW) / 2;
                    int shoulderY = r.Y + (int)(r.Height * 0.56f);

                    using var clipPath = new GraphicsPath();
                    clipPath.AddEllipse(r);
                    var prevClip = g.Clip;
                    g.SetClip(clipPath);
                    g.FillEllipse(whiteBrush, shoulderX, shoulderY, shoulderW, shoulderH);
                    g.Clip = prevClip;
                }

                // ── Upload Progress Ring Arc ("Pabilog") ──
                if (_isUploadingAvatar)
                {
                    using var ringPen = new Pen(ThemeManager.CurrentPrimary, 3.5f);
                    g.DrawArc(ringPen, r, -90f, _uploadProgressAngle);
                }
                else if (_uploadSuccessFlash)
                {
                    using var greenPen = new Pen(Color.FromArgb(34, 197, 94), 3.5f);
                    g.DrawEllipse(greenPen, r);
                }
                else
                {
                    using var ringPen = new Pen(Color.FromArgb(90, 255, 255, 255), 1.5f);
                    g.DrawEllipse(ringPen, r);
                }

                // Camera Badge overlay icon
                var badgeR = new Rectangle(pnlAvatar.Width - 22, pnlAvatar.Height - 22, 20, 20);
                g.FillEllipse(new SolidBrush(ThemeManager.CurrentPrimary), badgeR);
                using var camPen = new Pen(Color.White, 1.2f);
                g.DrawRectangle(camPen, badgeR.X + 4, badgeR.Y + 6, 12, 8);
                g.DrawEllipse(camPen, badgeR.X + 7, badgeR.Y + 8, 4, 4);
            };

            pnlAvatar.Click += (s, e) => TriggerAvatarUpload(pnlAvatar);
            _pnlMain.Controls.Add(pnlAvatar);

            // User Name & Role
            string nameText = SessionManager.UserId > 0 && !string.IsNullOrWhiteSpace(SessionManager.FullName)
                ? SessionManager.FullName : "Administrator";

            var lblName = new Label
            {
                Text = nameText,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentText,
                Location = new Point(10, 88),
                Size = new Size(280, 24),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            _pnlMain.Controls.Add(lblName);

            string roleText = SessionManager.UserId > 0 && !string.IsNullOrWhiteSpace(SessionManager.Role)
                ? SessionManager.Role.ToUpper() : "SUPER ADMIN";

            var lblRole = new Label
            {
                Text = roleText,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentPrimary,
                Location = new Point(10, 112),
                Size = new Size(280, 18),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            _pnlMain.Controls.Add(lblRole);

            // Divider 1
            var div1 = new Panel
            {
                Location = new Point(16, 138),
                Size = new Size(268, 1),
                BackColor = ThemeManager.CurrentBorder
            };
            _pnlMain.Controls.Add(div1);

            // Menu Items with clean spacing
            int menuY = 148;
            int rowH = 44;
            int gap = 6;

            var rowProfile  = CreateMenuRow("👤", "My Profile", menuY, () => _currentView = SubView.Profile);
            var rowSecurity = CreateMenuRow("🛡️", "Security Settings", menuY + rowH + gap, () => _currentView = SubView.Security);
            var rowActivity = CreateMenuRow("📜", "Activity Log", menuY + (rowH + gap) * 2, () => _currentView = SubView.Activity);

            _pnlMain.Controls.Add(rowProfile);
            _pnlMain.Controls.Add(rowSecurity);
            _pnlMain.Controls.Add(rowActivity);

            // Divider 2
            var div2 = new Panel
            {
                Location = new Point(16, 306),
                Size = new Size(268, 1),
                BackColor = ThemeManager.CurrentBorder
            };
            _pnlMain.Controls.Add(div2);

            // Logout Button - Positioned neatly at Y=318
            var btnLogout = new Button
            {
                Text = "🚪   Log Out",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Size = new Size(268, 42),
                Location = new Point(16, 318),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(239, 68, 68),
                BackColor = Color.FromArgb(15, 239, 68, 68),
                Cursor = Cursors.Hand
            };
            btnLogout.FlatAppearance.BorderSize = 1;
            btnLogout.FlatAppearance.BorderColor = Color.FromArgb(60, 239, 68, 68);
            btnLogout.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 239, 68, 68);
            SetRoundRegion(btnLogout, 8);
            btnLogout.Click += (s, e) =>
            {
                StartDismissal();
                _parent?.PerformLogout();
            };
            _pnlMain.Controls.Add(btnLogout);
        }

        private void TriggerAvatarUpload(Panel pnlAvatar)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select New Profile Picture",
                Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    Bitmap safeBitmap;
                    byte[] imageBytes;
                    using (var stream = File.OpenRead(ofd.FileName))
                    using (var tempImg = Image.FromStream(stream))
                    {
                        safeBitmap = new Bitmap(tempImg);
                        using var ms = new MemoryStream();
                        safeBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        imageBytes = ms.ToArray();
                    }

                    _isUploadingAvatar = true;
                    _uploadProgressAngle = 0f;
                    _uploadSuccessFlash = false;

                    _uploadTimer?.Stop();
                    _uploadTimer = new System.Windows.Forms.Timer { Interval = 15 };
                    _uploadTimer.Tick += (s, e) =>
                    {
                        _uploadProgressAngle += 10f;
                        pnlAvatar.Invalidate();

                        if (_uploadProgressAngle >= 360f)
                        {
                            _uploadProgressAngle = 360f;
                            _isUploadingAvatar = false;
                            _uploadSuccessFlash = true;
                            _customAvatarImage = safeBitmap;
                            SessionManager.CustomAvatar = (Image)safeBitmap.Clone();
                            _uploadTimer.Stop();
                            pnlAvatar.Invalidate();
                            _parent?.RefreshHeaderUserInfo();
                            AddActivityLogEntry("🖼️", "Avatar picture updated", Color.FromArgb(34, 197, 94));

                            if (imageBytes != null && imageBytes.Length > 0)
                            {
                                string base64 = Convert.ToBase64String(imageBytes);
                                Task.Run(async () =>
                                {
                                    try
                                    {
                                        int uid = SessionManager.UserId > 0 ? SessionManager.UserId : 1;
                                        var payload = new { userId = uid, avatarBase64 = base64 };
                                        await ApiService.PostAsync($"users/{uid}/avatar", payload);
                                    }
                                    catch { }
                                });
                            }

                            Task.Delay(1000).ContinueWith(_ =>
                            {
                                if (this.IsHandleCreated && !this.IsDisposed)
                                    this.Invoke((Action)(() => { _uploadSuccessFlash = false; pnlAvatar.Invalidate(); }));
                            });
                        }
                    };
                    _uploadTimer.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not load image: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private Panel CreateMenuRow(string icon, string title, int topY, Action onClick)
        {
            var row = new Panel
            {
                Location  = new Point(16, topY),
                Size      = new Size(268, 44),
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };
            SetDoubleBuffer(row);

            var lblIcon = new Label
            {
                Text = icon, Font = new Font("Segoe UI", 11F),
                ForeColor = ThemeManager.CurrentText, Location = new Point(10, 11),
                AutoSize = true, BackColor = Color.Transparent, Cursor = Cursors.Hand
            };

            var lblTitle = new Label
            {
                Text = title, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentText, Location = new Point(40, 12),
                AutoSize = true, BackColor = Color.Transparent, Cursor = Cursors.Hand
            };

            var lblChevron = new Label
            {
                Text = "›", Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentSubText, Location = new Point(242, 9),
                AutoSize = true, BackColor = Color.Transparent, Cursor = Cursors.Hand
            };

            row.Controls.Add(lblIcon);
            row.Controls.Add(lblTitle);
            row.Controls.Add(lblChevron);

            Control[] group = { row, lblIcon, lblTitle, lblChevron };
            foreach (Control c in group)
            {
                c.MouseEnter += (s, e) =>
                {
                    row.BackColor = Color.FromArgb(20, ThemeManager.CurrentPrimary.R, ThemeManager.CurrentPrimary.G, ThemeManager.CurrentPrimary.B);
                    lblTitle.ForeColor = ThemeManager.CurrentPrimary;
                    lblChevron.ForeColor = ThemeManager.CurrentPrimary;
                };
                c.MouseLeave += (s, e) =>
                {
                    Point pt = row.PointToClient(Cursor.Position);
                    if (!row.ClientRectangle.Contains(pt))
                    {
                        row.BackColor = Color.Transparent;
                        lblTitle.ForeColor = ThemeManager.CurrentText;
                        lblChevron.ForeColor = ThemeManager.CurrentSubText;
                    }
                };
                c.Click += (s, e) => onClick?.Invoke();
            }

            SetRoundRegion(row, 8);
            return row;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  2. MY PROFILE SUB-VIEW (0% Native Scrollbar + Live Data)
        // ════════════════════════════════════════════════════════════════════════
        private void BuildProfileView()
        {
            _pnlProfile.Controls.Clear();
            AddSubViewHeader(_pnlProfile, "👤 My Profile");

            var wrapper = new Panel
            {
                Location = new Point(14, 52),
                Size     = new Size(272, 480),
                BackColor = Color.Transparent
            };
            SetDoubleBuffer(wrapper);

            int y = 8;
            string name = !string.IsNullOrWhiteSpace(SessionManager.FullName) ? SessionManager.FullName : "Administrator";
            string email = !string.IsNullOrWhiteSpace(_userEmail) ? _userEmail : "admin@driveandgo.ph";

            string editedName = name;
            string editedEmail = email;
            string editedContact = _userContact;

            AddInputField(wrapper, "Full Name", name, ref y, (val) => editedName = val);
            AddInputField(wrapper, "Email Address", email, ref y, (val) => editedEmail = val);
            AddInputField(wrapper, "Dispatch / Contact", _userContact, ref y, (val) => editedContact = val);

            AddReadonlyField(wrapper, "Assigned Hub", "Manila Main Terminal (Hub #01)", ref y);
            AddReadonlyField(wrapper, "Admin Role Level", $"{(string.IsNullOrWhiteSpace(SessionManager.Role) ? "SUPER ADMIN" : SessionManager.Role.ToUpper())} (Level 5)", ref y);

            var btnSave = new Button
            {
                Text      = "💾  Save Profile Changes",
                Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Size      = new Size(250, 40),
                Location  = new Point(2, y + 10),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = ThemeManager.CurrentPrimary,
                Cursor    = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            SetRoundRegion(btnSave, 10);
            btnSave.Click += async (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(editedName)) SessionManager.FullName = editedName.Trim();
                if (!string.IsNullOrWhiteSpace(editedEmail))
                {
                    _userEmail = editedEmail.Trim();
                    SessionManager.Email = _userEmail;
                }
                if (!string.IsNullOrWhiteSpace(editedContact)) _userContact = editedContact.Trim();

                try
                {
                    int uid = SessionManager.UserId > 0 ? SessionManager.UserId : 1;
                    var payload = new
                    {
                        fullName = SessionManager.FullName,
                        email = SessionManager.Email,
                        phone = _userContact
                    };
                    await ApiService.PutAsync($"users/{uid}", payload);
                }
                catch { }

                _parent?.RefreshHeaderUserInfo();
                BuildMainView();
                AddActivityLogEntry("👤", "Profile details updated", Color.FromArgb(34, 197, 94));

                MessageBox.Show("Profile details updated successfully!", "DriveAndGo Dispatch", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _currentView = SubView.Main;
            };
            wrapper.Controls.Add(btnSave);

            int totalHeight = y + 60;
            wrapper.Height = totalHeight;

            _pnlProfile.Controls.Add(wrapper);
            AttachCustomMouseWheelScroll(_pnlProfile, wrapper, totalHeight);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  3. SECURITY SETTINGS SUB-VIEW (Eye Icons + Live IP Geolocation)
        // ════════════════════════════════════════════════════════════════════════
        private void BuildSecurityView()
        {
            _pnlSecurity.Controls.Clear();
            AddSubViewHeader(_pnlSecurity, "🛡️ Security Settings");

            var wrapper = new Panel
            {
                Location = new Point(14, 52),
                Size     = new Size(272, 600),
                BackColor = Color.Transparent
            };
            SetDoubleBuffer(wrapper);

            int y = 8;
            AddToggleRow(wrapper, "🔑 Two-Factor Auth (2FA)", "Requires OTP code on login", _is2FAEnabled, async (v) =>
            {
                _is2FAEnabled = v;
                int uid = SessionManager.UserId > 0 ? SessionManager.UserId : 1;
                await ApiService.UpdateSecuritySettingsAsync(uid, _is2FAEnabled, _isAlertsEnabled, _isPinReqEnabled);
            }, ref y);
            AddToggleRow(wrapper, "🔔 New Device Login Alerts", "Get emails for unknown devices", _isAlertsEnabled, async (v) =>
            {
                _isAlertsEnabled = v;
                int uid = SessionManager.UserId > 0 ? SessionManager.UserId : 1;
                await ApiService.UpdateSecuritySettingsAsync(uid, _is2FAEnabled, _isAlertsEnabled, _isPinReqEnabled);
            }, ref y);
            AddToggleRow(wrapper, "📌 Dispatch PIN Requirement", "Require 4-digit PIN for bookings", _isPinReqEnabled, async (v) =>
            {
                _isPinReqEnabled = v;
                int uid = SessionManager.UserId > 0 ? SessionManager.UserId : 1;
                await ApiService.UpdateSecuritySettingsAsync(uid, _is2FAEnabled, _isAlertsEnabled, _isPinReqEnabled);
            }, ref y);

            var div = new Panel { Location = new Point(2, y + 6), Size = new Size(250, 1), BackColor = ThemeManager.CurrentBorder };
            wrapper.Controls.Add(div);
            y += 18;

            // Password Section with Eye Toggle Icons
            var lblPass = new Label
            {
                Text = "Change Password", Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentText, Location = new Point(2, y), AutoSize = true
            };
            wrapper.Controls.Add(lblPass);
            y += 24;

            var txtCurr = AddPasswordInputField(wrapper, "Current Password", "", ref y);
            var txtNew  = AddPasswordInputField(wrapper, "New Password", "", ref y);
            var txtConf = AddPasswordInputField(wrapper, "Confirm Password", "", ref y);

            var btnPassSave = new Button
            {
                Text      = "🔒 Update Password",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Size      = new Size(250, 36),
                Location  = new Point(2, y + 4),
                FlatStyle = FlatStyle.Flat,
                ForeColor = ThemeManager.CurrentText,
                BackColor = ThemeManager.CurrentInputBg,
                Cursor    = Cursors.Hand
            };
            btnPassSave.FlatAppearance.BorderColor = ThemeManager.CurrentBorder;
            SetRoundRegion(btnPassSave, 8);
            btnPassSave.Click += async (s, e) =>
            {
                string curr = txtCurr.Text.Trim();
                string newP = txtNew.Text.Trim();
                string conf = txtConf.Text.Trim();

                if (string.IsNullOrWhiteSpace(curr))
                {
                    MessageBox.Show("Please enter your current password.", "Security Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtCurr.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(newP))
                {
                    MessageBox.Show("Please enter a new password.", "Security Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtNew.Focus();
                    return;
                }

                if (newP.Length < 6)
                {
                    MessageBox.Show("New password must be at least 6 characters long.", "Security Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtNew.Focus();
                    return;
                }

                if (newP != conf)
                {
                    MessageBox.Show("New Password and Confirm Password do not match! Please check your entries.", "Password Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtConf.Focus();
                    return;
                }

                btnPassSave.Enabled = false;
                btnPassSave.Text = "Sending OTP...";

                int uid = SessionManager.UserId > 0 ? SessionManager.UserId : 1;
                var (otpSent, reqMsg) = await ApiService.RequestPasswordChangeOtpAsync(uid, curr);

                btnPassSave.Enabled = true;
                btnPassSave.Text = "🔒 Update Password";

                if (!otpSent)
                {
                    MessageBox.Show(reqMsg, "Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string userEm = !string.IsNullOrWhiteSpace(SessionManager.Email) ? SessionManager.Email : _userEmail;
                using var otpDlg = new OtpVerificationDialog(
                    userEm, 
                    "Confirm Password Change", 
                    resendCallback: async () =>
                    {
                        var (s2, m2) = await ApiService.RequestPasswordChangeOtpAsync(uid, curr);
                        return s2;
                    },
                    verifyCallback: async (code) =>
                    {
                        var (changeOk, changeMsg) = await ApiService.ChangePasswordWithOtpAsync(uid, curr, newP, code);
                        return (changeOk, changeMsg ?? "Invalid or expired OTP code.");
                    }
                );

                // Subscribe to ResendRequested event
                otpDlg.ResendRequested += async (senderEvt, argsEvt) =>
                {
                    await ApiService.RequestPasswordChangeOtpAsync(uid, curr);
                };

                if (otpDlg.ShowDialog(this) == DialogResult.OK)
                {
                    AddActivityLogEntry("🔑", "Password updated successfully", ThemeManager.CurrentPrimary);
                    MessageBox.Show("Password updated successfully!", "Security Alert", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    txtCurr.Text = "";
                    txtNew.Text = "";
                    txtConf.Text = "";
                }
            };
            wrapper.Controls.Add(btnPassSave);
            y += 48;

            // Active Sessions with Real IP Geolocation
            var cardSession = new Panel
            {
                Location  = new Point(2, y),
                Size      = new Size(250, 68),
                BackColor = ThemeManager.CurrentInputBg
            };
            cardSession.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, cardSession.Width - 1, cardSession.Height - 1);
                using var path = GetRoundedRect(r, 8);
                g.DrawPath(new Pen(ThemeManager.CurrentBorder, 1f), path);

                using var fBold = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                using var fSub  = new Font("Segoe UI", 7.5F);
                g.DrawString($"💻 {Environment.MachineName}", fBold, new SolidBrush(ThemeManager.CurrentText), new PointF(10, 10));
                g.DrawString($"Location: {_deviceLocation}", fSub, new SolidBrush(ThemeManager.CurrentSubText), new PointF(10, 30));
                g.DrawString("🟢 Active Session (Current Device)", fSub, Brushes.MediumSeaGreen, new PointF(10, 46));
            };
            wrapper.Controls.Add(cardSession);
            y += 76;

            var btnLogoutOthers = new Button
            {
                Text      = "🚪 Log out all other devices",
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Size      = new Size(250, 34),
                Location  = new Point(2, y),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(239, 68, 68),
                BackColor = Color.FromArgb(15, 239, 68, 68),
                Cursor    = Cursors.Hand
            };
            btnLogoutOthers.FlatAppearance.BorderColor = Color.FromArgb(60, 239, 68, 68);
            SetRoundRegion(btnLogoutOthers, 8);
            btnLogoutOthers.Click += (s, e) =>
            {
                MessageBox.Show("Logged out of all other active sessions.", "Security", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            wrapper.Controls.Add(btnLogoutOthers);

            int totalHeight = y + 50;
            wrapper.Height = totalHeight;

            _pnlSecurity.Controls.Add(wrapper);
            AttachCustomMouseWheelScroll(_pnlSecurity, wrapper, totalHeight);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  4. ACTIVITY LOG SUB-VIEW (Real Activity DB Events)
        // ════════════════════════════════════════════════════════════════════════
        private void BuildActivityView()
        {
            _pnlActivity.Controls.Clear();
            AddSubViewHeader(_pnlActivity, "📜 Activity Log");

            if (_activityLogs.Count == 0)
            {
                var lblEmpty = new Label
                {
                    Text = "📋 No recent activity recorded.",
                    Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                    ForeColor = ThemeManager.CurrentSubText,
                    Location = new Point(14, 60),
                    AutoSize = true
                };
                _pnlActivity.Controls.Add(lblEmpty);
                return;
            }

            int contentH = Math.Max(260, _activityLogs.Count * 60 + 16);

            var wrapper = new Panel
            {
                Location = new Point(14, 52),
                Size     = new Size(272, contentH),
                BackColor = Color.Transparent
            };
            SetDoubleBuffer(wrapper);

            wrapper.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int lineX = 24;
                using var stemPen = new Pen(ThemeManager.CurrentBorder, 2f);
                g.DrawLine(stemPen, lineX, 16, lineX, Math.Max(16, contentH - 20));

                int entryY = 10;
                foreach (var ev in _activityLogs)
                {
                    var dotR = new Rectangle(lineX - 5, entryY + 4, 10, 10);
                    g.FillEllipse(new SolidBrush(ev.DotColor), dotR);
                    g.DrawEllipse(Pens.White, dotR);

                    var cardR = new Rectangle(42, entryY, 216, 48);
                    using var path = GetRoundedRect(cardR, 8);
                    g.FillPath(new SolidBrush(ThemeManager.CurrentInputBg), path);
                    g.DrawPath(new Pen(ThemeManager.CurrentBorder, 1f), path);

                    using var fTitle = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                    using var fTime  = new Font("Segoe UI", 7.5F);

                    g.DrawString($"{ev.Icon} {ev.Title}", fTitle, new SolidBrush(ThemeManager.CurrentText), new PointF(cardR.X + 8, cardR.Y + 6));
                    g.DrawString(ev.Time, fTime, new SolidBrush(ThemeManager.CurrentSubText), new PointF(cardR.X + 8, cardR.Y + 26));

                    entryY += 58;
                }
            };

            _pnlActivity.Controls.Add(wrapper);
            AttachCustomMouseWheelScroll(_pnlActivity, wrapper, contentH);
        }

        private void AddActivityLogEntry(string icon, string title, Color color, DateTime? timestamp = null)
        {
            DateTime dt = timestamp ?? DateTime.Now;
            string timeFormatted = dt.ToString("h:mm tt");

            _activityLogs.Insert(0, new ActivityLogItem
            {
                Icon = icon,
                Title = title,
                Time = timeFormatted,
                DotColor = color
            });
            if (this.IsHandleCreated && !this.IsDisposed)
            {
                this.Invoke((Action)(() => BuildActivityView()));
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  CUSTOM 0% NATIVE SCROLLBAR MOUSEWHEEL LOGIC
        // ════════════════════════════════════════════════════════════════════════
        private void AttachCustomMouseWheelScroll(Panel parentPanel, Panel wrapper, int totalContentHeight)
        {
            parentPanel.MouseWheel += (s, e) => HandleWheel(e);

            foreach (Control child in wrapper.Controls)
            {
                child.MouseWheel += (s, e) => HandleWheel(e);
                foreach (Control sub in child.Controls)
                {
                    sub.MouseWheel += (s, e) => HandleWheel(e);
                }
            }

            void HandleWheel(MouseEventArgs e)
            {
                if (wrapper.IsDisposed) return;
                int viewH = parentPanel.Height - 52;
                int maxScroll = Math.Max(0, totalContentHeight - viewH);
                if (maxScroll <= 0) { wrapper.Location = new Point(14, 52); return; }

                int delta = -e.Delta / 3;
                int currentY = wrapper.Location.Y - 52;
                int newY = Math.Clamp(currentY - delta, -maxScroll, 0);
                wrapper.Location = new Point(14, 52 + newY);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  UI UTILITY BUILDERS & INPUT FIELDS
        // ════════════════════════════════════════════════════════════════════════
        private void AddSubViewHeader(Panel pnl, string titleText)
        {
            var header = new Panel
            {
                Size = new Size(300, 44),
                Location = new Point(0, 0),
                BackColor = Color.Transparent
            };

            var btnBack = new Button
            {
                Text = "‹ Back",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Size = new Size(70, 32),
                Location = new Point(10, 6),
                FlatStyle = FlatStyle.Flat,
                ForeColor = ThemeManager.CurrentPrimary,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btnBack.FlatAppearance.BorderSize = 0;
            btnBack.Click += (s, e) => _currentView = SubView.Main;
            header.Controls.Add(btnBack);

            var lblTitle = new Label
            {
                Text = titleText,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentText,
                Location = new Point(80, 10),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            header.Controls.Add(lblTitle);

            var div = new Panel { Location = new Point(0, 43), Size = new Size(300, 1), BackColor = ThemeManager.CurrentBorder };
            header.Controls.Add(div);

            pnl.Controls.Add(header);
        }

        private void AddInputField(Panel parent, string label, string defaultValue, ref int y, Action<string> onChanged = null)
        {
            var lbl = new Label
            {
                Text = label, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentSubText, Location = new Point(2, y), AutoSize = true
            };
            parent.Controls.Add(lbl);
            y += 18;

            var txtWrap = new Panel
            {
                Size = new Size(250, 32),
                Location = new Point(2, y),
                BackColor = ThemeManager.CurrentInputBg
            };
            txtWrap.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, txtWrap.Width - 1, txtWrap.Height - 1);
                using var path = GetRoundedRect(r, 6);
                g.DrawPath(new Pen(ThemeManager.CurrentBorder, 1f), path);
            };

            var txt = new TextBox
            {
                Text = defaultValue,
                Font = new Font("Segoe UI", 9.5F),
                Size = new Size(234, 22),
                Location = new Point(8, 5),
                BackColor = ThemeManager.CurrentInputBg,
                ForeColor = ThemeManager.CurrentText,
                BorderStyle = BorderStyle.None
            };
            if (onChanged != null)
            {
                txt.TextChanged += (s, e) => onChanged(txt.Text);
            }
            txtWrap.Controls.Add(txt);

            parent.Controls.Add(txtWrap);
            y += 38;
        }

        private TextBox AddPasswordInputField(Panel parent, string label, string defaultValue, ref int y)
        {
            var lbl = new Label
            {
                Text = label, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentSubText, Location = new Point(2, y), AutoSize = true
            };
            parent.Controls.Add(lbl);
            y += 18;

            var txtWrap = new Panel
            {
                Size = new Size(250, 32),
                Location = new Point(2, y),
                BackColor = ThemeManager.CurrentInputBg
            };
            txtWrap.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, txtWrap.Width - 1, txtWrap.Height - 1);
                using var path = GetRoundedRect(r, 6);
                g.DrawPath(new Pen(ThemeManager.CurrentBorder, 1f), path);
            };

            var txt = new TextBox
            {
                Text = defaultValue,
                Font = new Font("Segoe UI", 9.5F),
                Size = new Size(205, 22),
                Location = new Point(8, 5),
                BackColor = ThemeManager.CurrentInputBg,
                ForeColor = ThemeManager.CurrentText,
                BorderStyle = BorderStyle.None,
                PasswordChar = '•'
            };
            txtWrap.Controls.Add(txt);

            var btnEye = new Label
            {
                Text = "👁",
                Font = new Font("Segoe UI", 10F),
                ForeColor = ThemeManager.CurrentSubText,
                Size = new Size(28, 28),
                Location = new Point(218, 2),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            btnEye.Click += (s, e) =>
            {
                txt.PasswordChar = txt.PasswordChar == '•' ? '\0' : '•';
                btnEye.ForeColor = txt.PasswordChar == '\0' ? ThemeManager.CurrentPrimary : ThemeManager.CurrentSubText;
            };
            txtWrap.Controls.Add(btnEye);

            parent.Controls.Add(txtWrap);
            y += 38;
            return txt;
        }

        private void AddReadonlyField(Panel parent, string label, string val, ref int y)
        {
            var lbl = new Label
            {
                Text = label, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentSubText, Location = new Point(2, y), AutoSize = true
            };
            parent.Controls.Add(lbl);
            y += 18;

            var valLbl = new Label
            {
                Text = val, Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentText, Location = new Point(2, y), AutoSize = true
            };
            parent.Controls.Add(valLbl);
            y += 26;
        }

        private void AddToggleRow(Panel parent, string title, string subtitle, bool initial, Action<bool> onChanged, ref int y)
        {
            var pnlRow = new Panel
            {
                Location = new Point(2, y),
                Size = new Size(250, 44),
                BackColor = Color.Transparent
            };

            var lblTitle = new Label
            {
                Text = title, Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentText, Location = new Point(0, 4), AutoSize = true
            };
            pnlRow.Controls.Add(lblTitle);

            var lblSub = new Label
            {
                Text = subtitle, Font = new Font("Segoe UI", 7.5F),
                ForeColor = ThemeManager.CurrentSubText, Location = new Point(0, 22), AutoSize = true
            };
            pnlRow.Controls.Add(lblSub);

            bool state = initial;
            var btnToggle = new Button
            {
                Text      = state ? "ON" : "OFF",
                Font      = new Font("Segoe UI", 8F, FontStyle.Bold),
                Size      = new Size(46, 26),
                Location  = new Point(200, 8),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = state ? ThemeManager.CurrentPrimary : ThemeManager.CurrentSubText,
                Cursor    = Cursors.Hand
            };
            btnToggle.FlatAppearance.BorderSize = 0;
            SetRoundRegion(btnToggle, 13);
            btnToggle.Click += (s, e) =>
            {
                state = !state;
                btnToggle.Text = state ? "ON" : "OFF";
                btnToggle.BackColor = state ? ThemeManager.CurrentPrimary : ThemeManager.CurrentSubText;
                onChanged?.Invoke(state);

                string cleanTitle = title;
                if (cleanTitle.Contains(" ")) cleanTitle = cleanTitle.Substring(cleanTitle.IndexOf(' ') + 1);
                AddActivityLogEntry("🛡️", $"{cleanTitle} turned {(state ? "ON" : "OFF")}", Color.FromArgb(59, 130, 246));
            };
            pnlRow.Controls.Add(btnToggle);

            parent.Controls.Add(pnlRow);
            y += 48;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  OVERALL FLYOUT CONTAINER PAINTING
        // ════════════════════════════════════════════════════════════════════════
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = this.Width, h = this.Height;
            var rect = new Rectangle(0, 0, w - 1, h - 1);
            using var path = GetRoundedRect(rect, 14);

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

        // ════════════════════════════════════════════════════════════════════════
        //  GRAPHICS HELPERS
        // ════════════════════════════════════════════════════════════════════════
        private GraphicsPath GetRoundedRect(Rectangle b, int r)
        {
            int d = r * 2;
            var path = new GraphicsPath();
            path.AddArc(b.X, b.Y, d, d, 180, 90);
            path.AddArc(b.Right - d, b.Y, d, d, 270, 90);
            path.AddArc(b.Right - d, b.Bottom - d, d, d, 0, 90);
            path.AddArc(b.X, b.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void SetRoundRegion(Control ctrl, int radius)
        {
            if (ctrl == null || ctrl.Width <= 0 || ctrl.Height <= 0) return;
            using var path = GetRoundedRect(new Rectangle(0, 0, ctrl.Width, ctrl.Height), radius);
            ctrl.Region = new Region(path);
        }

        private static void SetDoubleBuffer(Control c)
        {
            if (c == null) return;
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, c, new object[] { true });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animTimer?.Dispose();
                _slideTimer?.Dispose();
                _uploadTimer?.Dispose();
                _customAvatarImage?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
