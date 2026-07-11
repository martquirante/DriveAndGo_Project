using System;

namespace DriveAndGo_Admin.Helpers
{
    /// <summary>
    /// Formerly handled direct MySQL data reconciliation.
    /// In the centralized architecture, all reconciliation is managed automatically by DriveAndGo_API.
    /// </summary>
    public static class AdminDataHelper
    {
        public static int ReconcilePaidRentalTransactions(string connStr, int? rentalId = null)
        {
            // Handled server-side by DriveAndGo_API
            return 0;
        }
    }
}
