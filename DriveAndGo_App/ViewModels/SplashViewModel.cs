using DriveAndGo_App.Contracts;
using DriveAndGo_App.State;
using DriveAndGo_App.Utilities;

namespace DriveAndGo_App.ViewModels;

public sealed class SplashViewModel : ViewModelBase
{
    private readonly AppSessionState _sessionState;
    private readonly IThemeService _themeService;
    private bool _initialized;

    public SplashViewModel(
        AppSessionState sessionState,
        INavigationService navigationService,
        IThemeService themeService)
        : base(navigationService)
    {
        _sessionState = sessionState;
        _themeService = themeService;
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await _themeService.InitializeAsync();
        await _sessionState.InitializeAsync();
        await Task.Delay(1400);

        if (_sessionState.IsAuthenticated)
        {
            await NavigationService.ResetToShellAsync();
        }
        else
        {
            await NavigationService.ResetToLoginAsync();
        }
    }
}
