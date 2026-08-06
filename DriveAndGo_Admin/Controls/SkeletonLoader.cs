using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DriveAndGo_Admin.Helpers;

namespace DriveAndGo_Admin.Controls
{
    /// <summary>
    /// Custom, borderless WinForms Control that overlays on top of any Panel, Dashboard Card, or DataGridView
    /// featuring a smooth GDI+ Shimmer/Glare animation effect across a dark theme palette.
    /// </summary>
    public class SkeletonLoader : Control
    {
        private System.Windows.Forms.Timer _shimmerTimer;
        private float _shimmerOffset = 0f;
        private SkeletonLayoutType _layoutType = SkeletonLayoutType.Grid;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SkeletonLayoutType LayoutType
        {
            get => _layoutType;
            set
            {
                _layoutType = value;
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BaseColor { get; set; } = Color.Empty;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color HighlightColor { get; set; } = Color.Empty;

        public SkeletonLoader()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.ResizeRedraw, true);

            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;

            _shimmerTimer = new System.Windows.Forms.Timer { Interval = 25 }; // ~40 FPS smooth shimmer transition
            _shimmerTimer.Tick += (s, e) =>
            {
                _shimmerOffset += 0.035f;
                if (_shimmerOffset > 1.0f)
                {
                    _shimmerOffset = 0f;
                }
                Invalidate();
            };
        }

        public void StartAnimation()
        {
            if (_shimmerTimer != null && !_shimmerTimer.Enabled)
            {
                _shimmerTimer.Start();
            }
        }

