using DriveAndGo_App.Configuration;
using DriveAndGo_App.Contracts;
using DriveAndGo_App.Models;
using DriveAndGo_App.State;
using DriveAndGo_App.Utilities;
using System.Collections.ObjectModel;

namespace DriveAndGo_App.ViewModels;

public sealed class BookingsViewModel : ViewModelBase
{
    private readonly IDriveAndGoApiService _apiService;
    private readonly AppSessionState _sessionState;
    private readonly List<RentalItem> _allRentals = new();
    private string _selectedFilter = "All";

    public BookingsViewModel(
        IDriveAndGoApiService apiService,
        AppSessionState sessionState,
        INavigationService navigationService)
        : base(navigationService)
    {
        _apiService = apiService;
        _sessionState = sessionState;
        Rentals = new ObservableCollection<RentalItem>();
        Filters = new ObservableCollection<string>(new[] { "All", "Pending", "Approved", "Active", "Completed", "Cancelled" });
        RefreshCommand = new AsyncCommand(LoadAsync, () => !IsBusy);
    }

    public ObservableCollection<RentalItem> Rentals { get; }
    public ObservableCollection<string> Filters { get; }
    public AsyncCommand RefreshCommand { get; }

    public string SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool HasRentals => Rentals.Count > 0;

    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (_sessionState.CurrentUser == null)
            {
                Rentals.Clear();
                return;
            }

            _allRentals.Clear();
            _allRentals.AddRange(await _apiService.GetCustomerRentalsAsync(_sessionState.CurrentUser.UserId));
            ApplyFilter();
        }, "Loading your bookings...");
    }

    public async Task OpenRentalAsync(RentalItem? rental)
    {
        if (rental == null)
        {
            return;
        }

        await NavigationService.GoToAsync(AppRoutes.RentalDetails, new Dictionary<string, object>
        {
            ["Rental"] = rental
        });
    }

    private void ApplyFilter()
    {
        Rentals.Clear();

        foreach (var rental in _allRentals.Where(MatchesFilter))
        {
            Rentals.Add(rental);
        }

        OnPropertyChanged(nameof(HasRentals));
    }

    private bool MatchesFilter(RentalItem rental)
    {
        if (string.Equals(SelectedFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(SelectedFilter, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return rental.Status is "cancelled" or "rejected";
        }

        return string.Equals(rental.StatusLabel, SelectedFilter, StringComparison.OrdinalIgnoreCase);
    }
}
