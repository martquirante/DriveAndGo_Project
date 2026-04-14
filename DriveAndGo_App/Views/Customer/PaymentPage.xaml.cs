using DriveAndGo_App.Configuration;
using DriveAndGo_App.Models;
using DriveAndGo_App.ViewModels;

namespace DriveAndGo_App.Views.Customer;

public partial class PaymentPage : ContentPage, IQueryAttributable
{
    private readonly PaymentViewModel _viewModel;

    public PaymentPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = AppServices.GetRequiredService<PaymentViewModel>();
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Rental", out var value) && value is RentalItem rental)
        {
            await _viewModel.LoadAsync(rental);
        }
    }
}
