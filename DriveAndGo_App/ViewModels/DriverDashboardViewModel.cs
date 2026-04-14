using DriveAndGo_App.Configuration;
using DriveAndGo_App.Contracts;
using DriveAndGo_App.Models;
using DriveAndGo_App.State;
using DriveAndGo_App.Utilities;
using System.Collections.ObjectModel;

namespace DriveAndGo_App.ViewModels;

public sealed class DriverDashboardViewModel : ViewModelBase
{
    private readonly IDriveAndGoApiService _apiService;
    private readonly AppSessionState _sessionState;

    public DriverDashboardViewModel(
        IDriveAndGoApiService apiService,
        AppSessionState sessionState,
        INavigationService navigationService)
        : base(navigationService)
    {
        _apiService = apiService;
        _sessionState = sessionState;
        Assignments = new ObservableCollection<RentalItem>();
        RefreshCommand = new AsyncCommand(LoadAsync, () => !IsBusy);
    }

    public ObservableCollection<RentalItem> Assignments { get; }
    public AsyncCommand RefreshCommand { get; }
    public bool HasAssignments => Assignments.Count > 0;
    public int ActiveTrips => Assignments.Count(item => item.Status is "approved" or "active" or "in-use");
    public decimal EstimatedIncome => Assignments.Where(item => item.Status is "approved" or "active" or "in-use" or "completed").Sum(item => item.TotalAmount * 0.20m);
    public string EstimatedIncomeLabel => $"PHP {EstimatedIncome:N0}";

    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            Assignments.Clear();

            if (_sessionState.CurrentUser == null)
            {
                return;
            }

            foreach (var assignment in await _apiService.GetDriverAssignmentsAsync(_sessionState.CurrentUser.UserId))
            {
                Assignments.Add(assignment);
            }

            OnPropertyChanged(nameof(HasAssignments));
            OnPropertyChanged(nameof(ActiveTrips));
            OnPropertyChanged(nameof(EstimatedIncomeLabel));
        }, "Loading driver dashboard...");
    }

    public Task OpenAssignmentAsync(RentalItem? rental)
    {
        if (rental == null)
        {
            return Task.CompletedTask;
        }

        return NavigationService.GoToAsync(AppRoutes.RentalDetails, new Dictionary<string, object>
        {
            ["Rental"] = rental
        });
    }
}
