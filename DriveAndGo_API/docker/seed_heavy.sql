-- ============================================================
--  DriveAndGo — Production-Grade Heavy SQL Seeder
--  Generates Admin accounts, 80 customers, 15 drivers, and 20 verified vehicles
-- ============================================================

DO $$
DECLARE
    -- Arrays of Filipino names for realistic customer and driver generation
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

    -- Authentic 20-Fleet Spec (Brand, Model, Type, Plate, CC, RatePerDay, RateWithDriver, Transmission, Seats, Color, PhotoUrl)
    fleet_specs jsonb := '[
        {"brand": "Toyota", "model": "Vios GR-S", "type": "Sedan", "plate": "NBJ-1029", "cc": 1500, "rate": 1800, "rate_drv": 2500, "trans": "Automatic", "seats": 5, "color": "Super White II", "photo": "/uploads/vehicles/50fc3f31-3ff0-4a00-8165-6d1885653362.png"},
        {"brand": "Honda", "model": "Civic RS Turbo", "type": "Sedan", "plate": "NCA-4820", "cc": 1500, "rate": 2800, "rate_drv": 3800, "trans": "Automatic", "seats": 5, "color": "Ignite Red Metallic", "photo": "/uploads/vehicles/5a0fb00d-0c7b-46bf-985a-f2908430333a.webp"},
        {"brand": "Mitsubishi", "model": "Mirage G4", "type": "Sedan", "plate": "NDB-3819", "cc": 1200, "rate": 1500, "rate_drv": 2200, "trans": "Automatic", "seats": 5, "color": "Cool Silver", "photo": "/uploads/vehicles/1d3d584b-8f4b-497b-b472-c93be44bb7a1.webp"},
        {"brand": "Ford", "model": "Everest Titanium+ 4x4", "type": "SUV", "plate": "NFO-8841", "cc": 2000, "rate": 4500, "rate_drv": 5800, "trans": "Automatic", "seats": 7, "color": "Absolute Black", "photo": "/uploads/vehicles/ford_everest_black.jpg"},
        {"brand": "Ford", "model": "Ranger Wildtrak 4x2", "type": "Pickup", "plate": "NFR-7193", "cc": 2000, "rate": 3500, "rate_drv": 4500, "trans": "Automatic", "seats": 5, "color": "Sedona Orange", "photo": "/uploads/vehicles/34313fa8-ff70-4c6e-9b48-092c257107f9.jpg"},
        {"brand": "Nissan", "model": "Navara PRO-4X", "type": "Pickup", "plate": "NNV-5510", "cc": 2500, "rate": 3600, "rate_drv": 4600, "trans": "Automatic", "seats": 5, "color": "Stealth Pearl Gray", "photo": "/uploads/vehicles/78413a42-869a-4185-bca5-070fca53a679.jpg"},
        {"brand": "Toyota", "model": "Fortuner GR Sport", "type": "SUV", "plate": "NTF-9921", "cc": 2800, "rate": 4800, "rate_drv": 6000, "trans": "Automatic", "seats": 7, "color": "Attitude Black Mica", "photo": "/uploads/vehicles/a051c046-b147-4059-89eb-d49c041919d7.jpg"},
        {"brand": "Mitsubishi", "model": "Montero Sport GT", "type": "SUV", "plate": "NMM-2044", "cc": 2400, "rate": 4200, "rate_drv": 5400, "trans": "Automatic", "seats": 7, "color": "White Diamond", "photo": "/uploads/vehicles/b314fdd9-0182-4849-9b01-213ae8c8aff3.jpg"},
        {"brand": "Hyundai", "model": "Tucson GLS", "type": "SUV", "plate": "NHT-3190", "cc": 2000, "rate": 3200, "rate_drv": 4200, "trans": "Automatic", "seats": 5, "color": "Phantom Black", "photo": "/uploads/vehicles/c8657a2d-b7a6-41a6-bb9b-dc35e91a6084.webp"},
        {"brand": "Suzuki", "model": "Swift GLX", "type": "Hatchback", "plate": "NSS-6281", "cc": 1200, "rate": 1600, "rate_drv": 2300, "trans": "Automatic", "seats": 5, "color": "Speedy Blue Metallic", "photo": "/uploads/vehicles/dc39c718-b9d6-4cfe-90bf-668252a53746.webp"},
        {"brand": "Mazda", "model": "Mazda 3 Fastback", "type": "Sedan", "plate": "NMZ-4012", "cc": 2000, "rate": 2900, "rate_drv": 3900, "trans": "Automatic", "seats": 5, "color": "Soul Red Crystal", "photo": "/uploads/vehicles/e030c2e0-9b06-43a0-879e-d0c0b62b8ded.jpg"},
        {"brand": "Toyota", "model": "Innova Zenix Q", "type": "MPV", "plate": "NTI-7733", "cc": 2000, "rate": 3300, "rate_drv": 4300, "trans": "Automatic", "seats": 7, "color": "Platinum White Pearl", "photo": "/uploads/vehicles/0cff11547ac24a8baef44f9b0e50b7c6.jpg"},
        {"brand": "Kia", "model": "Seltos SX", "type": "SUV", "plate": "NKS-5829", "cc": 1400, "rate": 2600, "rate_drv": 3500, "trans": "Automatic", "seats": 5, "color": "Starbright Yellow", "photo": "/uploads/vehicles/317eb6e2f178488e9e7c44e9ba9982ac.jpg"},
        {"brand": "Chevrolet", "model": "Tracker Redline", "type": "SUV", "plate": "NCT-1940", "cc": 1000, "rate": 2400, "rate_drv": 3300, "trans": "Automatic", "seats": 5, "color": "Summit White", "photo": "/uploads/vehicles/83b16604-4474-4098-9452-36db2bc98a3a.jpg"},
        {"brand": "Nissan", "model": "Almera VL Turbo", "type": "Sedan", "plate": "NNA-8819", "cc": 1000, "rate": 1900, "rate_drv": 2600, "trans": "Automatic", "seats": 5, "color": "Gun Metallic", "photo": "/uploads/vehicles/a79c77a2-e53b-4060-9888-4e9f89c06ad4.jpg"},
        {"brand": "Honda", "model": "CR-V RS e:HEV", "type": "SUV", "plate": "NHC-6041", "cc": 2000, "rate": 4600, "rate_drv": 5800, "trans": "Automatic", "seats": 5, "color": "Canyon River Blue", "photo": "/uploads/vehicles/ecbb8296-4f4f-4270-8a7f-1f9d05bde387.webp"},
        {"brand": "Isuzu", "model": "D-MAX LS-E 4x4", "type": "Pickup", "plate": "NID-9930", "cc": 3000, "rate": 3800, "rate_drv": 4900, "trans": "Automatic", "seats": 5, "color": "Valencia Orange", "photo": "/uploads/vehicles/ca244bcb-3107-4c15-9875-26127cb4e2d4.webp"},
        {"brand": "Ford", "model": "Everest Sport 4x2", "type": "SUV", "plate": "NFE-5018", "cc": 2000, "rate": 4000, "rate_drv": 5100, "trans": "Automatic", "seats": 7, "color": "Meteor Grey", "photo": "/uploads/vehicles/ford_everest_grey.jpg"},
        {"brand": "Toyota", "model": "Hilux Conquest 4x4", "type": "Pickup", "plate": "NTH-3392", "cc": 2800, "rate": 3700, "rate_drv": 4800, "trans": "Automatic", "seats": 5, "color": "Emotional Red", "photo": "/uploads/vehicles/5ac4a216-705a-489c-8bed-93b43471c8f2.webp"},
        {"brand": "BMW", "model": "320i M Sport", "type": "Sedan", "plate": "NBM-3300", "cc": 2000, "rate": 7500, "rate_drv": 9500, "trans": "Automatic", "seats": 5, "color": "Alpine White", "photo": "/uploads/vehicles/6523258b-9a1b-4fc6-94fc-16ccbb518b9b.png"}
    ]'::jsonb;

    -- Helper counters
    i int;
    new_user_id int;
    v_first_name text;
    v_last_name text;
    v_full_name text;
    v_email text;
    v_phone text;
    v_license_no text;
    v_spec jsonb;
