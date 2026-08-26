using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;
using System.Text.Json.Serialization;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PromosController : ControllerBase
    {
        private readonly string _connectionString;

        public PromosController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        private void EnsureTableSchema(NpgsqlConnection conn)
        {
            try
            {
                using var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS promo_codes (
                        promo_id SERIAL PRIMARY KEY,
                        code VARCHAR(50) NOT NULL UNIQUE,
                        discount_percentage DECIMAL(5, 2) NOT NULL DEFAULT 0,
                        max_discount_amount DECIMAL(10, 2) NOT NULL DEFAULT 0,
                        is_active BOOLEAN NOT NULL DEFAULT TRUE,
                        expiry_date TIMESTAMP NOT NULL DEFAULT (NOW() + INTERVAL '90 days')
                    );
                    ALTER TABLE promo_codes ADD COLUMN IF NOT EXISTS discount_percentage DECIMAL(5, 2) DEFAULT 0;
                    ALTER TABLE promo_codes ADD COLUMN IF NOT EXISTS max_discount_amount DECIMAL(10, 2) DEFAULT 0;
                    ALTER TABLE promo_codes ADD COLUMN IF NOT EXISTS is_active BOOLEAN DEFAULT TRUE;
                    ALTER TABLE promo_codes ADD COLUMN IF NOT EXISTS expiry_date TIMESTAMP DEFAULT (NOW() + INTERVAL '90 days');

                    DO $$
                    BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promo_codes' AND column_name = 'discount_value') THEN
                            ALTER TABLE promo_codes ALTER COLUMN discount_value DROP NOT NULL;
                        END IF;
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promo_codes' AND column_name = 'discount_type') THEN
                            ALTER TABLE promo_codes ALTER COLUMN discount_type DROP NOT NULL;
                        END IF;
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promo_codes' AND column_name = 'valid_until') THEN
                            ALTER TABLE promo_codes ALTER COLUMN valid_until DROP NOT NULL;
                        END IF;
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promo_codes' AND column_name = 'valid_from') THEN
                            ALTER TABLE promo_codes ALTER COLUMN valid_from DROP NOT NULL;
                        END IF;
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promo_codes' AND column_name = 'min_rental_days') THEN
                            ALTER TABLE promo_codes ALTER COLUMN min_rental_days DROP NOT NULL;
                        END IF;
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promo_codes' AND column_name = 'max_uses') THEN
                            ALTER TABLE promo_codes ALTER COLUMN max_uses DROP NOT NULL;
                        END IF;
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promo_codes' AND column_name = 'used_count') THEN
                            ALTER TABLE promo_codes ALTER COLUMN used_count DROP NOT NULL;
                        END IF;
                    END $$;
                ", conn);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }


        [HttpGet]
        public IActionResult GetPromos()
        {
            try
            {
                var list = new List<object>();
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureTableSchema(conn);

                using var cmd = new NpgsqlCommand(@"
                    SELECT 
                        promo_id,
                        code,
                        COALESCE(discount_percentage, 0) AS discount_percentage,
                        COALESCE(max_discount_amount, 0) AS max_discount_amount,
                        COALESCE(is_active, true) AS is_active,
                        COALESCE(expiry_date, NOW() + INTERVAL '30 days') AS expiry_date
                    FROM promo_codes
                    ORDER BY expiry_date DESC", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new
                    {
                        promoId = Convert.ToInt32(reader["promo_id"]),
                        code = reader["code"]?.ToString(),
                        discountPercentage = Convert.ToDecimal(reader["discount_percentage"]),
                        maxDiscountAmount = Convert.ToDecimal(reader["max_discount_amount"]),
                        isActive = Convert.ToBoolean(reader["is_active"]),
                        expiryDate = Convert.ToDateTime(reader["expiry_date"])
                    });
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "DB Error: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult CreatePromo([FromBody] CreatePromoDto req)
        {
            try
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Code))
                {
                    return BadRequest(new { Message = "Promo code cannot be empty." });
                }

                string code = req.Code.Trim().ToUpperInvariant();
                decimal discountPercentage = req.DiscountPercentage > 0 ? req.DiscountPercentage : 10m;
                decimal maxDiscountAmount = req.MaxDiscountAmount >= 0 ? req.MaxDiscountAmount : 0m;
                bool isActive = req.IsActive;
                DateTime expiryDate = req.ExpiryDate != default ? req.ExpiryDate : DateTime.UtcNow.AddDays(30);

                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureTableSchema(conn);

                using var cmd = new NpgsqlCommand(@"
                    DO $$
                    BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promo_codes' AND column_name = 'discount_value') THEN
                            ALTER TABLE promo_codes ALTER COLUMN discount_value DROP NOT NULL;
                        END IF;
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promo_codes' AND column_name = 'discount_type') THEN
                            ALTER TABLE promo_codes ALTER COLUMN discount_type DROP NOT NULL;
                        END IF;
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promo_codes' AND column_name = 'valid_until') THEN
                            ALTER TABLE promo_codes ALTER COLUMN valid_until DROP NOT NULL;
                        END IF;
                    END $$;

                    INSERT INTO promo_codes (code, discount_percentage, max_discount_amount, is_active, expiry_date)
                    VALUES (@code, @discount_percentage, @max_discount_amount, @is_active, @expiry_date)
                    ON CONFLICT (code) DO UPDATE 
                    SET discount_percentage = EXCLUDED.discount_percentage,
                        max_discount_amount = EXCLUDED.max_discount_amount,
                        is_active = EXCLUDED.is_active,
                        expiry_date = EXCLUDED.expiry_date
                    RETURNING promo_id;", conn);


                cmd.Parameters.AddWithValue("@code", code);
                cmd.Parameters.AddWithValue("@discount_percentage", discountPercentage);
                cmd.Parameters.AddWithValue("@max_discount_amount", maxDiscountAmount);
                cmd.Parameters.AddWithValue("@is_active", isActive);
                cmd.Parameters.AddWithValue("@expiry_date", expiryDate);

                int id = Convert.ToInt32(cmd.ExecuteScalar());
                return Ok(new { Message = "Promo code created successfully.", PromoId = id, Code = code });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "DB Error: " + ex.Message });
            }
        }

        // GET /api/promos/ai-suggest - Live Database Analytics Demand Optimizer
        [HttpGet("ai-suggest")]
        public IActionResult GetAiSuggestedPromo()
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();

                // 1. Check fleet metrics
                int totalVehicles = 0;
                int availableVehicles = 0;
                using (var vCmd = new NpgsqlCommand("SELECT COUNT(*), COUNT(*) FILTER (WHERE LOWER(status) = 'available') FROM vehicles", conn))
                using (var vR = vCmd.ExecuteReader())
                {
                    if (vR.Read())
                    {
                        totalVehicles = Convert.ToInt32(vR[0]);
                        availableVehicles = Convert.ToInt32(vR[1]);
                    }
                }

                // 2. Check recent booking volume and average order value
                decimal avgOrderValue = 12000m;
                using (var rCmd = new NpgsqlCommand("SELECT COALESCE(AVG(total_amount), 12000) FROM rentals WHERE created_at >= NOW() - INTERVAL '60 days'", conn))
                {
                    var aovObj = rCmd.ExecuteScalar();
                    if (aovObj != null && aovObj != DBNull.Value)
                    {
                        avgOrderValue = Convert.ToDecimal(aovObj);
                    }
                }

                double idleRatio = totalVehicles > 0 ? (double)availableVehicles / totalVehicles : 0.5;

                string strategyName;
                string codePrefix;
                int discountPct;
                decimal maxCap;
                int expiryDays;
                string rationale;

                if (idleRatio >= 0.60) // High Idle / Low Demand
                {
                    strategyName = "Flash Fleet Boost (High Idle Fleet)";
                    codePrefix = "BOOST";
                    discountPct = 25;
                    maxCap = Math.Round((avgOrderValue * 0.30m) / 500m) * 500m;
                    if (maxCap < 3000m) maxCap = 3500m;
                    expiryDays = 14;
                    rationale = $"High vehicle availability detected ({availableVehicles}/{totalVehicles} cars idle). 25% discount suggested to stimulate bookings.";
                }
                else if (idleRatio <= 0.25 && totalVehicles > 0) // High Demand / Scarcity
                {
                    strategyName = "Prime Peak Season Strategy";
                    codePrefix = "PRIME";
                    discountPct = 10;
                    maxCap = Math.Round((avgOrderValue * 0.15m) / 500m) * 500m;
                    if (maxCap < 1500m) maxCap = 1500m;
                    expiryDays = 30;
                    rationale = $"Peak fleet demand ({totalVehicles - availableVehicles}/{totalVehicles} active). Conservative 10% discount to protect margins.";
                }
                else // Balanced
                {
                    strategyName = "Customer Loyalty & Retention";
                    codePrefix = "LOYALTY";
                    discountPct = 15;
                    maxCap = Math.Round((avgOrderValue * 0.22m) / 500m) * 500m;
                    if (maxCap < 2500m) maxCap = 3000m;
                    expiryDays = 45;
                    rationale = $"Balanced fleet utilization. 15% discount calibrated against Average Order Value of ₱{avgOrderValue:N0}.";
                }

                int randNum = new Random().Next(1000, 9999);
                string suggestedCode = $"{codePrefix}-{randNum}-{discountPct}";

                return Ok(new
                {
                    Success = true,
                    Strategy = strategyName,
                    Code = suggestedCode,
                    DiscountPercentage = discountPct,
                    MaxDiscountAmount = maxCap,
                    ExpiryDays = expiryDays,
                    Rationale = rationale,
                    FleetIdleRate = $"{Math.Round(idleRatio * 100)}%",
                    AverageBookingValue = avgOrderValue
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "AI Calculation Error: " + ex.Message });
            }
        }

        [HttpGet("validate/{code}")]
        public IActionResult ValidatePromo(string code)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureTableSchema(conn);

                using var cmd = new NpgsqlCommand(@"
                    SELECT * FROM promo_codes
                    WHERE UPPER(code) = @code AND is_active = TRUE AND expiry_date > NOW()
                    LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@code", code.ToUpperInvariant());

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return Ok(new
                    {
                        valid = true,
                        promoId = Convert.ToInt32(reader["promo_id"]),
                        code = reader["code"]?.ToString(),
                        discountPercentage = Convert.ToDecimal(reader["discount_percentage"]),
                        maxDiscountAmount = Convert.ToDecimal(reader["max_discount_amount"])
                    });
                }

                return BadRequest(new { valid = false, Message = "Invalid, expired, or inactive promo code." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "DB Error: " + ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public IActionResult DeletePromo(int id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureTableSchema(conn);

                using var cmd = new NpgsqlCommand("DELETE FROM promo_codes WHERE promo_id = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);

                int affected = cmd.ExecuteNonQuery();
                if (affected == 0) return NotFound(new { Message = "Promo code not found." });

                return Ok(new { Message = "Promo code deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "DB Error: " + ex.Message });
            }
        }
    }

    public class CreatePromoDto
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("discountPercentage")]
        public decimal DiscountPercentage { get; set; }

        [JsonPropertyName("maxDiscountAmount")]
        public decimal MaxDiscountAmount { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;

        [JsonPropertyName("expiryDate")]
        public DateTime ExpiryDate { get; set; }
    }
}
