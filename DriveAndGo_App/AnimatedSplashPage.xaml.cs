using Microsoft.Maui.Controls;
using System.Threading.Tasks;

namespace DriveAndGo_App
{
    public partial class AnimatedSplashPage : ContentPage
    {
        public AnimatedSplashPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // 1. Kunin ang lapad ng screen (Para responsive sa Tablet at Phone)
            double screenWidth = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;

            // 2. I-set ang sasakyan sa pinakakaliwa (labas ng screen)
            CarImage.TranslationX = -(screenWidth / 2) - 100;
            CarImage.TranslationY = -40; // Medyo itaas para may space sa text
            BrandTextLayout.TranslationY = 60; // Ibaba ng konti ang text

            // 3. Ipakita ang sasakyan (Fade In)
            CarImage.Opacity = 1;

            // 4. ANIMATION: "Aandar" ang sasakyan papunta sa gitna
            // Easing.CubicOut = Bibilis sa simula tapos dahan-dahang preno sa gitna
            await CarImage.TranslateTo(0, -40, 1200, Easing.CubicOut);

            // 5. ANIMATION: Lilitaw ang "Drive&Go" text at mag-i-slide pataas ng konti
            var fadeTextTask = BrandTextLayout.FadeTo(1, 800);
            var moveTextTask = BrandTextLayout.TranslateTo(0, 40, 800, Easing.CubicOut);
            await Task.WhenAll(fadeTextTask, moveTextTask);

            // 6. Maghihintay habang umiikot yung loading spinner (Simulating connection to API)
            await Task.Delay(2000);

            // 7. Pagkatapos, lilipat na sa Login/Role Selection Page!
            // Gagamit tayo ng NavigationPage para may smooth transition
            Application.Current.MainPage = new NavigationPage(new LoginPage());
        }
    }
}