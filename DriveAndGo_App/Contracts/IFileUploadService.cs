using DriveAndGo_App.Models;

namespace DriveAndGo_App.Contracts;

public interface IFileUploadService
{
    Task<string?> PickAndUploadAsync(UploadCategory category, CancellationToken cancellationToken = default);
}
