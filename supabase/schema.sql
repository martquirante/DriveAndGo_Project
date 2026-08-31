-- ===================================================================
-- DRIVE&GO ULTIMATE MASTER DATABASE & STORAGE SCHEMA FOR SUPABASE
-- ===================================================================
-- Run this directly in: Supabase Dashboard -> SQL Editor -> "+ New query"
-- Safe for pre-existing databases: Upgrades existing columns and tables!
-- ===================================================================

-- ───────────────────────────────────────────────────────────────────
-- 1. UPGRADE EXISTING TABLES (ADD MISSING COLUMNS IF ALREADY EXIST)
-- ───────────────────────────────────────────────────────────────────

-- Blockchain Ledger Upgrade (Fixes error: column "action_type" does not exist)
CREATE TABLE IF NOT EXISTS blockchain_ledger (
    block_index SERIAL PRIMARY KEY,
    rental_id INT,
    action_type VARCHAR(50) NOT NULL DEFAULT 'CONTRACT_SEALED',
    block_hash TEXT NOT NULL,
    previous_hash TEXT NOT NULL DEFAULT '0',
    contract_data JSONB NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);
ALTER TABLE blockchain_ledger ADD COLUMN IF NOT EXISTS action_type VARCHAR(50) NOT NULL DEFAULT 'CONTRACT_SEALED';
ALTER TABLE blockchain_ledger ADD COLUMN IF NOT EXISTS previous_hash TEXT NOT NULL DEFAULT '0';
ALTER TABLE blockchain_ledger ADD COLUMN IF NOT EXISTS contract_data JSONB;

-- Users Table Upgrade
CREATE TABLE IF NOT EXISTS users (
    user_id SERIAL PRIMARY KEY,
    full_name VARCHAR(150) NOT NULL,
    email VARCHAR(200) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL
);
ALTER TABLE users ADD COLUMN IF NOT EXISTS phone VARCHAR(30) DEFAULT '';
ALTER TABLE users ADD COLUMN IF NOT EXISTS role VARCHAR(30) DEFAULT 'customer';
ALTER TABLE users ADD COLUMN IF NOT EXISTS signature_base64 TEXT;
ALTER TABLE users ADD COLUMN IF NOT EXISTS profile_picture TEXT;
ALTER TABLE users ADD COLUMN IF NOT EXISTS id_photo_url TEXT;
ALTER TABLE users ADD COLUMN IF NOT EXISTS selfie_photo_url TEXT;
ALTER TABLE users ADD COLUMN IF NOT EXISTS secondary_id_url TEXT;
ALTER TABLE users ADD COLUMN IF NOT EXISTS failed_login_attempts INT DEFAULT 0;
ALTER TABLE users ADD COLUMN IF NOT EXISTS lockout_enabled BOOLEAN DEFAULT FALSE;
ALTER TABLE users ADD COLUMN IF NOT EXISTS lockout_end TIMESTAMP;
ALTER TABLE users ADD COLUMN IF NOT EXISTS two_factor_enabled BOOLEAN DEFAULT FALSE;
ALTER TABLE users ADD COLUMN IF NOT EXISTS login_alerts_enabled BOOLEAN DEFAULT TRUE;
ALTER TABLE users ADD COLUMN IF NOT EXISTS pin_required BOOLEAN DEFAULT FALSE;
ALTER TABLE users ADD COLUMN IF NOT EXISTS firebase_uid VARCHAR(128);
ALTER TABLE users ADD COLUMN IF NOT EXISTS loyalty_points INT NOT NULL DEFAULT 0;
ALTER TABLE users ADD COLUMN IF NOT EXISTS trust_rating NUMERIC(3,2) NOT NULL DEFAULT 5.00;
ALTER TABLE users ADD COLUMN IF NOT EXISTS is_blacklisted BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE users ADD COLUMN IF NOT EXISTS blacklist_reason TEXT;
ALTER TABLE users ADD COLUMN IF NOT EXISTS created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP;

