using DriveAndGo_App.Contracts;
using DriveAndGo_App.Dtos;
using DriveAndGo_App.Models;
using DriveAndGo_App.Utilities;

namespace DriveAndGo_App.ViewModels;

public sealed class ExtensionRequestViewModel : ViewModelBase
{
    private readonly IDriveAndGoApiService _apiService;
    private RentalItem? _rental;
    private int _addedDays = 1;

    public ExtensionRequestViewModel(IDriveAndGoApiService apiService, INavigationService navigationService)
        : base(navigationService)
    {
        _apiService = apiService;
        SubmitCommand = new AsyncCommand(SubmitAsync, () => !IsBusy);
    }

    public RentalItem? Rental
    {
        get => _rental;
        private set
        {
            if (SetProperty(ref _rental, value))
            {
                OnPropertyChanged(nameof(EstimatedFee));
                OnPropertyChanged(nameof(EstimatedFeeLabel));
            }
        }
    }

    public int AddedDays
    {
        get => _addedDays;
        set
        {
            if (SetProperty(ref _addedDays, Math.Max(1, value)))
            {
                OnPropertyChanged(nameof(EstimatedFee));
                OnPropertyChanged(nameof(EstimatedFeeLabel));
            }
        }
    }

    public AsyncCommand SubmitCommand { get; }

    public decimal EstimatedFee
    {
        get
        {
            if (Rental == null)
            {
                return 0;
            }

            var perDay = Rental.TotalAmount / Rental.RentalDays;
            return perDay * AddedDays;
        }
    }

    public string EstimatedFeeLabel => $"PHP {EstimatedFee:N0}";

    public Task LoadAsync(RentalItem? rental)
    {
        Rental = rental;
        return Task.CompletedTask;
    }

    private async Task SubmitAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (Rental == null)
            {
                throw new InvalidOperationException("Rental details are unavailable.");
            }

            await _apiService.RequestExtensionAsync(new CreateExtensionRequestDto
            {
                RentalId = Rental.RentalId,
                AddedDays = AddedDays
            });

            await NavigationService.GoBackAsync();
        }, "Submitting extension request...");
    }
}
