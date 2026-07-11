using DriveAndGo_API.Models;
using Microsoft.EntityFrameworkCore;

namespace DriveAndGo_API.Data;

/// <summary>
/// EF Core DbContext for DriveAndGo API.
/// Used primarily for:
///   - Schema management: `dotnet ef migrations add` / `dotnet ef database update`
///   - Type-safe queries in new Service classes
///
/// Existing controllers continue to use raw Npgsql ADO.NET via DbService.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ── DbSets ──────────────────────────────────────────────
    public DbSet<User>            Users           { get; set; }
    public DbSet<Driver>          Drivers         { get; set; }
    public DbSet<Vehicle>         Vehicles        { get; set; }
    public DbSet<Rental>          Rentals         { get; set; }
    public DbSet<Transaction>     Transactions    { get; set; }
    public DbSet<Extension>       Extensions      { get; set; }
    public DbSet<Issue>           Issues          { get; set; }
    public DbSet<Rating>          Ratings         { get; set; }
    public DbSet<AppNotification> Notifications   { get; set; }
    public DbSet<LocationLog>     LocationLogs    { get; set; }
    public DbSet<GpsLog>          GpsLogs         { get; set; }
    public DbSet<Message>         Messages        { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── USERS ────────────────────────────────────────────
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.UserId);
            e.Property(u => u.UserId).HasColumnName("user_id").UseIdentityAlwaysColumn();
            e.Property(u => u.FullName).HasColumnName("full_name").HasMaxLength(150).IsRequired();
            e.Property(u => u.Email).HasColumnName("email").HasMaxLength(200).IsRequired();
            e.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
            e.Property(u => u.Phone).HasColumnName("phone").HasMaxLength(30).HasDefaultValue("");
            e.Property(u => u.Role).HasColumnName("role").HasMaxLength(20).HasDefaultValue("customer");
            e.Property(u => u.IdPhotoUrl).HasColumnName("id_photo_url");
            e.Property(u => u.FirebaseUid).HasColumnName("firebase_uid").HasMaxLength(128);
            e.Property(u => u.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            e.HasIndex(u => u.Email).IsUnique();
        });

        // ── DRIVERS ──────────────────────────────────────────
        modelBuilder.Entity<Driver>(e =>
        {
            e.ToTable("drivers");
            e.HasKey(d => d.DriverId);
            e.Property(d => d.DriverId).HasColumnName("driver_id").UseIdentityAlwaysColumn();
            e.Property(d => d.UserId).HasColumnName("user_id").IsRequired();
            e.Property(d => d.LicenseNo).HasColumnName("license_no").HasMaxLength(50).IsRequired();
            e.Property(d => d.LicensePhotoUrl).HasColumnName("license_photo_url");
            e.Property(d => d.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("available");
            e.Property(d => d.RatingAvg).HasColumnName("rating_avg").HasPrecision(3, 2).HasDefaultValue(0.0m);
            e.Property(d => d.TotalTrips).HasColumnName("total_trips").HasDefaultValue(0);
            // Navigation
            e.HasOne<User>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Ignore(d => d.FullName);
            e.Ignore(d => d.Email);
            e.Ignore(d => d.Phone);
        });

        // ── VEHICLES ─────────────────────────────────────────
        modelBuilder.Entity<Vehicle>(e =>
        {
            e.ToTable("vehicles");
            e.HasKey(v => v.VehicleId);
            e.Property(v => v.VehicleId).HasColumnName("vehicle_id").UseIdentityAlwaysColumn();
            e.Property(v => v.PlateNo).HasColumnName("plate_no").HasMaxLength(20).IsRequired();
            e.Property(v => v.Brand).HasColumnName("brand").HasMaxLength(80).IsRequired();
            e.Property(v => v.Model).HasColumnName("model").HasMaxLength(80).IsRequired();
            e.Property(v => v.Type).HasColumnName("type").HasMaxLength(30).HasDefaultValue("Car");
            e.Property(v => v.CC).HasColumnName("cc");
            e.Property(v => v.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("available");
            e.Property(v => v.RatePerDay).HasColumnName("rate_per_day").HasPrecision(10, 2).HasDefaultValue(0m);
            e.Property(v => v.RateWithDriver).HasColumnName("rate_with_driver").HasPrecision(10, 2).HasDefaultValue(0m);
            e.Property(v => v.PhotoUrl).HasColumnName("photo_url").HasDefaultValue("");
            e.Property(v => v.Description).HasColumnName("description").HasDefaultValue("");
            e.Property(v => v.SeatCapacity).HasColumnName("seat_capacity").HasDefaultValue(5);
            e.Property(v => v.Transmission).HasColumnName("transmission").HasMaxLength(20).HasDefaultValue("Automatic");
            e.Property(v => v.Model3dUrl).HasColumnName("model_3d_url").HasDefaultValue("");
            e.Property(v => v.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            e.Property(v => v.Latitude).HasColumnName("latitude");
            e.Property(v => v.Longitude).HasColumnName("longitude");
            e.Property(v => v.CurrentSpeed).HasColumnName("current_speed");
            e.Property(v => v.LastUpdate).HasColumnName("last_update");
            e.Property(v => v.InGarage).HasColumnName("in_garage").HasDefaultValue(true);
            e.HasIndex(v => v.PlateNo).IsUnique();
        });

        // ── RENTALS ──────────────────────────────────────────
        modelBuilder.Entity<Rental>(e =>
        {
            e.ToTable("rentals");
            e.HasKey(r => r.RentalId);
            e.Property(r => r.RentalId).HasColumnName("rental_id").UseIdentityAlwaysColumn();
            e.Property(r => r.CustomerId).HasColumnName("customer_id").IsRequired();
            e.Property(r => r.VehicleId).HasColumnName("vehicle_id").IsRequired();
            e.Property(r => r.DriverId).HasColumnName("driver_id");
            e.Property(r => r.StartDate).HasColumnName("start_date").IsRequired();
            e.Property(r => r.EndDate).HasColumnName("end_date");
            e.Property(r => r.Destination).HasColumnName("destination");
            e.Property(r => r.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("pending");
            e.Property(r => r.TotalAmount).HasColumnName("total_amount").HasPrecision(10, 2).HasDefaultValue(0m);
            e.Property(r => r.PaymentMethod).HasColumnName("payment_method").HasMaxLength(20).HasDefaultValue("cash");
            e.Property(r => r.PaymentStatus).HasColumnName("payment_status").HasMaxLength(20).HasDefaultValue("unpaid");
            e.Property(r => r.QrCode).HasColumnName("qr_code");
            e.Property(r => r.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            // Ignore JOIN-populated display fields
            e.Ignore(r => r.CustomerName);
            e.Ignore(r => r.CustomerPhone);
            e.Ignore(r => r.CustomerEmail);
            e.Ignore(r => r.VehicleName);
            e.Ignore(r => r.VehiclePlateNo);
            e.Ignore(r => r.DriverName);
            e.Ignore(r => r.DriverPhone);
        });

        // ── TRANSACTIONS ─────────────────────────────────────
        modelBuilder.Entity<Transaction>(e =>
        {
            e.ToTable("transactions");
            e.HasKey(t => t.TransactionId);
            e.Property(t => t.TransactionId).HasColumnName("transaction_id").UseIdentityAlwaysColumn();
            e.Property(t => t.RentalId).HasColumnName("rental_id").IsRequired();
            e.Property(t => t.Amount).HasColumnName("amount").HasPrecision(10, 2).HasDefaultValue(0m);
            e.Property(t => t.Type).HasColumnName("type").HasMaxLength(20).HasDefaultValue("payment");
            e.Property(t => t.Method).HasColumnName("method").HasMaxLength(20).HasDefaultValue("cash");
            e.Property(t => t.ProofUrl).HasColumnName("proof_url");
            e.Property(t => t.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("pending");
            e.Property(t => t.PaidAt).HasColumnName("paid_at").HasDefaultValueSql("NOW()");
            e.Ignore(t => t.CustomerName);
            e.Ignore(t => t.VehicleName);
        });

        // ── EXTENSIONS ───────────────────────────────────────
        modelBuilder.Entity<Extension>(e =>
        {
            e.ToTable("extensions");
            e.HasKey(x => x.ExtensionId);
            e.Property(x => x.ExtensionId).HasColumnName("extension_id").UseIdentityAlwaysColumn();
            e.Property(x => x.RentalId).HasColumnName("rental_id").IsRequired();
            e.Property(x => x.AddedDays).HasColumnName("added_days").HasDefaultValue(1);
            e.Property(x => x.AddedFee).HasColumnName("added_fee").HasPrecision(10, 2).HasDefaultValue(0m);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("pending");
            e.Property(x => x.RequestedAt).HasColumnName("requested_at").HasDefaultValueSql("NOW()");
            e.Ignore(x => x.CustomerName);
            e.Ignore(x => x.VehicleName);
        });

        // ── ISSUES ───────────────────────────────────────────
        modelBuilder.Entity<Issue>(e =>
        {
            e.ToTable("issues");
            e.HasKey(i => i.IssueId);
            e.Property(i => i.IssueId).HasColumnName("issue_id").UseIdentityAlwaysColumn();
            e.Property(i => i.RentalId).HasColumnName("rental_id").IsRequired();
            e.Property(i => i.ReporterId).HasColumnName("reporter_id").IsRequired();
            e.Property(i => i.IssueType).HasColumnName("issue_type").HasMaxLength(50).HasDefaultValue("General");
            e.Property(i => i.Description).HasColumnName("description").IsRequired();
            e.Property(i => i.ImageUrl).HasColumnName("image_url");
            e.Property(i => i.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Pending");
            e.Property(i => i.ReportedAt).HasColumnName("reported_at").HasDefaultValueSql("NOW()");
            e.Ignore(i => i.ReporterName);
            e.Ignore(i => i.VehicleName);
        });

        // ── RATINGS ──────────────────────────────────────────
        modelBuilder.Entity<Rating>(e =>
        {
            e.ToTable("ratings");
            e.HasKey(r => r.RatingId);
            e.Property(r => r.RatingId).HasColumnName("rating_id").UseIdentityAlwaysColumn();
            e.Property(r => r.RentalId).HasColumnName("rental_id").IsRequired();
            e.Property(r => r.CustomerId).HasColumnName("customer_id").IsRequired();
            e.Property(r => r.DriverId).HasColumnName("driver_id");
            e.Property(r => r.VehicleId).HasColumnName("vehicle_id").IsRequired();
            e.Property(r => r.DriverScore).HasColumnName("driver_score");
            e.Property(r => r.VehicleScore).HasColumnName("vehicle_score").IsRequired();
            e.Property(r => r.Comment).HasColumnName("comment");
            e.Property(r => r.RatedAt).HasColumnName("rated_at").HasDefaultValueSql("NOW()");
            e.Ignore(r => r.CustomerName);
            e.Ignore(r => r.VehicleName);
        });

        // ── NOTIFICATIONS ────────────────────────────────────
        modelBuilder.Entity<AppNotification>(e =>
        {
            e.ToTable("notifications");
            e.HasKey(n => n.NotifId);
            e.Property(n => n.NotifId).HasColumnName("notif_id").UseIdentityAlwaysColumn();
            e.Property(n => n.UserId).HasColumnName("user_id").IsRequired();
            e.Property(n => n.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            e.Property(n => n.Body).HasColumnName("body").IsRequired();
            e.Property(n => n.Type).HasColumnName("type").HasMaxLength(30);
            e.Property(n => n.IsRead).HasColumnName("is_read").HasDefaultValue(false);
            e.Property(n => n.SentAt).HasColumnName("sent_at").HasDefaultValueSql("NOW()");
        });

        // ── LOCATION LOGS ────────────────────────────────────
        modelBuilder.Entity<LocationLog>(e =>
        {
            e.ToTable("location_logs");
            e.HasKey(l => l.LogId);
            e.Property(l => l.LogId).HasColumnName("log_id").UseIdentityAlwaysColumn();
            e.Property(l => l.RentalId).HasColumnName("rental_id").IsRequired();
            e.Property(l => l.VehicleId).HasColumnName("vehicle_id").IsRequired();
            e.Property(l => l.Latitude).HasColumnName("latitude").HasPrecision(10, 7).IsRequired();
            e.Property(l => l.Longitude).HasColumnName("longitude").HasPrecision(10, 7).IsRequired();
            e.Property(l => l.SpeedKmh).HasColumnName("speed_kmh").HasPrecision(6, 2);
            e.Property(l => l.LoggedAt).HasColumnName("logged_at").HasDefaultValueSql("NOW()");
            e.Ignore(l => l.SpeedKmH);      // alias property
            e.Ignore(l => l.VehicleName);
            e.Ignore(l => l.PlateNumber);
            e.Ignore(l => l.DriverName);
        });

        // ── GPS LOGS ─────────────────────────────────────────
        modelBuilder.Entity<GpsLog>(e =>
        {
            e.ToTable("gps_logs");
            e.HasKey(g => g.LogId);
            e.Property(g => g.LogId).HasColumnName("log_id").UseIdentityAlwaysColumn();
            e.Property(g => g.RentalId).HasColumnName("rental_id").IsRequired();
            e.Property(g => g.Latitude).HasColumnName("latitude").HasPrecision(10, 7).IsRequired();
            e.Property(g => g.Longitude).HasColumnName("longitude").HasPrecision(10, 7).IsRequired();
            e.Property(g => g.OdometerKm).HasColumnName("odometer_km").HasPrecision(8, 2);
            e.Property(g => g.LoggedAt).HasColumnName("logged_at").HasDefaultValueSql("NOW()");
        });

        // ── MESSAGES ─────────────────────────────────────────
        modelBuilder.Entity<Message>(e =>
        {
            e.ToTable("messages");
            e.HasKey(m => m.MessageId);
            e.Property(m => m.MessageId).HasColumnName("message_id").UseIdentityAlwaysColumn();
            e.Property(m => m.RentalId).HasColumnName("rental_id").IsRequired();
            e.Property(m => m.SenderId).HasColumnName("sender_id").IsRequired();
            e.Property(m => m.Content).HasColumnName("message_text");
            e.Property(m => m.AttachmentUrl).HasColumnName("media_url");
            e.Property(m => m.SentAt).HasColumnName("sent_at").HasDefaultValueSql("NOW()");
            e.Ignore(m => m.SenderName);
        });
    }
}
