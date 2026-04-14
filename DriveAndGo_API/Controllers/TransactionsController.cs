using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using DriveAndGo_API.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private readonly string _connectionString;

        public TransactionsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

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
                    WHERE LOWER(COALESCE(t.status, '')) <> 'duplicate'
                    ORDER BY t.paid_at DESC", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    transactions.Add(new Transaction
                    {
                        TransactionId = Convert.ToInt32(reader["transaction_id"]),
                        RentalId = Convert.ToInt32(reader["rental_id"]),
                        Amount = Convert.ToDecimal(reader["amount"]),
                        Type = reader["type"].ToString(),
                        Method = reader["method"].ToString(),
                        ProofUrl = reader["proof_url"].ToString(),
                        Status = reader["status"].ToString(),
                        PaidAt = reader["paid_at"] != DBNull.Value ? Convert.ToDateTime(reader["paid_at"]) : null,
                        CustomerName = reader["customer_name"].ToString(),
                        VehicleName = reader["vehicle_name"].ToString()
                    });
                }
                return Ok(DeduplicateTransactions(transactions));
            }
            catch (Exception ex) { return StatusCode(500, new { message = "DB Error: " + ex.Message }); }
        }

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
                      AND LOWER(COALESCE(t.status, '')) <> 'duplicate'
                    ORDER BY t.paid_at DESC", conn);
                cmd.Parameters.AddWithValue("@rental_id", rentalId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    transactions.Add(new Transaction
                    {
                        TransactionId = Convert.ToInt32(reader["transaction_id"]),
                        RentalId = Convert.ToInt32(reader["rental_id"]),
                        Amount = Convert.ToDecimal(reader["amount"]),
                        Type = reader["type"].ToString(),
                        Method = reader["method"].ToString(),
                        ProofUrl = reader["proof_url"].ToString(),
                        Status = reader["status"].ToString(),
                        PaidAt = reader["paid_at"] != DBNull.Value ? Convert.ToDateTime(reader["paid_at"]) : null,
                        CustomerName = reader["customer_name"].ToString(),
                        VehicleName = reader["vehicle_name"].ToString()
                    });
                }
                return Ok(DeduplicateTransactions(transactions));
            }
            catch (Exception ex) { return StatusCode(500, new { message = "DB Error: " + ex.Message }); }
        }

        [HttpPost]
        public IActionResult AddTransaction([FromBody] Transaction transaction)
        {
            if (transaction.RentalId == 0 || transaction.Amount <= 0)
                return BadRequest(new { message = "RentalId at Amount ay required." });

            string requestedMethod = NormalizeMethod(transaction.Method);
            var validMethods = new[] { "cash", "gcash", "maya", "bank" };
            if (!validMethods.Contains(requestedMethod))
                return BadRequest(new { message = "Valid methods: cash, gcash, maya, bank" });

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // 🔴 ANTI-SPAM BLOCKER 🔴
                var pendingCmd = new MySqlCommand(@"
                    SELECT COUNT(*) FROM transactions 
                    WHERE rental_id = @rental_id 
                      AND LOWER(status) = 'pending'", conn);
                pendingCmd.Parameters.AddWithValue("@rental_id", transaction.RentalId);

                if (Convert.ToInt32(pendingCmd.ExecuteScalar()) > 0)
                {
                    return Conflict(new { message = "May nakabinbin (pending) na payment ka na para sa rental na ito. Hintayin muna ang confirmation ni Admin bago mag-submit ulit." });
                }

                string normalizedType = NormalizeTransactionType(transaction.Type);
                string normalizedMethod = requestedMethod;
                string normalizedProof = NormalizeProofUrl(transaction.ProofUrl);

                var rentalCmd = new MySqlCommand("SELECT payment_status FROM rentals WHERE rental_id = @id LIMIT 1", conn);
                rentalCmd.Parameters.AddWithValue("@id", transaction.RentalId);
                string? paymentStatus = rentalCmd.ExecuteScalar()?.ToString();

                if (paymentStatus == null) return NotFound(new { message = "Rental not found." });

                var existingTx = FindExistingTransaction(conn, transaction.RentalId, transaction.Amount, normalizedType, normalizedMethod, normalizedProof);

                if (existingTx.HasValue)
                {
                    bool alreadyFinalized = existingTx.Value.Status is "confirmed" or "paid" or "verified";
                    return Ok(new
                    {
                        message = alreadyFinalized ? "Payment already exists for this rental." : "Payment was already submitted earlier.",
                        transaction_id = existingTx.Value.TransactionId,
                        duplicate_prevented = true
                    });
                }

                if (normalizedType == "payment" && string.Equals(paymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
                {
                    return Conflict(new { message = "This rental is already marked as paid." });
                }

                var insertCmd = new MySqlCommand(@"
                    INSERT INTO transactions (rental_id, amount, type, method, proof_url, status, paid_at)
                    VALUES (@rental_id, @amount, @type, @method, @proof_url, 'pending', NOW())", conn);

                insertCmd.Parameters.AddWithValue("@rental_id", transaction.RentalId);
                insertCmd.Parameters.AddWithValue("@amount", transaction.Amount);
                insertCmd.Parameters.AddWithValue("@type", normalizedType);
                insertCmd.Parameters.AddWithValue("@method", normalizedMethod);
                insertCmd.Parameters.AddWithValue("@proof_url", normalizedProof);
                insertCmd.ExecuteNonQuery();

                int newId = Convert.ToInt32(new MySqlCommand("SELECT LAST_INSERT_ID()", conn).ExecuteScalar());

                return Ok(new { message = "Payment submitted! Hintayin ang confirmation ni Admin.", transaction_id = newId });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "DB Error: " + ex.Message }); }
        }

        [HttpPatch("{id}/confirm")]
        public IActionResult ConfirmPayment(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var checkCmd = new MySqlCommand("SELECT t.status, t.rental_id FROM transactions t WHERE t.transaction_id = @id", conn);
                checkCmd.Parameters.AddWithValue("@id", id);
                using var reader = checkCmd.ExecuteReader();

                if (!reader.Read()) return NotFound(new { message = "Transaction not found." });

                string currentStatus = reader["status"].ToString()!;
                int rentalId = Convert.ToInt32(reader["rental_id"]);
                reader.Close();

                if (!IsConfirmableStatus(currentStatus))
                    return BadRequest(new { message = $"Hindi ma-confirm — status is '{currentStatus}'." });

                var confirmCmd = new MySqlCommand("UPDATE transactions SET status = 'confirmed' WHERE transaction_id = @id", conn);
                confirmCmd.Parameters.AddWithValue("@id", id);
                confirmCmd.ExecuteNonQuery();

                var rentalCmd = new MySqlCommand("UPDATE rentals SET payment_status = 'paid' WHERE rental_id = @rental_id", conn);
                rentalCmd.Parameters.AddWithValue("@rental_id", rentalId);
                rentalCmd.ExecuteNonQuery();

                return Ok(new { message = "Payment confirmed! Rental payment status updated to paid.", transaction_id = id, rental_id = rentalId });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "DB Error: " + ex.Message }); }
        }

        [HttpPatch("{id}/reject")]
        public IActionResult RejectPayment(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var checkCmd = new MySqlCommand("SELECT status FROM transactions WHERE transaction_id = @id", conn);
                checkCmd.Parameters.AddWithValue("@id", id);
                var status = checkCmd.ExecuteScalar()?.ToString();

                if (status == null) return NotFound(new { message = "Transaction not found." });
                if (!IsConfirmableStatus(status)) return BadRequest(new { message = $"Hindi ma-reject — status is '{status}'." });

                var rejectCmd = new MySqlCommand("UPDATE transactions SET status = 'rejected' WHERE transaction_id = @id", conn);
                rejectCmd.Parameters.AddWithValue("@id", id);
                rejectCmd.ExecuteNonQuery();

                return Ok(new { message = "Payment rejected. Pakiulit ang payment ng customer.", transaction_id = id });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "DB Error: " + ex.Message }); }
        }

        [HttpGet("summary")]
        public IActionResult GetSummary([FromQuery] string period = "monthly")
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                string groupBy = period == "daily" ? "DATE(paid_at)" : period == "yearly" ? "YEAR(paid_at)" : "DATE_FORMAT(paid_at, '%Y-%m')";
                string label = period == "daily" ? "DATE(paid_at)" : period == "yearly" ? "YEAR(paid_at)" : "DATE_FORMAT(paid_at, '%Y-%m')";

                var cmd = new MySqlCommand($@"
                    SELECT {label} AS period, COUNT(*) AS total_transactions, SUM(amount) AS total_amount
                    FROM transactions
                    WHERE LOWER(COALESCE(status, '')) IN ('confirmed', 'paid', 'verified')
                    GROUP BY {groupBy} ORDER BY period DESC LIMIT 12", conn);

                var results = new List<object>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(new
                    {
                        period = reader["period"].ToString(),
                        total_transactions = Convert.ToInt32(reader["total_transactions"]),
                        total_amount = Convert.ToDecimal(reader["total_amount"])
                    });
                }
                return Ok(results);
            }
            catch (Exception ex) { return StatusCode(500, new { message = "DB Error: " + ex.Message }); }
        }

        private static string NormalizeTransactionType(string? type)
        {
            string normalized = type?.Trim().ToLowerInvariant() ?? "payment";
            return normalized switch { "" => "payment", "rental" => "payment", _ => normalized };
        }
        private static string NormalizeMethod(string? method) => string.IsNullOrWhiteSpace(method) ? "cash" : method.Trim().ToLowerInvariant();
        private static string NormalizeProofUrl(string? proofUrl) => string.IsNullOrWhiteSpace(proofUrl) ? string.Empty : proofUrl.Trim();
        private static bool IsConfirmableStatus(string? status) { string normalized = status?.Trim().ToLowerInvariant() ?? string.Empty; return normalized is "pending" or "verified" or ""; }

        private static (int TransactionId, string Status)? FindExistingTransaction(MySqlConnection conn, int rentalId, decimal amount, string type, string method, string proofUrl)
        {
            const string sql = @"
                SELECT transaction_id, LOWER(COALESCE(status, '')) AS normalized_status FROM transactions
                WHERE rental_id = @rental_id AND ABS(COALESCE(amount, 0) - @amount) < 0.01
                  AND (CASE WHEN LOWER(COALESCE(type, '')) IN ('', 'rental') THEN 'payment' ELSE LOWER(COALESCE(type, '')) END) = @type
                  AND LOWER(COALESCE(method, '')) = @method AND COALESCE(NULLIF(TRIM(COALESCE(proof_url, '')), ''), '') = @proof_url
                  AND LOWER(COALESCE(status, '')) NOT IN ('rejected', 'refunded', 'duplicate')
                ORDER BY CASE LOWER(COALESCE(status, '')) WHEN 'paid' THEN 0 WHEN 'confirmed' THEN 1 WHEN 'verified' THEN 2 WHEN 'pending' THEN 3 WHEN '' THEN 4 ELSE 5 END, transaction_id DESC LIMIT 1";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@rental_id", rentalId);
            cmd.Parameters.AddWithValue("@amount", amount);
            cmd.Parameters.AddWithValue("@type", type);
            cmd.Parameters.AddWithValue("@method", method);
            cmd.Parameters.AddWithValue("@proof_url", proofUrl);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return (Convert.ToInt32(reader["transaction_id"]), reader["normalized_status"]?.ToString() ?? string.Empty);
        }

        private static List<Transaction> DeduplicateTransactions(IEnumerable<Transaction> transactions)
        {
            return transactions.GroupBy(BuildTransactionKey, StringComparer.OrdinalIgnoreCase).Select(group => group
                .OrderBy(tx => GetStatusRank(NormalizeStatus(tx.Status)))
                .ThenBy(tx => string.IsNullOrWhiteSpace(tx.Type) ? 1 : 0)
                .ThenByDescending(tx => tx.PaidAt ?? DateTime.MinValue).ThenByDescending(tx => tx.TransactionId).First())
                .OrderByDescending(tx => tx.PaidAt ?? DateTime.MinValue).ThenByDescending(tx => tx.TransactionId).ToList();
        }

        private static string BuildTransactionKey(Transaction tx) => string.Join("|", tx.RentalId.ToString(), NormalizeTransactionType(tx.Type), tx.Amount.ToString("0.##", CultureInfo.InvariantCulture), NormalizeMethod(tx.Method), NormalizeProofUrl(tx.ProofUrl));
        private static string NormalizeStatus(string? status) => string.IsNullOrWhiteSpace(status) ? string.Empty : status.Trim().ToLowerInvariant();
        private static int GetStatusRank(string status) => status switch { "paid" => 0, "confirmed" => 1, "verified" => 2, "pending" => 3, "" => 4, _ => 5 };
    }
}