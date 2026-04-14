using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DriveAndGo_Admin.Helpers
{
    public static class AdminDataHelper
    {
        public static int ReconcilePaidRentalTransactions(string connStr, int? rentalId = null)
        {
            using var conn = new MySqlConnection(connStr);
            conn.Open();

            int changed = NormalizeActiveTransactions(conn, rentalId);
            changed += InsertMissingPaidRentalTransactions(conn, rentalId);
            return changed;
        }

        public static int EnsurePaidRentalTransaction(MySqlConnection conn, int rentalId)
        {
            int changed = NormalizeActiveTransactions(conn, rentalId);

            const string findSql = @"
                SELECT transaction_id
                FROM transactions
                WHERE rental_id = @rental_id
                  AND (
                        CASE
                            WHEN LOWER(COALESCE(type, '')) IN ('', 'rental') THEN 'payment'
                            ELSE LOWER(COALESCE(type, ''))
                        END
                      ) = 'payment'
                  AND LOWER(COALESCE(status, '')) NOT IN ('rejected', 'refunded', 'duplicate')
                ORDER BY
                    CASE LOWER(COALESCE(status, ''))
                        WHEN 'paid' THEN 0
                        WHEN 'confirmed' THEN 1
                        WHEN 'verified' THEN 2
                        WHEN 'pending' THEN 3
                        WHEN '' THEN 4
                        ELSE 5
                    END,
                    COALESCE(paid_at, NOW()) DESC,
                    transaction_id DESC
                LIMIT 1";

            using var findCmd = new MySqlCommand(findSql, conn);
            findCmd.Parameters.AddWithValue("@rental_id", rentalId);
            object existingId = findCmd.ExecuteScalar();

            if (existingId != null && existingId != DBNull.Value)
            {
                const string updateSql = @"
                    UPDATE transactions t
                    JOIN rentals r ON r.rental_id = t.rental_id
                    SET t.amount = CASE
                            WHEN COALESCE(t.amount, 0) > 0 THEN t.amount
                            ELSE COALESCE(r.total_amount, 0)
                        END,
                        t.type = 'payment',
                        t.method = LOWER(COALESCE(NULLIF(t.method, ''), NULLIF(r.payment_method, ''), 'cash')),
                        t.status = CASE
                            WHEN LOWER(COALESCE(t.status, '')) = 'paid' THEN 'paid'
                            ELSE 'confirmed'
                        END,
                        t.paid_at = COALESCE(t.paid_at, NOW())
                    WHERE t.transaction_id = @transaction_id";

                using var updateCmd = new MySqlCommand(updateSql, conn);
                updateCmd.Parameters.AddWithValue("@transaction_id", Convert.ToInt32(existingId, CultureInfo.InvariantCulture));
                changed += updateCmd.ExecuteNonQuery();
                return changed;
            }

            const string insertSql = @"
                INSERT INTO transactions (rental_id, amount, type, method, status, paid_at)
                SELECT rental_id,
                       COALESCE(total_amount, 0),
                       'payment',
                       LOWER(COALESCE(NULLIF(payment_method, ''), 'cash')),
                       'confirmed',
                       NOW()
                FROM rentals
                WHERE rental_id = @rental_id";

            using var insertCmd = new MySqlCommand(insertSql, conn);
            insertCmd.Parameters.AddWithValue("@rental_id", rentalId);
            changed += insertCmd.ExecuteNonQuery();
            return changed;
        }

        private static int InsertMissingPaidRentalTransactions(MySqlConnection conn, int? rentalId)
        {
            const string sql = @"
                INSERT INTO transactions
                    (rental_id, amount, type, method, status, paid_at)
                SELECT
                    r.rental_id,
                    COALESCE(r.total_amount, 0),
                    'payment',
                    LOWER(COALESCE(NULLIF(r.payment_method, ''), 'cash')),
                    'confirmed',
                    COALESCE(r.start_date, r.created_at, NOW())
                FROM rentals r
                LEFT JOIN transactions t
                    ON t.rental_id = r.rental_id
                   AND (
                        CASE
                            WHEN LOWER(COALESCE(t.type, '')) IN ('', 'rental') THEN 'payment'
                            ELSE LOWER(COALESCE(t.type, ''))
                        END
                       ) = 'payment'
                   AND LOWER(COALESCE(t.status, '')) NOT IN ('rejected', 'refunded', 'duplicate')
                WHERE LOWER(COALESCE(r.payment_status, '')) = 'paid'
                  AND t.transaction_id IS NULL
                  AND (@rental_id IS NULL OR r.rental_id = @rental_id)";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@rental_id", rentalId.HasValue ? rentalId.Value : DBNull.Value);
            return cmd.ExecuteNonQuery();
        }

        private static int NormalizeActiveTransactions(MySqlConnection conn, int? rentalId)
        {
            const string normalizeSql = @"
                UPDATE transactions t
                LEFT JOIN rentals r ON r.rental_id = t.rental_id
                SET t.amount = CASE
                        WHEN COALESCE(t.amount, 0) > 0 THEN t.amount
                        ELSE COALESCE(r.total_amount, 0)
                    END,
                    t.type = CASE
                        WHEN LOWER(COALESCE(t.type, '')) IN ('', 'rental') THEN 'payment'
                        ELSE LOWER(COALESCE(t.type, ''))
                    END,
                    t.method = LOWER(COALESCE(NULLIF(t.method, ''), NULLIF(r.payment_method, ''), 'cash')),
                    t.proof_url = NULLIF(TRIM(COALESCE(t.proof_url, '')), ''),
                    t.status = CASE
                        WHEN LOWER(COALESCE(t.status, '')) = 'paid' THEN 'paid'
                        WHEN LOWER(COALESCE(t.status, '')) IN ('confirmed', 'verified') THEN 'confirmed'
                        WHEN LOWER(COALESCE(r.payment_status, '')) = 'paid' THEN 'confirmed'
                        WHEN LOWER(COALESCE(t.status, '')) = '' THEN 'pending'
                        ELSE LOWER(COALESCE(t.status, ''))
                    END,
                    t.paid_at = COALESCE(t.paid_at, r.start_date, r.created_at, NOW())
                WHERE LOWER(COALESCE(t.status, '')) NOT IN ('rejected', 'refunded', 'duplicate')
                  AND (@rental_id IS NULL OR t.rental_id = @rental_id)";

            const string markDuplicatesSql = @"
                UPDATE transactions t
                JOIN transactions k
                  ON k.transaction_id <> t.transaction_id
                 AND k.rental_id = t.rental_id
                 AND LOWER(COALESCE(k.status, '')) NOT IN ('rejected', 'refunded', 'duplicate')
                 AND LOWER(COALESCE(t.status, '')) NOT IN ('rejected', 'refunded', 'duplicate')
                 AND LOWER(COALESCE(k.type, '')) = LOWER(COALESCE(t.type, ''))
                 AND ABS(COALESCE(k.amount, 0) - COALESCE(t.amount, 0)) < 0.01
                 AND LOWER(COALESCE(k.method, '')) = LOWER(COALESCE(t.method, ''))
                 AND COALESCE(NULLIF(TRIM(COALESCE(k.proof_url, '')), ''), '') = COALESCE(NULLIF(TRIM(COALESCE(t.proof_url, '')), ''), '')
                 AND (
                        CASE LOWER(COALESCE(k.status, ''))
                            WHEN 'paid' THEN 0
                            WHEN 'confirmed' THEN 1
                            WHEN 'pending' THEN 2
                            ELSE 3
                        END <
                        CASE LOWER(COALESCE(t.status, ''))
                            WHEN 'paid' THEN 0
                            WHEN 'confirmed' THEN 1
                            WHEN 'pending' THEN 2
                            ELSE 3
                        END
                      OR (
                            CASE LOWER(COALESCE(k.status, ''))
                                WHEN 'paid' THEN 0
                                WHEN 'confirmed' THEN 1
                                WHEN 'pending' THEN 2
                                ELSE 3
                            END =
                            CASE LOWER(COALESCE(t.status, ''))
                                WHEN 'paid' THEN 0
                                WHEN 'confirmed' THEN 1
                                WHEN 'pending' THEN 2
                                ELSE 3
                            END
                        AND COALESCE(k.paid_at, TIMESTAMP('1000-01-01 00:00:00')) > COALESCE(t.paid_at, TIMESTAMP('1000-01-01 00:00:00'))
                      )
                      OR (
                            CASE LOWER(COALESCE(k.status, ''))
                                WHEN 'paid' THEN 0
                                WHEN 'confirmed' THEN 1
                                WHEN 'pending' THEN 2
                                ELSE 3
                            END =
                            CASE LOWER(COALESCE(t.status, ''))
                                WHEN 'paid' THEN 0
                                WHEN 'confirmed' THEN 1
                                WHEN 'pending' THEN 2
                                ELSE 3
                            END
                        AND COALESCE(k.paid_at, TIMESTAMP('1000-01-01 00:00:00')) = COALESCE(t.paid_at, TIMESTAMP('1000-01-01 00:00:00'))
                        AND k.transaction_id > t.transaction_id
                      )
                 )
                SET t.status = 'duplicate'
                WHERE (@rental_id IS NULL OR t.rental_id = @rental_id)";

            int changed = 0;

            using (var normalizeCmd = new MySqlCommand(normalizeSql, conn))
            {
                normalizeCmd.Parameters.AddWithValue("@rental_id", rentalId.HasValue ? rentalId.Value : DBNull.Value);
                changed += normalizeCmd.ExecuteNonQuery();
            }

            using (var duplicateCmd = new MySqlCommand(markDuplicatesSql, conn))
            {
                duplicateCmd.Parameters.AddWithValue("@rental_id", rentalId.HasValue ? rentalId.Value : DBNull.Value);
                changed += duplicateCmd.ExecuteNonQuery();
            }

            return changed;
        }

        private static int NormalizeKeeper(MySqlConnection conn, TransactionRow row)
        {
            const string sql = @"
                UPDATE transactions
                SET amount = @amount,
                    type = @type,
                    method = @method,
                    proof_url = NULLIF(@proof_url, ''),
                    status = @status,
                    paid_at = COALESCE(paid_at, @paid_at)
                WHERE transaction_id = @id";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@amount", row.Amount);
            cmd.Parameters.AddWithValue("@type", row.Type);
            cmd.Parameters.AddWithValue("@method", row.Method);
            cmd.Parameters.AddWithValue("@proof_url", row.ProofUrl);
            cmd.Parameters.AddWithValue("@status", GetCanonicalStatus(row));
            cmd.Parameters.AddWithValue("@paid_at", row.PaidAt ?? DateTime.Now);
            cmd.Parameters.AddWithValue("@id", row.TransactionId);
            return cmd.ExecuteNonQuery();
        }

        private static int UpdateTransactionStatus(MySqlConnection conn, int transactionId, string newStatus)
        {
            const string sql = @"
                UPDATE transactions
                SET status = @status
                WHERE transaction_id = @id
                  AND LOWER(COALESCE(status, '')) <> @status";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@status", newStatus);
            cmd.Parameters.AddWithValue("@id", transactionId);
            return cmd.ExecuteNonQuery();
        }

        private static int CompareRows(TransactionRow x, TransactionRow y)
        {
            int statusCompare = GetStatusRank(x.Status).CompareTo(GetStatusRank(y.Status));
            if (statusCompare != 0)
                return statusCompare;

            int explicitTypeCompare = GetTypeRank(x.RawType).CompareTo(GetTypeRank(y.RawType));
            if (explicitTypeCompare != 0)
                return explicitTypeCompare;

            DateTime xPaidAt = x.PaidAt ?? DateTime.MinValue;
            DateTime yPaidAt = y.PaidAt ?? DateTime.MinValue;
            int paidAtCompare = yPaidAt.CompareTo(xPaidAt);
            if (paidAtCompare != 0)
                return paidAtCompare;

            return y.TransactionId.CompareTo(x.TransactionId);
        }

        private static string BuildGroupKey(TransactionRow row) =>
            string.Join("|",
                row.RentalId.ToString(CultureInfo.InvariantCulture),
                row.Type,
                row.Amount.ToString("0.##", CultureInfo.InvariantCulture),
                row.Method,
                row.ProofUrl);

        private static decimal NormalizeAmount(decimal amount, decimal rentalTotalAmount) =>
            amount > 0 ? amount : Math.Max(0, rentalTotalAmount);

        private static string NormalizeTransactionType(string type)
        {
            string normalized = type?.Trim().ToLowerInvariant() ?? "payment";
            return normalized switch
            {
                "" => "payment",
                "rental" => "payment",
                _ => normalized
            };
        }

        private static string NormalizeMethod(string method, string fallbackMethod) =>
            !string.IsNullOrWhiteSpace(method)
                ? method.Trim().ToLowerInvariant()
                : !string.IsNullOrWhiteSpace(fallbackMethod)
                    ? fallbackMethod.Trim().ToLowerInvariant()
                    : "cash";

        private static string NormalizeProofUrl(string proofUrl) =>
            string.IsNullOrWhiteSpace(proofUrl) ? string.Empty : proofUrl.Trim();

        private static string NormalizeStatus(string status) =>
            string.IsNullOrWhiteSpace(status) ? string.Empty : status.Trim().ToLowerInvariant();

        private static bool IsActiveStatus(string status) =>
            status is not "rejected" and not "refunded" and not "duplicate";

        private static int GetStatusRank(string status) => status switch
        {
            "paid" => 0,
            "confirmed" => 1,
            "verified" => 2,
            "pending" => 3,
            "" => 4,
            _ => 5
        };

        private static int GetTypeRank(string rawType) =>
            string.IsNullOrWhiteSpace(rawType) ? 1 : 0;

        private static string GetCanonicalStatus(TransactionRow row)
        {
            if (row.Status == "paid")
                return "paid";

            if (row.Status is "confirmed" or "verified")
                return "confirmed";

            if (row.RentalPaymentStatus == "paid")
                return "confirmed";

            return row.Status == string.Empty ? "pending" : row.Status;
        }

        private sealed class TransactionRow
        {
            public int TransactionId { get; set; }
            public int RentalId { get; set; }
            public decimal Amount { get; set; }
            public string Type { get; set; } = "payment";
            public string RawType { get; set; } = string.Empty;
            public string Method { get; set; } = "cash";
            public string ProofUrl { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string RentalPaymentStatus { get; set; } = string.Empty;
            public DateTime? PaidAt { get; set; }
        }
    }
}
