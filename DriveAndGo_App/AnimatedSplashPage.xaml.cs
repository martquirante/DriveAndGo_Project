using DriveAndGo_App.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel; // ── DAGDAG ITO PARA SA MAINTHREAD ──

namespace DriveAndGo_App
{
    public partial class AnimatedSplashPage : ContentPage
    {
        private CancellationTokenSource _cts = new();

        // ── 1. NAKA-READY NA AGAD ANG LOGIN PAGE ──
        private readonly LoginPage _loginPage;

        // ── 2. HINIHINGI NA NATIN SIYA PAGKABUKAS PA LANG ──
        public AnimatedSplashPage(LoginPage loginPage)
        {
            InitializeComponent();
            _loginPage = loginPage;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                await RunSplashAnimation(_cts.Token);
            }
            catch (TaskCanceledException) { }
        }

        protected override void OnDisappearing()
        {
            _cts.Cancel();
            base.OnDisappearing();
        }

        private async Task RunSplashAnimation(CancellationToken ct)
        {
            double screenW = DeviceDisplay.MainDisplayInfo.Width
                           / DeviceDisplay.MainDisplayInfo.Density;
            double screenH = DeviceDisplay.MainDisplayInfo.Height
                           / DeviceDisplay.MainDisplayInfo.Density;

            // ── Phase 1: Background fade in ──
            await BgGradient.FadeTo(1, 400, Easing.CubicOut);

            // ── Phase 2: Particles fade in (async) ──
            _ = AnimateParticles(ct);

            // ── Phase 3: Vehicle drives in from LEFT ──
            HeadlightGlow.TranslationX = -300;
            _ = HeadlightGlow.FadeTo(0.6, 300, Easing.CubicOut);

            _ = ShowSpeedLines();

            var vehicleSlide = VehicleImage.TranslateTo(
                screenW + 50, 0, 1200, Easing.CubicOut);
            var glowSlide = HeadlightGlow.TranslateTo(
                screenW - 100, 0, 1100, Easing.CubicOut);

            await Task.WhenAll(vehicleSlide, glowSlide);

            // ── Phase 4: Speed lines fade out ──
            await Task.WhenAll(
                SpeedLine1.FadeTo(0, 200),
                SpeedLine2.FadeTo(0, 200),
                SpeedLine3.FadeTo(0, 200),
                HeadlightGlow.FadeTo(0, 200));

            await Task.Delay(100, ct);

            // ── Phase 5: Logo pops in (scale + fade) ──
            LogoImage.Scale = 0.3;
            LogoImage.Opacity = 0;

            var logoScale = LogoImage.ScaleTo(1.08, 500, Easing.SpringOut);
            var logoFade = LogoImage.FadeTo(1, 400, Easing.CubicOut);
            await Task.WhenAll(logoScale, logoFade);

            await LogoImage.ScaleTo(1.0, 200, Easing.CubicIn);

            // ── Phase 6: App name slides up ──
            AppNameLabel.TranslationY = 24;
            await Task.WhenAll(
                AppNameLabel.FadeTo(1, 500, Easing.CubicOut),
                AppNameLabel.TranslateTo(0, 0, 500, Easing.CubicOut));

            // ── Phase 7: Orange underline extends ──
            await Task.Delay(100, ct);
            var underlineAnim = new Animation(v => {
                OrangeUnderline.WidthRequest = v;
            }, 0, 180, Easing.CubicOut);
            underlineAnim.Commit(this, "Underline", 16, 400);
            await Task.Delay(420, ct);

            // ── Phase 8: Tagline appears letter by letter (typewriter) ──
            TaglineLabel.Opacity = 1;
            string fullTagline = "DRIVE SAFE, RENT EASY";
            TaglineLabel.Text = "";
            foreach (char c in fullTagline)
            {
                ct.ThrowIfCancellationRequested();
                TaglineLabel.Text += c;
                await Task.Delay(38, ct);
            }

            // ── Phase 9: Subtitle + version fade in ──
            await Task.WhenAll(
                SubtitleLabel.FadeTo(1, 400, Easing.CubicOut),
                VersionLabel.FadeTo(1, 400, Easing.CubicOut));

            // ── Phase 10: Loading dots appear + pulse ──
            await LoadingDots.FadeTo(1, 300, Easing.CubicOut);
            _ = AnimateLoadingDots(ct);

            // ── Phase 11: Logo floats gently ──
            _ = FloatLogo(ct);

            // ── Phase 12: Hold for 1.5s then transition ──
            await Task.Delay(1800, ct);

            // ── Phase 13: Exit animation ──
            await Task.WhenAll(
                RootLayout.FadeTo(0, 500, Easing.CubicIn),
                RootLayout.ScaleTo(1.06, 500, Easing.CubicIn));

            // ── 3. I-FORCE NATIN SA MAIN THREAD PARA HINDI MAG-CRASH ──
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Application.Current!.MainPage = new NavigationPage(_loginPage)
                {
                    BarBackgroundColor = Color.FromArgb("#0A0A14"),
                    BarTextColor = Colors.White
                };
            });
        }

        // ── Speed lines reveal ──
        private async Task ShowSpeedLines()
        {
            await Task.WhenAll(
                SpeedLine1.FadeTo(0.9, 200, Easing.CubicOut),
                SpeedLine2.FadeTo(0.7, 200, Easing.CubicOut),
                SpeedLine3.FadeTo(0.8, 200, Easing.CubicOut));
        }

        // ── Particle floating animation ──
        private async Task AnimateParticles(CancellationToken ct)
        {
            var dots = new[] { Dot1, Dot2, Dot3, Dot4 };
            int[] delays = { 0, 200, 100, 300 };

            for (int i = 0; i < dots.Length; i++)
            {
                int idx = i;
                _ = AnimateSingleParticle(dots[idx], delays[idx], ct);
            }
            await Task.CompletedTask;
        }

        private async Task AnimateSingleParticle(
            BoxView dot, int delay, CancellationToken ct)
        {
            await Task.Delay(delay, ct);
            while (!ct.IsCancellationRequested)
            {
                await dot.FadeTo(0.9, 800, Easing.CubicOut);
                await dot.TranslateTo(0, -12, 1200, Easing.SinOut);
                await Task.WhenAll(
                    dot.FadeTo(0, 600, Easing.CubicIn),
                    dot.TranslateTo(0, 0, 600));
                await Task.Delay(400, ct);
            }
        }

        // ── Loading dots pulse ──
        private async Task AnimateLoadingDots(CancellationToken ct)
        {
            var dots = new[] { LoadDot1, LoadDot2, LoadDot3 };
            var active = Color.FromArgb("#E6510D");
            var idle = Color.FromArgb("#444455");
            int idx = 0;

            while (!ct.IsCancellationRequested)
            {
                for (int i = 0; i < 3; i++)
                    dots[i].Color = i == idx ? active : idle;

                await dots[idx].ScaleTo(1.4, 200, Easing.CubicOut);
                await dots[idx].ScaleTo(1.0, 200, Easing.CubicIn);

                idx = (idx + 1) % 3;
                await Task.Delay(200, ct);
            }
        }

        // ── Logo gentle float ──
        private async Task FloatLogo(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await LogoImage.TranslateTo(0, -8, 1200, Easing.SinOut);
                await LogoImage.TranslateTo(0, 0, 1200, Easing.SinIn);
            }
        }
    }
}