using DriveAndGo_App.Contracts;
using DriveAndGo_App.Dtos;
using DriveAndGo_App.State;
using DriveAndGo_App.Utilities;

namespace DriveAndGo_App.ViewModels;

public sealed class RegisterViewModel : ViewModelBase
{
    private readonly IDriveAndGoApiService _apiService;
    private readonly AppSessionState _sessionState;

    private string _fullName = string.Empty;
    private string _email = string.Empty;
    private string _phone = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;

    public RegisterViewModel(
        IDriveAndGoApiService apiService,
        AppSessionState sessionState,
        INavigationService navigationService)
        : base(navigationService)
    {
        _apiService = apiService;
        _sessionState = sessionState;

        RegisterCommand = new AsyncCommand(RegisterAsync, () => !IsBusy);
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

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    public AsyncCommand RegisterCommand { get; }

    private async Task RegisterAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(FullName) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Password))
            {
                throw new InvalidOperationException("Complete the registration form first.");
            }

            if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Passwords do not match.");
            }

            if (await _apiService.CheckEmailExistsAsync(Email.Trim()))
            {
                throw new InvalidOperationException("That email is already registered.");
            }

            var user = await _apiService.RegisterAsync(new RegisterRequestDto
            {
                FullName = FullName.Trim(),
                Email = Email.Trim(),
                Phone = Phone.Trim(),
                Password = Password,
                Role = "customer"
            });

            await _sessionState.SetCurrentUserAsync(user);
            await NavigationService.ResetToShellAsync();
        }, "Creating your account...");
    }
}
