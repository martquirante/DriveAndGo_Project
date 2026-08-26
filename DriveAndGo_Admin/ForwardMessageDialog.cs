#nullable disable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DriveAndGo_Admin
{
    /// <summary>
    /// Borderless dark-themed "Forward message" dialog.
    /// Exposes SelectedContactId after DialogResult.OK.
    /// </summary>
    public class ForwardMessageDialog : Form
    {
        // ── Public result ─────────────────────────────────────────────────────
        public string SelectedContactId   { get; private set; }
        public string SelectedContactName { get; private set; }

        // ── Colors ────────────────────────────────────────────────────────────
        private static Color BgColor       => Helpers.ThemeManager.CurrentCard;
        private static Color CardBg        => Helpers.ThemeManager.CurrentCardHover;
        private static Color BorderColor   => Helpers.ThemeManager.CurrentBorder;
        private static Color TextMain      => Helpers.ThemeManager.CurrentText;
        private static Color TextSub       => Helpers.ThemeManager.CurrentSubText;
        private static Color OrangePrimary => Helpers.ThemeManager.CurrentPrimary;
        private static Color OrangeHover   => Helpers.ThemeManager.CurrentPrimaryGlow;
        private static Color InputBg       => Helpers.ThemeManager.CurrentInputBg;
        private static Color RowHover      => Helpers.ThemeManager.IsDarkMode ? Color.FromArgb(25, 255, 255, 255) : Color.FromArgb(20, 0, 0, 0);

        // ── Controls ──────────────────────────────────────────────────────────
        private TextBox        _txtSearch;
        private FlowLayoutPanel _contactFlow;

        // ── Contacts data (populated externally or via mock) ─────────────────
        private readonly List<ContactEntry> _contacts;
        private Point _dragStart;
        private bool  _dragging;

        public struct ContactEntry
        {
            public string Id, Name, Role;
        }

        public ForwardMessageDialog(List<ContactEntry> contacts = null)
        {
            _contacts = contacts ?? MockContacts();

            FormBorderStyle = FormBorderStyle.None;
            StartPosition   = FormStartPosition.CenterParent;
            Size            = new Size(390, 500);
            BackColor       = BgColor;
            TopMost         = true;
            ShowInTaskbar   = false;

            EnableDoubleBuffer(this);
            ApplyRoundedRegion(18);
            Resize += (s, e) => ApplyRoundedRegion(18);

            MouseDown += (s, e) => { _dragging = true; _dragStart = e.Location; };
            MouseMove += (s, e) => { if (_dragging) Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y); };
            MouseUp   += (s, e) => _dragging = false;

            Paint += OnPaint;
            BuildControls();
            PopulateContacts("");
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var path = RoundedRect(ClientRectangle, 18);
            using var bg   = new SolidBrush(BgColor);
            g.FillPath(bg, path);
            using var pen = new Pen(BorderColor, 1f);
            g.DrawPath(pen, path);

            // Header title
            using var titleFont = new Font("Segoe UI", 13F, FontStyle.Bold);
            g.DrawString("Forward Message", titleFont, new SolidBrush(TextMain), new PointF(20, 18));

            // "RECENT" label
            using var secFont = new Font("Segoe UI", 8F, FontStyle.Bold);
            g.DrawString("RECENT", secFont, new SolidBrush(TextSub), new PointF(20, 112));
        }

        private void BuildControls()
        {
            // Close (×) button
            var btnClose = new Label
            {
                Text      = "×",
                Font      = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = TextSub,
                Size      = new Size(30, 30),
                Location  = new Point(Width - 42, 12),
                Cursor    = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            btnClose.Click      += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            btnClose.MouseEnter += (s, e) => btnClose.ForeColor = TextMain;
            btnClose.MouseLeave += (s, e) => btnClose.ForeColor = TextSub;
            Controls.Add(btnClose);

            // Search box wrapper (custom-painted rounded)
            var searchWrap = new Panel
            {
                Location  = new Point(18, 46),
                Size      = new Size(Width - 36, 44),
                BackColor = Color.Transparent,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            EnableDoubleBuffer(searchWrap);
            searchWrap.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 4, searchWrap.Width - 1, 34);
                using var path = RoundedRect(r, 17);
                g.FillPath(new SolidBrush(InputBg), path);
                g.DrawPath(new Pen(BorderColor, 1f), path);
                // Search icon
                using var iconPen = new Pen(TextSub, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawEllipse(iconPen, 10, 12, 12, 12);
                g.DrawLine(iconPen, 20, 22, 25, 27);
            };
            Controls.Add(searchWrap);

            _txtSearch = new TextBox
            {
                BorderStyle     = BorderStyle.None,
                BackColor       = InputBg,
                ForeColor       = TextMain,
                Font            = new Font("Segoe UI", 10F),
                PlaceholderText = "Search people or groups...",
                Location        = new Point(34, 13),
                Size            = new Size(searchWrap.Width - 42, 22),
                Anchor          = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            _txtSearch.TextChanged += (s, e) => PopulateContacts(_txtSearch.Text);
            searchWrap.Controls.Add(_txtSearch);

            // Contacts flow
            _contactFlow = new FlowLayoutPanel
            {
                Location      = new Point(0, 130),
                Size          = new Size(Width, Height - 140),
                FlowDirection = FlowDirection.TopDown,
                WrapContents  = false,
                AutoScroll    = true,
                // ── FIX: explicit dark background — avoids white-flash on repaint ──
                BackColor     = BgColor,
                BorderStyle   = BorderStyle.None,
                Anchor        = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Padding       = new Padding(16, 4, 16, 4)
            };
            EnableDoubleBuffer(_contactFlow);

            // ── FIX: permanently kill the horizontal scrollbar that causes
            //         the white strip at the bottom of the contact list ──
            _contactFlow.HorizontalScroll.Maximum = 0;
            _contactFlow.HorizontalScroll.Visible = false;
            _contactFlow.HorizontalScroll.Enabled = false;
            _contactFlow.AutoScroll = true;

            Controls.Add(_contactFlow);
        }

        private void PopulateContacts(string filter)
        {
            _contactFlow.SuspendLayout();
            _contactFlow.Controls.Clear();

            foreach (var c in _contacts)
            {
                bool match = string.IsNullOrWhiteSpace(filter)
                    || c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || c.Role.Contains(filter, StringComparison.OrdinalIgnoreCase);
                if (!match) continue;

                _contactFlow.Controls.Add(BuildContactRow(c));
            }
            _contactFlow.ResumeLayout();
        }

        private Panel BuildContactRow(ContactEntry c)
        {
            var row = new Panel
            {
                Size      = new Size(_contactFlow.ClientSize.Width - 8, 58),
                BackColor = Color.Transparent,
                Cursor    = Cursors.Default,
                Margin    = new Padding(0, 2, 0, 2)
            };
            EnableDoubleBuffer(row);

            Color roleColor = c.Role == "Driver"   ? Color.FromArgb(59, 130, 246)
                            : c.Role == "Group"    ? Color.FromArgb(34, 197, 94)
                            : Color.FromArgb(168, 85, 247);

            bool rowHovered = false;

            row.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 2, row.Width - 1, row.Height - 4);
                using var path = RoundedRect(r, 10);
                if (rowHovered)
                {
                    g.FillPath(new SolidBrush(RowHover), path);
                }
                // Avatar circle
                var av = new Rectangle(8, 10, 36, 36);
                using var avBrush = new SolidBrush(roleColor);
                g.FillEllipse(avBrush, av);
                string init = c.Name.Length > 0 ? c.Name[0].ToString().ToUpper() : "?";
                using var initFont = new Font("Segoe UI", 13F, FontStyle.Bold);
                using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(init, initFont, Brushes.White, new RectangleF(av.X, av.Y, av.Width, av.Height), fmt);
                // Name
                using var nameFont = new Font("Segoe UI", 11F, FontStyle.Bold);
                g.DrawString(c.Name, nameFont, new SolidBrush(TextMain), new PointF(54, 9));
                // Role badge
                using var roleFont = new Font("Segoe UI", 8F);
                g.DrawString(c.Role, roleFont, new SolidBrush(roleColor), new PointF(54, 31));
            };

            row.MouseEnter += (s, e) => { rowHovered = true;  row.Invalidate(); };
            row.MouseLeave += (s, e) => { rowHovered = false; row.Invalidate(); };

            // "Send" button (right-anchored)
            var sendTag = OrangePrimary;
            var btnSend = new Panel
            {
                Size      = new Size(64, 30),
                Location  = new Point(row.Width - 72, 14),
                Cursor    = Cursors.Hand,
                Tag       = (object)sendTag,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            EnableDoubleBuffer(btnSend);

            btnSend.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, btnSend.Width - 1, btnSend.Height - 1);
                using var path = RoundedRect(r, 15);
                g.FillPath(new SolidBrush((Color)btnSend.Tag), path);
                using var font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                using var fmt  = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("Send", font, Brushes.White, new RectangleF(0, 0, btnSend.Width, btnSend.Height), fmt);
            };

            btnSend.MouseEnter += (s, e) => { btnSend.Tag = OrangeHover; btnSend.Invalidate(); };
            btnSend.MouseLeave += (s, e) => { btnSend.Tag = OrangePrimary; btnSend.Invalidate(); };
            btnSend.Click += (s, e) =>
            {
                SelectedContactId   = c.Id;
                SelectedContactName = c.Name;
                DialogResult = DialogResult.OK;
                Close();
            };

            row.Controls.Add(btnSend);
            row.Resize += (s, e) => btnSend.Location = new Point(row.Width - 72, 14);
            return row;
        }

        // ── Mock data ─────────────────────────────────────────────────────────
        private static List<ContactEntry> MockContacts() => new()
        {
            new() { Id="user_1", Name="Juan dela Cruz", Role="Driver"   },
            new() { Id="user_2", Name="Maria Santos",   Role="Customer" },
            new() { Id="user_3", Name="Carlos Reyes",   Role="Driver"   },
            new() { Id="user_4", Name="Ana Lim",        Role="Customer" },
            new() { Id="gc_1",   Name="Fleet Group",    Role="Group"    },
            new() { Id="user_5", Name="Pedro Ocampo",   Role="Driver"   },
        };

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
