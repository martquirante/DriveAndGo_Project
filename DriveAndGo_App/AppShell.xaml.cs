using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using DriveAndGo_App.Services; // Kailangan ito para makilala ang Database

namespace DriveAndGo_App
{
    public partial class AppShell : Shell
    {
        // 1. Nakahanda na ang database connection dito
        private readonly FirebaseHelper _firebaseHelper;

        public AppShell(FirebaseHelper firebaseHelper)
        {
            InitializeComponent();
            _firebaseHelper = firebaseHelper;
        }

        // ==========================================
        // 🚪 LOGOUT LOGIC
        // ==========================================
        private void OnLogoutClicked(object sender, EventArgs e)
        {
            // 2. TINGNAN DITO: Ipapasa na natin si _firebaseHelper sa loob ng LoginPage!
            Application.Current!.MainPage =
                new NavigationPage(new LoginPage(_firebaseHelper))
                {
                    BarBackgroundColor = Color.FromArgb("#0A0A14"),
                    BarTextColor = Colors.White
                };
        }
    }
}