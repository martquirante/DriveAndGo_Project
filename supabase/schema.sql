-- ============================================================
-- DriveAndGo Supabase PostgreSQL Database Schema & Seed Data
-- Run this directly in the Supabase Dashboard -> SQL Editor
-- ============================================================

-- 1. USERS TABLE
CREATE TABLE IF NOT EXISTS users (
    user_id SERIAL PRIMARY KEY,
    full_name VARCHAR(150) NOT NULL,
    email VARCHAR(150) UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    phone VARCHAR(30),
    role VARCHAR(30) DEFAULT 'customer',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 2. VEHICLES TABLE
CREATE TABLE IF NOT EXISTS vehicles (
    vehicle_id SERIAL PRIMARY KEY,
    brand VARCHAR(100) NOT NULL,
    model VARCHAR(100) NOT NULL,
    plate_no VARCHAR(30) UNIQUE NOT NULL,
    type VARCHAR(50) DEFAULT 'Car',
    cc INT,
    rate_per_day NUMERIC(10,2) NOT NULL DEFAULT 0.00,
    rate_with_driver NUMERIC(10,2) NOT NULL DEFAULT 0.00,
    status VARCHAR(30) DEFAULT 'available',
    photo_url TEXT,
    description TEXT,
    seat_capacity INT DEFAULT 5,
    transmission VARCHAR(30) DEFAULT 'Automatic',
    model_3d_url TEXT,
    latitude DOUBLE PRECISION,
    longitude DOUBLE PRECISION,
    current_speed DOUBLE PRECISION DEFAULT 0,
    last_update TIMESTAMP WITH TIME ZONE,
    in_garage BOOLEAN DEFAULT TRUE
);

-- 3. DRIVERS TABLE
CREATE TABLE IF NOT EXISTS drivers (
    driver_id SERIAL PRIMARY KEY,
    user_id INT REFERENCES users(user_id) ON DELETE CASCADE,
    license_no VARCHAR(50),
    status VARCHAR(30) DEFAULT 'active',
    rating_avg NUMERIC(3,2) DEFAULT 5.00,
    total_trips INT DEFAULT 0
);

-- 4. RENTALS TABLE
CREATE TABLE IF NOT EXISTS rentals (
    rental_id SERIAL PRIMARY KEY,
    customer_id INT NOT NULL REFERENCES users(user_id),
    vehicle_id INT NOT NULL REFERENCES vehicles(vehicle_id),
    driver_id INT REFERENCES users(user_id),
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    destination TEXT,
    status VARCHAR(30) DEFAULT 'pending',
    total_amount NUMERIC(10,2) NOT NULL DEFAULT 0.00,
    payment_method VARCHAR(30) DEFAULT 'cash',
    payment_status VARCHAR(30) DEFAULT 'unpaid',
    qr_code TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 5. TRANSACTIONS TABLE
CREATE TABLE IF NOT EXISTS transactions (
    transaction_id SERIAL PRIMARY KEY,
    rental_id INT REFERENCES rentals(rental_id) ON DELETE SET NULL,
    amount NUMERIC(10,2) NOT NULL,
    type VARCHAR(30) DEFAULT 'payment',
    method VARCHAR(30) DEFAULT 'cash',
    proof_url TEXT,
    status VARCHAR(30) DEFAULT 'verified',
    paid_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 6. EXTENSIONS TABLE
CREATE TABLE IF NOT EXISTS extensions (
    extension_id SERIAL PRIMARY KEY,
    rental_id INT REFERENCES rentals(rental_id) ON DELETE CASCADE,
    added_days INT NOT NULL,
    added_fee NUMERIC(10,2) NOT NULL DEFAULT 0.00,
    status VARCHAR(30) DEFAULT 'pending',
    requested_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 7. ISSUES TABLE
CREATE TABLE IF NOT EXISTS issues (
    issue_id SERIAL PRIMARY KEY,
    rental_id INT REFERENCES rentals(rental_id) ON DELETE CASCADE,
    reporter_id INT REFERENCES users(user_id),
    issue_type VARCHAR(50),
    description TEXT,
    status VARCHAR(30) DEFAULT 'open',
    reported_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 8. MESSAGES TABLE
CREATE TABLE IF NOT EXISTS messages (
    message_id SERIAL PRIMARY KEY,
    rental_id INT REFERENCES rentals(rental_id) ON DELETE CASCADE,
    sender_id INT REFERENCES users(user_id),
    text TEXT,
    sent_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 9. RATINGS TABLE
CREATE TABLE IF NOT EXISTS ratings (
    rating_id SERIAL PRIMARY KEY,
    rental_id INT REFERENCES rentals(rental_id) ON DELETE CASCADE,
    customer_id INT REFERENCES users(user_id),
    driver_id INT REFERENCES drivers(driver_id),
    vehicle_id INT REFERENCES vehicles(vehicle_id),
    vehicle_score NUMERIC(3,2),
    driver_score NUMERIC(3,2),
    comment TEXT,
    rated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- INITIAL SEEDING
INSERT INTO users (full_name, email, password_hash, role)
VALUES 
    ('System Admin', 'admin@driveandgo.com', 'admin123', 'admin'),
    ('Test Customer', 'customer@driveandgo.com', 'customer123', 'customer')
ON CONFLICT (email) DO NOTHING;
