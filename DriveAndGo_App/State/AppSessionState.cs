using DriveAndGo_App.Models;
using System.Text.Json;

namespace DriveAndGo_App.State;

public sealed class AppSessionState
{
    private const string SessionKey = "driveandgo.current-user";
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private bool _isInitialized;

    public SessionUser? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;
    public bool IsDriver => CurrentUser?.IsDriver == true;

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        var payload = Preferences.Default.Get(SessionKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(payload))
        {
            CurrentUser = JsonSerializer.Deserialize<SessionUser>(payload, _serializerOptions);
        }

        _isInitialized = true;
        await Task.CompletedTask;
    }

    public async Task SetCurrentUserAsync(SessionUser user)
    {
        CurrentUser = user;
        Preferences.Default.Set(SessionKey, JsonSerializer.Serialize(user, _serializerOptions));
        await Task.CompletedTask;
    }

    public async Task ClearAsync()
    {
        CurrentUser = null;
        Preferences.Default.Remove(SessionKey);
        await Task.CompletedTask;
    }
}
