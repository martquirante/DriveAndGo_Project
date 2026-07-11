-- ============================================================
--  DriveAndGo — Seeding Remaining Advanced Feature Tables
--  Covers: add_ons, rental_add_ons, promo_codes, geofence_zones,
--          driver_bids, maintenance_logs, expenses, split_payments,
--          toll_logs, and blockchain_ledger
-- ============================================================

DO $$
DECLARE
    -- Collections for selection
    r_rental record;
    r_addon_id int;
    r_driver_id int;
    r_vehicle_id int;
    r_admin_id int;
    
    -- Seeding variables
    num_rentals int;
    num_addons int;
    num_drivers int;
    num_vehicles int;
    
    -- Blockchain simulation variables
    prev_hash text := '0000000000000000000000000000000000000000000000000000000000000000';
    curr_hash text;
    block_idx int := 1;
    json_data jsonb;
    
    -- Declared variables for query loops
    new_rental_id int;
    start_dt timestamptz;
    r_total_amount numeric;
    
    i int;
    rand_idx int;
BEGIN
    -- 1. Get system admin ID for expense logger
    SELECT user_id INTO r_admin_id FROM users WHERE role = 'admin' LIMIT 1;
    IF r_admin_id IS NULL THEN
        r_admin_id := 1;
    END IF;

    -- 2. SEED ADD_ONS (IF NOT EXISTS)
    RAISE NOTICE 'Seeding add_ons...';
    INSERT INTO add_ons (name, description, daily_rate, flat_rate, is_active)
    VALUES 
        ('GPS Navigator', 'Interactive GPS screen dashboard with local maps.', 150.00, 0.00, TRUE),
        ('Child Safety Seat', 'Certified comfortable toddler booster car seat.', 250.00, 0.00, TRUE),
        ('Roof Luggage Rack', 'Extra rooftop cargo box for heavy travel luggage.', 400.00, 0.00, TRUE),
        ('Unlimited Mileage', 'Bypass standard daily mileage distance limits.', 0.00, 1500.00, TRUE),
        ('Extra Driver Coverage', 'Add authorization for a secondary driver companion.', 0.00, 500.00, TRUE)
    ON CONFLICT DO NOTHING;

    -- 3. SEED PROMO CODES
    RAISE NOTICE 'Seeding promo_codes...';
    INSERT INTO promo_codes (code, discount_type, discount_value, min_rental_days, max_uses, used_count, valid_from, valid_until, is_active, created_at)
    VALUES
        ('DRIVE10', 'Percentage', 10.00, 1, 500, 42, '2026-01-01', '2026-12-31', TRUE, NOW()),
        ('ROADTRIP20', 'Percentage', 20.00, 3, 200, 15, '2026-01-01', '2026-12-31', TRUE, NOW()),
        ('FLAT500', 'Fixed', 500.00, 2, 100, 8, '2026-01-01', '2026-12-31', TRUE, NOW()),
        ('WELCOME15', 'Percentage', 15.00, 1, 1000, 95, '2026-01-01', '2026-12-31', TRUE, NOW())
    ON CONFLICT DO NOTHING;

    -- 4. SEED GEOFENCE ZONES
    RAISE NOTICE 'Seeding geofence_zones...';
    INSERT INTO geofence_zones (name, center_lat, center_lng, radius_km, is_active)
    VALUES
        ('NAIA Airport Area', 14.5086, 121.0194, 3.0, TRUE),
        ('Tagaytay City Limits', 14.1153, 120.9621, 8.0, TRUE),
        ('Baguio City Proper', 16.4023, 120.5960, 5.0, TRUE)
    ON CONFLICT DO NOTHING;

    -- Gather counts
    SELECT count(*) INTO num_addons FROM add_ons;
    SELECT count(*) INTO num_drivers FROM drivers;
    SELECT count(*) INTO num_vehicles FROM vehicles;

    -- Create temporary tables for looping
    CREATE TEMP TABLE t_rentals AS 
    SELECT rental_id, vehicle_id, start_date, total_amount, status, row_number() OVER () as row_idx FROM rentals;
    
    SELECT count(*) INTO num_rentals FROM t_rentals;

    -- 5. SEED RENTAL_ADD_ONS (Link 40 random rentals to some add_ons)
    RAISE NOTICE 'Seeding rental_add_ons...';
    FOR i IN 1..40 LOOP
        rand_idx := floor(random() * num_rentals + 1);
        SELECT rental_id INTO new_rental_id FROM t_rentals WHERE row_idx = rand_idx;
        
        SELECT add_on_id INTO r_addon_id FROM add_ons ORDER BY random() LIMIT 1;
        
        INSERT INTO rental_add_ons (rental_id, add_on_id, quantity)
        VALUES (new_rental_id, r_addon_id, 1)
        ON CONFLICT DO NOTHING;
    END LOOP;

    -- 6. SEED DRIVER BIDS (For the 10 pending rentals)
    RAISE NOTICE 'Seeding driver_bids...';
    FOR r_rental IN SELECT rental_id, start_date FROM rentals WHERE status = 'pending' LOOP
        -- Generate 2-3 driver bids for each pending rental
        FOR i IN 1..(floor(random() * 2 + 2)::int) LOOP
            -- Fetch the driver's user_id since driver_bids.driver_id is a FK to users(user_id)
            SELECT user_id INTO r_driver_id FROM drivers ORDER BY random() LIMIT 1;
            
            INSERT INTO driver_bids (rental_id, driver_id, status, bid_at)
            VALUES (
                r_rental.rental_id,
                r_driver_id,
                CASE WHEN i = 1 THEN 'pending' ELSE 'rejected' END,
                r_rental.start_date - interval '1 day'
            )
            ON CONFLICT DO NOTHING;
        END LOOP;
    END LOOP;

    -- 7. SEED VEHICLE MAINTENANCE LOGS (For 8 vehicles)
    RAISE NOTICE 'Seeding maintenance_logs...';
    FOR i IN 1..8 LOOP
        SELECT vehicle_id INTO r_vehicle_id FROM vehicles ORDER BY random() LIMIT 1;
        
        INSERT INTO maintenance_logs (vehicle_id, maintenance_type, cost, status, notes, scheduled_at, completed_at, created_at)
        VALUES (
            r_vehicle_id,
            (ARRAY['Oil Change', 'Brake Replacement', 'Tire Rotation', 'Aircon Cleaning'])[floor(random() * 4 + 1)],
            1500.00 + (floor(random() * 8) * 500.00),
            'Completed',
            'Completed routine maintenance checks. Clean fluids replaced.',
            (NOW() - interval '30 days')::date,
            (NOW() - interval '29 days')::date,
            NOW() - interval '30 days'
        );
    END LOOP;

    -- 8. SEED EXPENSES (For 30 rentals)
    RAISE NOTICE 'Seeding expenses...';
    FOR i IN 1..30 LOOP
        rand_idx := floor(random() * num_rentals + 1);
        SELECT rental_id, vehicle_id, start_date INTO new_rental_id, r_vehicle_id, start_dt FROM t_rentals WHERE row_idx = rand_idx;
        
        INSERT INTO expenses (rental_id, vehicle_id, amount, category, description, receipt_url, ocr_raw_text, logged_by, created_at)
        VALUES (
            new_rental_id,
            r_vehicle_id,
            300.00 + (floor(random() * 15) * 100.00),
            (ARRAY['Fuel', 'Toll', 'Car Wash', 'Flat Tire Repair'])[floor(random() * 4 + 1)],
            'Simulated road expense for rental ' || new_rental_id,
            '/uploads/receipts/expense_' || i || '.jpg',
            'PETRON SERVICE STATION OCR SCAN OK - AMOUNT P1,500.00',
            r_admin_id,
            start_dt + interval '1 day'
        );
    END LOOP;

    -- 9. SEED SPLIT PAYMENTS (For 10 rentals)
    RAISE NOTICE 'Seeding split_payments...';
    FOR i IN 1..10 LOOP
        rand_idx := floor(random() * num_rentals + 1);
        SELECT rental_id, total_amount, start_date INTO new_rental_id, r_total_amount, start_dt FROM t_rentals WHERE row_idx = rand_idx;
        
        -- Split payment among 2 friends
        INSERT INTO split_payments (rental_id, participant_email, share_amount, payment_status, paid_at)
        VALUES 
            (new_rental_id, 'friend1.' || i || '@test.com', ROUND(r_total_amount / 2, 2), 'paid', start_dt - interval '6 hours'),
            (new_rental_id, 'friend2.' || i || '@test.com', ROUND(r_total_amount / 2, 2), 'paid', start_dt - interval '5 hours');
    END LOOP;

    -- 10. SEED TOLL LOGS (For 25 rentals)
    RAISE NOTICE 'Seeding toll_logs...';
    FOR i IN 1..25 LOOP
        rand_idx := floor(random() * num_rentals + 1);
        SELECT rental_id, start_date INTO new_rental_id, start_dt FROM t_rentals WHERE row_idx = rand_idx;
        
        INSERT INTO toll_logs (rental_id, toll_name, toll_amount, location, logged_at)
        VALUES 
            (new_rental_id, 'Balintawak Toll Plaza', 350.00, 'NLEX Northbound', start_dt + interval '3 hours'),
            (new_rental_id, 'Nichols Toll Entry', 150.00, 'SLEX Southbound', start_dt + interval '4 hours');
    END LOOP;

    -- 11. SEED BLOCKCHAIN AUDIT LEDGER (Building a cryptographically sequential hash chain!)
    RAISE NOTICE 'Seeding blockchain_ledger...';
    -- Retrieve all rentals and lock them in index blocks
    FOR r_rental IN SELECT rental_id, total_amount, start_date, status FROM rentals ORDER BY rental_id LOOP
        json_data := json_build_object(
            'rental_id', r_rental.rental_id,
            'total_amount', r_rental.total_amount,
            'status', r_rental.status,
            'timestamp', r_rental.start_date
        );
        
        -- Generate MD5 or SHA hash chain representation in text
        curr_hash := md5(block_idx::text || prev_hash || json_data::text);
        
        INSERT INTO blockchain_ledger (block_index, rental_id, block_hash, previous_hash, contract_data, created_at)
        VALUES (
            block_idx,
            r_rental.rental_id,
            curr_hash,
            prev_hash,
            json_data,
            r_rental.start_date
        );
        
        -- Set previous hash for the next block
        prev_hash := curr_hash;
        block_idx := block_idx + 1;
    END LOOP;

    DROP TABLE t_rentals;
    RAISE NOTICE 'All advanced features successfully seeded!';
END $$;
