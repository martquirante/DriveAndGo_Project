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
    /// <summary>
    /// Reusable, Theme-Aware, Borderless 6-Box Segmented OTP Verification Modal Dialog.
    /// Features:
    /// 1. 100% ThemeManager compliance (Light & Dark theme dynamic styling).
    /// 2. Segmented 6-Box OTP input with auto-tabbing, backspace navigation, and paste support.
    /// 3. 2-Minute anti-spam Resend Timer UX loop + public ResendRequested event.
    /// 4. Tactile GDI+ Error Shake Animation on invalid/incomplete input.
    /// 5. Animated GDI+ Green Checkmark Success transition.
    /// </summary>
    public class OtpVerificationDialog : Form
    {
        public event EventHandler ResendRequested;

        private readonly string _email;
        private readonly string _title;
        private readonly Func<Task<bool>> _resendCallback;
        private readonly Func<string, Task<(bool Success, string ErrorMessage)>> _verifyCallback;

        private Panel     _pnlCard;
        private Panel     _pnlMain;
        private Panel     _pnlSuccess;
        private Panel     _pnlOtpContainer;
        private TextBox[] _otpBoxes = new TextBox[6];
        private Panel[]   _otpWraps = new Panel[6];
        private Label     _lblTitle;
        private Label     _lblSub;
        private Label     _lblError;
        private Button    _btnResend;
        private Button    _btnVerify;
        private Button    _btnCancel;

        private System.Windows.Forms.Timer _countdownTimer;
        private int _secondsRemaining = 120; // 2 minutes

        // ── Animation Timers ──
        private System.Windows.Forms.Timer _shakeTimer;
        private int   _shakeCount = 0;
        private int   _shakeOrigX = 0;
        private float _checkAnimProgress = 0f;
        private System.Windows.Forms.Timer _checkAnimTimer;

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS m);
        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS { public int Left, Right, Top, Bottom; }

        public string EnteredOtp
        {
            get
            {
                string otp = "";
                foreach (var box in _otpBoxes)
                {
                    if (box != null) otp += box.Text.Trim();
                }
                return otp;
            }
        }

        public OtpVerificationDialog(
            string email = "", 
            string title = "Security Verification", 
            Func<Task<bool>> resendCallback = null,
            Func<string, Task<(bool Success, string ErrorMessage)>> verifyCallback = null)
        {
            _email = !string.IsNullOrWhiteSpace(email) ? email : "your email";
            _title = title;
            _resendCallback = resendCallback;
            _verifyCallback = verifyCallback;

            this.DoubleBuffered = true;
            this.SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint, true);

            BuildUI();
            StartTimer();
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
            this.Size = new Size(420, 380);
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

            // Main OTP Content Panel
            _pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ThemeManager.CurrentBackground,
                Padding = new Padding(24)
            };
            EnableDB(_pnlMain);

            _lblTitle = new Label
            {
                Text = "🔐 " + _title,
                Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentText,
                Location = new Point(24, 28),
                Size = new Size(372, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _pnlMain.Controls.Add(_lblTitle);

            _lblSub = new Label
            {
                Text = $"Enter the 6-digit code sent to:\n{_email}",
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = ThemeManager.CurrentSubText,
                Location = new Point(24, 62),
                Size = new Size(372, 38),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _pnlMain.Controls.Add(_lblSub);

            _lblError = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(239, 68, 68),
                Location = new Point(24, 100),
                Size = new Size(372, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };
            _pnlMain.Controls.Add(_lblError);

            // 6 Segmented OTP Boxes Container
            _pnlOtpContainer = new Panel
            {
                Location = new Point(24, 124),
                Size = new Size(372, 56),
                BackColor = Color.Transparent
            };
            EnableDB(_pnlOtpContainer);

            int boxWidth = 48;
            int boxHeight = 48;
            int gap = 12;
            int startX = (372 - (6 * boxWidth + 5 * gap)) / 2;

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
                    Location = new Point(3, 7),
                    Size = new Size(boxWidth - 6, 34),
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
                        _btnVerify.PerformClick();
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
            _pnlMain.Controls.Add(_pnlOtpContainer);

            // Resend Label Button (Combines timer display & resend action)
            _btnResend = new Button
            {
                Text = "Resend code in 02:00",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = ThemeManager.CurrentSubText,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(24, 188),
                Size = new Size(372, 28),
                Cursor = Cursors.Default,
                Enabled = false
            };
            _btnResend.FlatAppearance.BorderSize = 0;
            _btnResend.Click += async (s, e) =>
            {
                if (!_btnResend.Enabled) return;

                StartTimer();
                ClearOtpBoxes();
                _otpBoxes[0].Focus();

                ResendRequested?.Invoke(this, EventArgs.Empty);

                if (_resendCallback != null)
                {
                    await _resendCallback();
                }
            };
            _pnlMain.Controls.Add(_btnResend);

            _btnVerify = new Button
            {
                Text = "VERIFY CODE",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ThemeManager.CurrentPrimary,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(24, 230),
                Size = new Size(372, 44),
                Cursor = Cursors.Hand
            };
            _btnVerify.FlatAppearance.BorderSize = 0;
            _btnVerify.MouseEnter += (s, e) => _btnVerify.BackColor = Color.FromArgb(225, 80, 10);
            _btnVerify.MouseLeave += (s, e) => _btnVerify.BackColor = ThemeManager.CurrentPrimary;
            SetRoundRegion(_btnVerify, 10);
            _btnVerify.Click += async (s, e) =>
            {
                if (EnteredOtp.Length != 6)
                {
                    ShowError("Please enter all 6 digits of the OTP code.");
                    TriggerShakeAnimation();
                    return;
                }
                
                _lblError.Visible = false;

                if (_verifyCallback != null)
                {
                    _btnVerify.Enabled = false;
                    _btnVerify.Text = "VERIFYING...";

                    var (success, errorMsg) = await _verifyCallback(EnteredOtp);

                    _btnVerify.Text = "VERIFY CODE";
                    _btnVerify.Enabled = true;

                    if (!success)
                    {
                        ShowError(errorMsg ?? "Invalid or expired verification code.");
                        TriggerShakeAnimation();
                        return;
                    }
                }

                TransitionToSuccess();
            };
            _pnlMain.Controls.Add(_btnVerify);

            _btnCancel = new Button
            {
                Text = "Cancel",
                Font = new Font("Segoe UI", 9F),
                ForeColor = ThemeManager.CurrentSubText,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(24, 282),
                Size = new Size(372, 32),
                Cursor = Cursors.Hand
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.MouseEnter += (s, e) => _btnCancel.ForeColor = ThemeManager.CurrentPrimary;
            _btnCancel.MouseLeave += (s, e) => _btnCancel.ForeColor = ThemeManager.CurrentSubText;
            _btnCancel.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            _pnlMain.Controls.Add(_btnCancel);

            // Success Animation Panel
            _pnlSuccess = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ThemeManager.CurrentBackground,
                Visible = false
            };
            EnableDB(_pnlSuccess);

            _pnlSuccess.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int cx = _pnlSuccess.Width / 2;
                int cy = 135;
                int radius = 38;

                var circleR = new Rectangle(cx - radius, cy - radius, radius * 2, radius * 2);
                using (var circlePen = new Pen(Color.FromArgb(34, 197, 94), 3f))
                {
                    g.DrawEllipse(circlePen, circleR);
                }
                using (var fillBrush = new SolidBrush(Color.FromArgb(25, 34, 197, 94)))
                {
                    g.FillEllipse(fillBrush, circleR);
                }

                if (_checkAnimProgress > 0f)
                {
                    using var checkPen = new Pen(Color.FromArgb(34, 197, 94), 4f)
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round
                    };

                    PointF p1 = new PointF(cx - 16, cy);
                    PointF p2 = new PointF(cx - 5, cy + 12);
                    PointF p3 = new PointF(cx + 16, cy - 10);

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

                using var fontHead = new Font("Segoe UI", 15F, FontStyle.Bold);
                using var fontSub = new Font("Segoe UI", 9.5F);
                using var brushHead = new SolidBrush(ThemeManager.CurrentText);
                using var brushSub = new SolidBrush(ThemeManager.CurrentSubText);

                var sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString("Verification Successful!", fontHead, brushHead, new PointF(cx, cy + 56), sf);
                g.DrawString("Security code confirmed.\nProcessing request...", fontSub, brushSub, new PointF(cx, cy + 92), sf);
            };

            _pnlCard.Controls.Add(_pnlMain);
            _pnlCard.Controls.Add(_pnlSuccess);
            this.Controls.Add(_pnlCard);
        }

        public void TransitionToSuccess()
        {
            _pnlMain.Visible = false;
            _pnlSuccess.Visible = true;
            _pnlSuccess.BringToFront();

            _checkAnimProgress = 0f;
            _checkAnimTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _checkAnimTimer.Tick += (s, e) =>
            {
                _checkAnimProgress += 0.06f;
                _pnlSuccess.Invalidate();

                if (_checkAnimProgress >= 1f)
                {
                    _checkAnimProgress = 1f;
                    _checkAnimTimer.Stop();

                    var closeTimer = new System.Windows.Forms.Timer { Interval = 1100 };
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

        public void TriggerShakeAnimation()
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

        private void StartTimer()
        {
            _secondsRemaining = 120;
            _countdownTimer?.Stop();
            _countdownTimer?.Dispose();

            _btnResend.Enabled = false;
            _btnResend.Cursor = Cursors.Default;
            _btnResend.ForeColor = ThemeManager.CurrentSubText;
            _btnResend.Text = "Resend code in 02:00";

            _countdownTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _countdownTimer.Tick += (s, e) =>
            {
                _secondsRemaining--;
                if (_secondsRemaining <= 0)
                {
                    _countdownTimer.Stop();
                    _btnResend.Enabled = true;
                    _btnResend.Cursor = Cursors.Hand;
                    _btnResend.ForeColor = ThemeManager.CurrentPrimary;
                    _btnResend.Text = "Didn't receive it? Resend Code";
                }
                else
                {
                    int mins = _secondsRemaining / 60;
                    int secs = _secondsRemaining % 60;
                    _btnResend.Text = $"Resend code in {mins:D2}:{secs:D2}";
                }
            };
            _countdownTimer.Start();
        }

        public void ShowError(string msg)
        {
            _lblError.Text = "⚠ " + msg;
            _lblError.Visible = true;
            _otpBoxes[0].Focus();
        }

        private void ClearOtpBoxes()
        {
            foreach (var box in _otpBoxes)
            {
                if (box != null) box.Text = "";
            }
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
            _countdownTimer?.Stop();
            _countdownTimer?.Dispose();
            _shakeTimer?.Stop();
            _shakeTimer?.Dispose();
            _checkAnimTimer?.Stop();
            _checkAnimTimer?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
