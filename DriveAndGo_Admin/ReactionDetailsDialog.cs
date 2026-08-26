#nullable disable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace DriveAndGo_Admin
{
    /// <summary>
    /// Borderless dark-themed "Reaction Details" dialog.
    /// Shows tabs for "All" + per-emoji, listing userId → emoji pairs.
    /// Pass the reactions Dictionary[userId, emoji] from the bubble.
    /// currentUserId: the logged-in admin's user ID (used to detect own reaction row).
    /// After ShowDialog(), check RemoveMyReaction to see if the user wants to un-react.
    /// </summary>
    public class ReactionDetailsDialog : Form
    {
        // ── Public result ──────────────────────────────────────────────────────
        /// <summary>True if the current user clicked their own reaction row to remove it.</summary>
        public bool RemoveMyReaction { get; private set; } = false;

        // ── Colors ────────────────────────────────────────────────────────────
        private static Color BgColor        => Helpers.ThemeManager.CurrentCard;
        private static Color BorderColor    => Helpers.ThemeManager.CurrentBorder;
        private static Color TextMain       => Helpers.ThemeManager.CurrentText;
        private static Color TextSub        => Helpers.ThemeManager.CurrentSubText;
        private static Color OrangePrimary  => Helpers.ThemeManager.CurrentPrimary;
        private static Color TabBg          => Color.FromArgb(40, Helpers.ThemeManager.CurrentPrimary);
        private static Color RowHoverBg     => Helpers.ThemeManager.IsDarkMode ? Color.FromArgb(25, 255, 255, 255) : Color.FromArgb(20, 0, 0, 0);
        // Own-row highlight: warm orange tint so it feels interactable
        private static Color OwnRowHoverBg  => Color.FromArgb(35, Helpers.ThemeManager.CurrentPrimary);
        private static Color TabBorder      => Helpers.ThemeManager.CurrentPrimary;

        // ── Data ──────────────────────────────────────────────────────────────
        private readonly Dictionary<string, string> _reactions;  // userId → emoji
        private readonly string                     _currentUserId;
        private readonly List<string>               _tabs;       // "All", "👍", "❤️", …
        private string _activeTab = "All";

        // ── Controls ──────────────────────────────────────────────────────────
        private Panel           _tabBar;
        private FlowLayoutPanel _rowFlow;

        private Point _dragStart;
        private bool  _dragging;

        // ── Constructor ───────────────────────────────────────────────────────
        /// <param name="reactions">Dictionary of userId → emoji for this message.</param>
        /// <param name="currentUserId">The logged-in admin's user ID (e.g. "admin").</param>
        public ReactionDetailsDialog(Dictionary<string, string> reactions, string currentUserId = "admin")
        {
            _reactions     = reactions ?? new Dictionary<string, string>();
            _currentUserId = currentUserId ?? "admin";

            // Build tab list: "All" + distinct emojis in order of first appearance
            var emojisSeen = new List<string>();
            foreach (var v in _reactions.Values)
                if (!emojisSeen.Contains(v)) emojisSeen.Add(v);
            _tabs = new List<string> { "All" };
            _tabs.AddRange(emojisSeen);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition   = FormStartPosition.CenterParent;
            Size            = new Size(360, 440);
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
            RenderRows();
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var path = RoundedRect(ClientRectangle, 18);
            g.FillPath(new SolidBrush(BgColor), path);
            g.DrawPath(new Pen(BorderColor, 1f), path);

            using var titleFont = new Font("Segoe UI", 13F, FontStyle.Bold);
            g.DrawString("Reactions", titleFont, new SolidBrush(TextMain), new PointF(20, 16));
        }

        private void BuildControls()
        {
            // Close button
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

            // Tab bar
            _tabBar = new Panel
            {
                Location  = new Point(0, 50),
                Size      = new Size(Width, 40),
                BackColor = Color.Transparent,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            EnableDoubleBuffer(_tabBar);
            Controls.Add(_tabBar);
            BuildTabBar();

            // Separator
            var sep = new Panel
            {
                Location  = new Point(0, 90),
                Size      = new Size(Width, 1),
                BackColor = Color.FromArgb(50, 255, 255, 255),
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(sep);

            // ── Reactions list ────────────────────────────────────────────────
            // BackColor = BgColor (not Transparent) prevents the white-bar artifact.
            // HorizontalScroll is suppressed to kill the bottom white strip.
            _rowFlow = new FlowLayoutPanel
            {
                Location      = new Point(0, 92),
                Size          = new Size(Width, Height - 100),
                FlowDirection = FlowDirection.TopDown,
                WrapContents  = false,
                AutoScroll    = true,
                BackColor     = BgColor,        // ← FIX: explicit dark bg
                BorderStyle   = BorderStyle.None,
                Anchor        = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Padding       = new Padding(10, 6, 10, 6)
            };
            EnableDoubleBuffer(_rowFlow);

            // ── FIX: kill horizontal scrollbar (causes white bottom strip) ──
            _rowFlow.HorizontalScroll.Maximum = 0;
            _rowFlow.HorizontalScroll.Visible = false;
            _rowFlow.HorizontalScroll.Enabled = false;
            _rowFlow.AutoScroll = true;

            Controls.Add(_rowFlow);
        }

        private void BuildTabBar()
        {
            _tabBar.Controls.Clear();
            int x = 12;
            foreach (var tab in _tabs)
            {
                string label = tab == "All"
                    ? $"All  {_reactions.Count}"
                    : $"{tab}  {_reactions.Values.Count(v => v == tab)}";

                string capturedTab = tab;

                var btn = new Panel { Cursor = Cursors.Hand, BackColor = Color.Transparent };
                EnableDoubleBuffer(btn);

                SizeF sz;
                using (var g = CreateGraphics())
                using (var f = new Font("Segoe UI", 11F, FontStyle.Bold))
                    sz = g.MeasureString(label, f);

                btn.Size     = new Size((int)sz.Width + 20, 34);
                btn.Location = new Point(x, 3);
                x += btn.Width + 4;

                btn.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    bool active = _activeTab == capturedTab;
                    var r = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                    if (active)
                    {
                        using var path = RoundedRect(r, 14);
                        g.FillPath(new SolidBrush(TabBg), path);
                    }
                    using var font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
                    using var fmt  = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(label, font,
                        new SolidBrush(active ? OrangePrimary : TextSub),
                        new RectangleF(0, 0, btn.Width, btn.Height), fmt);
                    if (active)
                    {
                        using var pen = new Pen(TabBorder, 2f);
                        g.DrawLine(pen, 6, btn.Height - 2, btn.Width - 6, btn.Height - 2);
                    }
                };

                btn.Click += (s, e) =>
                {
                    _activeTab = capturedTab;
                    BuildTabBar();
                    RenderRows();
                };

                _tabBar.Controls.Add(btn);
            }
        }

        private void RenderRows()
        {
            _rowFlow.SuspendLayout();
            _rowFlow.Controls.Clear();

            var entries = _activeTab == "All"
                ? _reactions.ToList()
                : _reactions.Where(kvp => kvp.Value == _activeTab).ToList();

            if (entries.Count == 0)
            {
                var lbl = new Label
                {
                    Text      = "No reactions yet",
                    Font      = new Font("Segoe UI", 10F),
                    ForeColor = TextSub,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock      = DockStyle.Fill
                };
                _rowFlow.Controls.Add(lbl);
            }
            else
            {
                foreach (var (userId, emoji) in entries)
                    _rowFlow.Controls.Add(BuildReactionRow(userId, emoji));
            }

            _rowFlow.ResumeLayout();
        }

        private Panel BuildReactionRow(string userId, string emoji)
        {
            bool isOwnRow = string.Equals(userId, _currentUserId, StringComparison.OrdinalIgnoreCase);

            var row = new Panel
            {
                Size      = new Size(_rowFlow.ClientSize.Width - 8, 54),
                BackColor = Color.Transparent,
                // Own row gets a hand cursor to signal it's clickable (undo reaction)
                Cursor    = isOwnRow ? Cursors.Hand : Cursors.Default,
                Margin    = new Padding(0, 2, 0, 2)
            };
            EnableDoubleBuffer(row);

            bool hovered = false;

            row.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Hover background
                if (hovered)
                {
                    var r = new Rectangle(0, 2, row.Width - 1, row.Height - 4);
                    using var path = RoundedRect(r, 10);
                    var hoverColor = isOwnRow ? OwnRowHoverBg : RowHoverBg;
                    g.FillPath(new SolidBrush(hoverColor), path);
                }

                // Own row: subtle left-side orange accent bar
                if (isOwnRow)
                {
                    using var accentBrush = new SolidBrush(OrangePrimary);
                    g.FillRectangle(accentBrush, new Rectangle(0, 8, 3, row.Height - 16));
                }

                // Avatar gradient circle
                var av = new Rectangle(8, 9, 36, 36);
                using var avGrad = new LinearGradientBrush(av,
                    isOwnRow ? OrangePrimary : Color.FromArgb(139, 92, 246),
                    isOwnRow ? Color.FromArgb(251, 146, 60) : Color.FromArgb(234, 88, 12),
                    LinearGradientMode.ForwardDiagonal);
                g.FillEllipse(avGrad, av);
                string init = userId.Length > 0 ? userId[0].ToString().ToUpper() : "?";
                using var initFont = new Font("Segoe UI", 13F, FontStyle.Bold);
                using var fmt      = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(init, initFont, Brushes.White, new RectangleF(av.X, av.Y, av.Width, av.Height), fmt);

                // User name
                using var nameFont = new Font("Segoe UI", 10.5F, isOwnRow ? FontStyle.Bold : FontStyle.Regular);
                var nameColor = isOwnRow ? OrangePrimary : TextMain;
                g.DrawString(userId, nameFont, new SolidBrush(nameColor), new PointF(54, 11));

                // "Tap to remove" hint on own row
                if (isOwnRow)
                {
                    using var hintFont = new Font("Segoe UI", 8F);
                    g.DrawString("Tap to remove", hintFont, new SolidBrush(TextSub), new PointF(54, 30));
                }

                // Emoji (right side)
                using var emojiFont = new Font("Segoe UI Emoji", 18F);
                using var efmt      = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                g.DrawString(emoji, emojiFont, new SolidBrush(TextMain),
                    new RectangleF(0, 0, row.Width - 12, row.Height), efmt);
            };

            row.MouseEnter += (s, e) => { hovered = true;  row.Invalidate(); };
            row.MouseLeave += (s, e) => { hovered = false; row.Invalidate(); };

            // ── Own row: clicking removes the reaction ─────────────────────────
            if (isOwnRow)
            {
                EventHandler removeHandler = (s, e) =>
                {
                    RemoveMyReaction = true;
                    DialogResult     = DialogResult.OK;
                    Close();
                };
                row.Click += removeHandler;

                // Propagate click from any child controls added later
                row.ControlAdded += (s, e) =>
                {
                    if (e.Control != null)
                        e.Control.Click += removeHandler;
                };
            }

            return row;
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
