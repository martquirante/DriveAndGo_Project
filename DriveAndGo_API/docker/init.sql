-- ============================================================
--  DriveAndGo — PostgreSQL Database Schema
--  Runs automatically when the Docker container starts
--  Compatible with Supabase (PostgreSQL 15/16)
-- ============================================================

-- ─────────────────────────────────────────────────────────────
--  USERS
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS users (
    user_id       SERIAL PRIMARY KEY,
    full_name     VARCHAR(150)        NOT NULL,
    email         VARCHAR(200)        NOT NULL UNIQUE,
    password_hash TEXT                NOT NULL,
    phone         VARCHAR(30)         NOT NULL DEFAULT '',
    role          VARCHAR(20)         NOT NULL DEFAULT 'customer', -- admin, customer, driver
    id_photo_url  TEXT,
    firebase_uid  VARCHAR(128),                                   -- Firebase Auth UID (optional)
    created_at    TIMESTAMPTZ         NOT NULL DEFAULT NOW()
);

-- ─────────────────────────────────────────────────────────────
--  DRIVERS
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS drivers (
    driver_id        SERIAL PRIMARY KEY,
    user_id          INT             NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    license_no       VARCHAR(50)     NOT NULL,
    license_photo_url TEXT,
    status           VARCHAR(20)     NOT NULL DEFAULT 'available',  -- available, on-trip, off-duty, inactive
    rating_avg       NUMERIC(3,2)    DEFAULT 0.0,
    total_trips      INT             NOT NULL DEFAULT 0
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_drivers_user_id ON drivers(user_id);

-- ─────────────────────────────────────────────────────────────
--  VEHICLES
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS vehicles (
    vehicle_id      SERIAL PRIMARY KEY,
    plate_no        VARCHAR(20)     NOT NULL UNIQUE,
    brand           VARCHAR(80)     NOT NULL,
    model           VARCHAR(80)     NOT NULL,
    type            VARCHAR(30)     NOT NULL DEFAULT 'Car',
    cc              INT,
    status          VARCHAR(20)     NOT NULL DEFAULT 'available',  -- available, rented, maintenance, retired
    rate_per_day    NUMERIC(10,2)   NOT NULL DEFAULT 0,
    rate_with_driver NUMERIC(10,2)  NOT NULL DEFAULT 0,
    photo_url       TEXT            NOT NULL DEFAULT '',
    description     TEXT            NOT NULL DEFAULT '',
    seat_capacity   INT             NOT NULL DEFAULT 5,
    transmission    VARCHAR(20)     NOT NULL DEFAULT 'Automatic',
    model_3d_url    TEXT            NOT NULL DEFAULT '',
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    latitude        DOUBLE PRECISION,
    longitude       DOUBLE PRECISION,
    current_speed   INT,
    last_update     TIMESTAMPTZ,
    in_garage       BOOLEAN         NOT NULL DEFAULT TRUE
);

-- ─────────────────────────────────────────────────────────────
--  RENTALS
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS rentals (
    rental_id      SERIAL PRIMARY KEY,
    customer_id    INT             NOT NULL REFERENCES users(user_id),
    vehicle_id     INT             NOT NULL REFERENCES vehicles(vehicle_id),
    driver_id      INT             REFERENCES drivers(driver_id),
    start_date     TIMESTAMPTZ     NOT NULL,
    end_date       TIMESTAMPTZ,
    destination    TEXT,
    status         VARCHAR(20)     NOT NULL DEFAULT 'pending',   -- pending, approved, active, in-use, completed, cancelled, rejected
    total_amount   NUMERIC(10,2)   NOT NULL DEFAULT 0,
    payment_method VARCHAR(20)     NOT NULL DEFAULT 'cash',      -- cash, gcash, maya, bank
    payment_status VARCHAR(20)     NOT NULL DEFAULT 'unpaid',    -- unpaid, paid
    qr_code        TEXT,
    created_at     TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_rentals_customer_id ON rentals(customer_id);
CREATE INDEX IF NOT EXISTS ix_rentals_vehicle_id  ON rentals(vehicle_id);
CREATE INDEX IF NOT EXISTS ix_rentals_status       ON rentals(status);

-- ─────────────────────────────────────────────────────────────
--  TRANSACTIONS
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS transactions (
    transaction_id SERIAL PRIMARY KEY,
    rental_id      INT             NOT NULL REFERENCES rentals(rental_id),
    amount         NUMERIC(10,2)   NOT NULL DEFAULT 0,
    type           VARCHAR(20)     DEFAULT 'payment',            -- payment, refund, extension
    method         VARCHAR(20)     DEFAULT 'cash',               -- cash, gcash, maya, bank
    proof_url      TEXT,
    status         VARCHAR(20)     DEFAULT 'pending',            -- pending, confirmed, rejected, paid, verified
    paid_at        TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_transactions_rental_id ON transactions(rental_id);

-- ─────────────────────────────────────────────────────────────
--  EXTENSIONS (Rental day extensions)
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS extensions (
    extension_id SERIAL PRIMARY KEY,
    rental_id    INT             NOT NULL REFERENCES rentals(rental_id),
    added_days   INT             NOT NULL DEFAULT 1,
    added_fee    NUMERIC(10,2)   NOT NULL DEFAULT 0,
    status       VARCHAR(20)     DEFAULT 'pending',              -- pending, approved, rejected
    requested_at TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- ─────────────────────────────────────────────────────────────
--  ISSUES / INCIDENT REPORTS
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS issues (
    issue_id     SERIAL PRIMARY KEY,
    rental_id    INT             NOT NULL REFERENCES rentals(rental_id),
    reporter_id  INT             NOT NULL REFERENCES users(user_id),
    issue_type   VARCHAR(50)     NOT NULL DEFAULT 'General',
    description  TEXT            NOT NULL,
    image_url    TEXT,
    status       VARCHAR(20)     NOT NULL DEFAULT 'Pending',     -- Pending, In Progress, Resolved
    reported_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- ─────────────────────────────────────────────────────────────
--  RATINGS
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS ratings (
    rating_id     SERIAL PRIMARY KEY,
    rental_id     INT             NOT NULL REFERENCES rentals(rental_id),
    customer_id   INT             NOT NULL REFERENCES users(user_id),
    driver_id     INT             REFERENCES drivers(driver_id),
    vehicle_id    INT             NOT NULL REFERENCES vehicles(vehicle_id),
    driver_score  INT             CHECK (driver_score BETWEEN 1 AND 5),
    vehicle_score INT             NOT NULL CHECK (vehicle_score BETWEEN 1 AND 5),
    comment       TEXT,
    rated_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- ─────────────────────────────────────────────────────────────
--  NOTIFICATIONS
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS notifications (
    notif_id  SERIAL PRIMARY KEY,
    user_id   INT             NOT NULL REFERENCES users(user_id),
    title     VARCHAR(200)    NOT NULL,
    body      TEXT            NOT NULL,
    type      VARCHAR(30),
    is_read   BOOLEAN         NOT NULL DEFAULT FALSE,
    sent_at   TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_notifications_user_id ON notifications(user_id);

-- ─────────────────────────────────────────────────────────────
--  LOCATION LOGS (GPS tracking - stays in PostgreSQL)
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS location_logs (
    log_id     SERIAL PRIMARY KEY,
    rental_id  INT             NOT NULL REFERENCES rentals(rental_id),
    vehicle_id INT             NOT NULL REFERENCES vehicles(vehicle_id),
    latitude   NUMERIC(10,7)   NOT NULL,
    longitude  NUMERIC(10,7)   NOT NULL,
    speed_kmh  NUMERIC(6,2),
    logged_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_location_logs_rental_id  ON location_logs(rental_id);
CREATE INDEX IF NOT EXISTS ix_location_logs_logged_at  ON location_logs(logged_at DESC);

-- ─────────────────────────────────────────────────────────────
--  GPS LOGS (Odometer tracking)
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS gps_logs (
    log_id       SERIAL PRIMARY KEY,
    rental_id    INT             NOT NULL REFERENCES rentals(rental_id),
    latitude     NUMERIC(10,7)   NOT NULL,
    longitude    NUMERIC(10,7)   NOT NULL,
    odometer_km  NUMERIC(8,2),
    logged_at    TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- ─────────────────────────────────────────────────────────────
--  MESSAGES (In-app chat)
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS messages (
    message_id   SERIAL PRIMARY KEY,
    rental_id    INT             NOT NULL REFERENCES rentals(rental_id),
    sender_id    INT             NOT NULL REFERENCES users(user_id),
    message_text TEXT,
    media_url    TEXT,
    sent_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_messages_rental_id ON messages(rental_id);

-- ─────────────────────────────────────────────────────────────
--  SEED DATA FOR LOCAL TESTING
-- ─────────────────────────────────────────────────────────────

-- 1. Default Admin Account (Password: Admin@123)
INSERT INTO users (full_name, email, password_hash, phone, role)
VALUES ('DriveAndGo Admin', 'admin@driveandgo.com', '$2a$11$2LPGl8V1HcMUPXlR3L2sBO5cz1hX6rHHGyCW5zf1K6S0r5Y3nVBmC', '09170000000', 'admin')
ON CONFLICT (email) DO NOTHING;

-- 2. Default Customer Account (Password: Admin@123)
INSERT INTO users (full_name, email, password_hash, phone, role)
VALUES ('Test Customer', 'customer@driveandgo.com', '$2a$11$2LPGl8V1HcMUPXlR3L2sBO5cz1hX6rHHGyCW5zf1K6S0r5Y3nVBmC', '09171111111', 'customer')
ON CONFLICT (email) DO NOTHING;

-- 3. Default Driver Account (Password: Admin@123)
INSERT INTO users (full_name, email, password_hash, phone, role)
VALUES ('Test Driver', 'driver@driveandgo.com', '$2a$11$2LPGl8V1HcMUPXlR3L2sBO5cz1hX6rHHGyCW5zf1K6S0r5Y3nVBmC', '09172222222', 'driver')
ON CONFLICT (email) DO NOTHING;
