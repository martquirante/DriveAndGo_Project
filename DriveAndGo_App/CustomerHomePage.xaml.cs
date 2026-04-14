using DriveAndGo_App.Configuration;
using DriveAndGo_App.Models;
using DriveAndGo_App.ViewModels;

namespace DriveAndGo_App;

public partial class CustomerHomePage : ContentPage
{
    private readonly CustomerHomeViewModel _viewModel;

    public CustomerHomePage()
    {
        InitializeComponent();
        BindingContext = _viewModel = AppServices.GetRequiredService<CustomerHomeViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnVehicleSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is VehicleItem vehicle)
        {
            await _viewModel.OpenVehicleAsync(vehicle);
            ((CollectionView)sender!).SelectedItem = null;
        }
    }
}
