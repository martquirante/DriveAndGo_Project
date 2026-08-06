using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DriveAndGo_Admin.Helpers
{
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
            this.Size = new Size(380, 210);
            this.BackColor = Color.FromArgb(14, 15, 27);

            BuildUI();
        }

        private void BuildUI()
        {
            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 16, 16));

            // Custom Border & Header Paint
            this.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Outer border glow
                using var borderPen = new Pen(Color.FromArgb(50, 234, 88, 12), 1.5f);
                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                using var path = GetRoundedPath(rect, 16);
                g.DrawPath(borderPen, path);

                // Top Accent Line
                using var accentPen = new Pen(Color.FromArgb(234, 88, 12), 3f);
                g.DrawLine(accentPen, 20, 0, Width - 20, 0);
            };

            // Icon Badge
            string iconEmoji = _icon switch
            {
                MessageBoxIcon.Question => "❓",
                MessageBoxIcon.Warning => "⚠️",
                MessageBoxIcon.Error => "❌",
                _ => "✨"
            };

            if (_title.Contains("Logout", StringComparison.OrdinalIgnoreCase))
            {
                iconEmoji = "🚪";
            }

            var lblIcon = new Label
            {
                Text = iconEmoji,
                Font = new Font("Segoe UI Emoji", 20F, FontStyle.Regular),
                Location = new Point(24, 22),
                Size = new Size(42, 42),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(25, 234, 88, 12),
                ForeColor = Color.White
            };
            SetRoundRegion(lblIcon, 12);
            this.Controls.Add(lblIcon);

            // Title Label
            var lblTitle = new Label
            {
                Text = _title,
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(78, 22),
                Size = new Size(278, 26),
                TextAlign = ContentAlignment.MiddleLeft
            };
            this.Controls.Add(lblTitle);

            // Message Body Label
            var lblMessage = new Label
            {
                Text = _message,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(180, 190, 205),
                Location = new Point(78, 52),
                Size = new Size(278, 80),
                TextAlign = ContentAlignment.TopLeft
            };
            this.Controls.Add(lblMessage);

            // Action Buttons Panel
            int btnY = Height - 52;
            if (_buttons == MessageBoxButtons.YesNo)
            {
                var btnNo = CreateButton("Cancel", Color.FromArgb(28, 30, 48), Color.FromArgb(160, 175, 195), new Point(172, btnY), new Size(90, 36));
                btnNo.Click += (s, e) => { this.DialogResult = DialogResult.No; this.Close(); };
                this.Controls.Add(btnNo);

                var btnYes = CreateButton("Yes, Confirm", Color.FromArgb(234, 88, 12), Color.White, new Point(268, btnY), new Size(98, 36));
                btnYes.Click += (s, e) => { this.DialogResult = DialogResult.Yes; this.Close(); };
                this.Controls.Add(btnYes);

                this.AcceptButton = btnYes;
                this.CancelButton = btnNo;
            }
            else if (_buttons == MessageBoxButtons.OKCancel)
            {
                var btnCancel = CreateButton("Cancel", Color.FromArgb(28, 30, 48), Color.FromArgb(160, 175, 195), new Point(172, btnY), new Size(90, 36));
                btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
                this.Controls.Add(btnCancel);

                var btnOk = CreateButton("OK", Color.FromArgb(234, 88, 12), Color.White, new Point(268, btnY), new Size(98, 36));
                btnOk.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };
                this.Controls.Add(btnOk);

                this.AcceptButton = btnOk;
                this.CancelButton = btnCancel;
            }
            else
            {
                var btnOk = CreateButton("OK", Color.FromArgb(234, 88, 12), Color.White, new Point(268, btnY), new Size(98, 36));
                btnOk.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };
                this.Controls.Add(btnOk);

                this.AcceptButton = btnOk;
                this.CancelButton = btnOk;
            }
        }

        private Button CreateButton(string text, Color bg, Color fg, Point pt, Size sz)
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

            btn.MouseEnter += (s, e) =>
            {
                btn.BackColor = bg == Color.FromArgb(234, 88, 12)
                    ? Color.FromArgb(249, 115, 22)
                    : Color.FromArgb(42, 45, 68);
            };
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
