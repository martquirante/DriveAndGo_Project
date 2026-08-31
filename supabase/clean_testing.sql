-- =====================================================================
-- DriveAndGo — Clean Database for Testing (Zero Seed / Empty Data)
-- =====================================================================
-- Run this in your Supabase SQL Editor if you want to wipe all test
-- transactions, bookings, and dummy data for a fresh testing start.
-- =====================================================================

TRUNCATE TABLE 
    blockchain_ledger,
    toll_logs,
    split_payments,
    driver_bids,
    rental_add_ons,
    chat_messages,
    notifications,
    ratings,
    issues,
    extensions,
    transactions,
    rentals,
    gps_logs,
    location_logs,
    ai_copilot_messages,
    ai_copilot_sessions,
    admin_calendar_notes,
    driver_incidents,
    driver_documents,
    driver_emergency_contacts,
    driver_payout_accounts,
    vehicle_maintenance
RESTART IDENTITY CASCADE;

-- Optional: If you also want to remove test vehicles, drivers, and non-admin customers:
-- DELETE FROM drivers;
-- DELETE FROM vehicles;
-- DELETE FROM users WHERE role NOT IN ('admin', 'superadmin');