        public void StopAnimation()
        {
            if (_shimmerTimer != null && _shimmerTimer.Enabled)
            {
                _shimmerTimer.Stop();
            }
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            if (Parent != null && !DesignMode)
            {
                StartAnimation();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int w = Width;
            int h = Height;

            if (w <= 0 || h <= 0) return;

            bool isDark = ThemeManager.IsDarkMode;
            Color baseCol = BaseColor != Color.Empty ? BaseColor : (isDark ? Color.FromArgb(22, 24, 38) : Color.FromArgb(228, 232, 245));
            Color shineCol = HighlightColor != Color.Empty ? HighlightColor : (isDark ? Color.FromArgb(42, 45, 68) : Color.FromArgb(248, 250, 255));
            Color bgCol = isDark ? Color.FromArgb(14, 15, 26) : Color.FromArgb(242, 244, 252);

            // Fill base canvas
            using (var bgBrush = new SolidBrush(bgCol))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            // Create shimmer LinearGradientBrush
            float shimmerWidth = w * 0.75f;
            float startX = -shimmerWidth + (_shimmerOffset * (w + shimmerWidth * 2));

            using var shimmerBrush = new LinearGradientBrush(
                new PointF(startX, 0),
                new PointF(startX + shimmerWidth, 0),
                baseCol,
                baseCol)
            {
                InterpolationColors = new ColorBlend
                {
                    Colors = new Color[] { baseCol, shineCol, baseCol },
                    Positions = new float[] { 0.0f, 0.5f, 1.0f }
                }
            };

            switch (_layoutType)
            {
                case SkeletonLayoutType.DashboardCard:
                    DrawDashboardCardSkeleton(g, shimmerBrush, w, h);
                    break;
                case SkeletonLayoutType.ListRow:
                    DrawListRowSkeleton(g, shimmerBrush, w, h);
                    break;
                case SkeletonLayoutType.FormFields:
                    DrawFormFieldsSkeleton(g, shimmerBrush, w, h);
                    break;
                case SkeletonLayoutType.Custom:
                    DrawCustomSkeleton(g, shimmerBrush, w, h);
                    break;
                case SkeletonLayoutType.Grid:
                default:
                    DrawGridSkeleton(g, shimmerBrush, w, h);
                    break;
            }
        }

        private void DrawGridSkeleton(Graphics g, Brush brush, int w, int h)
        {
            // Table Header Bar
            DrawRoundedRect(g, brush, 12, 12, w - 24, 36, 6);

            // Table Skeleton Rows
            int rowY = 56;
            int rowHeight = 32;
            int gap = 10;

            while (rowY + rowHeight < h - 12)
            {
                int col1W = (int)((w - 48) * 0.25f);
                int col2W = (int)((w - 48) * 0.35f);
                int col3W = (int)((w - 48) * 0.20f);
                int col4W = (int)((w - 48) * 0.20f);

                DrawRoundedRect(g, brush, 16, rowY + 6, col1W, 20, 4);
                DrawRoundedRect(g, brush, 24 + col1W, rowY + 6, col2W, 20, 4);
                DrawRoundedRect(g, brush, 32 + col1W + col2W, rowY + 6, col3W, 20, 4);
                DrawRoundedRect(g, brush, 40 + col1W + col2W + col3W, rowY + 6, col4W, 20, 4);

                rowY += rowHeight + gap;
            }
        }

        private void DrawDashboardCardSkeleton(Graphics g, Brush brush, int w, int h)
        {
            int padding = 16;

            // Metric Icon Badge (Circle)
            g.FillEllipse(brush, padding, padding, 42, 42);

            // Value Block
            DrawRoundedRect(g, brush, padding + 56, padding + 4, Math.Max(40, Math.Min(140, w - padding * 2 - 60)), 22, 6);

            // Subtitle Line
            DrawRoundedRect(g, brush, padding + 56, padding + 30, Math.Max(30, Math.Min(180, w - padding * 2 - 60)), 14, 4);

            // Bottom Progress Bar Placeholder
            if (h > 90)
            {
                DrawRoundedRect(g, brush, padding, h - 28, Math.Max(20, w - padding * 2), 12, 6);
            }
        }

        private void DrawListRowSkeleton(Graphics g, Brush brush, int w, int h)
        {
            int rowY = 12;
            int rowH = 50;

            while (rowY + rowH < h)
            {
                // Avatar Circle
                g.FillEllipse(brush, 16, rowY + 5, 40, 40);

                // Main Title Bar
                DrawRoundedRect(g, brush, 68, rowY + 10, Math.Max(40, Math.Min(220, w - 120)), 16, 4);

                // Subtitle / Meta Line
                DrawRoundedRect(g, brush, 68, rowY + 30, Math.Max(30, Math.Min(140, w - 180)), 12, 4);

                // Right Badge
                if (w > 200)
                {
                    DrawRoundedRect(g, brush, w - 70, rowY + 16, 54, 16, 8);
                }

                rowY += rowH + 12;
            }
        }

        private void DrawFormFieldsSkeleton(Graphics g, Brush brush, int w, int h)
        {
            int pad = 16;
            int curY = pad;

            // Field 1
            DrawRoundedRect(g, brush, pad, curY, 100, 14, 4);
            curY += 20;
            DrawRoundedRect(g, brush, pad, curY, Math.Max(20, w - pad * 2), 38, 6);
            curY += 50;

            // Field 2
            if (curY + 60 < h)
            {
                DrawRoundedRect(g, brush, pad, curY, 120, 14, 4);
                curY += 20;
                DrawRoundedRect(g, brush, pad, curY, Math.Max(20, w - pad * 2), 38, 6);
                curY += 50;
            }

            // Action Button
            if (curY + 40 < h)
            {
                DrawRoundedRect(g, brush, pad, curY, 130, 36, 8);
            }
        }

        private void DrawCustomSkeleton(Graphics g, Brush brush, int w, int h)
        {
            int pad = 16;
            DrawRoundedRect(g, brush, pad, pad, Math.Max(20, w - pad * 2), 28, 6);
            DrawRoundedRect(g, brush, pad, pad + 38, Math.Max(20, (int)((w - pad * 2) * 0.8f)), 18, 4);
            DrawRoundedRect(g, brush, pad, pad + 64, Math.Max(20, (int)((w - pad * 2) * 0.6f)), 18, 4);
        }

        private void DrawRoundedRect(Graphics g, Brush brush, int x, int y, int width, int height, int radius)
        {
            if (width <= 0 || height <= 0) return;
            radius = Math.Min(radius, Math.Min(width / 2, height / 2));
            if (radius <= 0)
            {
                g.FillRectangle(brush, x, y, width, height);
                return;
            }

            using var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + width - d, y, d, d, 270, 90);
            path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
            path.AddArc(x, y + height - d, d, d, 90, 90);
            path.CloseFigure();
            g.FillPath(brush, path);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopAnimation();
                _shimmerTimer?.Dispose();
                _shimmerTimer = null;
            }
            base.Dispose(disposing);
        }
    }
}
