using Npgsql;

namespace DriveAndGo_API.Services
{
    public static class DatabaseInitializer
    {
        public static void Initialize(string connectionString)
        {
            try
            {
                using var conn = new NpgsqlConnection(connectionString);
                conn.Open();

                // 1. vehicle_maintenance table
                using (var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS vehicle_maintenance (
                        maintenance_id SERIAL PRIMARY KEY,
                        vehicle_id INT NOT NULL,
                        description TEXT NOT NULL,
                        cost DECIMAL(10, 2) NOT NULL DEFAULT 0,
                        status VARCHAR(30) NOT NULL DEFAULT 'scheduled',
                        scheduled_date TIMESTAMP NOT NULL,
                        completed_date TIMESTAMP NULL
                    );", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 2. fuel_logs table
                using (var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS fuel_logs (
                        fuel_log_id SERIAL PRIMARY KEY,
                        vehicle_id INT NOT NULL,
                        rental_id INT NULL,
                        fuel_qty_liters DECIMAL(10, 2) NOT NULL DEFAULT 0,
                        cost DECIMAL(10, 2) NOT NULL DEFAULT 0,
                        current_odometer DECIMAL(10, 2) NOT NULL DEFAULT 0,
                        logged_date TIMESTAMP NOT NULL DEFAULT NOW()
                    );", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 3. promo_codes table
                using (var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS promo_codes (
                        promo_id SERIAL PRIMARY KEY,
                        code VARCHAR(50) NOT NULL UNIQUE,
                        discount_percentage DECIMAL(5, 2) NOT NULL DEFAULT 0,
                        max_discount_amount DECIMAL(10, 2) NOT NULL DEFAULT 0,
                        is_active BOOLEAN NOT NULL DEFAULT TRUE,
                        expiry_date TIMESTAMP NOT NULL
                    );", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 4. chat_messages table
                using (var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS chat_messages (
                        message_id SERIAL PRIMARY KEY,
                        sender_id VARCHAR(100) NOT NULL,
                        receiver_id VARCHAR(100) NOT NULL,
                        message_body TEXT NOT NULL,
                        timestamp TIMESTAMP NOT NULL DEFAULT NOW(),
                        is_group_chat BOOLEAN NOT NULL DEFAULT FALSE
                    );", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 5. Add current_odometer and last_maintenance_odometer to vehicles if they don't exist
                using (var cmd = new NpgsqlCommand(@"
                    ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS current_odometer NUMERIC DEFAULT 0;
                    ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS last_maintenance_odometer NUMERIC DEFAULT 0;
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 6. geofences table
                using (var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS geofences (
                        fence_id SERIAL PRIMARY KEY,
                        name VARCHAR(100) NOT NULL,
                        type VARCHAR(50) NOT NULL,
                        geometry_data TEXT NOT NULL,
                        created_at TIMESTAMP NOT NULL DEFAULT NOW()
                    );", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 7. users lockout & security columns migration
                using (var cmd = new NpgsqlCommand(@"
                    ALTER TABLE users ADD COLUMN IF NOT EXISTS failed_login_attempts INT DEFAULT 0;
                    ALTER TABLE users ADD COLUMN IF NOT EXISTS lockout_enabled BOOLEAN DEFAULT FALSE;
                    ALTER TABLE users ADD COLUMN IF NOT EXISTS lockout_end TIMESTAMP;
                    ALTER TABLE users ADD COLUMN IF NOT EXISTS two_factor_enabled BOOLEAN DEFAULT FALSE;
                    ALTER TABLE users ADD COLUMN IF NOT EXISTS login_alerts_enabled BOOLEAN DEFAULT TRUE;
                    ALTER TABLE users ADD COLUMN IF NOT EXISTS pin_required BOOLEAN DEFAULT FALSE;
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 7b. otp_codes table creation
                using (var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS otp_codes (
                        otp_id SERIAL PRIMARY KEY,
                        email VARCHAR(255) NOT NULL,
                        otp_code VARCHAR(10) NOT NULL,
                        purpose VARCHAR(50) NOT NULL,
                        created_at TIMESTAMP NOT NULL DEFAULT NOW(),
                        expires_at TIMESTAMP NOT NULL,
                        is_used BOOLEAN NOT NULL DEFAULT FALSE
                    );
                    CREATE INDEX IF NOT EXISTS idx_otp_email_purpose ON otp_codes(email, purpose, is_used);
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 8. system_audit_logs table
                using (var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS system_audit_logs (
                        audit_id SERIAL PRIMARY KEY,
                        admin_user_id INT,
                        action_type VARCHAR(50) NOT NULL,
                        target_user_id INT,
                        timestamp TIMESTAMP NOT NULL DEFAULT NOW(),
                        ip_address VARCHAR(45) NOT NULL,
                        metadata_json TEXT NOT NULL
                    );", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 9. expenses status column migration
                using (var cmd = new NpgsqlCommand(@"
                    ALTER TABLE expenses ADD COLUMN IF NOT EXISTS status VARCHAR(50) DEFAULT 'Approved';
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 10. rentals penalty_fee column migration
                using (var cmd = new NpgsqlCommand(@"
                    ALTER TABLE rentals ADD COLUMN IF NOT EXISTS penalty_fee NUMERIC DEFAULT 0;
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 11. idempotent_keys table
                using (var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS idempotent_keys (
                        key_value VARCHAR(255) PRIMARY KEY,
                        created_at TIMESTAMP NOT NULL DEFAULT NOW()
                    );", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 12. damage_claims table
                using (var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS damage_claims (
                        claim_id SERIAL PRIMARY KEY,
                        rental_id INT NOT NULL,
                        damage_severity VARCHAR(20) NOT NULL,
                        description TEXT NOT NULL,
                        photo_url TEXT,
                        liability_cost NUMERIC DEFAULT 0,
                        created_at TIMESTAMP NOT NULL DEFAULT NOW()
                    );", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 13. ai_copilot_sessions table — one row per AI conversation thread
                using (var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS ai_copilot_sessions (
                        session_id    SERIAL PRIMARY KEY,
                        admin_user_id INT NOT NULL,
                        title         TEXT NOT NULL DEFAULT 'New Conversation',
                        created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                        updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
                    );", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 14. ai_copilot_messages table — every turn (system/user/assistant/tool)
                using (var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS ai_copilot_messages (
                        copilot_msg_id    BIGSERIAL PRIMARY KEY,
                        session_id        INT NOT NULL REFERENCES ai_copilot_sessions(session_id) ON DELETE CASCADE,
                        sender_id         VARCHAR(100) NOT NULL DEFAULT 'bot_copilot',
                        llm_role          VARCHAR(20)  NOT NULL DEFAULT 'user',
                        content           TEXT         NOT NULL,
                        ui_component_type VARCHAR(30)  NULL,
                        ui_payload        TEXT         NULL,
                        tool_name         VARCHAR(100) NULL,
                        provider_used     VARCHAR(50)  NULL,
                        tokens_used       INT          NULL,
                        sent_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW()
                    );
                    CREATE INDEX IF NOT EXISTS idx_ai_msgs_session
                        ON ai_copilot_messages(session_id, sent_at ASC);", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 15. chat_messages delivery_status column migration
                //     Tracks Messenger-style delivery states: sent → delivered → seen
                using (var cmd = new NpgsqlCommand(@"
                    ALTER TABLE chat_messages
                        ADD COLUMN IF NOT EXISTS delivery_status VARCHAR(20) NOT NULL DEFAULT 'sent';
                    CREATE INDEX IF NOT EXISTS idx_chat_msgs_status
                        ON chat_messages(receiver_id, delivery_status)
                        WHERE delivery_status != 'seen';
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 16. Advanced Messenger Features columns
                using (var cmd = new NpgsqlCommand(@"
                    ALTER TABLE chat_messages 
                        ADD COLUMN IF NOT EXISTS is_edited BOOLEAN NOT NULL DEFAULT false,
                        ADD COLUMN IF NOT EXISTS edit_history JSONB NOT NULL DEFAULT '[]',
                        ADD COLUMN IF NOT EXISTS is_unsent BOOLEAN NOT NULL DEFAULT false,
                        ADD COLUMN IF NOT EXISTS hidden_for JSONB NOT NULL DEFAULT '[]',
                        ADD COLUMN IF NOT EXISTS reactions JSONB NOT NULL DEFAULT '{}';
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 17. Fix legacy sender/receiver IDs for AI Copilot
                using (var cmd = new NpgsqlCommand(@"
                    UPDATE chat_messages SET sender_id = 'admin' WHERE sender_id = '1';
                    UPDATE chat_messages SET receiver_id = 'admin' WHERE receiver_id = '1';
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                Console.WriteLine("Database tables initialized successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database initialization error: {ex.Message}");
            }
        }
    }
}
