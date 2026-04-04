using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;

namespace DriveAndGo_App
{
    public partial class LoginPage : ContentPage
    {
        private string _selectedRole = "Customer"; // Default role

        public LoginPage()
        {
            InitializeComponent();
        }

        private void OnRoleSelected(object sender, EventArgs e)
        {
            var clickedBtn = (Button)sender;
            bool isDark = Application.Current.RequestedTheme == AppTheme.Dark;

            // 1. I-reset muna natin pareho sa "Inactive" style
            btnCustomer.BackgroundColor = Colors.Transparent;
            btnCustomer.TextColor = isDark ? Color.FromArgb("#a0a0c0") : Color.FromArgb("#404060");

            btnDriver.BackgroundColor = Colors.Transparent;
            btnDriver.TextColor = isDark ? Color.FromArgb("#a0a0c0") : Color.FromArgb("#404060");

            // 2. I-set sa "Active" style kung sino ang pinindot
            clickedBtn.BackgroundColor = Color.FromArgb("#e6510d"); // Orange
            clickedBtn.TextColor = Colors.White;

            // 3. I-save kung anong role ang napili
            _selectedRole = clickedBtn.Text;
        }

        private async void OnSignInClicked(object sender, EventArgs e)
        {
            string email = txtEmail.Text?.Trim();
            string password = txtPassword.Text?.Trim();

            // Simple validation muna bago i-connect sa database
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                await DisplayAlert("Error", "Please enter both email and password.", "OK");
                return;
            }

            // TODO: Dito natin tatawagin yung API para i-check sa MySQL Database kung tama ang login.
            // Pansamantala, ide-diretso muna natin sa tamang Dashboard base sa napiling Role:

            if (_selectedRole == "Driver")
            {
                // Kung driver ang napili, pupunta sa Driver Dashboard (gagawin natin next time)
                await DisplayAlert("Success", "Logging in as Driver...", "OK");
                // Application.Current.MainPage = new NavigationPage(new DriverDashboardPage());
            }
            else
            {
                // Kung customer ang napili, pupunta sa Customer Home Page (yung ginawa nating may sasakyan)
                Application.Current.MainPage = new NavigationPage(new CustomerHomePage());
            }
        }

        private async void OnForgotPasswordTapped(object sender, EventArgs e)
        {
            await DisplayAlert("Forgot Password", "Navigate to Forgot Password Screen", "OK");
        }

        private async void OnSignUpTapped(object sender, EventArgs e)
        {
            await DisplayAlert("Sign Up", "Navigate to Registration Screen", "OK");
            // await Navigation.PushAsync(new RegisterPage());
        }
    }
}