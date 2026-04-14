using DriveAndGo_App.Configuration;
using DriveAndGo_App.ViewModels;

namespace DriveAndGo_App;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _viewModel;

    public LoginPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = AppServices.GetRequiredService<LoginViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        AuthCard.Opacity = 0;
        AuthCard.TranslationY = 22;
        await _viewModel.InitializeAsync();
        await Task.WhenAll(
            AuthCard.FadeToAsync(1, 350, Easing.CubicOut),
            AuthCard.TranslateToAsync(0, 0, 350, Easing.CubicOut));
    }

    private void OnCustomerClicked(object? sender, EventArgs e)
    {
        _viewModel.SelectCustomer();
        CustomerPanel.IsVisible = true;
        DriverPanel.IsVisible = false;
    }

    private void OnDriverClicked(object? sender, EventArgs e)
    {
        _viewModel.SelectDriver();
        CustomerPanel.IsVisible = false;
        DriverPanel.IsVisible = true;
    }
}
