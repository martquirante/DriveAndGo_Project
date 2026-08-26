using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DriveAndGo_Admin.Helpers
{
    /// <summary>
    /// Sleek, Modern, Theme-Aware Modal Dialog (Confirmations, Alerts, Logout).
    /// Dynamically synchronizes with ThemeManager (Dark & Light Mode).
    /// </summary>
    public class ModernDialog : Form
    {
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        private readonly string _title;
        private readonly string _message;
        private readonly MessageBoxButtons _buttons;
        private readonly MessageBoxIcon _icon;

        private ModernDialog(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            _message = message;
            _title = string.IsNullOrWhiteSpace(title) ? "Drive&Go System" : title;
            _buttons = buttons;
            _icon = icon;

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;
            this.Size = new Size(390, 215);
            this.BackColor = ThemeManager.CurrentCard;

            BuildUI();
        }

        private void BuildUI()
        {
            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 16, 16));

            bool isDark = ThemeManager.IsDarkMode;
            Color primaryColor = ThemeManager.CurrentPrimary;
            Color textColor = ThemeManager.CurrentText;
            Color subTextColor = ThemeManager.CurrentSubText;
            Color borderColor = ThemeManager.CurrentBorder;

            // Custom Border & Header Paint
            this.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Outer border
                using var borderPen = new Pen(borderColor, 1.2f);
                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                using var path = GetRoundedPath(rect, 16);
                g.DrawPath(borderPen, path);

                // Top Accent Glow Line
                using var accentPen = new Pen(primaryColor, 3f);
                g.DrawLine(accentPen, 20, 0, Width - 20, 0);
            };

            // Modern Icon Badge Panel (painted cleanly)
            var pnlIcon = new Panel
            {
                Location = new Point(22, 22),
                Size = new Size(42, 42),
                BackColor = Color.FromArgb(isDark ? 28 : 20, primaryColor)
            };
            SetRoundRegion(pnlIcon, 12);
            pnlIcon.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using var pen = new Pen(primaryColor, 2f);
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                int cx = pnlIcon.Width / 2;
                int cy = pnlIcon.Height / 2;

                if (_title.Contains("Logout", StringComparison.OrdinalIgnoreCase))
                {
                    // Draw Door / Exit icon
                    g.DrawRectangle(pen, cx - 8, cy - 10, 16, 20);
                    g.FillEllipse(new SolidBrush(primaryColor), cx + 2, cy - 1, 3, 3);
                }
                else if (_icon == MessageBoxIcon.Question)
                {
                    // Draw Question Mark
                    using var font = new Font("Segoe UI", 16F, FontStyle.Bold);
                    using var brush = new SolidBrush(primaryColor);
                    var sz = g.MeasureString("?", font);
                    g.DrawString("?", font, brush, (pnlIcon.Width - sz.Width) / 2 + 1, (pnlIcon.Height - sz.Height) / 2);
                }
                else if (_icon == MessageBoxIcon.Warning)
                {
                    // Draw Warning Triangle
                    Point[] pts = { new Point(cx, cy - 9), new Point(cx - 10, cy + 9), new Point(cx + 10, cy + 9) };
                    g.DrawPolygon(pen, pts);
                    g.DrawLine(pen, cx, cy - 3, cx, cy + 2);
                    g.FillEllipse(new SolidBrush(primaryColor), cx - 1, cy + 5, 2, 2);
                }
                else if (_icon == MessageBoxIcon.Error)
                {
                    // Draw X
                    g.DrawLine(pen, cx - 7, cy - 7, cx + 7, cy + 7);
                    g.DrawLine(pen, cx + 7, cy - 7, cx - 7, cy + 7);
                }
                else
                {
                    // Draw Check / Info
                    g.DrawEllipse(pen, cx - 9, cy - 9, 18, 18);
                    g.DrawLine(pen, cx - 4, cy, cx - 1, cy + 4);
                    g.DrawLine(pen, cx - 1, cy + 4, cx + 5, cy - 3);
                }
            };
            this.Controls.Add(pnlIcon);

            // Title Label
            var lblTitle = new Label
            {
                Text = _title,
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = textColor,
                Location = new Point(76, 22),
                Size = new Size(285, 26),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            this.Controls.Add(lblTitle);

            // Message Body Label
            var lblMessage = new Label
            {
                Text = _message,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = subTextColor,
                Location = new Point(76, 52),
                Size = new Size(285, 80),
                TextAlign = ContentAlignment.TopLeft,
                BackColor = Color.Transparent
            };
            this.Controls.Add(lblMessage);

            // Action Buttons Panel
            int btnY = Height - 52;
            Color cancelBg = isDark ? Color.FromArgb(24, 24, 44) : Color.FromArgb(235, 238, 250);
            Color cancelHover = isDark ? Color.FromArgb(34, 34, 60) : Color.FromArgb(220, 225, 240);
            Color cancelFg = textColor;

            Color confirmBg = primaryColor;
            Color confirmHover = ThemeManager.CurrentPrimaryGlow;
            Color confirmFg = Color.White;

            if (_buttons == MessageBoxButtons.YesNo)
            {
                var btnNo = CreateButton("Cancel", cancelBg, cancelHover, cancelFg, new Point(176, btnY), new Size(92, 36));
                btnNo.Click += (s, e) => { this.DialogResult = DialogResult.No; this.Close(); };
                this.Controls.Add(btnNo);

                var btnYes = CreateButton("Yes, Confirm", confirmBg, confirmHover, confirmFg, new Point(274, btnY), new Size(100, 36));
                btnYes.Click += (s, e) => { this.DialogResult = DialogResult.Yes; this.Close(); };
                this.Controls.Add(btnYes);

                this.AcceptButton = btnYes;
                this.CancelButton = btnNo;
            }
            else if (_buttons == MessageBoxButtons.OKCancel)
            {
                var btnCancel = CreateButton("Cancel", cancelBg, cancelHover, cancelFg, new Point(176, btnY), new Size(92, 36));
                btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
                this.Controls.Add(btnCancel);

                var btnOk = CreateButton("OK", confirmBg, confirmHover, confirmFg, new Point(274, btnY), new Size(100, 36));
                btnOk.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };
                this.Controls.Add(btnOk);

                this.AcceptButton = btnOk;
                this.CancelButton = btnCancel;
            }
            else
            {
                var btnOk = CreateButton("OK", confirmBg, confirmHover, confirmFg, new Point(274, btnY), new Size(100, 36));
                btnOk.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };
                this.Controls.Add(btnOk);

                this.AcceptButton = btnOk;
                this.CancelButton = btnOk;
            }
        }

        private Button CreateButton(string text, Color bg, Color hoverBg, Color fg, Point pt, Size sz)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = pt,
                Size = sz,
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = fg,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            SetRoundRegion(btn, 8);

            btn.MouseEnter += (s, e) => { btn.BackColor = hoverBg; };
            btn.MouseLeave += (s, e) => { btn.BackColor = bg; };
            return btn;
        }

        private static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2f;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void SetRoundRegion(Control ctrl, int radius)
        {
            ctrl.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, ctrl.Width, ctrl.Height, radius, radius));
        }

        // ── Public Static Launcher Methods ──────────────────────────────────────

        public static DialogResult Show(IWin32Window owner, string message, string title = "Confirm Action", MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.Information)
        {
            using var dlg = new ModernDialog(message, title, buttons, icon);
            return owner != null ? dlg.ShowDialog(owner) : dlg.ShowDialog();
        }

        public static DialogResult Show(string message, string title = "Confirm Action", MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.Information)
        {
            return Show(Form.ActiveForm, message, title, buttons, icon);
        }
    }
}
