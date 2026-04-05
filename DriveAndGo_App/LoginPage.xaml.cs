using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Authentication;
using DriveAndGo_App.Services;
using Microsoft.Maui.ApplicationModel;

namespace DriveAndGo_App
{
    public partial class LoginPage : ContentPage
    {
        // ── API base URL para sa Admin/Driver side ──
        private const string ApiBase = "http://10.0.2.2:7243/api";
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        private readonly FirebaseHelper _firebaseHelper;

        private bool _custPasswordVisible = false;
        private bool _drvPasswordVisible = false;
        private bool _isCustomerTab = true;

        public LoginPage(FirebaseHelper firebaseHelper)
        {
            InitializeComponent();
            _firebaseHelper = firebaseHelper;

            // INDUSTRY LOGIC: I-set ang initial position ng Custom Theme Toggle
            SetupInitialThemeState();
        }

        private void SetupInitialThemeState()
        {
            bool isDark = Application.Current!.RequestedTheme == AppTheme.Dark;
            // Dahil pinalitan natin ang ThemeSwitch ng ThemeThumb:
            if (isDark)
            {
                ThemeThumb.TranslationX = 40;
                ThemeToggleContainer.BackgroundColor = Color.FromArgb("#E6510D");
            }
            else
            {
                ThemeThumb.TranslationX = 0;
                ThemeToggleContainer.BackgroundColor = Color.FromArgb("#D1D5DB");
            }
        }

        // ══ ON APPEARING — logo bounce ══
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            AppLogo.Scale = 0.6;
            AppLogo.Opacity = 0;
            WelcomeLabel.Opacity = 0;
            WelcomeLabel.TranslationY = 20;

            await Task.WhenAll(
                AppLogo.FadeTo(1, 400, Easing.CubicOut),
                AppLogo.ScaleTo(1.08, 500, Easing.SpringOut));
            await AppLogo.ScaleTo(1.0, 200, Easing.CubicIn);

            await Task.WhenAll(
                WelcomeLabel.FadeTo(1, 400, Easing.CubicOut),
                WelcomeLabel.TranslateTo(0, 0, 400, Easing.CubicOut));
        }

        // ══ CUSTOM ANIMATED THEME TOGGLE ══
        private async void OnThemeToggleTapped(object sender, TappedEventArgs e)
        {
            bool isCurrentlyDark = Application.Current!.UserAppTheme == AppTheme.Dark;

            if (isCurrentlyDark)
            {
                // Switch to Light Mode
                Application.Current.UserAppTheme = AppTheme.Light;
                ThemeToggleContainer.BackgroundColor = Color.FromArgb("#D1D5DB");
                await ThemeThumb.TranslateTo(0, 0, 250, Easing.CubicInOut);
            }
            else
            {
                // Switch to Dark Mode
                Application.Current.UserAppTheme = AppTheme.Dark;
                ThemeToggleContainer.BackgroundColor = Color.FromArgb("#E6510D");
                await ThemeThumb.TranslateTo(40, 0, 250, Easing.CubicInOut);
            }
        }

        // ══ TAB: CUSTOMER ══
        private async void OnCustomerTabClicked(object sender, TappedEventArgs e)
        {
            if (_isCustomerTab) return;
            _isCustomerTab = true;

            CustomerTabBg.BackgroundColor = Color.FromArgb("#E6510D");
            CustomerTabLabel.TextColor = Colors.White;
            DriverTabBg.BackgroundColor = Colors.Transparent;
            DriverTabLabel.TextColor = Application.Current!.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#6B7A90") : Color.FromArgb("#6B7280");

            await Task.WhenAll(
                DriverForm.FadeTo(0, 150, Easing.CubicIn),
                DriverForm.TranslateTo(20, 0, 150));
            DriverForm.IsVisible = false;
            DriverForm.TranslationX = 0;

            CustomerForm.IsVisible = true;
            CustomerForm.TranslationX = -20;
            CustomerForm.Opacity = 0;
            await Task.WhenAll(
                CustomerForm.FadeTo(1, 250, Easing.CubicOut),
                CustomerForm.TranslateTo(0, 0, 250, Easing.CubicOut));
        }

        // ══ TAB: DRIVER ══
        private async void OnDriverTabClicked(object sender, TappedEventArgs e)
        {
            if (!_isCustomerTab) return;
            _isCustomerTab = false;

            DriverTabBg.BackgroundColor = Color.FromArgb("#E6510D");
            DriverTabLabel.TextColor = Colors.White;
            CustomerTabBg.BackgroundColor = Colors.Transparent;
            CustomerTabLabel.TextColor = Application.Current!.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#6B7A90") : Color.FromArgb("#6B7280");

            await Task.WhenAll(
                CustomerForm.FadeTo(0, 150, Easing.CubicIn),
                CustomerForm.TranslateTo(-20, 0, 150));
            CustomerForm.IsVisible = false;
            CustomerForm.TranslationX = 0;

            DriverForm.IsVisible = true;
            DriverForm.TranslationX = 20;
            DriverForm.Opacity = 0;
            await Task.WhenAll(
                DriverForm.FadeTo(1, 250, Easing.CubicOut),
                DriverForm.TranslateTo(0, 0, 250, Easing.CubicOut));
        }

