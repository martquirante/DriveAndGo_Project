using DriveAndGo_App.Configuration;
using DriveAndGo_App.Contracts;
using DriveAndGo_App.Views.Auth;

namespace DriveAndGo_App.Services.Navigation;

public sealed class NavigationService : INavigationService
{
    public Task GoToAsync(string route, IDictionary<string, object>? parameters = null)
    {
        if (Shell.Current != null)
        {
            return parameters == null
                ? Shell.Current.GoToAsync(route)
                : Shell.Current.GoToAsync(route, true, parameters);
        }

        if (TryCreateStandalonePage(route, out var page) &&
            Application.Current?.Windows.Count > 0)
        {
            return Application.Current.Windows[0].Page.Navigation.PushAsync(page);
        }

        return Task.CompletedTask;
    }

    public Task GoBackAsync()
    {
        if (Shell.Current != null)
        {
            return Shell.Current.GoToAsync("..");
        }

        if (Application.Current?.Windows.Count > 0)
        {
            return Application.Current.Windows[0].Page.Navigation.PopAsync();
        }

        return Task.CompletedTask;
    }

    public Task ResetToShellAsync()
    {
        SetRootPage(new AppShell());
        return Task.CompletedTask;
    }

    public Task ResetToLoginAsync()
    {
        SetRootPage(new NavigationPage(new LoginPage()));
        return Task.CompletedTask;
    }

    private static void SetRootPage(Page page)
    {
        if (Application.Current?.Windows.Count > 0)
        {
            Application.Current.Windows[0].Page = page;
        }
    }

    private static bool TryCreateStandalonePage(string route, out Page page)
    {
        switch (route.Trim('/'))
        {
            case AppRoutes.Register:
                page = new RegisterPage();
                return true;
            default:
                page = new ContentPage();
                return false;
        }
    }
}