BEGIN
    -- 1. SEED SYSTEM ADMIN ACCOUNTS
    RAISE NOTICE 'Seeding verified Admin accounts...';
    INSERT INTO users (full_name, email, password_hash, phone, role)
    VALUES 
      ('Ray Quirante', 'rayquirante@gmail.com', '$2a$11$2LPGl8V1HcMUPXlR3L2sBO5cz1hX6rHHGyCW5zf1K6S0r5Y3nVBmC', '09171234567', 'admin'),
      ('Ray Quirante', 'rayuirante@gmail.com', '$2a$11$2LPGl8V1HcMUPXlR3L2sBO5cz1hX6rHHGyCW5zf1K6S0r5Y3nVBmC', '09171234567', 'admin'),
      ('Super Admin', 'admin@driveandgo.com', '$2a$11$2LPGl8V1HcMUPXlR3L2sBO5cz1hX6rHHGyCW5zf1K6S0r5Y3nVBmC', '09170000000', 'superadmin')
    ON CONFLICT (email) DO UPDATE SET 
      role = EXCLUDED.role,
      password_hash = EXCLUDED.password_hash;

    -- 2. GENERATE 80 CUSTOMERS
    RAISE NOTICE 'Generating 80 customers...';
    FOR i IN 1..80 LOOP
        v_first_name := first_names[floor(random() * array_length(first_names, 1) + 1)];
        v_last_name := last_names[floor(random() * array_length(last_names, 1) + 1)];
        v_full_name := v_first_name || ' ' || v_last_name;
        
        v_email := lower(replace(v_first_name, ' ', '')) || '.' || lower(replace(v_last_name, ' ', '')) || i || '@driveandgo.com';
        v_phone := '09' || floor(random() * 900000000 + 100000000)::text;
        
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

    -- 3. GENERATE 15 DRIVERS
    RAISE NOTICE 'Generating 15 professional drivers...';
    FOR i IN 1..15 LOOP
        v_first_name := first_names[floor(random() * array_length(first_names, 1) + 1)];
        v_last_name := last_names[floor(random() * array_length(last_names, 1) + 1)];
        v_full_name := v_first_name || ' ' || v_last_name;
        
        v_email := 'driver.' || lower(replace(v_first_name, ' ', '')) || '.' || lower(replace(v_last_name, ' ', '')) || i || '@driveandgo.com';
        v_phone := '09' || floor(random() * 900000000 + 100000000)::text;
        
        INSERT INTO users (full_name, email, password_hash, phone, role)
        VALUES (
            v_full_name, 
            v_email, 
            '$2a$11$2LPGl8V1HcMUPXlR3L2sBO5cz1hX6rHHGyCW5zf1K6S0r5Y3nVBmC', 
            v_phone, 
            'driver'
        )
        ON CONFLICT (email) DO UPDATE SET role = 'driver'
        RETURNING user_id INTO new_user_id;

        IF new_user_id IS NOT NULL THEN
            v_license_no := 'N' || floor(random() * 90 + 10)::text || '-' || 
                            floor(random() * 90 + 10)::text || '-' || 
                            floor(random() * 900000 + 100000)::text;
                            
            INSERT INTO drivers (user_id, license_no, license_photo_url, status, rating_avg, total_trips)
            VALUES (
                new_user_id, 
                v_license_no, 
                '/uploads/driver_docs/license_sample.png',
                'available', 
                round((4.0 + (random() * 1.0))::numeric, 2), 
                floor(random() * 50 + 5)::int
            )
            ON CONFLICT DO NOTHING;
        END IF;
    END LOOP;

    -- 4. INSERT 20 SPEC-ACCURATE FLEET VEHICLES
    RAISE NOTICE 'Seeding 20 verified fleet vehicles...';
    FOR i IN 0..jsonb_array_length(fleet_specs)-1 LOOP
        v_spec := fleet_specs->i;
        
        INSERT INTO vehicles (
            plate_no, brand, model, type, cc, status, 
            rate_per_day, rate_with_driver, photo_url, 
            description, seat_capacity, transmission, 
            model_3d_url, latitude, longitude, 
            current_speed, last_update, in_garage,
            color, flood_risk_status, engine_water_ingress_alert, last_weather_temp,
            fuel_level_pct, odometer_km, health_score, engine_status, maintenance_due_km,
            telematics_locked, lto_expiry_date, insurance_expiry_date, safety_score,
            rfid_balance_autosweep, rfid_balance_easytrip, expressway_rfid_balance
        )
        VALUES (
            v_spec->>'plate',
            v_spec->>'brand',
            v_spec->>'model',
            v_spec->>'type',
            (v_spec->>'cc')::int,
            'available',
            (v_spec->>'rate')::numeric,
            (v_spec->>'rate_drv')::numeric,
            v_spec->>'photo',
            'Well-maintained, premium ' || (v_spec->>'brand') || ' ' || (v_spec->>'model') || ' (' || (v_spec->>'type') || '). Available for self-drive or with professional chauffeur.',
            (v_spec->>'seats')::int,
            v_spec->>'trans',
            '',
            14.871116 + (random() * 0.04 - 0.02),
            121.048088 + (random() * 0.04 - 0.02),
            0,
            NOW(),
            TRUE,
            v_spec->>'color',
            'safe',
            FALSE,
            28.5,
            100,
            floor(random() * 45000 + 5000)::int,
            98,
            'off',
            5000,
            TRUE,
            NOW() + INTERVAL '180 days',
            NOW() + INTERVAL '365 days',
            95,
            500.00,
            500.00,
            500.00
        )
        ON CONFLICT (plate_no) DO UPDATE SET
            brand = EXCLUDED.brand,
            model = EXCLUDED.model,
            type = EXCLUDED.type,
            photo_url = EXCLUDED.photo_url,
            color = EXCLUDED.color,
            rate_per_day = EXCLUDED.rate_per_day,
            rate_with_driver = EXCLUDED.rate_with_driver;
    END LOOP;
    
    RAISE NOTICE 'Done realistic database seeding!';
END $$;
