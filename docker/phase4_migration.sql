-- ============================================================
-- DriveAndGo Phase 4 Migration Script
-- New tables: blockchain_blocks, expenses, toll_logs, split_payments
-- Run against: Local Docker PostgreSQL + Supabase Production
-- Date: 2026-07-10
-- ============================================================

-- ─────────────────────────────────────────────────────────────
-- 1. BLOCKCHAIN BLOCKS (tamper-evident contract ledger)
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS blockchain_blocks (
    block_id      SERIAL PRIMARY KEY,
    rental_id     INT REFERENCES rentals(rental_id) ON DELETE CASCADE,
    block_index   INT NOT NULL,
    block_hash    TEXT NOT NULL,
    prev_hash     TEXT NOT NULL DEFAULT '0',
    contract_data TEXT NOT NULL,
    created_at    TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- ─────────────────────────────────────────────────────────────
-- 2. EXPENSES (vehicle-level expense tracking with OCR support)
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS expenses (
    expense_id  SERIAL PRIMARY KEY,
    vehicle_id  INT REFERENCES vehicles(vehicle_id) ON DELETE SET NULL,
    amount      NUMERIC(10,2) NOT NULL,
    category    VARCHAR(50) NOT NULL DEFAULT 'other',
    receipt_url TEXT,
    created_at  TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- ─────────────────────────────────────────────────────────────
-- 3. TOLL LOGS (per-rental toll tracking)
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS toll_logs (
    toll_log_id SERIAL PRIMARY KEY,
    rental_id   INT NOT NULL REFERENCES rentals(rental_id) ON DELETE CASCADE,
    toll_amount NUMERIC(10,2) NOT NULL,
    location    TEXT,
    timestamp   TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- ─────────────────────────────────────────────────────────────
-- 4. SPLIT PAYMENTS (Barkada Mode)
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS split_payments (
    split_payment_id SERIAL PRIMARY KEY,
    rental_id        INT NOT NULL REFERENCES rentals(rental_id) ON DELETE CASCADE,
    email            VARCHAR(255) NOT NULL,
    share_amount     NUMERIC(10,2) NOT NULL,
    payment_status   VARCHAR(30) NOT NULL DEFAULT 'pending',
    paid_at          TIMESTAMP WITH TIME ZONE
);
