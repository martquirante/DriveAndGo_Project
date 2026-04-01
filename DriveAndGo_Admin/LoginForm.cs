using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Reflection; // Idinagdag para sa Double Buffering
using MySql.Data.MySqlClient;
using DriveAndGo_Admin.Helpers;

namespace DriveAndGo_Admin
{
    public class LoginForm : Form
    {
        // ── UI Elements ──
        private Panel cardPanel;
        private Panel glowPanel;
        private PictureBox picLogo;
        private Label lblAppName;
        private Label lblSubtitle;
        private Label lblEmailHint;
        private Label lblPasswordHint;
        private Label lblError;
        private TextBox txtEmail;
        private TextBox txtPassword;
        private Button btnLogin;
        private Button btnShowPassword;
        private Button btnExit;
        private bool _passwordVisible = false;

        // FIX: Method para matago ang grid. Palitan ng 'true' kung gusto itong ipakita.
        private bool _showGrid = false;

        // ── Animation timers ──
        private System.Windows.Forms.Timer _fadeTimer;
        private System.Windows.Forms.Timer _floatTimer;
        private System.Windows.Forms.Timer _pulseTimer;
        private float _opacity = 0f;
        private int _floatOffset = 0;
        private bool _floatUp = true;
        private int _pulseAlpha = 60;
        private bool _pulseGrow = true;

