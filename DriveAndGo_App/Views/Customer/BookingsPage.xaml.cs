using DriveAndGo_App.Configuration;
using DriveAndGo_App.Models;
using DriveAndGo_App.ViewModels;

namespace DriveAndGo_App.Views.Customer;

public partial class BookingsPage : ContentPage
{
    private readonly BookingsViewModel _viewModel;

    public BookingsPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = AppServices.GetRequiredService<BookingsViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnRentalSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is RentalItem rental)
        {
            await _viewModel.OpenRentalAsync(rental);
            ((CollectionView)sender!).SelectedItem = null;
        }
    }
}
