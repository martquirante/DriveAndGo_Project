using System;
using System.Drawing;
using System.Windows.Forms;
using MySql.Data.MySqlClient; // Idinagdag para maka-connect sa XAMPP

namespace DriveAndGo_Admin
{
    public partial class LoginForm : Form
    {
        // 1. I-declare ang mga UI elements
        private PictureBox picLogo;
        private Label lblTitle;
        private TextBox txtUsername;
        private TextBox txtPassword;
        private Button btnLogin;
        private Label lblError;

        // ══ CONNECTION STRING MO ══
        private string connectionString = "Server=localhost;Database=vehicle_rental_db;Uid=root;Pwd=;";

        public LoginForm()
        {
            
            SetupManualUI();
        }

        private void SetupManualUI()
        {
            // ══ FORM SETTINGS ══
            this.Size = new Size(400, 550);
            this.StartPosition = FormStartPosition.CenterScreen; // Gitna lagi
            this.FormBorderStyle = FormBorderStyle.FixedDialog; // Bawal i-resize
            this.MaximizeBox = false;
            this.Text = "Drive & Go - Login";
            this.BackColor = ThemeManager.DarkBackground;

            // ══ LOGO ══
            picLogo = new PictureBox();
            picLogo.Size = new Size(150, 150);
            picLogo.Location = new Point((this.ClientSize.Width - 150) / 2, 40);
            picLogo.SizeMode = PictureBoxSizeMode.Zoom;

            try
            {
                picLogo.Image = Properties.Resources.DriveAndGo_Logo;
            }
            catch
            {
                // Ignore kung wala pa yung image
            }
            this.Controls.Add(picLogo);

            // ══ TITLE ══
            lblTitle = new Label();
            lblTitle.Text = "ADMIN LOGIN";
            lblTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            lblTitle.ForeColor = ThemeManager.DarkText;
            lblTitle.AutoSize = false;
            lblTitle.Size = new Size(this.ClientSize.Width, 30);
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            lblTitle.Location = new Point(0, 210);
            this.Controls.Add(lblTitle);

            // ══ USERNAME (Email) ══
            Label lblUser = new Label() { Text = "Email Address", ForeColor = Color.Gray, Location = new Point(50, 270), AutoSize = true, Font = new Font("Segoe UI", 10F) };
            this.Controls.Add(lblUser);

            txtUsername = new TextBox();
            txtUsername.Size = new Size(280, 30);
            txtUsername.Location = new Point(50, 295);
            txtUsername.Font = new Font("Segoe UI", 12F);
            this.Controls.Add(txtUsername);

            // ══ PASSWORD ══
            Label lblPass = new Label() { Text = "Password", ForeColor = Color.Gray, Location = new Point(50, 340), AutoSize = true, Font = new Font("Segoe UI", 10F) };
            this.Controls.Add(lblPass);

            txtPassword = new TextBox();
            txtPassword.Size = new Size(280, 30);
            txtPassword.Location = new Point(50, 365);
            txtPassword.Font = new Font("Segoe UI", 12F);
            txtPassword.UseSystemPasswordChar = true; // Para maging tuldok-tuldok
            this.Controls.Add(txtPassword);

            // ══ ERROR MESSAGE LABEL (Nakatago muna) ══
            lblError = new Label();
            lblError.Text = "Invalid email or password!";
            lblError.ForeColor = Color.IndianRed;
            lblError.AutoSize = false;
            lblError.Size = new Size(this.ClientSize.Width, 20);
            lblError.TextAlign = ContentAlignment.MiddleCenter;
            lblError.Location = new Point(0, 410);
            lblError.Visible = false;
            this.Controls.Add(lblError);

            // ══ LOGIN BUTTON ══
            btnLogin = new Button();
            btnLogin.Text = "LOG IN";
            btnLogin.Size = new Size(280, 45);
            btnLogin.Location = new Point(50, 440);
            btnLogin.BackColor = ThemeManager.DarkPrimary;
            btnLogin.ForeColor = Color.White;
            btnLogin.FlatStyle = FlatStyle.Flat;
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            btnLogin.Cursor = Cursors.Hand;
            btnLogin.Click += BtnLogin_Click;
            this.Controls.Add(btnLogin);
        }

        // 3. Logic kapag pinindot ang Login (Naka-connect na sa DB)
        private void BtnLogin_Click(object sender, EventArgs e)
        {
            string email = txtUsername.Text.Trim();
            string password = txtPassword.Text;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                lblError.Text = "Please enter both email and password.";
                lblError.Visible = true;
                return;
            }

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    // Hinahanap natin yung email at yung role na 'admin' na ginawa ng SuperAdmin console mo
                    string query = "SELECT password_hash FROM users WHERE email = @email AND role = 'admin' LIMIT 1";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@email", email);

                    object result = cmd.ExecuteScalar();

                    if (result != null)
                    {
                        string dbPassword = result.ToString();

                        // Cini-check kung parehas yung tinype na password sa nasa database
                        if (password == dbPassword)
                        {
                            lblError.Visible = false;

                            // 1. Buksan ang Dashboard (Yung ginawa nating Form1 kanina)
                            Form1 dashboard = new Form1();
                            dashboard.Show();

                            // 2. Itago ang Login Form
                            this.Hide();
                        }
                        else
                        {
                            lblError.Text = "Invalid email or password!";
                            lblError.Visible = true;
                        }
                    }
                    else
                    {
                        lblError.Text = "Admin account not found!";
                        lblError.Visible = true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Database Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}