using Microsoft.AspNetCore.SignalR;
using Npgsql;
using DriveAndGo_API.Hubs;

namespace DriveAndGo_API.Services;

/// <summary>
/// Writes an in-app notification row directly to the notifications table.
/// Accepts an open NpgsqlConnection (and optional transaction) so it can participate
/// in the caller's transaction scope.
/// </summary>
public class NotificationWriter
{
    private readonly IHubContext<AdminHub> _hubContext;

    public NotificationWriter(IHubContext<AdminHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public void Create(
        NpgsqlConnection connection,
        int userId,
        string title,
        string body,
        string type,
        NpgsqlTransaction? transaction = null)
    {
        using var command = new NpgsqlCommand(
            @"INSERT INTO notifications
                (user_id, title, body, type, is_read, sent_at)
              VALUES
                (@user_id, @title, @body, @type, false, NOW())
              RETURNING notif_id",
            connection,
            transaction);

        command.Parameters.AddWithValue("@user_id", userId);
        command.Parameters.AddWithValue("@title", title);
        command.Parameters.AddWithValue("@body", body);
        command.Parameters.AddWithValue("@type", type);
        
        int newNotifId = Convert.ToInt32(command.ExecuteScalar());

        // Broadcast the notification via SignalR in real-time
        _ = Task.Run(async () =>
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", new
                {
                    notifId = newNotifId,
                    userId = userId,
                    title = title,
                    body = body,
                    type = type,
                    isRead = false,
                    sentAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("SignalR Notification broadcast error: " + ex.Message);
            }
        });
    }
}
