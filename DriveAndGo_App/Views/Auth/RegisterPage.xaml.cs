using DriveAndGo_App.Configuration;
using DriveAndGo_App.ViewModels;

namespace DriveAndGo_App.Views.Auth;

public partial class RegisterPage : ContentPage
{
    public RegisterPage()
    {
        InitializeComponent();
        BindingContext = AppServices.GetRequiredService<RegisterViewModel>();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
