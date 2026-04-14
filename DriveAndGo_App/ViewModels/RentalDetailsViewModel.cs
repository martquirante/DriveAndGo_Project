using DriveAndGo_App.Configuration;
using DriveAndGo_App.Contracts;
using DriveAndGo_App.Models;
using DriveAndGo_App.Utilities;
using System.Collections.ObjectModel;

namespace DriveAndGo_App.ViewModels;

public sealed class RentalDetailsViewModel : ViewModelBase
{
    private readonly IDriveAndGoApiService _apiService;
    private RentalItem? _rental;

    public RentalDetailsViewModel(IDriveAndGoApiService apiService, INavigationService navigationService)
        : base(navigationService)
    {
        _apiService = apiService;
        Transactions = new ObservableCollection<TransactionItem>();
        CancelCommand = new AsyncCommand(CancelRentalAsync, () => !IsBusy);
    }

    public RentalItem? Rental
    {
        get => _rental;
        private set
        {
            if (SetProperty(ref _rental, value))
            {
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(CanPay));
                OnPropertyChanged(nameof(CanRate));
            }
        }
    }

    public ObservableCollection<TransactionItem> Transactions { get; }
    public AsyncCommand CancelCommand { get; }
    public bool CanCancel => Rental?.CanBeCancelled == true;
    public bool CanPay => Rental?.NeedsPayment == true;
    public bool CanRate => Rental?.CanBeRated == true;

    public async Task LoadAsync(RentalItem? rental)
    {
        if (rental == null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            Rental = await _apiService.GetRentalAsync(rental.RentalId);
            Transactions.Clear();
            foreach (var item in await _apiService.GetTransactionsByRentalAsync(rental.RentalId))
            {
                Transactions.Add(item);
            }
        }, "Loading booking details...");
    }

    public Task OpenPaymentAsync() => NavigateWithRentalAsync(AppRoutes.Payment);
    public Task OpenMessagesAsync() => NavigateWithRentalAsync(AppRoutes.Messages);
    public Task OpenRouteAsync() => NavigateWithRentalAsync(AppRoutes.LocationRoute);
    public Task OpenIssueAsync() => NavigateWithRentalAsync(AppRoutes.IssueReport);
    public Task OpenExtensionAsync() => NavigateWithRentalAsync(AppRoutes.ExtensionRequest);
    public Task OpenRatingAsync() => NavigateWithRentalAsync(AppRoutes.Rating);

    private async Task CancelRentalAsync()
    {
        if (Rental == null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await _apiService.CancelRentalAsync(Rental.RentalId);
            Rental = await _apiService.GetRentalAsync(Rental.RentalId);
        }, "Cancelling booking...");
    }

    private Task NavigateWithRentalAsync(string route)
    {
        if (Rental == null)
        {
            return Task.CompletedTask;
        }

        return NavigationService.GoToAsync(route, new Dictionary<string, object>
        {
            ["Rental"] = Rental
        });
    }
}
