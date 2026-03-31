using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using DriveAndGo_API.Models;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private readonly string _connectionString;

        public TransactionsController(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")!;
        }

        // ══ GET ALL — para sa Admin dashboard ══
        [HttpGet]
        public IActionResult GetTransactions()
        {
            try
            {
                List<Transaction> transactions = new List<Transaction>();

                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT t.transaction_id, t.rental_id, t.amount,
                           t.type, t.method, t.proof_url,
                           t.status, t.paid_at,
                           u.full_name AS customer_name,
                           CONCAT(v.brand, ' ', v.model) AS vehicle_name
                    FROM transactions t
                    JOIN rentals r ON t.rental_id = r.rental_id
                    JOIN users u   ON r.customer_id = u.user_id
                    JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                    ORDER BY t.paid_at DESC", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    transactions.Add(new Transaction
                    {
                        TransactionId = Convert.ToInt32(
                            reader["transaction_id"]),
                        RentalId = Convert.ToInt32(reader["rental_id"]),
                        Amount = Convert.ToDecimal(reader["amount"]),
                        Type = reader["type"].ToString(),
                        Method = reader["method"].ToString(),
                        ProofUrl = reader["proof_url"].ToString(),
                        Status = reader["status"].ToString(),
                        PaidAt = reader["paid_at"] != DBNull.Value
                                        ? Convert.ToDateTime(reader["paid_at"])
                                        : null,
                        CustomerName = reader["customer_name"].ToString(),
                        VehicleName = reader["vehicle_name"].ToString()
                    });
                }

                return Ok(transactions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ GET BY RENTAL ID — para sa mobile app ══
        [HttpGet("rental/{rentalId}")]
        public IActionResult GetByRental(int rentalId)
        {
            try
            {
                List<Transaction> transactions = new List<Transaction>();

                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT t.transaction_id, t.rental_id, t.amount,
                           t.type, t.method, t.proof_url,
                           t.status, t.paid_at,
                           u.full_name AS customer_name,
                           CONCAT(v.brand, ' ', v.model) AS vehicle_name
                    FROM transactions t
                    JOIN rentals r  ON t.rental_id = r.rental_id
                    JOIN users u    ON r.customer_id = u.user_id
                    JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                    WHERE t.rental_id = @rental_id
                    ORDER BY t.paid_at DESC", conn);
                cmd.Parameters.AddWithValue("@rental_id", rentalId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    transactions.Add(new Transaction
                    {
                        TransactionId = Convert.ToInt32(
                            reader["transaction_id"]),
                        RentalId = Convert.ToInt32(reader["rental_id"]),
                        Amount = Convert.ToDecimal(reader["amount"]),
                        Type = reader["type"].ToString(),
                        Method = reader["method"].ToString(),
                        ProofUrl = reader["proof_url"].ToString(),
                        Status = reader["status"].ToString(),
                        PaidAt = reader["paid_at"] != DBNull.Value
                                        ? Convert.ToDateTime(reader["paid_at"])
                                        : null,
                        CustomerName = reader["customer_name"].ToString(),
                        VehicleName = reader["vehicle_name"].ToString()
                    });
                }

                return Ok(transactions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ POST — customer nag-submit ng payment ══
        [HttpPost]
        public IActionResult AddTransaction([FromBody] Transaction transaction)
        {
            if (transaction.RentalId == 0 || transaction.Amount <= 0)
                return BadRequest(new
                {
                    message =
                    "RentalId at Amount ay required."
                });

            var validMethods = new[] { "cash", "gcash", "maya", "bank" };
            if (!validMethods.Contains(transaction.Method?.ToLower()))
                return BadRequest(new
                {
                    message =
                    "Valid methods: cash, gcash, maya, bank"
                });

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // Check kung existing ang rental
                var checkCmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM rentals WHERE rental_id = @id",
                    conn);
                checkCmd.Parameters.AddWithValue("@id", transaction.RentalId);
                var exists = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (exists == 0)
                    return NotFound(new { message = "Rental not found." });

                // I-save ang transaction
                var insertCmd = new MySqlCommand(@"
                    INSERT INTO transactions
                        (rental_id, amount, type,
                         method, proof_url, status, paid_at)
                    VALUES
                        (@rental_id, @amount, @type,
                         @method, @proof_url, 'pending', NOW())",
                    conn);

                insertCmd.Parameters.AddWithValue("@rental_id",
                    transaction.RentalId);
                insertCmd.Parameters.AddWithValue("@amount",
                    transaction.Amount);
                insertCmd.Parameters.AddWithValue("@type",
                    transaction.Type ?? "payment");
                insertCmd.Parameters.AddWithValue("@method",
                    transaction.Method?.ToLower() ?? "cash");
                insertCmd.Parameters.AddWithValue("@proof_url",
                    transaction.ProofUrl ?? "");

                insertCmd.ExecuteNonQuery();

                var idCmd = new MySqlCommand(
                    "SELECT LAST_INSERT_ID()", conn);
                int newId = Convert.ToInt32(idCmd.ExecuteScalar());

                return Ok(new
                {
                    message = "Payment submitted! " +
                              "Hintayin ang confirmation ni Admin.",
                    transaction_id = newId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ PATCH CONFIRM — admin nag-confirm ng payment ══
        [HttpPatch("{id}/confirm")]
        public IActionResult ConfirmPayment(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // Kunin ang transaction + rental info
                var checkCmd = new MySqlCommand(@"
                    SELECT t.status, t.rental_id
                    FROM transactions t
                    WHERE t.transaction_id = @id", conn);
                checkCmd.Parameters.AddWithValue("@id", id);

                using var reader = checkCmd.ExecuteReader();
                if (!reader.Read())
                    return NotFound(new
                    {
                        message =
                        "Transaction not found."
                    });

                string currentStatus = reader["status"].ToString()!;
                int rentalId = Convert.ToInt32(reader["rental_id"]);
                reader.Close();

                if (currentStatus != "pending")
                    return BadRequest(new
                    {
                        message =
                        $"Hindi ma-confirm — status is '{currentStatus}'."
                    });

                // I-confirm ang transaction
                var confirmCmd = new MySqlCommand(@"
                    UPDATE transactions
                    SET status = 'confirmed'
                    WHERE transaction_id = @id", conn);
                confirmCmd.Parameters.AddWithValue("@id", id);
                confirmCmd.ExecuteNonQuery();

                // I-update ang payment_status ng rental → paid
                var rentalCmd = new MySqlCommand(@"
                    UPDATE rentals
                    SET payment_status = 'paid'
                    WHERE rental_id = @rental_id", conn);
                rentalCmd.Parameters.AddWithValue("@rental_id", rentalId);
                rentalCmd.ExecuteNonQuery();

                return Ok(new
                {
                    message = "Payment confirmed! " +
                              "Rental payment status updated to paid.",
                    transaction_id = id,
                    rental_id = rentalId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ PATCH REJECT — admin nag-reject ng payment ══
        [HttpPatch("{id}/reject")]
        public IActionResult RejectPayment(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var checkCmd = new MySqlCommand(@"
                    SELECT status FROM transactions
                    WHERE transaction_id = @id", conn);
                checkCmd.Parameters.AddWithValue("@id", id);
                var status = checkCmd.ExecuteScalar()?.ToString();

                if (status == null)
                    return NotFound(new
                    {
                        message =
                        "Transaction not found."
                    });

                if (status != "pending")
                    return BadRequest(new
                    {
                        message =
                        $"Hindi ma-reject — status is '{status}'."
                    });

                var rejectCmd = new MySqlCommand(@"
                    UPDATE transactions
                    SET status = 'rejected'
                    WHERE transaction_id = @id", conn);
                rejectCmd.Parameters.AddWithValue("@id", id);
                rejectCmd.ExecuteNonQuery();

                return Ok(new
                {
                    message = "Payment rejected. " +
                              "Pakiulit ang payment ng customer.",
                    transaction_id = id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }

        // ══ GET SUMMARY — para sa Admin reports ══
        [HttpGet("summary")]
        public IActionResult GetSummary([FromQuery] string period = "monthly")
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                string groupBy = period == "daily"
                    ? "DATE(paid_at)"
                    : period == "yearly"
                    ? "YEAR(paid_at)"
                    : "DATE_FORMAT(paid_at, '%Y-%m')";

                string label = period == "daily"
                    ? "DATE(paid_at)"
                    : period == "yearly"
                    ? "YEAR(paid_at)"
                    : "DATE_FORMAT(paid_at, '%Y-%m')";

                var cmd = new MySqlCommand($@"
                    SELECT {label} AS period,
                           COUNT(*) AS total_transactions,
                           SUM(amount) AS total_amount
                    FROM transactions
                    WHERE status = 'confirmed'
                    GROUP BY {groupBy}
                    ORDER BY period DESC
                    LIMIT 12", conn);

                var results = new List<object>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(new
                    {
                        period = reader["period"].ToString(),
                        total_transactions = Convert.ToInt32(
                            reader["total_transactions"]),
                        total_amount = Convert.ToDecimal(
                            reader["total_amount"])
                    });
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "DB Error: " + ex.Message });
            }
        }
    }
}