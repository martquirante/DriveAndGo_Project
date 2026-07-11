-- ============================================================
--  DriveAndGo — Rentals, Transactions, Extensions, Issues,
--  Ratings, Logs, and Messages Seeder (Using Temp Tables)
-- ============================================================

DO $$
DECLARE
    -- Helper counts
    num_cust int;
    num_drv int;
    num_veh int;
    rand_idx int;
    
    -- Seeding variables
    r_cust_id int;
    r_veh_id int;
    r_rate_per_day numeric;
    r_rate_with_driver numeric;
    
    r_drv_id int;
    r_drv_user_id int;
    
    -- Date generation
    start_dt timestamptz;
    end_dt timestamptz;
    rent_days int;
    r_status text;
    r_pay_method text;
    r_pay_status text;
    r_total_amount numeric;
    new_rental_id int;
    
    -- Transaction variables
    tx_status text;
    tx_amount numeric;
    
    -- Extensions variables
    ext_days int;
    ext_fee numeric;
    ext_status text;
    
    -- Issues variables
    issue_desc text;
    issue_type text;
    issue_status text;
    
    -- Message text pools
    cust_messages text[] := ARRAY[
        'Hi, san po ba kayo banda? Pasensya na medyo matraffic dito.',
        'Sige po, papunta na ako sa pick-up point.',
        'Pwede po ba extend ng 1 day? Medyo na-delay kami sa biyahe.',
        'All goods naman po yung car, napaka-smooth i-drive. Salamat!',
        'Ask ko lang if kasama na ba fuel sa bayad?'
    ];
    drv_messages text[] := ARRAY[
        'Hello po, nandito na po ako sa labas ng terminal.',
        'No problem po, wait ko kayo rito.',
        'Sige po sir/maam, notify ko si admin para sa extension request nyo.',
        'Salamat din po! Drive safely po.',
        'Hindi po kasama ang fuel, sir/maam. Paki-balik na lang din po ng may karga.'
    ];

    i int;
    j int;
