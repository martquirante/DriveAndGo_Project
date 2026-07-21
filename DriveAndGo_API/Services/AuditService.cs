using Npgsql;
using System.Text.Json;
using System.Threading.Tasks;

namespace DriveAndGo_API.Services
{
    public class AuditService
    {
        private readonly NpgsqlDataSource _ds;

        public AuditService(NpgsqlDataSource ds)
        {
            _ds = ds;
        }

        /// <summary>
        /// Registers an administrative log entry asynchronously inside the system_audit_logs table.
        /// </summary>
        public async Task LogActionAsync(
            int adminUserId,
            string actionType,
            int targetUserId,
            string ipAddress,
            object oldValues,
            object newValues)
        {
            try
            {
                var metadata = new
                {
                    oldValues = oldValues,
                    newValues = newValues
                };
                string metadataJson = JsonSerializer.Serialize(metadata);

                await using var conn = await _ds.OpenConnectionAsync();
                await using var cmd = new NpgsqlCommand(
                    @"INSERT INTO system_audit_logs 
                        (admin_user_id, action_type, target_user_id, timestamp, ip_address, metadata_json)
                      VALUES 
                        (@admin_user_id, @action_type, @target_user_id, NOW(), @ip_address, @metadata_json)", 
                    conn);

                cmd.Parameters.AddWithValue("@admin_user_id", adminUserId == 0 ? DBNull.Value : adminUserId);
                cmd.Parameters.AddWithValue("@action_type", actionType);
                cmd.Parameters.AddWithValue("@target_user_id", targetUserId);
                cmd.Parameters.AddWithValue("@ip_address", string.IsNullOrEmpty(ipAddress) ? "127.0.0.1" : ipAddress);
                cmd.Parameters.AddWithValue("@metadata_json", metadataJson);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                // Graceful fallback to console to prevent blocking critical auth/profile changes if logging fails
                Console.WriteLine("Audit trail logging failed: " + ex.Message);
            }
        }
    }
}
