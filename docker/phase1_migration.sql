-- ============================================================
-- DriveAndGo Phase 1 Migration Script (Fixed 42P10 constraints)
-- Run against: Local Docker PostgreSQL + Supabase Production
-- Date: 2026-07-09
-- ============================================================

-- ─────────────────────────────────────────────────────────────
-- 1. ADD MISSING COLUMNS TO EXISTING TABLES
-- ─────────────────────────────────────────────────────────────

-- add_ons (fix for pre-existing tables)
ALTER TABLE add_ons ADD COLUMN IF NOT EXISTS description TEXT;
ALTER TABLE add_ons ADD COLUMN IF NOT EXISTS is_active BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE add_ons ADD COLUMN IF NOT EXISTS daily_rate NUMERIC(10,2) NOT NULL DEFAULT 0.00;
ALTER TABLE add_ons ADD COLUMN IF NOT EXISTS flat_rate NUMERIC(10,2) NOT NULL DEFAULT 0.00;

-- users: loyalty_points, trust_rating, selfie_photo_url, secondary_id_url, is_blacklisted, blacklist_reason
ALTER TABLE users ADD COLUMN IF NOT EXISTS loyalty_points INT NOT NULL DEFAULT 0;
ALTER TABLE users ADD COLUMN IF NOT EXISTS trust_rating NUMERIC(3,2) NOT NULL DEFAULT 5.00;
ALTER TABLE users ADD COLUMN IF NOT EXISTS selfie_photo_url TEXT;
ALTER TABLE users ADD COLUMN IF NOT EXISTS secondary_id_url TEXT;
ALTER TABLE users ADD COLUMN IF NOT EXISTS is_blacklisted BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE users ADD COLUMN IF NOT EXISTS blacklist_reason TEXT;

-- vehicles: purchase cost, depreciation for ROI analytics
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS purchase_cost NUMERIC(12,2) NOT NULL DEFAULT 0.00;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS depreciation_rate NUMERIC(5,2) NOT NULL DEFAULT 0.10;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS odometer_km INT NOT NULL DEFAULT 0;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS last_oil_change_km INT NOT NULL DEFAULT 0;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS next_registration_due DATE;
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS fuel_level NUMERIC(5,2) NOT NULL DEFAULT 100.00;

-- drivers: license photo for document vault
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS license_photo_url TEXT;
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS license_expiry DATE;
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS verification_status VARCHAR(30) NOT NULL DEFAULT 'pending';
ALTER TABLE drivers ADD COLUMN IF NOT EXISTS rejection_reason TEXT;

-- rentals: grace period and penalty tracking, add-ons, security deposit
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS security_deposit NUMERIC(10,2) NOT NULL DEFAULT 0.00;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS late_penalty_amount NUMERIC(10,2) NOT NULL DEFAULT 0.00;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS fuel_level_start NUMERIC(5,2) NOT NULL DEFAULT 100.00;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS fuel_level_end NUMERIC(5,2);
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS odometer_start INT NOT NULL DEFAULT 0;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS odometer_end INT;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS is_sos_active BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS mileage_limit_km INT;
ALTER TABLE rentals ADD COLUMN IF NOT EXISTS blockchain_hash TEXT;

-- ─────────────────────────────────────────────────────────────
-- 2. CREATE NEW TABLES
-- ─────────────────────────────────────────────────────────────

