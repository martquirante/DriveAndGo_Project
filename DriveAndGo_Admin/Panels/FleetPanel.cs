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
        private readonly Color ColNeon = Color.FromArgb(0, 255, 200);
        private const int MaxVehicleMediaItems = 8;

        private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".jfif"
        };

        private static readonly HashSet<string> SupportedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".webm", ".mov", ".m4v"
        };

        // ── HQ / Firebase ──────────────────────────────────────────────────
        private const double HQ_LAT = 14.8169;
        private const double HQ_LNG = 121.0453;
        private const string HQ_NAME = "DriveAndGo Garage";
        private const string FbUrl = "https://vechiclerentaldb-default-rtdb.asia-southeast1.firebasedatabase.app";
        private const string FbGpsPath = "/vehicle_locations.json";

        private readonly string _connStr =
            "Server=127.0.0.1;Port=3306;Database=vehicle_rental_db;Uid=root;Pwd=;";

        // ── UI ──────────────────────────────────────────────────────────────
        private SplitContainer splitContainer;
        private Panel topBar, bottomBar, cardScrollPanel;
        private FlowLayoutPanel flowCards;
        private WebView2 browser;
        private Label lblTitle, lblCount, lblLiveStatus;
        private Button btnAdd, btnEdit, btnDelete, btnRefresh;
        private TextBox txtSearch;
        private ComboBox cboFilterStatus;

        // ── Detail overlay ──────────────────────────────────────────────────
        private Panel _overlay;
        private bool _overlayOpen = false;
        private System.Windows.Forms.Timer _slideTimer;
        private System.Windows.Forms.Timer _carouselAutoTimer;

        // ── State ───────────────────────────────────────────────────────────
        private DataTable _vehicleData = new DataTable();
        private int _selectedId = -1;
        private bool _mapReady = false;
        private readonly Dictionary<int, Panel> _cardMap = new();
        private readonly Dictionary<int, Image> _imgCache = new();
        private int _lastCardPanelWidth = 0;

        private System.Windows.Forms.Timer _liveTimer;
        private static readonly HttpClient _http = new HttpClient();

        private string _garage3DBase64 = "";

        private enum VehicleMediaKind
        {
            Unknown,
            Image,
            Video
        }

        private sealed class VehicleMediaItem
        {
            public VehicleMediaItem(string source)
            {
                Source = NormalizeMediaSource(source);
                Kind = DetectMediaKind(Source);
            }

            public string Source { get; }
            public VehicleMediaKind Kind { get; }
            public bool IsImage => Kind == VehicleMediaKind.Image;
            public bool IsVideo => Kind == VehicleMediaKind.Video;
        }

        // ════════════════════════════════════════════════════════════════════
        public FleetPanel()
        {
            this.Dock = DockStyle.Fill;
            this.DoubleBuffered = true;
            this.BackColor = ColBg;
            ThemeManager.ThemeChanged += OnThemeChanged;

            LoadGarage3DImage();
            BuildUI();
            LoadVehiclesFromDB();
            StartLiveGPSPolling();
        }

        // ── Load garage image ───────────────────────────────────────────────
        private void LoadGarage3DImage()
        {
            try
            {
                string[] searchPaths = {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "garage_3D.png"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets",    "garage_3D.png"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "garage_3D.png"),
                    Path.Combine(Application.StartupPath, "garage_3D.png")
                };
                foreach (var p in searchPaths)
                {
                    if (!File.Exists(p)) continue;
                    string webAssets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets");
                    Directory.CreateDirectory(webAssets);
                    string dest = Path.Combine(webAssets, "garage_3D.png");
                    if (p != dest) File.Copy(p, dest, true);
                    _garage3DBase64 = Convert.ToBase64String(File.ReadAllBytes(p));
                    break;
                }
            }
            catch { }
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
                BackColor = dk ? Color.FromArgb(16, 16, 32) : Color.FromArgb(200, 200, 220)
            };

            this.SizeChanged += (s, e) =>
            {
                if (this.Width > 900)
                    splitContainer.SplitterDistance = Math.Min(480, this.Width / 3);
            };

            splitContainer.Panel1.BackColor = ColBg;
            splitContainer.Panel2.BackColor = Color.FromArgb(3, 3, 8);

            BuildLeftPanel();
            BuildRightPanel();
            this.Controls.Add(splitContainer);

            _overlay = new Panel
            {
                Visible = false,
                BackColor = dk ? Color.FromArgb(4, 4, 12) : Color.White,
                Dock = DockStyle.None
            };
            splitContainer.Panel2.Controls.Add(_overlay);
            _overlay.BringToFront();
        }

        // ── LEFT PANEL ─────────────────────────────────────────────────────
        private void BuildLeftPanel()
        {
            bool dk = ThemeManager.IsDarkMode;
            Color hdrBg = dk ? Color.FromArgb(4, 4, 14) : Color.FromArgb(248, 248, 255);

            // ── Top bar ──────────────────────────────────────────────────
            topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 124,
                BackColor = hdrBg,
                Padding = new Padding(14, 10, 14, 8)
            };
            topBar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var br = new LinearGradientBrush(
                    new Point(0, topBar.Height - 2), new Point(topBar.Width, topBar.Height - 2),
                    ColAccent, Color.Transparent);
                g.FillRectangle(br, 0, topBar.Height - 2, topBar.Width, 2);
                using var scanPen = new Pen(Color.FromArgb(dk ? 8 : 4, ColAccent), 1);
                for (int sy = 0; sy < topBar.Height; sy += 4)
                    g.DrawLine(scanPen, 0, sy, topBar.Width, sy);
            };

            lblTitle = new Label
            {
                Text = "⬡  FLEET MANAGEMENT",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = ColAccent,
                AutoSize = true,
                Location = new Point(14, 10)
            };

            lblCount = new Label
            {
                Text = "Loading…",
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = ColSub,
                AutoSize = true,
                Location = new Point(16, 34)
            };

            lblLiveStatus = new Label
            {
                Text = "● LIVE",
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = ColGreen,
                AutoSize = true,
                Location = new Point(190, 34)
            };

            var blink = new System.Windows.Forms.Timer { Interval = 900 };
            blink.Tick += (s, e) =>
            {
                if (lblLiveStatus.Text == "● LIVE")
                    lblLiveStatus.ForeColor = lblLiveStatus.ForeColor == ColGreen
                        ? Color.FromArgb(80, ColGreen) : ColGreen;
            };
            blink.Start();

            txtSearch = new TextBox
            {
                Size = new Size(210, 30),
                Location = new Point(14, 68),
                Font = new Font("Segoe UI", 9F),
                BackColor = dk ? Color.FromArgb(10, 10, 22) : Color.White,
                ForeColor = ColText,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "  Search name or plate…"
            };
            txtSearch.TextChanged += (s, e) => FilterAndRebuildCards();

            cboFilterStatus = new ComboBox
            {
                Size = new Size(130, 30),
                Location = new Point(232, 68),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F),
                BackColor = dk ? Color.FromArgb(10, 10, 22) : Color.White,
                ForeColor = ColText
            };
            cboFilterStatus.Items.AddRange(new object[] { "All", "Available", "In-Use", "Maintenance" });
            cboFilterStatus.SelectedIndex = 0;
            cboFilterStatus.SelectedIndexChanged += (s, e) => FilterAndRebuildCards();

            btnRefresh = MakeBtn("⟳", ColCyan, 372, 64, 44, 36);
            btnRefresh.Font = new Font("Segoe UI", 14F);
            btnRefresh.Click += (s, e) => { _imgCache.Clear(); LoadVehiclesFromDB(); };

            topBar.Controls.AddRange(new Control[]
                { lblTitle, lblCount, lblLiveStatus, txtSearch, cboFilterStatus, btnRefresh });

            // ── Bottom bar ────────────────────────────────────────────────
            bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = dk ? Color.FromArgb(4, 4, 12) : Color.FromArgb(238, 238, 250)
            };
            bottomBar.Paint += (s, e) =>
            {
                using var p = new Pen(ColBorder, 1);
                e.Graphics.DrawLine(p, 0, 0, bottomBar.Width, 0);
                using var glow = new Pen(Color.FromArgb(30, ColAccent), 1);
                e.Graphics.DrawLine(glow, 0, 1, bottomBar.Width, 1);
            };

            btnAdd = MakeBtn("✚  Add Vehicle", ColGreen, 14, 12, 128, 36);
            btnEdit = MakeBtn("✎  Edit", ColBlue, 150, 12, 82, 36);
            btnDelete = MakeBtn("⊗  Delete", ColRed, 240, 12, 90, 36);

            btnAdd.Click += OnAddVehicle;
            btnEdit.Click += OnEditVehicle;
            btnDelete.Click += OnDeleteVehicle;

            bottomBar.Controls.AddRange(new Control[] { btnAdd, btnEdit, btnDelete });

            // ── Card scroll panel ─────────────────────────────────────────
            cardScrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = ColBg,
                Padding = new Padding(6, 4, 6, 4)
            };

            // ── Vertical flow (TopDown = scrollable list) ─────────────────
            flowCards = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent,
                Location = new Point(0, 0)
            };
            cardScrollPanel.Controls.Add(flowCards);

            // Keep card widths in sync when panel is resized
            cardScrollPanel.Resize += (s, e) =>
            {
                int nw = cardScrollPanel.ClientSize.Width;
                if (Math.Abs(nw - _lastCardPanelWidth) > 6)
                {
                    _lastCardPanelWidth = nw;
                    flowCards.Width = nw;
                    FilterAndRebuildCards();
                }
            };

            splitContainer.Panel1.Controls.Add(cardScrollPanel);
            splitContainer.Panel1.Controls.Add(topBar);
            splitContainer.Panel1.Controls.Add(bottomBar);
        }

        // ── RIGHT PANEL ────────────────────────────────────────────────────
        private void BuildRightPanel()
        {
            browser = new WebView2 { Dock = DockStyle.Fill };
            splitContainer.Panel2.Controls.Add(browser);
            InitWebView();
        }

        private async void InitWebView()
        {
            await browser.EnsureCoreWebView2Async(null);

            string outputAssetsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets");
            if (!Directory.Exists(outputAssetsFolder))
                Directory.CreateDirectory(outputAssetsFolder);

            // Try possible source locations
            string[] sourceCandidates =
            {
        Path.Combine(Application.StartupPath, "WebAssets", "FleetMap.html"),
        Path.Combine(Application.StartupPath, "FleetMap.html"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "FleetMap.html"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FleetMap.html"),
        Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\WebAssets\FleetMap.html")),
        Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\..\WebAssets\FleetMap.html"))
    };

            string sourceHtml = sourceCandidates.FirstOrDefault(File.Exists);
            string destHtml = Path.Combine(outputAssetsFolder, "FleetMap.html");

            // Copy HTML to runtime WebAssets folder if found somewhere else
            if (!string.IsNullOrEmpty(sourceHtml))
            {
                if (!string.Equals(sourceHtml, destHtml, StringComparison.OrdinalIgnoreCase))
                    File.Copy(sourceHtml, destHtml, true);
            }

            // Copy garage image too
            string[] garageCandidates =
            {
        Path.Combine(Application.StartupPath, "WebAssets", "garage_3D.png"),
        Path.Combine(Application.StartupPath, "garage_3D.png"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "garage_3D.png"),
        Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\WebAssets\garage_3D.png")),
        Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\..\WebAssets\garage_3D.png"))
    };

            string sourceGarage = garageCandidates.FirstOrDefault(File.Exists);
            string destGarage = Path.Combine(outputAssetsFolder, "garage_3D.png");

            if (!string.IsNullOrEmpty(sourceGarage))
            {
                if (!string.Equals(sourceGarage, destGarage, StringComparison.OrdinalIgnoreCase))
                    File.Copy(sourceGarage, destGarage, true);
            }

            browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "appassets",
                outputAssetsFolder,
                CoreWebView2HostResourceAccessKind.Allow);

            browser.NavigationCompleted += async (s, e) =>
            {
                if (!e.IsSuccess) return;

                _mapReady = true;
                bool dk = ThemeManager.IsDarkMode;

                await browser.CoreWebView2.ExecuteScriptAsync($"setTheme({(dk ? "true" : "false")});");

                string garageImgSrc = File.Exists(destGarage)
                    ? "https://appassets/garage_3D.png"
                    : "";

                await browser.CoreWebView2.ExecuteScriptAsync(
                    $"setHQ({HQ_LAT},{HQ_LNG},'{HQ_NAME}','{garageImgSrc}');");

                await PushAllMarkersAsync();
            };

            if (File.Exists(destHtml))
            {
                browser.CoreWebView2.Navigate("https://appassets/FleetMap.html");
            }
            else
            {
                browser.NavigateToString(
                    "<html><body style='background:#030308;color:#c8d0f0;font-family:Segoe UI;" +
                    "display:flex;align-items:center;justify-content:center;height:100vh;margin:0'>" +
                    "<div style='text-align:center'><div style='font-size:32px'>⬡</div>" +
                    "<p>FleetMap.html not found.</p>" +
                    "<p style='color:#404060;font-size:12px'>Check WebAssets output copy settings.</p></div></body></html>");
            }
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
                        COALESCE(photo_url,'')              AS photo_url,
                        COALESCE(description,'')            AS description,
                        COALESCE(seat_capacity,5)           AS seat_capacity,
                        COALESCE(transmission,'Automatic')  AS transmission,
                        COALESCE(model_3d_url,'')           AS model_3d_url,
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
                MessageBox.Show(ex.Message, "DB Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        //  VEHICLE CARDS  (vertical list — full-width rows)
        // ════════════════════════════════════════════════════════════════════
        private void BuildVehicleCards(DataTable dt)
        {
            if (flowCards.InvokeRequired)
            { flowCards.Invoke(new Action(() => BuildVehicleCards(dt))); return; }

            flowCards.SuspendLayout();
            flowCards.Controls.Clear();
            _cardMap.Clear();

            // Set flow width to match scroll panel
            int panelW = cardScrollPanel.ClientSize.Width;
            if (panelW < 10) panelW = 440;
            flowCards.Width = panelW;
            _lastCardPanelWidth = panelW;

            foreach (DataRow row in dt.Rows)
            {
                if (row["vehicle_id"] == DBNull.Value) continue;
                var card = CreateVehicleCard(row, panelW);
                flowCards.Controls.Add(card);
                _cardMap[Convert.ToInt32(row["vehicle_id"])] = card;
            }
            flowCards.ResumeLayout();
        }

        /// <summary>
        /// Creates a horizontal list-row card:
        ///   [colorBar 3px] [photo 82px] [info: name / plate / rate+dist / spd]
        /// </summary>
        private Panel CreateVehicleCard(DataRow row, int panelW)
        {
            int vid = Convert.ToInt32(row["vehicle_id"]);
            string name = row["vehicle_name"]?.ToString() ?? "";
            string plate = row["plate_no"]?.ToString() ?? "";
            string type = row["type"]?.ToString() ?? "";
            string status = row["status"]?.ToString() ?? "available";
            decimal rate = row["rate_per_day"] != DBNull.Value ? Convert.ToDecimal(row["rate_per_day"]) : 0;
            string photo = GetPrimaryMediaPreviewSource(row["photo_url"]?.ToString() ?? "");
            double lat = row["latitude"] != DBNull.Value ? Convert.ToDouble(row["latitude"]) : HQ_LAT;
            double lng = row["longitude"] != DBNull.Value ? Convert.ToDouble(row["longitude"]) : HQ_LNG;
            bool isLost = row["is_lost"] != DBNull.Value && Convert.ToInt32(row["is_lost"]) == 1;
            double dist = CalculateDistance(HQ_LAT, HQ_LNG, lat, lng);

            bool dk = ThemeManager.IsDarkMode;
            Color sc = StatusToColor(status);
            Color cardBg = dk ? Color.FromArgb(8, 8, 20) : Color.White;

            // Width fills the panel; height is a compact row
            int W = Math.Max(200, panelW - 12);
            const int H = 90;
            const int IMG_W = 82;
            const int BAR_W = 3;
            int infoX = BAR_W + IMG_W + 4;
            int infoW = W - infoX - 8;

            var card = new Panel
            {
                Size = new Size(W, H),
                Margin = new Padding(4, 3, 4, 3),
                BackColor = cardBg,
                Cursor = Cursors.Hand,
                Tag = row
            };

            // ── 3-px left accent bar ──────────────────────────────────────
            var colorBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(BAR_W, H),
                BackColor = sc
            };

            // ── Photo ─────────────────────────────────────────────────────
            var pic = new PictureBox
            {
                Location = new Point(BAR_W, 0),
                Size = new Size(IMG_W, H),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = dk ? Color.FromArgb(6, 6, 16) : Color.FromArgb(224, 224, 246)
            };
            _ = LoadImageAsync(pic, photo, vid, type);

            // ── Info area ─────────────────────────────────────────────────
            var info = new Panel
            {
                Location = new Point(infoX, 0),
                Size = new Size(infoW, H),
                BackColor = Color.Transparent
            };

            // Status badge (top-right of info panel)
            var badge = new Label
            {
                Text = "  " + status.ToUpper() + "  ",
                Font = new Font("Segoe UI", 6F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(200, sc.R, sc.G, sc.B),
                AutoSize = true,
                Padding = new Padding(3, 1, 3, 1)
            };
            badge.Location = new Point(infoW - badge.PreferredWidth - 6, 8);

            var lblName = new Label
            {
                Text = name,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = ColText,
                AutoSize = false,
                Size = new Size(infoW - badge.PreferredWidth - 12, 22),
                Location = new Point(6, 6),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblPlate = new Label
            {
                Text = "🔖 " + plate + "  ·  " + type,
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = ColSub,
                AutoSize = false,
                Size = new Size(infoW - 8, 18),
                Location = new Point(6, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblRate = new Label
            {
                Text = "₱" + rate.ToString("N0") + "/day",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ColAccent,
                AutoSize = false,
                Size = new Size(infoW / 2, 20),
                Location = new Point(6, 52),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblDist = new Label
            {
                Name = "dist_" + vid,
                Text = isLost ? "⚠ No GPS" : $"📍 {dist:F1} km",
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = isLost ? ColRed : ColBlue,
                AutoSize = false,
                Size = new Size(infoW / 2 - 4, 20),
                Location = new Point(infoW / 2 + 4, 52),
                TextAlign = ContentAlignment.MiddleRight
            };

            var spdLbl = new Label
            {
                Name = "spd_" + vid,
                Text = "",
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                ForeColor = ColNeon,
                BackColor = Color.FromArgb(160, 2, 2, 12),
                AutoSize = true,
                Location = new Point(6, 70),
                Padding = new Padding(3, 1, 3, 1),
                Visible = false
            };

            info.Controls.AddRange(new Control[]
                { lblName, badge, lblPlate, lblRate, lblDist, spdLbl });

            card.Controls.AddRange(new Control[] { colorBar, pic, info });

            // ── Rounded corners + selection glow ─────────────────────────
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                bool sel = _selectedId == vid;
                var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using var path = RoundRect(rect, 10);

                if (sel)
                {
                    int[] glowA = { 6, 14, 26, 40 };
                    int[] glowW = { 16, 10, 6, 2 };
                    for (int gi = 0; gi < glowA.Length; gi++)
                        using (var gp = new Pen(Color.FromArgb(glowA[gi], sc), glowW[gi]))
                            e.Graphics.DrawPath(gp, path);
                }

                using var brd = new Pen(
                    sel ? sc : (dk ? Color.FromArgb(18, 18, 40) : Color.FromArgb(218, 218, 238)),
                    sel ? 2f : 1f);
                e.Graphics.DrawPath(brd, path);
                card.Region = new Region(path);
            };

            Color hoverBg = dk ? Color.FromArgb(14, 14, 30) : Color.FromArgb(242, 242, 255);
            void Enter(object _, EventArgs __) => card.BackColor = hoverBg;
            void Leave(object _, EventArgs __) => card.BackColor = cardBg;
            void Click(object _, EventArgs __) => SelectCard(vid, row);

            foreach (Control c in FlattenControls(card))
            { c.Click += Click; c.MouseEnter += Enter; c.MouseLeave += Leave; }

            return card;
        }

        private IEnumerable<Control> FlattenControls(Control root)
        {
            yield return root;
            foreach (Control c in root.Controls)
                foreach (var sub in FlattenControls(c))
                    yield return sub;
        }

        // ── Image loading ──────────────────────────────────────────────────
        private async Task LoadImageAsync(PictureBox pic, string url, int vid, string type)
        {
            if (DetectMediaKind(url) == VehicleMediaKind.Video)
            {
                DrawDefaultIcon(pic, type, true);
                return;
            }

            if (_imgCache.TryGetValue(vid, out Image cached))
            { SafeSetImage(pic, cached); return; }

            Image img = await LoadImageFromSourceAsync(_http, url);

            if (img != null) { _imgCache[vid] = img; SafeSetImage(pic, img); }
            else DrawDefaultIcon(pic, type, false);
        }

        private void SafeSetImage(PictureBox p, Image img)
        {
            if (!p.IsHandleCreated) return;
            try { p.Invoke(new Action(() => { if (!p.IsDisposed) p.Image = img; })); }
            catch { }
        }

        private void DrawDefaultIcon(PictureBox pic, string type, bool isVideo = false)
        {
            if (!pic.IsHandleCreated) return;
            try
            {
                pic.Invoke(new Action(() =>
                {
                    if (pic.IsDisposed) return;
                    int w = Math.Max(pic.Width, 1), h = Math.Max(pic.Height, 1);
                    var bmp = new Bitmap(w, h);
                    using var g = Graphics.FromImage(bmp);
                    bool dk = ThemeManager.IsDarkMode;
                    g.Clear(dk ? Color.FromArgb(8, 8, 18) : Color.FromArgb(228, 228, 250));
                    using var gp = new Pen(Color.FromArgb(dk ? 14 : 20, ColAccent), 1);
                    for (int gx = 0; gx < w; gx += 20) g.DrawLine(gp, gx, 0, gx, h);
                    for (int gy = 0; gy < h; gy += 20) g.DrawLine(gp, 0, gy, w, gy);
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
                    g.DrawString(em, new Font("Segoe UI Emoji", 24F),
                        new SolidBrush(dk ? Color.FromArgb(50, 50, 80) : Color.FromArgb(170, 170, 210)),
                        new RectangleF(0, 0, w, h), fmt);

                    if (isVideo)
                    {
                        using var badgeBrush = new SolidBrush(Color.FromArgb(220, 0, 0, 0));
                        using var badgeTextBrush = new SolidBrush(Color.White);
                        var badgeRect = new Rectangle(6, h - 24, Math.Max(44, w - 12), 18);
                        g.FillRectangle(badgeBrush, badgeRect);
                        g.DrawString("VIDEO", new Font("Segoe UI", 7F, FontStyle.Bold),
                            badgeTextBrush, badgeRect,
                            new StringFormat
                            {
                                Alignment = StringAlignment.Center,
                                LineAlignment = StringAlignment.Center
                            });
                    }

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
            double speed = row["current_speed"] != DBNull.Value ? Convert.ToDouble(row["current_speed"]) : 0;
            string name = Esc(row["vehicle_name"]);
            string plate = Esc(row["plate_no"]);
            string status = Esc(row["status"]);
            string desc = Esc(row["description"]);
            string lastU = row["last_update"] != DBNull.Value
                ? Convert.ToDateTime(row["last_update"]).ToString("MMM dd, yyyy HH:mm") : "No data";
            string mapIcon = Esc(GetMarkerIconSource(row));

            await browser.CoreWebView2.ExecuteScriptAsync(
                $"focusVehicle({vid},'{name}','{plate}','{status}',{lat},{lng},{speed},'{lastU}','{mapIcon}','{desc}');");
        }

        // ════════════════════════════════════════════════════════════════════
        //  DETAIL OVERLAY
        // ════════════════════════════════════════════════════════════════════
        private void ShowDetailOverlay(DataRow row)
        {
            int vid = Convert.ToInt32(row["vehicle_id"]);
            bool dk = ThemeManager.IsDarkMode;
            Color bg = dk ? Color.FromArgb(4, 4, 12) : Color.FromArgb(251, 251, 255);
            Color cardBg = dk ? Color.FromArgb(10, 10, 24) : Color.White;
            Color border = dk ? Color.FromArgb(20, 20, 44) : Color.FromArgb(218, 218, 238);

            _carouselAutoTimer?.Stop();
            _carouselAutoTimer?.Dispose();
            _carouselAutoTimer = null;
            _overlay.Controls.Clear();
            _overlay.BackColor = bg;

            int pw = splitContainer.Panel2.Width;
            int ph = splitContainer.Panel2.Height;
            _overlay.Size = new Size(pw, ph);
            _overlay.Location = new Point(pw, 0);

            // Close
            var btnClose = new Button
            {
                Text = "← Back to Map",
                Size = new Size(140, 32),
                Location = new Point(14, 12),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = ColSub,
                BackColor = Color.FromArgb(16, ColSub),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderColor = border;
            btnClose.FlatAppearance.BorderSize = 1;
            btnClose.Click += (s, e) => HideDetailOverlay();
            _overlay.Controls.Add(btnClose);

            // Header
            string name = row["vehicle_name"]?.ToString() ?? "";
            string plate = row["plate_no"]?.ToString() ?? "";
            string status = row["status"]?.ToString() ?? "available";
            Color sc = StatusToColor(status);
            decimal ratePD = row["rate_per_day"] != DBNull.Value ? Convert.ToDecimal(row["rate_per_day"]) : 0;

            _overlay.Controls.Add(new Label
            {
                Text = name,
                Font = new Font("Segoe UI", 17F, FontStyle.Bold),
                ForeColor = ColText,
                AutoSize = true,
                Location = new Point(14, 52)
            });
            _overlay.Controls.Add(new Label
            {
                Text = plate + "  ·  " + (row["type"]?.ToString() ?? ""),
                Font = new Font("Segoe UI", 9F),
                ForeColor = ColSub,
                AutoSize = true,
                Location = new Point(16, 82)
            });
            _overlay.Controls.Add(new Label
            {
                Text = "  " + status.ToUpper() + "  ",
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(200, sc.R, sc.G, sc.B),
                AutoSize = true,
                Location = new Point(14, 106),
                Padding = new Padding(6, 3, 6, 3)
            });
            _overlay.Controls.Add(new Label
            {
                Text = "₱" + ratePD.ToString("N0") + " / day",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = ColAccent,
                AutoSize = true,
                Location = new Point(pw - 190, 54)
            });

            // Carousel
            List<VehicleMediaItem> mediaItems = ParseMediaItems(row["photo_url"]?.ToString() ?? "");
            int carouselTop = 132, carouselH = 220;

            var carouselPanel = new Panel
            {
                Location = new Point(0, carouselTop),
                Size = new Size(pw, carouselH),
                BackColor = dk ? Color.FromArgb(3, 3, 10) : Color.FromArgb(224, 224, 245)
            };

            var carouselPic = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Visible = false
            };

            var carouselVideo = new WebView2
            {
                Dock = DockStyle.Fill,
                Visible = false
            };

            var lblNoMedia = new Label
            {
                Text = "No media uploaded yet.",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ColSub,
                AutoSize = false,
                Size = new Size(pw, carouselH),
                Location = new Point(0, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };

            int currentPhoto = 0;
            var loadedMedia = new Dictionary<int, Image>();
            bool videoReady = false;
            Button btnPrev = null;
            Button btnNext = null;
            FlowLayoutPanel dotFlow = null;
            Label lblPhotoCount = null;

            void RestartCarouselAutoTimer()
            {
                _carouselAutoTimer?.Stop();
                if (mediaItems.Count <= 1) return;

                _carouselAutoTimer ??= new System.Windows.Forms.Timer();
                _carouselAutoTimer.Interval = 1500;
                _carouselAutoTimer.Tick -= CarouselAutoAdvance;
                _carouselAutoTimer.Tick += CarouselAutoAdvance;
                _carouselAutoTimer.Start();
            }

            async void CarouselAutoAdvance(object sender, EventArgs e)
            {
                if (!_overlayOpen || mediaItems.Count <= 1)
                {
                    _carouselAutoTimer?.Stop();
                    return;
                }

                currentPhoto = (currentPhoto + 1) % mediaItems.Count;
                await ShowCarouselItemAsync(currentPhoto);
            }

            async Task EnsureCarouselVideoReadyAsync()
            {
                if (videoReady || carouselVideo.IsDisposed) return;
                await carouselVideo.EnsureCoreWebView2Async(null);
                videoReady = true;
            }

            async Task ShowCarouselItemAsync(int index)
            {
                if (carouselPanel.IsDisposed || carouselPic.IsDisposed) return;

                btnPrev.Visible = mediaItems.Count > 1;
                btnNext.Visible = mediaItems.Count > 1;
                dotFlow.Visible = mediaItems.Count > 1;

                if (mediaItems.Count == 0)
                {
                    lblPhotoCount.Text = "No media";
                    lblNoMedia.Visible = true;
                    carouselPic.Visible = false;
                    carouselVideo.Visible = false;
                    return;
                }

                if (index < 0 || index >= mediaItems.Count)
                    index = 0;

                currentPhoto = index;
                var item = mediaItems[index];

                lblNoMedia.Visible = false;
                lblPhotoCount.Text = $"{index + 1} / {mediaItems.Count}";
                UpdateDots(carouselPanel, index, mediaItems.Count);

                if (item.IsVideo)
                {
                    carouselPic.Visible = false;
                    carouselVideo.Visible = true;
                    await EnsureCarouselVideoReadyAsync();
                    if (!carouselVideo.IsDisposed)
                        carouselVideo.NavigateToString(BuildVideoPreviewHtml(item.Source));
                    return;
                }

                if (videoReady && !carouselVideo.IsDisposed)
                    carouselVideo.NavigateToString("<html><body style='margin:0;background:#030308'></body></html>");

                carouselVideo.Visible = false;
                carouselPic.Visible = true;

                if (!loadedMedia.TryGetValue(index, out var img))
                {
                    img = await LoadImageFromSourceAsync(_http, item.Source);
                    if (img != null)
                        loadedMedia[index] = img;
                }

                if (img != null) SafeSetImage(carouselPic, img);
                else DrawDefaultIcon(carouselPic, row["type"]?.ToString() ?? "", false);
            }

            carouselPic.Cursor = mediaItems.Count > 0 ? Cursors.Hand : Cursors.Default;
            carouselPic.Click += (s, e) =>
            {
                if (mediaItems.Count == 0) return;
                ShowFleetMediaPreview(mediaItems[currentPhoto].Source, $"{name} Media Preview");
            };

            lblPhotoCount = new Label
            {
                Text = mediaItems.Count > 0 ? $"1 / {mediaItems.Count}" : "No media",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(160, 0, 0, 0),
                AutoSize = true,
                Location = new Point(pw - 74, carouselH - 28),
                Padding = new Padding(6, 2, 6, 2)
            };

            btnPrev = MakeCarouselBtn("‹", 0, carouselH);
            btnNext = MakeCarouselBtn("›", pw - 42, carouselH);
            btnPrev.Location = new Point(0, 0);
            btnNext.Location = new Point(pw - 42, 0);

            btnPrev.Click += (s, e) =>
            {
                if (mediaItems.Count == 0) return;
                currentPhoto = (currentPhoto - 1 + mediaItems.Count) % mediaItems.Count;
                RestartCarouselAutoTimer();
                _ = ShowCarouselItemAsync(currentPhoto);
            };
            btnNext.Click += (s, e) =>
            {
                if (mediaItems.Count == 0) return;
                currentPhoto = (currentPhoto + 1) % mediaItems.Count;
                RestartCarouselAutoTimer();
                _ = ShowCarouselItemAsync(currentPhoto);
            };

            dotFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(pw / 2 - (mediaItems.Count * 14) / 2, carouselH - 20)
            };
            for (int i = 0; i < mediaItems.Count; i++)
            {
                int idx = i;
                var dot = new Label
                {
                    Size = new Size(10, 10),
                    BackColor = i == 0 ? ColAccent : Color.FromArgb(70, 255, 255, 255),
                    Margin = new Padding(3),
                    Tag = idx,
                    Name = "dot_" + i,
                    Cursor = Cursors.Hand
                };
                dot.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.FillEllipse(new SolidBrush(dot.BackColor), 0, 0, dot.Width - 1, dot.Height - 1);
                };
                dot.Click += (s, e) =>
                {
                    if (mediaItems.Count == 0) return;
                    currentPhoto = idx;
                    RestartCarouselAutoTimer();
                    _ = ShowCarouselItemAsync(idx);
                };
                dotFlow.Controls.Add(dot);
            }

            carouselPanel.Controls.AddRange(new Control[]
                { carouselPic, carouselVideo, lblNoMedia, btnPrev, btnNext, lblPhotoCount, dotFlow });
            btnPrev.BringToFront(); btnNext.BringToFront();
            lblPhotoCount.BringToFront(); dotFlow.BringToFront();
            _overlay.Controls.Add(carouselPanel);
            _ = ShowCarouselItemAsync(0);
            RestartCarouselAutoTimer();

            // Scrollable body
            int actionBarH = 62;
            var scrollBody = new Panel
            {
                Location = new Point(0, carouselTop + carouselH),
                Size = new Size(pw, ph - carouselTop - carouselH - actionBarH),
                AutoScroll = true,
                BackColor = bg
            };
            _overlay.Controls.Add(scrollBody);

            int y = 18;

            // SPECIFICATIONS
            OverlaySectionLabel(scrollBody, "SPECIFICATIONS", 14, ref y);

            int seats = row["seat_capacity"] != DBNull.Value ? Convert.ToInt32(row["seat_capacity"]) : 5;
            string trans = row["transmission"]?.ToString() ?? "Automatic";
            string typeStr = row["type"]?.ToString() ?? "";
            string cc = row["cc"] != DBNull.Value ? row["cc"].ToString() + " cc" : "—";
            decimal rateWD = row["rate_with_driver"] != DBNull.Value ? Convert.ToDecimal(row["rate_with_driver"]) : 0;
            bool inGarage = row["in_garage"] != DBNull.Value && Convert.ToBoolean(row["in_garage"]);
            double lat = row["latitude"] != DBNull.Value ? Convert.ToDouble(row["latitude"]) : HQ_LAT;
            double lng = row["longitude"] != DBNull.Value ? Convert.ToDouble(row["longitude"]) : HQ_LNG;
            double curSpd = row["current_speed"] != DBNull.Value ? Convert.ToDouble(row["current_speed"]) : 0;
            string lastGPS = row["last_update"] != DBNull.Value
                ? Convert.ToDateTime(row["last_update"]).ToString("MMM dd HH:mm") : "—";
            double dist = CalculateDistance(HQ_LAT, HQ_LNG, lat, lng);

            var specs = new (string icon, string label, string val)[]
            {
                ("🚗","Type",         typeStr),
                ("⚙", "Engine",       cc),
                ("👥","Seats",        seats + " seats"),
                ("🔧","Transmission", trans),
                ("₱", "Rate / Day",   "₱" + ratePD.ToString("N0")),
                ("🚘","With Driver",  "₱" + rateWD.ToString("N0")),
                ("🏠","In Garage",    inGarage ? "Yes" : "No"),
                ("📍","Distance",     $"{dist:F1} km from HQ"),
                ("⚡","Speed",        curSpd > 0 ? $"{curSpd:F0} km/h" : "Parked"),
                ("📡","Last GPS",     lastGPS)
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
                        Size = new Size(specW, 56),
                        Location = new Point(14 + j * (specW + 6), y),
                        BackColor = cardBg
                    };
                    specCard.Paint += (s, e) =>
                    {
                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        using var path = RoundRect(new Rectangle(0, 0, specCard.Width - 1, specCard.Height - 1), 7);
                        using var pen = new Pen(border, 1);
                        e.Graphics.DrawPath(pen, path);
                        specCard.Region = new Region(path);
                    };
                    specCard.Controls.Add(new Label
                    {
                        Text = sp.label,
                        Font = new Font("Segoe UI", 6.5F),
                        ForeColor = ColSub,
                        AutoSize = true,
                        Location = new Point(10, 7)
                    });
                    specCard.Controls.Add(new Label
                    {
                        Text = sp.icon + " " + sp.val,
                        Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                        ForeColor = ColText,
                        AutoSize = false,
                        Size = new Size(specW - 14, 22),
                        Location = new Point(10, 24),
                        TextAlign = ContentAlignment.MiddleLeft
                    });
                    scrollBody.Controls.Add(specCard);
                }
                y += 62;
            }

            // DESCRIPTION
            y += 4;
            OverlaySectionLabel(scrollBody, "DESCRIPTION", 14, ref y);
            string descText = row["description"]?.ToString();
            if (string.IsNullOrWhiteSpace(descText)) descText = "No description available.";
            var descBox = new RichTextBox
            {
                Text = descText,
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
                descBox.Height = Math.Max(48, e.NewRectangle.Height + 6);
            scrollBody.Controls.Add(descBox);
            y += descBox.Height + 22;

            // CUSTOMER REVIEWS
            OverlaySectionLabel(scrollBody, "CUSTOMER REVIEWS", 14, ref y);
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
                y += 32;
            }
            else
            {
                double avg = 0;
                foreach (DataRow r in reviewsData.Rows)
                    if (r["vehicle_score"] != DBNull.Value) avg += Convert.ToDouble(r["vehicle_score"]);
                avg /= reviewsData.Rows.Count;

                var ratingCard = new Panel
                {
                    Location = new Point(14, y),
                    Size = new Size(pw - 28, 66),
                    BackColor = cardBg
                };
                ratingCard.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = RoundRect(new Rectangle(0, 0, ratingCard.Width - 1, ratingCard.Height - 1), 8);
                    using var pen = new Pen(border, 1);
                    e.Graphics.DrawPath(pen, path); ratingCard.Region = new Region(path);
                };
                ratingCard.Controls.Add(new Label
                {
                    Text = avg.ToString("F1"),
                    Font = new Font("Segoe UI", 26F, FontStyle.Bold),
                    ForeColor = ColYellow,
                    AutoSize = true,
                    Location = new Point(14, 10)
                });
                ratingCard.Controls.Add(new Label
                {
                    Text = new string('★', (int)Math.Round(avg)) + new string('☆', 5 - (int)Math.Round(avg)),
                    Font = new Font("Segoe UI", 13F),
                    ForeColor = ColYellow,
                    AutoSize = true,
                    Location = new Point(72, 12)
                });
                ratingCard.Controls.Add(new Label
                {
                    Text = $"{reviewsData.Rows.Count} review{(reviewsData.Rows.Count != 1 ? "s" : "")}",
                    Font = new Font("Segoe UI", 8.5F),
                    ForeColor = ColSub,
                    AutoSize = true,
                    Location = new Point(74, 40)
                });
                scrollBody.Controls.Add(ratingCard);
                y += 74;

                foreach (DataRow r in reviewsData.Rows)
                {
                    var rc = BuildReviewCard(r, pw - 28, cardBg, border, ColText, ColSub, ColYellow, y);
                    scrollBody.Controls.Add(rc);
                    y += rc.Height + 8;
                }
            }
            y += 24;

            // Action bar
            var actionBar = new Panel
            {
                Location = new Point(0, ph - actionBarH),
                Size = new Size(pw, actionBarH),
                BackColor = dk ? Color.FromArgb(4, 4, 12) : Color.FromArgb(238, 238, 250)
            };
            actionBar.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(border, 1), 0, 0, actionBar.Width, 0);
                e.Graphics.DrawLine(new Pen(Color.FromArgb(40, ColAccent), 1), 0, 1, actionBar.Width, 1);
            };

            var btnEdit2 = MakeBtn("✎  Edit", ColBlue, 14, 13, 100, 36);
            var btnDel2 = MakeBtn("⊗  Delete", ColRed, 122, 13, 94, 36);
            var btnTrack2 = MakeBtn("📍  Track", ColGreen, 224, 13, 100, 36);

            btnEdit2.Click += (s, e) => { HideDetailOverlay(); OnEditByRow(row); };
            btnDel2.Click += (s, e) => { HideDetailOverlay(); OnDeleteById(vid); };
            btnTrack2.Click += (s, e) => HideDetailOverlay();

            actionBar.Controls.AddRange(new Control[] { btnEdit2, btnDel2, btnTrack2 });
            _overlay.Controls.Add(actionBar);

            // Slide in
            _overlay.Visible = true;
            _overlay.BringToFront();
            _overlayOpen = true;

            _slideTimer?.Stop();
            _slideTimer = new System.Windows.Forms.Timer { Interval = 8 };
            _slideTimer.Tick += (s, e) =>
            {
                int cur = _overlay.Left;
                int step = Math.Max(1, Math.Abs(cur) / 4 + 2);
                if (cur - step <= 0) { _overlay.Left = 0; _slideTimer.Stop(); }
                else _overlay.Left = cur - step;
            };
            _slideTimer.Start();
        }

        private Button MakeCarouselBtn(string text, int x, int h)
        {
            var b = new Button
            {
                Text = text,
                Size = new Size(42, h),
                Location = new Point(x, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 22F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(70, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private void UpdateDots(Panel carousel, int active, int total)
        {
            foreach (Control c in carousel.Controls)
                if (c is FlowLayoutPanel fp)
                    foreach (Control d in fp.Controls)
                        if (d is Label dl && dl.Name.StartsWith("dot_"))
                            dl.BackColor = ((int)dl.Tag == active)
                                ? ColAccent : Color.FromArgb(70, 255, 255, 255);
        }

        private void HideDetailOverlay()
        {
            if (!_overlayOpen) return;
            int pw = splitContainer.Panel2.Width;
            _carouselAutoTimer?.Stop();
            _carouselAutoTimer?.Dispose();
            _carouselAutoTimer = null;
            _slideTimer?.Stop();
            _slideTimer = new System.Windows.Forms.Timer { Interval = 8 };
            _slideTimer.Tick += (s, e) =>
            {
                int cur = _overlay.Left;
                int step = Math.Max(1, (pw - cur) / 4 + 2);
                if (cur + step >= pw)
                {
                    _overlay.Left = pw; _overlay.Visible = false;
                    _overlayOpen = false; _slideTimer.Stop();
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

            var card = new Panel { Size = new Size(w, 88), Location = new Point(14, y), BackColor = bg };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundRect(new Rectangle(0, 0, w - 1, card.Height - 1), 7);
                using var pen = new Pen(border, 1);
                e.Graphics.DrawPath(pen, path); card.Region = new Region(path);
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
                Location = new Point(10, 34)
            });
            card.Controls.Add(new Label
            {
                Text = comment,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = text,
                AutoSize = false,
                Size = new Size(w - 20, 30),
                Location = new Point(10, 52),
                TextAlign = ContentAlignment.TopLeft
            });
            return card;
        }

        private static void OverlaySectionLabel(Panel p, string text, int x, ref int y)
        {
            p.Controls.Add(new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 150),
                AutoSize = true,
                Location = new Point(x, y)
            });
            y += 18;
            p.Controls.Add(new Panel
            {
                Location = new Point(x, y),
                Size = new Size(p.Width - x * 2, 1),
                BackColor = Color.FromArgb(24, 24, 54)
            });
            y += 9;
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
            if (lblCount.InvokeRequired) lblCount.Invoke(new Action(() => lblCount.Text = txt));
            else lblCount.Text = txt;
        }

        // ════════════════════════════════════════════════════════════════════
        //  MAP PUSH
        // ════════════════════════════════════════════════════════════════════
        private async Task PushAllMarkersAsync()
        {
            if (!_mapReady || browser?.CoreWebView2 == null) return;
            await browser.CoreWebView2.ExecuteScriptAsync("clearMarkers();");

            string assetsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets");
            string garageImgSrc = "https://appassets/garage_3D.png";
            if (string.IsNullOrEmpty(_garage3DBase64)) garageImgSrc = "";
            else if (!File.Exists(Path.Combine(assetsFolder, "garage_3D.png")))
                garageImgSrc = "data:image/png;base64," + _garage3DBase64;

            await browser.CoreWebView2.ExecuteScriptAsync(
                $"setHQ({HQ_LAT},{HQ_LNG},'{HQ_NAME}','{garageImgSrc}');");

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
            double spd = row["current_speed"] != DBNull.Value ? Convert.ToDouble(row["current_speed"]) : 0;
            string name = Esc(row["vehicle_name"]);
            string plate = Esc(row["plate_no"]);
            string type = Esc(row["type"]);
            string status = Esc(row["status"]);
            string lastU = row["last_update"] != DBNull.Value
                ? Convert.ToDateTime(row["last_update"]).ToString("MMM dd, HH:mm") : "No data";

            string mapIcon = GetMarkerIconSource(row);
            string desc = Esc(row["description"]);

            string js = $"updateVehicle({id},'{name}','{plate}','{type}','{status}'," +
                        $"{lat},{lng},{spd},'{lastU}','{Esc(mapIcon)}'," +
                        $"{(isLost ? "true" : "false")},'{desc}');";
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

            double dist = CalculateDistance(HQ_LAT, HQ_LNG, lat, lng);

            foreach (Control c in FlattenControls(card))
            {
                if (c.Name == "spd_" + vid && c is Label sl)
                {
                    sl.Text = speed > 0 ? $"⚡ {speed:F0} km/h" : "";
                    sl.Visible = speed > 0;
                    sl.ForeColor = speed > 80 ? ColRed : speed > 40 ? ColYellow : ColNeon;
                }
                if (c.Name == "dist_" + vid && c is Label dl)
                {
                    dl.Text = $"📍 {dist:F1} km";
                    dl.ForeColor = ColBlue;
                }
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
            topBar.BackColor = dk ? Color.FromArgb(4, 4, 14) : Color.FromArgb(248, 248, 255);
            bottomBar.BackColor = dk ? Color.FromArgb(4, 4, 12) : Color.FromArgb(238, 238, 250);
            cardScrollPanel.BackColor = ColBg;
            txtSearch.BackColor = dk ? Color.FromArgb(10, 10, 22) : Color.White;
            txtSearch.ForeColor = ColText;
            cboFilterStatus.BackColor = dk ? Color.FromArgb(10, 10, 22) : Color.White;
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
                BackColor = Color.FromArgb(20, color),
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

        private static string NormalizeMediaSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return "";
            string value = source.Trim();
            return string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) ? "" : value;
        }

        private static bool IsRemoteMediaUrl(string source) =>
            Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        private static string GetMediaExtension(string source)
        {
            source = NormalizeMediaSource(source);
            if (string.IsNullOrWhiteSpace(source)) return "";

            try
            {
                if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
                    !string.IsNullOrWhiteSpace(uri.AbsolutePath))
                    return Path.GetExtension(uri.AbsolutePath);
            }
            catch { }

            return Path.GetExtension(source);
        }

        private static VehicleMediaKind DetectMediaKind(string source)
        {
            string ext = GetMediaExtension(source);
            if (SupportedImageExtensions.Contains(ext)) return VehicleMediaKind.Image;
            if (SupportedVideoExtensions.Contains(ext)) return VehicleMediaKind.Video;
            return VehicleMediaKind.Unknown;
        }

        private static List<string> DeserializeMediaSources(string json)
        {
            var items = new List<string>();
            string raw = NormalizeMediaSource(json);
            if (string.IsNullOrWhiteSpace(raw)) return items;

            try
            {
                if (raw.TrimStart().StartsWith("["))
                    return JsonSerializer.Deserialize<List<string>>(raw) ?? new List<string>();
            }
            catch { }

            items.Add(raw);
            return items;
        }

        private static List<VehicleMediaItem> ParseMediaItems(string json)
        {
            var items = new List<VehicleMediaItem>();
            foreach (var source in DeserializeMediaSources(json))
            {
                var item = new VehicleMediaItem(source);
                if (!string.IsNullOrWhiteSpace(item.Source))
                    items.Add(item);
            }
            return items;
        }

        private static string GetPrimaryMediaPreviewSource(string json)
        {
            var items = ParseMediaItems(json);
            return items.FirstOrDefault(m => m.IsImage)?.Source
                ?? items.FirstOrDefault()?.Source
                ?? "";
        }

        private static string GetFirstPhoto(string json) =>
            ParseMediaItems(json).FirstOrDefault(m => m.IsImage)?.Source ?? "";

        private static List<string> GetAllPhotos(string json) =>
            ParseMediaItems(json).Select(m => m.Source).ToList();

        private string GetMarkerIconSource(DataRow row)
        {
            string mapIcon = NormalizeMediaSource(row["model_3d_url"]?.ToString() ?? "");
            if (DetectMediaKind(mapIcon) == VehicleMediaKind.Image)
                return mapIcon;

            return GetFirstPhoto(row["photo_url"]?.ToString() ?? "");
        }

        private static string SerializeMediaSources(IReadOnlyList<string> sources)
        {
            var items = sources
                .Select(NormalizeMediaSource)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (items.Count == 0) return "";
            if (items.Count == 1) return items[0];
            return JsonSerializer.Serialize(items);
        }

        private static async Task<Image> LoadImageFromSourceAsync(HttpClient client, string source)
        {
            source = NormalizeMediaSource(source);
            if (string.IsNullOrWhiteSpace(source)) return null;
            if (DetectMediaKind(source) == VehicleMediaKind.Video) return null;

            try
            {
                byte[] bytes = null;

                if (IsRemoteMediaUrl(source))
                    bytes = await client.GetByteArrayAsync(source);
                else if (File.Exists(source))
                    bytes = await File.ReadAllBytesAsync(source);

                if (bytes == null || bytes.Length == 0) return null;
                return Image.FromStream(new MemoryStream(bytes));
            }
            catch
            {
                return null;
            }
        }

        private static string BuildVideoPreviewHtml(string source)
        {
            string encodedUrl = System.Net.WebUtility.HtmlEncode(source);
            return "<html><body style='margin:0;background:#030308;display:flex;align-items:center;justify-content:center;height:100vh'>" +
                   $"<video src='{encodedUrl}' controls autoplay playsinline style='width:100%;height:100%;object-fit:contain;background:#000'></video>" +
                   "</body></html>";
        }

        private void ShowFleetMediaPreview(string source, string title)
        {
            string mediaSource = NormalizeMediaSource(source);
            if (string.IsNullOrWhiteSpace(mediaSource))
                return;

            using var dlg = new Form
            {
                Text = title,
                Size = new Size(980, 620),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(4, 4, 12) : Color.White
            };

            if (DetectMediaKind(mediaSource) == VehicleMediaKind.Video)
            {
                var web = new WebView2 { Dock = DockStyle.Fill };
                dlg.Controls.Add(web);
                dlg.Shown += async (s, e) =>
                {
                    await web.EnsureCoreWebView2Async(null);
                    web.NavigateToString(BuildVideoPreviewHtml(mediaSource));
                };
            }
            else
            {
                var pic = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Black,
                    SizeMode = PictureBoxSizeMode.Zoom
                };
                dlg.Controls.Add(pic);
                dlg.Shown += async (s, e) =>
                {
                    var img = await LoadImageFromSourceAsync(_http, mediaSource);
                    if (img != null)
                        pic.Image = img;
                };
            }

            dlg.ShowDialog(this);
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
            _carouselAutoTimer?.Stop(); _carouselAutoTimer?.Dispose(); _carouselAutoTimer = null;
            ThemeManager.ThemeChanged -= OnThemeChanged;
            browser?.Dispose();
            base.Dispose(disposing);
        }

        // ════════════════════════════════════════════════════════════════════
        //  VEHICLE FORM DIALOG
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
            private Button btnAddPhoto, btnBrowseMapIcon, btnSave;
            private Label lblUpload;
            private Panel scrollContent, scrollWrapper;
            private PictureBox _mapIconPreview;


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
                Color accent = ThemeManager.CurrentPrimary;

                Text = isEdit ? "✎  Edit Vehicle" : "✚  Add New Vehicle";
                Size = new Size(560, 800);
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                BackColor = bg;
                Font = new Font("Segoe UI", 9.5F);

                var hdr = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 56,
                    BackColor = dk ? Color.FromArgb(4, 4, 14) : Color.FromArgb(248, 248, 255)
                };
                hdr.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var br = new LinearGradientBrush(new Point(0, 54), new Point(hdr.Width, 54), accent, Color.Transparent);
                    e.Graphics.FillRectangle(br, 0, 54, hdr.Width, 2);
                    using var sp = new Pen(Color.FromArgb(dk ? 6 : 3, accent), 1);
                    for (int sy = 0; sy < 56; sy += 4)
                        e.Graphics.DrawLine(sp, 0, sy, hdr.Width, sy);
                };
                hdr.Controls.Add(new Label
                {
                    Text = isEdit ? "✎  Edit Vehicle Details" : "✚  Add New Vehicle",
                    Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                    ForeColor = accent,
                    AutoSize = true,
                    Location = new Point(16, 18)
                });
                Controls.Add(hdr);

                var footer = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 64,
                    BackColor = dk ? Color.FromArgb(4, 4, 12) : Color.FromArgb(238, 238, 252)
                };
                footer.Paint += (s, e) =>
                {
                    using var p = new Pen(ThemeManager.CurrentBorder, 1);
                    e.Graphics.DrawLine(p, 0, 0, footer.Width, 0);
                    using var glow = new Pen(Color.FromArgb(30, accent), 1);
                    e.Graphics.DrawLine(glow, 0, 1, footer.Width, 1);
                };

                btnSave = new Button
                {
                    Text = isEdit ? "💾  Save Changes" : "✚  Add Vehicle",
                    Size = new Size(210, 40),
                    Location = new Point((560 - 210) / 2 - 8, 12),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    BackColor = accent,
                    ForeColor = Color.White,
                    Cursor = Cursors.Hand
                };
                btnSave.FlatAppearance.BorderSize = 0;
                btnSave.Click += OnSave;
                footer.Controls.Add(btnSave);
                Controls.Add(footer);

                scrollWrapper = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    BackColor = bg
                };
                Controls.Add(scrollWrapper);

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

                int y = 16, lx = 16, vx = 170;
                int vw = 540 - vx - 28;

                txtBrand = AddField("Brand / Make:", lx, vx, ref y, vw);
                txtModel = AddField("Model:", lx, vx, ref y, vw);
                txtPlate = AddField("Plate No.:", lx, vx, ref y, vw);
                cboType = AddCombo("Vehicle Type:", lx, vx, ref y, vw, new[] { "Car", "Motorcycle", "Van", "Truck", "Bicycle" });
                txtCC = AddField("Engine CC:", lx, vx, ref y, vw);
                txtRate = AddField("Rate / Day (₱):", lx, vx, ref y, vw);
                txtRateDriver = AddField("Rate + Driver (₱):", lx, vx, ref y, vw);
                txtSeats = AddField("Seat Capacity:", lx, vx, ref y, vw);
                txtTrans = AddField("Transmission:", lx, vx, ref y, vw);
                cboStatus = AddCombo("Status:", lx, vx, ref y, vw, new[] { "available", "in-use", "maintenance" });

                AddSectionDivider(lx, ref y);
                AddFormLabel("Description:", lx, y + 5);

                txtDesc = new RichTextBox
                {
                    Location = new Point(lx, y + 24),
                    Width = scrollContent.Width - lx * 2,
                    Height = 76,
                    Font = new Font("Segoe UI", 9F),
                    BackColor = card,
                    ForeColor = text,
                    BorderStyle = BorderStyle.FixedSingle,
                    ScrollBars = RichTextBoxScrollBars.Vertical,
                    WordWrap = true
                };
                txtDesc.ContentsResized += (s, e) =>
                {
                    int nh = Math.Min(200, Math.Max(76, e.NewRectangle.Height + 10));
                    if (txtDesc.Height != nh) txtDesc.Height = nh;
                };
                scrollContent.Controls.Add(txtDesc);
                y += 24 + txtDesc.Height + 16;

                AddSectionDivider(lx, ref y);
                scrollContent.Controls.Add(new Label
                {
                    Text = "Vehicle Media",
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    ForeColor = ThemeManager.CurrentText,
                    AutoSize = true,
                    Location = new Point(lx, y + 4)
                });
                scrollContent.Controls.Add(new Label
                {
                    Text = $"(max {FleetPanel.MaxVehicleMediaItems} — images/videos — first image becomes the main preview)",
                    Font = new Font("Segoe UI", 7.5F),
                    ForeColor = ThemeManager.CurrentSubText,
                    AutoSize = true,
                    Location = new Point(lx + 120, y + 7)
                });
                y += 28;

                btnAddPhoto = new Button
                {
                    Text = "✚  Add Media",
                    Size = new Size(132, 34),
                    Location = new Point(lx, y),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    BackColor = Color.FromArgb(20, ThemeManager.CurrentPrimary),
                    ForeColor = ThemeManager.CurrentPrimary,
                    Cursor = Cursors.Hand
                };
                btnAddPhoto.FlatAppearance.BorderColor = ThemeManager.CurrentPrimary;
                btnAddPhoto.FlatAppearance.BorderSize = 1;
                btnAddPhoto.Click += OnAddPhoto;
                scrollContent.Controls.Add(btnAddPhoto);

                lblUpload = new Label
                {
                    Text = "",
                    Font = new Font("Segoe UI", 7.5F, FontStyle.Italic),
                    ForeColor = ThemeManager.CurrentPrimary,
                    AutoSize = true,
                    Location = new Point(lx + 136, y + 9),
                    Visible = false
                };
                scrollContent.Controls.Add(lblUpload);
                y += 44;

                thumbFlow = new FlowLayoutPanel
                {
                    Location = new Point(lx, y),
                    Size = new Size(scrollContent.Width - lx * 2, 96),
                    BackColor = dk ? Color.FromArgb(8, 8, 18) : Color.FromArgb(234, 234, 252),
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    AutoScroll = true,
                    Padding = new Padding(4)
                };
                thumbFlow.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = RoundRect2(new Rectangle(0, 0, thumbFlow.Width - 1, thumbFlow.Height - 1), 8);
                    using var pen = new Pen(ThemeManager.CurrentBorder, 1);
                    e.Graphics.DrawPath(pen, path);
                    thumbFlow.Region = new Region(path);
                };
                scrollContent.Controls.Add(thumbFlow);
                y += 106;

                AddSectionDivider(lx, ref y);
                AddFormLabel("Map / 3D Icon URL\n(image used for marker preview in app/map):", lx, y + 4);

                int iconFieldW = scrollContent.Width - lx * 2 - 110;
                txtMapIcon = new TextBox
                {
                    Size = new Size(iconFieldW, 30),
                    Location = new Point(lx, y + 46),
                    Font = new Font("Segoe UI", 8.5F),
                    BackColor = card,
                    ForeColor = text,
                    BorderStyle = BorderStyle.FixedSingle,
                    PlaceholderText = "https://… (auto-filled from first photo)"
                };
                scrollContent.Controls.Add(txtMapIcon);

                btnBrowseMapIcon = new Button
                {
                    Text = "📁 Browse",
                    Size = new Size(100, 30),
                    Location = new Point(lx + iconFieldW + 6, y + 46),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    BackColor = Color.FromArgb(20, ThemeManager.CurrentPrimary),
                    ForeColor = ThemeManager.CurrentPrimary,
                    Cursor = Cursors.Hand
                };
                btnBrowseMapIcon.FlatAppearance.BorderColor = ThemeManager.CurrentPrimary;
                btnBrowseMapIcon.FlatAppearance.BorderSize = 1;
                btnBrowseMapIcon.Click += OnBrowseMapIcon;
                scrollContent.Controls.Add(btnBrowseMapIcon);

                _mapIconPreview = new PictureBox
                {
                    Size = new Size(60, 60),
                    Location = new Point(lx, y + 82),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = dk ? Color.FromArgb(10, 10, 22) : Color.FromArgb(228, 228, 248),
                    BorderStyle = BorderStyle.FixedSingle,
                    Visible = false,
                    Cursor = Cursors.Hand
                };
                _mapIconPreview.Click += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(txtMapIcon.Text))
                        ShowMediaPreview(txtMapIcon.Text.Trim(), "Map Icon Preview");
                };
                scrollContent.Controls.Add(_mapIconPreview);

                txtMapIcon.TextChanged += async (s, e) =>
                {
                    string v = txtMapIcon.Text.Trim();
                    if (string.IsNullOrWhiteSpace(v) ||
                        FleetPanel.DetectMediaKind(v) != FleetPanel.VehicleMediaKind.Image)
                    {
                        _mapIconPreview.Visible = false;
                        return;
                    }

                    var img = await FleetPanel.LoadImageFromSourceAsync(_http2, v);
                    if (img != null)
                    {
                        _mapIconPreview.Image = img;
                        _mapIconPreview.Visible = true;
                    }
                    else
                    {
                        _mapIconPreview.Visible = false;
                    }
                };

                y += 96 + 66;
                scrollContent.Controls.Add(new Panel
                {
                    Location = new Point(0, y),
                    Size = new Size(1, 24),
                    BackColor = Color.Transparent
                });
                y += 24;
                scrollContent.Height = y;

                if (isEdit)
                {
                    txtBrand.Text = _existing["brand"]?.ToString() ?? "";
                    txtModel.Text = _existing["model"]?.ToString() ?? "";
                    txtPlate.Text = _existing["plate_no"]?.ToString() ?? "";
                    txtCC.Text = _existing["cc"] != DBNull.Value ? _existing["cc"].ToString() : "";
                    txtRate.Text = _existing["rate_per_day"] != DBNull.Value
                        ? Convert.ToDecimal(_existing["rate_per_day"]).ToString("0.00")
                        : "";
                    txtRateDriver.Text = _existing["rate_with_driver"] != DBNull.Value
                        ? Convert.ToDecimal(_existing["rate_with_driver"]).ToString("0.00")
                        : "";
                    txtSeats.Text = _existing["seat_capacity"]?.ToString() ?? "5";
                    txtTrans.Text = _existing["transmission"]?.ToString() ?? "Automatic";
                    txtDesc.Text = _existing["description"]?.ToString() ?? "";
                    txtMapIcon.Text = _existing["model_3d_url"]?.ToString() ?? "";

                    SelectCombo(cboType, _existing["type"]?.ToString() ?? "");
                    SelectCombo(cboStatus, _existing["status"]?.ToString() ?? "");

                    foreach (var url in FleetPanel.DeserializeMediaSources(_existing["photo_url"]?.ToString() ?? ""))
                    {
                        _photoUrls.Add(url);
                        AddThumbnail(url);
                    }
                }
            }

            private void AddSectionDivider(int lx, ref int y)
            {
                scrollContent.Controls.Add(new Panel
                {
                    Location = new Point(lx, y),
                    Size = new Size(scrollContent.Width - lx * 2, 1),
                    BackColor = ThemeManager.CurrentBorder
                });
                y += 12;
            }

            private async void OnBrowseMapIcon(object s, EventArgs e)
            {
                using var ofd = new OpenFileDialog
                {
                    Filter = "Image Files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp;*.jfif",
                    Title = "Select Map / Top-Down Icon"
                };

                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                btnBrowseMapIcon.Enabled = false;
                lblUpload.Text = "⬆ Uploading map icon…";
                lblUpload.ForeColor = ThemeManager.CurrentPrimary;
                lblUpload.Visible = true;

                string url = await UploadImageToApiAsync(ofd.FileName, true);

                if (!string.IsNullOrWhiteSpace(url))
                {
                    txtMapIcon.Text = url;
                    lblUpload.Text = "✅ Map icon uploaded";
                    lblUpload.ForeColor = Color.FromArgb(34, 197, 94);
                }
                else
                {
                    txtMapIcon.Text = ofd.FileName;
                    lblUpload.Text = "⚠ Local preview only — will retry upload on save";
                    lblUpload.ForeColor = Color.FromArgb(245, 158, 11);
                }

                btnBrowseMapIcon.Enabled = true;
                await Task.Delay(2500);
                if (!lblUpload.IsDisposed) lblUpload.Visible = false;
            }

            private async void OnAddPhoto(object s, EventArgs e)
            {
                if (_photoUrls.Count >= FleetPanel.MaxVehicleMediaItems)
                {
                    MessageBox.Show($"Maximum {FleetPanel.MaxVehicleMediaItems} media files per vehicle.", "Limit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var ofd = new OpenFileDialog
                {
                    Filter = "Supported Media|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp;*.jfif;*.mp4;*.webm;*.mov;*.m4v",
                    Title = "Select Vehicle Media",
                    Multiselect = true
                };

                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                btnAddPhoto.Enabled = false;
                btnSave.Enabled = false;

                try
                {
                    foreach (string filePath in ofd.FileNames)
                    {
                        if (_photoUrls.Count >= FleetPanel.MaxVehicleMediaItems)
                            break;

                        lblUpload.Text = $"⬆ Uploading {Path.GetFileName(filePath)}…";
                        lblUpload.ForeColor = ThemeManager.CurrentPrimary;
                        lblUpload.Visible = true;

                        string uploadedUrl = await UploadImageToApiAsync(filePath, false);

                        string finalUrl = uploadedUrl ?? filePath;
                        _photoUrls.Add(finalUrl);
                        AddThumbnail(finalUrl);

                        if (!string.IsNullOrWhiteSpace(uploadedUrl))
                        {
                            lblUpload.Text = $"✅ Uploaded ({_photoUrls.Count}/{FleetPanel.MaxVehicleMediaItems})";
                            lblUpload.ForeColor = Color.FromArgb(34, 197, 94);

                            if (FleetPanel.DetectMediaKind(uploadedUrl) == FleetPanel.VehicleMediaKind.Image &&
                                string.IsNullOrWhiteSpace(txtMapIcon.Text))
                                txtMapIcon.Text = uploadedUrl;
                        }
                        else
                        {
                            lblUpload.Text = "⚠ Upload failed — queued locally, will retry on save";
                            lblUpload.ForeColor = Color.FromArgb(245, 158, 11);

                            if (FleetPanel.DetectMediaKind(filePath) == FleetPanel.VehicleMediaKind.Image &&
                                string.IsNullOrWhiteSpace(txtMapIcon.Text))
                                txtMapIcon.Text = filePath;
                        }
                    }
                }
                finally
                {
                    btnAddPhoto.Enabled = _photoUrls.Count < FleetPanel.MaxVehicleMediaItems;
                    btnSave.Enabled = true;

                    await Task.Delay(2500);
                    if (!lblUpload.IsDisposed) lblUpload.Visible = false;
                }
            }

            private void AddThumbnail(string url)
            {
                bool dk = ThemeManager.IsDarkMode;
                int idx = _photoUrls.IndexOf(url);
                var kind = FleetPanel.DetectMediaKind(url);

                var wrap = new Panel
                {
                    Size = new Size(82, 82),
                    Margin = new Padding(4),
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };

                var pic = new PictureBox
                {
                    Size = new Size(82, 82),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = dk ? Color.FromArgb(10, 10, 22) : Color.FromArgb(226, 226, 248),
                    Cursor = Cursors.Hand
                };

                pic.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = RoundRect2(new Rectangle(0, 0, pic.Width - 1, pic.Height - 1), 8);
                    using var pen = new Pen(ThemeManager.CurrentBorder, 1);
                    e.Graphics.DrawPath(pen, path);
                    pic.Region = new Region(path);
                };

                Label star = null;
                if (idx == GetPrimaryThumbnailIndex())
                {
                    star = new Label
                    {
                        Text = "★",
                        Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                        ForeColor = Color.White,
                        BackColor = Color.FromArgb(210, 230, 81, 0),
                        AutoSize = true,
                        Location = new Point(3, 3),
                        Padding = new Padding(2)
                    };
                    wrap.Controls.Add(star);
                }

                if (kind == FleetPanel.VehicleMediaKind.Video)
                {
                    var videoBadge = new Label
                    {
                        Text = "VIDEO",
                        Font = new Font("Segoe UI", 6.5F, FontStyle.Bold),
                        ForeColor = Color.White,
                        BackColor = Color.FromArgb(190, 0, 0, 0),
                        AutoSize = false,
                        Size = new Size(48, 16),
                        Location = new Point(17, 60),
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    wrap.Controls.Add(videoBadge);
                    videoBadge.BringToFront();
                }

                var btnX = new Label
                {
                    Text = "✕",
                    Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.FromArgb(180, 239, 68, 68),
                    Size = new Size(18, 18),
                    Location = new Point(61, 3),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Cursor = Cursors.Hand
                };

                btnX.Click += (s, e) =>
                {
                    _photoUrls.Remove(url);
                    thumbFlow.Controls.Remove(wrap);
                    RefreshPrimaryPhotoStar();
                };

                EventHandler openPreview = (s, e) => ShowMediaPreview(url, "Vehicle Media Preview");
                wrap.Click += openPreview;
                pic.Click += openPreview;

                _ = Task.Run(async () =>
                {
                    Image img = kind == FleetPanel.VehicleMediaKind.Image
                        ? await FleetPanel.LoadImageFromSourceAsync(_http2, url)
                        : CreateMediaPlaceholderBitmap(pic.Size, kind);

                    if (img != null && !pic.IsDisposed)
                    {
                        pic.Invoke(new Action(() =>
                        {
                            if (!pic.IsDisposed) pic.Image = img;
                        }));
                    }
                });

                wrap.Controls.Add(pic);
                wrap.Controls.Add(btnX);
                btnX.BringToFront();
                if (star != null) star.BringToFront();

                thumbFlow.Controls.Add(wrap);
            }

            private void RefreshPrimaryPhotoStar()
            {
                int primaryIndex = GetPrimaryThumbnailIndex();

                for (int i = 0; i < thumbFlow.Controls.Count; i++)
                {
                    if (thumbFlow.Controls[i] is not Panel panel)
                        continue;

                    Label existingStar = null;
                    foreach (Control c in panel.Controls)
                    {
                        if (c is Label lbl && lbl.Text == "★")
                        {
                            existingStar = lbl;
                            break;
                        }
                    }

                    if (i == primaryIndex)
                    {
                        if (existingStar == null)
                        {
                            existingStar = new Label
                            {
                                Text = "★",
                                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                                ForeColor = Color.White,
                                BackColor = Color.FromArgb(210, 230, 81, 0),
                                AutoSize = true,
                                Location = new Point(3, 3),
                                Padding = new Padding(2)
                            };
                            panel.Controls.Add(existingStar);
                        }
                        existingStar.Visible = true;
                        existingStar.BringToFront();
                    }
                    else if (existingStar != null)
                    {
                        existingStar.Visible = false;
                    }
                }
            }

            private async void OnSave(object s, EventArgs e)
            {
                if (string.IsNullOrWhiteSpace(txtBrand.Text) || string.IsNullOrWhiteSpace(txtPlate.Text))
                {
                    MessageBox.Show("Brand and Plate No. are required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!decimal.TryParse(txtRate.Text, out decimal rate))
                {
                    MessageBox.Show("Invalid daily rate — enter a number.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!decimal.TryParse(txtRateDriver.Text, out decimal rateDriver)) rateDriver = 0;
                if (!int.TryParse(txtSeats.Text, out int seats)) seats = 5;
                if (!int.TryParse(txtCC.Text, out int cc)) cc = 0;

                btnSave.Enabled = false;
                btnAddPhoto.Enabled = false;
                btnBrowseMapIcon.Enabled = false;

                try
                {
                    var resolvedMedia = await EnsurePersistableVehicleMediaAsync();
                    if (resolvedMedia == null)
                        return;

                    var mapIconResult = await ResolveMapIconForSaveAsync(resolvedMedia);
                    if (!mapIconResult.ok)
                        return;

                    string photoJson = FleetPanel.SerializeMediaSources(resolvedMedia);
                    string mapIcon = mapIconResult.value;

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
                    cmd.Parameters.AddWithValue("@photo", string.IsNullOrWhiteSpace(photoJson) ? DBNull.Value : photoJson);
                    cmd.Parameters.AddWithValue("@desc", txtDesc.Text.Trim());
                    cmd.Parameters.AddWithValue("@seats", seats);
                    cmd.Parameters.AddWithValue("@trans", string.IsNullOrWhiteSpace(txtTrans.Text) ? "Automatic" : txtTrans.Text.Trim());
                    cmd.Parameters.AddWithValue("@mapicon", string.IsNullOrWhiteSpace(mapIcon) ? DBNull.Value : mapIcon);

                    if (_existing != null)
                        cmd.Parameters.AddWithValue("@id", _existing["vehicle_id"]);

                    cmd.ExecuteNonQuery();
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("DB Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    if (!IsDisposed)
                    {
                        btnSave.Enabled = true;
                        btnAddPhoto.Enabled = _photoUrls.Count < FleetPanel.MaxVehicleMediaItems;
                        btnBrowseMapIcon.Enabled = true;
                    }
                }
            }


            private async Task<string> UploadImageToApiAsync(string path, bool isMapIcon = false)
            {
                try
                {
                    using var form = new MultipartFormDataContent();
                    await using var fs = File.OpenRead(path);

                    var fileContent = new StreamContent(fs);
                    fileContent.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                    form.Add(fileContent, "file", Path.GetFileName(path));

                    string endpoint = isMapIcon
                        ? ApiService.BuildUrl("upload/map-icon")
                        : ApiService.BuildUrl("upload/vehicle-image");

                    var response = await _http2.PostAsync(endpoint, form);
                    var json = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        MessageBox.Show(
                            $"Upload failed ({(int)response.StatusCode}): {json}",
                            "Upload Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return null;
                    }

                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("url", out var urlProp))
                        return urlProp.GetString();

                    MessageBox.Show(
                        "Upload failed: URL not found in response.\n\n" + json,
                        "Upload Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Upload error: " + ex.Message,
                        "Upload Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                return null;
            }

            private static List<string> GetAllPhotos2(string json)
            {
                return FleetPanel.DeserializeMediaSources(json);
            }

            private int GetPrimaryThumbnailIndex()
            {
                if (_photoUrls.Count == 0) return -1;

                for (int i = 0; i < _photoUrls.Count; i++)
                {
                    if (FleetPanel.DetectMediaKind(_photoUrls[i]) == FleetPanel.VehicleMediaKind.Image)
                        return i;
                }

                return 0;
            }

            private static Image CreateMediaPlaceholderBitmap(Size size, FleetPanel.VehicleMediaKind kind)
            {
                int width = Math.Max(size.Width, 1);
                int height = Math.Max(size.Height, 1);
                var bmp = new Bitmap(width, height);

                using var g = Graphics.FromImage(bmp);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(14, 14, 24));

                using var gridPen = new Pen(Color.FromArgb(30, 230, 81, 0), 1);
                for (int gx = 0; gx < width; gx += 16)
                    g.DrawLine(gridPen, gx, 0, gx, height);
                for (int gy = 0; gy < height; gy += 16)
                    g.DrawLine(gridPen, 0, gy, width, gy);

                string glyph = kind == FleetPanel.VehicleMediaKind.Video ? "▶" : "⬡";
                using var glyphBrush = new SolidBrush(Color.FromArgb(220, 255, 255, 255));
                using var glyphFont = new Font("Segoe UI Symbol", 18F, FontStyle.Bold);
                var centerRect = new RectangleF(0, 10, width, height - 30);
                var centerFmt = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(glyph, glyphFont, glyphBrush, centerRect, centerFmt);

                using var badgeBrush = new SolidBrush(Color.FromArgb(200, 0, 0, 0));
                using var badgeTextBrush = new SolidBrush(Color.White);
                using var badgeFont = new Font("Segoe UI", 7F, FontStyle.Bold);
                var badgeRect = new Rectangle(8, height - 24, width - 16, 16);
                g.FillRectangle(badgeBrush, badgeRect);
                g.DrawString(kind == FleetPanel.VehicleMediaKind.Video ? "VIDEO" : "MEDIA",
                    badgeFont, badgeTextBrush, badgeRect, centerFmt);

                return bmp;
            }

            private async Task<List<string>> EnsurePersistableVehicleMediaAsync()
            {
                var resolved = new List<string>();

                foreach (var source in _photoUrls.Select(FleetPanel.NormalizeMediaSource).Where(s => !string.IsNullOrWhiteSpace(s)))
                {
                    string remote = await EnsureRemoteMediaAsync(source, false, "vehicle media");
                    if (remote == null)
                        return null;

                    resolved.Add(remote);
                }

                _photoUrls.Clear();
                _photoUrls.AddRange(resolved);
                return resolved;
            }

            private async Task<(bool ok, string value)> ResolveMapIconForSaveAsync(IReadOnlyList<string> resolvedMedia)
            {
                string mapIcon = FleetPanel.NormalizeMediaSource(txtMapIcon.Text);

                if (string.IsNullOrWhiteSpace(mapIcon))
                {
                    mapIcon = resolvedMedia.FirstOrDefault(m =>
                        FleetPanel.DetectMediaKind(m) == FleetPanel.VehicleMediaKind.Image) ?? "";
                }

                if (string.IsNullOrWhiteSpace(mapIcon))
                    return (true, "");

                if (FleetPanel.DetectMediaKind(mapIcon) == FleetPanel.VehicleMediaKind.Video)
                {
                    MessageBox.Show(
                        "Map / 3D icon must be an image file, not a video.",
                        "Validation",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return (false, "");
                }

                string remote = await EnsureRemoteMediaAsync(mapIcon, true, "map icon");
                if (remote == null)
                    return (false, "");

                txtMapIcon.Text = remote;
                return (true, remote);
            }

            private async Task<string> EnsureRemoteMediaAsync(string source, bool isMapIcon, string label)
            {
                string normalized = FleetPanel.NormalizeMediaSource(source);
                if (string.IsNullOrWhiteSpace(normalized))
                    return "";

                if (FleetPanel.IsRemoteMediaUrl(normalized))
                    return normalized;

                if (!File.Exists(normalized))
                {
                    MessageBox.Show(
                        $"The {label} file was not found:\n\n{normalized}\n\nIt would not be accessible from the mobile app.",
                        "Missing File",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return null;
                }

                if (isMapIcon && FleetPanel.DetectMediaKind(normalized) != FleetPanel.VehicleMediaKind.Image)
                {
                    MessageBox.Show(
                        "Map / 3D icon must be an image file.",
                        "Validation",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return null;
                }

                string uploaded = await UploadImageToApiAsync(normalized, isMapIcon);
                if (string.IsNullOrWhiteSpace(uploaded))
                {
                    MessageBox.Show(
                        $"The {label} could not be uploaded. The save was stopped so the app will not receive a broken local path.",
                        "Upload Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return null;
                }

                return uploaded;
            }

            private void ShowMediaPreview(string source, string title)
            {
                string mediaSource = FleetPanel.NormalizeMediaSource(source);
                if (string.IsNullOrWhiteSpace(mediaSource))
                    return;

                using var dlg = new Form
                {
                    Text = title,
                    Size = new Size(860, 560),
                    StartPosition = FormStartPosition.CenterParent,
                    BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(4, 4, 12) : Color.White
                };

                if (FleetPanel.DetectMediaKind(mediaSource) == FleetPanel.VehicleMediaKind.Video)
                {
                    var web = new WebView2 { Dock = DockStyle.Fill };
                    dlg.Controls.Add(web);
                    dlg.Shown += async (s, e) =>
                    {
                        await web.EnsureCoreWebView2Async(null);
                        web.NavigateToString(FleetPanel.BuildVideoPreviewHtml(mediaSource));
                    };
                }
                else
                {
                    var pic = new PictureBox
                    {
                        Dock = DockStyle.Fill,
                        BackColor = Color.Black,
                        SizeMode = PictureBoxSizeMode.Zoom
                    };
                    dlg.Controls.Add(pic);
                    dlg.Shown += async (s, e) =>
                    {
                        var img = await FleetPanel.LoadImageFromSourceAsync(_http2, mediaSource);
                        if (img != null)
                            pic.Image = img;
                        else
                            pic.Image = CreateMediaPlaceholderBitmap(pic.Size, FleetPanel.VehicleMediaKind.Unknown);
                    };
                }

                dlg.ShowDialog(this);
            }

            private TextBox AddField(string label, int lx, int vx, ref int y, int vw)
            {
                AddFormLabel(label, lx, y + 7);
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
                AddFormLabel(label, lx, y + 7);
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

            private void AddFormLabel(string text, int x, int y)
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
                {
                    if (string.Equals(cb.Items[i].ToString(), value, StringComparison.OrdinalIgnoreCase))
                    {
                        cb.SelectedIndex = i;
                        return;
                    }
                }
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
