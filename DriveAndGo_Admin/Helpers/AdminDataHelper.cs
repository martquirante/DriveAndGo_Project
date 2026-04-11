using MySql.Data.MySqlClient;

namespace DriveAndGo_Admin.Helpers
{
    public static class AdminDataHelper
    {
        public static int ReconcilePaidRentalTransactions(string connStr, int? rentalId = null)
        {
            using var conn = new MySqlConnection(connStr);
            conn.Open();

            const string sql = @"
                INSERT INTO transactions
                    (rental_id, amount, type, method, status, paid_at)
                SELECT
                    r.rental_id,
                    COALESCE(r.total_amount, 0),
                    'rental',
                    LOWER(COALESCE(NULLIF(r.payment_method, ''), 'cash')),
                    'confirmed',
                    COALESCE(r.start_date, r.created_at, NOW())
                FROM rentals r
                LEFT JOIN transactions t
                    ON t.rental_id = r.rental_id
                   AND LOWER(COALESCE(t.status, '')) IN ('confirmed', 'paid')
                WHERE LOWER(COALESCE(r.payment_status, '')) = 'paid'
                  AND t.transaction_id IS NULL
                  AND (@rental_id IS NULL OR r.rental_id = @rental_id)";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@rental_id", rentalId.HasValue ? rentalId.Value : DBNull.Value);
            return cmd.ExecuteNonQuery();
        }
    }
}
