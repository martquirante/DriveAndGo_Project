#nullable disable
using DriveAndGo_Admin.Helpers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

using Color = System.Drawing.Color;
using Image = System.Drawing.Image;
using Button = System.Windows.Forms.Button;
using ComboBox = System.Windows.Forms.ComboBox;
using TextBox = System.Windows.Forms.TextBox;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace DriveAndGo_Admin.Panels
{
    public class FleetPanel : UserControl
    {
        // ── Theme ─────────────────────────────────────────────────────────
        private Color ColBg => ThemeManager.CurrentBackground;
        private Color ColCard => ThemeManager.CurrentCard;
        private Color ColText => ThemeManager.CurrentText;
        private Color ColSub => ThemeManager.CurrentSubText;
        private Color ColBorder => ThemeManager.CurrentBorder;

        private readonly Color ColGreen = Color.FromArgb(34, 197, 94);
        private readonly Color ColRed = Color.FromArgb(239, 68, 68);
        private readonly Color ColBlue = Color.FromArgb(59, 130, 246);
        private readonly Color ColYellow = Color.FromArgb(245, 158, 11);
        private readonly Color ColPurple = Color.FromArgb(168, 85, 247);
        private readonly Color ColAccent = Color.FromArgb(230, 81, 0);
        private readonly Color ColCyan = Color.FromArgb(6, 182, 212);

        // ── Cloudinary ────────────────────────────────────────────────────
        private const string CloudName = "dducouuy5";
        private const string CloudApiKey = "818381663113767";
        private const string CloudSecret = "FVHDbX63zD5hD0xYAYCyNeJhAxM";

        // ── HQ / Firebase ─────────────────────────────────────────────────
        private const double HQ_LAT = 14.8169;
        private const double HQ_LNG = 121.0453;
        private const string HQ_NAME = "DriveAndGo Garage";
        private const string FbUrl = "https://vechiclerentaldb-default-rtdb.asia-southeast1.firebasedatabase.app";
        private const string FbGpsPath = "/location_logs.json";

        private readonly string _connStr =
            "Server=127.0.0.1;Port=3306;Database=vehicle_rental_db;Uid=root;Pwd=;";

        // ── UI ─────────────────────────────────────────────────────────────
        private SplitContainer splitContainer;
        private Panel topBar, bottomBar, cardScrollPanel;
        private FlowLayoutPanel flowCards;
        private WebView2 browser;
        private Label lblTitle, lblCount, lblLiveStatus;
        private Button btnAdd, btnEdit, btnDelete, btnRefresh;
        private TextBox txtSearch;
        private ComboBox cboFilterStatus;

        // ── Detail overlay ─────────────────────────────────────────────────
        private Panel _overlay;
        private bool _overlayOpen = false;
        private System.Windows.Forms.Timer _slideTimer;

        // ── State ──────────────────────────────────────────────────────────
        private DataTable _vehicleData = new DataTable();
        private int _selectedId = -1;
        private bool _mapReady = false;
        private readonly Dictionary<int, Panel> _cardMap = new();
        private readonly Dictionary<int, Image> _imgCache = new();

        private System.Windows.Forms.Timer _liveTimer;
        private static readonly HttpClient _http = new HttpClient();

        // ════════════════════════════════════════════════════════════════════
        public FleetPanel()
        {
            this.Dock = DockStyle.Fill;
            this.DoubleBuffered = true;
            this.BackColor = ColBg;
            ThemeManager.ThemeChanged += OnThemeChanged;

            BuildUI();
            LoadVehiclesFromDB();
            StartLiveGPSPolling();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  BUILD UI
        // ─────────────────────────────────────────────────────────────────────
        private void BuildUI()
        {
            bool dk = ThemeManager.IsDarkMode;

            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 2,
                SplitterDistance = 440,
                BackColor = dk ? Color.FromArgb(20, 20, 38) : Color.FromArgb(200, 200, 220)
            };

            this.SizeChanged += (s, e) => {
                if (this.Width > 900)
                    splitContainer.SplitterDistance = Math.Min(480, this.Width / 3);
            };

            splitContainer.Panel1.BackColor = ColBg;
            splitContainer.Panel2.BackColor = Color.FromArgb(5, 5, 12);

            BuildLeftPanel();
            BuildRightPanel();
            this.Controls.Add(splitContainer);

            // Overlay (hidden, sits above Panel2)
            _overlay = new Panel
            {
                Visible = false,
                BackColor = dk ? Color.FromArgb(6, 6, 16) : Color.White,
                Dock = DockStyle.None
            };
            splitContainer.Panel2.Controls.Add(_overlay);
            _overlay.BringToFront();
        }

        // ── LEFT PANEL ────────────────────────────────────────────────────
        private void BuildLeftPanel()
        {
            bool dk = ThemeManager.IsDarkMode;
            Color hdrBg = dk ? Color.FromArgb(6, 6, 16) : Color.FromArgb(248, 248, 255);

            topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 118,
                BackColor = hdrBg,
                Padding = new Padding(14, 10, 14, 8)
            };
            topBar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // gradient accent line at bottom
                using var br = new LinearGradientBrush(
                    new Point(0, topBar.Height - 2),
                    new Point(topBar.Width, topBar.Height - 2),
                    ColAccent, Color.Transparent);
                g.FillRectangle(br, 0, topBar.Height - 2, topBar.Width, 2);
                // subtle top glow stripe
                using var glow = new LinearGradientBrush(
                    new Rectangle(0, 0, topBar.Width, 3),
                    Color.FromArgb(60, ColAccent), Color.Transparent, LinearGradientMode.Vertical);
                g.FillRectangle(glow, 0, 0, topBar.Width, 3);
            };

            lblTitle = new Label
            {
                Text = "⬡  FLEET MANAGEMENT",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = ColAccent,
                AutoSize = true,
                Location = new Point(14, 10)
            };

            lblCount = new Label
            {
                Text = "Loading…",
                Font = new Font("Segoe UI", 8F),
                ForeColor = ColSub,
                AutoSize = true,
                Location = new Point(16, 36)
            };

            lblLiveStatus = new Label
            {
                Text = "● LIVE",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = ColGreen,
                AutoSize = true,
                Location = new Point(190, 36)
            };

            txtSearch = new TextBox
            {
                Size = new Size(210, 30),
                Location = new Point(14, 64),
                Font = new Font("Segoe UI", 9F),
                BackColor = dk ? Color.FromArgb(12, 12, 24) : Color.White,
                ForeColor = ColText,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "  Search name or plate…"
            };
            txtSearch.TextChanged += (s, e) => FilterAndRebuildCards();

            cboFilterStatus = new ComboBox
            {
                Size = new Size(130, 30),
                Location = new Point(232, 64),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F),
                BackColor = dk ? Color.FromArgb(12, 12, 24) : Color.White,
                ForeColor = ColText
            };
            cboFilterStatus.Items.AddRange(new object[]
                { "All", "Available", "In-Use", "Maintenance" });
            cboFilterStatus.SelectedIndex = 0;
            cboFilterStatus.SelectedIndexChanged += (s, e) => FilterAndRebuildCards();

            btnRefresh = MakeBtn("⟳", ColCyan, 372, 60, 44, 36);
            btnRefresh.Font = new Font("Segoe UI", 14F);
            btnRefresh.Click += (s, e) => { _imgCache.Clear(); LoadVehiclesFromDB(); };

            // Stats strip
            var statsStrip = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                BackColor = Color.Transparent
            };
            topBar.Controls.Add(statsStrip);

            topBar.Controls.AddRange(new Control[]
                { lblTitle, lblCount, lblLiveStatus, txtSearch, cboFilterStatus, btnRefresh });

            // Bottom bar
            bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                BackColor = dk ? Color.FromArgb(5, 5, 13) : Color.FromArgb(238, 238, 250)
            };
            bottomBar.Paint += (s, e) =>
            {
                using var p = new Pen(ColBorder, 1);
                e.Graphics.DrawLine(p, 0, 0, bottomBar.Width, 0);
            };

            btnAdd = MakeBtn("✚  Add Vehicle", ColGreen, 14, 11, 128, 36);
            btnEdit = MakeBtn("✎  Edit", ColBlue, 150, 11, 82, 36);
            btnDelete = MakeBtn("⊗  Delete", ColRed, 240, 11, 90, 36);

            btnAdd.Click += OnAddVehicle;
            btnEdit.Click += OnEditVehicle;
            btnDelete.Click += OnDeleteVehicle;

            bottomBar.Controls.AddRange(new Control[] { btnAdd, btnEdit, btnDelete });

            // Card scroll
            cardScrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = ColBg,
                Padding = new Padding(6, 4, 6, 4)
            };

            flowCards = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent,
                Location = new Point(0, 0)
            };
            cardScrollPanel.Controls.Add(flowCards);

            splitContainer.Panel1.Controls.Add(cardScrollPanel);
            splitContainer.Panel1.Controls.Add(topBar);
            splitContainer.Panel1.Controls.Add(bottomBar);
        }

        // ── RIGHT PANEL ───────────────────────────────────────────────────
        private void BuildRightPanel()
        {
            browser = new WebView2 { Dock = DockStyle.Fill };
            splitContainer.Panel2.Controls.Add(browser);
            InitWebView();
        }

        private async void InitWebView()
        {
            await browser.EnsureCoreWebView2Async(null);

            string assetsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets");
            if (!Directory.Exists(assetsFolder)) Directory.CreateDirectory(assetsFolder);

            browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "appassets", assetsFolder, CoreWebView2HostResourceAccessKind.Allow);

            browser.NavigateToString(GetMapHtml());
            browser.NavigationCompleted += async (s, e) =>
            {
                if (!e.IsSuccess) return;
                _mapReady = true;
                bool dk = ThemeManager.IsDarkMode;
                await browser.CoreWebView2.ExecuteScriptAsync($"setTheme({(dk ? "true" : "false")});");
                await browser.CoreWebView2.ExecuteScriptAsync($"setHQ({HQ_LAT},{HQ_LNG},'{HQ_NAME}');");
                await PushAllMarkersAsync();
            };
        }

        // ════════════════════════════════════════════════════════════════════
        //  DATA
        // ════════════════════════════════════════════════════════════════════
        private void LoadVehiclesFromDB()
        {
            _vehicleData = new DataTable();
            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();
                const string sql = @"
                    SELECT
                        vehicle_id,
                        CONCAT(brand,' ',model) AS vehicle_name,
                        brand, model, plate_no, type, cc,
                        status, rate_per_day, rate_with_driver,
                        COALESCE(photo_url,'')         AS photo_url,
                        COALESCE(description,'')       AS description,
                        COALESCE(seat_capacity,5)      AS seat_capacity,
                        COALESCE(transmission,'Automatic') AS transmission,
                        COALESCE(model_3d_url,'')      AS model_3d_url,
                        latitude, longitude, current_speed, last_update, in_garage,
                        CASE WHEN latitude IS NULL THEN 1 ELSE 0 END AS is_lost
                    FROM vehicles
                    ORDER BY brand, model";

                using var adapter = new MySqlDataAdapter(new MySqlCommand(sql, conn));
                adapter.Fill(_vehicleData);
                BuildVehicleCards(_vehicleData);
                UpdateCountLabel();
                if (_mapReady) _ = PushAllMarkersAsync();
                SetLiveLabel(true);
            }
            catch (Exception ex)
            {
                SetLiveLabel(false);
                MessageBox.Show(ex.Message, "DB Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetLiveLabel(bool ok)
        {
            void Do()
            {
                lblLiveStatus.Text = ok ? "● LIVE" : "⚠ Error";
                lblLiveStatus.ForeColor = ok ? ColGreen : ColRed;
            }
            if (lblLiveStatus.InvokeRequired) lblLiveStatus.Invoke((Action)Do); else Do();
        }

        // ════════════════════════════════════════════════════════════════════
        //  VEHICLE CARDS
        // ════════════════════════════════════════════════════════════════════
        private void BuildVehicleCards(DataTable dt)
        {
            if (flowCards.InvokeRequired)
            { flowCards.Invoke(new Action(() => BuildVehicleCards(dt))); return; }

            flowCards.SuspendLayout();
            flowCards.Controls.Clear();
            _cardMap.Clear();

            foreach (DataRow row in dt.Rows)
            {
                if (row["vehicle_id"] == DBNull.Value) continue;
                var card = CreateVehicleCard(row);
                flowCards.Controls.Add(card);
                _cardMap[Convert.ToInt32(row["vehicle_id"])] = card;
            }
            flowCards.ResumeLayout();
        }

        private Panel CreateVehicleCard(DataRow row)
        {
            int vid = Convert.ToInt32(row["vehicle_id"]);
            string name = row["vehicle_name"]?.ToString() ?? "";
            string plate = row["plate_no"]?.ToString() ?? "";
            string type = row["type"]?.ToString() ?? "";
            string status = row["status"]?.ToString() ?? "available";
            decimal rate = row["rate_per_day"] != DBNull.Value ? Convert.ToDecimal(row["rate_per_day"]) : 0;
            string photoUrl = GetFirstPhoto(row["photo_url"]?.ToString() ?? "");
            double lat = row["latitude"] != DBNull.Value ? Convert.ToDouble(row["latitude"]) : HQ_LAT;
            double lng = row["longitude"] != DBNull.Value ? Convert.ToDouble(row["longitude"]) : HQ_LNG;
            bool isLost = row["is_lost"] != DBNull.Value && Convert.ToInt32(row["is_lost"]) == 1;
            double dist = CalculateDistance(HQ_LAT, HQ_LNG, lat, lng);

            bool dk = ThemeManager.IsDarkMode;
            Color sc = StatusToColor(status);
            Color cardBg = dk ? Color.FromArgb(10, 10, 22) : Color.White;

            const int W = 206, H = 232;

            var card = new Panel
            {
                Size = new Size(W, H),
                Margin = new Padding(5),
                BackColor = cardBg,
                Cursor = Cursors.Hand,
                Tag = row
            };

            // Photo strip
            var photoPanel = new Panel
            {
                Size = new Size(W, 118),
                Location = new Point(0, 0),
                BackColor = dk ? Color.FromArgb(7, 7, 17) : Color.FromArgb(232, 232, 250)
            };

            var pic = new PictureBox
            {
                Size = new Size(W, 118),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            _ = LoadImageAsync(pic, photoUrl, vid, type);

            // Status badge
            var badge = new Label
            {
                Text = "  " + status.ToUpper() + "  ",
                Font = new Font("Segoe UI", 6.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(210, sc.R, sc.G, sc.B),
                AutoSize = true,
                Location = new Point(7, 7),
                Padding = new Padding(4, 2, 4, 2)
            };

            // Speed label
            var spdLbl = new Label
            {
                Name = "spd_" + vid,
                Text = "",
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = ColGreen,
                BackColor = Color.FromArgb(190, 4, 4, 14),
                AutoSize = true,
                Location = new Point(7, 92),
                Padding = new Padding(4, 1, 4, 1),
                Visible = false
            };

            photoPanel.Controls.AddRange(new Control[] { pic, badge, spdLbl });
            badge.BringToFront(); spdLbl.BringToFront();

            // Info area
            var info = new Panel
            {
                Location = new Point(0, 120),
                Size = new Size(W, H - 120),
                BackColor = Color.Transparent
            };

            // Left accent bar
            var accentBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(3, H - 120),
                BackColor = sc
            };

            var lblName = new Label
            {
                Text = name,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ColText,
                AutoSize = false,
                Size = new Size(W - 12, 22),
                Location = new Point(9, 5),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblPlate = new Label
            {
                Text = "🔖 " + plate + "  ·  " + type,
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = ColSub,
                AutoSize = false,
                Size = new Size(W - 12, 18),
                Location = new Point(9, 26),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblRate = new Label
            {
                Text = "₱" + rate.ToString("N0") + "/day",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ColAccent,
                AutoSize = false,
                Size = new Size(100, 20),
                Location = new Point(9, 48),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblDist = new Label
            {
                Name = "dist_" + vid,
                Text = isLost ? "⚠ No GPS" : $"📍 {dist:F1} km",
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = isLost ? ColRed : ColBlue,
                AutoSize = false,
                Size = new Size(88, 20),
                Location = new Point(W - 96, 48),
                TextAlign = ContentAlignment.MiddleRight
            };

            info.Controls.AddRange(new Control[] { accentBar, lblName, lblPlate, lblRate, lblDist });
            card.Controls.AddRange(new Control[] { photoPanel, info });

            // Paint rounded + glow
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                bool sel = _selectedId == vid;
                var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using var path = RoundRect(rect, 10);
                if (sel)
                {
                    for (int g = 14; g >= 2; g -= 3)
                    {
                        using var gp = new Pen(Color.FromArgb(18, sc), g);
                        e.Graphics.DrawPath(gp, path);
                    }
                }
                using var brd = new Pen(sel ? sc : (dk ? Color.FromArgb(22, 22, 44) : Color.FromArgb(220, 220, 240)), sel ? 1.8f : 1f);
                e.Graphics.DrawPath(brd, path);
                card.Region = new Region(path);
            };

            // Hover / click
            Color hoverBg = dk ? Color.FromArgb(16, 16, 32) : Color.FromArgb(244, 244, 255);

            void Enter(object _, EventArgs __) => card.BackColor = hoverBg;
            void Leave(object _, EventArgs __) => card.BackColor = cardBg;
            void Click(object _, EventArgs __) => SelectCard(vid, row);

            foreach (Control c in FlattenControls(card))
            {
                c.Click += Click;
                c.MouseEnter += Enter;
                c.MouseLeave += Leave;
            }

            return card;
        }

        private IEnumerable<Control> FlattenControls(Control root)
        {
            yield return root;
            foreach (Control c in root.Controls)
                foreach (var sub in FlattenControls(c))
                    yield return sub;
        }

        // ── Image loading ─────────────────────────────────────────────────
        private async Task LoadImageAsync(PictureBox pic, string url, int vid, string type)
        {
            if (_imgCache.TryGetValue(vid, out Image cached))
            { SafeSetImage(pic, cached); return; }

            Image img = null;
            try
            {
                if (!string.IsNullOrEmpty(url) && url != "null")
                {
                    if (url.StartsWith("http"))
                    {
                        var bytes = await _http.GetByteArrayAsync(url);
                        img = Image.FromStream(new MemoryStream(bytes));
                    }
                    else if (File.Exists(url))
                        img = Image.FromFile(url);
                }
            }
            catch { }

            if (img != null) { _imgCache[vid] = img; SafeSetImage(pic, img); }
            else DrawDefaultIcon(pic, type);
        }

        private void SafeSetImage(PictureBox p, Image img)
        {
            if (!p.IsHandleCreated) return;
            try { p.Invoke(new Action(() => { if (!p.IsDisposed) p.Image = img; })); }
            catch { }
        }

        private void DrawDefaultIcon(PictureBox pic, string type)
        {
            if (!pic.IsHandleCreated) return;
            try
            {
                pic.Invoke(new Action(() => {
                    if (pic.IsDisposed) return;
                    int w = Math.Max(pic.Width, 1), h = Math.Max(pic.Height, 1);
                    var bmp = new Bitmap(w, h);
                    using var g = Graphics.FromImage(bmp);
                    bool dk = ThemeManager.IsDarkMode;
                    g.Clear(dk ? Color.FromArgb(10, 10, 20) : Color.FromArgb(230, 230, 252));
                    string em = type?.ToLower() switch
                    {
                        var t when t != null && t.Contains("motor") => "🏍",
                        var t when t != null && t.Contains("van") => "🚐",
                        var t when t != null && t.Contains("truck") => "🚛",
                        var t when t != null && t.Contains("bicy") => "🚲",
                        _ => "🚗"
                    };
                    using var fmt = new StringFormat
                    { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(em, new Font("Segoe UI Emoji", 28F),
                        new SolidBrush(dk ? Color.FromArgb(55, 55, 80) : Color.FromArgb(180, 180, 220)),
                        new RectangleF(0, 0, w, h), fmt);
                    pic.Image = bmp;
                }));
            }
            catch { }
        }

        // ════════════════════════════════════════════════════════════════════
        //  CARD SELECTION
        // ════════════════════════════════════════════════════════════════════
        private void SelectCard(int vid, DataRow row)
        {
            _selectedId = vid;
            foreach (var kv in _cardMap) kv.Value.Invalidate();

            if (_cardMap.TryGetValue(vid, out Panel sel))
                cardScrollPanel.ScrollControlIntoView(sel);

            _ = FocusOnMap(vid, row);
            ShowDetailOverlay(row);
        }

        private async Task FocusOnMap(int vid, DataRow row)
        {
            if (!_mapReady || browser?.CoreWebView2 == null) return;
            double lat = row["latitude"] != DBNull.Value ? Convert.ToDouble(row["latitude"]) : HQ_LAT;
            double lng = row["longitude"] != DBNull.Value ? Convert.ToDouble(row["longitude"]) : HQ_LNG;
            string name = Esc(row["vehicle_name"]);
            string plate = Esc(row["plate_no"]);
            string status = Esc(row["status"]);
            string desc = Esc(row["description"]);
            int seats = row["seat_capacity"] != DBNull.Value ? Convert.ToInt32(row["seat_capacity"]) : 5;
            string lastU = row["last_update"] != DBNull.Value
                ? Convert.ToDateTime(row["last_update"]).ToString("MMM dd, yyyy HH:mm") : "No data";

            await browser.CoreWebView2.ExecuteScriptAsync(
                $"focusVehicle({vid},'{name}','{plate}','{status}',{lat},{lng},0,'{lastU}','{desc}',{seats});");
        }

        // ════════════════════════════════════════════════════════════════════
        //  DETAIL OVERLAY — Product-page shopping UX
        // ════════════════════════════════════════════════════════════════════
        private void ShowDetailOverlay(DataRow row)
        {
            int vid = Convert.ToInt32(row["vehicle_id"]);
            bool dk = ThemeManager.IsDarkMode;
            Color bg = dk ? Color.FromArgb(6, 6, 16) : Color.FromArgb(252, 252, 255);
            Color cardBg = dk ? Color.FromArgb(12, 12, 26) : Color.White;
            Color border = dk ? Color.FromArgb(22, 22, 46) : Color.FromArgb(220, 220, 240);

            _overlay.Controls.Clear();
            _overlay.BackColor = bg;

            int pw = splitContainer.Panel2.Width;
            int ph = splitContainer.Panel2.Height;
            _overlay.Size = new Size(pw, ph);
            _overlay.Location = new Point(pw, 0);

            // ── Close button ─────────────────────────────────────────────
            var btnClose = new Button
            {
                Text = "← Back to Map",
                Size = new Size(140, 32),
                Location = new Point(14, 12),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = ColSub,
                BackColor = Color.FromArgb(18, ColSub),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderColor = border;
            btnClose.FlatAppearance.BorderSize = 1;
            btnClose.Click += (s, e) => HideDetailOverlay();
            _overlay.Controls.Add(btnClose);

            // ── Vehicle header ────────────────────────────────────────────
            string name = row["vehicle_name"]?.ToString() ?? "";
            string plate = row["plate_no"]?.ToString() ?? "";
            string status = row["status"]?.ToString() ?? "available";
            Color sc = StatusToColor(status);
            decimal ratePD = row["rate_per_day"] != DBNull.Value ? Convert.ToDecimal(row["rate_per_day"]) : 0;

            var lblHdrName = new Label
            {
                Text = name,
                Font = new Font("Segoe UI", 17F, FontStyle.Bold),
                ForeColor = ColText,
                AutoSize = true,
                Location = new Point(14, 50)
            };
            var lblPlateType = new Label
            {
                Text = plate + "  ·  " + (row["type"]?.ToString() ?? ""),
                Font = new Font("Segoe UI", 9F),
                ForeColor = ColSub,
                AutoSize = true,
                Location = new Point(16, 80)
            };
            var lblStatus = new Label
            {
                Text = "  " + status.ToUpper() + "  ",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(200, sc.R, sc.G, sc.B),
                AutoSize = true,
                Location = new Point(14, 104),
                Padding = new Padding(6, 3, 6, 3)
            };
            var lblRateHdr = new Label
            {
                Text = "₱" + ratePD.ToString("N0") + " / day",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = ColAccent,
                AutoSize = true,
                Location = new Point(pw - 180, 54)
            };

            _overlay.Controls.AddRange(new Control[]
                { lblHdrName, lblPlateType, lblStatus, lblRateHdr });

            // ── Image Carousel ────────────────────────────────────────────
            List<string> photos = GetAllPhotos(row["photo_url"]?.ToString() ?? "");
            int carouselTop = 130;
            int carouselH = 210;

            var carouselPanel = new Panel
            {
                Location = new Point(0, carouselTop),
                Size = new Size(pw, carouselH),
                BackColor = dk ? Color.FromArgb(4, 4, 12) : Color.FromArgb(228, 228, 248)
            };

            var carouselPic = new PictureBox
            {
                Size = new Size(pw, carouselH),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };

            int currentPhoto = 0;
            var loadedPhotos = new List<Image>();

            _ = Task.Run(async () =>
            {
                foreach (var url in photos)
                {
                    Image img = null;
                    try
                    {
                        if (url.StartsWith("http"))
                        {
                            var b = await _http.GetByteArrayAsync(url);
                            img = Image.FromStream(new MemoryStream(b));
                        }
                        else if (File.Exists(url))
                            img = Image.FromFile(url);
                    }
                    catch { }
                    if (img != null) loadedPhotos.Add(img);
                }
                if (loadedPhotos.Count > 0) SafeSetImage(carouselPic, loadedPhotos[0]);
            });

            var lblPhotoCount = new Label
            {
                Text = photos.Count > 0 ? $"1 / {photos.Count}" : "No photos",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(150, 0, 0, 0),
                AutoSize = true,
                Location = new Point(pw - 72, carouselH - 26),
                Padding = new Padding(6, 2, 6, 2)
            };

            var btnPrev = new Button
            {
                Text = "‹",
                Size = new Size(38, carouselH),
                Location = new Point(0, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(80, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            btnPrev.FlatAppearance.BorderSize = 0;

            var btnNext = new Button
            {
                Text = "›",
                Size = new Size(38, carouselH),
                Location = new Point(pw - 38, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(80, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            btnNext.FlatAppearance.BorderSize = 0;

            btnPrev.Click += (s, e) => {
                if (loadedPhotos.Count == 0) return;
                currentPhoto = (currentPhoto - 1 + loadedPhotos.Count) % loadedPhotos.Count;
                SafeSetImage(carouselPic, loadedPhotos[currentPhoto]);
                lblPhotoCount.Text = $"{currentPhoto + 1} / {photos.Count}";
            };
            btnNext.Click += (s, e) => {
                if (loadedPhotos.Count == 0) return;
                currentPhoto = (currentPhoto + 1) % loadedPhotos.Count;
                SafeSetImage(carouselPic, loadedPhotos[currentPhoto]);
                lblPhotoCount.Text = $"{currentPhoto + 1} / {photos.Count}";
            };

            // Dot indicators
            var dotFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(pw / 2 - (photos.Count * 14) / 2, carouselH - 22)
            };
            for (int i = 0; i < photos.Count; i++)
            {
                int idx = i;
                var dot = new Label
                {
                    Size = new Size(10, 10),
                    BackColor = i == 0 ? ColAccent : Color.FromArgb(80, 255, 255, 255),
                    Margin = new Padding(2),
                    Tag = idx,
                    Cursor = Cursors.Hand,
                    Name = "dot_" + i
                };
                dot.Paint += (s, e) => {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.FillEllipse(new SolidBrush(dot.BackColor), 0, 0, dot.Width - 1, dot.Height - 1);
                };
                dot.Click += (s, e) => {
                    if (loadedPhotos.Count == 0) return;
                    currentPhoto = idx;
                    SafeSetImage(carouselPic, loadedPhotos[idx]);
                    lblPhotoCount.Text = $"{idx + 1} / {photos.Count}";
                    foreach (Control d in dotFlow.Controls)
                        if (d is Label dl) dl.BackColor = ((int)dl.Tag == idx) ? ColAccent : Color.FromArgb(80, 255, 255, 255);
                };
                dotFlow.Controls.Add(dot);
            }

            carouselPanel.Controls.AddRange(new Control[]
                { carouselPic, btnPrev, btnNext, lblPhotoCount, dotFlow });
            btnPrev.BringToFront(); btnNext.BringToFront();
            lblPhotoCount.BringToFront(); dotFlow.BringToFront();
            _overlay.Controls.Add(carouselPanel);

            // ── Scrollable body ───────────────────────────────────────────
            int actionBarH = 60;
            var scrollBody = new Panel
            {
                Location = new Point(0, carouselTop + carouselH),
                Size = new Size(pw, ph - carouselTop - carouselH - actionBarH),
                AutoScroll = true,
                BackColor = bg
            };
            _overlay.Controls.Add(scrollBody);

            int y = 16;

            // ── SPECIFICATIONS ────────────────────────────────────────────
            SectionLabel(scrollBody, "SPECIFICATIONS", 14, ref y);

            int seats = row["seat_capacity"] != DBNull.Value ? Convert.ToInt32(row["seat_capacity"]) : 5;
            string trans = row["transmission"]?.ToString() ?? "Automatic";
            string typeStr = row["type"]?.ToString() ?? "";
            string cc = row["cc"] != DBNull.Value ? row["cc"].ToString() + "cc" : "—";
            decimal rateWD = row["rate_with_driver"] != DBNull.Value ? Convert.ToDecimal(row["rate_with_driver"]) : 0;
            bool inGarage = row["in_garage"] != DBNull.Value && Convert.ToBoolean(row["in_garage"]);
            string lastGPS = row["last_update"] != DBNull.Value
                ? Convert.ToDateTime(row["last_update"]).ToString("MMM dd HH:mm") : "—";

            // Spec cards grid
            var specs = new (string icon, string label, string value)[]
            {
                ("🚗", "Type",        typeStr),
                ("⚙", "Engine",      cc),
                ("👥", "Seats",       seats + " seats"),
                ("🔧", "Transmission", trans),
                ("₱", "Rate/Day",    "₱" + ratePD.ToString("N0")),
                ("🚗", "With Driver", "₱" + rateWD.ToString("N0")),
                ("🏠", "In Garage",  inGarage ? "Yes" : "No"),
                ("📡", "Last GPS",   lastGPS)
            };

            int specCols = 2;
            int specW = (pw - 28 - (specCols - 1) * 6) / specCols;

            for (int i = 0; i < specs.Length; i += specCols)
            {
                for (int j = 0; j < specCols && i + j < specs.Length; j++)
                {
                    var sp = specs[i + j];
                    var specCard = new Panel
                    {
                        Size = new Size(specW, 52),
                        Location = new Point(14 + j * (specW + 6), y),
                        BackColor = cardBg
                    };
                    specCard.Paint += (s, e) => {
                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        using var path = RoundRect(new Rectangle(0, 0, specCard.Width - 1, specCard.Height - 1), 6);
                        using var pen = new Pen(border, 1);
                        e.Graphics.DrawPath(pen, path);
                        specCard.Region = new Region(path);
                    };

                    specCard.Controls.Add(new Label
                    {
                        Text = sp.label,
                        Font = new Font("Segoe UI", 7F),
                        ForeColor = ColSub,
                        AutoSize = true,
                        Location = new Point(10, 6)
                    });
                    specCard.Controls.Add(new Label
                    {
                        Text = sp.icon + " " + sp.value,
                        Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                        ForeColor = ColText,
                        AutoSize = false,
                        Size = new Size(specW - 12, 22),
                        Location = new Point(10, 22),
                        TextAlign = ContentAlignment.MiddleLeft
                    });
                    scrollBody.Controls.Add(specCard);
                }
                y += 58;
            }

            // ── DESCRIPTION ───────────────────────────────────────────────
            y += 6;
            SectionLabel(scrollBody, "DESCRIPTION", 14, ref y);

            var descBox = new RichTextBox
            {
                Text = row["description"]?.ToString() ?? "No description available.",
                Font = new Font("Segoe UI", 9F),
                ForeColor = ColSub,
                BackColor = bg,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.None,
                Location = new Point(14, y),
                Width = pw - 28,
                WordWrap = true,
                Height = 60
            };
            descBox.ContentsResized += (s, e) =>
                descBox.Height = Math.Max(50, e.NewRectangle.Height + 8);
            scrollBody.Controls.Add(descBox);
            y += descBox.Height + 20;

            // ── CUSTOMER REVIEWS ──────────────────────────────────────────
            SectionLabel(scrollBody, "CUSTOMER REVIEWS", 14, ref y);

            var reviewsData = LoadReviewsFromDB(vid);
            if (reviewsData.Rows.Count == 0)
            {
                scrollBody.Controls.Add(new Label
                {
                    Text = "No reviews yet for this vehicle.",
                    Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                    ForeColor = ColSub,
                    AutoSize = true,
                    Location = new Point(14, y)
                });
                y += 30;
            }
            else
            {
                double avg = 0;
                foreach (DataRow r in reviewsData.Rows)
                    if (r["vehicle_score"] != DBNull.Value)
                        avg += Convert.ToDouble(r["vehicle_score"]);
                avg /= reviewsData.Rows.Count;

                // Star rating summary
                var ratingCard = new Panel
                {
                    Location = new Point(14, y),
                    Size = new Size(pw - 28, 64),
                    BackColor = cardBg
                };
                ratingCard.Paint += (s, e) => {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = RoundRect(new Rectangle(0, 0, ratingCard.Width - 1, ratingCard.Height - 1), 8);
                    using var pen = new Pen(border, 1);
                    e.Graphics.DrawPath(pen, path);
                    ratingCard.Region = new Region(path);
                };
                ratingCard.Controls.Add(new Label
                {
                    Text = avg.ToString("F1"),
                    Font = new Font("Segoe UI", 26F, FontStyle.Bold),
                    ForeColor = ColYellow,
                    AutoSize = true,
                    Location = new Point(14, 8)
                });
                ratingCard.Controls.Add(new Label
                {
                    Text = new string('★', (int)Math.Round(avg)) + new string('☆', 5 - (int)Math.Round(avg)),
                    Font = new Font("Segoe UI", 12F),
                    ForeColor = ColYellow,
                    AutoSize = true,
                    Location = new Point(72, 10)
                });
                ratingCard.Controls.Add(new Label
                {
                    Text = $"{reviewsData.Rows.Count} review{(reviewsData.Rows.Count != 1 ? "s" : "")}",
                    Font = new Font("Segoe UI", 8.5F),
                    ForeColor = ColSub,
                    AutoSize = true,
                    Location = new Point(73, 38)
                });
                scrollBody.Controls.Add(ratingCard);
                y += 72;

                foreach (DataRow r in reviewsData.Rows)
                {
                    var rc = BuildReviewCard(r, pw - 28, cardBg, border, ColText, ColSub, ColYellow, y);
                    scrollBody.Controls.Add(rc);
                    y += rc.Height + 8;
                }
            }
            y += 20; // bottom padding

            // ── Action bar ────────────────────────────────────────────────
            var actionBar = new Panel
            {
                Location = new Point(0, ph - actionBarH),
                Size = new Size(pw, actionBarH),
                BackColor = dk ? Color.FromArgb(5, 5, 13) : Color.FromArgb(238, 238, 250)
            };
            actionBar.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(border, 1), 0, 0, actionBar.Width, 0);

            var btnEdit2 = MakeBtn("✎  Edit", ColBlue, 14, 12, 100, 36);
            var btnDel2 = MakeBtn("⊗  Delete", ColRed, 122, 12, 94, 36);
            var btnTrack2 = MakeBtn("📍  Track", ColGreen, 224, 12, 100, 36);

            btnEdit2.Click += (s, e) => { HideDetailOverlay(); OnEditByRow(row); };
            btnDel2.Click += (s, e) => { HideDetailOverlay(); OnDeleteById(vid); };
            btnTrack2.Click += (s, e) => HideDetailOverlay();

            actionBar.Controls.AddRange(new Control[] { btnEdit2, btnDel2, btnTrack2 });
            _overlay.Controls.Add(actionBar);

            // ── Slide in ──────────────────────────────────────────────────
            _overlay.Visible = true;
            _overlay.BringToFront();
            _overlayOpen = true;

            _slideTimer?.Stop();
            _slideTimer = new System.Windows.Forms.Timer { Interval = 8 };
            _slideTimer.Tick += (s, e) =>
            {
                int cur = _overlay.Left;
                int step = Math.Max(1, Math.Abs(cur) / 4);
                if (cur - step <= 0) { _overlay.Left = 0; _slideTimer.Stop(); }
                else _overlay.Left = cur - step;
            };
            _slideTimer.Start();
        }

        private void HideDetailOverlay()
        {
            if (!_overlayOpen) return;
            int pw = splitContainer.Panel2.Width;

            _slideTimer?.Stop();
            _slideTimer = new System.Windows.Forms.Timer { Interval = 8 };
            _slideTimer.Tick += (s, e) =>
            {
                int cur = _overlay.Left;
                int step = Math.Max(1, (pw - cur) / 4 + 1);
                if (cur + step >= pw)
                {
                    _overlay.Left = pw;
                    _overlay.Visible = false;
                    _overlayOpen = false;
                    _slideTimer.Stop();
                }
                else _overlay.Left = cur + step;
            };
            _slideTimer.Start();
        }

        // ── Review card ────────────────────────────────────────────────────
        private Panel BuildReviewCard(DataRow r, int w,
            Color bg, Color border, Color text, Color sub, Color star, int y)
        {
            int score = r["vehicle_score"] != DBNull.Value ? Convert.ToInt32(r["vehicle_score"]) : 0;
            string comment = r["comment"]?.ToString() ?? "No comment.";
            string ratedAt = r["rated_at"] != DBNull.Value
                ? Convert.ToDateTime(r["rated_at"]).ToString("MMM dd, yyyy") : "";
            string customer = r["customer_name"]?.ToString() ?? "Anonymous";

            var card = new Panel
            {
                Size = new Size(w, 84),
                Location = new Point(14, y),
                BackColor = bg
            };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundRect(new Rectangle(0, 0, w - 1, card.Height - 1), 7);
                using var pen = new Pen(border, 1);
                e.Graphics.DrawPath(pen, path);
                card.Region = new Region(path);
            };

            card.Controls.Add(new Label
            {
                Text = new string('★', score) + new string('☆', 5 - score),
                Font = new Font("Segoe UI", 11F),
                ForeColor = star,
                AutoSize = true,
                Location = new Point(10, 8)
            });
            card.Controls.Add(new Label
            {
                Text = customer + "  ·  " + ratedAt,
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = sub,
                AutoSize = true,
                Location = new Point(10, 32)
            });
            card.Controls.Add(new Label
            {
                Text = comment,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = text,
                AutoSize = false,
                Size = new Size(w - 20, 26),
                Location = new Point(10, 52),
                TextAlign = ContentAlignment.TopLeft
            });
            return card;
        }

        private static void SectionLabel(Panel p, string text, int x, ref int y)
        {
            p.Controls.Add(new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(90, 90, 160),
                AutoSize = true,
                Location = new Point(x, y)
            });
            y += 20;
            p.Controls.Add(new Panel
            {
                Location = new Point(x, y),
                Size = new Size(p.Width - x * 2, 1),
                BackColor = Color.FromArgb(28, 28, 58)
            });
            y += 10;
        }

        // ════════════════════════════════════════════════════════════════════
        //  REVIEWS
        // ════════════════════════════════════════════════════════════════════
        private DataTable LoadReviewsFromDB(int vehicleId)
        {
            var dt = new DataTable();
            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();
                const string sql = @"
                    SELECT r.vehicle_score, r.driver_score, r.comment, r.rated_at,
                           u.full_name AS customer_name
                    FROM ratings r
                    LEFT JOIN users u ON u.user_id = r.customer_id
                    WHERE r.vehicle_id = @vid
                    ORDER BY r.rated_at DESC LIMIT 20";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@vid", vehicleId);
                using var adapter = new MySqlDataAdapter(cmd);
                adapter.Fill(dt);
            }
            catch { }
            return dt;
        }

        // ════════════════════════════════════════════════════════════════════
        //  FILTER
        // ════════════════════════════════════════════════════════════════════
        private void FilterAndRebuildCards()
        {
            string filter = cboFilterStatus.SelectedItem?.ToString().ToLower() ?? "all";
            string search = txtSearch.Text.Trim().ToLower();
            var filtered = _vehicleData.Clone();

            foreach (DataRow row in _vehicleData.Rows)
            {
                bool ms = filter == "all" || row["status"].ToString().ToLower() == filter;
                bool mq = string.IsNullOrEmpty(search)
                    || row["vehicle_name"].ToString().ToLower().Contains(search)
                    || row["plate_no"].ToString().ToLower().Contains(search);
                if (ms && mq) filtered.ImportRow(row);
            }

            BuildVehicleCards(filtered);
            UpdateCountLabel(filtered);
        }

        private void UpdateCountLabel(DataTable dt = null)
        {
            var src = dt ?? _vehicleData;
            int avail = 0;
            foreach (DataRow r in src.Rows)
                if (r["status"]?.ToString().ToLower() == "available") avail++;
            string txt = $"{avail} available  ·  {src.Rows.Count} total";
            if (lblCount.InvokeRequired)
                lblCount.Invoke(new Action(() => lblCount.Text = txt));
            else lblCount.Text = txt;
        }

        // ════════════════════════════════════════════════════════════════════
        //  MAP PUSH
        // ════════════════════════════════════════════════════════════════════
        private async Task PushAllMarkersAsync()
        {
            if (!_mapReady || browser?.CoreWebView2 == null) return;
            await browser.CoreWebView2.ExecuteScriptAsync("clearMarkers();");
            await browser.CoreWebView2.ExecuteScriptAsync($"setHQ({HQ_LAT},{HQ_LNG},'{HQ_NAME}');");
            foreach (DataRow row in _vehicleData.Rows)
                await PushVehicleMarker(row);
        }

        private async Task PushVehicleMarker(DataRow row)
        {
            if (browser?.CoreWebView2 == null) return;
            double lat = row["latitude"] != DBNull.Value ? Convert.ToDouble(row["latitude"]) : HQ_LAT;
            double lng = row["longitude"] != DBNull.Value ? Convert.ToDouble(row["longitude"]) : HQ_LNG;
            bool isLost = row["is_lost"] != DBNull.Value && Convert.ToInt32(row["is_lost"]) == 1;
            int id = Convert.ToInt32(row["vehicle_id"]);
            string name = Esc(row["vehicle_name"]);
            string plate = Esc(row["plate_no"]);
            string type = Esc(row["type"]);
            string status = Esc(row["status"]);
            double spd = row["current_speed"] != DBNull.Value ? Convert.ToDouble(row["current_speed"]) : 0;
            string lastU = row["last_update"] != DBNull.Value
                ? Convert.ToDateTime(row["last_update"]).ToString("MMM dd, HH:mm") : "No data";

            // Prefer model_3d_url (top-down), fallback to first photo
            string mapIcon = row["model_3d_url"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(mapIcon) || mapIcon == "null")
                mapIcon = GetFirstPhoto(row["photo_url"]?.ToString() ?? "");

            string js = $"updateVehicle({id},'{name}','{plate}','{type}','{status}'," +
                        $"{lat},{lng},{spd},'{lastU}','{Esc(mapIcon)}'," +
                        $"{(isLost ? "true" : "false")});";
            await browser.CoreWebView2.ExecuteScriptAsync(js);
        }

        // ════════════════════════════════════════════════════════════════════
        //  LIVE GPS POLLING
        // ════════════════════════════════════════════════════════════════════
        private void StartLiveGPSPolling()
        {
            _liveTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _liveTimer.Tick += async (s, e) => await PollFirebaseGPS();
            _liveTimer.Start();
        }

        private async Task PollFirebaseGPS()
        {
            try
            {
                var resp = await _http.GetStringAsync(FbUrl + FbGpsPath);
                if (resp == "null" || string.IsNullOrEmpty(resp)) return;

                using var doc = JsonDocument.Parse(resp);
                foreach (var v in doc.RootElement.EnumerateObject())
                {
                    if (!int.TryParse(v.Name, out int vid)) continue;
                    if (!v.Value.TryGetProperty("lat", out var latEl) ||
                        !v.Value.TryGetProperty("lng", out var lngEl)) continue;

                    double lat = latEl.GetDouble();
                    double lng = lngEl.GetDouble();
                    double speed = v.Value.TryGetProperty("speed", out var sp) ? sp.GetDouble() : 0;

                    if (_mapReady && browser?.CoreWebView2 != null)
                        await browser.CoreWebView2.ExecuteScriptAsync(
                            $"liveUpdateGPS({vid},{lat},{lng},{speed});");

                    UpdateCardLive(vid, lat, lng, speed);

                    using var conn = new MySqlConnection(_connStr);
                    await conn.OpenAsync();
                    using var cmd = new MySqlCommand(
                        "UPDATE vehicles SET latitude=@lat,longitude=@lng,current_speed=@sp,last_update=NOW() WHERE vehicle_id=@vid",
                        conn);
                    cmd.Parameters.AddWithValue("@lat", lat);
                    cmd.Parameters.AddWithValue("@lng", lng);
                    cmd.Parameters.AddWithValue("@sp", speed);
                    cmd.Parameters.AddWithValue("@vid", vid);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch { }
        }

        private void UpdateCardLive(int vid, double lat, double lng, double speed)
        {
            if (!_cardMap.TryGetValue(vid, out Panel card)) return;
            if (card.InvokeRequired)
            { card.Invoke(new Action(() => UpdateCardLive(vid, lat, lng, speed))); return; }

            foreach (Control c in card.Controls)
                if (c is Panel imgPanel)
                    foreach (Control ch in imgPanel.Controls)
                        if (ch is Label sl && ch.Name == "spd_" + vid)
                        {
                            sl.Text = speed > 0 ? $"⚡ {speed:F0} km/h" : "";
                            sl.Visible = speed > 0;
                            sl.ForeColor = speed > 80 ? ColRed : speed > 40 ? ColYellow : ColGreen;
                        }

            double dist = CalculateDistance(HQ_LAT, HQ_LNG, lat, lng);
            foreach (Control c in card.Controls)
                if (c is Panel info)
                    foreach (Control ch in info.Controls)
                        if (ch is Label dl && ch.Name == "dist_" + vid)
                        {
                            dl.Text = $"📍 {dist:F1} km";
                            dl.ForeColor = ColBlue;
                        }
        }

        // ════════════════════════════════════════════════════════════════════
        //  CRUD
        // ════════════════════════════════════════════════════════════════════
        private void OnAddVehicle(object s, EventArgs e)
        {
            using var dlg = new VehicleFormDialog(null, _connStr);
            if (dlg.ShowDialog() == DialogResult.OK) { _imgCache.Clear(); LoadVehiclesFromDB(); }
        }

        private void OnEditVehicle(object s, EventArgs e)
        {
            if (_selectedId < 0)
            { MessageBox.Show("Select a vehicle first.", "Edit", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var rows = _vehicleData.Select($"vehicle_id = {_selectedId}");
            if (rows.Length == 0) return;
            OnEditByRow(rows[0]);
        }

        private void OnEditByRow(DataRow row)
        {
            using var dlg = new VehicleFormDialog(row, _connStr);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _imgCache.Remove(Convert.ToInt32(row["vehicle_id"]));
                LoadVehiclesFromDB();
            }
        }

        private void OnDeleteVehicle(object s, EventArgs e)
        {
            if (_selectedId < 0)
            { MessageBox.Show("Select a vehicle first.", "Delete", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            OnDeleteById(_selectedId);
        }

        private void OnDeleteById(int vid)
        {
            if (MessageBox.Show("Delete this vehicle permanently?", "Confirm Delete",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                using var conn = new MySqlConnection(_connStr);
                conn.Open();
                using var cmd = new MySqlCommand("DELETE FROM vehicles WHERE vehicle_id=@id", conn);
                cmd.Parameters.AddWithValue("@id", vid);
                cmd.ExecuteNonQuery();
                _imgCache.Remove(vid);
                _selectedId = -1;
                LoadVehiclesFromDB();
            }
            catch (Exception ex)
            { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // ════════════════════════════════════════════════════════════════════
        //  THEME
        // ════════════════════════════════════════════════════════════════════
        private void OnThemeChanged(object s, EventArgs e)
        {
            bool dk = ThemeManager.IsDarkMode;
            this.BackColor = ColBg;
            topBar.BackColor = dk ? Color.FromArgb(6, 6, 16) : Color.FromArgb(248, 248, 255);
            bottomBar.BackColor = dk ? Color.FromArgb(5, 5, 13) : Color.FromArgb(238, 238, 250);
            cardScrollPanel.BackColor = ColBg;
            txtSearch.BackColor = dk ? Color.FromArgb(12, 12, 24) : Color.White;
            txtSearch.ForeColor = ColText;
            cboFilterStatus.BackColor = dk ? Color.FromArgb(12, 12, 24) : Color.White;
            cboFilterStatus.ForeColor = ColText;
            _imgCache.Clear();
            BuildVehicleCards(_vehicleData);
            if (browser?.CoreWebView2 != null)
                _ = browser.CoreWebView2.ExecuteScriptAsync($"setTheme({(dk ? "true" : "false")});");
            this.Invalidate(true);
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════
        private Button MakeBtn(string text, Color color, int x, int y, int w, int h = 36)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(w, h),
                Location = new Point(x, y),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                BackColor = Color.FromArgb(22, color),
                ForeColor = color,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = color;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(44, color);
            return btn;
        }

        private Color StatusToColor(string s) => s?.ToLower() switch
        {
            "available" => ColGreen,
            "in-use" or "rented" => ColYellow,
            "maintenance" => ColPurple,
            "retired" => ColRed,
            _ => ColSub
        };

        private static string GetFirstPhoto(string json)
        {
            if (string.IsNullOrEmpty(json)) return "";
            try
            {
                if (json.TrimStart().StartsWith("["))
                {
                    var list = JsonSerializer.Deserialize<List<string>>(json);
                    return list?.Count > 0 ? list[0] : "";
                }
            }
            catch { }
            return json;
        }

        private static List<string> GetAllPhotos(string json)
        {
            if (string.IsNullOrEmpty(json)) return new List<string>();
            try
            {
                if (json.TrimStart().StartsWith("["))
                    return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch { }
            return string.IsNullOrEmpty(json) ? new List<string>() : new List<string> { json };
        }

        private double CalculateDistance(double la1, double lo1, double la2, double lo2)
        {
            const double R = 6371;
            double dLat = (la2 - la1) * Math.PI / 180;
            double dLon = (lo2 - lo1) * Math.PI / 180;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(la1 * Math.PI / 180) * Math.Cos(la2 * Math.PI / 180)
                * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private string Esc(object v) =>
            v?.ToString()?.Replace("'", "\\'").Replace("\r", " ").Replace("\n", " ") ?? "";

        private static GraphicsPath RoundRect(Rectangle b, int r)
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
            _liveTimer?.Stop(); _liveTimer?.Dispose();
            _slideTimer?.Stop(); _slideTimer?.Dispose();
            ThemeManager.ThemeChanged -= OnThemeChanged;
            browser?.Dispose();
            base.Dispose(disposing);
        }

        // ════════════════════════════════════════════════════════════════════
        //  MAP HTML — Futuristic with rotating top-down vehicle icons
        // ════════════════════════════════════════════════════════════════════
        private string GetMapHtml() => """
<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<title>DriveAndGo Fleet</title>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<style>
*{margin:0;padding:0;box-sizing:border-box;}
html,body{height:100%;font-family:'Segoe UI',sans-serif;display:flex;flex-direction:column;overflow:hidden;background:#050510;color:#dde0f0;}

#topbar{height:46px;background:rgba(6,6,18,.97);border-bottom:1px solid rgba(230,81,0,.25);display:flex;align-items:center;gap:5px;padding:0 14px;flex-shrink:0;}
#topbar::after{content:'';position:absolute;top:46px;left:0;right:0;height:1px;background:linear-gradient(90deg,transparent,rgba(230,81,0,.4),transparent);}
.title{font-weight:900;color:#e6510d;font-size:10px;letter-spacing:2px;text-transform:uppercase;margin-right:8px;}
.btn{padding:5px 13px;border-radius:20px;cursor:pointer;font-size:9px;font-weight:700;border:1px solid rgba(255,255,255,.1);background:transparent;color:#7070a8;transition:all .2s;}
.btn.active,.btn:hover{background:rgba(230,81,0,.18);color:#e6510d;border-color:rgba(230,81,0,.5);}
.spacer{flex:1;}

#map{flex:1;position:relative;min-height:0;}

/* Speed badge */
#spdbadge{position:absolute;bottom:86px;right:14px;width:76px;height:76px;background:rgba(5,5,15,.94);border:2px solid #e6510d;border-radius:50%;display:none;flex-direction:column;align-items:center;justify-content:center;z-index:900;box-shadow:0 0 28px rgba(230,81,13,.5);}
#spdval{font-size:24px;font-weight:900;color:#fff;line-height:1;}
#spdunit{font-size:7px;color:#5050a0;letter-spacing:2px;margin-top:2px;}

/* Info bar */
#infobar{height:68px;background:rgba(5,5,15,.97);border-top:1px solid rgba(230,81,0,.2);display:flex;align-items:center;flex-shrink:0;overflow:hidden;}
.ib-cell{display:flex;flex-direction:column;justify-content:center;padding:0 16px;border-right:1px solid rgba(255,255,255,.06);height:100%;}
.ib-cell:last-child{border-right:none;flex:1;}
.ib-lbl{font-size:7px;color:#353575;text-transform:uppercase;letter-spacing:1px;margin-bottom:3px;}
.ib-val{font-size:12px;font-weight:700;color:#dde0f0;}
.ib-desc{font-size:9px;color:#606090;line-height:1.4;overflow:hidden;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;}

/* Vehicle marker */
.v-wrap{display:flex;flex-direction:column;align-items:center;gap:4px;cursor:pointer;filter:drop-shadow(0 4px 12px rgba(0,0,0,.7));}
.v-ring{width:58px;height:58px;border-radius:12px;border:2.5px solid var(--rc,#22c55e);background:rgba(4,4,14,.9);overflow:hidden;box-shadow:0 0 20px color-mix(in srgb,var(--rc) 50%,transparent),inset 0 0 12px rgba(0,0,0,.5);transition:transform .5s cubic-bezier(.25,.8,.25,1);position:relative;}
.v-ring.selected{box-shadow:0 0 30px var(--rc),0 0 60px color-mix(in srgb,var(--rc) 30%,transparent);border-width:3px;}
.v-img{width:100%;height:100%;object-fit:cover;object-position:center;}
.v-sym{width:100%;height:100%;display:flex;align-items:center;justify-content:center;font-size:26px;}
.v-name{font-size:8.5px;font-weight:800;padding:2px 8px;border-radius:10px;background:rgba(4,4,14,.92);border:1px solid rgba(255,255,255,.12);color:#fff;white-space:nowrap;max-width:110px;overflow:hidden;text-overflow:ellipsis;}
.v-spd{font-size:8.5px;font-weight:800;padding:1px 7px;border-radius:8px;background:rgba(4,4,14,.9);border:1px solid rgba(255,255,255,.1);color:#22c55e;display:none;}
.v-spd.on{display:block;}

/* Pulse ring on movement */
.v-pulse{position:absolute;inset:-8px;border-radius:18px;border:2px solid var(--rc,#22c55e);opacity:0;animation:none;}
.v-pulse.moving{animation:pulse 1.5s infinite;}
@keyframes pulse{0%{opacity:.7;transform:scale(.9)}100%{opacity:0;transform:scale(1.4)}}

/* Lost marker */
.v-lost{width:22px;height:22px;border-radius:50%;background:#ef4444;border:3px solid rgba(255,255,255,.9);animation:blink 1.1s infinite;box-shadow:0 0 12px rgba(239,68,68,.7);}
@keyframes blink{0%,100%{opacity:1;box-shadow:0 0 0 0 rgba(239,68,68,.6)}50%{opacity:.7;box-shadow:0 0 0 12px rgba(239,68,68,0)}}

/* HQ */
.hq-wrap{display:flex;flex-direction:column;align-items:center;gap:3px;filter:drop-shadow(0 4px 14px rgba(0,0,0,.8));}
.hq-ring{width:64px;height:64px;border-radius:14px;border:2.5px solid #e6510d;background:rgba(4,4,14,.92);display:flex;align-items:center;justify-content:center;font-size:28px;box-shadow:0 0 24px rgba(230,81,13,.55);}
.hq-lbl{background:linear-gradient(135deg,#e6510d,#ff8c42);color:#fff;padding:2px 10px;border-radius:10px;font-size:8.5px;font-weight:800;white-space:nowrap;box-shadow:0 2px 10px rgba(0,0,0,.6);}

/* Popups */
.leaflet-popup-content-wrapper{background:#0c0c22!important;border:1px solid rgba(230,81,0,.35)!important;border-radius:14px!important;color:#dde0f0!important;box-shadow:0 8px 36px rgba(0,0,0,.9)!important;backdrop-filter:blur(8px)!important;}
.leaflet-popup-tip{background:#0c0c22!important;}
.pp-name{font-size:13px;font-weight:800;color:#e6510d;margin-bottom:5px;}
.pp-plate{display:inline-block;background:#1a1a3a;padding:2px 9px;border-radius:5px;font-size:10px;margin-bottom:7px;border:1px solid rgba(255,255,255,.1);}
.pp-row{font-size:10.5px;color:#8080b0;margin-top:3px;}

/* Light theme */
body.lt{background:#f0f0f8;color:#1a1a2e;}
body.lt #topbar{background:rgba(255,255,255,.98);border-bottom-color:rgba(230,81,0,.2);}
body.lt .btn{color:#505080;}
body.lt #infobar{background:rgba(250,250,255,.98);border-top-color:#dcdcf0;}
body.lt .ib-val{color:#1a1a2e;}
body.lt .ib-desc{color:#606090;}
body.lt .ib-cell{border-right-color:#e4e4f0;}
body.lt .ib-lbl{color:#9090b0;}
body.lt .leaflet-popup-content-wrapper{background:#fff!important;border-color:#d0d0e8!important;color:#1a1a2e!important;}
body.lt .leaflet-popup-tip{background:#fff!important;}
body.lt .v-ring{background:rgba(248,248,255,.95);}
</style>
</head>
<body>
<div id='topbar'>
  <span class='title'>⬡ DriveAndGo Fleet</span>
  <button class='btn active' id='bStreet'  onclick='setLayer("street")'>Street</button>
  <button class='btn'        id='bSat'     onclick='setLayer("satellite")'>Satellite</button>
  <button class='btn'        id='bTraffic' onclick='toggleTraffic()'>Traffic</button>
  <button class='btn'                     onclick='fitAll()'>Fit All</button>
  <button class='btn active' id='bRoutes'  onclick='toggleRoutes()'>Routes</button>
  <span class='spacer'></span>
  <span id='liveCount' style='font-size:9px;color:#404080;font-weight:700;'></span>
</div>
<div id='map'>
  <div id='spdbadge'>
    <div id='spdval'>0</div>
    <div id='spdunit'>KM/H</div>
  </div>
</div>
<div id='infobar'>
  <div class='ib-cell'><div class='ib-lbl'>Vehicle</div><div class='ib-val' id='iName'>Select a vehicle</div></div>
  <div class='ib-cell'><div class='ib-lbl'>Plate</div><div class='ib-val' id='iPlate'>—</div></div>
  <div class='ib-cell'><div class='ib-lbl'>Status</div><div class='ib-val' id='iStatus'>—</div></div>
  <div class='ib-cell'><div class='ib-lbl'>Speed</div><div class='ib-val' id='iSpd'>—</div></div>
  <div class='ib-cell'><div class='ib-lbl'>Last Update</div><div class='ib-val' id='iLast' style='font-size:10px;font-weight:500;'>—</div></div>
  <div class='ib-cell'><div class='ib-lbl'>Description</div><div class='ib-desc' id='iDesc'>—</div></div>
</div>
<script>
var map = L.map('map',{zoomControl:true,attributionControl:false}).setView([14.8169,121.0453],13);
var layerDark    = L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png',{maxZoom:20});
var layerLight   = L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png',{maxZoom:20});
var layerSat     = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',{maxZoom:20});
var layerTraffic = L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png',{opacity:.22});
layerDark.addTo(map);

var dark=true,mapType='street',trafficOn=false,routesOn=true;
var markers={},polylines={},distLabels={},vdata={};
var hqMarker=null,hqLat=14.8169,hqLng=121.0453;
var selectedVid=-1;

function setTheme(d){
  dark=d;
  document.body.classList.toggle('lt',!d);
  rebuildBase();
}
function rebuildBase(){
  [layerDark,layerLight,layerSat].forEach(l=>{try{map.removeLayer(l);}catch(e){}});
  (mapType==='satellite'?layerSat:dark?layerDark:layerLight).addTo(map);
  if(trafficOn)layerTraffic.bringToFront();
}
function setLayer(t){
  mapType=t;
  document.getElementById('bStreet').classList.toggle('active',t==='street');
  document.getElementById('bSat').classList.toggle('active',t==='satellite');
  rebuildBase();
}
function toggleTraffic(){
  trafficOn=!trafficOn;
  var b=document.getElementById('bTraffic');
  trafficOn?(layerTraffic.addTo(map),b.classList.add('active'),layerTraffic.bringToFront())
           :(map.removeLayer(layerTraffic),b.classList.remove('active'));
}
function toggleRoutes(){
  routesOn=!routesOn;
  document.getElementById('bRoutes').classList.toggle('active',routesOn);
  for(var id in polylines)polylines[id].forEach(p=>routesOn?map.addLayer(p):map.removeLayer(p));
  for(var id in distLabels)distLabels[id].forEach(m=>routesOn?map.addLayer(m):map.removeLayer(m));
}

function setHQ(lat,lng,name){
  hqLat=lat;hqLng=lng;
  if(hqMarker)map.removeLayer(hqMarker);
  var ic=L.divIcon({className:'',
    html:`<div class='hq-wrap'><div class='hq-ring'>🏢</div><div class='hq-lbl'>${name}</div></div>`,
    iconSize:[64,82],iconAnchor:[32,42]});
  hqMarker=L.marker([lat,lng],{icon:ic,zIndexOffset:2000}).addTo(map);
  hqMarker.bindPopup(`<div class='pp-name'>🏢 ${name}</div><div class='pp-row'>Main Garage & Operations Base</div>`);
}

function statusColor(s){
  s=(s||'').toLowerCase();
  if(s==='available')return'#22c55e';
  if(s==='in-use'||s==='rented')return'#eab308';
  if(s==='maintenance')return'#a855f7';
  return'#3b82f6';
}
function typeEmoji(t){
  t=(t||'').toLowerCase();
  if(t.includes('motor'))return'🏍';
  if(t.includes('van'))return'🚐';
  if(t.includes('truck'))return'🚛';
  if(t.includes('bicy'))return'🚲';
  return'🚗';
}

function markerHtml(vid,iconUrl,type,status,speed,isLost,name){
  var c=statusColor(status);
  var moving=speed>0;
  if(isLost)return`<div class='v-wrap'><div class='v-lost'></div><div class='v-name' style='color:#ef4444'>No GPS</div></div>`;
  var inner=iconUrl&&iconUrl.trim()&&iconUrl!=='null'&&(iconUrl.startsWith('http')||iconUrl.startsWith('https'))
    ?`<div class='v-pulse' id='vpulse${vid}' ${moving?'class="v-pulse moving"':''}></div><img src='${iconUrl}' class='v-img' id='vimg${vid}' onerror='this.style.display="none";document.getElementById("vsym${vid}").style.display="flex"'/><div class='v-sym' id='vsym${vid}' style='display:none'>${typeEmoji(type)}</div>`
    :`<div class='v-pulse' id='vpulse${vid}'></div><div class='v-sym' id='vsym${vid}'>${typeEmoji(type)}</div>`;
  var spdHtml=`<div class='v-spd${moving?" on":""}' id='vspd${vid}' style='color:${speed>80?'#ef4444':speed>40?'#eab308':'#22c55e'}'>${speed>0?speed+' km/h':''}</div>`;
  return`<div class='v-wrap'>${spdHtml}<div class='v-ring${vid===selectedVid?" selected":""}' id='vring${vid}' style='--rc:${c}'>${inner}</div><div class='v-name'>${name}</div></div>`;
}

function clearMarkers(){
  for(var id in markers){try{map.removeLayer(markers[id]);}catch(e){}}
  for(var id in polylines){try{polylines[id].forEach(p=>map.removeLayer(p));}catch(e){}}
  for(var id in distLabels){try{distLabels[id].forEach(m=>map.removeLayer(m));}catch(e){}}
  markers={};polylines={};distLabels={};vdata={};
}

async function drawRoute(vid,la1,lo1,la2,lo2,color){
  try{
    var url=`https://router.project-osrm.org/route/v1/driving/${lo1},${la1};${lo2},${la2}?overview=full&geometries=geojson`;
    var r=await(await fetch(url)).json();
    var pls=polylines[vid]||[],dls=distLabels[vid]||[];
    if(r.routes&&r.routes.length){
      var coords=r.routes[0].geometry.coordinates.map(c=>[c[1],c[0]]);
      var dkm=(r.routes[0].distance/1000).toFixed(1)+' km';
      var poly=L.polyline(coords,{color,weight:3,opacity:.6,dashArray:'6,4'});
      if(routesOn)poly.addTo(map);pls.push(poly);
      var mid=coords[Math.floor(coords.length/2)];
      var m=L.marker(mid,{icon:L.divIcon({className:'',
        html:`<div style='background:rgba(4,4,14,.9);color:${color};border:1px solid ${color};border-radius:6px;padding:2px 8px;font-size:9px;font-weight:700;white-space:nowrap;box-shadow:0 2px 8px rgba(0,0,0,.5)'>${dkm}</div>`,
        iconSize:[64,20]})});
      if(routesOn)m.addTo(map);dls.push(m);
    }
    polylines[vid]=pls;distLabels[vid]=dls;
  }catch(e){}
}

function updateVehicle(vid,name,plate,type,status,lat,lng,speed,lastU,iconUrl,isLost){
  vdata[vid]={name,plate,type,status,lat,lng,speed,lastU,iconUrl,isLost};
  if(markers[vid])map.removeLayer(markers[vid]);
  if(polylines[vid]){polylines[vid].forEach(p=>map.removeLayer(p));polylines[vid]=[];}
  if(distLabels[vid]){distLabels[vid].forEach(m=>map.removeLayer(m));distLabels[vid]=[];}

  if(!isLost)drawRoute(vid,hqLat,hqLng,lat,lng,statusColor(status));

  var sz=isLost?[22,22]:[58,96];
  var an=isLost?[11,11]:[29,88];
  var ic=L.divIcon({className:'',
    html:markerHtml(vid,iconUrl,type,status,speed||0,isLost,name),
    iconSize:sz,iconAnchor:an});

  var pop=`<div class='pp-name'>${name}</div><span class='pp-plate'>${plate}</span>`+
    `<div class='pp-row'><span style='color:${statusColor(status)}'>●</span> ${status.toUpperCase()}</div>`+
    `<div class='pp-row'>⏱ Last update: ${lastU}</div>`;

  var mk=L.marker([lat,lng],{icon:ic});
  mk.bindPopup(pop);
  mk.addTo(map);
  mk.on('click',()=>focusVehicle(vid,name,plate,status,lat,lng,speed||0,lastU,'',5));
  markers[vid]=mk;
  document.getElementById('liveCount').textContent=Object.keys(markers).length+' vehicles tracked';
}

function liveUpdateGPS(vid,lat,lng,speed){
  if(!markers[vid])return;
  var old=markers[vid].getLatLng();
  markers[vid].setLatLng([lat,lng]);
  if(vdata[vid]){vdata[vid].lat=lat;vdata[vid].lng=lng;vdata[vid].speed=speed;}

  // Speed label
  var spdEl=document.getElementById('vspd'+vid);
  if(spdEl){
    spdEl.textContent=speed>0?speed+' km/h':'';
    spdEl.className=speed>0?'v-spd on':'v-spd';
    spdEl.style.color=speed>80?'#ef4444':speed>40?'#eab308':'#22c55e';
  }

  // Pulse ring
  var pulse=document.getElementById('vpulse'+vid);
  if(pulse)pulse.className=speed>0?'v-pulse moving':'v-pulse';

  // Speed badge (selected vehicle)
  if(vid===selectedVid){
    var badge=document.getElementById('spdbadge');
    if(speed>0){
      badge.style.display='flex';
      document.getElementById('spdval').textContent=Math.round(speed);
      document.getElementById('iSpd').textContent=speed+' km/h';
      badge.style.borderColor=speed>80?'#ef4444':speed>40?'#eab308':'#22c55e';
      badge.style.boxShadow='0 0 28px '+(speed>80?'rgba(239,68,68,.6)':speed>40?'rgba(234,179,8,.6)':'rgba(34,197,94,.6)');
    }else{
      badge.style.display='none';
      document.getElementById('iSpd').textContent='Parked';
    }
  }

  // Auto-rotate marker icon based on bearing
  if(Math.abs(lat-old.lat)>0.000001||Math.abs(lng-old.lng)>0.000001){
    var dy=lat-old.lat;
    var dx=Math.cos(Math.PI/180*old.lat)*(lng-old.lng);
    var bearing=(90-Math.atan2(dy,dx)*180/Math.PI+360)%360;
    var ring=document.getElementById('vring'+vid);
    if(ring)ring.style.transform='rotate('+bearing+'deg)';
  }
}

function focusVehicle(vid,name,plate,status,lat,lng,speed,lastU,desc,seats){
  // Deselect old
  if(selectedVid>=0){
    var old=document.getElementById('vring'+selectedVid);
    if(old)old.classList.remove('selected');
  }
  selectedVid=vid;
  var ring=document.getElementById('vring'+vid);
  if(ring)ring.classList.add('selected');

  document.getElementById('iName').textContent=name;
  document.getElementById('iPlate').textContent=plate;
  document.getElementById('iStatus').textContent=status.toUpperCase();
  document.getElementById('iSpd').textContent=speed>0?speed+' km/h':'Parked';
  document.getElementById('iLast').textContent=lastU;
  document.getElementById('iDesc').textContent=desc||'—';

  map.flyTo([lat,lng],16,{animate:true,duration:1.2});
  if(markers[vid])markers[vid].openPopup();
}

function fitAll(){
  var b=[];
  for(var id in markers)b.push(markers[id].getLatLng());
  if(hqMarker)b.push(hqMarker.getLatLng());
  if(b.length)map.fitBounds(b,{padding:[60,60],animate:true});
}
</script>
</body>
</html>
""";

        // ════════════════════════════════════════════════════════════════════
        //  VEHICLE FORM DIALOG — multiple images, Cloudinary upload, auto-scroll
        // ════════════════════════════════════════════════════════════════════
        public class VehicleFormDialog : Form
        {
            private readonly string _connStr;
            private readonly DataRow _existing;

            private TextBox txtBrand, txtModel, txtPlate, txtCC, txtRate,
                               txtRateDriver, txtSeats, txtTrans, txtMapIcon;
            private RichTextBox txtDesc;
            private ComboBox cboType, cboStatus;
            private FlowLayoutPanel thumbFlow;
            private Button btnAddPhoto, btnSave;
            private Label lblUpload;
            private Panel scrollContent;
            private Panel scrollWrapper;

            private const string CloudName2 = "dducouuy5";
            private const string ApiKey2 = "818381663113767";
            private const string ApiSecret2 = "FVHDbX63zD5hD0xYAYCyNeJhAxM";
            private static readonly HttpClient _http2 = new HttpClient();

            private readonly List<string> _photoUrls = new List<string>();

            public VehicleFormDialog(DataRow existing, string connStr)
            {
                _existing = existing;
                _connStr = connStr;
                BuildForm();
            }

            private void BuildForm()
            {
                bool isEdit = _existing != null;
                bool dk = ThemeManager.IsDarkMode;
                Color bg = ThemeManager.CurrentBackground;
                Color card = ThemeManager.CurrentCard;
                Color text = ThemeManager.CurrentText;
                Color sub = ThemeManager.CurrentSubText;
                Color accent = ThemeManager.CurrentPrimary;

                this.Text = isEdit ? "✎  Edit Vehicle" : "✚  Add New Vehicle";
                this.Size = new Size(540, 740);
                this.StartPosition = FormStartPosition.CenterParent;
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.MaximizeBox = false;
                this.BackColor = bg;
                this.Font = new Font("Segoe UI", 9.5F);

                // Header
                var hdr = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 54,
                    BackColor = dk ? Color.FromArgb(6, 6, 16) : Color.FromArgb(248, 248, 255)
                };
                hdr.Paint += (s, e) =>
                {
                    using var br = new LinearGradientBrush(
                        new Point(0, 52), new Point(hdr.Width, 52),
                        accent, Color.Transparent);
                    e.Graphics.FillRectangle(br, 0, 52, hdr.Width, 2);
                };
                hdr.Controls.Add(new Label
                {
                    Text = isEdit ? "✎  Edit Vehicle Details" : "✚  Add New Vehicle",
                    Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                    ForeColor = accent,
                    AutoSize = true,
                    Location = new Point(16, 16)
                });
                this.Controls.Add(hdr);

                // Save button (footer)
                var footer = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 60,
                    BackColor = dk ? Color.FromArgb(5, 5, 13) : Color.FromArgb(238, 238, 252)
                };
                footer.Paint += (s, e) =>
                {
                    using var p = new Pen(ThemeManager.CurrentBorder, 1);
                    e.Graphics.DrawLine(p, 0, 0, footer.Width, 0);
                };

                btnSave = new Button
                {
                    Text = isEdit ? "💾  Save Changes" : "✚  Add Vehicle",
                    Size = new Size(200, 40),
                    Location = new Point((this.ClientSize.Width - 200) / 2, 10),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    BackColor = accent,
                    ForeColor = Color.White,
                    Cursor = Cursors.Hand
                };
                btnSave.FlatAppearance.BorderSize = 0;
                btnSave.Click += OnSave;
                footer.Controls.Add(btnSave);
                this.Controls.Add(footer);

                // Scrollable content
                scrollWrapper = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    BackColor = bg
                };
                this.Controls.Add(scrollWrapper);

                scrollContent = new Panel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    BackColor = bg,
                    Width = scrollWrapper.ClientSize.Width
                };
                scrollWrapper.Controls.Add(scrollContent);
                scrollWrapper.Resize += (s, e) =>
                    scrollContent.Width = scrollWrapper.ClientSize.Width - SystemInformation.VerticalScrollBarWidth;

                int y = 14;
                int lx = 16;
                int vx = 160;
                int vw = scrollWrapper.ClientSize.Width - vx - 24;

                // ── Fields ──────────────────────────────────────────────
                txtBrand = AddField("Brand / Make:", lx, vx, ref y, vw);
                txtModel = AddField("Model:", lx, vx, ref y, vw);
                txtPlate = AddField("Plate No.:", lx, vx, ref y, vw);
                cboType = AddCombo("Vehicle Type:", lx, vx, ref y, vw,
                    new[] { "Car", "Motorcycle", "Van", "Truck", "Bicycle" });
                txtCC = AddField("Engine CC:", lx, vx, ref y, vw);
                txtRate = AddField("Rate / Day (₱):", lx, vx, ref y, vw);
                txtRateDriver = AddField("Rate + Driver (₱):", lx, vx, ref y, vw);
                txtSeats = AddField("Seat Capacity:", lx, vx, ref y, vw);
                txtTrans = AddField("Transmission:", lx, vx, ref y, vw);
                cboStatus = AddCombo("Status:", lx, vx, ref y, vw,
                    new[] { "available", "in-use", "maintenance" });

                // Description
                AddLabel("Description:", lx, y + 5);
                txtDesc = new RichTextBox
                {
                    Location = new Point(lx, y + 24),
                    Width = scrollContent.Width - lx * 2,
                    Height = 72,
                    Font = new Font("Segoe UI", 9F),
                    BackColor = card,
                    ForeColor = text,
                    BorderStyle = BorderStyle.FixedSingle,
                    ScrollBars = RichTextBoxScrollBars.Vertical,
                    WordWrap = true
                };
                txtDesc.ContentsResized += (s, e) =>
                {
                    int nh = Math.Min(200, Math.Max(72, e.NewRectangle.Height + 10));
                    txtDesc.Height = nh;
                    // Reflow everything below
                    ReflowBelow(txtDesc, scrollContent);
                };
                scrollContent.Controls.Add(txtDesc);
                y += 24 + txtDesc.Height + 14;

                // Divider
                scrollContent.Controls.Add(new Panel
                {
                    Location = new Point(lx, y),
                    Size = new Size(scrollContent.Width - lx * 2, 1),
                    BackColor = ThemeManager.CurrentBorder
                });
                y += 10;

                // ── Photos section ───────────────────────────────────────
                AddLabel("Vehicle Photos (max 5):", lx, y + 4);
                y += 24;

                btnAddPhoto = new Button
                {
                    Text = "✚  Add Photo",
                    Size = new Size(136, 34),
                    Location = new Point(lx, y),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    BackColor = Color.FromArgb(22, accent),
                    ForeColor = accent,
                    Cursor = Cursors.Hand
                };
                btnAddPhoto.FlatAppearance.BorderColor = accent;
                btnAddPhoto.FlatAppearance.BorderSize = 1;
                btnAddPhoto.Click += OnAddPhoto;
                scrollContent.Controls.Add(btnAddPhoto);

                lblUpload = new Label
                {
                    Text = "⬆  Uploading to Cloudinary…",
                    Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                    ForeColor = accent,
                    AutoSize = true,
                    Location = new Point(lx + 144, y + 7),
                    Visible = false
                };
                scrollContent.Controls.Add(lblUpload);
                y += 42;

                thumbFlow = new FlowLayoutPanel
                {
                    Location = new Point(lx, y),
                    Size = new Size(scrollContent.Width - lx * 2, 84),
                    BackColor = dk ? Color.FromArgb(10, 10, 20) : Color.FromArgb(238, 238, 252),
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    AutoScroll = false
                };
                scrollContent.Controls.Add(thumbFlow);
                y += 92;

                // Map icon URL
                scrollContent.Controls.Add(new Panel
                {
                    Location = new Point(lx, y),
                    Size = new Size(scrollContent.Width - lx * 2, 1),
                    BackColor = ThemeManager.CurrentBorder
                });
                y += 10;

                AddLabel("Top-Down / 3D Icon URL\n(shown on map):", lx, y + 4);
                txtMapIcon = new TextBox
                {
                    Size = new Size(scrollContent.Width - lx * 2, 30),
                    Location = new Point(lx, y + 44),
                    Font = new Font("Segoe UI", 8.5F),
                    BackColor = card,
                    ForeColor = text,
                    BorderStyle = BorderStyle.FixedSingle,
                    PlaceholderText = "https://… (optional – auto-filled from first photo)"
                };
                scrollContent.Controls.Add(txtMapIcon);
                y += 86;

                // Spacer
                scrollContent.Controls.Add(new Panel
                {
                    Location = new Point(0, y),
                    Size = new Size(1, 20),
                    BackColor = Color.Transparent
                });
                y += 20;
                scrollContent.Height = y;

                // ── Populate existing ────────────────────────────────────
                if (isEdit)
                {
                    txtBrand.Text = _existing["brand"]?.ToString() ?? "";
                    txtModel.Text = _existing["model"]?.ToString() ?? "";
                    txtPlate.Text = _existing["plate_no"]?.ToString() ?? "";
                    txtCC.Text = _existing["cc"] != DBNull.Value ? _existing["cc"].ToString() : "";
                    txtRate.Text = _existing["rate_per_day"] != DBNull.Value
                        ? Convert.ToDecimal(_existing["rate_per_day"]).ToString("0.00") : "";
                    txtRateDriver.Text = _existing["rate_with_driver"] != DBNull.Value
                        ? Convert.ToDecimal(_existing["rate_with_driver"]).ToString("0.00") : "";
                    txtSeats.Text = _existing["seat_capacity"]?.ToString() ?? "5";
                    txtTrans.Text = _existing["transmission"]?.ToString() ?? "Automatic";
                    txtDesc.Text = _existing["description"]?.ToString() ?? "";
                    txtMapIcon.Text = _existing["model_3d_url"]?.ToString() ?? "";

                    SelectCombo(cboType, _existing["type"]?.ToString() ?? "");
                    SelectCombo(cboStatus, _existing["status"]?.ToString() ?? "");

                    foreach (var url in GetAllPhotos2(_existing["photo_url"]?.ToString() ?? ""))
                    {
                        _photoUrls.Add(url);
                        AddThumbnail(url);
                    }
                }
            }

            // Reflow all controls below a given control (for auto-resize desc)
            private void ReflowBelow(Control anchor, Panel parent)
            {
                int bottom = anchor.Bottom + 14;
                bool found = false;
                foreach (Control c in parent.Controls)
                {
                    if (c == anchor) { found = true; continue; }
                    if (!found) continue;
                    c.Top = bottom;
                    bottom += c.Height + (c is Panel p && p.Height == 1 ? 10 : 8);
                }
                parent.Height = bottom + 20;
            }

            private async void OnAddPhoto(object s, EventArgs e)
            {
                if (_photoUrls.Count >= 5)
                { MessageBox.Show("Max 5 photos per vehicle.", "Limit", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

                using var ofd = new OpenFileDialog
                {
                    Filter = "Image Files|*.png;*.jpg;*.jpeg;*.webp;*.gif",
                    Title = "Select Vehicle Photo"
                };
                if (ofd.ShowDialog() != DialogResult.OK) return;

                btnAddPhoto.Enabled = false;
                btnSave.Enabled = false;
                lblUpload.Text = $"⬆  Uploading photo {_photoUrls.Count + 1} / 5…";
                lblUpload.ForeColor = ThemeManager.CurrentPrimary;
                lblUpload.Visible = true;

                string uploadedUrl = null;
                try { uploadedUrl = await UploadToCloudinaryAsync(ofd.FileName); }
                catch { }

                string finalUrl = uploadedUrl ?? ofd.FileName;
                _photoUrls.Add(finalUrl);
                AddThumbnail(finalUrl);

                lblUpload.Text = uploadedUrl != null
                    ? $"✅  Uploaded: {finalUrl}"
                    : "⚠  Cloudinary failed – using local path";
                lblUpload.ForeColor = uploadedUrl != null
                    ? Color.FromArgb(34, 197, 94) : Color.FromArgb(245, 158, 11);

                // Auto-fill map icon URL if first photo
                if (_photoUrls.Count == 1 && string.IsNullOrEmpty(txtMapIcon.Text.Trim()) && uploadedUrl != null)
                    txtMapIcon.Text = uploadedUrl;

                btnAddPhoto.Enabled = _photoUrls.Count < 5;
                btnSave.Enabled = true;

                await Task.Delay(3000);
                lblUpload.Visible = false;
            }

            private void AddThumbnail(string url)
            {
                int idx = _photoUrls.IndexOf(url);
                bool dk = ThemeManager.IsDarkMode;

                var wrap = new Panel
                {
                    Size = new Size(78, 78),
                    Margin = new Padding(3),
                    BackColor = Color.Transparent
                };

                var pic = new PictureBox
                {
                    Size = new Size(78, 78),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = dk ? Color.FromArgb(14, 14, 28) : Color.FromArgb(230, 230, 252),
                    Cursor = Cursors.Default
                };
                pic.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = RoundRect2(new Rectangle(0, 0, pic.Width - 1, pic.Height - 1), 6);
                    using var pen = new Pen(ThemeManager.CurrentBorder, 1);
                    e.Graphics.DrawPath(pen, path);
                    pic.Region = new Region(path);
                };

                // Primary badge
                if (idx == 0)
                    wrap.Controls.Add(new Label
                    {
                        Text = "★",
                        Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                        ForeColor = Color.White,
                        BackColor = Color.FromArgb(200, 230, 81, 0),
                        AutoSize = true,
                        Location = new Point(3, 3),
                        Padding = new Padding(2)
                    });

                // Remove
                var btnX = new Label
                {
                    Text = "✕",
                    Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.FromArgb(185, 239, 68, 68),
                    Size = new Size(17, 17),
                    Location = new Point(58, 3),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Cursor = Cursors.Hand
                };
                btnX.Click += (s, e) =>
                {
                    _photoUrls.Remove(url);
                    thumbFlow.Controls.Remove(wrap);
                };

                _ = Task.Run(async () =>
                {
                    Image img = null;
                    try
                    {
                        if (url.StartsWith("http"))
                        {
                            var b = await _http2.GetByteArrayAsync(url);
                            img = Image.FromStream(new MemoryStream(b));
                        }
                        else if (File.Exists(url))
                            img = Image.FromFile(url);
                    }
                    catch { }
                    if (img != null && !pic.IsDisposed)
                        pic.Invoke(new Action(() => { if (!pic.IsDisposed) pic.Image = img; }));
                });

                wrap.Controls.Add(pic);
                wrap.Controls.Add(btnX);
                btnX.BringToFront();
                thumbFlow.Controls.Add(wrap);
            }

            private void OnSave(object s, EventArgs e)
            {
                if (string.IsNullOrWhiteSpace(txtBrand.Text) || string.IsNullOrWhiteSpace(txtPlate.Text))
                { MessageBox.Show("Brand and Plate are required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

                if (!decimal.TryParse(txtRate.Text, out decimal rate))
                { MessageBox.Show("Invalid daily rate.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

                if (!decimal.TryParse(txtRateDriver.Text, out decimal rateDriver)) rateDriver = 0;
                if (!int.TryParse(txtSeats.Text, out int seats)) seats = 5;
                if (!int.TryParse(txtCC.Text, out int cc)) cc = 0;

                // Build photo JSON
                string photoJson = _photoUrls.Count == 0 ? ""
                    : _photoUrls.Count == 1 ? _photoUrls[0]
                    : JsonSerializer.Serialize(_photoUrls);

                string mapIcon = txtMapIcon.Text.Trim();
                if (string.IsNullOrEmpty(mapIcon) && _photoUrls.Count > 0)
                    mapIcon = _photoUrls[0];

                try
                {
                    using var conn = new MySqlConnection(_connStr);
                    conn.Open();

                    string sql = _existing == null
                        ? @"INSERT INTO vehicles
                              (brand,model,plate_no,type,cc,rate_per_day,rate_with_driver,
                               status,photo_url,description,seat_capacity,transmission,model_3d_url)
                            VALUES
                              (@brand,@model,@plate,@type,@cc,@rate,@rateD,
                               @status,@photo,@desc,@seats,@trans,@mapicon)"
                        : @"UPDATE vehicles SET
                              brand=@brand,model=@model,plate_no=@plate,type=@type,cc=@cc,
                              rate_per_day=@rate,rate_with_driver=@rateD,status=@status,
                              photo_url=@photo,description=@desc,
                              seat_capacity=@seats,transmission=@trans,model_3d_url=@mapicon
                            WHERE vehicle_id=@id";

                    using var cmd = new MySqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@brand", txtBrand.Text.Trim());
                    cmd.Parameters.AddWithValue("@model", txtModel.Text.Trim());
                    cmd.Parameters.AddWithValue("@plate", txtPlate.Text.Trim());
                    cmd.Parameters.AddWithValue("@type", cboType.SelectedItem?.ToString() ?? "Car");
                    cmd.Parameters.AddWithValue("@cc", cc > 0 ? (object)cc : DBNull.Value);
                    cmd.Parameters.AddWithValue("@rate", rate);
                    cmd.Parameters.AddWithValue("@rateD", rateDriver);
                    cmd.Parameters.AddWithValue("@status", cboStatus.SelectedItem?.ToString() ?? "available");
                    cmd.Parameters.AddWithValue("@photo", photoJson);
                    cmd.Parameters.AddWithValue("@desc", txtDesc.Text.Trim());
                    cmd.Parameters.AddWithValue("@seats", seats);
                    cmd.Parameters.AddWithValue("@trans", txtTrans.Text.Trim());
                    cmd.Parameters.AddWithValue("@mapicon", mapIcon);
                    if (_existing != null)
                        cmd.Parameters.AddWithValue("@id", _existing["vehicle_id"]);

                    cmd.ExecuteNonQuery();
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                catch (Exception ex)
                { MessageBox.Show("DB Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }

            private async Task<string> UploadToCloudinaryAsync(string path)
            {
                try
                {
                    long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    string fld = "driveandgo_vehicles";
                    string sigStr = $"folder={fld}&timestamp={ts}{ApiSecret2}";
                    string sig = SHA1Hex(sigStr);

                    using var form = new MultipartFormDataContent();
                    form.Add(new StringContent(ApiKey2), "api_key");
                    form.Add(new StringContent(ts.ToString()), "timestamp");
                    form.Add(new StringContent(sig), "signature");
                    form.Add(new StringContent(fld), "folder");
                    form.Add(new ByteArrayContent(await File.ReadAllBytesAsync(path)),
                        "file", Path.GetFileName(path));

                    var res = await _http2.PostAsync(
                        $"https://api.cloudinary.com/v1_1/{CloudName2}/image/upload", form);
                    var json = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("secure_url", out var u))
                        return u.GetString();
                    if (doc.RootElement.TryGetProperty("error", out var err))
                        MessageBox.Show("Cloudinary: " + err.GetProperty("message").GetString(),
                            "Upload Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Upload error: " + ex.Message, "Cloudinary",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return null;
            }

            private static string SHA1Hex(string input)
            {
                using var sha = SHA1.Create();
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(input)))
                    .Replace("-", "").ToLower();
            }

            private static List<string> GetAllPhotos2(string json)
            {
                if (string.IsNullOrEmpty(json)) return new List<string>();
                try
                {
                    if (json.TrimStart().StartsWith("["))
                        return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                }
                catch { }
                return new List<string> { json };
            }

            // ── Form builder helpers ──────────────────────────────────────
            private TextBox AddField(string label, int lx, int vx, ref int y, int vw)
            {
                AddLabel(label, lx, y + 7);
                var tb = new TextBox
                {
                    Size = new Size(vw, 30),
                    Location = new Point(vx, y),
                    Font = new Font("Segoe UI", 9.5F),
                    BackColor = ThemeManager.CurrentCard,
                    ForeColor = ThemeManager.CurrentText,
                    BorderStyle = BorderStyle.FixedSingle
                };
                scrollContent.Controls.Add(tb);
                y += 42;
                return tb;
            }

            private ComboBox AddCombo(string label, int lx, int vx, ref int y, int vw, string[] items)
            {
                AddLabel(label, lx, y + 7);
                var cb = new ComboBox
                {
                    Size = new Size(vw, 30),
                    Location = new Point(vx, y),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new Font("Segoe UI", 9.5F),
                    BackColor = ThemeManager.CurrentCard,
                    ForeColor = ThemeManager.CurrentText
                };
                cb.Items.AddRange(items);
                cb.SelectedIndex = 0;
                scrollContent.Controls.Add(cb);
                y += 42;
                return cb;
            }

            private void AddLabel(string text, int x, int y)
            {
                scrollContent.Controls.Add(new Label
                {
                    Text = text,
                    Font = new Font("Segoe UI", 8.5F),
                    ForeColor = ThemeManager.CurrentSubText,
                    AutoSize = true,
                    Location = new Point(x, y)
                });
            }

            private static void SelectCombo(ComboBox cb, string value)
            {
                for (int i = 0; i < cb.Items.Count; i++)
                    if (cb.Items[i].ToString().ToLower() == value.ToLower())
                    { cb.SelectedIndex = i; return; }
            }

            private static GraphicsPath RoundRect2(Rectangle b, int r)
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
        }
    }
}