        // ── Win32 drop shadow ──
        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(
            IntPtr hwnd, ref MARGINS margins);

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int Left, Right, Top, Bottom;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000;
                return cp;
            }
        }

        // ── Drag support (no title bar) ──
        private bool _dragging;
        private Point _dragStart;

        public LoginForm()
        {
            // FIX: Enable Double Buffering para smooth ang background grid at walang flicker
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer |
                          ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint, true);
            this.UpdateStyles();

            BuildUI();
            StartAnimations();
        }

        // FIX: Helper method para i-force ang Double Buffering sa standard Panels
        private void EnableDoubleBuffering(Control control)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, control, new object[] { true });
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                var m = new MARGINS { Left = 1, Right = 1, Top = 1, Bottom = 1 };
                DwmExtendFrameIntoClientArea(this.Handle, ref m);
            }
            catch { }
        }

        private void BuildUI()
        {
            // ── Form ──
            this.Size = new Size(460, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            // FIX: Use a real dark color instead of Color.Transparent for BackColor
            // Transparent BackColor + None border causes the black window issue
            this.BackColor = Color.FromArgb(10, 10, 18);
            this.Font = new Font("Segoe UI", 10F);
            // FIX: Start at Opacity 0 is fine, but ensure timer fires ASAP
            this.Opacity = 0;

            // FIX: Tawagin ang static background generator bago i-setup ang UI elements
            SetupStaticBackground();

            // Allow dragging
            this.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left)
                {
                    _dragging = true;
                    _dragStart = e.Location;
                }
            };
            this.MouseMove += (s, e) => {
                if (_dragging)
                    this.Location = new Point(
                        this.Left + e.X - _dragStart.X,
                        this.Top + e.Y - _dragStart.Y);
            };
            this.MouseUp += (s, e) => _dragging = false;

            // ── Paint background ──
            this.Paint += OnFormPaint;

            // ── Outer glow panel (3D depth illusion) ──
            glowPanel = new Panel();
            EnableDoubleBuffering(glowPanel); // In-apply ang Double Buffering
            glowPanel.Size = new Size(380, 500);
            glowPanel.Location = new Point(38, 68);
            glowPanel.BackColor = Color.FromArgb(10, 10, 18);
            glowPanel.Paint += OnGlowPaint;
            this.Controls.Add(glowPanel);

            // ── Card Panel ──
            cardPanel = new Panel();
            EnableDoubleBuffering(cardPanel); // In-apply ang Double Buffering
            cardPanel.Size = new Size(370, 490);
            cardPanel.Location = new Point(45, 75);
            cardPanel.BackColor = Color.FromArgb(22, 22, 35);
            cardPanel.Paint += OnCardPaint;

            cardPanel.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left)
                {
                    _dragging = true;
                    _dragStart = new Point(
                        e.X + cardPanel.Left,
                        e.Y + cardPanel.Top);
                }
            };
            this.Controls.Add(cardPanel);

            // ── Exit button ──
            btnExit = new Button();
            btnExit.Text = "✕";
            btnExit.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            btnExit.ForeColor = Color.FromArgb(100, 100, 130);
            btnExit.BackColor = Color.Transparent;
            btnExit.FlatStyle = FlatStyle.Flat;
            btnExit.FlatAppearance.BorderSize = 0;
            btnExit.Size = new Size(36, 36);
            btnExit.Location = new Point(this.Width - 44, 8);
            btnExit.Cursor = Cursors.Hand;
            btnExit.Click += (s, e) => FadeOut();
            btnExit.MouseEnter += (s, e) =>
                btnExit.ForeColor = Color.FromArgb(239, 68, 68);
            btnExit.MouseLeave += (s, e) =>
                btnExit.ForeColor = Color.FromArgb(100, 100, 130);
            this.Controls.Add(btnExit);

            // ── Logo ──
            picLogo = new PictureBox();
            picLogo.Size = new Size(72, 72);
            picLogo.Location = new Point(
                (cardPanel.Width - 72) / 2, 28);
            picLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picLogo.BackColor = Color.Transparent;
            try
            {
                picLogo.Image = Properties.Resources.DriveAndGo_Logo;
            }
            catch { }
            cardPanel.Controls.Add(picLogo);

            // ── App name ──
            lblAppName = new Label();
            lblAppName.Text = "Drive & Go";
            lblAppName.UseMnemonic = false;
            lblAppName.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
            lblAppName.ForeColor = Color.FromArgb(230, 81, 0);
            lblAppName.AutoSize = false;
            lblAppName.Size = new Size(cardPanel.Width, 36);
            lblAppName.TextAlign = ContentAlignment.MiddleCenter;
            lblAppName.Location = new Point(0, 108);
            lblAppName.BackColor = Color.Transparent;
            cardPanel.Controls.Add(lblAppName);

            // ── Subtitle ──
            lblSubtitle = new Label();
            lblSubtitle.Text = "Vehicle Rental — Admin Portal";
            lblSubtitle.Font = new Font("Segoe UI", 9F);
            lblSubtitle.ForeColor = Color.FromArgb(90, 90, 120);
            lblSubtitle.AutoSize = false;
            lblSubtitle.Size = new Size(cardPanel.Width, 20);
            lblSubtitle.TextAlign = ContentAlignment.MiddleCenter;
            lblSubtitle.Location = new Point(0, 146);
            lblSubtitle.BackColor = Color.Transparent;
            cardPanel.Controls.Add(lblSubtitle);

            // ── Accent line ──
            var accentLine = new Panel();
            accentLine.Size = new Size(50, 3);
            accentLine.Location = new Point(
                (cardPanel.Width - 50) / 2, 172);
            accentLine.BackColor = Color.FromArgb(230, 81, 0);
            cardPanel.Controls.Add(accentLine);

            // ── Email label ──
            lblEmailHint = new Label();
            lblEmailHint.Text = "EMAIL ADDRESS";
            lblEmailHint.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            lblEmailHint.ForeColor = Color.FromArgb(100, 100, 140);
            lblEmailHint.AutoSize = true;
            lblEmailHint.Location = new Point(35, 196);
            lblEmailHint.BackColor = Color.Transparent;
            cardPanel.Controls.Add(lblEmailHint);

            // ── Email TextBox ──
            txtEmail = CreateStyledTextBox(35, 216, 300, false);
            txtEmail.PlaceholderText = "admin@driveandgo.com";
            cardPanel.Controls.Add(txtEmail);

            // ── Password label ──
            lblPasswordHint = new Label();
            lblPasswordHint.Text = "PASSWORD";
            lblPasswordHint.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            lblPasswordHint.ForeColor = Color.FromArgb(100, 100, 140);
            lblPasswordHint.AutoSize = true;
            lblPasswordHint.Location = new Point(35, 278);
            lblPasswordHint.BackColor = Color.Transparent;
            cardPanel.Controls.Add(lblPasswordHint);

            // ── Password TextBox ──
            txtPassword = CreateStyledTextBox(35, 298, 256, true);
            txtPassword.PlaceholderText = "••••••••";
            txtPassword.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter) OnLogin(s, e);
            };
            cardPanel.Controls.Add(txtPassword);

            // ── Show/hide password ──
            btnShowPassword = new Button();
            btnShowPassword.Text = "👁";
            btnShowPassword.Size = new Size(40, 40);
            btnShowPassword.Location = new Point(295, 297);
            btnShowPassword.FlatStyle = FlatStyle.Flat;
            btnShowPassword.FlatAppearance.BorderSize = 0;
            btnShowPassword.BackColor = Color.Transparent;
            btnShowPassword.ForeColor = Color.FromArgb(80, 80, 110);
            btnShowPassword.Font = new Font("Segoe UI", 13F);
            btnShowPassword.Cursor = Cursors.Hand;
            btnShowPassword.Click += (s, e) => {
                _passwordVisible = !_passwordVisible;
                txtPassword.UseSystemPasswordChar = !_passwordVisible;
                btnShowPassword.ForeColor = _passwordVisible
                    ? Color.FromArgb(230, 81, 0)
                    : Color.FromArgb(80, 80, 110);
            };
            cardPanel.Controls.Add(btnShowPassword);

            // ── Error label ──
            lblError = new Label();
            lblError.AutoSize = false;
            lblError.Size = new Size(300, 20);
            lblError.Location = new Point(35, 355);
            lblError.Font = new Font("Segoe UI", 9F);
            lblError.ForeColor = Color.FromArgb(239, 68, 68);
            lblError.TextAlign = ContentAlignment.MiddleLeft;
            lblError.BackColor = Color.Transparent;
            lblError.Visible = false;
            cardPanel.Controls.Add(lblError);

            // ── Login button ──
            btnLogin = new Button();
            btnLogin.Text = "LOG IN";
            btnLogin.Size = new Size(300, 50);
            btnLogin.Location = new Point(35, 382);
            btnLogin.FlatStyle = FlatStyle.Flat;
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.BackColor = Color.FromArgb(230, 81, 0);
            btnLogin.ForeColor = Color.White;
            btnLogin.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            btnLogin.Cursor = Cursors.Hand;
            btnLogin.Click += OnLogin;
            btnLogin.Paint += OnLoginButtonPaint;
            btnLogin.MouseEnter += (s, e) => {
                btnLogin.BackColor = Color.FromArgb(255, 100, 20);
                btnLogin.Invalidate();
            };
            btnLogin.MouseLeave += (s, e) => {
                btnLogin.BackColor = Color.FromArgb(230, 81, 0);
                btnLogin.Invalidate();
            };
            cardPanel.Controls.Add(btnLogin);

            // ── Version ──
            var lblVersion = new Label();
            lblVersion.Text = "DriveAndGo v1.0  •  © 2026";
            lblVersion.Font = new Font("Segoe UI", 8F);
            lblVersion.ForeColor = Color.FromArgb(40, 40, 60);
            lblVersion.AutoSize = false;
            lblVersion.Size = new Size(cardPanel.Width, 22);
            lblVersion.TextAlign = ContentAlignment.MiddleCenter;
            lblVersion.Location = new Point(0, 458);
            lblVersion.BackColor = Color.Transparent;
            cardPanel.Controls.Add(lblVersion);

            // FIX: Set z-order so cardPanel is above glowPanel
            this.Controls.SetChildIndex(cardPanel, 0);
            this.Controls.SetChildIndex(btnExit, 0);
        }

        // ══ PAINT HANDLERS ══

        private void OnFormPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // FIX: Inalis na dito yung pag-draw ng background gradient at grid
            // dahil handle na yun ng SetupStaticBackground() bilang BackgroundImage.

            // Pulsing glow behind card na lang ang id-draw dito
            int cx = cardPanel.Left + cardPanel.Width / 2;
            int cy = cardPanel.Top + cardPanel.Height / 2;
            try
            {
                using var glowBrush = new PathGradientBrush(
                    new Point[] {
                        new Point(cx - 200, cy - 250),
                        new Point(cx + 200, cy - 250),
                        new Point(cx + 200, cy + 250),
                        new Point(cx - 200, cy + 250)
                    })
                {
                    CenterColor = Color.FromArgb(_pulseAlpha, 230, 81, 0),
                    SurroundColors = new[] { Color.Transparent }
                };
                g.FillEllipse(glowBrush, cx - 180, cy - 220, 360, 440);
            }
            catch { }
        }

        private void OnGlowPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            g.FillRectangle(
                new SolidBrush(Color.FromArgb(10, 10, 18)),
                glowPanel.ClientRectangle);

            // Multi-layer shadow for 3D depth
            for (int i = 8; i >= 1; i--)
            {
                int alpha = i * 6;
                float blur = i * 3;
                using var shadowBrush = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0));
                var shadowRect = new RectangleF(
                    blur, blur + _floatOffset * 0.3f,
                    glowPanel.Width - blur * 2,
                    glowPanel.Height - blur * 2);
                using var path = GetRoundedRectF(shadowRect, 18);
                g.FillPath(shadowBrush, path);
            }
        }

        private void OnCardPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0,
                cardPanel.Width - 1, cardPanel.Height - 1);
            using var path = GetRoundedRect(rect, 18);

            cardPanel.Region = new Region(path);

            // Card background
            using var cardBrush = new LinearGradientBrush(
                rect,
                Color.FromArgb(28, 28, 42),
                Color.FromArgb(18, 18, 30),
                LinearGradientMode.Vertical);
            g.FillPath(cardBrush, path);

            // Top edge highlight (3D effect)
            using var topPen = new Pen(Color.FromArgb(55, 255, 255, 255), 1f);
            g.DrawLine(topPen, 18, 1, cardPanel.Width - 18, 1);

            // Border glow
            using var borderPen = new Pen(Color.FromArgb(45, 230, 81, 0), 1.5f);
            g.DrawPath(borderPen, path);

            // Inner subtle border
            using var innerPen = new Pen(Color.FromArgb(25, 255, 255, 255), 0.5f);
            var innerRect = new Rectangle(1, 1,
                cardPanel.Width - 3, cardPanel.Height - 3);
            using var innerPath = GetRoundedRect(innerRect, 17);
            g.DrawPath(innerPen, innerPath);
        }

        private void OnLoginButtonPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var btn = (Button)sender;
            var rect = new Rectangle(0, 0,
                btn.Width - 1, btn.Height - 1);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = GetRoundedRect(rect, 10);
            btn.Region = new Region(path);

            // Gradient button
            using var btnBrush = new LinearGradientBrush(
                rect,
                Color.FromArgb(255, 120, 30),
                Color.FromArgb(200, 60, 0),
                LinearGradientMode.Vertical);
            g.FillPath(btnBrush, path);

            // Shine on top half
            var shineRect = new Rectangle(2, 2, btn.Width - 4, btn.Height / 2 - 2);
            using var shinePath = GetRoundedRect(shineRect, 8);
            using var shineBrush = new LinearGradientBrush(
                shineRect,
                Color.FromArgb(60, 255, 255, 255),
                Color.FromArgb(5, 255, 255, 255),
                LinearGradientMode.Vertical);
            g.FillPath(shineBrush, shinePath);

            // Button text
            var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            using var textBrush = new SolidBrush(Color.White);
            g.DrawString(btn.Text,
                new Font("Segoe UI", 12F, FontStyle.Bold),
                textBrush, rect, format);
        }

        // ══ ANIMATIONS ══

        private void StartAnimations()
        {
            // Fade in
            _fadeTimer = new System.Windows.Forms.Timer();
            _fadeTimer.Interval = 16;
            _fadeTimer.Tick += (s, e) => {
                _opacity += 0.04f;
                if (_opacity >= 1f)
                {
                    _opacity = 1f;
                    _fadeTimer.Stop();
                }
                this.Opacity = _opacity;
            };
            _fadeTimer.Start();

            _floatOffset = -3;
            _floatUp = false;

            _floatTimer = new System.Windows.Forms.Timer();
            _floatTimer.Interval = 40;
            _floatTimer.Tick += (s, e) => {
                if (_floatUp)
                {
                    _floatOffset--;
                    if (_floatOffset <= -6) _floatUp = false;
                }
                else
                {
                    _floatOffset++;
                    if (_floatOffset >= 0) _floatUp = true;
                }
                cardPanel.Top = 75 + _floatOffset;
                glowPanel.Top = 68 + _floatOffset;
                glowPanel.Invalidate();
            };
            _floatTimer.Start();

            // Pulse glow
            _pulseTimer = new System.Windows.Forms.Timer();
            _pulseTimer.Interval = 40;
            _pulseTimer.Tick += (s, e) => {
                if (_pulseGrow)
                {
                    _pulseAlpha += 1;
                    if (_pulseAlpha >= 80) _pulseGrow = false;
                }
                else
                {
                    _pulseAlpha -= 1;
                    if (_pulseAlpha <= 30) _pulseGrow = true;
                }
                this.Invalidate();
            };
            _pulseTimer.Start();
        }

        private void FadeOut()
        {
            var t = new System.Windows.Forms.Timer();
            t.Interval = 16;
            t.Tick += (s, e) => {
                this.Opacity -= 0.06f;
                if (this.Opacity <= 0)
                {
                    t.Stop();
                    Application.Exit();
                }
            };
            t.Start();
        }

        // ══ LOGIN LOGIC ══

        private void OnLogin(object sender, EventArgs e)
        {
            string email = txtEmail.Text.Trim();
            string password = txtPassword.Text;

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                ShowError("Please enter both email and password.");
                return;
            }

            btnLogin.Text = "Authenticating...";
            btnLogin.Enabled = false;
            lblError.Visible = false;

            try
            {
                using var conn = new MySqlConnection(
                    "Server=localhost;Database=vehicle_rental_db;" +
                    "Uid=root;Pwd=;");
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT user_id, full_name,
                           password_hash, role
                    FROM   users
                    WHERE  email = @email
                    AND    role  = 'admin'
                    LIMIT  1", conn);
                cmd.Parameters.AddWithValue("@email", email);

                using var reader = cmd.ExecuteReader();

                if (!reader.Read())
                {
                    ShowError("⚠  Admin account not found.");
                    return;
                }

                string storedHash = reader["password_hash"].ToString()!;
                bool isValid = storedHash.StartsWith("$2")
                    ? BCrypt.Net.BCrypt.Verify(password, storedHash)
                    : password == storedHash;

                if (!isValid)
                {
                    ShowError("⚠  Incorrect password. Please try again.");
                    return;
                }

                // Save session
                SessionManager.UserId = Convert.ToInt32(reader["user_id"]);
                SessionManager.FullName = reader["full_name"].ToString()!;
                SessionManager.Email = email;
                SessionManager.Role = reader["role"].ToString()!;
                reader.Close();

                // Fade out then open MainForm
                var t = new System.Windows.Forms.Timer();
                t.Interval = 16;
                t.Tick += (s2, e2) => {
                    this.Opacity -= 0.06f;
                    if (this.Opacity <= 0)
                    {
                        t.Stop();
                        var mainForm = new MainForm();
                        mainForm.Show();
                        this.Hide();
                    }
                };
                t.Start();
            }
            catch (Exception ex)
            {
                ShowError("⚠  Database error. Check XAMPP connection.");
                Console.WriteLine(ex.Message); // For debugging purposes
            }
            finally
            {
                btnLogin.Text = "LOG IN";
                btnLogin.Enabled = true;
                btnLogin.Invalidate();
            }
        }

        // ══ HELPERS ══

        private void ShowError(string message)
        {
            lblError.Text = message;
            lblError.Visible = true;

            int origX = cardPanel.Left;
            var shakeTimer = new System.Windows.Forms.Timer();
            int count = 0;
            shakeTimer.Interval = 25;
            shakeTimer.Tick += (s, e) => {
                count++;
                cardPanel.Left = count % 2 == 0
                    ? origX + 8 : origX - 8;
                if (count >= 8)
                {
                    cardPanel.Left = origX;
                    shakeTimer.Stop();
                    shakeTimer.Dispose();
                }
            };
            shakeTimer.Start();
        }

        private TextBox CreateStyledTextBox(
            int x, int y, int width, bool isPassword)
        {
            var tb = new TextBox();
            tb.Size = new Size(width, 40);
            tb.Location = new Point(x, y);
            tb.Font = new Font("Segoe UI", 11F);
            tb.BackColor = Color.FromArgb(14, 14, 24);
            tb.ForeColor = Color.FromArgb(210, 210, 240);
            tb.BorderStyle = BorderStyle.FixedSingle;
            if (isPassword)
                tb.UseSystemPasswordChar = true;

            tb.Enter += (s, e) => {
                tb.BackColor = Color.FromArgb(20, 20, 36);
                cardPanel.Invalidate();
            };
            tb.Leave += (s, e) => {
                tb.BackColor = Color.FromArgb(14, 14, 24);
                cardPanel.Invalidate();
            };
            return tb;
        }

        private GraphicsPath GetRoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, radius, radius, 180, 90);
            path.AddArc(r.Right - radius, r.Y, radius, radius, 270, 90);
            path.AddArc(r.Right - radius, r.Bottom - radius, radius, radius, 0, 90);
            path.AddArc(r.X, r.Bottom - radius, radius, radius, 90, 90);
            path.CloseFigure();
            return path;
        }

        private GraphicsPath GetRoundedRectF(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, radius, radius, 180, 90);
            path.AddArc(r.Right - radius, r.Y, radius, radius, 270, 90);
            path.AddArc(r.Right - radius, r.Bottom - radius, radius, radius, 0, 90);
            path.AddArc(r.X, r.Bottom - radius, radius, radius, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            _fadeTimer?.Dispose();
            _floatTimer?.Dispose();
            _pulseTimer?.Dispose();
            base.Dispose(disposing);
        }

        private void SetupStaticBackground()
        {
            var bmp = new Bitmap(this.Width, this.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                // 1. D-DRAWING NG GRADIENT (Kailangan naka-ON ang AntiAlias para smooth ang kulay)
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var bgBrush = new LinearGradientBrush(
                    new Rectangle(0, 0, this.Width, this.Height),
                    Color.FromArgb(8, 8, 16),
                    Color.FromArgb(18, 18, 32),
                    LinearGradientMode.ForwardDiagonal);
                g.FillRectangle(bgBrush, new Rectangle(0, 0, this.Width, this.Height));

                // FIX: Check if grid should be shown. Method para matago ang grid.
                if (_showGrid)
                {
                    // 2. D-DRAWING NG GRID (Dapat NAKA-OFF ang AntiAlias para crisp at 1px exact ang linya)
                    g.SmoothingMode = SmoothingMode.None; // <-- ITO ANG MAGPAPALINIS NG GRID

                    // Ginawa kong mas subtle (opacity 8 or 10 lang) para hindi agaw-pansin
                    using var gridPen = new Pen(Color.FromArgb(10, 255, 255, 255), 1f);

                    // Medyo niluwagan ko ang spacing (40) para mas mukhang modern at hindi crowded
                    int spacing = 40;

                    for (int x = 0; x < this.Width; x += spacing)
                        g.DrawLine(gridPen, x, 0, x, this.Height);

                    for (int y = 0; y < this.Height; y += spacing)
                        g.DrawLine(gridPen, 0, y, this.Width, y);
                }
            }

            this.BackgroundImage = bmp;
            this.BackgroundImageLayout = ImageLayout.None;
        }
    }
}