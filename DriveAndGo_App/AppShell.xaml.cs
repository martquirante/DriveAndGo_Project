using DriveAndGo_App.Configuration;
using DriveAndGo_App.State;
using DriveAndGo_App.Views.Customer;
using DriveAndGo_App.Views.Driver;
using DriveAndGo_App.Views.Shared;

namespace DriveAndGo_App;

public partial class AppShell : Shell
{
    private static bool _routesRegistered;
    private readonly AppSessionState _sessionState;

    public AppShell()
    {
        InitializeComponent();
        _sessionState = AppServices.GetRequiredService<AppSessionState>();

        RegisterRoutes();
        BuildTabs();
    }

    private void RegisterRoutes()
    {
        if (_routesRegistered)
        {
            return;
        }

        Routing.RegisterRoute(AppRoutes.Register, typeof(Views.Auth.RegisterPage));
        Routing.RegisterRoute(AppRoutes.VehicleDetails, typeof(VehicleDetailsPage));
        Routing.RegisterRoute(AppRoutes.RentalDetails, typeof(RentalDetailsPage));
        Routing.RegisterRoute(AppRoutes.Payment, typeof(PaymentPage));
        Routing.RegisterRoute(AppRoutes.Messages, typeof(MessagesPage));
        Routing.RegisterRoute(AppRoutes.LocationRoute, typeof(LocationRoutePage));
        Routing.RegisterRoute(AppRoutes.IssueReport, typeof(IssueReportPage));
        Routing.RegisterRoute(AppRoutes.ExtensionRequest, typeof(ExtensionRequestPage));
        Routing.RegisterRoute(AppRoutes.Rating, typeof(RatingPage));

        _routesRegistered = true;
    }

    private void BuildTabs()
    {
        Items.Clear();
        var tabBar = new TabBar();

        if (_sessionState.IsDriver)
        {
            tabBar.Items.Add(CreateTab<DriverDashboardPage>("Dashboard", AppRoutes.DriverDashboard));
            tabBar.Items.Add(CreateTab<NotificationsPage>("Alerts", AppRoutes.Notifications));
            tabBar.Items.Add(CreateTab<ProfilePage>("Profile", AppRoutes.Profile));
        }
        else
        {
            tabBar.Items.Add(CreateTab<CustomerHomePage>("Explore", AppRoutes.CustomerHome));
            tabBar.Items.Add(CreateTab<BookingsPage>("Bookings", AppRoutes.Bookings));
            tabBar.Items.Add(CreateTab<NotificationsPage>("Alerts", AppRoutes.Notifications));
            tabBar.Items.Add(CreateTab<ProfilePage>("Profile", AppRoutes.Profile));
        }

        Items.Add(tabBar);
    }

    private static ShellContent CreateTab<TPage>(string title, string route)
        where TPage : Page
    {
        return new ShellContent
        {
            Title = title,
            Route = route,
            ContentTemplate = new DataTemplate(() => AppServices.GetRequiredService<TPage>())
        };
    }
}
