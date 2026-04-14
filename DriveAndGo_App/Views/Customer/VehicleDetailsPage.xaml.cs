using DriveAndGo_App.Configuration;
using DriveAndGo_App.Models;
using DriveAndGo_App.ViewModels;

namespace DriveAndGo_App.Views.Customer;

public partial class VehicleDetailsPage : ContentPage, IQueryAttributable
{
    private readonly VehicleDetailsViewModel _viewModel;

    public VehicleDetailsPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = AppServices.GetRequiredService<VehicleDetailsViewModel>();
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Vehicle", out var value) && value is VehicleItem vehicle)
        {
            await _viewModel.LoadAsync(vehicle);
        }
    }
}
