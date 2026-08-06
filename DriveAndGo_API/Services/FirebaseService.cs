using FirebaseAdmin;
using FirebaseAdmin.Auth;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace DriveAndGo_API.Services;

// ─────────────────────────────────────────────────────────────
//  Interface
// ─────────────────────────────────────────────────────────────
public interface IFirebaseService
{
    /// <summary>Verify a Firebase ID token from the Flutter mobile app.</summary>
    Task<FirebaseToken?> VerifyIdTokenAsync(string idToken);

    /// <summary>Send an FCM push notification to a single device token.</summary>
    Task<string?> SendPushNotificationAsync(
        string deviceToken,
        string title,
        string body,
        Dictionary<string, string>? data = null);

    /// <summary>Send an FCM push notification to a topic (e.g., "drivers", "customers").</summary>
    Task<string?> SendTopicNotificationAsync(
        string topic,
        string title,
        string body,
        Dictionary<string, string>? data = null);
}

// ─────────────────────────────────────────────────────────────
//  Implementation
// ─────────────────────────────────────────────────────────────
public class FirebaseService : IFirebaseService
{
    private readonly ILogger<FirebaseService> _logger;
    private readonly IConfiguration _configuration;

    public FirebaseService(ILogger<FirebaseService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    // ── Verify Firebase ID Token (Flutter → API) ──────────────
    public async Task<FirebaseToken?> VerifyIdTokenAsync(string idToken)
    {
        try
        {
            EnsureFirebaseInitialized();
            var decoded = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(idToken);
            return decoded;
        }
        catch (FirebaseAuthException ex)
        {
            _logger.LogWarning("Firebase token verification failed: {Message}", ex.Message);
            return null;
        }
    }

    // ── Send Push to Single Device ─────────────────────────────
    public async Task<string?> SendPushNotificationAsync(
        string deviceToken,
        string title,
        string body,
        Dictionary<string, string>? data = null)
    {
        try
        {
            EnsureFirebaseInitialized();

            var message = new Message
            {
                Token = deviceToken,
                Notification = new Notification
                {
                    Title = title,
                    Body  = body,
                },
                Data = data ?? new Dictionary<string, string>(),
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        Sound = "default",
                        ClickAction = "FLUTTER_NOTIFICATION_CLICK"
                    }
                },
                Apns = new ApnsConfig
                {
                    Aps = new Aps { Sound = "default" }
                }
            };

            var messageId = await FirebaseMessaging.DefaultInstance.SendAsync(message);
            _logger.LogInformation("FCM sent. MessageId: {MessageId}", messageId);
            return messageId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FCM send failed for token {Token}", deviceToken);
            return null;
        }
    }

    // ── Send Push to Topic ────────────────────────────────────
    public async Task<string?> SendTopicNotificationAsync(
        string topic,
        string title,
        string body,
        Dictionary<string, string>? data = null)
    {
        try
        {
            EnsureFirebaseInitialized();

            var message = new Message
            {
                Topic = topic,
                Notification = new Notification { Title = title, Body = body },
                Data = data ?? new Dictionary<string, string>()
            };

            var messageId = await FirebaseMessaging.DefaultInstance.SendAsync(message);
            _logger.LogInformation("FCM topic '{Topic}' sent. MessageId: {MessageId}", topic, messageId);
            return messageId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FCM topic send failed for topic {Topic}", topic);
            return null;
        }
    }

    // ── Lazy Initialization ───────────────────────────────────
    private void EnsureFirebaseInitialized()
    {
        if (FirebaseApp.DefaultInstance != null) return;

        var credentialPath = Environment.GetEnvironmentVariable("FIREBASE_CREDENTIAL_PATH")
            ?? _configuration["Firebase:CredentialPath"];

        GoogleCredential credential;

        if (!string.IsNullOrWhiteSpace(credentialPath) && File.Exists(credentialPath))
        {
            // Use downloaded service account JSON (production & CI)
            credential = GoogleCredential.FromFile(credentialPath);
        }
        else
        {
            // Fall back to Application Default Credentials (Cloud Run, GCP, etc.)
            credential = GoogleCredential.GetApplicationDefault();
        }

        var projectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID")
            ?? _configuration["Firebase:ProjectId"];

        FirebaseApp.Create(new AppOptions
        {
            Credential = credential,
            ProjectId  = projectId
        });
    }
}
