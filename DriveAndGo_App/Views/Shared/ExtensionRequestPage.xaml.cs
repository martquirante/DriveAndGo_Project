using DriveAndGo_App.Configuration;
using DriveAndGo_App.Models;
using DriveAndGo_App.ViewModels;

namespace DriveAndGo_App.Views.Shared;

public partial class ExtensionRequestPage : ContentPage, IQueryAttributable
{
    private readonly ExtensionRequestViewModel _viewModel;

    public ExtensionRequestPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = AppServices.GetRequiredService<ExtensionRequestViewModel>();
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Rental", out var value) && value is RentalItem rental)
        {
            await _viewModel.LoadAsync(rental);
        }
    }
}