        // ══ PASSWORD TOGGLES ══
        private void OnCustTogglePassword(object sender, TappedEventArgs e)
        {
            _custPasswordVisible = !_custPasswordVisible;
            CustPasswordEntry.IsPassword = !_custPasswordVisible;
            CustEyeIcon.TextColor = _custPasswordVisible ? Color.FromArgb("#E6510D") : (Application.Current!.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#404060") : Color.FromArgb("#B0B8C8"));
        }

        private void OnDrvTogglePassword(object sender, TappedEventArgs e)
        {
            _drvPasswordVisible = !_drvPasswordVisible;
            DriverPasswordEntry.IsPassword = !_drvPasswordVisible;
            DrvEyeIcon.TextColor = _drvPasswordVisible ? Color.FromArgb("#E6510D") : (Application.Current!.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#404060") : Color.FromArgb("#B0B8C8"));
        }

        // ══ CUSTOMER LOGIN (Industry Validation) ══
        private async void OnCustomerLoginClicked(object sender, TappedEventArgs e)
        {
            string email = CustEmailEntry.Text?.Trim() ?? "";
            string pass = CustPasswordEntry.Text ?? "";

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
            {
                await ShakeForm(CustomerForm);
                await DisplayAlert("⚠️ Incomplete", "Please enter your email and password.", "OK");
                return;
            }

            SetLoadingState(true, true);

            try
            {
                // 1. Validate sa API/XAMPP
                var response = await _http.PostAsJsonAsync($"{ApiBase}/auth/login", new { Email = email, Password = pass });

                if (response.IsSuccessStatusCode)
                {
                    await CustLoginBtn.ScaleTo(0.96, 80);
                    await CustLoginBtn.ScaleTo(1.0, 80);
                    await DisplayAlert("🎉 Welcome!", "Login Successful!", "Let's Ride");
                    // Application.Current!.MainPage = new NavigationPage(new CustomerHomePage());
                }
                else
                {
                    await ShakeForm(CustomerForm);
                    await DisplayAlert("❌ Denied", "Invalid email or password.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Server Connection Failed", "OK");
            }
            finally { SetLoadingState(false, true); }
        }

        // ══ DRIVER LOGIN ══
        private async void OnDriverLoginClicked(object sender, TappedEventArgs e)
        {
            string email = DriverEmailEntry.Text?.Trim() ?? "";
            string pass = DriverPasswordEntry.Text ?? "";

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
            {
                await ShakeForm(DriverForm);
                await DisplayAlert("⚠️ Incomplete", "Driver credentials required.", "OK");
                return;
            }

            SetLoadingState(true, false);
            await Task.Delay(1500); // Simulate API call
            await DisplayAlert("🚗 Driver Login", "Welcome! Drive safe!", "Start Route");
            SetLoadingState(false, false);
        }

        // ══ INDUSTRY-LEVEL SOCIAL LOGINS (Firebase <-> XAMPP Auto-Sync) ══
        private async void OnGoogleLoginClicked(object sender, TappedEventArgs e)
        {
            try
            {
                var authResult = await WebAuthenticator.Default.AuthenticateAsync(
                    new WebAuthenticatorOptions
                    {
                        Url = new Uri("https://vechiclerentaldb.firebaseapp.com/__/auth/handler"),
                        CallbackUrl = new Uri("driveandgo://"),
                        PrefersEphemeralWebBrowserSession = true
                    });

                // INDUSTRY FLOW:
                // 1. User logs in via Google/Firebase.
                // 2. We get the AccessToken.
                // 3. We send this to our C# API.
                // 4. API creates/updates the user in XAMPP MySQL automatically.

                var syncResponse = await _http.PostAsJsonAsync($"{ApiBase}/auth/social-sync", new
                {
                    Provider = "Google",
                    Token = authResult.AccessToken
                });

                if (syncResponse.IsSuccessStatusCode)
                    await DisplayAlert("Success", "Google Sync Complete!", "OK");
            }
            catch (Exception ex) { await DisplayAlert("Auth Error", "Google login failed.", "OK"); }
        }

        private async void OnFacebookLoginClicked(object sender, TappedEventArgs e)
        {
            try
            {
                var authResult = await WebAuthenticator.Default.AuthenticateAsync(
                    new WebAuthenticatorOptions
                    {
                        Url = new Uri("https://vechiclerentaldb.firebaseapp.com/__/auth/handler"),
                        CallbackUrl = new Uri("driveandgo://"),
                        PrefersEphemeralWebBrowserSession = true
                    });

                var syncResponse = await _http.PostAsJsonAsync($"{ApiBase}/auth/social-sync", new
                {
                    Provider = "Facebook",
                    Token = authResult.AccessToken
                });

                if (syncResponse.IsSuccessStatusCode)
                    await DisplayAlert("Success", "Facebook Sync Complete!", "OK");
            }
            catch (Exception ex) { await DisplayAlert("Auth Error", "Facebook login failed.", "OK"); }
        }

        // ══ NAVIGATION & HELPERS ══
        private async void OnSignUpTapped(object sender, TappedEventArgs e) => await DisplayAlert("Sign Up", "Loading Registration...", "OK");
        private async void OnForgotPasswordTapped(object sender, TappedEventArgs e) => await DisplayAlert("Forgot Password", "Contact Admin to reset.", "OK");

        private void SetLoadingState(bool loading, bool isCustomer)
        {
            if (isCustomer)
            {
                CustLoginText.IsVisible = !loading;
                CustLoader.IsVisible = loading;
                CustLoader.IsRunning = loading;
                CustLoginBtn.Opacity = loading ? 0.75 : 1.0;
            }
            else
            {
                DrvLoginText.IsVisible = !loading;
                DrvLoader.IsVisible = loading;
                DrvLoader.IsRunning = loading;
                DrvLoginBtn.Opacity = loading ? 0.75 : 1.0;
            }
        }

        private async Task ShakeForm(VisualElement element)
        {
            for (int i = 0; i < 4; i++)
            {
                await element.TranslateTo(i % 2 == 0 ? 10 : -10, 0, 50);
            }
            await element.TranslateTo(0, 0, 50);
        }
    }
}