using DriveAndGo_App.Contracts;
using DriveAndGo_App.Dtos;
using DriveAndGo_App.Models;
using DriveAndGo_App.State;
using DriveAndGo_App.Utilities;

namespace DriveAndGo_App.ViewModels;

public sealed class ProfileViewModel : ViewModelBase
{
    private readonly IDriveAndGoApiService _apiService;
    private readonly AppSessionState _sessionState;
    private readonly IThemeService _themeService;

    private string _fullName = string.Empty;
    private string _email = string.Empty;
    private string _phone = string.Empty;

    public ProfileViewModel(
        IDriveAndGoApiService apiService,
        AppSessionState sessionState,
        INavigationService navigationService,
        IThemeService themeService)
        : base(navigationService)
    {
        _apiService = apiService;
        _sessionState = sessionState;
        _themeService = themeService;
        SaveCommand = new AsyncCommand(SaveAsync, () => !IsBusy);
        LogoutCommand = new AsyncCommand(LogoutAsync, () => !IsBusy);
        ToggleThemeCommand = new AsyncCommand(() => _themeService.ToggleBetweenLightAndDarkAsync(), () => !IsBusy);
    }

    public string FullName
    {
        get => _fullName;
        set => SetProperty(ref _fullName, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    public string RoleLabel => _sessionState.CurrentUser?.IsDriver == true ? "Driver account" : "Customer account";
    public string ThemeHint => _themeService.AutoModeLabel;
    public AsyncCommand SaveCommand { get; }
    public AsyncCommand LogoutCommand { get; }
    public AsyncCommand ToggleThemeCommand { get; }

    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (_sessionState.CurrentUser == null)
            {
                return;
            }

            var profile = await _apiService.GetUserProfileAsync(_sessionState.CurrentUser.UserId);
            FullName = profile.FullName;
            Email = profile.Email;
            Phone = profile.Phone;
        }, "Loading profile...");
    }

    private async Task SaveAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (_sessionState.CurrentUser == null)
            {
                return;
            }

            await _apiService.UpdateUserProfileAsync(_sessionState.CurrentUser.UserId, new UpdateUserProfileRequestDto
            {
                FullName = FullName.Trim(),
                Email = Email.Trim(),
                Phone = Phone.Trim()
            });

            await _sessionState.SetCurrentUserAsync(new SessionUser
            {
                UserId = _sessionState.CurrentUser.UserId,
                DriverId = _sessionState.CurrentUser.DriverId,
                FullName = FullName.Trim(),
                Email = Email.Trim(),
                Phone = Phone.Trim(),
                Role = _sessionState.CurrentUser.Role
            });
        }, "Saving profile...");
    }

    private async Task LogoutAsync()
    {
        await _sessionState.ClearAsync();
        await NavigationService.ResetToLoginAsync();
    }
}
