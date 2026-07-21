using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;
using System.Text.Json;

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

        [HttpGet]
        public IActionResult GetPromos()
        {
            try
            {
                var list = new List<object>();
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();

                using var cmd = new NpgsqlCommand(@"
                    SELECT * FROM promo_codes
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
        public IActionResult CreatePromo([FromBody] dynamic payload)
        {
            try
            {
                var element = (JsonElement)payload;
                string code = element.GetProperty("code").GetString()!.ToUpperInvariant();
                decimal discountPercentage = element.GetProperty("discountPercentage").GetDecimal();
                decimal maxDiscountAmount = element.GetProperty("maxDiscountAmount").GetDecimal();
                bool isActive = element.TryGetProperty("isActive", out var ia) ? ia.GetBoolean() : true;
                DateTime expiryDate = element.GetProperty("expiryDate").GetDateTime();

                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();

                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO promo_codes (code, discount_percentage, max_discount_amount, is_active, expiry_date)
                    VALUES (@code, @discount_percentage, @max_discount_amount, @is_active, @expiry_date)
                    RETURNING promo_id;", conn);

                cmd.Parameters.AddWithValue("@code", code);
                cmd.Parameters.AddWithValue("@discount_percentage", discountPercentage);
                cmd.Parameters.AddWithValue("@max_discount_amount", maxDiscountAmount);
                cmd.Parameters.AddWithValue("@is_active", isActive);
                cmd.Parameters.AddWithValue("@expiry_date", expiryDate);

                int id = Convert.ToInt32(cmd.ExecuteScalar());
                return Ok(new { Message = "Promo code created successfully.", PromoId = id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "DB Error: " + ex.Message });
            }
        }

        [HttpGet("validate/{code}")]
        public IActionResult ValidatePromo(string code)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();

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
}
