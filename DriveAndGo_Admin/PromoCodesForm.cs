using DriveAndGo_Admin.Helpers;
using DriveAndGo_Admin.Panels;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DriveAndGo_Admin
{
    public class PromoCodesForm : Form
    {
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        public PromoCodesForm()
        {
            this.Size = new Size(950, 600);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = ThemeManager.CurrentBackground;

            // Double Buffering
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            this.UpdateStyles();

            BuildUI();
        }

        private void BuildUI()
        {
            // Custom Title Bar
            var titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(8, 8, 16)
            };
            titleBar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawLine(new Pen(Color.FromArgb(255, 90, 31), 1), 0, titleBar.Height - 1, titleBar.Width, titleBar.Height - 1);
            };

            var lblTitle = new Label
            {
                Text = "🎫  PROMO CODES MANAGEMENT",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 14),
                BackColor = Color.Transparent
            };

            var btnClose = new Button
            {
                Text = "✕",
                Size = new Size(36, 30),
                Location = new Point(this.Width - 50, 10),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 200, 200),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 68, 68);
            btnClose.Click += (s, e) => this.Close();

            titleBar.Controls.Add(lblTitle);
            titleBar.Controls.Add(btnClose);
            this.Controls.Add(titleBar);

            // Promo Codes Panel Content
            var promoPanel = new PromoCodesPanel { Dock = DockStyle.Fill };
            this.Controls.Add(promoPanel);
            promoPanel.BringToFront();

            // Form border painting
            this.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(255, 90, 31), 2);
                e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            };
        }
    }
}
