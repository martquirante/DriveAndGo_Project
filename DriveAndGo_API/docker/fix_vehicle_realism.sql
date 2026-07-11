-- ============================================================
--  DriveAndGo — Correcting Mismatched and Missing Vehicle Data
--  Ensures plate numbers, engine CC, brands, and models align
--  perfectly for all 22 seeded vehicles.
-- ============================================================

-- Update Vehicle 6 (Toyota Vios)
UPDATE vehicles 
SET brand = 'Toyota', model = 'Vios', type = 'Sedan', cc = 1500, plate_no = 'NDL-4819', description = 'Reliable and fuel-efficient Toyota Vios. Best for city trips.' 
WHERE vehicle_id = 6;

-- Update Vehicle 7 (Mitsubishi Mirage)
UPDATE vehicles 
SET brand = 'Mitsubishi', model = 'Mirage', type = 'Hatchback', cc = 1200, plate_no = 'XYZ-7892', description = 'Compact and nimble Mitsubishi Mirage. Easy parking.' 
WHERE vehicle_id = 7;

-- Update Vehicle 9 (Suzuki Swift)
UPDATE vehicles 
SET brand = 'Suzuki', model = 'Swift', type = 'Hatchback', cc = 1200, plate_no = 'XWK-9492', description = 'Sporty and modern Suzuki Swift Hatchback.' 
WHERE vehicle_id = 9;

-- Update Vehicle 10 (Nissan Navara)
UPDATE vehicles 
SET brand = 'Nissan', model = 'Navara', type = 'Pickup', cc = 2500, plate_no = 'TIM-3253', description = 'Powerful Nissan Navara Pickup. Built for rugged roads.' 
WHERE vehicle_id = 10;

-- Update Vehicle 11 (Chevrolet Spark)
UPDATE vehicles 
SET brand = 'Chevrolet', model = 'Spark', type = 'Hatchback', cc = 1000, plate_no = 'SQU-9682', description = 'Super compact Chevrolet Spark. Ideal for narrow streets.' 
WHERE vehicle_id = 11;

-- Update Vehicle 12 (Hyundai Tucson)
UPDATE vehicles 
SET brand = 'Hyundai', model = 'Tucson', type = 'SUV', cc = 2000, plate_no = 'GRW-5582', description = 'Comfortable Hyundai Tucson SUV. Smooth riding.' 
WHERE vehicle_id = 12;

-- Update Vehicle 13 (Toyota Vios)
UPDATE vehicles 
SET brand = 'Toyota', model = 'Vios', type = 'Sedan', cc = 1500, plate_no = 'JUJ-4346', description = 'Reliable and fuel-efficient Toyota Vios. Best for city trips.' 
WHERE vehicle_id = 13;

-- Update Vehicle 14 (Honda City)
UPDATE vehicles 
SET brand = 'Honda', model = 'City', type = 'Sedan', cc = 1500, plate_no = 'JLZ-2134', description = 'Stylish Honda City Sedan. Smooth handling.' 
WHERE vehicle_id = 14;

-- Update Vehicle 15 (Toyota Fortuner - Fixing placeholder plate and engine CC!)
UPDATE vehicles 
SET brand = 'Toyota', 
    model = 'Fortuner', 
    type = 'SUV', 
    cc = 2800, 
    plate_no = 'ABC-1234', 
    rate_per_day = 3950.00,
    rate_with_driver = 4950.00,
    description = 'Comfortable and reliable Toyota Fortuner SUV for hire. Perfect for family road trips.' 
WHERE vehicle_id = 15;

-- Update Vehicle 16 (Ford Ranger)
UPDATE vehicles 
SET brand = 'Ford', model = 'Ranger', type = 'Pickup', cc = 2200, plate_no = 'TIE-4833', description = 'Tough Ford Ranger Pickup. Heavy-duty utility.' 
WHERE vehicle_id = 16;