BEGIN
    -- 1. Create temporary tables with indexes to easily pick random records
    CREATE TEMP TABLE temp_customers AS
    SELECT user_id, row_number() OVER () as row_idx
    FROM users WHERE role = 'customer';

    CREATE TEMP TABLE temp_vehicles AS
    SELECT vehicle_id, rate_per_day, rate_with_driver, row_number() OVER () as row_idx
    FROM vehicles;

    CREATE TEMP TABLE temp_drivers AS
    SELECT d.driver_id, d.user_id as driver_user_id, row_number() OVER () as row_idx
    FROM drivers d;

    -- Count elements
    SELECT count(*) INTO num_cust FROM temp_customers;
    SELECT count(*) INTO num_veh FROM temp_vehicles;
    SELECT count(*) INTO num_drv FROM temp_drivers;

    IF num_cust = 0 OR num_veh = 0 THEN
        RAISE EXCEPTION 'Users or Vehicles tables are empty. Please run seed_heavy.sql first!';
    END IF;

    RAISE NOTICE 'Found % customers, % vehicles, and % drivers. Seeding rentals...', num_cust, num_veh, num_drv;

    -- 2. GENERATE 100 RENTALS WITH REALISTIC LIFECYCLE STATES
    FOR i IN 1..100 LOOP
        -- Select random customer
        rand_idx := floor(random() * num_cust + 1);
        SELECT user_id INTO r_cust_id FROM temp_customers WHERE row_idx = rand_idx;
        
        -- Select random vehicle details
        rand_idx := floor(random() * num_veh + 1);
        SELECT vehicle_id, rate_per_day, rate_with_driver 
        INTO r_veh_id, r_rate_per_day, r_rate_with_driver 
        FROM temp_vehicles WHERE row_idx = rand_idx;
        
        -- Decide if driver is requested (35% chance)
        r_drv_id := NULL;
        r_drv_user_id := NULL;
        IF random() < 0.35 AND num_drv > 0 THEN
            rand_idx := floor(random() * num_drv + 1);
            SELECT driver_id, driver_user_id 
            INTO r_drv_id, r_drv_user_id 
            FROM temp_drivers WHERE row_idx = rand_idx;
        END IF;

        -- Decide rental status distribution
        -- 70% completed, 10% active, 10% pending, 10% cancelled/rejected
        IF i <= 70 THEN
            r_status := 'completed';
            r_pay_status := 'paid';
            -- Historical dates (spread across past 3 months)
            start_dt := NOW() - (floor(random() * 90 + 5)::text || ' days')::interval - (floor(random() * 24)::text || ' hours')::interval;
            rent_days := floor(random() * 5 + 1)::int;
            end_dt := start_dt + (rent_days::text || ' days')::interval;
        ELSIF i <= 80 THEN
            r_status := 'active';
            r_pay_status := 'paid';
            -- Started recently, ends in the future
            start_dt := NOW() - (floor(random() * 24)::text || ' hours')::interval;
            rent_days := floor(random() * 4 + 2)::int;
            end_dt := start_dt + (rent_days::text || ' days')::interval;
        ELSIF i <= 90 THEN
            r_status := 'pending';
            r_pay_status := CASE WHEN random() < 0.5 THEN 'unpaid' ELSE 'paid' END; -- some paid but pending approval
            -- Booked for future
            start_dt := NOW() + (floor(random() * 10 + 1)::text || ' days')::interval;
            rent_days := floor(random() * 5 + 1)::int;
            end_dt := start_dt + (rent_days::text || ' days')::interval;
        ELSE
            r_status := CASE WHEN random() < 0.5 THEN 'cancelled' ELSE 'rejected' END;
            r_pay_status := 'unpaid';
            start_dt := NOW() - (floor(random() * 30 + 1)::text || ' days')::interval;
            rent_days := floor(random() * 3 + 1)::int;
            end_dt := start_dt + (rent_days::text || ' days')::interval;
        END IF;

        -- Calculate amount
        IF r_drv_id IS NOT NULL THEN
            r_total_amount := rent_days * r_rate_with_driver;
        ELSE
            r_total_amount := rent_days * r_rate_per_day;
        END IF;

        r_pay_method := (ARRAY['cash', 'gcash', 'maya', 'bank'])[floor(random() * 4 + 1)];

        -- Insert Rental record
        INSERT INTO rentals (
            customer_id, vehicle_id, driver_id, start_date, end_date, 
            destination, status, total_amount, payment_method, 
            payment_status, qr_code, created_at
        )
        VALUES (
            r_cust_id,
            r_veh_id,
            r_drv_id,
            start_dt,
            end_dt,
            (ARRAY['Tagaytay', 'Baguio', 'Batangas', 'Subic', 'Manila Area', 'Laguna', 'Pampanga'])[floor(random() * 7 + 1)],
            r_status,
            r_total_amount,
            r_pay_method,
            r_pay_status,
            'qr_code_rental_' || i || '.png',
            start_dt - interval '1 day'
        )
        RETURNING rental_id INTO new_rental_id;

        -- 3. SEED TRANSACTIONS (IF PAID OR ATTEMPTED)
        IF r_pay_status = 'paid' OR (r_status = 'pending' AND r_pay_status = 'paid') THEN
            tx_status := CASE WHEN r_status = 'pending' THEN 'pending' ELSE 'confirmed' END;
            
            INSERT INTO transactions (rental_id, amount, type, method, proof_url, status, paid_at)
            VALUES (
                new_rental_id,
                r_total_amount,
                'payment',
                r_pay_method,
                CASE WHEN r_pay_method IN ('gcash', 'maya') THEN '/uploads/receipts/proof_' || new_rental_id || '.png' ELSE NULL END,
                tx_status,
                start_dt - interval '12 hours'
            );
        END IF;

        -- 4. SEED EXTENSIONS (ON 15 RENTALS)
        IF i <= 15 THEN
            ext_days := floor(random() * 2 + 1)::int;
            IF r_drv_id IS NOT NULL THEN
                ext_fee := ext_days * r_rate_with_driver;
            ELSE
                ext_fee := ext_days * r_rate_per_day;
            END IF;
            ext_status := CASE WHEN r_status = 'completed' THEN 'approved' ELSE 'pending' END;

            INSERT INTO extensions (rental_id, added_days, added_fee, status, requested_at)
            VALUES (
                new_rental_id,
                ext_days,
                ext_fee,
                ext_status,
                start_dt + interval '12 hours'
            );

            -- Add a transaction for approved extensions
            IF ext_status = 'approved' THEN
                INSERT INTO transactions (rental_id, amount, type, method, proof_url, status, paid_at)
                VALUES (
                    new_rental_id,
                    ext_fee,
                    'extension',
                    r_pay_method,
                    NULL,
                    'confirmed',
                    start_dt + interval '18 hours'
                );
            END IF;
        END IF;

        -- 5. SEED INCIDENT REPORTS / ISSUES (ON 8 RENTALS)
        IF i >= 40 AND i <= 47 THEN
            issue_type := (ARRAY['Engine Heat', 'Flat Tire', 'Minor Scratch', 'Aircon Fault', 'Lost Key'])[floor(random() * 5 + 1)];
            issue_desc := 'Simulated report for ' || lower(issue_type) || '. Driver noticed issue during the trip.';
            issue_status := CASE WHEN r_status = 'completed' THEN 'Resolved' WHEN r_status = 'active' THEN 'In Progress' ELSE 'Pending' END;

            INSERT INTO issues (rental_id, reporter_id, issue_type, description, image_url, status, reported_at)
            VALUES (
                new_rental_id,
                r_cust_id,
                issue_type,
                issue_desc,
                '/uploads/issues/issue_' || new_rental_id || '.png',
                issue_status,
                start_dt + interval '1 day'
            );
        END IF;

        -- 6. SEED CUSTOMER FEEDBACK & RATINGS (FOR ALL 70 COMPLETED RENTALS)
        IF r_status = 'completed' THEN
            INSERT INTO ratings (
                rental_id, customer_id, driver_id, vehicle_id, 
                driver_score, vehicle_score, comment, rated_at
            )
            VALUES (
                new_rental_id,
                r_cust_id,
                r_drv_id,
                r_veh_id,
                CASE WHEN r_drv_id IS NOT NULL THEN floor(random() * 3 + 3)::int ELSE NULL END, -- 3 to 5 stars for driver
                floor(random() * 2 + 4)::int, -- 4 to 5 stars for vehicle
                (ARRAY[
                    'Napakabait ng driver at sobrang lamig ng sasakyan!',
                    'Smooth transaction. On time ang delivery.',
                    'Satisfied customer here. Will rent again next time.',
                    'Okay naman, malinis yung loob. Medyo matagtag lang ng konti.',
                    'Excellent service. highly recommended!'
                ])[floor(random() * 5 + 1)],
                end_dt + interval '2 hours'
            );
        END IF;

        -- 7. SEED LIVE GPS & LOCATION LOGS (FOR ACTIVE RENTALS)
        IF r_status = 'active' THEN
            FOR j IN 1..5 LOOP
                INSERT INTO location_logs (rental_id, vehicle_id, latitude, longitude, speed_kmh, logged_at)
                VALUES (
                    new_rental_id,
                    r_veh_id,
                    14.5995 + (random() * 0.05 - 0.025),
                    120.9842 + (random() * 0.05 - 0.025),
                    30 + (floor(random() * 40)),
                    NOW() - (j * 15 || ' minutes')::interval
                );

                INSERT INTO gps_logs (rental_id, latitude, longitude, odometer_km, logged_at)
                VALUES (
                    new_rental_id,
                    14.5995 + (random() * 0.05 - 0.025),
                    120.9842 + (random() * 0.05 - 0.025),
                    12500.50 + (j * 2.3),
                    NOW() - (j * 15 || ' minutes')::interval
                );
            END LOOP;
        END IF;

        -- 8. SEED CHAT MESSAGES BETWEEN SENDER AND DRIVER / ADMIN (FOR 15 RENTALS)
        IF i <= 15 AND r_drv_id IS NOT NULL THEN
            -- Message 1: Customer asks something
            INSERT INTO messages (rental_id, sender_id, message_text, media_url, sent_at)
            VALUES (
                new_rental_id,
                r_cust_id,
                cust_messages[floor(random() * 5 + 1)],
                NULL,
                start_dt - interval '2 hours'
            );

            -- Message 2: Driver replies
            INSERT INTO messages (rental_id, sender_id, message_text, media_url, sent_at)
            VALUES (
                new_rental_id,
                r_drv_user_id,
                drv_messages[floor(random() * 5 + 1)],
                NULL,
                start_dt - interval '1 hour 45 minutes'
            );
        END IF;

        -- 9. SEED NOTIFICATIONS
        INSERT INTO notifications (user_id, title, body, type, is_read, sent_at)
        VALUES (
            r_cust_id,
            'Rental Request Status',
            'Your rental booking request for vehicle ID ' || r_veh_id || ' has been updated to ' || r_status,
            'BookingUpdate',
            CASE WHEN r_status = 'completed' THEN TRUE ELSE FALSE END,
            start_dt
        );

    END LOOP;
    
    -- 10. Update Average Ratings dynamically for drivers
    RAISE NOTICE 'Updating driver aggregated ratings...';
    UPDATE drivers d
    SET rating_avg = COALESCE((
        SELECT ROUND(AVG(driver_score)::numeric, 2)
        FROM ratings r
        WHERE r.driver_id = d.driver_id AND r.driver_score IS NOT NULL
    ), d.rating_avg),
    total_trips = COALESCE((
        SELECT COUNT(*)
        FROM rentals rent
        WHERE rent.driver_id = d.driver_id AND rent.status = 'completed'
    ), d.total_trips);

    -- Clean up temporary tables
    DROP TABLE temp_customers;
    DROP TABLE temp_vehicles;
    DROP TABLE temp_drivers;

    RAISE NOTICE 'Done rentals and transactions database seeding!';
END $$;
