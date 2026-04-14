using DriveAndGo_App.Configuration;
using DriveAndGo_App.Models;
using DriveAndGo_App.ViewModels;

namespace DriveAndGo_App.Views.Customer;

public partial class RentalDetailsPage : ContentPage, IQueryAttributable
{
    private readonly RentalDetailsViewModel _viewModel;

    public RentalDetailsPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = AppServices.GetRequiredService<RentalDetailsViewModel>();
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Rental", out var value) && value is RentalItem rental)
        {
            await _viewModel.LoadAsync(rental);
        }
    }

    private Task OpenAsync(Func<Task> action) => action();
    private async void OnPaymentClicked(object? sender, EventArgs e) => await _viewModel.OpenPaymentAsync();
    private async void OnMessagesClicked(object? sender, EventArgs e) => await _viewModel.OpenMessagesAsync();
    private async void OnRouteClicked(object? sender, EventArgs e) => await _viewModel.OpenRouteAsync();
    private async void OnIssueClicked(object? sender, EventArgs e) => await _viewModel.OpenIssueAsync();
    private async void OnExtensionClicked(object? sender, EventArgs e) => await _viewModel.OpenExtensionAsync();
    private async void OnRatingClicked(object? sender, EventArgs e) => await _viewModel.OpenRatingAsync();
}
