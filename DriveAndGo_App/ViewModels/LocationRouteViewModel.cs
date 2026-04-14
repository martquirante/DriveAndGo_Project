using DriveAndGo_App.Contracts;
using DriveAndGo_App.Models;
using DriveAndGo_App.Utilities;
using System.Collections.ObjectModel;

namespace DriveAndGo_App.ViewModels;

public sealed class LocationRouteViewModel : ViewModelBase
{
    private readonly ILocationService _locationService;
    private RentalItem? _rental;

    public LocationRouteViewModel(ILocationService locationService, INavigationService navigationService)
        : base(navigationService)
    {
        _locationService = locationService;
        RoutePoints = new ObservableCollection<LocationPoint>();
    }

    public ObservableCollection<LocationPoint> RoutePoints { get; }

    public RentalItem? Rental
    {
        get => _rental;
        private set
        {
            if (SetProperty(ref _rental, value))
            {
                OnPropertyChanged(nameof(HasRoute));
                OnPropertyChanged(nameof(LatestPoint));
                OnPropertyChanged(nameof(SupportsLiveMapSync));
            }
        }
    }

    public bool SupportsLiveMapSync => _locationService.SupportsLiveMapSync;
    public bool HasRoute => RoutePoints.Count > 0;
    public LocationPoint? LatestPoint => RoutePoints.LastOrDefault();

    public async Task LoadAsync(RentalItem? rental)
    {
        if (rental == null)
        {
            return;
        }

        Rental = rental;

        await RunBusyAsync(async () =>
        {
            RoutePoints.Clear();
            foreach (var point in await _locationService.GetRouteAsync(rental.RentalId))
            {
                RoutePoints.Add(point);
            }

            OnPropertyChanged(nameof(HasRoute));
            OnPropertyChanged(nameof(LatestPoint));
        }, "Loading route history...");
    }
}
