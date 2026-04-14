using DriveAndGo_App.Configuration;
using DriveAndGo_App.Models;
using DriveAndGo_App.ViewModels;

namespace DriveAndGo_App.Views.Shared;

public partial class MessagesPage : ContentPage, IQueryAttributable
{
    private readonly MessagesViewModel _viewModel;

    public MessagesPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = AppServices.GetRequiredService<MessagesViewModel>();
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Rental", out var value) && value is RentalItem rental)
        {
            await _viewModel.LoadAsync(rental);
        }
    }
}
