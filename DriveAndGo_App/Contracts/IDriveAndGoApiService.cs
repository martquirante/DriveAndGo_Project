using DriveAndGo_App.Dtos;
using DriveAndGo_App.Models;

namespace DriveAndGo_App.Contracts;

public interface IDriveAndGoApiService
{
    Task<bool> CheckEmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<SessionUser> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<SessionUser> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VehicleItem>> GetAvailableVehiclesAsync(CancellationToken cancellationToken = default);
    Task<VehicleItem> GetVehicleAsync(int vehicleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DriverSummary>> GetAvailableDriversAsync(CancellationToken cancellationToken = default);
    Task<int> CreateRentalAsync(CreateRentalRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RentalItem>> GetCustomerRentalsAsync(int customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RentalItem>> GetDriverAssignmentsAsync(int userId, CancellationToken cancellationToken = default);
    Task<RentalItem> GetRentalAsync(int rentalId, CancellationToken cancellationToken = default);
    Task CancelRentalAsync(int rentalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransactionItem>> GetTransactionsByRentalAsync(int rentalId, CancellationToken cancellationToken = default);
    Task<int> SubmitPaymentAsync(CreateTransactionRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppNotificationModel>> GetNotificationsAsync(int userId, CancellationToken cancellationToken = default);
    Task MarkNotificationReadAsync(int notificationId, CancellationToken cancellationToken = default);
    Task<SessionUser> GetUserProfileAsync(int userId, CancellationToken cancellationToken = default);
    Task UpdateUserProfileAsync(int userId, UpdateUserProfileRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessageItem>> GetMessagesAsync(int rentalId, CancellationToken cancellationToken = default);
    Task SendMessageAsync(SendMessageRequestDto request, CancellationToken cancellationToken = default);
    Task<int> ReportIssueAsync(CreateIssueRequestDto request, CancellationToken cancellationToken = default);
    Task<int> RequestExtensionAsync(CreateExtensionRequestDto request, CancellationToken cancellationToken = default);
    Task SubmitRatingAsync(CreateRatingRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LocationPoint>> GetLocationHistoryAsync(int rentalId, CancellationToken cancellationToken = default);
}