-- Vehicles Table Upgrade
CREATE TABLE IF NOT EXISTS vehicles (
    vehicle_id SERIAL PRIMARY KEY,
    brand VARCHAR(80) NOT NULL,
    model VARCHAR(80) NOT NULL,
    plate_no VARCHAR(30) UNIQUE NOT NULL
);
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS type VARCHAR(50) DEFAULT 'Car';
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS cc INT;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS rate_per_day NUMERIC(10,2) NOT NULL DEFAULT 0.00;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS rate_with_driver NUMERIC(10,2) NOT NULL DEFAULT 0.00;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS status VARCHAR(30) DEFAULT 'available';
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS photo_url TEXT DEFAULT '';
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS photo_urls JSONB DEFAULT '[]'::jsonb;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS description TEXT DEFAULT '';
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS seat_capacity INT DEFAULT 5;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS transmission VARCHAR(30) DEFAULT 'Automatic';
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS model_3d_url TEXT DEFAULT '';
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS latitude DOUBLE PRECISION;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS longitude DOUBLE PRECISION;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS current_speed DOUBLE PRECISION DEFAULT 0;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS last_update TIMESTAMP WITH TIME ZONE;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS in_garage BOOLEAN DEFAULT TRUE;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS fuel_level_pct INT NOT NULL DEFAULT 100;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS odometer_km INT NOT NULL DEFAULT 0;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS health_score INT NOT NULL DEFAULT 98;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS engine_status VARCHAR(20) NOT NULL DEFAULT 'off';
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS maintenance_due_km INT NOT NULL DEFAULT 5000;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS telematics_locked BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS lto_expiry_date TIMESTAMPTZ;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS insurance_expiry_date TIMESTAMPTZ;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS safety_score INT NOT NULL DEFAULT 95;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS idle_minutes INT NOT NULL DEFAULT 0;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS rfid_balance_autosweep NUMERIC(10,2) NOT NULL DEFAULT 500.00;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS rfid_balance_easytrip NUMERIC(10,2) NOT NULL DEFAULT 500.00;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS expressway_rfid_balance NUMERIC(10,2) NOT NULL DEFAULT 500.00;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS or_cr_url TEXT NOT NULL DEFAULT '';
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS insurance_url TEXT NOT NULL DEFAULT '';
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS color VARCHAR(50) NOT NULL DEFAULT 'Pearl White';
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS flood_risk_status VARCHAR(50) NOT NULL DEFAULT 'safe';
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS engine_water_ingress_alert BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS last_weather_temp NUMERIC(5,2) NOT NULL DEFAULT 28.5;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS current_odometer NUMERIC DEFAULT 0;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS last_maintenance_odometer NUMERIC DEFAULT 0;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS purchase_cost NUMERIC(12,2) NOT NULL DEFAULT 0.00;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS depreciation_rate NUMERIC(5,2) NOT NULL DEFAULT 0.10;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS last_oil_change_km INT NOT NULL DEFAULT 0;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS next_registration_due DATE;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS fuel_level NUMERIC(5,2) NOT NULL DEFAULT 100.00;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP;

-- Drivers Table Upgrade
CREATE TABLE IF NOT EXISTS drivers (
    driver_id SERIAL PRIMARY KEY,
    user_id INT NOT NULL,
    license_no VARCHAR(50) NOT NULL
);
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS license_photo_url TEXT;
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS status VARCHAR(30) DEFAULT 'available';
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS rating_avg NUMERIC(3,2) DEFAULT 5.00;
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS total_trips INT DEFAULT 0;
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS current_lat DOUBLE PRECISION;
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS current_lng DOUBLE PRECISION;
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS assigned_vehicle_id INT;
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS license_expiry DATE;
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS license_class VARCHAR(50);
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS restrictions VARCHAR(100);
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS conditions VARCHAR(100);
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS blood_type VARCHAR(10);
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS birth_date DATE;
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS address TEXT;
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS nationality VARCHAR(50) NOT NULL DEFAULT 'Filipino';
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS sex VARCHAR(10);
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS weight_kg VARCHAR(20);
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS height_m VARCHAR(20);
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS eye_color VARCHAR(30);
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS nbi_expiry DATE;
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS police_expiry DATE;
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS drug_test_expiry DATE;
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS medical_expiry DATE;
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS shift_schedule VARCHAR(50) NOT NULL DEFAULT 'Morning Shift';
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS skill_flags TEXT;
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS cash_on_hand NUMERIC(10,2) NOT NULL DEFAULT 0;
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS verification_status VARCHAR(30) NOT NULL DEFAULT 'unverified';
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS rejection_reason TEXT;

