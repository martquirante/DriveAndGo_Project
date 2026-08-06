#nullable disable
using DriveAndGo_Admin.Helpers;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DriveAndGo_Admin
{
    public class ResetPasswordDialog : Form
    {
        // ── Child Step Panels ──
        private Panel _pnlCard;
        private Panel _pnlStep1;
        private Panel _pnlStep2;
        private Panel _pnlStep3;
        private Panel _pnlStep4;

        // ── Step 1 Controls ──
        private TextBox _txtEmail;
        private Button  _btnSendCode;
        private Label   _lblStep1Error;

        // ── Step 2 Controls (6-Box Segmented OTP) ──
        private Panel     _pnlOtpContainer;
        private TextBox[] _otpBoxes = new TextBox[6];
        private Panel[]   _otpWraps = new Panel[6];
        private Button    _btnResendCode;
        private Button    _btnVerifyOtp;
        private Label     _lblStep2Error;
        private System.Windows.Forms.Timer _resendTimer;
        private int _secondsRemaining = 120; // 2 minutes

        // ── Step 3 Controls ──
        private TextBox _txtNewPass;
        private TextBox _txtConfPass;
        private Button  _btnShowPass;
        private Button  _btnResetPass;
        private Label   _lblStep3Error;
        private bool    _passVisible = false;

        // ── Step 4 Success Animation State ──
        private float _checkAnimProgress = 0f;
        private System.Windows.Forms.Timer _checkAnimTimer;

        // ── Shake & Transition Animation Timers ──
        private System.Windows.Forms.Timer _shakeTimer;
        private System.Windows.Forms.Timer _slideTimer;
        private int   _shakeCount = 0;
        private int   _shakeOrigX = 0;
        private Panel _activePanel = null;

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS m);
        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS { public int Left, Right, Top, Bottom; }

        public ResetPasswordDialog(string initialEmail = "")
        {
            this.DoubleBuffered = true;
            this.SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint, true);

            BuildForm();
            BuildStep1();
            BuildStep2();
            BuildStep3();
            BuildStep4();

            if (!string.IsNullOrWhiteSpace(initialEmail))
            {
                _txtEmail.Text = initialEmail;
            }

            TransitionTo(_pnlStep1, animate: false);
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

        private void BuildForm()
        {
            this.Size = new Size(440, 430);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = ThemeManager.CurrentBackground;
            this.ShowInTaskbar = false;

            _pnlCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ThemeManager.CurrentBackground
            };
            EnableDB(_pnlCard);
            _pnlCard.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, _pnlCard.Width - 1, _pnlCard.Height - 1);
                using var path = GetRoundedPath(rect, 16);
                g.DrawPath(new Pen(ThemeManager.CurrentBorder, 1.5f), path);
            };

            this.Controls.Add(_pnlCard);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  STEP 1: EMAIL REQUEST (Vertically & Horizontally Centered)
        // ════════════════════════════════════════════════════════════════════════
        private void BuildStep1()
        {
            _pnlStep1 = CreateStepPanel();

            var lblTitle = MakeHeaderLabel("🔑 Reset Account Password", 30);
            _pnlStep1.Controls.Add(lblTitle);

            var lblSub = MakeSubLabel("Enter your registered email address to receive a 6-digit security code.", 68, 388, 38);
            _pnlStep1.Controls.Add(lblSub);

            _lblStep1Error = MakeErrorLabel(112);
            _pnlStep1.Controls.Add(_lblStep1Error);

            var wrapEmail = MakeInputContainer(_pnlStep1, "ADMIN EMAIL ADDRESS", 138, out _txtEmail, false);
            _txtEmail.PlaceholderText = "admin@driveandgo.com";
            _txtEmail.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _btnSendCode.PerformClick(); }
            };

            _btnSendCode = MakePrimaryButton("SEND RESET CODE", 220, 388, 48);
            _btnSendCode.Click += async (s, e) =>
            {
                string email = _txtEmail.Text.Trim();
                if (string.IsNullOrWhiteSpace(email))
                {
                    ShowError(_lblStep1Error, "Please enter your registered email address.");
                    return;
                }

                _lblStep1Error.Visible = false;
                _btnSendCode.Text = "SENDING CODE...";
                _btnSendCode.Enabled = false;

                var (success, msg) = await ApiService.SendResetOtpAsync(email);

                _btnSendCode.Text = "SEND RESET CODE";
                _btnSendCode.Enabled = true;

                if (!success)
                {
                    ShowError(_lblStep1Error, msg);
                    return;
                }

                StartResendTimer();
                TransitionTo(_pnlStep2);
            };
            _pnlStep1.Controls.Add(_btnSendCode);

            var btnBack = MakeLinkButton("← Back to Login", 280, 388, 30);
            btnBack.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            _pnlStep1.Controls.Add(btnBack);

            _pnlCard.Controls.Add(_pnlStep1);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  STEP 2: 6-BOX SEGMENTED OTP VERIFICATION (Centered & Balanced)
        // ════════════════════════════════════════════════════════════════════════
        private void BuildStep2()
        {
            _pnlStep2 = CreateStepPanel();

            var lblTitle = MakeHeaderLabel("🔐 Security Verification", 30);
            _pnlStep2.Controls.Add(lblTitle);

            var lblSub = MakeSubLabel("Enter the 6-digit verification code sent to your email address.", 68, 388, 38);
            _pnlStep2.Controls.Add(lblSub);

            _lblStep2Error = MakeErrorLabel(112);
            _pnlStep2.Controls.Add(_lblStep2Error);

            // 6 Segmented OTP Boxes Container
            _pnlOtpContainer = new Panel
            {
                Location = new Point(26, 138),
                Size = new Size(388, 56),
                BackColor = Color.Transparent
            };
            EnableDB(_pnlOtpContainer);

            int boxWidth = 50;
            int boxHeight = 50;
            int gap = 12;
            int startX = (388 - (6 * boxWidth + 5 * gap)) / 2;

            for (int i = 0; i < 6; i++)
            {
                int index = i;
                var wrap = new Panel
                {
                    Location = new Point(startX + i * (boxWidth + gap), 3),
                    Size = new Size(boxWidth, boxHeight),
                    BackColor = ThemeManager.CurrentInputBg
                };
                EnableDB(wrap);

                wrap.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    var r = new Rectangle(0, 0, wrap.Width - 1, wrap.Height - 1);
                    using var path = GetRoundedPath(r, 10);
                    Color borderColor = (_otpBoxes[index] != null && _otpBoxes[index].Focused)
                        ? ThemeManager.CurrentPrimary
                        : ThemeManager.CurrentBorder;
                    float borderWidth = (_otpBoxes[index] != null && _otpBoxes[index].Focused) ? 2f : 1f;
                    g.DrawPath(new Pen(borderColor, borderWidth), path);
                };

                var txt = new TextBox
                {
                    Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                    ForeColor = ThemeManager.CurrentPrimary,
                    BackColor = ThemeManager.CurrentInputBg,
                    BorderStyle = BorderStyle.None,
                    Location = new Point(4, 8),
                    Size = new Size(boxWidth - 8, 34),
                    MaxLength = 6, // Allowed for paste
                    TextAlign = HorizontalAlignment.Center
                };

                txt.GotFocus += (s, e) => wrap.Invalidate();
                txt.LostFocus += (s, e) => wrap.Invalidate();

                txt.TextChanged += (s, e) =>
                {
                    string val = txt.Text.Trim();

                    // Handle multi-character Paste
                    if (val.Length > 1)
                    {
                        for (int k = 0; k < 6 && k < val.Length; k++)
                        {
                            _otpBoxes[k].Text = val[k].ToString();
                        }
                        _otpBoxes[Math.Min(5, val.Length - 1)].Focus();
                        return;
                    }

                    if (val.Length == 1 && index < 5)
                    {
                        _otpBoxes[index + 1].Focus();
                        _otpBoxes[index + 1].SelectAll();
                    }
                };

                txt.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Back && txt.Text.Length == 0 && index > 0)
                    {
                        _otpBoxes[index - 1].Focus();
                        _otpBoxes[index - 1].SelectAll();
                    }
                    else if (e.KeyCode == Keys.Enter)
                    {
                        e.SuppressKeyPress = true;
                        _btnVerifyOtp.PerformClick();
                    }
                    else if (e.KeyCode == Keys.Left && index > 0)
                    {
                        _otpBoxes[index - 1].Focus();
                    }
                    else if (e.KeyCode == Keys.Right && index < 5)
                    {
                        _otpBoxes[index + 1].Focus();
                    }
                };

                _otpBoxes[i] = txt;
                _otpWraps[i] = wrap;
                wrap.Controls.Add(txt);
                _pnlOtpContainer.Controls.Add(wrap);
            }
            _pnlStep2.Controls.Add(_pnlOtpContainer);

            // Resend Label Button (Combines timer display & resend action)
            _btnResendCode = new Button
            {
                Text = "Resend code in 02:00",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentSubText,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(26, 202),
                Size = new Size(388, 28),
                Cursor = Cursors.Default,
                Enabled = false
            };
            _btnResendCode.FlatAppearance.BorderSize = 0;
            _btnResendCode.Click += async (s, e) =>
            {
                if (!_btnResendCode.Enabled) return;

                string email = _txtEmail.Text.Trim();

                // CRITICAL: Immediately restart 120-second timer to prevent spamming
                StartResendTimer();

                var (ok, msg) = await ApiService.SendResetOtpAsync(email);
                if (ok)
                {
                    ClearOtpBoxes();
                    _otpBoxes[0].Focus();
                }
                else
                {
                    ShowError(_lblStep2Error, msg);
                }
            };
            _pnlStep2.Controls.Add(_btnResendCode);

            _btnVerifyOtp = MakePrimaryButton("VERIFY CODE", 242, 388, 48);
            _btnVerifyOtp.Click += async (s, e) =>
            {
                string email = _txtEmail.Text.Trim();
                string otp = GetEnteredOtp();
                if (otp.Length != 6)
                {
                    ShowError(_lblStep2Error, "Please enter all 6 digits of the OTP code.");
                    TriggerShakeAnimation();
                    return;
                }

                _lblStep2Error.Visible = false;
                _btnVerifyOtp.Text = "VERIFYING...";
                _btnVerifyOtp.Enabled = false;

                var (success, errorMsg) = await ApiService.VerifyResetOtpAsync(email, otp);

                _btnVerifyOtp.Text = "VERIFY CODE";
                _btnVerifyOtp.Enabled = true;

                if (!success)
                {
                    ShowError(_lblStep2Error, errorMsg ?? "Invalid or expired verification code.");
                    TriggerShakeAnimation();
                    return;
                }

                TransitionTo(_pnlStep3);
            };
            _pnlStep2.Controls.Add(_btnVerifyOtp);

            var btnCancel2 = MakeLinkButton("← Change Email", 300, 388, 30);
            btnCancel2.Click += (s, e) => TransitionTo(_pnlStep1);
            _pnlStep2.Controls.Add(btnCancel2);

            _pnlCard.Controls.Add(_pnlStep2);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  STEP 3: NEW PASSWORD INPUT (Centered)
        // ════════════════════════════════════════════════════════════════════════
        private void BuildStep3()
        {
            _pnlStep3 = CreateStepPanel();

            var lblTitle = MakeHeaderLabel("🔒 Create New Password", 24);
            _pnlStep3.Controls.Add(lblTitle);

            var lblSub = MakeSubLabel("Your identity has been verified. Enter your new password below.", 56, 388, 36);
            _pnlStep3.Controls.Add(lblSub);

            _lblStep3Error = MakeErrorLabel(96);
            _pnlStep3.Controls.Add(_lblStep3Error);

            var wrapNew = MakeInputContainer(_pnlStep3, "NEW PASSWORD", 118, out _txtNewPass, true);
            _txtNewPass.PlaceholderText = "••••••••";

            var wrapConf = MakeInputContainer(_pnlStep3, "CONFIRM NEW PASSWORD", 190, out _txtConfPass, true);
            _txtConfPass.PlaceholderText = "••••••••";

            _btnShowPass = new Button
            {
                Text = "👁",
                Font = new Font("Segoe UI Emoji", 10F),
                ForeColor = ThemeManager.CurrentSubText,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(376, 212),
                Size = new Size(32, 32),
                Cursor = Cursors.Hand
            };
            _btnShowPass.FlatAppearance.BorderSize = 0;
            _btnShowPass.Click += (s, e) =>
            {
                _passVisible = !_passVisible;
                _txtNewPass.UseSystemPasswordChar = !_passVisible;
                _txtConfPass.UseSystemPasswordChar = !_passVisible;
            };
            _pnlStep3.Controls.Add(_btnShowPass);
            _btnShowPass.BringToFront();

            _btnResetPass = MakePrimaryButton("SAVE NEW PASSWORD", 274, 388, 48);
            _btnResetPass.Click += async (s, e) =>
            {
                string email = _txtEmail.Text.Trim();
                string otp = GetEnteredOtp();
                string newP = _txtNewPass.Text.Trim();
                string confP = _txtConfPass.Text.Trim();

                if (string.IsNullOrWhiteSpace(newP) || newP.Length < 6)
                {
                    ShowError(_lblStep3Error, "New password must be at least 6 characters long.");
                    return;
                }
                if (newP != confP)
                {
                    ShowError(_lblStep3Error, "New Password and Confirm Password do not match!");
                    return;
                }

                _lblStep3Error.Visible = false;
                _btnResetPass.Text = "SAVING PASSWORD...";
                _btnResetPass.Enabled = false;

                var (success, msg) = await ApiService.ResetPasswordWithOtpAsync(email, otp, newP);

                _btnResetPass.Text = "SAVE NEW PASSWORD";
                _btnResetPass.Enabled = true;

                if (!success)
                {
                    if (msg.Contains("OTP", StringComparison.OrdinalIgnoreCase) || msg.Contains("expired", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowError(_lblStep2Error, msg);
                        TransitionTo(_pnlStep2);
                        TriggerShakeAnimation();
                    }
                    else
                    {
                        ShowError(_lblStep3Error, msg);
                    }
                    return;
                }

                TransitionToStep4Success();
            };
            _pnlStep3.Controls.Add(_btnResetPass);

            var btnCancel3 = MakeLinkButton("← Cancel", 332, 388, 30);
            btnCancel3.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            _pnlStep3.Controls.Add(btnCancel3);

            _pnlCard.Controls.Add(_pnlStep3);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  STEP 4: ANIMATED SUCCESS COMPLETION
        // ════════════════════════════════════════════════════════════════════════
        private void BuildStep4()
        {
            _pnlStep4 = CreateStepPanel();

            _pnlStep4.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int cx = _pnlStep4.Width / 2;
                int cy = 150;
                int radius = 42;

                var circleR = new Rectangle(cx - radius, cy - radius, radius * 2, radius * 2);
                using (var circlePen = new Pen(Color.FromArgb(34, 197, 94), 3.5f))
                {
                    g.DrawEllipse(circlePen, circleR);
                }
                using (var fillBrush = new SolidBrush(Color.FromArgb(25, 34, 197, 94)))
                {
                    g.FillEllipse(fillBrush, circleR);
                }

                if (_checkAnimProgress > 0f)
                {
                    using var checkPen = new Pen(Color.FromArgb(34, 197, 94), 4.5f)
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round
                    };

                    PointF p1 = new PointF(cx - 18, cy);
                    PointF p2 = new PointF(cx - 5, cy + 13);
                    PointF p3 = new PointF(cx + 18, cy - 12);

                    if (_checkAnimProgress <= 0.5f)
                    {
                        float t = _checkAnimProgress / 0.5f;
                        float curX = p1.X + (p2.X - p1.X) * t;
                        float curY = p1.Y + (p2.Y - p1.Y) * t;
                        g.DrawLine(checkPen, p1, new PointF(curX, curY));
                    }
                    else
                    {
                        g.DrawLine(checkPen, p1, p2);
                        float t = (_checkAnimProgress - 0.5f) / 0.5f;
                        float curX = p2.X + (p3.X - p2.X) * t;
                        float curY = p2.Y + (p3.Y - p2.Y) * t;
                        g.DrawLine(checkPen, p2, new PointF(curX, curY));
                    }
                }

                using var fontHead = new Font("Segoe UI", 16F, FontStyle.Bold);
                using var fontSub = new Font("Segoe UI", 10F);
                using var brushHead = new SolidBrush(ThemeManager.CurrentText);
                using var brushSub = new SolidBrush(ThemeManager.CurrentSubText);

                var sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString("Password Reset Successful!", fontHead, brushHead, new PointF(cx, cy + 62), sf);
                g.DrawString("Your account security has been updated.\nRedirecting to login portal...", fontSub, brushSub, new PointF(cx, cy + 100), sf);
            };

            _pnlCard.Controls.Add(_pnlStep4);
        }

        private void TransitionToStep4Success()
        {
            TransitionTo(_pnlStep4);

            _checkAnimProgress = 0f;
            _checkAnimTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _checkAnimTimer.Tick += (s, e) =>
            {
                _checkAnimProgress += 0.06f;
                _pnlStep4.Invalidate();

                if (_checkAnimProgress >= 1f)
                {
                    _checkAnimProgress = 1f;
                    _checkAnimTimer.Stop();

                    var closeTimer = new System.Windows.Forms.Timer { Interval = 1400 };
                    closeTimer.Tick += (s2, e2) =>
                    {
                        closeTimer.Stop();
                        closeTimer.Dispose();
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    };
                    closeTimer.Start();
                }
            };
            _checkAnimTimer.Start();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  SMOOTH SLIDE-IN PANEL TRANSITION ANIMATION
        // ════════════════════════════════════════════════════════════════════════
        private void TransitionTo(Panel targetPanel, bool animate = true)
        {
            if (_activePanel == targetPanel) return;

            _slideTimer?.Stop();
            _slideTimer?.Dispose();

            if (!animate || _activePanel == null)
            {
                targetPanel.Location = new Point(0, 0);
                targetPanel.Visible = true;
                targetPanel.BringToFront();

                if (_activePanel != null && _activePanel != targetPanel)
                {
                    _activePanel.Visible = false;
                }
                _activePanel = targetPanel;
                FocusStepTarget(targetPanel);
                return;
            }

            int startX = 30;
            targetPanel.Location = new Point(startX, 0);
            targetPanel.Visible = true;
            targetPanel.BringToFront();

            if (_activePanel != null)
            {
                _activePanel.Visible = false;
            }

            _activePanel = targetPanel;

            _slideTimer = new System.Windows.Forms.Timer { Interval = 12 };
            _slideTimer.Tick += (s, e) =>
            {
                int currentX = targetPanel.Location.X;
                int diff = 0 - currentX;
                if (Math.Abs(diff) <= 1)
                {
                    targetPanel.Location = new Point(0, 0);
                    _slideTimer.Stop();
                    FocusStepTarget(targetPanel);
                }
                else
                {
                    int step = (int)(diff * 0.35f);
                    if (step == 0) step = diff > 0 ? 1 : -1;
                    targetPanel.Location = new Point(currentX + step, 0);
                }
            };
            _slideTimer.Start();
        }

        private void FocusStepTarget(Panel panel)
        {
            if (panel == _pnlStep2)
            {
                _otpBoxes[0].Focus();
            }
            else if (panel == _pnlStep3)
            {
                _txtNewPass.Focus();
            }
        }

        private void TriggerShakeAnimation()
        {
            if (_shakeTimer != null && _shakeTimer.Enabled) return;

            _shakeOrigX = _pnlOtpContainer.Left;
            _shakeCount = 0;

            _shakeTimer = new System.Windows.Forms.Timer { Interval = 22 };
            _shakeTimer.Tick += (s, e) =>
            {
                _shakeCount++;
                int offset = (_shakeCount % 2 == 0) ? 7 : -7;
                _pnlOtpContainer.Left = _shakeOrigX + offset;

                if (_shakeCount >= 10)
                {
                    _pnlOtpContainer.Left = _shakeOrigX;
                    _shakeTimer.Stop();
                    _shakeTimer.Dispose();
                }
            };
            _shakeTimer.Start();
        }

        private void StartResendTimer()
        {
            _secondsRemaining = 120;
            _resendTimer?.Stop();
            _resendTimer?.Dispose();

            // Initial State: Unclickable, muted gray subtext color, countdown format "Resend code in 02:00"
            _btnResendCode.Enabled = false;
            _btnResendCode.Cursor = Cursors.Default;
            _btnResendCode.ForeColor = ThemeManager.CurrentSubText;
            _btnResendCode.Text = "Resend code in 02:00";

            _resendTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _resendTimer.Tick += (s, e) =>
            {
                _secondsRemaining--;
                if (_secondsRemaining <= 0)
                {
                    _resendTimer.Stop();
                    // Timer Expiration State: Clickable, Primary Orange accent, Cursor.Hand
                    _btnResendCode.Enabled = true;
                    _btnResendCode.Cursor = Cursors.Hand;
                    _btnResendCode.ForeColor = ThemeManager.CurrentPrimary;
                    _btnResendCode.Text = "Didn't receive it? Resend Code";
                }
                else
                {
                    int mins = _secondsRemaining / 60;
                    int secs = _secondsRemaining % 60;
                    _btnResendCode.Text = $"Resend code in {mins:D2}:{secs:D2}";
                }
            };
            _resendTimer.Start();
        }

        private string GetEnteredOtp()
        {
            string otp = "";
            foreach (var box in _otpBoxes)
            {
                if (box != null) otp += box.Text.Trim();
            }
            return otp;
        }

        private void ClearOtpBoxes()
        {
            foreach (var box in _otpBoxes)
            {
                if (box != null) box.Text = "";
            }
        }

        private Panel CreateStepPanel()
        {
            var pnl = new Panel
            {
                Size = new Size(440, 430),
                Location = new Point(0, 0),
                BackColor = ThemeManager.CurrentBackground,
                Visible = false
            };
            EnableDB(pnl);
            return pnl;
        }

        private Label MakeHeaderLabel(string text, int topY)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentText,
                Location = new Point(26, topY),
                Size = new Size(388, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        private Label MakeSubLabel(string text, int topY, int width, int height)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = ThemeManager.CurrentSubText,
                Location = new Point(26, topY),
                Size = new Size(width, height),
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        private Label MakeErrorLabel(int topY)
        {
            return new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(239, 68, 68),
                Location = new Point(26, topY),
                Size = new Size(388, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };
        }

        private Panel MakeInputContainer(Control parent, string labelText, int topY, out TextBox txt, bool isPassword = false)
        {
            var lbl = new Label
            {
                Text = labelText,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentSubText,
                Location = new Point(26, topY),
                AutoSize = true
            };
            parent.Controls.Add(lbl);

            txt = new TextBox
            {
                Font = new Font("Segoe UI", 10.5F),
                ForeColor = ThemeManager.CurrentText,
                BackColor = ThemeManager.CurrentInputBg,
                BorderStyle = BorderStyle.None,
                Location = new Point(12, 11),
                Size = new Size(364, 22),
                UseSystemPasswordChar = isPassword
            };

            var localTxt = txt;

            var wrap = new Panel
            {
                Location = new Point(26, topY + 18),
                Size = new Size(388, 44),
                BackColor = ThemeManager.CurrentInputBg
            };
            EnableDB(wrap);

            wrap.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, wrap.Width - 1, wrap.Height - 1);
                using var path = GetRoundedPath(r, 8);
                Color borderColor = (localTxt != null && localTxt.Focused) ? ThemeManager.CurrentPrimary : ThemeManager.CurrentBorder;
                g.DrawPath(new Pen(borderColor, 1f), path);
            };

            localTxt.GotFocus += (s, e) => wrap.Invalidate();
            localTxt.LostFocus += (s, e) => wrap.Invalidate();

            wrap.Controls.Add(localTxt);
            parent.Controls.Add(wrap);

            return wrap;
        }

        private Button MakePrimaryButton(string text, int topY, int width, int height)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ThemeManager.CurrentPrimary,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(26, topY),
                Size = new Size(width, height),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(225, 80, 10);
            btn.MouseLeave += (s, e) => btn.BackColor = ThemeManager.CurrentPrimary;
            SetRoundRegion(btn, 10);
            return btn;
        }

        private Button MakeLinkButton(string text, int topY, int width, int height)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentSubText,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(26, topY),
                Size = new Size(width, height),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => btn.ForeColor = ThemeManager.CurrentPrimary;
            btn.MouseLeave += (s, e) => btn.ForeColor = ThemeManager.CurrentSubText;
            return btn;
        }

        private void ShowError(Label errorLbl, string msg)
        {
            errorLbl.Text = "⚠ " + msg;
            errorLbl.Visible = true;
        }

        private GraphicsPath GetRoundedPath(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void SetRoundRegion(Control c, int radius)
        {
            using var path = GetRoundedPath(c.ClientRectangle, radius);
            c.Region = new Region(path);
        }

        private void EnableDB(Control c)
        {
            try
            {
                var pi = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                pi?.SetValue(c, true, null);
            }
            catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _resendTimer?.Stop();
            _resendTimer?.Dispose();
            _shakeTimer?.Stop();
            _shakeTimer?.Dispose();
            _slideTimer?.Stop();
            _slideTimer?.Dispose();
            _checkAnimTimer?.Stop();
            _checkAnimTimer?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
