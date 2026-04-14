using MySql.Data.MySqlClient;

namespace DriveAndGo_API.Services;

public class NotificationWriter
{
    public void Create(
        MySqlConnection connection,
        int userId,
        string title,
        string body,
        string type,
        MySqlTransaction? transaction = null)
    {
        using var command = new MySqlCommand(
            @"INSERT INTO notifications
                (user_id, title, body, type, is_read, sent_at)
              VALUES
                (@user_id, @title, @body, @type, 0, NOW())",
            connection,
            transaction);

        command.Parameters.AddWithValue("@user_id", userId);
        command.Parameters.AddWithValue("@title", title);
        command.Parameters.AddWithValue("@body", body);
        command.Parameters.AddWithValue("@type", type);
        command.ExecuteNonQuery();
    }
}
