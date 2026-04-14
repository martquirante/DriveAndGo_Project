using DriveAndGo_App.Configuration;
using DriveAndGo_App.Contracts;
using DriveAndGo_App.Dtos;
using DriveAndGo_App.Models;
using DriveAndGo_App.State;
using DriveAndGo_App.Utilities;
using System.Collections.ObjectModel;

namespace DriveAndGo_App.ViewModels;

public sealed class VehicleDetailsViewModel : ViewModelBase
{
    private readonly IDriveAndGoApiService _apiService;
    private readonly AppSessionState _sessionState;

    private VehicleItem? _vehicle;
    private bool _withDriver;
    private DriverSummary? _selectedDriver;
    private DateTime _startDate = DateTime.Today.AddDays(1);
    private DateTime _endDate = DateTime.Today.AddDays(2);
    private string _destination = string.Empty;
    private bool _acceptedTerms;
    private string _selectedPaymentMethod = "cash";

    public VehicleDetailsViewModel(
        IDriveAndGoApiService apiService,
        AppSessionState sessionState,
        INavigationService navigationService)
        : base(navigationService)
    {
        _apiService = apiService;
        _sessionState = sessionState;
        AvailableDrivers = new ObservableCollection<DriverSummary>();
        PaymentMethods = new ObservableCollection<string>(new[] { "cash", "gcash", "maya", "bank" });
        BookCommand = new AsyncCommand(SubmitBookingAsync, () => !IsBusy);
    }

    public VehicleItem? Vehicle
    {
        get => _vehicle;
        private set
        {
            if (SetProperty(ref _vehicle, value))
            {
                OnPropertyChanged(nameof(BookingTotalLabel));
            }
        }
    }

    public ObservableCollection<DriverSummary> AvailableDrivers { get; }
    public ObservableCollection<string> PaymentMethods { get; }
    public AsyncCommand BookCommand { get; }

    public bool WithDriver
    {
        get => _withDriver;
        set
        {
            if (SetProperty(ref _withDriver, value))
            {
                OnPropertyChanged(nameof(IsDriverPickerVisible));
                OnPropertyChanged(nameof(BookingTotal));
                OnPropertyChanged(nameof(BookingTotalLabel));
            }
        }
    }

    public bool IsDriverPickerVisible => WithDriver;

    public DriverSummary? SelectedDriver
    {
        get => _selectedDriver;
        set
        {
            if (SetProperty(ref _selectedDriver, value))
            {
                OnPropertyChanged(nameof(BookingTotalLabel));
            }
        }
    }

    public DateTime StartDate
    {
        get => _startDate;
        set
        {
            if (SetProperty(ref _startDate, value))
            {
                if (EndDate <= StartDate)
                {
                    EndDate = StartDate.AddDays(1);
                }

                OnPropertyChanged(nameof(BookingTotal));
                OnPropertyChanged(nameof(BookingTotalLabel));
            }
        }
    }

    public DateTime EndDate
    {
        get => _endDate;
        set
        {
            if (SetProperty(ref _endDate, value))
            {
                OnPropertyChanged(nameof(BookingTotal));
                OnPropertyChanged(nameof(BookingTotalLabel));
            }
        }
    }

    public string Destination
    {
        get => _destination;
        set => SetProperty(ref _destination, value);
    }

    public bool AcceptedTerms
    {
        get => _acceptedTerms;
        set => SetProperty(ref _acceptedTerms, value);
    }

    public string SelectedPaymentMethod
    {
        get => _selectedPaymentMethod;
        set => SetProperty(ref _selectedPaymentMethod, value);
    }

    public decimal BookingTotal
    {
        get
        {
            if (Vehicle == null)
            {
                return 0;
            }

            var dailyRate = WithDriver && Vehicle.RateWithDriver > 0
                ? Vehicle.RateWithDriver
                : Vehicle.RatePerDay;

            var days = Math.Max(1, (EndDate.Date - StartDate.Date).Days);
            return dailyRate * days;
        }
    }

    public string BookingTotalLabel => $"PHP {BookingTotal:N0}";

    public async Task LoadAsync(VehicleItem? vehicle)
    {
        if (vehicle == null)
        {
            return;
        }

        Vehicle = vehicle;
        AvailableDrivers.Clear();

        foreach (var driver in await _apiService.GetAvailableDriversAsync())
        {
            AvailableDrivers.Add(driver);
        }
    }

    private async Task SubmitBookingAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (_sessionState.CurrentUser == null)
            {
                throw new InvalidOperationException("You must sign in first.");
            }

            if (Vehicle == null)
            {
                throw new InvalidOperationException("Vehicle details are not loaded.");
            }

            if (string.IsNullOrWhiteSpace(Destination))
            {
                throw new InvalidOperationException("Enter your destination.");
            }

            if (!AcceptedTerms)
            {
                throw new InvalidOperationException("Confirm the rental agreement first.");
            }

            if (WithDriver && SelectedDriver == null)
            {
                throw new InvalidOperationException("Choose an available driver for this booking.");
            }

            var rentalId = await _apiService.CreateRentalAsync(new CreateRentalRequestDto
            {
                CustomerId = _sessionState.CurrentUser.UserId,
                VehicleId = Vehicle.VehicleId,
                DriverId = WithDriver ? SelectedDriver?.DriverId : null,
                Destination = Destination.Trim(),
                StartDate = StartDate,
                EndDate = EndDate,
                TotalAmount = BookingTotal,
                PaymentMethod = SelectedPaymentMethod
            });

            await NavigationService.GoToAsync($"//{AppRoutes.Bookings}");
        }, "Submitting booking...");
    }
}