-- Update Vehicle 17 (Kia Rio)
UPDATE vehicles 
SET brand = 'Kia', model = 'Rio', type = 'Sedan', cc = 1400, plate_no = 'JRT-6695', description = 'Comfortable Kia Rio Sedan. Great daily drive.' 
WHERE vehicle_id = 17;

-- Update Vehicle 18 (Mitsubishi Mirage)
UPDATE vehicles 
SET brand = 'Mitsubishi', model = 'Mirage', type = 'Hatchback', cc = 1200, plate_no = 'AMW-6780', description = 'Compact and nimble Mitsubishi Mirage. Easy parking.' 
WHERE vehicle_id = 18;

-- Update Vehicle 19 (Honda City)
UPDATE vehicles 
SET brand = 'Honda', model = 'City', type = 'Sedan', cc = 1500, plate_no = 'LFH-6871', description = 'Stylish Honda City Sedan. Smooth handling.' 
WHERE vehicle_id = 19;

-- Update Vehicle 20 (Toyota Fortuner)
UPDATE vehicles 
SET brand = 'Toyota', model = 'Fortuner', type = 'SUV', cc = 2800, plate_no = 'TZQ-5663', description = 'Comfortable and reliable Toyota Fortuner SUV for hire. Perfect for family road trips.' 
WHERE vehicle_id = 20;

-- Update Vehicle 21 (Ford Everest)
UPDATE vehicles 
SET brand = 'Ford', model = 'Everest', type = 'SUV', cc = 2000, plate_no = 'MZC-8599', description = 'Premium Ford Everest SUV. High comfort and capability.' 
WHERE vehicle_id = 21;

-- Update Vehicle 22 (Nissan Almera)
UPDATE vehicles 
SET brand = 'Nissan', model = 'Almera', type = 'Sedan', cc = 1500, plate_no = 'OSH-2977', description = 'Spacious Nissan Almera Sedan. Large trunk capacity.' 
WHERE vehicle_id = 22;

-- Update Vehicle 23 (Suzuki Swift)
UPDATE vehicles 
SET brand = 'Suzuki', model = 'Swift', type = 'Hatchback', cc = 1200, plate_no = 'PFH-3606', description = 'Sporty and modern Suzuki Swift Hatchback.' 
WHERE vehicle_id = 23;

-- Update Vehicle 24 (Honda CR-V)
UPDATE vehicles 
SET brand = 'Honda', model = 'CR-V', type = 'SUV', cc = 2000, plate_no = 'VTD-4542', description = 'Premium Honda CR-V crossover SUV.' 
WHERE vehicle_id = 24;

-- Update Vehicle 25 (Hyundai Tucson)
UPDATE vehicles 
SET brand = 'Hyundai', model = 'Tucson', type = 'SUV', cc = 2000, plate_no = 'BZA-4236', description = 'Comfortable Hyundai Tucson SUV. Smooth riding.' 
WHERE vehicle_id = 25;

-- Update Vehicle 26 (Toyota Hilux)
UPDATE vehicles 
SET brand = 'Toyota', model = 'Hilux', type = 'Pickup', cc = 2400, plate_no = 'WTD-5483', description = 'Unbreakable Toyota Hilux Pickup. Ready for anything.' 
WHERE vehicle_id = 26;

-- Update Vehicle 27 (Toyota Vios)
UPDATE vehicles 
SET brand = 'Toyota', model = 'Vios', type = 'Sedan', cc = 1500, plate_no = 'VSW-3689', description = 'Reliable and fuel-efficient Toyota Vios. Best for city trips.' 
WHERE vehicle_id = 27;

-- Update Vehicle 28 (Ford Everest)
UPDATE vehicles 
SET brand = 'Ford', model = 'Everest', type = 'SUV', cc = 2000, plate_no = 'RUF-8955', description = 'Premium Ford Everest SUV. High comfort and capability.' 
WHERE vehicle_id = 28;
