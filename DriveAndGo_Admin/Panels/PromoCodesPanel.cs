using DriveAndGo_Admin.Helpers;
using System;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinColor = System.Drawing.Color;

namespace DriveAndGo_Admin.Panels
{
    public class PromoCodesPanel : UserControl
    {
        // ── Theme colors ──
        private WinColor ColBg => ThemeManager.CurrentBackground;
        private WinColor ColCard => ThemeManager.CurrentCard;
        private WinColor ColText => ThemeManager.CurrentText;
        private WinColor ColSub => ThemeManager.CurrentSubText;
        private WinColor ColBorder => ThemeManager.CurrentBorder;
        private WinColor ColAccent = WinColor.FromArgb(255, 90, 31);
        private WinColor ColGreen = WinColor.FromArgb(34, 197, 94);
        private WinColor ColRed = WinColor.FromArgb(239, 68, 68);

        // ── UI Controls ──
        private SplitContainer splitContainer;
        private DataGridView dgvPromos;
        private TextBox txtCode;
        private TextBox txtDiscount;
        private TextBox txtMaxAmount;
        private DateTimePicker dtExpiry;
        private Button btnSave;
        private Button btnDelete;

        private DataTable dtPromos = new DataTable();
        private int selectedPromoId = -1;

        public PromoCodesPanel()
        {
            this.Dock = DockStyle.Fill;
            this.DoubleBuffered = true;
            this.BackColor = ColBg;
            ThemeManager.ThemeChanged += (s, e) => ApplyTheme();

            BuildUI();
            this.Load += async (s, e) => await RefreshPromos();
        }

