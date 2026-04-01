namespace DriveAndGo_Admin.Models
{
    public class DashboardStats
    {
        public decimal TotalRevenue { get; set; }
        public int ActiveRentals { get; set; }
        public int TotalCustomers { get; set; }
        public int FleetSize { get; set; }
        public int PendingBookings { get; set; }
        public int PendingPayments { get; set; }
    }
}