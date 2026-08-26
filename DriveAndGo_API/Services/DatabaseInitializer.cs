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

                // 3. promo_codes table & schema migration
                using (var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS promo_codes (
                        promo_id SERIAL PRIMARY KEY,
                        code VARCHAR(50) NOT NULL UNIQUE,
                        discount_percentage DECIMAL(5, 2) NOT NULL DEFAULT 0,
                        max_discount_amount DECIMAL(10, 2) NOT NULL DEFAULT 0,
                        is_active BOOLEAN NOT NULL DEFAULT TRUE,
                        expiry_date TIMESTAMP NOT NULL DEFAULT (NOW() + INTERVAL '90 days')
                    );

                    ALTER TABLE promo_codes ADD COLUMN IF NOT EXISTS discount_percentage DECIMAL(5, 2) DEFAULT 0;
                    ALTER TABLE promo_codes ADD COLUMN IF NOT EXISTS max_discount_amount DECIMAL(10, 2) DEFAULT 0;
                    ALTER TABLE promo_codes ADD COLUMN IF NOT EXISTS is_active BOOLEAN DEFAULT TRUE;
                    ALTER TABLE promo_codes ADD COLUMN IF NOT EXISTS expiry_date TIMESTAMP DEFAULT (NOW() + INTERVAL '90 days');

                    DO $$
                    BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promo_codes' AND column_name = 'discount_value') THEN
                            ALTER TABLE promo_codes ALTER COLUMN discount_value DROP NOT NULL;
                            UPDATE promo_codes SET discount_percentage = discount_value WHERE (discount_percentage IS NULL OR discount_percentage = 0) AND discount_value > 0;
                        END IF;
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promo_codes' AND column_name = 'discount_type') THEN
                            ALTER TABLE promo_codes ALTER COLUMN discount_type DROP NOT NULL;
                        END IF;
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promo_codes' AND column_name = 'valid_until') THEN
                            ALTER TABLE promo_codes ALTER COLUMN valid_until DROP NOT NULL;
                            UPDATE promo_codes SET expiry_date = valid_until WHERE expiry_date IS NULL;
                        END IF;
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promo_codes' AND column_name = 'valid_from') THEN
                            ALTER TABLE promo_codes ALTER COLUMN valid_from DROP NOT NULL;
                        END IF;
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promo_codes' AND column_name = 'min_rental_days') THEN
                            ALTER TABLE promo_codes ALTER COLUMN min_rental_days DROP NOT NULL;
                        END IF;
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promo_codes' AND column_name = 'max_uses') THEN
                            ALTER TABLE promo_codes ALTER COLUMN max_uses DROP NOT NULL;
                        END IF;
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promo_codes' AND column_name = 'used_count') THEN
                            ALTER TABLE promo_codes ALTER COLUMN used_count DROP NOT NULL;
                        END IF;
                    END $$;

                    -- One-time cleanup of initial demo codes
                    DELETE FROM promo_codes WHERE code IN ('WELCOME10', 'VIP-LUXURY-20', 'WEEKEND-ESCAPE');
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 3b. rentals table column migrations for return workflow
                using (var cmd = new NpgsqlCommand(@"
                    ALTER TABLE rentals ADD COLUMN IF NOT EXISTS return_odometer DECIMAL(10, 2) DEFAULT 0;
                    ALTER TABLE rentals ADD COLUMN IF NOT EXISTS return_fuel_level VARCHAR(30);
                    ALTER TABLE rentals ADD COLUMN IF NOT EXISTS return_notes TEXT;
                    ALTER TABLE rentals ADD COLUMN IF NOT EXISTS penalty_fee DECIMAL(10, 2) DEFAULT 0;
                    ALTER TABLE rentals ADD COLUMN IF NOT EXISTS damage_fee DECIMAL(10, 2) DEFAULT 0;
                    ALTER TABLE rentals ADD COLUMN IF NOT EXISTS start_odometer DECIMAL(10, 2) DEFAULT 0;
                ", conn))
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
                        is_group_chat BOOLEAN NOT NULL DEFAULT FALSE,
                        sender_name VARCHAR(150)
                    );
                    ALTER TABLE chat_messages ADD COLUMN IF NOT EXISTS sender_name VARCHAR(150);
                ", conn))
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
                        admin_name VARCHAR(150),
                        action_type VARCHAR(50) NOT NULL,
                        target_user_id INT,
                        timestamp TIMESTAMP NOT NULL DEFAULT NOW(),
                        ip_address VARCHAR(45) NOT NULL,
                        metadata_json TEXT NOT NULL
                    );
                    ALTER TABLE system_audit_logs ADD COLUMN IF NOT EXISTS admin_name VARCHAR(150);
                ", conn))
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

                // 10. rentals penalty_fee and return inspection columns migration
                using (var cmd = new NpgsqlCommand(@"
                    ALTER TABLE rentals ADD COLUMN IF NOT EXISTS penalty_fee NUMERIC DEFAULT 0;
                    ALTER TABLE rentals ADD COLUMN IF NOT EXISTS return_date TIMESTAMP;
                    ALTER TABLE rentals ADD COLUMN IF NOT EXISTS return_odometer NUMERIC;
                    ALTER TABLE rentals ADD COLUMN IF NOT EXISTS return_fuel_level VARCHAR(20);
                    ALTER TABLE rentals ADD COLUMN IF NOT EXISTS return_notes TEXT;
                    ALTER TABLE rentals ADD COLUMN IF NOT EXISTS damage_fee NUMERIC DEFAULT 0;
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
                    );
                    ALTER TABLE damage_claims ADD COLUMN IF NOT EXISTS photo_urls JSONB DEFAULT '[]'::jsonb;
                    ALTER TABLE rentals ADD COLUMN IF NOT EXISTS return_photos JSONB DEFAULT '[]'::jsonb;
                    ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS photo_urls JSONB DEFAULT '[]'::jsonb;
                ", conn))
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

                // 16. Advanced Messenger Features columns & Media Attachments
                using (var cmd = new NpgsqlCommand(@"
                    ALTER TABLE chat_messages 
                        ADD COLUMN IF NOT EXISTS is_edited BOOLEAN NOT NULL DEFAULT false,
                        ADD COLUMN IF NOT EXISTS edit_history JSONB NOT NULL DEFAULT '[]',
                        ADD COLUMN IF NOT EXISTS is_unsent BOOLEAN NOT NULL DEFAULT false,
                        ADD COLUMN IF NOT EXISTS hidden_for JSONB NOT NULL DEFAULT '[]',
                        ADD COLUMN IF NOT EXISTS reactions JSONB NOT NULL DEFAULT '{}',
                        ADD COLUMN IF NOT EXISTS media_type VARCHAR(50),
                        ADD COLUMN IF NOT EXISTS media_url TEXT,
                        ADD COLUMN IF NOT EXISTS media_metadata JSONB;
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 17. Fleet Telematics & Telemetry columns migration & Real-Life Data Fix
                using (var cmd = new NpgsqlCommand(@"
                    ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS fuel_level_pct         INT             NOT NULL DEFAULT 100;
                    ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS odometer_km             INT             NOT NULL DEFAULT 0;
                    ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS health_score            INT             NOT NULL DEFAULT 98;
                    ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS engine_status           VARCHAR(20)     NOT NULL DEFAULT 'off';
                    ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS maintenance_due_km      INT             NOT NULL DEFAULT 5000;
                    ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS telematics_locked       BOOLEAN         NOT NULL DEFAULT TRUE;
                    ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS lto_expiry_date         TIMESTAMPTZ     NULL;
                    ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS insurance_expiry_date   TIMESTAMPTZ     NULL;
                    ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS safety_score            INT             NOT NULL DEFAULT 95;
                    ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS idle_minutes            INT             NOT NULL DEFAULT 0;
                    ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS rfid_balance_autosweep  NUMERIC(10,2)   NOT NULL DEFAULT 500.00;
                    ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS rfid_balance_easytrip   NUMERIC(10,2)   NOT NULL DEFAULT 500.00;
                    ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS or_cr_url               TEXT            NOT NULL DEFAULT '';
                    ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS color                   VARCHAR(50)     NOT NULL DEFAULT 'Pearl White';
                    ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS flood_risk_status       VARCHAR(50)     NOT NULL DEFAULT 'safe';
                    ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS engine_water_ingress_alert BOOLEAN       NOT NULL DEFAULT FALSE;
                    ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS last_weather_temp       NUMERIC(5,2)    NOT NULL DEFAULT 28.5;

                    -- Populate distinct vehicle colors if unassigned or default
                    UPDATE vehicles SET color = 'Pearl White' WHERE (vehicle_id % 5 = 0) AND (color IS NULL OR color = '' OR color = 'White' OR color = 'Pearl White');
                    UPDATE vehicles SET color = 'Absolute Black' WHERE (vehicle_id % 5 = 1) AND (color IS NULL OR color = '' OR color = 'White' OR color = 'Pearl White');
                    UPDATE vehicles SET color = 'Crimson Red' WHERE (vehicle_id % 5 = 2) AND (color IS NULL OR color = '' OR color = 'White' OR color = 'Pearl White');
                    UPDATE vehicles SET color = 'Agate Black' WHERE (vehicle_id % 5 = 3) AND (color IS NULL OR color = '' OR color = 'White' OR color = 'Pearl White');
                    UPDATE vehicles SET color = 'Metropolitan Grey' WHERE (vehicle_id % 5 = 4) AND (color IS NULL OR color = '' OR color = 'White' OR color = 'Pearl White');

                    -- Set active realistic future dates for LTO & Insurance if null or expired in past years
                    UPDATE vehicles 
                    SET lto_expiry_date = NOW() + (INTERVAL '1 day' * ((vehicle_id * 37) % 300 + 30))
                    WHERE lto_expiry_date IS NULL OR lto_expiry_date < NOW() - INTERVAL '30 days';

                    UPDATE vehicles 
                    SET insurance_expiry_date = NOW() + (INTERVAL '1 day' * ((vehicle_id * 43) % 360 + 60))
                    WHERE insurance_expiry_date IS NULL OR insurance_expiry_date < NOW() - INTERVAL '30 days';

                    -- Populate realistic telematics metrics if uninitialized
                    UPDATE vehicles
                    SET fuel_level_pct = CASE WHEN fuel_level_pct = 0 THEN ((vehicle_id * 23) % 75 + 25) ELSE fuel_level_pct END,
                        health_score = CASE WHEN health_score = 0 THEN ((vehicle_id * 17) % 30 + 70) ELSE health_score END,
                        odometer_km = CASE WHEN odometer_km = 0 THEN ((vehicle_id * 3421) % 45000 + 5000) ELSE odometer_km END,
                        rfid_balance_autosweep = CASE WHEN rfid_balance_autosweep = 0 THEN 750.00 ELSE rfid_balance_autosweep END,
                        rfid_balance_easytrip = CASE WHEN rfid_balance_easytrip = 0 THEN 600.00 ELSE rfid_balance_easytrip END;

                    -- Clean up duplicate plate numbers in PostgreSQL if any exist from test entries
                    UPDATE vehicles v
                    SET plate_no = v.plate_no || '-' || v.vehicle_id
                    WHERE vehicle_id IN (
                        SELECT vehicle_id FROM (
                            SELECT vehicle_id, ROW_NUMBER() OVER (PARTITION BY REPLACE(LOWER(TRIM(plate_no)), '-', '') ORDER BY vehicle_id ASC) as rnum
                            FROM vehicles
                        ) sub WHERE sub.rnum > 1
                    );
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 18. flood_hazard_zones table
                using (var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS flood_hazard_zones (
                        id SERIAL PRIMARY KEY,
                        zone_name VARCHAR(100) NOT NULL,
                        risk_level VARCHAR(30) NOT NULL DEFAULT 'moderate',
                        water_depth_level VARCHAR(100) NOT NULL DEFAULT 'Tire-Deep Level (25 - 35 cm)',
                        polygon_coordinates_json TEXT NOT NULL,
                        advisory_timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                        recommended_reroute VARCHAR(200) NOT NULL DEFAULT '',
                        is_active BOOLEAN NOT NULL DEFAULT TRUE
                    );

                    -- Migration: Convert legacy database snake_case strings to clear human-readable levels
                    UPDATE flood_hazard_zones SET water_depth_level = 'Tire-Deep Level (25 - 35 cm)' WHERE LOWER(water_depth_level) = 'tire_deep';
                    UPDATE flood_hazard_zones SET water_depth_level = 'Waist-Deep Hazard (60 - 80 cm)' WHERE LOWER(water_depth_level) = 'waist_deep';

                    INSERT INTO flood_hazard_zones (zone_name, risk_level, water_depth_level, polygon_coordinates_json, recommended_reroute, is_active)
                    SELECT 'España Blvd - UST Corridor', 'moderate', 'Tire-Deep Level (25 - 35 cm)', '[[14.6080,120.9880],[14.6120,120.9930],[14.6100,120.9960],[14.6060,120.9910]]', 'Reroute via Lacson Ave or Boulevard Bypass', true
                    WHERE NOT EXISTS (SELECT 1 FROM flood_hazard_zones WHERE zone_name = 'España Blvd - UST Corridor');

                    INSERT INTO flood_hazard_zones (zone_name, risk_level, water_depth_level, polygon_coordinates_json, recommended_reroute, is_active)
                    SELECT 'Araneta Avenue Underpass', 'impassable', 'Waist-Deep Hazard (60 - 80 cm)', '[[14.6200,121.0100],[14.6250,121.0150],[14.6220,121.0200],[14.6170,121.0140]]', 'Reroute via E. Rodriguez Sr. Ave or A. Bonifacio', true
                    WHERE NOT EXISTS (SELECT 1 FROM flood_hazard_zones WHERE zone_name = 'Araneta Avenue Underpass');

                    INSERT INTO flood_hazard_zones (zone_name, risk_level, water_depth_level, polygon_coordinates_json, recommended_reroute, is_active)
                    SELECT 'R-10 Navotas Coastal Slipway', 'severe', 'Waist-Deep Hazard (60 - 80 cm)', '[[14.6500,120.9400],[14.6550,120.9480],[14.6510,120.9530],[14.6460,120.9450]]', 'Reroute via Circumferential Road 4 (C4)', true
                    WHERE NOT EXISTS (SELECT 1 FROM flood_hazard_zones WHERE zone_name = 'R-10 Navotas Coastal Slipway');

                    INSERT INTO flood_hazard_zones (zone_name, risk_level, water_depth_level, polygon_coordinates_json, recommended_reroute, is_active)
                    SELECT 'Marikina Riverbank Inundation Area', 'severe', 'Waist-Deep Hazard (60 - 80 cm)', '[[14.6300,121.0900],[14.6350,121.0980],[14.6310,121.1030],[14.6260,121.0950]]', 'Reroute via Marcos Highway or Sumulong Highway', true
                    WHERE NOT EXISTS (SELECT 1 FROM flood_hazard_zones WHERE zone_name = 'Marikina Riverbank Inundation Area');
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 19. Promote rayquirante@gmail.com to admin role
                using (var cmd = new NpgsqlCommand(@"
                    UPDATE users SET role = 'admin' WHERE LOWER(email) = 'rayquirante@gmail.com';
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 20. Driver Management Enterprise Schema — Extended driver fields
                using (var cmd = new NpgsqlCommand(@"
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS license_expiry        DATE;
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS license_class         VARCHAR(50);
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS restrictions          VARCHAR(100);
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS conditions            VARCHAR(100);
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS blood_type            VARCHAR(10);
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS birth_date            DATE;
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS address               TEXT;
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS nationality           VARCHAR(50) NOT NULL DEFAULT 'Filipino';
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS sex                   VARCHAR(10);
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS weight_kg             VARCHAR(20);
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS height_m              VARCHAR(20);
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS eye_color             VARCHAR(30);
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS nbi_expiry            DATE;
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS police_expiry         DATE;
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS drug_test_expiry      DATE;
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS medical_expiry        DATE;
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS shift_schedule        VARCHAR(50) NOT NULL DEFAULT 'Morning Shift';
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS skill_flags           TEXT;
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS cash_on_hand          NUMERIC(10,2) NOT NULL DEFAULT 0;
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS verification_status   VARCHAR(30) NOT NULL DEFAULT 'unverified';
                    ALTER TABLE drivers ADD COLUMN IF NOT EXISTS rejection_reason      TEXT;
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 20a. Users avatar/selfie columns
                using (var cmd = new NpgsqlCommand(@"
                    ALTER TABLE users ADD COLUMN IF NOT EXISTS id_photo_url       TEXT;
                    ALTER TABLE users ADD COLUMN IF NOT EXISTS selfie_photo_url   TEXT;
                    ALTER TABLE users ADD COLUMN IF NOT EXISTS secondary_id_url   TEXT;
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 21. driver_payout_accounts — normalized payout channels (1-to-Many)
                using (var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS driver_payout_accounts (
                        payout_id    SERIAL PRIMARY KEY,
                        driver_id    INT NOT NULL REFERENCES drivers(driver_id) ON DELETE CASCADE,
                        channel      VARCHAR(50) NOT NULL DEFAULT 'Cash',
                        account_name VARCHAR(150),
                        account_no   VARCHAR(100),
                        is_primary   BOOLEAN NOT NULL DEFAULT FALSE,
                        created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
                    );
                    CREATE INDEX IF NOT EXISTS ix_payout_driver_id ON driver_payout_accounts(driver_id);
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 22. driver_emergency_contacts — normalized emergency contacts (1-to-Many)
                using (var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS driver_emergency_contacts (
                        contact_id    SERIAL PRIMARY KEY,
                        driver_id     INT NOT NULL REFERENCES drivers(driver_id) ON DELETE CASCADE,
                        full_name     VARCHAR(150) NOT NULL,
                        relationship  VARCHAR(80),
                        phone         VARCHAR(50) NOT NULL,
                        blood_type    VARCHAR(10),
                        medical_notes TEXT,
                        is_primary    BOOLEAN NOT NULL DEFAULT FALSE,
                        created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
                    );
                    CREATE INDEX IF NOT EXISTS ix_emergency_driver_id ON driver_emergency_contacts(driver_id);
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 23. driver_documents — compliance document vault (1-to-Many)
                using (var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS driver_documents (
                        doc_id      SERIAL PRIMARY KEY,
                        driver_id   INT NOT NULL REFERENCES drivers(driver_id) ON DELETE CASCADE,
                        doc_type    VARCHAR(80) NOT NULL,
                        file_url    TEXT,
                        expiry_date DATE,
                        status      VARCHAR(30) NOT NULL DEFAULT 'pending',
                        uploaded_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
                    );
                    CREATE INDEX IF NOT EXISTS ix_driver_docs_driver_id ON driver_documents(driver_id);
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 24. driver_incidents — violation & commendation history (1-to-Many)
                using (var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS driver_incidents (
                        incident_id    SERIAL PRIMARY KEY,
                        driver_id      INT NOT NULL REFERENCES drivers(driver_id) ON DELETE CASCADE,
                        type           VARCHAR(30) NOT NULL DEFAULT 'Violation',
                        description    TEXT NOT NULL,
                        incident_date  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                        penalty_amount NUMERIC(10,2) NOT NULL DEFAULT 0,
                        status         VARCHAR(30) NOT NULL DEFAULT 'open'
                    );
                    CREATE INDEX IF NOT EXISTS ix_incidents_driver_id ON driver_incidents(driver_id);
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 25. Reconcile Fleet & Driver Statuses with live active Rentals
                using (var cmd = new NpgsqlCommand(@"

                    -- Set vehicles to 'rented' if they have an active/ongoing/overdue rental
                    UPDATE vehicles
                    SET status = 'rented'
                    WHERE vehicle_id IN (
                        SELECT DISTINCT vehicle_id 
                        FROM rentals 
                        WHERE LOWER(status) IN ('approved', 'active', 'in-use', 'ongoing', 'rented', 'overdue')
                    )
                    AND LOWER(status) NOT IN ('maintenance', 'repair');

                    -- Set vehicles to 'available' if they have NO active/ongoing/overdue rental
                    UPDATE vehicles
                    SET status = 'available'
                    WHERE vehicle_id NOT IN (
                        SELECT DISTINCT vehicle_id 
                        FROM rentals 
                        WHERE LOWER(status) IN ('approved', 'active', 'in-use', 'ongoing', 'rented', 'overdue')
                    )
                    AND LOWER(status) NOT IN ('maintenance', 'repair');

                    -- Set drivers to 'assigned' if assigned to active/ongoing/overdue rental
                    UPDATE drivers
                    SET status = 'assigned'
                    WHERE driver_id IN (
                        SELECT DISTINCT driver_id 
                        FROM rentals 
                        WHERE driver_id IS NOT NULL 
                          AND LOWER(status) IN ('approved', 'active', 'in-use', 'ongoing', 'rented', 'overdue')
                    )
                    AND LOWER(status) NOT IN ('suspended', 'inactive', 'on-leave');

                    -- Set drivers to 'available' if they have NO active/ongoing/overdue rental
                    UPDATE drivers
                    SET status = 'available'
                    WHERE driver_id NOT IN (
                        SELECT DISTINCT driver_id 
                        FROM rentals 
                        WHERE driver_id IS NOT NULL 
                          AND LOWER(status) IN ('approved', 'active', 'in-use', 'ongoing', 'rented', 'overdue')
                    )
                    AND LOWER(status) NOT IN ('suspended', 'inactive', 'on-leave');
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                Console.WriteLine("Database tables initialized and fleet/driver statuses reconciled successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database initialization error: {ex.Message}");
            }

        }
    }
}