        private void BuildUI()
        {
            // Title & Header Area
            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = WinColor.Transparent };
            var lblTitle = new Label
            {
                Text = "🎫  Promo Codes & Offers",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = ColText,
                Location = new Point(16, 12),
                AutoSize = true
            };
            var lblSub = new Label
            {
                Text = "Create, validate, and manage discount offer codes for customers",
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = ColSub,
                Location = new Point(18, 48),
                AutoSize = true
            };
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSub);
            this.Controls.Add(pnlHeader);

            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 620,
                SplitterWidth = 5
            };
            this.Controls.Add(splitContainer);

            // Left Grid View
            dgvPromos = new DataGridView { Dock = DockStyle.Fill };
            StyleGrid(dgvPromos);
            dgvPromos.SelectionChanged += (s, e) =>
            {
                if (dgvPromos.SelectedRows.Count > 0)
                {
                    var row = dgvPromos.SelectedRows[0];
                    selectedPromoId = Convert.ToInt32(row.Cells["promoId"].Value);
                    txtCode.Text = row.Cells["code"].Value?.ToString();
                    txtDiscount.Text = row.Cells["discountPercentage"].Value?.ToString();
                    txtMaxAmount.Text = row.Cells["maxDiscountAmount"].Value?.ToString();
                    dtExpiry.Value = Convert.ToDateTime(row.Cells["expiryDate"].Value);
                    btnDelete.Enabled = true;
                }
                else
                {
                    ClearForm();
                }
            };
            splitContainer.Panel1.Controls.Add(dgvPromos);

            // Right Form Layout
            var pnlForm = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
            int y = 16;

            var lblFormTitle = new Label { Text = "Configure Promo Code", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = ColText, Location = new Point(16, y), AutoSize = true };
            y += 36;

            var lblC = new Label { Text = "Promo Code Name:", Location = new Point(16, y), AutoSize = true, ForeColor = ColSub };
            txtCode = new TextBox { Location = new Point(16, y + 20), Width = 260, CharacterCasing = CharacterCasing.Upper };
            y += 56;

            var lblD = new Label { Text = "Discount Percentage (e.g. 15 for 15%):", Location = new Point(16, y), AutoSize = true, ForeColor = ColSub };
            txtDiscount = new TextBox { Location = new Point(16, y + 20), Width = 260, Text = "0" };
            y += 56;

            var lblMax = new Label { Text = "Maximum Discount Amount (PHP):", Location = new Point(16, y), AutoSize = true, ForeColor = ColSub };
            txtMaxAmount = new TextBox { Location = new Point(16, y + 20), Width = 260, Text = "0.00" };
            y += 56;

            var lblEx = new Label { Text = "Expiration Date:", Location = new Point(16, y), AutoSize = true, ForeColor = ColSub };
            dtExpiry = new DateTimePicker { Location = new Point(16, y + 20), Width = 260, Format = DateTimePickerFormat.Short };
            y += 66;

            btnSave = CreateBtn("Save Promo Code", ColAccent, 16, y, 260);
            btnSave.Click += async (s, e) => await SavePromo();
            y += 46;

            btnDelete = CreateBtn("Delete Offer", ColRed, 16, y, 260);
            btnDelete.Click += async (s, e) => await DeletePromo();
            btnDelete.Enabled = false;

            pnlForm.Controls.AddRange(new Control[] {
                lblFormTitle, lblC, txtCode, lblD, txtDiscount, lblMax, txtMaxAmount, lblEx, dtExpiry, btnSave, btnDelete
            });
            splitContainer.Panel2.Controls.Add(pnlForm);
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            this.BackColor = ColBg;
            splitContainer.Panel2.BackColor = ThemeManager.IsDarkMode ? WinColor.FromArgb(20, 20, 32) : WinColor.FromArgb(245, 245, 250);
            StyleGrid(dgvPromos);
        }

        private async Task RefreshPromos()
        {
            try
            {
                var res = await ApiService.GetAsync("promos");
                if (res.Success)
                {
                    using var doc = JsonDocument.Parse(res.Body);
                    dtPromos = new DataTable();
                    dtPromos.Columns.Add("promoId", typeof(int));
                    dtPromos.Columns.Add("code", typeof(string));
                    dtPromos.Columns.Add("discountPercentage", typeof(decimal));
                    dtPromos.Columns.Add("maxDiscountAmount", typeof(decimal));
                    dtPromos.Columns.Add("isActive", typeof(bool));
                    dtPromos.Columns.Add("expiryDate", typeof(DateTime));

                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        dtPromos.Rows.Add(
                            elem.GetProperty("promoId").GetInt32(),
                            elem.GetProperty("code").GetString(),
                            elem.GetProperty("discountPercentage").GetDecimal(),
                            elem.GetProperty("maxDiscountAmount").GetDecimal(),
                            elem.GetProperty("isActive").GetBoolean(),
                            Convert.ToDateTime(elem.GetProperty("expiryDate").GetString())
                        );
                    }

                    dgvPromos.DataSource = dtPromos;
                }
            }
            catch { }
        }

        private async Task SavePromo()
        {
            if (string.IsNullOrWhiteSpace(txtCode.Text))
            {
                MessageBox.Show("Please enter a promo code name.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal.TryParse(txtDiscount.Text, out decimal discount);
            decimal.TryParse(txtMaxAmount.Text, out decimal maxAmount);

            var payload = new
            {
                code = txtCode.Text.Trim().ToUpperInvariant(),
                discountPercentage = discount,
                maxDiscountAmount = maxAmount,
                expiryDate = dtExpiry.Value.Date
            };

            var res = await ApiService.PostAsync("promos", payload);
            if (res.Success)
            {
                MessageBox.Show("Promo code saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearForm();
                await RefreshPromos();
            }
            else
            {
                MessageBox.Show("Save failed: " + res.Body, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task DeletePromo()
        {
            if (selectedPromoId <= 0) return;
            if (MessageBox.Show("Delete this promo code?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            var res = await ApiService.DeleteAsync($"promos/{selectedPromoId}");
            if (res.Success)
            {
                ClearForm();
                await RefreshPromos();
            }
        }

        private void ClearForm()
        {
            selectedPromoId = -1;
            txtCode.Clear();
            txtDiscount.Text = "0";
            txtMaxAmount.Text = "0.00";
            dtExpiry.Value = DateTime.Now;
            btnDelete.Enabled = false;
        }

        private void StyleGrid(DataGridView dgv)
        {
            bool dk = ThemeManager.IsDarkMode;
            dgv.BackgroundColor = ColBg;
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.GridColor = ColBorder;
            dgv.RowHeadersVisible = false;
            dgv.AllowUserToAddRows = false;
            dgv.ReadOnly = true;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.Font = new Font("Segoe UI", 9.5F);
            dgv.RowTemplate.Height = 40;
            dgv.EnableHeadersVisualStyles = false;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            dgv.DefaultCellStyle.BackColor = dk ? ColBg : WinColor.White;
            dgv.DefaultCellStyle.ForeColor = ColText;
            dgv.DefaultCellStyle.SelectionBackColor = dk ? WinColor.FromArgb(32, 255, 90, 31) : WinColor.FromArgb(255, 240, 230);
            dgv.DefaultCellStyle.SelectionForeColor = ColAccent;
            dgv.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);

            dgv.ColumnHeadersDefaultCellStyle.BackColor = dk ? WinColor.FromArgb(8, 8, 16) : WinColor.FromArgb(235, 236, 245);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = ColSub;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgv.ColumnHeadersHeight = 36;

            dgv.AlternatingRowsDefaultCellStyle.BackColor = dk ? WinColor.FromArgb(20, 20, 34) : WinColor.FromArgb(250, 250, 255);
        }

        private Button CreateBtn(string text, WinColor color, int x, int y, int w)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(w, 36),
                Location = new Point(x, y),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BackColor = WinColor.FromArgb(15, color),
                ForeColor = color
            };
            btn.FlatAppearance.BorderColor = color;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = WinColor.FromArgb(35, color);

            btn.HandleCreated += (s, e) =>
            {
                if (btn.IsDisposed) return;
                using var p = RoundRect(new Rectangle(0, 0, btn.Width, btn.Height), 6);
                btn.Region = new Region(p);
            };

            return btn;
        }

        private GraphicsPath RoundRect(Rectangle b, int r)
        {
            int d = r * 2;
            var arc = new Rectangle(b.Location, new Size(d, d));
            var path = new GraphicsPath();
            path.AddArc(arc, 180, 90);
            arc.X = b.Right - d;
            path.AddArc(arc, 270, 90);
            arc.Y = b.Bottom - d;
            path.AddArc(arc, 0, 90);
            arc.X = b.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
