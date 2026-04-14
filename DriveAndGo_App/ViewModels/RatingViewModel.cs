using DriveAndGo_App.Contracts;
using DriveAndGo_App.Dtos;
using DriveAndGo_App.Models;
using DriveAndGo_App.State;
using DriveAndGo_App.Utilities;

namespace DriveAndGo_App.ViewModels;

public sealed class RatingViewModel : ViewModelBase
{
    private readonly IDriveAndGoApiService _apiService;
    private readonly AppSessionState _sessionState;
    private RentalItem? _rental;
    private int _vehicleScore = 5;
    private int _driverScore = 5;
    private string _comment = string.Empty;

    public RatingViewModel(
        IDriveAndGoApiService apiService,
        AppSessionState sessionState,
        INavigationService navigationService)
        : base(navigationService)
    {
        _apiService = apiService;
        _sessionState = sessionState;
        SubmitCommand = new AsyncCommand(SubmitAsync, () => !IsBusy);
    }

    public RentalItem? Rental
    {
        get => _rental;
        private set
        {
            if (SetProperty(ref _rental, value))
            {
                OnPropertyChanged(nameof(HasDriverRating));
            }
        }
    }

    public int VehicleScore
    {
        get => _vehicleScore;
        set => SetProperty(ref _vehicleScore, Math.Clamp(value, 1, 5));
    }

    public int DriverScore
    {
        get => _driverScore;
        set => SetProperty(ref _driverScore, Math.Clamp(value, 1, 5));
    }

    public string Comment
    {
        get => _comment;
        set => SetProperty(ref _comment, value);
    }

    public bool HasDriverRating => Rental?.DriverId != null;
    public AsyncCommand SubmitCommand { get; }

    public Task LoadAsync(RentalItem? rental)
    {
        Rental = rental;
        return Task.CompletedTask;
    }

    private async Task SubmitAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (Rental == null || _sessionState.CurrentUser == null)
            {
                throw new InvalidOperationException("Rating details are unavailable.");
            }

            await _apiService.SubmitRatingAsync(new CreateRatingRequestDto
            {
                RentalId = Rental.RentalId,
                CustomerId = _sessionState.CurrentUser.UserId,
                DriverId = Rental.DriverId,
                VehicleId = Rental.VehicleId,
                VehicleScore = VehicleScore,
                DriverScore = HasDriverRating ? DriverScore : null,
                Comment = string.IsNullOrWhiteSpace(Comment) ? null : Comment.Trim()
            });

            await NavigationService.GoBackAsync();
        }, "Submitting rating...");
    }
}
