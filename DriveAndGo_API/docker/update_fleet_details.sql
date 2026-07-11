-- ============================================================
--  DriveAndGo — Seeding Advanced Fleet Management Fields
--  Populates: purchase_cost, depreciation_rate, odometer_km,
--             last_oil_change_km, next_registration_due, fuel_level,
--             photo_url, and model_3d_url
-- ============================================================

-- 1. Update purchase costs, depreciation, odometer, fuel, registration dates, and asset URLs
UPDATE vehicles
SET 
  purchase_cost = CASE 
    WHEN brand = 'Toyota' AND model = 'Fortuner' THEN 1650000.00
    WHEN brand = 'Ford' AND model = 'Everest' THEN 1850000.00
    WHEN brand = 'Nissan' AND model = 'Navara' THEN 1350000.00
    WHEN brand = 'Ford' AND model = 'Ranger' THEN 1250000.00
    WHEN brand = 'Toyota' AND model = 'Hilux' THEN 1200000.00
    WHEN brand = 'Honda' AND model = 'CR-V' THEN 1700000.00
    WHEN brand = 'Hyundai' AND model = 'Tucson' THEN 1400000.00
    WHEN brand = 'Toyota' AND model = 'Vios' THEN 850000.00
    WHEN brand = 'Honda' AND model = 'City' THEN 950000.00
    WHEN brand = 'Kia' AND model = 'Rio' THEN 800000.00
    WHEN brand = 'Nissan' AND model = 'Almera' THEN 850000.00
    WHEN brand = 'Mitsubishi' AND model = 'Mirage' THEN 680000.00
    WHEN brand = 'Suzuki' AND model = 'Swift' THEN 780000.00
    WHEN brand = 'Chevrolet' AND model = 'Spark' THEN 650000.00
    ELSE 900000.00
  END,
  depreciation_rate = ROUND((0.05 + random() * 0.08)::numeric, 2),
  odometer_km = floor(15000 + random() * 75000)::int,
  fuel_level = ROUND((30.0 + random() * 65.0)::numeric, 2),
  next_registration_due = CURRENT_DATE + floor(30 + random() * 300)::int,
  photo_url = '/uploads/vehicles/' || lower(brand) || '_' || lower(replace(model, ' ', '')) || '.png',
  model_3d_url = '/assets/models/' || lower(brand) || '_' || lower(replace(model, ' ', '')) || '.glb';

-- 2. Update last oil change to be a realistic number of kilometers before the current odometer
UPDATE vehicles
SET last_oil_change_km = odometer_km - floor(1000 + random() * 7000)::int;
