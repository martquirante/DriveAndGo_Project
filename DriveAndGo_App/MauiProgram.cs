using DriveAndGo_App.Configuration;
using DriveAndGo_App.Contracts;
using DriveAndGo_App.Services.Api;
using DriveAndGo_App.Services.Location;
using DriveAndGo_App.Services.Navigation;
using DriveAndGo_App.Services.Theme;
using DriveAndGo_App.Services.Uploads;
using DriveAndGo_App.State;
using DriveAndGo_App.ViewModels;
using DriveAndGo_App.Views.Auth;
using DriveAndGo_App.Views.Customer;
using DriveAndGo_App.Views.Driver;
using DriveAndGo_App.Views.Shared;
using Microsoft.Extensions.Logging;

namespace DriveAndGo_App;

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

        builder.Services.AddSingleton(_ => ApiOptions.CreateHttpClient());
        builder.Services.AddSingleton<AppSessionState>();
        builder.Services.AddSingleton<IDriveAndGoApiService, DriveAndGoApiService>();
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<IThemeService, ThemeService>();
        builder.Services.AddSingleton<IFileUploadService, FileUploadService>();
        builder.Services.AddSingleton<ILocationService, LocationService>();

        builder.Services.AddTransient<SplashViewModel>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<CustomerHomeViewModel>();
        builder.Services.AddTransient<VehicleDetailsViewModel>();
        builder.Services.AddTransient<BookingsViewModel>();
        builder.Services.AddTransient<RentalDetailsViewModel>();
        builder.Services.AddTransient<PaymentViewModel>();
        builder.Services.AddTransient<NotificationsViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<DriverDashboardViewModel>();
        builder.Services.AddTransient<MessagesViewModel>();
        builder.Services.AddTransient<LocationRouteViewModel>();
        builder.Services.AddTransient<IssueReportViewModel>();
        builder.Services.AddTransient<ExtensionRequestViewModel>();
        builder.Services.AddTransient<RatingViewModel>();

        builder.Services.AddTransient<AnimatedSplashPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<CustomerHomePage>();
        builder.Services.AddTransient<VehicleDetailsPage>();
        builder.Services.AddTransient<BookingsPage>();
        builder.Services.AddTransient<RentalDetailsPage>();
        builder.Services.AddTransient<PaymentPage>();
        builder.Services.AddTransient<NotificationsPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<DriverDashboardPage>();
        builder.Services.AddTransient<MessagesPage>();
        builder.Services.AddTransient<LocationRoutePage>();
        builder.Services.AddTransient<IssueReportPage>();
        builder.Services.AddTransient<ExtensionRequestPage>();
        builder.Services.AddTransient<RatingPage>();
        builder.Services.AddTransient<AppShell>();

        var app = builder.Build();
        AppServices.Configure(app.Services);
        return app;
    }
}
