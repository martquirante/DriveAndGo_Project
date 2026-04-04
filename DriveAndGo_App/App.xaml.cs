using Microsoft.Extensions.DependencyInjection;

namespace DriveAndGo_App
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Pinalitan natin ang AppShell para yung Splash Screen natin ang unang bumukas!
            return new Window(new AnimatedSplashPage());
        }
    }
}