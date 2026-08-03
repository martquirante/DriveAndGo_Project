#nullable disable
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace DriveAndGo_Admin
{
    /// <summary>
    /// Borderless dark-themed "Remove for you" confirmation dialog.
    /// Returns DialogResult.OK when the user clicks Remove.
    /// </summary>
    public class RemoveConfirmationDialog : Form
    {
        // ── Colors ────────────────────────────────────────────────────────────
        private static readonly Color BgColor     = Color.FromArgb(36, 37, 38);
        private static readonly Color BorderColor = Color.FromArgb(60, 255, 255, 255);
        private static readonly Color TextMain    = Color.FromArgb(228, 230, 235);
        private static readonly Color TextSub     = Color.FromArgb(176, 179, 184);
        private static readonly Color RemoveRed   = Color.FromArgb(224, 45, 60);
        private static readonly Color RemoveHover = Color.FromArgb(192, 35, 47);
        private static readonly Color CancelBg    = Color.FromArgb(30, 255, 255, 255);
        private static readonly Color CancelHover = Color.FromArgb(55, 255, 255, 255);

        // ── Drag support ──────────────────────────────────────────────────────
        private Point _dragStart;
        private bool  _dragging;

        // ── Buttons (kept as fields for MouseEnter/Leave wiring) ──────────────
        private Panel _btnCancel;
        private Panel _btnRemove;

        public RemoveConfirmationDialog()
        {
            // Window chrome
            FormBorderStyle = FormBorderStyle.None;
            StartPosition   = FormStartPosition.CenterParent;
            Size            = new Size(360, 240);
            BackColor       = BgColor;
            TopMost         = true;
            ShowInTaskbar   = false;

            EnableDoubleBuffer(this);

            // Rounded region
            ApplyRoundedRegion(20);
            Resize += (s, e) => ApplyRoundedRegion(20);

            // Drop shadow (DWM)
            try
            {
                var val = new int[] { 1 };
                NativeMethods.DwmSetWindowAttribute(Handle, 20, val, 4); // DWMWA_USE_IMMERSIVE_DARK_MODE
            }
            catch { }

            BuildControls();

            // Allow dragging the borderless form
            MouseDown += (s, e) => { _dragging = true; _dragStart = e.Location; };
            MouseMove += (s, e) => { if (_dragging) Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y); };
            MouseUp   += (s, e) => _dragging = false;

            Paint += OnPaint;
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background
            using var bg = new SolidBrush(BgColor);
            using var path = RoundedRect(ClientRectangle, 20);
            g.FillPath(bg, path);

            // Border
            using var borderPen = new Pen(BorderColor, 1f);
            g.DrawPath(borderPen, path);

            // Trash icon circle
            int iconR = 28;
            var iconRect = new Rectangle(ClientSize.Width / 2 - iconR, 22, iconR * 2, iconR * 2);
            using var iconBg = new SolidBrush(Color.FromArgb(40, RemoveRed));
            g.FillEllipse(iconBg, iconRect);
            using var iconFont = new Font("Segoe UI Symbol", 18F);
            using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("🗑", iconFont, new SolidBrush(RemoveRed),
                new RectangleF(iconRect.X, iconRect.Y, iconRect.Width, iconRect.Height), fmt);

            // Title
            using var titleFont = new Font("Segoe UI", 13F, FontStyle.Bold);
            using var centerFmt = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString("Remove message?", titleFont, new SolidBrush(TextMain),
                new PointF(ClientSize.Width / 2f, 82), centerFmt);

            // Body text
            using var bodyFont = new Font("Segoe UI", 9.5F);
            var bodyRect = new RectangleF(28, 108, ClientSize.Width - 56, 52);
            using var bodyCenterFmt = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString(
                "This message will be removed for you.\nOther chat members will still be able to see it.",
                bodyFont, new SolidBrush(TextSub), bodyRect, bodyCenterFmt);
        }

        private void BuildControls()
        {
            int btnY  = Height - 62;
            int btnH  = 40;
            int btnW  = (Width - 56) / 2;
            int gap   = 12;
            int leftX = 20;
            int rightX = leftX + btnW + gap;

            _btnCancel = MakeButton("Cancel", leftX, btnY, btnW, btnH, CancelBg, TextMain, false);
            _btnRemove = MakeButton("Remove", rightX, btnY, btnW, btnH, RemoveRed, Color.White, true);

            Controls.Add(_btnCancel);
            Controls.Add(_btnRemove);
        }

        private Panel MakeButton(string text, int x, int y, int w, int h,
                                  Color normalBg, Color fg, bool isRemove)
        {
            var btn = new Panel { Location = new Point(x, y), Size = new Size(w, h), Cursor = Cursors.Hand, Tag = normalBg };
            EnableDoubleBuffer(btn);

            btn.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                using var path = RoundedRect(r, 10);
                using var bgBrush = new SolidBrush((Color)btn.Tag);
                g.FillPath(bgBrush, path);
                if (!isRemove)
                {
                    using var pen = new Pen(BorderColor, 1f);
                    g.DrawPath(pen, path);
                }
                using var font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
                using var fmt  = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(text, font, new SolidBrush(fg),
                    new RectangleF(0, 0, btn.Width, btn.Height), fmt);
            };

            if (isRemove)
            {
                btn.MouseEnter += (s, e) => { btn.Tag = RemoveHover; btn.Invalidate(); };
                btn.MouseLeave += (s, e) => { btn.Tag = RemoveRed;   btn.Invalidate(); };
                btn.Click      += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            }
            else
            {
                btn.MouseEnter += (s, e) => { btn.Tag = CancelHover; btn.Invalidate(); };
                btn.MouseLeave += (s, e) => { btn.Tag = CancelBg;    btn.Invalidate(); };
                btn.Click      += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            }

            return btn;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var arc = new Rectangle(r.Location, new Size(d, d));
            var path = new GraphicsPath();
            path.AddArc(arc, 180, 90); arc.X = r.Right - d;
            path.AddArc(arc, 270, 90); arc.Y = r.Bottom - d;
            path.AddArc(arc, 0,   90); arc.X = r.Left;
            path.AddArc(arc, 90,  90); path.CloseFigure();
            return path;
        }

        private void ApplyRoundedRegion(int radius)
        {
            Region = new Region(RoundedRect(new Rectangle(0, 0, Width, Height), radius));
        }

        private static void EnableDoubleBuffer(Control c)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty |
                System.Reflection.BindingFlags.Instance    |
                System.Reflection.BindingFlags.NonPublic,
                null, c, new object[] { true });
        }
    }
}
