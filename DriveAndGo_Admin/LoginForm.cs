using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
using DriveAndGo_Admin.Helpers; // Idinagdag para makilala si SessionManager at ThemeManager

namespace DriveAndGo_Admin
{
    public class LoginForm : Form
    {
        // ── UI Elements ──
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
        private Panel cardPanel;
        private bool _passwordVisible = false;

        // 3D Drop Shadow Effect
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000;
                return cp;
            }
        }

        public LoginForm()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            // ── Form settings ──
            this.Size = new Size(440, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None; // Ginawa kong None para mas modern at lumabas ang 3D shadow
            this.MaximizeBox = false;
            this.Text = "Drive & Go — Login";
            this.BackColor = ThemeManager.CurrentBackground; // Ginamit ang ThemeManager natin
            this.Font = new Font("Segoe UI", 10F);

            // ── Background gradient via Paint ──
            this.Paint += (s, e) => {
                using var brush = new LinearGradientBrush(
                    this.ClientRectangle,
                    Color.FromArgb(15, 15, 25),
                    Color.FromArgb(25, 25, 45),
                    LinearGradientMode.Vertical);
                e.Graphics.FillRectangle(brush, this.ClientRectangle);
            };

            // ── Card Panel ──
            cardPanel = new Panel();
            cardPanel.Size = new Size(360, 460);
            cardPanel.Location = new Point(40, 80);
            cardPanel.BackColor = Color.FromArgb(30, 30, 45);
            cardPanel.Paint += (s, e) => {
                var path = GetRoundedRect(new Rectangle(0, 0, cardPanel.Width, cardPanel.Height), 16);
                cardPanel.Region = new Region(path);
                using var pen = new Pen(Color.FromArgb(60, 60, 90), 1);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(pen, path);
            };
            this.Controls.Add(cardPanel);

            // ── Exit Button (Dahil walang border ang window) ──
            Button btnExit = new Button();
            btnExit.Text = "✖";
            btnExit.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            btnExit.ForeColor = Color.Gray;
            btnExit.BackColor = Color.Transparent;
            btnExit.FlatStyle = FlatStyle.Flat;
            btnExit.FlatAppearance.BorderSize = 0;
            btnExit.Size = new Size(40, 40);
            btnExit.Location = new Point(this.Width - 40, 0);
            btnExit.Cursor = Cursors.Hand;
            btnExit.Click += (s, e) => Application.Exit();
            this.Controls.Add(btnExit);

            // ── Logo ──
            picLogo = new PictureBox();
            picLogo.Size = new Size(80, 80);
            picLogo.Location = new Point((cardPanel.Width - 80) / 2, 30);
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
            lblAppName.Text = "Drive&Go";
            lblAppName.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
            lblAppName.ForeColor = Color.FromArgb(230, 81, 0); // Orange branding
            lblAppName.AutoSize = false;
            lblAppName.Size = new Size(cardPanel.Width, 32);
            lblAppName.TextAlign = ContentAlignment.MiddleCenter;
            lblAppName.Location = new Point(0, 120);
            lblAppName.BackColor = Color.Transparent;
            cardPanel.Controls.Add(lblAppName);

            // ── Subtitle ──
            lblSubtitle = new Label();
            lblSubtitle.Text = "Admin Dashboard";
            lblSubtitle.Font = new Font("Segoe UI", 10F);
            lblSubtitle.ForeColor = Color.FromArgb(120, 120, 160);
            lblSubtitle.AutoSize = false;
            lblSubtitle.Size = new Size(cardPanel.Width, 22);
            lblSubtitle.TextAlign = ContentAlignment.MiddleCenter;
            lblSubtitle.Location = new Point(0, 154);
            lblSubtitle.BackColor = Color.Transparent;
            cardPanel.Controls.Add(lblSubtitle);

            // ── Divider ──
            var divider = new Panel();
            divider.Size = new Size(300, 1);
            divider.Location = new Point(30, 188);
            divider.BackColor = Color.FromArgb(55, 55, 80);
            cardPanel.Controls.Add(divider);

            // ── Email field ──
            lblEmailHint = new Label();
            lblEmailHint.Text = "Email Address";
            lblEmailHint.Font = new Font("Segoe UI", 9F);
            lblEmailHint.ForeColor = Color.FromArgb(120, 120, 160);
            lblEmailHint.AutoSize = true;
            lblEmailHint.Location = new Point(30, 206);
            lblEmailHint.BackColor = Color.Transparent;
            cardPanel.Controls.Add(lblEmailHint);

            txtEmail = CreateTextBox(30, 228, 300);
            txtEmail.PlaceholderText = "admin@driveandgo.com";
            cardPanel.Controls.Add(txtEmail);

            // ── Password field ──
            lblPasswordHint = new Label();
            lblPasswordHint.Text = "Password";
            lblPasswordHint.Font = new Font("Segoe UI", 9F);
            lblPasswordHint.ForeColor = Color.FromArgb(120, 120, 160);
            lblPasswordHint.AutoSize = true;
            lblPasswordHint.Location = new Point(30, 290);
            lblPasswordHint.BackColor = Color.Transparent;
            cardPanel.Controls.Add(lblPasswordHint);

            txtPassword = CreateTextBox(30, 312, 260);
            txtPassword.UseSystemPasswordChar = true;
            txtPassword.PlaceholderText = "Enter your password";
            txtPassword.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter) OnLogin(s, e);
            };
            cardPanel.Controls.Add(txtPassword);

            // ── Show/hide password toggle ──
            btnShowPassword = new Button();
            btnShowPassword.Text = "👁";
            btnShowPassword.Size = new Size(36, 36);
            btnShowPassword.Location = new Point(294, 312);
            btnShowPassword.FlatStyle = FlatStyle.Flat;
            btnShowPassword.FlatAppearance.BorderSize = 0;
            btnShowPassword.BackColor = Color.Transparent;
            btnShowPassword.ForeColor = Color.FromArgb(120, 120, 160);
            btnShowPassword.Font = new Font("Segoe UI", 12F);
            btnShowPassword.Cursor = Cursors.Hand;
            btnShowPassword.Click += (s, e) => {
                _passwordVisible = !_passwordVisible;
                txtPassword.UseSystemPasswordChar = !_passwordVisible;
                btnShowPassword.ForeColor = _passwordVisible
                    ? Color.FromArgb(230, 81, 0)
                    : Color.FromArgb(120, 120, 160);
            };
            cardPanel.Controls.Add(btnShowPassword);

            // ── Error label ──
            lblError = new Label();
            lblError.AutoSize = false;
            lblError.Size = new Size(300, 22);
            lblError.Location = new Point(30, 362);
            lblError.Font = new Font("Segoe UI", 9F);
            lblError.ForeColor = Color.FromArgb(239, 68, 68);
            lblError.TextAlign = ContentAlignment.MiddleCenter;
            lblError.BackColor = Color.Transparent;
            lblError.Visible = false;
            cardPanel.Controls.Add(lblError);

            // ── Login button ──
            btnLogin = new Button();
            btnLogin.Text = "LOG IN";
            btnLogin.Size = new Size(300, 48);
            btnLogin.Location = new Point(30, 392);
            btnLogin.FlatStyle = FlatStyle.Flat;
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.BackColor = Color.FromArgb(230, 81, 0); // Orange
            btnLogin.ForeColor = Color.White;
            btnLogin.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            btnLogin.Cursor = Cursors.Hand;
            btnLogin.Click += OnLogin;

            btnLogin.MouseEnter += (s, e) => btnLogin.BackColor = Color.FromArgb(255, 100, 20);
            btnLogin.MouseLeave += (s, e) => btnLogin.BackColor = Color.FromArgb(230, 81, 0);

            cardPanel.Controls.Add(btnLogin);
        }

        // ── Login logic ──
        private void OnLogin(object sender, EventArgs e)
        {
            string email = txtEmail.Text.Trim();
            string password = txtPassword.Text;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ShowError("Please enter both email and password.");
                return;
            }

            btnLogin.Text = "Logging in...";
            btnLogin.Enabled = false;

            try
            {
                // Dito kumokonekta diretso sa XAMPP
                using var conn = new MySqlConnection("Server=localhost;Database=vehicle_rental_db;Uid=root;Pwd=;");
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT user_id, full_name, password_hash, role
                    FROM users
                    WHERE email = @email AND role = 'admin'
                    LIMIT 1", conn);

                cmd.Parameters.AddWithValue("@email", email);

                using var reader = cmd.ExecuteReader();

                if (!reader.Read())
                {
                    ShowError("Admin account not found.");
                    return;
                }

                string storedHash = reader["password_hash"].ToString()!;
                bool isValid = false;

                if (storedHash.StartsWith("$2"))
                {
                    isValid = BCrypt.Net.BCrypt.Verify(password, storedHash);
                }
                else
                {
                    isValid = (password == storedHash);
                }

                if (!isValid)
                {
                    ShowError("Incorrect password. Please try again.");
                    return;
                }

                // ── Save session ──
                SessionManager.UserId = Convert.ToInt32(reader["user_id"]);
                SessionManager.FullName = reader["full_name"].ToString()!;
                SessionManager.Email = email;
                SessionManager.Role = reader["role"].ToString()!;

                lblError.Visible = false;

                // ── BUBUSKAN NA ANG MAIN FORM (Pinalitan ko 'yung Form1) ──
                var mainForm = new MainForm();
                mainForm.Show();
                this.Hide();
            }
            catch (Exception ex)
            {
                ShowError("Database error: Check your XAMPP!");
            }
            finally
            {
                btnLogin.Text = "LOG IN";
                btnLogin.Enabled = true;
            }
        }

        // ── Helper: show error message ──
        private void ShowError(string message)
        {
            lblError.Text = message;
            lblError.Visible = true;

            int originalX = cardPanel.Left;
            var timer = new System.Windows.Forms.Timer();
            int shakeCount = 0;
            timer.Interval = 30;
            timer.Tick += (s, e) => {
                shakeCount++;
                cardPanel.Left = shakeCount % 2 == 0 ? originalX + 6 : originalX - 6;
                if (shakeCount >= 6)
                {
                    cardPanel.Left = originalX;
                    timer.Stop();
                }
            };
            timer.Start();
        }

        // ── Helper: create styled TextBox ──
        private TextBox CreateTextBox(int x, int y, int width)
        {
            var tb = new TextBox();
            tb.Size = new Size(width, 36);
            tb.Location = new Point(x, y);
            tb.Font = new Font("Segoe UI", 11F);
            tb.BackColor = Color.FromArgb(20, 20, 35);
            tb.ForeColor = Color.FromArgb(220, 220, 245);
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.Padding = new Padding(4);

            tb.Enter += (s, e) => tb.BackColor = Color.FromArgb(28, 28, 48);
            tb.Leave += (s, e) => tb.BackColor = Color.FromArgb(20, 20, 35);

            return tb;
        }

        // ── Helper: rounded rectangle path ──
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
    }
}