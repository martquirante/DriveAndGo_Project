using DriveAndGo_App.Contracts;
using DriveAndGo_App.Models;

namespace DriveAndGo_App.Services.Location;

public sealed class LocationService : ILocationService
{
    private readonly IDriveAndGoApiService _apiService;

    public LocationService(IDriveAndGoApiService apiService)
    {
        _apiService = apiService;
    }

    public bool SupportsLiveMapSync => false;

    public Task<IReadOnlyList<LocationPoint>> GetRouteAsync(int rentalId, CancellationToken cancellationToken = default)
    {
        return _apiService.GetLocationHistoryAsync(rentalId, cancellationToken);
    }
}
