#nullable disable
using DriveAndGo_Admin.Helpers;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DriveAndGo_Admin.Panels
{
    public class DocumentVaultPanel : UserControl
    {
        private Panel _headerPanel;
        private Label _titleLabel;
        private Label _subtitleLabel;
        private Label _pendingBadge;
        private FlowLayoutPanel _driverList;
        private Panel _detailPanel;
        private Label _detailName;
        private Label _detailStatus;
        private PictureBox _picLicense;
        private PictureBox _picSelfie;
        private PictureBox _picSecondary;
        private TextBox _reasonBox;
        private Button _btnApprove;
        private Button _btnReject;
        private int _selectedDriverId = -1;

        public DocumentVaultPanel()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.Transparent;
            BuildUI();
            _ = LoadDriversAsync();
        }

        private void BuildUI()
        {
            // ── Header ──────────────────────────────────────────────────
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.Transparent
            };

            _titleLabel = new Label
            {
                Text = "📋  Document Vault",
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentText,
                AutoSize = true,
                Location = new Point(0, 8),
                BackColor = Color.Transparent
            };

            _subtitleLabel = new Label
            {
                Text = "Review driver license, selfie, and ID documents before approval",
                Font = new Font("Segoe UI", 10F),
                ForeColor = ThemeManager.CurrentSubText,
                AutoSize = true,
                Location = new Point(2, 42),
                BackColor = Color.Transparent
            };

            _pendingBadge = new Label
            {
                Text = "0 Pending",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(245, 158, 11),
                AutoSize = true,
                Padding = new Padding(8, 4, 8, 4),
                Location = new Point(2, 60)
            };
            SetRound(_pendingBadge, 10);

            _headerPanel.Controls.Add(_titleLabel);
            _headerPanel.Controls.Add(_subtitleLabel);

            // ── Main Split ─────────────────────────────────────────────
            var splitPanel = new Panel { Dock = DockStyle.Fill };

            // Left: driver list (35% width)
            _driverList = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                Width = 300,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 0, 10, 0),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };

            // Right: detail panel
            _detailPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(10, 0, 0, 0)
            };

            BuildDetailPanel();

            splitPanel.Controls.Add(_detailPanel);
            splitPanel.Controls.Add(_driverList);

            this.Controls.Add(splitPanel);
            this.Controls.Add(_headerPanel);
        }

        private void BuildDetailPanel()
        {
            var placeholder = new Label
            {
                Text = "← Select a driver from the list to review their documents",
                Font = new Font("Segoe UI", 12F),
                ForeColor = ThemeManager.CurrentSubText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            _detailName = new Label
            {
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentText,
                AutoSize = true,
                Location = new Point(10, 10),
                BackColor = Color.Transparent
            };

            _detailStatus = new Label
            {
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(10, 40),
                BackColor = Color.Transparent
            };

            // Placeholder photo boxes
            var photoLabel = new Label
            {
                Text = "Documents",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentText,
                AutoSize = true,
                Location = new Point(10, 70),
                BackColor = Color.Transparent
            };

            _picLicense   = CreatePhotoBox(10, 95, "License Photo");
            _picSelfie    = CreatePhotoBox(200, 95, "Selfie");
            _picSecondary = CreatePhotoBox(390, 95, "Secondary ID");

            var reasonLabel = new Label
            {
                Text = "Rejection Reason (optional):",
                Font = new Font("Segoe UI", 10F),
                ForeColor = ThemeManager.CurrentText,
                AutoSize = true,
                Location = new Point(10, 280),
                BackColor = Color.Transparent
            };

            _reasonBox = new TextBox
            {
                Location = new Point(10, 300),
                Size = new Size(550, 60),
                Multiline = true,
                Font = new Font("Segoe UI", 10F),
                BackColor = ThemeManager.CurrentCard,
                ForeColor = ThemeManager.CurrentText,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Enter reason for rejection..."
            };

            _btnApprove = new Button
            {
                Text = "✅  Approve Driver",
                Location = new Point(10, 380),
                Size = new Size(180, 42),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnApprove.FlatAppearance.BorderSize = 0;
            SetRound(_btnApprove, 8);
            _btnApprove.Click += async (s, e) => await VerifyAsync(true);

            _btnReject = new Button
            {
                Text = "❌  Reject Driver",
                Location = new Point(200, 380),
                Size = new Size(160, 42),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(239, 68, 68),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnReject.FlatAppearance.BorderSize = 0;
            SetRound(_btnReject, 8);
            _btnReject.Click += async (s, e) => await VerifyAsync(false);

            _detailPanel.Controls.Add(placeholder);
            _detailPanel.Controls.Add(_detailName);
            _detailPanel.Controls.Add(_detailStatus);
            _detailPanel.Controls.Add(photoLabel);
            _detailPanel.Controls.Add(_picLicense);
            _detailPanel.Controls.Add(_picSelfie);
            _detailPanel.Controls.Add(_picSecondary);
            _detailPanel.Controls.Add(reasonLabel);
            _detailPanel.Controls.Add(_reasonBox);
            _detailPanel.Controls.Add(_btnApprove);
            _detailPanel.Controls.Add(_btnReject);

            // Initially hide details
            _detailName.Visible    = false;
            _detailStatus.Visible  = false;
            photoLabel.Visible     = false;
            _picLicense.Visible    = false;
            _picSelfie.Visible     = false;
            _picSecondary.Visible  = false;
            reasonLabel.Visible    = false;
            _reasonBox.Visible     = false;
            _btnApprove.Visible    = false;
            _btnReject.Visible     = false;
        }

        private PictureBox CreatePhotoBox(int x, int y, string caption)
        {
            var pb = new PictureBox
            {
                Location = new Point(x, y),
                Size = new Size(175, 140),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = ThemeManager.CurrentCard,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand
            };

            pb.Paint += (s, e) =>
            {
                if (pb.Image == null)
                {
                    var g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using var fnt = new Font("Segoe UI", 24F);
                    var sf = new StringFormat
                    { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("🪪", fnt, new SolidBrush(ThemeManager.CurrentSubText), pb.ClientRectangle, sf);
                    using var capFnt = new Font("Segoe UI", 9F);
                    g.DrawString(caption, capFnt, new SolidBrush(ThemeManager.CurrentSubText),
                        new RectangleF(0, pb.Height - 28, pb.Width, 24), sf);
                }
            };

            return pb;
        }

        private async Task LoadDriversAsync()
        {
            try
            {
                var result = await ApiService.GetAsync("drivers/pending");
                if (!result.Success) return;

                var drivers = JsonDocument.Parse(result.Body).RootElement;

                this.BeginInvoke(() =>
                {
                    _driverList.Controls.Clear();
                    int count = 0;

                    foreach (var d in drivers.EnumerateArray())
                    {
                        count++;
                        var card = BuildDriverCard(d);
                        _driverList.Controls.Add(card);
                    }

                    _pendingBadge.Text = $"{count} Pending";
                });
            }
            catch { }
        }

        private Panel BuildDriverCard(JsonElement d)
        {
            var driverId  = d.TryGetProperty("driverId", out var di)  ? di.GetInt32()   : 0;
            var name      = d.TryGetProperty("fullName", out var fn)   ? fn.GetString()  : "Unknown";
            var status    = d.TryGetProperty("verificationStatus", out var vs) ? vs.GetString() : "pending";
            var email     = d.TryGetProperty("email", out var em)      ? em.GetString()  : "";
            var licUrl    = d.TryGetProperty("licensePhotoUrl", out var lu)  && lu.ValueKind != JsonValueKind.Null ? lu.GetString()  : null;
            var selfieUrl = d.TryGetProperty("selfiePhotoUrl",  out var su)  && su.ValueKind != JsonValueKind.Null ? su.GetString()  : null;
            var secUrl    = d.TryGetProperty("secondaryIdUrl",  out var siu) && siu.ValueKind != JsonValueKind.Null ? siu.GetString() : null;

            var statusColor = status?.ToLower() == "rejected"
                ? Color.FromArgb(239, 68, 68)
                : Color.FromArgb(245, 158, 11);

            var card = new Panel
            {
                Width = 280,
                Height = 72,
                Margin = new Padding(0, 0, 0, 8),
                BackColor = ThemeManager.CurrentCard,
                Cursor = Cursors.Hand
            };
            SetRound(card, 10);

            var nameLabel = new Label
            {
                Text = name,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentText,
                Location = new Point(12, 10),
                AutoSize = true,
                BackColor = Color.Transparent
            };

            var emailLabel = new Label
            {
                Text = email,
                Font = new Font("Segoe UI", 9F),
                ForeColor = ThemeManager.CurrentSubText,
                Location = new Point(12, 34),
                AutoSize = true,
                BackColor = Color.Transparent
            };

            var statusDot = new Label
            {
                Text = (status ?? "pending").ToUpper(),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = statusColor,
                Location = new Point(12, 52),
                AutoSize = true,
                BackColor = Color.Transparent
            };

            card.Controls.Add(nameLabel);
            card.Controls.Add(emailLabel);
            card.Controls.Add(statusDot);

            card.Click += (s, e) => SelectDriver(driverId, name, status, licUrl, selfieUrl, secUrl);
            foreach (Control c in card.Controls)
                c.Click += (s, e) => SelectDriver(driverId, name, status, licUrl, selfieUrl, secUrl);

            return card;
        }

        private void SelectDriver(int driverId, string name, string status, string licUrl, string selfieUrl, string secUrl)
        {
            _selectedDriverId = driverId;

            _detailName.Text = name;
            _detailName.Visible = true;

            var statusColor = status?.ToLower() == "rejected"
                ? Color.FromArgb(239, 68, 68)
                : Color.FromArgb(245, 158, 11);
            _detailStatus.Text = $"Status: {(status ?? "pending").ToUpper()}";
            _detailStatus.ForeColor = statusColor;
            _detailStatus.Visible = true;

            LoadPhotoBox(_picLicense,   licUrl);
            LoadPhotoBox(_picSelfie,    selfieUrl);
            LoadPhotoBox(_picSecondary, secUrl);

            foreach (Control c in _detailPanel.Controls)
                c.Visible = true;

            _reasonBox.Clear();
        }

        private void LoadPhotoBox(PictureBox pb, string url)
        {
            pb.Image = null;
            pb.Invalidate();
            if (!string.IsNullOrWhiteSpace(url))
            {
                Task.Run(async () =>
                {
                    try
                    {
                        using var client = new System.Net.Http.HttpClient();
                        var bytes = await client.GetByteArrayAsync(url);
                        using var ms = new System.IO.MemoryStream(bytes);
                        var img = Image.FromStream(ms);
                        this.BeginInvoke(() => { pb.Image = img; });
                    }
                    catch { }
                });
            }
        }

        private async Task VerifyAsync(bool approve)
        {
            if (_selectedDriverId < 0) return;

            var reason = _reasonBox.Text.Trim();
            if (!approve && string.IsNullOrEmpty(reason))
            {
                MessageBox.Show("Please enter a rejection reason.", "Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var payload = new { approve, reason };
            var result  = await ApiService.PatchAsync($"drivers/{_selectedDriverId}/verify", payload);

            if (result.Success)
            {
                MessageBox.Show(
                    approve ? "Driver approved successfully!" : "Driver rejected.",
                    approve ? "Approved" : "Rejected",
                    MessageBoxButtons.OK,
                    approve ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

                _selectedDriverId = -1;
                await LoadDriversAsync();
            }
            else
            {
                MessageBox.Show("Error: " + result.Error, "Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void SetRound(Control ctrl, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(0, 0, d, d, 180, 90);
            path.AddArc(ctrl.Width - d, 0, d, d, 270, 90);
            path.AddArc(ctrl.Width - d, ctrl.Height - d, d, d, 0, 90);
            path.AddArc(0, ctrl.Height - d, d, d, 90, 90);
            path.CloseFigure();
            ctrl.Region = new Region(path);
        }
    }
}
