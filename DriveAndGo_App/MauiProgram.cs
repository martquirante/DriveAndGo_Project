using Microsoft.Extensions.Logging;
using DriveAndGo_App.Services; // ── DAGDAG ITO SA TAAS (Para makilala si Firebase) ──

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

            // ── 1. I-REGISTER ANG DATABASE NATIN (PINAKA-IMPORTANTE) ──
            builder.Services.AddSingleton<FirebaseHelper>();

            // ── 2. I-REGISTER ANG MGA PAGES ──
            builder.Services.AddTransient<AnimatedSplashPage>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<CustomerHomePage>();

            // Kung nagawa mo na rin yung sa driver, i-uncomment mo ito:
            // builder.Services.AddTransient<DriverHomePage>();

            return builder.Build();
        }
    }
}