-- Rentals Table Upgrade
CREATE TABLE IF NOT EXISTS rentals (
    rental_id SERIAL PRIMARY KEY,
    customer_id INT NOT NULL,
    vehicle_id INT NOT NULL,
    start_date TIMESTAMPTZ NOT NULL,
    end_date TIMESTAMPTZ NOT NULL
);
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS driver_id INT;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS destination TEXT;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS status VARCHAR(30) DEFAULT 'pending';
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS total_amount NUMERIC(10,2) NOT NULL DEFAULT 0.00;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS payment_method VARCHAR(30) DEFAULT 'cash';
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS payment_status VARCHAR(30) DEFAULT 'unpaid';
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS qr_code TEXT;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS penalty_fee NUMERIC(10,2) DEFAULT 0;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS damage_fee NUMERIC(10,2) DEFAULT 0;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS return_date TIMESTAMPTZ;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS return_odometer NUMERIC;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS return_fuel_level VARCHAR(30);
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS return_notes TEXT;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS return_photos JSONB DEFAULT '[]'::jsonb;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS start_odometer NUMERIC DEFAULT 0;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS blockchain_hash TEXT;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS security_deposit NUMERIC(10,2) NOT NULL DEFAULT 0.00;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS late_penalty_amount NUMERIC(10,2) NOT NULL DEFAULT 0.00;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS fuel_level_start NUMERIC(5,2) NOT NULL DEFAULT 100.00;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS fuel_level_end NUMERIC(5,2);
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS odometer_start INT NOT NULL DEFAULT 0;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS odometer_end INT;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS is_sos_active BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS mileage_limit_km INT;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP;

