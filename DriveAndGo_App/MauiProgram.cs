using Microsoft.Extensions.Logging;

namespace DriveAndGo_App
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // ── GOOD PRACTICE: I-register ang mga Pages dito ──
            builder.Services.AddTransient<AnimatedSplashPage>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<CustomerHomePage>();

            // Kung nagawa mo na rin yung sa driver, i-uncomment mo ito:
            // builder.Services.AddTransient<DriverDashboardPage>();

            return builder.Build();
        }
    }
}