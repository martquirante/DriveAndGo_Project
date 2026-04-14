using DriveAndGo_App.Configuration;
using DriveAndGo_App.Models;
using DriveAndGo_App.ViewModels;

namespace DriveAndGo_App.Views.Driver;

public partial class DriverDashboardPage : ContentPage
{
    private readonly DriverDashboardViewModel _viewModel;

    public DriverDashboardPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = AppServices.GetRequiredService<DriverDashboardViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnAssignmentSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is RentalItem rental)
        {
            await _viewModel.OpenAssignmentAsync(rental);
            ((CollectionView)sender!).SelectedItem = null;
        }
    }
}
