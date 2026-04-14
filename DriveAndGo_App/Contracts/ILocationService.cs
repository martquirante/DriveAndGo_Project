using DriveAndGo_App.Models;

namespace DriveAndGo_App.Contracts;

public interface ILocationService
{
    Task<IReadOnlyList<LocationPoint>> GetRouteAsync(int rentalId, CancellationToken cancellationToken = default);
    bool SupportsLiveMapSync { get; }
}
