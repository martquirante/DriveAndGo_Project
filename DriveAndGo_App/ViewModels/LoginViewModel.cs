using DriveAndGo_App.Configuration;
using DriveAndGo_App.Contracts;
using DriveAndGo_App.Dtos;
using DriveAndGo_App.State;
using DriveAndGo_App.Utilities;

namespace DriveAndGo_App.ViewModels;

public sealed class LoginViewModel : ViewModelBase
{
    private readonly IDriveAndGoApiService _apiService;
    private readonly AppSessionState _sessionState;
    private readonly IThemeService _themeService;

    private string _customerEmail = string.Empty;
    private string _customerPassword = string.Empty;
    private string _driverEmail = string.Empty;
    private string _driverPassword = string.Empty;
    private bool _isCustomerSelected = true;

    public LoginViewModel(
        IDriveAndGoApiService apiService,
        AppSessionState sessionState,
        INavigationService navigationService,
        IThemeService themeService)
        : base(navigationService)
    {
        _apiService = apiService;
        _sessionState = sessionState;
        _themeService = themeService;

        CustomerLoginCommand = new AsyncCommand(LoginCustomerAsync, () => !IsBusy);
        DriverLoginCommand = new AsyncCommand(LoginDriverAsync, () => !IsBusy);
        ToggleThemeCommand = new AsyncCommand(() => _themeService.ToggleBetweenLightAndDarkAsync(), () => !IsBusy);
        OpenRegisterCommand = new AsyncCommand(() => NavigationService.GoToAsync(AppRoutes.Register), () => !IsBusy);
    }

    public string CustomerEmail
    {
        get => _customerEmail;
        set => SetProperty(ref _customerEmail, value);
    }

    public string CustomerPassword
    {
        get => _customerPassword;
        set => SetProperty(ref _customerPassword, value);
    }

    public string DriverEmail
    {
        get => _driverEmail;
        set => SetProperty(ref _driverEmail, value);
    }

    public string DriverPassword
    {
        get => _driverPassword;
        set => SetProperty(ref _driverPassword, value);
    }

    public bool IsCustomerSelected
    {
        get => _isCustomerSelected;
        set
        {
            if (SetProperty(ref _isCustomerSelected, value))
            {
                OnPropertyChanged(nameof(IsDriverSelected));
            }
        }
    }

    public bool IsDriverSelected => !IsCustomerSelected;
    public AsyncCommand CustomerLoginCommand { get; }
    public AsyncCommand DriverLoginCommand { get; }
    public AsyncCommand ToggleThemeCommand { get; }
    public AsyncCommand OpenRegisterCommand { get; }

    public async Task InitializeAsync()
    {
        await _themeService.InitializeAsync();
    }

    public void SelectCustomer() => IsCustomerSelected = true;
    public void SelectDriver() => IsCustomerSelected = false;

    private Task LoginCustomerAsync() => LoginAsync(CustomerEmail, CustomerPassword, expectDriver: false);
    private Task LoginDriverAsync() => LoginAsync(DriverEmail, DriverPassword, expectDriver: true);

    private async Task LoginAsync(string email, string password, bool expectDriver)
    {
        await RunBusyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("Enter your email and password.");
            }

            var user = await _apiService.LoginAsync(new LoginRequestDto
            {
                Email = email.Trim(),
                Password = password
            });

            if (expectDriver && !user.IsDriver)
            {
                throw new InvalidOperationException("This account is not registered as a driver.");
            }

            if (!expectDriver && user.IsDriver)
            {
                throw new InvalidOperationException("Please use the Driver tab for this account.");
            }

            await _sessionState.SetCurrentUserAsync(user);
            await NavigationService.ResetToShellAsync();
        }, "Signing you in...");
    }
}
