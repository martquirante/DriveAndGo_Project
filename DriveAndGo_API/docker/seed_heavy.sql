-- ============================================================
--  DriveAndGo — heavy SQL seed data for local testing
--  Generates 80 customers, 15 drivers, and 20 vehicles
-- ============================================================

DO $$
DECLARE
    -- Arrays of names for generation
    first_names text[] := ARRAY[
        'Juan', 'Maria', 'Jose', 'Ana', 'Pedro', 'Ramon', 'Manuel', 'Liza', 
        'Gloria', 'Sarah', 'Mark', 'John', 'Paul', 'Michael', 'David', 'James', 
        'Robert', 'Mary', 'Patricia', 'Jennifer', 'Elizabeth', 'Linda', 'Barbara', 
        'Susan', 'Jessica', 'Karen', 'Nancy', 'Lisa', 'Betty', 'Margaret', 
        'Sandra', 'Ashley', 'Kimberly', 'Emily', 'Donna', 'Michelle', 'Dorothy', 
        'Carol', 'Amanda', 'Melissa', 'Deborah', 'Stephanie', 'Rebecca', 'Sharon', 
        'Laura', 'Cynthia', 'Kathleen', 'Amy', 'Shirley', 'Angela', 'Helen', 
        'Brenda', 'Pamela', 'Nicole', 'Samantha', 'Katherine', 'Emma', 'Ruth', 
        'Christine', 'Catherine', 'Debra', 'Rachel', 'Carolyn', 'Janet', 'Virginia', 
        'Heather', 'Diane', 'Julie', 'Joyce', 'Evelyn', 'Joan', 'Victoria', 'Kelly', 
        'Christina', 'Lauren', 'Frances', 'Martha', 'Antonio', 'Francisco', 'Gregorio'
    ];
    
    last_names text[] := ARRAY[
        'Santos', 'Reyes', 'Cruz', 'Bautista', 'Ocampo', 'Aquino', 'Marcos', 
        'Duterte', 'Ramos', 'Estrada', 'Macapagal', 'Garcia', 'Dela Cruz', 
        'Del Rosario', 'Villanueva', 'Santiago', 'Mendoza', 'Gonzales', 'Perez', 
        'Castro', 'Rodriguez', 'Flores', 'Gomez', 'Sanchez', 'Diaz', 'Alvarez', 
        'Ruiz', 'Fernandez', 'Valenzuela', 'Salazar', 'Guzman', 'Torres', 
        'Ramirez', 'Lopez', 'Hernandez', 'Martinez', 'Pineda', 'Sarmiento', 
        'Pascual', 'Quirino'
    ];

    -- Vehicle generation pools
    car_brands text[] := ARRAY[
        'Toyota', 'Honda', 'Mitsubishi', 'Hyundai', 'Nissan', 
        'Ford', 'Suzuki', 'Kia', 'Mazda', 'Chevrolet'
    ];
    car_models text[] := ARRAY[
        'Vios', 'Civic', 'Mirage', 'Accent', 'Almera', 
        'Ranger', 'Swift', 'Rio', 'Mazda3', 'Spark', 
        'Fortuner', 'Montero', 'Innova', 'Hilux', 'Ertiga', 
        'Tucson', 'CR-V', 'Navara', 'Everest', 'City'
    ];
    car_types text[] := ARRAY[
        'Sedan', 'Sedan', 'Hatchback', 'Sedan', 'Sedan', 
        'Pickup', 'Hatchback', 'Sedan', 'Sedan', 'Hatchback', 
        'SUV', 'SUV', 'MPV', 'Pickup', 'MPV', 
        'SUV', 'SUV', 'Pickup', 'SUV', 'Sedan'
    ];

    -- Helper counters
    i int;
    new_user_id int;
    v_first_name text;
    v_last_name text;
    v_full_name text;
    v_email text;
    v_phone text;
    v_license_no text;
    
    -- Vehicle data variables
    v_brand text;
    v_model text;
    v_type text;
    v_plate text;
    v_rate numeric;
    v_rate_drv numeric;
    v_transmission text;
    v_capacity int;
    v_cc int;
    idx int;