-- ───────────────────────────────────────────────────────────────────
-- 2. CREATE ALL APPLICATION TABLES
-- ───────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS transactions (
    transaction_id SERIAL PRIMARY KEY,
    rental_id INT REFERENCES rentals(rental_id) ON DELETE SET NULL,
    amount NUMERIC(10,2) NOT NULL DEFAULT 0,
    type VARCHAR(30) DEFAULT 'payment',
    method VARCHAR(30) DEFAULT 'cash',
    proof_url TEXT,
    status VARCHAR(30) DEFAULT 'verified',
    paid_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS add_ons (
    add_on_id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    daily_rate NUMERIC(10,2) NOT NULL DEFAULT 0.00,
    flat_rate NUMERIC(10,2) NOT NULL DEFAULT 0.00,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);
ALTER TABLE add_ons ADD COLUMN IF NOT EXISTS description TEXT;
ALTER TABLE add_ons ADD COLUMN IF NOT EXISTS is_active BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE add_ons ADD COLUMN IF NOT EXISTS daily_rate NUMERIC(10,2) NOT NULL DEFAULT 0.00;
ALTER TABLE add_ons ADD COLUMN IF NOT EXISTS flat_rate NUMERIC(10,2) NOT NULL DEFAULT 0.00;

CREATE TABLE IF NOT EXISTS rental_add_ons (
    rental_id INT NOT NULL REFERENCES rentals(rental_id) ON DELETE CASCADE,
    add_on_id INT NOT NULL REFERENCES add_ons(add_on_id) ON DELETE CASCADE,
    quantity INT NOT NULL DEFAULT 1,
    subtotal NUMERIC(10,2) DEFAULT 0,
    PRIMARY KEY (rental_id, add_on_id)
);

CREATE TABLE IF NOT EXISTS toll_logs (
    toll_log_id SERIAL PRIMARY KEY,
    rental_id INT NOT NULL REFERENCES rentals(rental_id) ON DELETE CASCADE,
    toll_name VARCHAR(100) NOT NULL DEFAULT 'Unknown Toll',
    toll_amount NUMERIC(10,2) NOT NULL,
    location VARCHAR(150),
    logged_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS split_payments (
    split_payment_id SERIAL PRIMARY KEY,
    rental_id INT NOT NULL REFERENCES rentals(rental_id) ON DELETE CASCADE,
    participant_email VARCHAR(150) NOT NULL,
    share_amount NUMERIC(10,2) NOT NULL,
    payment_status VARCHAR(30) NOT NULL DEFAULT 'pending',
    paid_at TIMESTAMP WITH TIME ZONE
);

CREATE TABLE IF NOT EXISTS driver_bids (
    bid_id SERIAL PRIMARY KEY,
    rental_id INT NOT NULL REFERENCES rentals(rental_id) ON DELETE CASCADE,
    driver_id INT NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    status VARCHAR(30) NOT NULL DEFAULT 'pending',
    bid_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS notifications (
    notif_id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    title VARCHAR(200) NOT NULL,
    body TEXT NOT NULL,
    type VARCHAR(30) DEFAULT 'general',
    is_read BOOLEAN NOT NULL DEFAULT FALSE,
    sent_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS extensions (
    extension_id SERIAL PRIMARY KEY,
    rental_id INT NOT NULL REFERENCES rentals(rental_id) ON DELETE CASCADE,
    added_days INT NOT NULL DEFAULT 1,
    added_fee NUMERIC(10,2) NOT NULL DEFAULT 0.00,
    status VARCHAR(20) DEFAULT 'pending',
    requested_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS issues (
    issue_id SERIAL PRIMARY KEY,
    rental_id INT NOT NULL REFERENCES rentals(rental_id) ON DELETE CASCADE,
    reporter_id INT NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    issue_type VARCHAR(50) DEFAULT 'General',
    description TEXT NOT NULL,
    image_url TEXT,
    status VARCHAR(20) DEFAULT 'Pending',
    reported_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS ratings (
    rating_id SERIAL PRIMARY KEY,
    rental_id INT NOT NULL REFERENCES rentals(rental_id) ON DELETE CASCADE,
    customer_id INT NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    driver_id INT REFERENCES drivers(driver_id) ON DELETE SET NULL,
    vehicle_id INT NOT NULL REFERENCES vehicles(vehicle_id) ON DELETE CASCADE,
    driver_score NUMERIC(3,2),
    vehicle_score NUMERIC(3,2) NOT NULL,
    comment TEXT,
    rated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS location_logs (
    log_id BIGSERIAL PRIMARY KEY,
    vehicle_id INT NOT NULL REFERENCES vehicles(vehicle_id) ON DELETE CASCADE,
    rental_id INT REFERENCES rentals(rental_id) ON DELETE SET NULL,
    latitude NUMERIC(10,7) NOT NULL,
    longitude NUMERIC(10,7) NOT NULL,
    speed_kmh NUMERIC(6,2),
    speed_kph NUMERIC(6,2),
    logged_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS gps_logs (
    log_id SERIAL PRIMARY KEY,
    rental_id INT NOT NULL REFERENCES rentals(rental_id) ON DELETE CASCADE,
    latitude NUMERIC(10,7) NOT NULL,
    longitude NUMERIC(10,7) NOT NULL,
    odometer_km NUMERIC(8,2),
    logged_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS fuel_logs (
    fuel_log_id SERIAL PRIMARY KEY,
    vehicle_id INT NOT NULL REFERENCES vehicles(vehicle_id) ON DELETE CASCADE,
    rental_id INT REFERENCES rentals(rental_id) ON DELETE SET NULL,
    fuel_qty_liters DECIMAL(10, 2) NOT NULL DEFAULT 0,
    cost DECIMAL(10, 2) NOT NULL DEFAULT 0,
    current_odometer DECIMAL(10, 2) NOT NULL DEFAULT 0,
    logged_date TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS vehicle_maintenance (
    maintenance_id SERIAL PRIMARY KEY,
    vehicle_id INT NOT NULL REFERENCES vehicles(vehicle_id) ON DELETE CASCADE,
    type VARCHAR(50) NOT NULL,
    cost DECIMAL(10, 2) NOT NULL DEFAULT 0,
    status VARCHAR(30) NOT NULL DEFAULT 'scheduled',
    scheduled_date TIMESTAMP NOT NULL,
    completed_date TIMESTAMP NULL
);

CREATE TABLE IF NOT EXISTS maintenance_logs (
    log_id SERIAL PRIMARY KEY,
    vehicle_id INT NOT NULL REFERENCES vehicles(vehicle_id) ON DELETE CASCADE,
    maintenance_type VARCHAR(100) NOT NULL,
    status VARCHAR(30) NOT NULL DEFAULT 'scheduled',
    scheduled_at DATE,
    completed_at DATE,
    cost NUMERIC(10,2) DEFAULT 0.00,
    notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS promo_codes (
    promo_id SERIAL PRIMARY KEY,
    code VARCHAR(50) NOT NULL UNIQUE,
    discount_percentage DECIMAL(5, 2) NOT NULL DEFAULT 0,
    max_discount_amount DECIMAL(10, 2) NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    expiry_date TIMESTAMP NOT NULL DEFAULT (NOW() + INTERVAL '90 days')
);

CREATE TABLE IF NOT EXISTS expenses (
    expense_id SERIAL PRIMARY KEY,
    vehicle_id INT REFERENCES vehicles(vehicle_id) ON DELETE SET NULL,
    rental_id INT REFERENCES rentals(rental_id) ON DELETE SET NULL,
    category VARCHAR(50) NOT NULL,
    amount NUMERIC(10,2) NOT NULL,
    description TEXT,
    receipt_url TEXT,
    ocr_raw_text TEXT,
    logged_by INT REFERENCES users(user_id) ON DELETE SET NULL,
    status VARCHAR(50) DEFAULT 'Approved',
    expense_date TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS damage_claims (
    claim_id SERIAL PRIMARY KEY,
    rental_id INT REFERENCES rentals(rental_id) ON DELETE CASCADE,
    vehicle_id INT REFERENCES vehicles(vehicle_id) ON DELETE SET NULL,
    damage_severity VARCHAR(20) NOT NULL,
    description TEXT NOT NULL,
    photo_url TEXT,
    photo_urls JSONB DEFAULT '[]'::jsonb,
    liability_cost NUMERIC DEFAULT 0,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);
ALTER TABLE damage_claims ADD COLUMN IF NOT EXISTS photo_urls JSONB DEFAULT '[]'::jsonb;

CREATE TABLE IF NOT EXISTS idempotent_keys (
    key_value VARCHAR(255) PRIMARY KEY,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

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

CREATE TABLE IF NOT EXISTS messages (
    message_id SERIAL PRIMARY KEY,
    rental_id INT NOT NULL REFERENCES rentals(rental_id) ON DELETE CASCADE,
    sender_id INT NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    message_text TEXT,
    media_url TEXT,
    sent_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS chat_messages (
    message_id SERIAL PRIMARY KEY,
    sender_id VARCHAR(100) NOT NULL,
    receiver_id VARCHAR(100) NOT NULL,
    sender_name VARCHAR(150),
    message_body TEXT NOT NULL,
    timestamp TIMESTAMP NOT NULL DEFAULT NOW(),
    is_group_chat BOOLEAN NOT NULL DEFAULT FALSE,
    delivery_status VARCHAR(20) NOT NULL DEFAULT 'sent',
    is_edited BOOLEAN NOT NULL DEFAULT false,
    edit_history JSONB NOT NULL DEFAULT '[]',
    is_unsent BOOLEAN NOT NULL DEFAULT false,
    hidden_for JSONB NOT NULL DEFAULT '[]',
    reactions JSONB NOT NULL DEFAULT '{}',
    media_type VARCHAR(50),
    media_url TEXT,
    media_metadata JSONB
);
ALTER TABLE chat_messages ADD COLUMN IF NOT EXISTS delivery_status VARCHAR(20) NOT NULL DEFAULT 'sent';
ALTER TABLE chat_messages ADD COLUMN IF NOT EXISTS is_edited BOOLEAN NOT NULL DEFAULT false;
ALTER TABLE chat_messages ADD COLUMN IF NOT EXISTS edit_history JSONB NOT NULL DEFAULT '[]';
ALTER TABLE chat_messages ADD COLUMN IF NOT EXISTS is_unsent BOOLEAN NOT NULL DEFAULT false;
ALTER TABLE chat_messages ADD COLUMN IF NOT EXISTS hidden_for JSONB NOT NULL DEFAULT '[]';
ALTER TABLE chat_messages ADD COLUMN IF NOT EXISTS reactions JSONB NOT NULL DEFAULT '{}';
ALTER TABLE chat_messages ADD COLUMN IF NOT EXISTS media_type VARCHAR(50);
ALTER TABLE chat_messages ADD COLUMN IF NOT EXISTS media_url TEXT;
ALTER TABLE chat_messages ADD COLUMN IF NOT EXISTS media_metadata JSONB;

CREATE TABLE IF NOT EXISTS otp_codes (
    otp_id SERIAL PRIMARY KEY,
    email VARCHAR(255) NOT NULL,
    otp_code VARCHAR(10) NOT NULL,
    purpose VARCHAR(50) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMP NOT NULL,
    is_used BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS driver_payout_accounts (
    payout_id SERIAL PRIMARY KEY,
    driver_id INT NOT NULL REFERENCES drivers(driver_id) ON DELETE CASCADE,
    channel VARCHAR(50) NOT NULL DEFAULT 'Cash',
    account_name VARCHAR(150),
    account_no VARCHAR(100),
    is_primary BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS driver_emergency_contacts (
    contact_id SERIAL PRIMARY KEY,
    driver_id INT NOT NULL REFERENCES drivers(driver_id) ON DELETE CASCADE,
    full_name VARCHAR(150) NOT NULL,
    relationship VARCHAR(80),
    phone VARCHAR(50) NOT NULL,
    blood_type VARCHAR(10),
    medical_notes TEXT,
    is_primary BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS driver_documents (
    doc_id SERIAL PRIMARY KEY,
    driver_id INT NOT NULL REFERENCES drivers(driver_id) ON DELETE CASCADE,
    doc_type VARCHAR(80) NOT NULL,
    file_url TEXT,
    expiry_date DATE,
    status VARCHAR(30) NOT NULL DEFAULT 'pending',
    uploaded_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS driver_incidents (
    incident_id SERIAL PRIMARY KEY,
    driver_id INT NOT NULL REFERENCES drivers(driver_id) ON DELETE CASCADE,
    type VARCHAR(30) NOT NULL DEFAULT 'Violation',
    description TEXT NOT NULL,
    incident_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    penalty_amount NUMERIC(10,2) NOT NULL DEFAULT 0,
    status VARCHAR(30) NOT NULL DEFAULT 'open'
);

CREATE TABLE IF NOT EXISTS geofences (
    fence_id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    type VARCHAR(50) NOT NULL,
    geometry_data TEXT NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS geofence_zones (
    zone_id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    center_lat DOUBLE PRECISION NOT NULL,
    center_lng DOUBLE PRECISION NOT NULL,
    radius_km DOUBLE PRECISION NOT NULL DEFAULT 100.0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

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

CREATE TABLE IF NOT EXISTS ai_copilot_sessions (
    session_id SERIAL PRIMARY KEY,
    admin_user_id INT NOT NULL,
    title TEXT NOT NULL DEFAULT 'New Conversation',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS ai_copilot_messages (
    copilot_msg_id BIGSERIAL PRIMARY KEY,
    session_id INT NOT NULL REFERENCES ai_copilot_sessions(session_id) ON DELETE CASCADE,
    sender_id VARCHAR(100) NOT NULL DEFAULT 'bot_copilot',
    llm_role VARCHAR(20) NOT NULL DEFAULT 'user',
    content TEXT NOT NULL,
    ui_component_type VARCHAR(30) NULL,
    ui_payload TEXT NULL,
    tool_name VARCHAR(100) NULL,
    provider_used VARCHAR(50) NULL,
    tokens_used INT NULL,
    sent_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS admin_calendar_notes (
    note_id SERIAL PRIMARY KEY,
    note_date DATE NOT NULL,
    title VARCHAR(150) NOT NULL,
    content TEXT,
    category VARCHAR(50) NOT NULL DEFAULT 'reminder',
    created_by VARCHAR(150) NOT NULL DEFAULT 'Admin',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_calendar_notes_date ON admin_calendar_notes(note_date);

-- ───────────────────────────────────────────────────────────────────
-- 3. PERFORMANCE COMPOSITE INDEXES
-- ───────────────────────────────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_blockchain_rental ON blockchain_ledger(rental_id);
CREATE INDEX IF NOT EXISTS idx_rentals_customer_status ON rentals(customer_id, status);
CREATE INDEX IF NOT EXISTS idx_rentals_vehicle_status ON rentals(vehicle_id, status);
CREATE INDEX IF NOT EXISTS idx_vehicles_status ON vehicles(status);
CREATE INDEX IF NOT EXISTS idx_ai_msgs_session ON ai_copilot_messages(session_id, sent_at ASC);
CREATE INDEX IF NOT EXISTS idx_chat_msgs_status ON chat_messages(receiver_id, delivery_status);
CREATE INDEX IF NOT EXISTS idx_notifs_user ON notifications(user_id, is_read);
CREATE INDEX IF NOT EXISTS ix_payout_driver_id ON driver_payout_accounts(driver_id);
CREATE INDEX IF NOT EXISTS ix_emergency_driver_id ON driver_emergency_contacts(driver_id);
CREATE INDEX IF NOT EXISTS ix_driver_docs_driver_id ON driver_documents(driver_id);
CREATE INDEX IF NOT EXISTS ix_incidents_driver_id ON driver_incidents(driver_id);

-- ───────────────────────────────────────────────────────────────────
-- 4. SUPABASE STORAGE BUCKETS & PUBLIC ACCESS POLICIES
-- ───────────────────────────────────────────────────────────────────
INSERT INTO storage.buckets (id, name, public)
VALUES 
  ('vehicles', 'vehicles', true),
  ('payment-proofs', 'payment-proofs', true),
  ('licenses', 'licenses', true)
ON CONFLICT (id) DO UPDATE SET public = true;

DO $$
BEGIN
    -- Safe recreation of Public Read Policy
    BEGIN
        DROP POLICY IF EXISTS "Public Access for DriveAndGo" ON storage.objects;
        CREATE POLICY "Public Access for DriveAndGo" ON storage.objects
        FOR SELECT USING (bucket_id IN ('vehicles', 'payment-proofs', 'licenses'));
    EXCEPTION WHEN OTHERS THEN
        NULL;
    END;

    -- Safe recreation of Public Upload Policy
    BEGIN
        DROP POLICY IF EXISTS "Public Upload for DriveAndGo" ON storage.objects;
        CREATE POLICY "Public Upload for DriveAndGo" ON storage.objects
        FOR INSERT WITH CHECK (bucket_id IN ('vehicles', 'payment-proofs', 'licenses'));
    EXCEPTION WHEN OTHERS THEN
        NULL;
    END;
END $$;
