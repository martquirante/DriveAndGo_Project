using DriveAndGo_App.Contracts;
using DriveAndGo_App.Dtos;
using DriveAndGo_App.Models;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace DriveAndGo_App.Services.Api;

public sealed class DriveAndGoApiService : IDriveAndGoApiService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public DriveAndGoApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> CheckEmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"auth/check-email?email={Uri.EscapeDataString(email)}", cancellationToken);
        await EnsureSuccessAsync(response);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.TryGetProperty("exists", out var exists) && exists.GetBoolean();
    }

    public Task<SessionUser> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default) =>
        PostForModelAsync<SessionUser>("auth/login", request, cancellationToken);

    public Task<SessionUser> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default) =>
        PostForModelAsync<SessionUser>("auth/register", request, cancellationToken);

    public Task<IReadOnlyList<VehicleItem>> GetAvailableVehiclesAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<VehicleItem>("vehicles/available", cancellationToken);

    public Task<VehicleItem> GetVehicleAsync(int vehicleId, CancellationToken cancellationToken = default) =>
        GetAsync<VehicleItem>($"vehicles/{vehicleId}", cancellationToken);

    public Task<IReadOnlyList<DriverSummary>> GetAvailableDriversAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<DriverSummary>("drivers/available", cancellationToken);

    public Task<int> CreateRentalAsync(CreateRentalRequestDto request, CancellationToken cancellationToken = default) =>
        PostForIdAsync("rentals", request, "rentalId", cancellationToken);

    public Task<IReadOnlyList<RentalItem>> GetCustomerRentalsAsync(int customerId, CancellationToken cancellationToken = default) =>
        GetListAsync<RentalItem>($"rentals/customer/{customerId}", cancellationToken);

    public Task<IReadOnlyList<RentalItem>> GetDriverAssignmentsAsync(int userId, CancellationToken cancellationToken = default) =>
        GetListAsync<RentalItem>($"drivers/assignments/user/{userId}", cancellationToken);

    public Task<RentalItem> GetRentalAsync(int rentalId, CancellationToken cancellationToken = default) =>
        GetAsync<RentalItem>($"rentals/{rentalId}", cancellationToken);

    public async Task CancelRentalAsync(int rentalId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"rentals/{rentalId}/cancel", content: null, cancellationToken);
        await EnsureSuccessAsync(response);
    }

    public Task<IReadOnlyList<TransactionItem>> GetTransactionsByRentalAsync(int rentalId, CancellationToken cancellationToken = default) =>
        GetListAsync<TransactionItem>($"transactions/rental/{rentalId}", cancellationToken);

    public Task<int> SubmitPaymentAsync(CreateTransactionRequestDto request, CancellationToken cancellationToken = default) =>
        PostForIdAsync("transactions", request, "transactionId", cancellationToken);

    public Task<IReadOnlyList<AppNotificationModel>> GetNotificationsAsync(int userId, CancellationToken cancellationToken = default) =>
        GetListAsync<AppNotificationModel>($"notifications/user/{userId}", cancellationToken);

    public async Task MarkNotificationReadAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"notifications/{notificationId}/read", content: null, cancellationToken);
        await EnsureSuccessAsync(response);
    }

    public Task<SessionUser> GetUserProfileAsync(int userId, CancellationToken cancellationToken = default) =>
        GetAsync<SessionUser>($"users/{userId}", cancellationToken);

    public async Task UpdateUserProfileAsync(int userId, UpdateUserProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"users/{userId}", request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response);
    }

    public Task<IReadOnlyList<MessageItem>> GetMessagesAsync(int rentalId, CancellationToken cancellationToken = default) =>
        GetListAsync<MessageItem>($"messages/history/{rentalId}", cancellationToken);

    public async Task SendMessageAsync(SendMessageRequestDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("messages/send", request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response);
    }

    public Task<int> ReportIssueAsync(CreateIssueRequestDto request, CancellationToken cancellationToken = default) =>
        PostForIdAsync("issues/report", request, "issueId", cancellationToken);

    public Task<int> RequestExtensionAsync(CreateExtensionRequestDto request, CancellationToken cancellationToken = default) =>
        PostForIdAsync("extensions", request, "extensionId", cancellationToken);

    public async Task SubmitRatingAsync(CreateRatingRequestDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("ratings", request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response);
    }

    public async Task<IReadOnlyList<LocationPoint>> GetLocationHistoryAsync(int rentalId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"locations/history/{rentalId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return Array.Empty<LocationPoint>();
        }

        await EnsureSuccessAsync(response);
        return await DeserializeListAsync<LocationPoint>(response, cancellationToken);
    }

    private Task<T> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        return SendForModelAsync<T>(() => _httpClient.GetAsync(relativeUrl, cancellationToken), cancellationToken);
    }

    private Task<T> PostForModelAsync<T>(string relativeUrl, object request, CancellationToken cancellationToken)
    {
        return SendForModelAsync<T>(() => _httpClient.PostAsJsonAsync(relativeUrl, request, _jsonOptions, cancellationToken), cancellationToken);
    }

    private async Task<T> SendForModelAsync<T>(Func<Task<HttpResponseMessage>> sender, CancellationToken cancellationToken)
    {
        var response = await sender();
        await EnsureSuccessAsync(response);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<T>(payload, _jsonOptions)
            ?? throw new InvalidOperationException("The server returned an empty response.");
    }

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
        await EnsureSuccessAsync(response);
        return await DeserializeListAsync<T>(response, cancellationToken);
    }

    private async Task<IReadOnlyList<T>> DeserializeListAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<List<T>>(payload, _jsonOptions) ?? new List<T>();
    }

    private async Task<int> PostForIdAsync(string relativeUrl, object request, string propertyName, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(relativeUrl, request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (document.RootElement.TryGetProperty(propertyName, out var idProperty))
        {
            return idProperty.GetInt32();
        }

        throw new InvalidOperationException("The server response did not include the expected identifier.");
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var payload = await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(payload))
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("message", out var message))
            {
                throw new InvalidOperationException(message.GetString() ?? "The server rejected the request.");
            }

            if (document.RootElement.TryGetProperty("Message", out var capitalMessage))
            {
                throw new InvalidOperationException(capitalMessage.GetString() ?? "The server rejected the request.");
            }
        }

        throw new InvalidOperationException($"Request failed with status code {(int)response.StatusCode}.");
    }
}
