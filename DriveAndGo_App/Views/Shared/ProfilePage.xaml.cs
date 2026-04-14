using DriveAndGo_App.Configuration;
using DriveAndGo_App.ViewModels;

namespace DriveAndGo_App.Views.Shared;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;

    public ProfilePage()
    {
        InitializeComponent();
        BindingContext = _viewModel = AppServices.GetRequiredService<ProfileViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