-- Trip Add-ons Catalog (Baby Seat, WiFi, Tent, etc.)
CREATE TABLE IF NOT EXISTS add_ons (
    add_on_id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    daily_rate NUMERIC(10,2) NOT NULL DEFAULT 0.00,
    flat_rate NUMERIC(10,2) NOT NULL DEFAULT 0.00,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

-- Add-ons linked to specific rentals
CREATE TABLE IF NOT EXISTS rental_add_ons (
    rental_id INT NOT NULL REFERENCES rentals(rental_id) ON DELETE CASCADE,
    add_on_id INT NOT NULL REFERENCES add_ons(add_on_id),
    quantity INT NOT NULL DEFAULT 1,
    PRIMARY KEY (rental_id, add_on_id)
);

-- Toll Management (NLEX, SLEX, Skyway tracking)
CREATE TABLE IF NOT EXISTS toll_logs (
    toll_log_id SERIAL PRIMARY KEY,
    rental_id INT NOT NULL REFERENCES rentals(rental_id) ON DELETE CASCADE,
    toll_name VARCHAR(100) NOT NULL DEFAULT 'Unknown Toll',
    toll_amount NUMERIC(10,2) NOT NULL,
    location VARCHAR(150),
    logged_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Barkada Mode: Split Payments per booking
CREATE TABLE IF NOT EXISTS split_payments (
    split_payment_id SERIAL PRIMARY KEY,
    rental_id INT NOT NULL REFERENCES rentals(rental_id) ON DELETE CASCADE,
    participant_email VARCHAR(150) NOT NULL,
    share_amount NUMERIC(10,2) NOT NULL,
    payment_status VARCHAR(30) NOT NULL DEFAULT 'pending',
    paid_at TIMESTAMP WITH TIME ZONE
);

-- Driver Matchmaking / Shift Bidding
CREATE TABLE IF NOT EXISTS driver_bids (
    bid_id SERIAL PRIMARY KEY,
    rental_id INT NOT NULL REFERENCES rentals(rental_id) ON DELETE CASCADE,
    driver_id INT NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    status VARCHAR(30) NOT NULL DEFAULT 'pending',
    bid_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Expenses & AI OCR Receipt Logs
CREATE TABLE IF NOT EXISTS expenses (
    expense_id SERIAL PRIMARY KEY,
    vehicle_id INT REFERENCES vehicles(vehicle_id) ON DELETE SET NULL,
    rental_id INT REFERENCES rentals(rental_id) ON DELETE SET NULL,
    amount NUMERIC(10,2) NOT NULL,
    category VARCHAR(50) NOT NULL DEFAULT 'fuel',
    description TEXT,
    receipt_url TEXT,
    ocr_raw_text TEXT,
    logged_by INT REFERENCES users(user_id) ON DELETE SET NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Promo Codes & Discounts Management
CREATE TABLE IF NOT EXISTS promo_codes (
    promo_id SERIAL PRIMARY KEY,
    code VARCHAR(50) UNIQUE NOT NULL,
    discount_type VARCHAR(20) NOT NULL DEFAULT 'percentage',
    discount_value NUMERIC(10,2) NOT NULL,
    min_rental_days INT NOT NULL DEFAULT 1,
    max_uses INT NOT NULL DEFAULT 100,
    used_count INT NOT NULL DEFAULT 0,
    valid_from DATE,
    valid_until DATE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Maintenance Watchdog: Scheduled maintenance events
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

-- Geofence Zones per rental or global
CREATE TABLE IF NOT EXISTS geofence_zones (
    zone_id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    center_lat DOUBLE PRECISION NOT NULL,
    center_lng DOUBLE PRECISION NOT NULL,
    radius_km DOUBLE PRECISION NOT NULL DEFAULT 100.0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

-- GPS location history logs (for offline-first batch sync)
CREATE TABLE IF NOT EXISTS location_logs (
    log_id BIGSERIAL PRIMARY KEY,
    vehicle_id INT NOT NULL REFERENCES vehicles(vehicle_id) ON DELETE CASCADE,
    rental_id INT REFERENCES rentals(rental_id) ON DELETE SET NULL,
    latitude DOUBLE PRECISION NOT NULL,
    longitude DOUBLE PRECISION NOT NULL,
    speed_kph NUMERIC(6,2) DEFAULT 0,
    logged_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Blockchain Ledger Blocks (Tamper-proof contract hashes)
CREATE TABLE IF NOT EXISTS blockchain_ledger (
    block_index SERIAL PRIMARY KEY,
    rental_id INT REFERENCES rentals(rental_id) ON DELETE SET NULL,
    block_hash TEXT NOT NULL,
    previous_hash TEXT NOT NULL DEFAULT '0',
    contract_data JSONB NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- ─────────────────────────────────────────────────────────────
-- 3. SEED DEFAULT CATALOG DATA (Protected with WHERE NOT EXISTS)
-- ─────────────────────────────────────────────────────────────

-- Seeding add_ons
INSERT INTO add_ons (name, description, daily_rate, flat_rate)
SELECT 'Baby Car Seat', 'Standard infant/toddler car seat (rear-facing or forward-facing)', 200.00, 0.00
WHERE NOT EXISTS (SELECT 1 FROM add_ons WHERE name = 'Baby Car Seat');

INSERT INTO add_ons (name, description, daily_rate, flat_rate)
SELECT 'Pocket Wi-Fi', 'Prepaid LTE pocket WiFi device with 5GB daily data', 300.00, 0.00
WHERE NOT EXISTS (SELECT 1 FROM add_ons WHERE name = 'Pocket Wi-Fi');

INSERT INTO add_ons (name, description, daily_rate, flat_rate)
SELECT 'Camping Tent', '2-4 person waterproof camping tent', 500.00, 0.00
WHERE NOT EXISTS (SELECT 1 FROM add_ons WHERE name = 'Camping Tent');

INSERT INTO add_ons (name, description, daily_rate, flat_rate)
SELECT 'Prepaid RFID Load', 'Pre-loaded RFID tag for expressway toll access (NLEX/SLEX)', 0.00, 500.00
WHERE NOT EXISTS (SELECT 1 FROM add_ons WHERE name = 'Prepaid RFID Load');

INSERT INTO add_ons (name, description, daily_rate, flat_rate)
SELECT 'First Aid Kit', 'Complete emergency first aid box', 0.00, 150.00
WHERE NOT EXISTS (SELECT 1 FROM add_ons WHERE name = 'First Aid Kit');

INSERT INTO add_ons (name, description, daily_rate, flat_rate)
SELECT 'Dash Camera', 'Recording dashcam for rental period documentation', 150.00, 0.00
WHERE NOT EXISTS (SELECT 1 FROM add_ons WHERE name = 'Dash Camera');


-- Seeding promo codes
INSERT INTO promo_codes (code, discount_type, discount_value, min_rental_days, max_uses, valid_until)
VALUES
    ('WELCOME500', 'flat', 500.00, 2, 50, '2026-12-31'),
    ('SUMMER20', 'percentage', 20.00, 3, 100, '2026-08-31'),
    ('SUKI10', 'percentage', 10.00, 1, 999, '2027-01-01')
ON CONFLICT (code) DO NOTHING;


-- Seeding geofence zones
INSERT INTO geofence_zones (name, center_lat, center_lng, radius_km)
SELECT 'Metro Manila & Luzon Zone', 14.5995, 120.9842, 250.0
WHERE NOT EXISTS (SELECT 1 FROM geofence_zones WHERE name = 'Metro Manila & Luzon Zone');


-- Seeding genesis blockchain block
INSERT INTO blockchain_ledger (rental_id, block_hash, previous_hash, contract_data)
SELECT NULL, 'GENESIS_BLOCK_DRIVEANDGO_000000000000000000000000000000', '0', '{"type":"genesis","message":"DriveAndGo Blockchain Ledger Initialized"}'
WHERE NOT EXISTS (SELECT 1 FROM blockchain_ledger WHERE block_hash = 'GENESIS_BLOCK_DRIVEANDGO_000000000000000000000000000000');


-- ─────────────────────────────────────────────────────────────
-- 4. CREATE INDEXES FOR PERFORMANCE
-- ─────────────────────────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_toll_logs_rental ON toll_logs(rental_id);
CREATE INDEX IF NOT EXISTS idx_split_payments_rental ON split_payments(rental_id);
CREATE INDEX IF NOT EXISTS idx_location_logs_vehicle ON location_logs(vehicle_id);
CREATE INDEX IF NOT EXISTS idx_location_logs_rental ON location_logs(rental_id);
CREATE INDEX IF NOT EXISTS idx_blockchain_rental ON blockchain_ledger(rental_id);
CREATE INDEX IF NOT EXISTS idx_expenses_vehicle ON expenses(vehicle_id);
CREATE INDEX IF NOT EXISTS idx_maintenance_vehicle ON maintenance_logs(vehicle_id);
CREATE INDEX IF NOT EXISTS idx_driver_bids_rental ON driver_bids(rental_id);
CREATE INDEX IF NOT EXISTS idx_promo_codes_code ON promo_codes(code);

SELECT 'Phase 1 Migration completed successfully!' AS status;
