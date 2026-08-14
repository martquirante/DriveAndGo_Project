#nullable disable
using DriveAndGo_Admin.Helpers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
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
        public const int MaxVehicleMediaItems = 8;

        public static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".jfif"
        };

        public static readonly HashSet<string> SupportedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".webm", ".mov", ".m4v"
        };

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        private static readonly HttpClient _firebaseClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        private WebView2 _fleetWebView;
        private System.Windows.Forms.Timer _liveTimer;
        private System.Windows.Forms.Timer _dbRefreshTimer;

        public enum VehicleMediaKind
        {
            Unknown,
            Image,
            Video
        }

        public FleetPanel()
        {
            this.Controls.Clear();
            this.Dock = DockStyle.Fill;
            this.BackColor = ThemeManager.CurrentBackground;

            ThemeManager.ThemeChanged += ThemeChanged_Handler;

            this.HandleCreated += (s, e) => BuildWebFleet();

            this.VisibleChanged += (s, e) =>
            {
                if (this.Visible && this.IsHandleCreated && !this.IsDisposed)
                {
                    RefreshWebViewData();
                    PushThemeToWebView(ThemeManager.IsDarkMode ? "dark" : "light");
                }
            };

            _liveTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _liveTimer.Tick += async (s, e) => await PollFirebaseGPS();
            _liveTimer.Start();

            _dbRefreshTimer = new System.Windows.Forms.Timer { Interval = 15000 };
            _dbRefreshTimer.Tick += (s, e) => RefreshWebViewData();
            _dbRefreshTimer.Start();
        }

        private void ThemeChanged_Handler(object sender, EventArgs e)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            this.BackColor = ThemeManager.CurrentBackground;
            PushThemeToWebView(ThemeManager.IsDarkMode ? "dark" : "light");
        }

        private async void BuildWebFleet()
        {
            try
            {
                string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "FleetOverview.html");

                if (!File.Exists(htmlPath))
                {
                    Console.WriteLine("[FleetPanel] FleetOverview.html not found.");
                    return;
                }

                _fleetWebView = new WebView2 { Dock = DockStyle.Fill };
                this.Controls.Add(_fleetWebView);
                _fleetWebView.BringToFront();

                var env = await CoreWebView2Environment.CreateAsync(
                    null, Path.Combine(Path.GetTempPath(), "DriveAndGo_FleetWV2"));

                await _fleetWebView.EnsureCoreWebView2Async(env);

                _fleetWebView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;

                _fleetWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _fleetWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _fleetWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;

                string token = SessionManager.Token ?? string.Empty;
                string apiBase = ApiService.BaseUrl.TrimEnd('/');
                string currentTheme = ThemeManager.IsDarkMode ? "dark" : "light";

                await _fleetWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                    $"window.API_BASE_URL = '{apiBase}'; window.AUTH_TOKEN = '{token}'; document.documentElement.setAttribute('data-theme', '{currentTheme}');");

                _fleetWebView.CoreWebView2.NavigationCompleted += async (sender, args) =>
                {
                    if (_fleetWebView == null || _fleetWebView.IsDisposed || _fleetWebView.CoreWebView2 == null) return;
                    try
                    {
                        await _fleetWebView.CoreWebView2.ExecuteScriptAsync(
                            $"window.API_BASE_URL = '{apiBase}'; window.AUTH_TOKEN = '{token}';" +
                            $"if(window.setFleetTheme) window.setFleetTheme('{currentTheme}');" +
                            "if(window.refreshFleetData) window.refreshFleetData();");
                    }
                    catch { }
                };

                _fleetWebView.CoreWebView2.Navigate("file:///" + htmlPath.Replace('\\', '/') + "?v=" + DateTime.UtcNow.Ticks);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[FleetPanel] BuildWebFleet failed: " + ex.Message);
            }
        }

        public void RefreshWebViewData()
        {
            if (!this.IsHandleCreated || this.IsDisposed || _fleetWebView == null) return;
            this.BeginInvoke((MethodInvoker)(async () =>
            {
                try
                {
                    if (_fleetWebView.IsDisposed || _fleetWebView.CoreWebView2 == null) return;
                    string token = SessionManager.Token ?? string.Empty;
                    string apiBase = ApiService.BaseUrl.TrimEnd('/');
                    string initJs = $"window.API_BASE_URL='{apiBase}'; window.AUTH_TOKEN='{token}';"
                                  + " if(window.refreshFleetData) window.refreshFleetData();";
                    await _fleetWebView.CoreWebView2.ExecuteScriptAsync(initJs);
                }
                catch { }
            }));
        }

        public void PushThemeToWebView(string theme)
        {
            if (!this.IsHandleCreated || this.IsDisposed || _fleetWebView == null) return;
            this.BeginInvoke((MethodInvoker)(async () =>
            {
                try
                {
                    if (_fleetWebView.IsDisposed || _fleetWebView.CoreWebView2 == null) return;
                    string safeTheme = theme == "light" ? "light" : "dark";
                    await _fleetWebView.CoreWebView2.ExecuteScriptAsync($"if(window.setFleetTheme) window.setFleetTheme('{safeTheme}');");
                }
                catch { }
            }));
        }

        private async Task PollFirebaseGPS()
        {
            if (!this.IsHandleCreated || this.IsDisposed || _fleetWebView == null) return;
            try
            {
                string fireUrl = "https://vechiclerentaldb-default-rtdb.asia-southeast1.firebasedatabase.app/vehicle_locations.json";
                var resp = await _firebaseClient.GetAsync(fireUrl);
                if (!resp.IsSuccessStatusCode) return;

                string json = await resp.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json) || json == "null") return;

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return;

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (int.TryParse(prop.Name, out int vid))
                    {
                        var vObj = prop.Value;
                        double lat = vObj.TryGetProperty("lat", out var pLat) ? pLat.GetDouble() : 0.0;
                        double lng = vObj.TryGetProperty("lng", out var pLng) ? pLng.GetDouble() : 0.0;
                        double spd = vObj.TryGetProperty("speed", out var pSpd) ? pSpd.GetDouble() : 0.0;

                        if (lat != 0.0 && lng != 0.0)
                        {
                            this.BeginInvoke((MethodInvoker)(async () =>
                            {
                                try
                                {
                                    if (_fleetWebView != null && !_fleetWebView.IsDisposed && _fleetWebView.CoreWebView2 != null)
                                    {
                                        await _fleetWebView.CoreWebView2.ExecuteScriptAsync(
                                            $"if(window.liveUpdateGPS) window.liveUpdateGPS({vid}, {lat}, {lng}, {spd});");
                                    }
                                }
                                catch { }
                            }));
                        }
                    }
                }
            }
            catch { }
        }

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string rawStr = e.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(rawStr)) return;

                if (rawStr == "open_vehicle_form")
                {
                    this.BeginInvoke((MethodInvoker)(() => HandleAddVehicleFromReact()));
                }
                else if (rawStr.StartsWith("edit_vehicle:"))
                {
                    string idStr = rawStr.Substring("edit_vehicle:".Length);
                    if (int.TryParse(idStr, out int vId))
                    {
                        this.BeginInvoke((MethodInvoker)(() => HandleEditVehicleFromReact(vId)));
                    }
                }
                else if (rawStr.StartsWith("delete_vehicle:"))
                {
                    string idStr = rawStr.Substring("delete_vehicle:".Length);
                    if (int.TryParse(idStr, out int vId))
                    {
                        this.BeginInvoke((MethodInvoker)(() => HandleDeleteVehicleFromReact(vId)));
                    }
                }
                else if (rawStr.StartsWith("open_media_preview:"))
                {
                    string url = rawStr.Substring("open_media_preview:".Length);
                    this.BeginInvoke((MethodInvoker)(() => ShowFleetMediaPreview(url, "Vehicle Media Preview")));
                }
            }
            catch { }
        }

        private void HandleAddVehicleFromReact()
        {
            try
            {
                string connStr = ApiService.BaseUrl;
                using var dlg = new VehicleFormDialog(null, connStr);
                if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
                {
                    RefreshWebViewData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error opening Add Vehicle dialog: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void HandleEditVehicleFromReact(int vehicleId)
        {
            try
            {
                var result = await ApiService.GetAsync($"vehicles/{vehicleId}");
                if (!result.Success || string.IsNullOrEmpty(result.Body))
                {
                    MessageBox.Show("Failed to load vehicle details for editing.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                using var doc = JsonDocument.Parse(result.Body);
                var root = doc.RootElement;

                DataTable dt = CreateEmptyVehicleSchema();
                DataRow row = dt.NewRow();
                PopulateRowFromJson(row, root);
                dt.Rows.Add(row);

                string connStr = ApiService.BaseUrl;
                using var dlg = new VehicleFormDialog(row, connStr);
                if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
                {
                    RefreshWebViewData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error editing vehicle: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void HandleDeleteVehicleFromReact(int vehicleId)
        {
            try
            {
                var confirm = MessageBox.Show(
                    "Are you sure you want to permanently delete this vehicle record?",
                    "Confirm Delete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm == DialogResult.Yes)
                {
                    var res = await ApiService.DeleteAsync($"vehicles/{vehicleId}");
                    if (res.Success)
                    {
                        MessageBox.Show("Vehicle deleted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        RefreshWebViewData();
                    }
                    else
                    {
                        MessageBox.Show("Failed to delete vehicle: " + (res.Error ?? res.Body), "Delete Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Delete error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private DataTable CreateEmptyVehicleSchema()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("vehicle_id", typeof(int));
            dt.Columns.Add("vehicle_name", typeof(string));
            dt.Columns.Add("brand", typeof(string));
            dt.Columns.Add("model", typeof(string));
            dt.Columns.Add("plate_no", typeof(string));
            dt.Columns.Add("type", typeof(string));
            dt.Columns.Add("cc", typeof(string));
            dt.Columns.Add("status", typeof(string));
            dt.Columns.Add("rate_per_day", typeof(decimal));
            dt.Columns.Add("rate_with_driver", typeof(decimal));
            dt.Columns.Add("photo_url", typeof(string));
            dt.Columns.Add("description", typeof(string));
            dt.Columns.Add("seat_capacity", typeof(int));
            dt.Columns.Add("transmission", typeof(string));
            dt.Columns.Add("model_3d_url", typeof(string));
            dt.Columns.Add("latitude", typeof(double));
            dt.Columns.Add("longitude", typeof(double));
            dt.Columns.Add("current_speed", typeof(double));
            dt.Columns.Add("last_update", typeof(DateTime));
            dt.Columns.Add("in_garage", typeof(bool));
            dt.Columns.Add("is_lost", typeof(int));
            return dt;
        }

        private void PopulateRowFromJson(DataRow row, JsonElement elem)
        {
            int vid = elem.TryGetProperty("vehicleId", out var v1) ? v1.GetInt32() :
                      elem.TryGetProperty("vehicle_id", out var v2) ? v2.GetInt32() :
                      elem.TryGetProperty("id", out var v3) ? v3.GetInt32() : 0;

            string brand = elem.TryGetProperty("brand", out var b1) ? b1.GetString() : "";
            string model = elem.TryGetProperty("model", out var m1) ? m1.GetString() : "";

            row["vehicle_id"] = vid;
            row["vehicle_name"] = $"{brand} {model}".Trim();
            row["brand"] = brand;
            row["model"] = model;
            row["plate_no"] = elem.TryGetProperty("plateNo", out var p1) ? p1.GetString() :
                              elem.TryGetProperty("plate_no", out var p2) ? p2.GetString() : "";
            row["type"] = elem.TryGetProperty("type", out var t1) ? t1.GetString() : "Car";
            row["cc"] = elem.TryGetProperty("cc", out var c1) ? c1.ToString() : "1500";
            row["status"] = elem.TryGetProperty("status", out var s1) ? s1.GetString() : "available";
            row["rate_per_day"] = elem.TryGetProperty("ratePerDay", out var r1) ? r1.GetDecimal() :
                                  elem.TryGetProperty("rate_per_day", out var r2) ? r2.GetDecimal() : 0m;
            row["rate_with_driver"] = elem.TryGetProperty("rateWithDriver", out var rd1) ? rd1.GetDecimal() :
                                       elem.TryGetProperty("rate_with_driver", out var rd2) ? rd2.GetDecimal() : 0m;
            row["photo_url"] = elem.TryGetProperty("photoUrl", out var ph1) ? ph1.GetString() :
                               elem.TryGetProperty("photo_url", out var ph2) ? ph2.GetString() : "";
            row["description"] = elem.TryGetProperty("description", out var d1) ? d1.GetString() : "";
            row["seat_capacity"] = elem.TryGetProperty("seatCapacity", out var sc1) ? sc1.GetInt32() : 5;
            row["transmission"] = elem.TryGetProperty("transmission", out var tr1) ? tr1.GetString() : "Automatic";
            row["model_3d_url"] = elem.TryGetProperty("model3dUrl", out var m3d1) ? m3d1.GetString() :
                                  elem.TryGetProperty("model_3d_url", out var m3d2) ? m3d2.GetString() : "";
            row["latitude"] = elem.TryGetProperty("latitude", out var lat1) && lat1.TryGetDouble(out var dlat) ? dlat : 14.8169;
            row["longitude"] = elem.TryGetProperty("longitude", out var lng1) && lng1.TryGetDouble(out var dlng) ? dlng : 121.0453;
            row["current_speed"] = elem.TryGetProperty("currentSpeed", out var sp1) && sp1.TryGetDouble(out var dsp) ? dsp : 0.0;
            row["last_update"] = DateTime.Now;
            row["in_garage"] = elem.TryGetProperty("inGarage", out var ig1) && ig1.GetBoolean();
            row["is_lost"] = 0;
        }

        public static string NormalizeMediaSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return "";
            string value = source.Trim();
            return string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) ? "" : value;
        }

        public static bool IsRemoteMediaUrl(string source) =>
            Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        public static string GetMediaExtension(string source)
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

        public static VehicleMediaKind DetectMediaKind(string source)
        {
            string ext = GetMediaExtension(source);
            if (SupportedImageExtensions.Contains(ext)) return VehicleMediaKind.Image;
            if (SupportedVideoExtensions.Contains(ext)) return VehicleMediaKind.Video;
            return VehicleMediaKind.Unknown;
        }

        public static async Task<Image> LoadImageFromSourceAsync(HttpClient client, string source)
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

        public static string BuildVideoPreviewHtml(string source)
        {
            string encodedUrl = System.Net.WebUtility.HtmlEncode(source);
            return "<html><body style='margin:0;background:#030308;display:flex;align-items:center;justify-content:center;height:100vh'>" +
                   $"<video src='{encodedUrl}' controls autoplay playsinline style='width:100%;height:100%;object-fit:contain;background:#000'></video>" +
                   "</body></html>";
        }

        public static List<string> DeserializeMediaSources(string json)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(json)) return list;
            try
            {
                if (json.TrimStart().StartsWith("["))
                {
                    var parsed = JsonSerializer.Deserialize<List<string>>(json);
                    if (parsed != null)
                    {
                        foreach (var item in parsed)
                        {
                            string norm = NormalizeMediaSource(item);
                            if (!string.IsNullOrEmpty(norm)) list.Add(norm);
                        }
                    }
                }
                else
                {
                    string norm = NormalizeMediaSource(json);
                    if (!string.IsNullOrEmpty(norm)) list.Add(norm);
                }
            }
            catch
            {
                string norm = NormalizeMediaSource(json);
                if (!string.IsNullOrEmpty(norm)) list.Add(norm);
            }
            return list;
        }

        public static string SerializeMediaSources(List<string> sources)
        {
            if (sources == null || sources.Count == 0) return "";
            var normalized = sources.Select(NormalizeMediaSource).Where(s => !string.IsNullOrEmpty(s)).ToList();
            if (normalized.Count == 0) return "";
            if (normalized.Count == 1) return normalized[0];
            return JsonSerializer.Serialize(normalized);
        }

        public void ShowFleetMediaPreview(string source, string title)
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

        public static GraphicsPath RoundRect(Rectangle b, int r)
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
            if (disposing)
            {
                ThemeManager.ThemeChanged -= ThemeChanged_Handler;
                _liveTimer?.Stop();
                _liveTimer?.Dispose();
                _dbRefreshTimer?.Stop();
                _dbRefreshTimer?.Dispose();
                try { _fleetWebView?.Dispose(); } catch { }
            }
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
                            txtRateDriver, txtSeats, txtMapIcon;
            private RichTextBox txtDesc;
            private ComboBox cboType, cboStatus, cboTransmission;
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

                Text = isEdit ? "Edit Vehicle" : "Add New Vehicle";
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
                    Text = isEdit ? "Edit Vehicle Details" : "Add New Vehicle",
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
                    Text = isEdit ? "Save Changes" : "+ Add Vehicle",
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
                scrollWrapper.BringToFront();

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

                txtBrand = AddField("Brand:", lx, vx, ref y, vw);
                txtBrand.PlaceholderText = "e.g. Honda, Ford, Toyota";

                txtModel = AddField("Model:", lx, vx, ref y, vw);
                txtModel.PlaceholderText = "e.g. Civic, Ranger, Vios";

                txtPlate = AddField("Plate No.:", lx, vx, ref y, vw);
                txtPlate.PlaceholderText = "e.g. ABC-1234";

                cboType = AddCombo("Vehicle Type:", lx, vx, ref y, vw,
                    new[] { "Car", "Motorcycle", "Van", "Truck", "Bicycle" });

                txtCC = AddField("Engine CC:", lx, vx, ref y, vw);
                txtCC.PlaceholderText = "e.g. 1500";

                AddFormLabel("Rate / Day (₱):", lx, y + 7);
                txtRate = new TextBox
                {
                    Size = new Size(vw - 100, 30),
                    Location = new Point(vx, y),
                    Font = new Font("Segoe UI", 9.5F),
                    BackColor = ThemeManager.CurrentCard,
                    ForeColor = ThemeManager.CurrentText,
                    BorderStyle = BorderStyle.FixedSingle
                };
                txtRate.PlaceholderText = "e.g. 2500";
                
                var btnSuggest = new Button
                {
                    Text = "Suggest",
                    Size = new Size(90, 30),
                    Location = new Point(vx + vw - 90, y),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(230, 81, 0),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnSuggest.FlatAppearance.BorderSize = 0;
                btnSuggest.Click += async (s, e) => {
                    decimal baseRate = 2000;
                    decimal.TryParse(txtRate.Text, out baseRate);
                    int? vId = null;
                    if (_existing != null && _existing["vehicle_id"] != DBNull.Value)
                    {
                        vId = Convert.ToInt32(_existing["vehicle_id"]);
                    }
                    await ShowPriceSuggestionAsync(baseRate, vId);
                };

                scrollContent.Controls.Add(txtRate);
                scrollContent.Controls.Add(btnSuggest);
                y += 42;

                txtRateDriver = AddField("Rate + Driver (₱):", lx, vx, ref y, vw);
                txtRateDriver.PlaceholderText = "e.g. 3500";

                txtSeats = AddField("Seat Capacity:", lx, vx, ref y, vw);
                txtSeats.PlaceholderText = "e.g. 5";

                cboTransmission = AddCombo("Transmission:", lx, vx, ref y, vw,
                    new[] { "Automatic", "Manual" });

                cboStatus = AddCombo("Status:", lx, vx, ref y, vw,
                    new[] { "available", "in-use", "maintenance", "retired" });

                AddSectionDivider(lx, ref y);
                AddFormLabel("Description:", lx, y + 5);

                txtDesc = new RichTextBox
                {
                    Location = new Point(lx, y + 24),
                    Width = scrollContent.Width - lx * 2,
                    Height = 120,
                    Font = new Font("Segoe UI", 9F),
                    BackColor = card,
                    ForeColor = text,
                    BorderStyle = BorderStyle.FixedSingle,
                    ScrollBars = RichTextBoxScrollBars.Vertical,
                    WordWrap = true
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
                    Text = "+ Add Media",
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
                    PlaceholderText = "https://… (Upload or paste icon URL here)"
                };
                scrollContent.Controls.Add(txtMapIcon);

                btnBrowseMapIcon = new Button
                {
                    Text = "Browse",
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
                    txtDesc.Text = _existing["description"]?.ToString() ?? "";
                    txtMapIcon.Text = _existing["model_3d_url"]?.ToString() ?? "";

                    SelectCombo(cboType, _existing["type"]?.ToString() ?? "");
                    SelectCombo(cboTransmission, _existing["transmission"]?.ToString() ?? "Automatic");
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
                    lblUpload.Text = "Map icon uploaded";
                    lblUpload.ForeColor = Color.FromArgb(34, 197, 94);
                }
                else
                {
                    txtMapIcon.Text = ofd.FileName;
                    lblUpload.Text = "Local preview only — will retry upload on save";
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

                        lblUpload.Text = $"Uploading {Path.GetFileName(filePath)}…";
                        lblUpload.ForeColor = ThemeManager.CurrentPrimary;
                        lblUpload.Visible = true;

                        string uploadedUrl = await UploadImageToApiAsync(filePath, false);

                        string finalUrl = uploadedUrl ?? filePath;
                        _photoUrls.Add(finalUrl);
                        AddThumbnail(finalUrl);

                        if (!string.IsNullOrWhiteSpace(uploadedUrl))
                        {
                            lblUpload.Text = $"Uploaded ({_photoUrls.Count}/{FleetPanel.MaxVehicleMediaItems})";
                            lblUpload.ForeColor = Color.FromArgb(34, 197, 94);
                        }
                        else
                        {
                            lblUpload.Text = "Upload failed — queued locally, will retry on save";
                            lblUpload.ForeColor = Color.FromArgb(245, 158, 11);
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
                        Text = "MAIN",
                        Font = new Font("Segoe UI", 6.5F, FontStyle.Bold),
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
                    Text = "X",
                    Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
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
                        if (c is Label lbl && (lbl.Text == "MAIN"))
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
                                Text = "MAIN",
                                Font = new Font("Segoe UI", 6.5F, FontStyle.Bold),
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
                if (string.IsNullOrWhiteSpace(txtBrand.Text))
                {
                    MessageBox.Show("Brand is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtBrand.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtModel.Text))
                {
                    MessageBox.Show("Model is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtModel.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtPlate.Text))
                {
                    MessageBox.Show("Plate No. is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtPlate.Focus();
                    return;
                }

                if (!decimal.TryParse(txtRate.Text, out decimal rate))
                {
                    MessageBox.Show("Invalid daily rate — enter a number.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtRate.Focus();
                    return;
                }

                if (!decimal.TryParse(txtRateDriver.Text, out decimal rateDriver))
                    rateDriver = 0;

                if (!int.TryParse(txtSeats.Text, out int seats))
                    seats = 5;

                if (!int.TryParse(txtCC.Text, out int cc))
                    cc = 0;

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

                    double? lat = null;
                    double? lng = null;
                    int? curSpd = null;
                    DateTime? lastUpd = null;
                    bool inGar = true;

                    if (_existing != null)
                    {
                        if (_existing["latitude"] != DBNull.Value) lat = Convert.ToDouble(_existing["latitude"]);
                        if (_existing["longitude"] != DBNull.Value) lng = Convert.ToDouble(_existing["longitude"]);
                        if (_existing["current_speed"] != DBNull.Value) curSpd = Convert.ToInt32(_existing["current_speed"]);
                        if (_existing["last_update"] != DBNull.Value) lastUpd = Convert.ToDateTime(_existing["last_update"]);
                        if (_existing["in_garage"] != DBNull.Value) inGar = Convert.ToBoolean(_existing["in_garage"]);
                    }

                    var vehiclePayload = new
                    {
                        brand          = txtBrand.Text.Trim(),
                        model          = txtModel.Text.Trim(),
                        plateNo        = txtPlate.Text.Trim(),
                        type           = cboType.SelectedItem?.ToString() ?? "Car",
                        cc             = cc,
                        status         = cboStatus.SelectedItem?.ToString() ?? "available",
                        ratePerDay     = rate,
                        rateWithDriver = rateDriver,
                        photoUrl       = photoJson,
                        description    = txtDesc.Text.Trim(),
                        seatCapacity   = seats,
                        transmission   = cboTransmission.SelectedItem?.ToString() ?? "Automatic",
                        model3dUrl     = mapIcon,
                        latitude       = lat,
                        longitude      = lng,
                        currentSpeed   = curSpd,
                        lastUpdate     = lastUpd,
                        inGarage       = inGar
                    };

                    ApiResult res;
                    if (_existing == null)
                    {
                        res = await ApiService.PostAsync("vehicles", vehiclePayload);
                    }
                    else
                    {
                        int id = Convert.ToInt32(_existing["vehicle_id"]);
                        res = await ApiService.PutAsync($"vehicles/{id}", vehiclePayload);
                    }

                    if (res.Success)
                    {
                        DialogResult = DialogResult.OK;
                        Close();
                    }
                    else
                    {
                        MessageBox.Show(ApiService.CleanErrorMessage(res.Error ?? res.Body), "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ApiService.CleanErrorMessage(ex.Message), "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

            private async Task ShowPriceSuggestionAsync(decimal baseRate, int? vehicleId)
            {
                try
                {
                    string endpoint = $"vehicles/suggest-rate?baseRate={baseRate}";
                    if (vehicleId.HasValue) endpoint += $"&vehicleId={vehicleId.Value}";
                    
                    var res = await ApiService.GetAsync(endpoint);
                    if (!res.Success)
                    {
                        MessageBox.Show("Failed to connect to Suggestion Engine.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var root = JsonDocument.Parse(res.Body).RootElement;
                    decimal suggested = root.GetProperty("suggestedRate").GetDecimal();
                    var breakdown = root.GetProperty("breakdown");
                    decimal seasonality = breakdown.GetProperty("seasonalityMarkup").GetDecimal();
                    string seasonReason = breakdown.GetProperty("seasonalityReason").GetString();
                    decimal weekend = breakdown.GetProperty("weekendMarkup").GetDecimal();
                    string weekendReason = breakdown.GetProperty("weekendReason").GetString();
                    decimal inflation = breakdown.GetProperty("inflationMarkup").GetDecimal();
                    string inflPct = breakdown.GetProperty("inflationPercentage").GetString();
                    decimal depreciation = breakdown.GetProperty("depreciationDiscount").GetDecimal();

                    string info = $"AI DYNAMIC PRICING SUGGESTION\n\n" +
                                  $"Base Rate: ₱{baseRate:N2}\n" +
                                  $"Seasonality: +₱{seasonality:N2} ({seasonReason})\n" +
                                  $"Demand Factor: +₱{weekend:N2} ({weekendReason})\n" +
                                  $"Inflation Markup: +₱{inflation:N2} ({inflPct} Economy CPI adjusted)\n" +
                                  $"Depreciation: -₱{depreciation:N2} (Vehicle Age discount)\n\n" +
                                  $"Suggested Price: ₱{suggested:N2} per day\n\n" +
                                  $"Would you like to apply the suggested price?";

                    var decision = MessageBox.Show(info, "Dynamic Price Suggestion", 
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (decision == DialogResult.Yes)
                    {
                        txtRate.Text = suggested.ToString("0");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ApiService.CleanErrorMessage(ex.Message), "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
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
