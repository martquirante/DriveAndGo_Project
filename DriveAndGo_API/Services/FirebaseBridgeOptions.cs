namespace DriveAndGo_API.Services
{
    public sealed class FirebaseBridgeOptions
    {
        public string DatabaseUrl { get; set; } = "";
        public string? Secret { get; set; }
        public int MirrorPollingSeconds { get; set; } = 2;
        public string RentalsNode { get; set; } = "rentals";
        public string VehiclesNode { get; set; } = "vehicles";
        public string UsersNode { get; set; } = "users";
        public string DriversNode { get; set; } = "drivers";
        public string TransactionsNode { get; set; } = "transactions";
        public string RatingsNode { get; set; } = "ratings";
        public string MessagesNode { get; set; } = "messages";
        public string NotificationsNode { get; set; } = "notifications";
        public string IssuesNode { get; set; } = "issues";
        public string ExtensionsNode { get; set; } = "extensions";
        public string VehicleLocationsNode { get; set; } = "vehicle_locations";
        public string LocationLogsNode { get; set; } = "location_logs";
    }
}