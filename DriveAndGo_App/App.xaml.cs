using Microsoft.Maui.Controls;
using System;

namespace DriveAndGo_App
{
    public partial class App : Application
    {
        // 1. Ibabalik natin sa simple (walang laman) ang constructor
        public App()
        {
            InitializeComponent();

            // Note: Wala nang "MainPage = ..." dito para hindi ma-shock ang Windows!
        }

        // 2. Dito natin gagawin ang magic! Tatawagin ito ng system kapag "ready" na ang Windows/Android
        protected override Window CreateWindow(IActivationState activationState)
        {
            // Ligtas na nating kukunin si Splash Page kasama yung Database connection niya
            var splashPage = activationState.Context.Services.GetService<AnimatedSplashPage>();

            // Ilo-load na natin sa screen!
            return new Window(splashPage);
        }
    }
}