BEGIN
    -- 1. GENERATE 80 CUSTOMERS
    RAISE NOTICE 'Generating 80 customers...';
    FOR i IN 1..80 LOOP
        v_first_name := first_names[floor(random() * array_length(first_names, 1) + 1)];
        v_last_name := last_names[floor(random() * array_length(last_names, 1) + 1)];
        v_full_name := v_first_name || ' ' || v_last_name;
        
        -- Create unique email by appending loop index
        v_email := lower(replace(v_first_name, ' ', '')) || '.' || lower(replace(v_last_name, ' ', '')) || i || '@driveandgo.com';
        
        -- Generate random 11-digit phone number starting with 09
        v_phone := '09' || floor(random() * 900000000 + 100000000)::text;
        
        -- Insert customer record (Password: Admin@123)
        INSERT INTO users (full_name, email, password_hash, phone, role)
        VALUES (
            v_full_name, 
            v_email, 
            '$2a$11$2LPGl8V1HcMUPXlR3L2sBO5cz1hX6rHHGyCW5zf1K6S0r5Y3nVBmC', 
            v_phone, 
            'customer'
        )
        ON CONFLICT (email) DO NOTHING;
    END LOOP;

    -- 2. GENERATE 15 DRIVERS
    RAISE NOTICE 'Generating 15 drivers...';
    FOR i IN 1..15 LOOP
        v_first_name := first_names[floor(random() * array_length(first_names, 1) + 1)];
        v_last_name := last_names[floor(random() * array_length(last_names, 1) + 1)];
        v_full_name := v_first_name || ' ' || v_last_name;
        
        v_email := 'driver.' || lower(replace(v_first_name, ' ', '')) || '.' || lower(replace(v_last_name, ' ', '')) || i || '@driveandgo.com';
        v_phone := '09' || floor(random() * 900000000 + 100000000)::text;
        
        -- Insert user account first with role = 'driver'
        INSERT INTO users (full_name, email, password_hash, phone, role)
        VALUES (
            v_full_name, 
            v_email, 
            '$2a$11$2LPGl8V1HcMUPXlR3L2sBO5cz1hX6rHHGyCW5zf1K6S0r5Y3nVBmC', 
            v_phone, 
            'driver'
        )
        ON CONFLICT (email) DO NOTHING
        RETURNING user_id INTO new_user_id;

        -- If user already existed, skip creating duplicate driver
        IF new_user_id IS NOT NULL THEN
            -- Generate driver profile details
            v_license_no := chr(floor(random() * 26 + 65)::int) || 
                            chr(floor(random() * 26 + 65)::int) || 
                            floor(random() * 80 + 10)::text || '-' || 
                            floor(random() * 90000 + 10000)::text;
                          
            INSERT INTO drivers (user_id, license_no, status, rating_avg, total_trips)
            VALUES (
                new_user_id, 
                v_license_no, 
                CASE (floor(random() * 3)::int)
                    WHEN 0 THEN 'available'
                    WHEN 1 THEN 'available'
                    ELSE 'off-duty'
                END,
                round((4.0 + random())::numeric, 2), -- rating between 4.0 and 5.0
                floor(random() * 50 + 5)::int
            )
            ON CONFLICT (user_id) DO NOTHING;
        END IF;
    END LOOP;

    -- 3. GENERATE 20 VEHICLES
    RAISE NOTICE 'Generating 20 vehicles...';
    FOR i IN 1..20 LOOP
        -- Select random brand index
        idx := floor(random() * array_length(car_brands, 1) + 1);
        v_brand := car_brands[idx];
        
        -- Select matching or random model index
        idx := floor(random() * array_length(car_models, 1) + 1);
        v_model := car_models[idx];
        v_type := car_types[idx];
        
        -- Generate random unique plate number (e.g. ABC-1234 or WXY-567)
        v_plate := chr(floor(random() * 26 + 65)::int) || 
                   chr(floor(random() * 26 + 65)::int) || 
                   chr(floor(random() * 26 + 65)::int) || '-' || 
                   floor(random() * 9000 + 1000)::text;
        
        -- Configure rates based on vehicle type
        IF v_type = 'SUV' THEN
            v_rate := 3000.00 + (floor(random() * 15) * 100);
            v_rate_drv := v_rate + 1000.00;
            v_capacity := 7;
            v_cc := 2500 + (floor(random() * 5) * 100);
        ELSIF v_type = 'MPV' OR v_type = 'Van' THEN
            v_rate := 2500.00 + (floor(random() * 10) * 100);
            v_rate_drv := v_rate + 800.00;
            v_capacity := 8;
            v_cc := 2000 + (floor(random() * 5) * 100);
        ELSIF v_type = 'Pickup' THEN
            v_rate := 2800.00 + (floor(random() * 12) * 100);
            v_rate_drv := v_rate + 900.00;
            v_capacity := 5;
            v_cc := 2400 + (floor(random() * 6) * 100);
        ELSE -- Sedan / Hatchback
            v_rate := 1200.00 + (floor(random() * 8) * 100);
            v_rate_drv := v_rate + 600.00;
            v_capacity := 5;
            v_cc := 1200 + (floor(random() * 4) * 200);
        END IF;

        v_transmission := CASE (floor(random() * 2)::int)
                              WHEN 0 THEN 'Automatic'
                              ELSE 'Manual'
                          END;
                          
        INSERT INTO vehicles (
            plate_no, brand, model, type, cc, status, 
            rate_per_day, rate_with_driver, photo_url, 
            description, seat_capacity, transmission, 
            model_3d_url, latitude, longitude, 
            current_speed, last_update, in_garage
        )
        VALUES (
            v_plate,
            v_brand,
            v_model,
            v_type,
            v_cc,
            'available',
            v_rate,
            v_rate_drv,
            '/uploads/' || lower(v_brand) || '_' || lower(replace(v_model, ' ', '')) || '.png',
            'Comfortable and reliable ' || v_brand || ' ' || v_model || ' for hire. Perfect for city tours or family travels.',
            v_capacity,
            v_transmission,
            '',
            14.5995 + (random() * 0.2 - 0.1), -- Manila area GPS jitter
            120.9842 + (random() * 0.2 - 0.1),
            0,
            NOW(),
            TRUE
        )
        ON CONFLICT (plate_no) DO NOTHING;
    END LOOP;
    
    RAISE NOTICE 'Done database seeding!';
END $$;
