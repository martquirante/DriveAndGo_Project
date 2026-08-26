using DriveAndGo_API.Helpers;
using DriveAndGo_API.Models;
using DriveAndGo_API.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Globalization;

namespace DriveAndGo_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RentalsController : ControllerBase
{
    private readonly string _connectionString;
    private readonly NpgsqlDataSource _ds;
    private readonly NotificationWriter _notificationWriter;
    private readonly AuditService _auditService;
    private readonly PdfService _pdfService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public RentalsController(
        IConfiguration configuration,
        NpgsqlDataSource ds,
        NotificationWriter notificationWriter,
        AuditService auditService,
        PdfService pdfService,
        IEmailService emailService)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        _ds = ds;
        _notificationWriter = notificationWriter;
        _auditService = auditService;
        _pdfService = pdfService;
        _emailService = emailService;
        _configuration = configuration;
    }


    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            await using var connection = await _ds.OpenConnectionAsync();

            int totalVehicles = 0;
            int availableVehicles = 0;
            int onRentVehicles = 0;
            int maintenanceVehicles = 0;

            await using (var cmdVeh = new NpgsqlCommand(@"
                SELECT 
                    COUNT(*) AS total_vehicles,
                    COUNT(*) FILTER (WHERE LOWER(v.status) IN ('maintenance', 'repair')) AS maintenance_vehicles,
                    COUNT(*) FILTER (
                        WHERE LOWER(v.status) NOT IN ('maintenance', 'repair')
                          AND EXISTS (
                              SELECT 1 FROM rentals r 
                              WHERE r.vehicle_id = v.vehicle_id 
                                AND LOWER(r.status) IN ('approved', 'active', 'in-use', 'ongoing', 'rented', 'overdue')
                          )
                    ) AS on_rent_vehicles,
                    COUNT(*) FILTER (
                        WHERE LOWER(v.status) NOT IN ('maintenance', 'repair')
                          AND NOT EXISTS (
                              SELECT 1 FROM rentals r 
                              WHERE r.vehicle_id = v.vehicle_id 
                                AND LOWER(r.status) IN ('approved', 'active', 'in-use', 'ongoing', 'rented', 'overdue')
                          )
                    ) AS available_vehicles
                FROM vehicles v", connection))

            await using (var reader = await cmdVeh.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    totalVehicles = reader["total_vehicles"] == DBNull.Value ? 0 : Convert.ToInt32(reader["total_vehicles"]);
                    maintenanceVehicles = reader["maintenance_vehicles"] == DBNull.Value ? 0 : Convert.ToInt32(reader["maintenance_vehicles"]);
                    onRentVehicles = reader["on_rent_vehicles"] == DBNull.Value ? 0 : Convert.ToInt32(reader["on_rent_vehicles"]);
                    availableVehicles = reader["available_vehicles"] == DBNull.Value ? 0 : Convert.ToInt32(reader["available_vehicles"]);
                }
            }

            int totalBookings = 0;
            int pendingBookings = 0;
            int activeBookings = 0;
            int overdueBookings = 0;
            int completedBookings = 0;

            await using (var cmdRent = new NpgsqlCommand("SELECT LOWER(COALESCE(status, '')) AS st, COUNT(*) AS cnt FROM rentals GROUP BY st", connection))
            await using (var reader = await cmdRent.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var st = (reader["st"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    var cnt = Convert.ToInt32(reader["cnt"]);
                    totalBookings += cnt;
                    if (st == "pending" || st == "requested" || st == "submitted") pendingBookings += cnt;
                    else if (st == "active" || st == "approved" || st == "in-use" || st == "ongoing" || st == "rented") activeBookings += cnt;
                    else if (st == "overdue" || st == "late") overdueBookings += cnt;
                    else if (st == "completed" || st == "returned" || st == "settled") completedBookings += cnt;
                }
            }

            return Ok(new
            {
                TotalVehicles = totalVehicles,
                AvailableVehicles = availableVehicles,
                OnRentVehicles = onRentVehicles,
                MaintenanceVehicles = maintenanceVehicles,
                TotalBookings = totalBookings,
                PendingBookings = pendingBookings,
                ActiveBookings = activeBookings,
                OverdueBookings = overdueBookings,
                CompletedBookings = completedBookings
            });


        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> GetRentalPdf(int id, [FromQuery] string? adminName = null)
    {
        try
        {
            var resolvedAdmin = !string.IsNullOrWhiteSpace(adminName) 
                ? adminName 
                : (Request.Headers.TryGetValue("X-Admin-Name", out var hName) && !string.IsNullOrWhiteSpace(hName) ? hName.ToString() : null);

            var agreementData = await FetchAgreementDataAsync(id, resolvedAdmin);
            if (agreementData == null)
            {
                return NotFound(new { Message = "Rental agreement not found." });
            }

            var pdfBytes = _pdfService.GenerateRentalAgreementPdf(agreementData);
            Response.Headers["Content-Disposition"] = $"inline; filename=\"Rental_Agreement_{agreementData.AgreementCode}.pdf\"";
            return File(pdfBytes, "application/pdf");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "PDF Generation Error: " + ex.Message });
        }
    }

    [HttpGet("code/{code}/pdf")]
    public async Task<IActionResult> GetRentalPdfByCode(string code, [FromQuery] string? adminName = null)
    {
        try
        {
            var rentalId = ExtractIdFromCode(code);
            var resolvedAdmin = !string.IsNullOrWhiteSpace(adminName) 
                ? adminName 
                : (Request.Headers.TryGetValue("X-Admin-Name", out var hName) && !string.IsNullOrWhiteSpace(hName) ? hName.ToString() : null);

            var agreementData = await FetchAgreementDataAsync(rentalId, resolvedAdmin);
            if (agreementData == null)
            {
                return NotFound(new { Message = "Rental agreement not found." });
            }

            var pdfBytes = _pdfService.GenerateRentalAgreementPdf(agreementData);
            Response.Headers["Content-Disposition"] = $"inline; filename=\"Rental_Agreement_{agreementData.AgreementCode}.pdf\"";
            return File(pdfBytes, "application/pdf");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "PDF Generation Error: " + ex.Message });
        }
    }

    [HttpGet("{id:int}/return-certificate")]
    public async Task<IActionResult> GetVehicleReturnCertificatePdf(int id, [FromQuery] string? adminName = null)
    {
        try
        {
            var resolvedAdmin = !string.IsNullOrWhiteSpace(adminName) 
                ? adminName 
                : (Request.Headers.TryGetValue("X-Admin-Name", out var hName) && !string.IsNullOrWhiteSpace(hName) ? hName.ToString() : null);

            var returnData = await FetchVehicleReturnDataAsync(id, resolvedAdmin);
            if (returnData == null)
            {
                return NotFound(new { Message = "Vehicle return settlement record not found." });
            }

            var pdfBytes = _pdfService.GenerateVehicleReturnCertificatePdf(returnData);
            Response.Headers["Content-Disposition"] = $"inline; filename=\"Vehicle_Return_{returnData.AgreementCode}.pdf\"";
            return File(pdfBytes, "application/pdf");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "PDF Generation Error: " + ex.Message });
        }
    }

    [HttpGet("code/{code}/return-certificate")]
    public async Task<IActionResult> GetVehicleReturnCertificatePdfByCode(string code, [FromQuery] string? adminName = null)
    {
        try
        {
            var rentalId = ExtractIdFromCode(code);
            var resolvedAdmin = !string.IsNullOrWhiteSpace(adminName) 
                ? adminName 
                : (Request.Headers.TryGetValue("X-Admin-Name", out var hName) && !string.IsNullOrWhiteSpace(hName) ? hName.ToString() : null);

            var returnData = await FetchVehicleReturnDataAsync(rentalId, resolvedAdmin);
            if (returnData == null)
            {
                return NotFound(new { Message = "Vehicle return settlement record not found." });
            }

            var pdfBytes = _pdfService.GenerateVehicleReturnCertificatePdf(returnData);
            Response.Headers["Content-Disposition"] = $"inline; filename=\"Vehicle_Return_{returnData.AgreementCode}.pdf\"";
            return File(pdfBytes, "application/pdf");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "PDF Generation Error: " + ex.Message });
        }
    }

    [HttpGet("verify/{code}")]
    public async Task<IActionResult> VerifyContract(string code)
    {
        try
        {
            var rentalId = ExtractIdFromCode(code);
            var returnData = await FetchVehicleReturnDataAsync(rentalId);
            if (returnData != null && string.Equals(returnData.InspectionStatus, "COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                return Content(GetReturnVerificationHtml(returnData, code), "text/html");
            }

            var data = await FetchAgreementDataAsync(rentalId);
            return Content(GetVerificationHtml(data, code), "text/html");
        }
        catch
        {
            return Content(GetVerificationHtml(null, code), "text/html");
        }
    }

    [HttpGet("respond/{code}")]
    public async Task<IActionResult> HandleCustomerResponse(string code, [FromQuery] string? action = "accept", [FromQuery] string? startDate = null)
    {
        try
        {
            var rentalId = ExtractIdFromCode(code);
            var agreement = await FetchAgreementDataAsync(rentalId);
            if (agreement == null)
            {
                return Content(GetVerificationHtml(null, code), "text/html");
            }

            action = (action ?? "accept").Trim().ToLowerInvariant();

            await using var connection = await _ds.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            var snapshot = GetRentalStatusSnapshot(connection, transaction, rentalId);
            if (snapshot == null)
            {
                return Content(GetVerificationHtml(null, code), "text/html");
            }

            if (action == "accept")
            {
                ExecuteStatusUpdate(connection, transaction, @"
                    UPDATE rentals 
                    SET status = 'approved',
                        start_date = CASE WHEN start_date < NOW() - INTERVAL '1 hour' THEN NOW() ELSE start_date END,
                        end_date = CASE WHEN start_date < NOW() - INTERVAL '1 hour' THEN NOW() + (end_date - start_date) ELSE end_date END
                    WHERE rental_id = @id", rentalId);

                _notificationWriter.Create(
                    connection,
                    snapshot.CustomerId,
                    "Schedule Confirmed by Customer",
                    $"The updated rental schedule for agreement {agreement.AgreementCode} has been accepted and confirmed.",
                    "booking",
                    transaction);

                await transaction.CommitAsync();

                var updatedData = await FetchAgreementDataAsync(rentalId);
                return Content(GetCustomerResponseSuccessHtml(updatedData ?? agreement, "accept"), "text/html");
            }
            else if (action == "decline")
            {
                ExecuteStatusUpdate(connection, transaction, "UPDATE rentals SET status = 'cancelled' WHERE rental_id = @id", rentalId);
                ExecuteStatusUpdate(connection, transaction, "UPDATE vehicles SET status = 'available' WHERE vehicle_id = @id", snapshot.VehicleId);
                if (snapshot.DriverId.HasValue)
                {
                    ExecuteStatusUpdate(connection, transaction, "UPDATE drivers SET status = 'available' WHERE driver_id = @id", snapshot.DriverId.Value);
                }

                _notificationWriter.Create(
                    connection,
                    snapshot.CustomerId,
                    "Booking Declined & Cancelled",
                    $"Booking {agreement.AgreementCode} has been cancelled per customer request. Full refund initiated.",
                    "cancellation",
                    transaction);

                await transaction.CommitAsync();
                return Content(GetCustomerResponseSuccessHtml(agreement, "decline"), "text/html");
            }
            else if (action == "reschedule" && !string.IsNullOrWhiteSpace(startDate) && DateTime.TryParse(startDate, out var parsedStart))
            {
                var duration = agreement.DurationDays > 0 ? agreement.DurationDays : 1;
                var parsedEnd = parsedStart.AddDays(duration);

                using (var updateCmd = new NpgsqlCommand(@"
                    UPDATE rentals 
                    SET status = 'approved',
                        start_date = @start,
                        end_date = @end
                    WHERE rental_id = @id", connection, transaction))
                {
                    updateCmd.Parameters.AddWithValue("@id", rentalId);
                    updateCmd.Parameters.AddWithValue("@start", parsedStart);
                    updateCmd.Parameters.AddWithValue("@end", parsedEnd);
                    await updateCmd.ExecuteNonQueryAsync();
                }

                _notificationWriter.Create(
                    connection,
                    snapshot.CustomerId,
                    "Custom Schedule Updated",
                    $"New dates for {agreement.AgreementCode} set to {parsedStart:MMM dd, yyyy} - {parsedEnd:MMM dd, yyyy}.",
                    "booking",
                    transaction);

                await transaction.CommitAsync();
                var updatedData = await FetchAgreementDataAsync(rentalId);
                return Content(GetCustomerResponseSuccessHtml(updatedData ?? agreement, "custom-reschedule"), "text/html");
            }
            else
            {
                return Content(GetCustomerRescheduleFormHtml(agreement), "text/html");
            }
        }
        catch (Exception ex)
        {
            return Content($"<!DOCTYPE html><html><body style='font-family:sans-serif;padding:30px;background:#0F172A;color:#fff;'><h3>Error processing response: {System.Web.HttpUtility.HtmlEncode(ex.Message)}</h3></body></html>", "text/html");
        }
    }

    [HttpGet("{id:int}/agreement-data")]

    public async Task<IActionResult> GetAgreementData(int id)
    {
        var data = await FetchAgreementDataAsync(id);
        if (data == null) return NotFound(new { Message = "Rental not found." });
        return Ok(data);
    }

    [HttpPatch("{id:int}/handover")]
    public async Task<IActionResult> HandoverRental(int id, [FromBody] HandoverRequest? request)
    {
        try
        {
            await using var connection = await _ds.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            var snapshot = GetRentalStatusSnapshot(connection, transaction, id);
            if (snapshot == null)
            {
                return NotFound(new { Message = "Rental not found." });
            }

            ExecuteStatusUpdate(connection, transaction, @"
                UPDATE rentals 
                SET status = 'active',
                    start_date = CASE WHEN start_date < NOW() - INTERVAL '1 hour' THEN NOW() ELSE start_date END,
                    end_date = CASE WHEN start_date < NOW() - INTERVAL '1 hour' THEN NOW() + (end_date - start_date) ELSE end_date END
                WHERE rental_id = @id", id);
            ExecuteStatusUpdate(connection, transaction, "UPDATE vehicles SET status = 'rented' WHERE vehicle_id = @id", snapshot.VehicleId);


            if (snapshot.DriverId.HasValue)
            {
                ExecuteStatusUpdate(connection, transaction, "UPDATE drivers SET status = 'on-trip' WHERE driver_id = @id", snapshot.DriverId.Value);
            }

            _notificationWriter.Create(
                connection,
                snapshot.CustomerId,
                "Vehicle Dispatched & Handed Over",
                "Your vehicle inspection is complete and the rental is now active. Have a safe trip!",
                "booking",
                transaction);

            await transaction.CommitAsync();
            return Ok(new
            {
                Message = "Vehicle handover confirmed and rental dispatched successfully.",
                RentalId = id,
                Status = "active"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Handover Error: " + ex.Message });
        }
    }



    private string GetAdminName()
    {
        if (Request.Headers.TryGetValue("X-Admin-Name", out var headerName) && !string.IsNullOrWhiteSpace(headerName))
        {
            return headerName.ToString();
        }
        string claimName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "";
        if (!string.IsNullOrWhiteSpace(claimName)) return claimName;
        return "Admin";
    }

    [HttpGet]
    public IActionResult GetRentals()
    {
        try
        {
            return Ok(ReadRentals(orderBy: "ORDER BY r.created_at DESC"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    public IActionResult GetRentalById(int id)
    {
        try
        {
            var rentals = ReadRentals("WHERE r.rental_id = @id", id);
            var rental = rentals.FirstOrDefault();

            return rental == null
                ? NotFound(new { Message = "Rental not found." })
                : Ok(rental);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpGet("customer/{customerId:int}")]
    public IActionResult GetRentalsByCustomer(int customerId)
    {
        try
        {
            return Ok(ReadRentals("WHERE r.customer_id = @id", customerId, "ORDER BY r.created_at DESC"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPost]
    public IActionResult AddRental([FromBody] Rental rental)
    {
        if (rental.CustomerId <= 0 || rental.VehicleId <= 0)
        {
            return BadRequest(new { Message = "CustomerId and VehicleId are required." });
        }

        if (!rental.EndDate.HasValue || rental.StartDate.Date >= rental.EndDate.Value.Date)
        {
            return BadRequest(new { Message = "End date must be later than the start date." });
        }

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            using var duplicateCommand = new NpgsqlCommand(
                @"SELECT COUNT(*) FROM rentals
                  WHERE customer_id = @customer_id
                    AND vehicle_id = @vehicle_id
                    AND LOWER(COALESCE(status, '')) = 'pending'",
                connection,
                transaction);
            duplicateCommand.Parameters.AddWithValue("@customer_id", rental.CustomerId);
            duplicateCommand.Parameters.AddWithValue("@vehicle_id", rental.VehicleId);

            if (Convert.ToInt32(duplicateCommand.ExecuteScalar()) > 0)
            {
                return Conflict(new { Message = "You already have a pending booking for this vehicle." });
            }

            using var vehicleCommand = new NpgsqlCommand(
                @"SELECT 
                    LOWER(COALESCE(v.status, '')),
                    EXISTS (
                        SELECT 1 FROM rentals r 
                        WHERE r.vehicle_id = v.vehicle_id 
                          AND LOWER(COALESCE(r.status, '')) IN ('approved', 'active', 'in-use', 'ongoing', 'rented', 'overdue')
                          AND (r.start_date < @end_date AND r.end_date > @start_date)
                    ) AS is_conflict
                  FROM vehicles v
                  WHERE v.vehicle_id = @vehicle_id
                  LIMIT 1",
                connection,
                transaction);
            vehicleCommand.Parameters.AddWithValue("@vehicle_id", rental.VehicleId);
            vehicleCommand.Parameters.AddWithValue("@start_date", rental.StartDate);
            vehicleCommand.Parameters.AddWithValue("@end_date", rental.EndDate.Value);

            using (var vehReader = vehicleCommand.ExecuteReader())
            {
                if (!vehReader.Read())
                {
                    return NotFound(new { Message = "Vehicle not found." });
                }

                var baseStatus = vehReader[0]?.ToString() ?? "";
                var isConflict = Convert.ToBoolean(vehReader[1]);
                vehReader.Close();

                if (baseStatus == "maintenance" || baseStatus == "repair")
                {
                    return Conflict(new { Message = "Selected vehicle is currently in maintenance." });
                }

                if (baseStatus == "rented" || isConflict)
                {
                    return Conflict(new { Message = "Selected vehicle is currently on-rent and unavailable for the chosen schedule." });
                }
            }


            int? driverUserId = null;
            if (rental.DriverId.HasValue)
            {
                using var driverCheckCommand = new NpgsqlCommand(
                    @"SELECT driver_id, user_id, LOWER(COALESCE(status, '')) AS status
                      FROM drivers
                      WHERE driver_id = @driver_id
                      LIMIT 1",
                    connection,
                    transaction);
                driverCheckCommand.Parameters.AddWithValue("@driver_id", rental.DriverId.Value);

                using var driverReader = driverCheckCommand.ExecuteReader();
                if (!driverReader.Read())
                {
                    return NotFound(new { Message = "Selected driver was not found." });
                }

                var driverStatus = driverReader["status"]?.ToString() ?? string.Empty;
                driverUserId = Convert.ToInt32(driverReader["user_id"], CultureInfo.InvariantCulture);
                driverReader.Close();

                if (!string.Equals(driverStatus, "available", StringComparison.OrdinalIgnoreCase))
                {
                    return Conflict(new { Message = "Selected driver is not available right now." });
                }
            }

            // PostgreSQL: use RETURNING to get new rental_id in one round-trip
            using var insertCommand = new NpgsqlCommand(
                @"INSERT INTO rentals
                    (customer_id, vehicle_id, driver_id, start_date, end_date, destination, status, total_amount, payment_method, payment_status, created_at)
                  VALUES
                    (@customer_id, @vehicle_id, @driver_id, @start_date, @end_date, @destination, 'pending', @total_amount, @payment_method, 'unpaid', NOW())
                  RETURNING rental_id",
                connection,
                transaction);

            insertCommand.Parameters.AddWithValue("@customer_id", rental.CustomerId);
            insertCommand.Parameters.AddWithValue("@vehicle_id", rental.VehicleId);
            insertCommand.Parameters.AddWithValue("@driver_id", rental.DriverId.HasValue ? rental.DriverId.Value : DBNull.Value);
            insertCommand.Parameters.AddWithValue("@start_date", rental.StartDate);
            insertCommand.Parameters.AddWithValue("@end_date", rental.EndDate.Value);
            insertCommand.Parameters.AddWithValue("@destination", string.IsNullOrWhiteSpace(rental.Destination) ? DBNull.Value : (object)rental.Destination.Trim());
            insertCommand.Parameters.AddWithValue("@total_amount", rental.TotalAmount);
            insertCommand.Parameters.AddWithValue("@payment_method", NormalizeLower(rental.PaymentMethod, "cash"));

            var rentalId = Convert.ToInt32(insertCommand.ExecuteScalar(), CultureInfo.InvariantCulture);

            _notificationWriter.Create(
                connection,
                rental.CustomerId,
                "Booking request submitted",
                "Your booking request has been sent to Drive & Go for review.",
                "booking",
                transaction);

            if (driverUserId.HasValue)
            {
                _notificationWriter.Create(
                    connection,
                    driverUserId.Value,
                    "Trip request assigned",
                    "A customer requested a booking that includes your driver service.",
                    "driver-assignment",
                    transaction);
            }

            transaction.Commit();

            return Ok(new
            {
                Message  = "Booking request submitted successfully.",
                RentalId = rentalId
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPatch("{id:int}/approve")]
    public IActionResult ApproveRental(int id)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            using var command = new NpgsqlCommand(
                @"SELECT
                    r.customer_id,
                    r.driver_id,
                    r.vehicle_id,
                    LOWER(COALESCE(r.status, '')) AS rental_status,
                    LOWER(COALESCE(v.status, '')) AS vehicle_status,
                    d.user_id AS driver_user_id,
                    LOWER(COALESCE(d.status, '')) AS driver_status
                  FROM rentals r
                  JOIN vehicles v ON v.vehicle_id = r.vehicle_id
                  LEFT JOIN drivers d ON d.driver_id = r.driver_id
                  WHERE r.rental_id = @id
                  LIMIT 1
                  FOR UPDATE OF r",
                connection,
                transaction);
            command.Parameters.AddWithValue("@id", id);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return NotFound(new { Message = "Rental not found." });
            }

            var customerId    = Convert.ToInt32(reader["customer_id"], CultureInfo.InvariantCulture);
            var vehicleId     = Convert.ToInt32(reader["vehicle_id"], CultureInfo.InvariantCulture);
            var driverId      = reader["driver_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["driver_id"], CultureInfo.InvariantCulture);
            var driverUserId  = reader["driver_user_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["driver_user_id"], CultureInfo.InvariantCulture);
            var rentalStatus  = reader["rental_status"]?.ToString() ?? string.Empty;
            var vehicleStatus = reader["vehicle_status"]?.ToString() ?? string.Empty;
            var driverStatus  = reader["driver_status"]?.ToString() ?? string.Empty;
            reader.Close();

            if (!string.Equals(rentalStatus, "pending", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { Message = $"Rental cannot be approved because it is already '{rentalStatus}'." });
            }

            if (!string.Equals(vehicleStatus, "available", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { Message = $"Vehicle cannot be approved because it is already '{vehicleStatus}'." });
            }

            if (driverId.HasValue && !string.Equals(driverStatus, "available", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { Message = "Assigned driver is no longer available." });
            }

            ExecuteStatusUpdate(connection, transaction, @"
                UPDATE rentals 
                SET status = 'approved',
                    start_date = CASE WHEN start_date < NOW() - INTERVAL '1 hour' THEN NOW() ELSE start_date END,
                    end_date = CASE WHEN start_date < NOW() - INTERVAL '1 hour' THEN NOW() + (end_date - start_date) ELSE end_date END
                WHERE rental_id = @id", id);
            ExecuteStatusUpdate(connection, transaction, "UPDATE vehicles SET status = 'rented' WHERE vehicle_id = @id", vehicleId);


            if (driverId.HasValue)
            {
                ExecuteStatusUpdate(connection, transaction, "UPDATE drivers SET status = 'on-trip' WHERE driver_id = @id", driverId.Value);
            }

            _notificationWriter.Create(
                connection,
                customerId,
                "Booking approved",
                "Your booking was approved. Please prepare for your rental schedule.",
                "booking",
                transaction);

            if (driverUserId.HasValue)
            {
                _notificationWriter.Create(
                    connection,
                    driverUserId.Value,
                    "Rental approved",
                    "A trip assigned to you has been approved and is ready for dispatch.",
                    "driver-assignment",
                    transaction);
            }

            transaction.Commit();

            // System-wide audit trail logging
            string adminName = GetAdminName();
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            _ = _auditService.LogActionAsync(
                adminUserId: 0,
                adminName: adminName,
                actionType: "RENTAL_APPROVED",
                targetUserId: customerId,
                ipAddress: clientIp,
                oldValues: new { rentalId = id },
                newValues: new { description = $"{adminName} approved booking BK-{id}" }
            );

            return Ok(new { Message = "Rental approved successfully.", RentalId = id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPatch("{id:int}/reject")]
    public IActionResult RejectRental(int id)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var rental = GetRentalStatusSnapshot(connection, transaction, id);
            if (rental == null)
            {
                return NotFound(new { Message = "Rental not found." });
            }

            if (!string.Equals(rental.Status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { Message = $"Rental cannot be rejected because it is already '{rental.Status}'." });
            }

            ExecuteStatusUpdate(connection, transaction, "UPDATE rentals SET status = 'rejected' WHERE rental_id = @id", id);
            _notificationWriter.Create(
                connection,
                rental.CustomerId,
                "Booking request rejected",
                "Your booking request was not approved. Please contact Drive & Go for assistance.",
                "booking",
                transaction);

            transaction.Commit();

            // System-wide audit trail logging
            string adminNameRej = GetAdminName();
            string clientIpRej = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            _ = _auditService.LogActionAsync(
                adminUserId: 0,
                adminName: adminNameRej,
                actionType: "RENTAL_REJECTED",
                targetUserId: rental.CustomerId,
                ipAddress: clientIpRej,
                oldValues: new { rentalId = id },
                newValues: new { description = $"{adminNameRej} rejected booking BK-{id}" }
            );

            return Ok(new { Message = "Rental rejected successfully.", RentalId = id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPatch("{id:int}/cancel")]
    public IActionResult CancelRental(int id)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var rental = GetRentalStatusSnapshot(connection, transaction, id);
            if (rental == null)
            {
                return NotFound(new { Message = "Rental not found." });
            }

            if (!string.Equals(rental.Status, "pending", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(rental.Status, "approved", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { Message = $"Rental cannot be cancelled because it is already '{rental.Status}'." });
            }

            ExecuteStatusUpdate(connection, transaction, "UPDATE rentals SET status = 'cancelled' WHERE rental_id = @id", id);

            if (string.Equals(rental.Status, "approved", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteStatusUpdate(connection, transaction, "UPDATE vehicles SET status = 'available' WHERE vehicle_id = @id", rental.VehicleId);

                if (rental.DriverId.HasValue)
                {
                    ExecuteStatusUpdate(connection, transaction, "UPDATE drivers SET status = 'available' WHERE driver_id = @id", rental.DriverId.Value);
                }
            }

            _notificationWriter.Create(
                connection,
                rental.CustomerId,
                "Booking cancelled",
                "Your booking has been cancelled.",
                "booking",
                transaction);

            if (rental.DriverUserId.HasValue)
            {
                _notificationWriter.Create(
                    connection,
                    rental.DriverUserId.Value,
                    "Trip cancelled",
                    "A previously assigned trip has been cancelled.",
                    "driver-assignment",
                    transaction);
            }

            transaction.Commit();
            return Ok(new { Message = "Rental cancelled successfully.", RentalId = id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    [HttpPatch("{id:int}/complete")]
    public async Task<IActionResult> CompleteRental(int id, [FromBody] CompleteRentalRequest? request = null)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            var rental = GetRentalStatusSnapshot(connection, transaction, id);
            if (rental == null)
            {
                return NotFound(new { Message = "Rental not found." });
            }

            if (!string.Equals(rental.Status, "approved", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(rental.Status, "active", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(rental.Status, "in-use", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(rental.Status, "overdue", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { Message = "Only approved, active, in-use, or overdue rentals can be completed." });
            }

            // 1. Update Rental Record (return metrics, status, penalties, damage fees)
            await using (var updateRentalCmd = new NpgsqlCommand(@"
                UPDATE rentals 
                SET status = 'completed',
                    return_date = NOW(),
                    return_odometer = @retOdo,
                    return_fuel_level = @retFuel,
                    return_notes = @retNotes,
                    penalty_fee = CASE WHEN @penalty > 0 THEN @penalty ELSE penalty_fee END,
                    damage_fee = CASE WHEN @damageFee > 0 THEN @damageFee ELSE damage_fee END
                WHERE rental_id = @id", connection, transaction))
            {
                updateRentalCmd.Parameters.AddWithValue("@retOdo", (object?)request?.ReturnOdometer ?? DBNull.Value);
                updateRentalCmd.Parameters.AddWithValue("@retFuel", (object?)request?.ReturnFuelLevel ?? DBNull.Value);
                updateRentalCmd.Parameters.AddWithValue("@retNotes", (object?)request?.ReturnNotes ?? DBNull.Value);
                updateRentalCmd.Parameters.AddWithValue("@penalty", request?.PenaltyFee ?? 0m);
                updateRentalCmd.Parameters.AddWithValue("@damageFee", request?.DamageFee ?? 0m);
                updateRentalCmd.Parameters.AddWithValue("@id", id);
                await updateRentalCmd.ExecuteNonQueryAsync();
            }

            // 2. Fetch current vehicle odometer to calculate update
            decimal currentOdometer = 0;
            decimal lastMaintOdometer = 0;
            await using (var selectCmd = new NpgsqlCommand("SELECT COALESCE(current_odometer, 0), COALESCE(last_maintenance_odometer, 0) FROM vehicles WHERE vehicle_id = @vid", connection, transaction))
            {
                selectCmd.Parameters.AddWithValue("@vid", rental.VehicleId);
                await using var reader = await selectCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    currentOdometer = Convert.ToDecimal(reader[0]);
                    lastMaintOdometer = Convert.ToDecimal(reader[1]);
                }
            }

            decimal newOdometer = request?.ReturnOdometer.HasValue == true && request.ReturnOdometer.Value >= currentOdometer
                ? request.ReturnOdometer.Value
                : currentOdometer + new Random().Next(120, 350);

            int fuelPct = (request?.ReturnFuelLevel?.Trim().ToLowerInvariant()) switch
            {
                "full" => 100,
                "3/4" => 75,
                "1/2" => 50,
                "1/4" => 25,
                "empty" => 10,
                _ => 100
            };

            // 3. Update Vehicle back to Available and update telemetry
            await using (var updateVehCmd = new NpgsqlCommand(@"
                UPDATE vehicles 
                SET status = 'available',
                    current_odometer = @odo,
                    fuel_level_pct = @fuelPct
                WHERE vehicle_id = @vid", connection, transaction))
            {
                updateVehCmd.Parameters.AddWithValue("@odo", newOdometer);
                updateVehCmd.Parameters.AddWithValue("@fuelPct", fuelPct);
                updateVehCmd.Parameters.AddWithValue("@vid", rental.VehicleId);
                await updateVehCmd.ExecuteNonQueryAsync();
            }

            // 4. Update Driver back to Available if assigned
            if (rental.DriverId.HasValue)
            {
                ExecuteStatusUpdate(connection, transaction, "UPDATE drivers SET status = 'available' WHERE driver_id = @id", rental.DriverId.Value);
            }

            // 5. If Damage claim reported, record into damage_claims
            if (request?.HasDamage == true)
            {
                string photosJson = System.Text.Json.JsonSerializer.Serialize(request.DamagePhotos ?? new List<string>());
                string firstPhoto = request.DamagePhotos != null && request.DamagePhotos.Count > 0 ? request.DamagePhotos[0] : "";

                await using (var claimCmd = new NpgsqlCommand(@"
                    INSERT INTO damage_claims (rental_id, damage_severity, description, liability_cost, photo_url, photo_urls, created_at)
                    VALUES (@rid, @sev, @desc, @cost, @photo, @photos::jsonb, NOW())", connection, transaction))
                {
                    claimCmd.Parameters.AddWithValue("@rid", id);
                    claimCmd.Parameters.AddWithValue("@sev", string.IsNullOrWhiteSpace(request.DamageSeverity) ? "Minor" : request.DamageSeverity);
                    claimCmd.Parameters.AddWithValue("@desc", string.IsNullOrWhiteSpace(request.DamageDescription) ? "Post-return vehicle inspection damage detected." : request.DamageDescription);
                    claimCmd.Parameters.AddWithValue("@cost", request.DamageFee ?? 0m);
                    claimCmd.Parameters.AddWithValue("@photo", string.IsNullOrWhiteSpace(firstPhoto) ? (object)DBNull.Value : firstPhoto);
                    claimCmd.Parameters.AddWithValue("@photos", photosJson);
                    await claimCmd.ExecuteNonQueryAsync();
                }
            }

            // 6. Maintenance due check
            if (newOdometer - lastMaintOdometer >= 5000)
            {
                string vehicleInfo = "Vehicle #" + rental.VehicleId;
                await using (var nameCmd = new NpgsqlCommand("SELECT CONCAT(brand, ' ', model, ' (', plate_no, ')') FROM vehicles WHERE vehicle_id = @vid", connection, transaction))
                {
                    nameCmd.Parameters.AddWithValue("@vid", rental.VehicleId);
                    var nameVal = await nameCmd.ExecuteScalarAsync();
                    if (nameVal != null) vehicleInfo = nameVal.ToString()!;
                }

                _notificationWriter.Create(
                    connection,
                    1, // Admin User Id
                    "🔧 MAINTENANCE DUE",
                    $"Oil change / service check required for {vehicleInfo} (Odometer: {newOdometer:F0} km).",
                    "maintenance-due",
                    transaction);
            }

            // 7. Audit & Completion Notifications
            _notificationWriter.Create(
                connection,
                rental.CustomerId,
                "Vehicle Returned & Rental Completed",
                "Your vehicle return inspection is complete and the booking is finalized. Thank you for choosing Drive & Go!",
                "booking",
                transaction);

            if (rental.DriverUserId.HasValue)
            {
                _notificationWriter.Create(
                    connection,
                    rental.DriverUserId.Value,
                    "Trip Completed",
                    "Assigned vehicle return has been checked in and completed.",
                    "driver-assignment",
                    transaction);
            }

            await transaction.CommitAsync();

            // ── Generate PDF + Send Confirmation Email ──
            bool emailSent = false;
            string? emailError = null;
            string targetEmail = string.Empty;

            try
            {
                string adminName = Request.Headers.TryGetValue("X-Admin-Name", out var hName) && !string.IsNullOrWhiteSpace(hName)
                    ? hName.ToString() : "Drive&Go Admin";

                string serverBase  = DriveAndGo_API.Helpers.NetworkHelper.GetServerBaseUrl(_configuration);
                string returnCode  = $"RN-{id:D6}";
                string certCode    = $"VR-{id:D6}";
                string verifyUrl   = $"{serverBase}/api/Rentals/verify/{returnCode}";
                string pdfUrl      = $"{serverBase}/api/Rentals/{id}/return-certificate";

                // Fetch full rental details for email & PDF
                await using var conn2 = await _ds.OpenConnectionAsync();
                await using var cmd2  = new NpgsqlCommand(@"
                    SELECT
                        u.full_name, u.email, COALESCE(u.phone, '') AS phone,
                        CONCAT(v.brand, ' ', v.model) AS vehicle_name,
                        v.plate_no, v.photo_url, COALESCE(v.current_odometer, 0) AS start_odometer,
                        r.start_date, r.return_date, r.total_amount, r.penalty_fee,
                        r.damage_fee, r.return_notes
                    FROM rentals r
                    JOIN users u    ON r.customer_id = u.user_id
                    JOIN vehicles v ON r.vehicle_id  = v.vehicle_id
                    WHERE r.rental_id = @id
                    LIMIT 1", conn2);

                cmd2.Parameters.AddWithValue("@id", id);

                await using var reader2 = await cmd2.ExecuteReaderAsync();
                if (await reader2.ReadAsync())
                {
                    var startDate  = reader2["start_date"]  == DBNull.Value ? DateTime.Now.AddDays(-3) : Convert.ToDateTime(reader2["start_date"]);
                    var returnDate = reader2["return_date"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(reader2["return_date"]);
                    int duration   = Math.Max(1, (returnDate.Date - startDate.Date).Days);

                    decimal totalAmount = reader2["total_amount"] == DBNull.Value ? 0m : Convert.ToDecimal(reader2["total_amount"]);
                    decimal penaltyFee  = reader2["penalty_fee"]  == DBNull.Value ? 0m : Convert.ToDecimal(reader2["penalty_fee"]);
                    decimal damageFee   = reader2["damage_fee"]   == DBNull.Value ? 0m : Convert.ToDecimal(reader2["damage_fee"]);
                    decimal startOdo    = reader2["start_odometer"]  == DBNull.Value ? 0m : Convert.ToDecimal(reader2["start_odometer"]);
                    decimal returnOdo   = request?.ReturnOdometer ?? (startOdo > 0 ? startOdo + 150 : 15000);

                    string retFuelRaw = request?.ReturnFuelLevel ?? "full";
                    string retFuelDisplay = retFuelRaw.Trim().ToLowerInvariant() switch
                    {
                        "full"  => "100% Full",
                        "3/4"   => "75% (3/4)",
                        "1/2"   => "50% (1/2)",
                        "1/4"   => "25% (1/4)",
                        "empty" => "Empty (0%)",
                        _       => retFuelRaw
                    };


                    targetEmail = !string.IsNullOrWhiteSpace(request?.RecipientEmail)
                        ? request.RecipientEmail.Trim()
                        : (reader2["email"]?.ToString() ?? "");

                    // Optionally look up promo gift details
                    string? promoDesc   = null;
                    string? promoExpiry = null;
                    if (request?.IncludePromoGift == true && !string.IsNullOrWhiteSpace(request.SelectedPromoCode))
                    {
                        await using var promoConn = await _ds.OpenConnectionAsync();
                        await using var promoCmd = new NpgsqlCommand(
                            "SELECT discount_percentage, max_discount_amount, expiry_date FROM promo_codes WHERE code = @code AND is_active = true LIMIT 1", promoConn);
                        promoCmd.Parameters.AddWithValue("@code", request.SelectedPromoCode);
                        await using var pr = await promoCmd.ExecuteReaderAsync();
                        if (await pr.ReadAsync())
                        {
                            int discPct        = pr["discount_percentage"] == DBNull.Value ? 0 : Convert.ToInt32(pr["discount_percentage"]);
                            decimal maxDisc    = pr["max_discount_amount"] == DBNull.Value ? 0m : Convert.ToDecimal(pr["max_discount_amount"]);
                            DateTime? expDate  = pr["expiry_date"] == DBNull.Value ? null : Convert.ToDateTime(pr["expiry_date"]);
                            promoDesc   = $"Enjoy {discPct}% OFF on your next luxury rental adventure!" + (maxDisc > 0 ? $" (Up to ₱{maxDisc:N0} discount)" : "");
                            promoExpiry = expDate?.ToString("MMMM dd, yyyy") ?? "";
                        }
                    }

                    var emailData = new DriveAndGo_API.Models.VehicleReturnEmailData
                    {
                        ReturnCertCode   = certCode,
                        AgreementCode    = returnCode,
                        CustomerName     = reader2["full_name"]?.ToString() ?? "",
                        CustomerEmail    = targetEmail,
                        CustomerPhone    = reader2["phone"]?.ToString() ?? "",
                        VehicleName      = reader2["vehicle_name"]?.ToString() ?? "",
                        PlateNo          = reader2["plate_no"]?.ToString() ?? "",
                        VehicleImageUrl  = reader2["photo_url"]?.ToString(),
                        PickupDate       = startDate.ToString("MMM dd, yyyy"),
                        ReturnDate       = returnDate.ToString("MMM dd, yyyy hh:mm tt"),


                        DurationDays     = duration,
                        StartOdometer    = startOdo,
                        ReturnOdometer   = returnOdo > 0 ? returnOdo : request?.ReturnOdometer,
                        ReturnFuel       = retFuelDisplay,
                        InspectionStatus = request?.HasDamage == true ? "INSPECTED" : "PASSED",
                        HasDamage        = request?.HasDamage == true,
                        BaseAmount       = totalAmount,
                        PenaltyFee       = penaltyFee + (request?.PenaltyFee ?? 0m),
                        DamageFee        = damageFee  + (request?.DamageFee  ?? 0m),
                        TotalSettled     = totalAmount + penaltyFee + damageFee,
                        AdminName        = adminName,
                        IncludePromoGift = request?.IncludePromoGift == true && !string.IsNullOrWhiteSpace(request.SelectedPromoCode),
                        PromoCode        = request?.SelectedPromoCode,
                        PromoDescription = promoDesc,
                        PromoExpiry      = promoExpiry,
                        VerificationUrl  = verifyUrl,
                        PdfDownloadUrl   = pdfUrl
                    };

                    // Generate PDF certificate
                    byte[]? pdfBytes = null;
                    try { pdfBytes = _pdfService.GenerateVehicleReturnCertificatePdf(emailData); }
                    catch (Exception pdfEx) { Console.WriteLine($"[RETURN PDF ERROR] {pdfEx.Message}"); }

                    // Send email
                    if (!string.IsNullOrWhiteSpace(emailData.CustomerEmail))
                    {
                        emailSent = await _emailService.SendVehicleReturnConfirmationAsync(emailData, pdfBytes);
                        if (!emailSent) emailError = "SMTP provider failed to dispatch message.";
                        Console.WriteLine($"[RETURN EMAIL] Sent confirmation to {emailData.CustomerEmail} for {returnCode}: {emailSent}");
                    }
                    else
                    {
                        emailError = "No recipient email address specified.";
                    }
                }
            }
            catch (Exception bgEx)
            {
                emailError = bgEx.Message;
                Console.WriteLine($"[RETURN EMAIL ERROR] {bgEx.Message}");
            }

            return Ok(new
            {
                Success = true,
                Message = "Vehicle return confirmed and rental completed successfully.",
                RentalId = id,
                Status = "completed",
                EmailSent = emailSent,
                EmailDestination = targetEmail,
                EmailError = emailError
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "DB Error: " + ex.Message });
        }
    }

    // GET /api/rentals/calendar?year=2026&month=7

    [HttpGet("calendar")]
    public async Task<IActionResult> GetCalendarEvents([FromQuery] int? year, [FromQuery] int? month)
    {
        try
        {
            int y = year  ?? DateTime.Now.Year;
            int m = month ?? DateTime.Now.Month;
            var startRange = new DateTime(y, m, 1);
            var endRange   = startRange.AddMonths(1).AddDays(-1);

            var events = new List<object>();
            await using var conn = await _ds.OpenConnectionAsync();
            await using var cmd  = new NpgsqlCommand(
                @"SELECT r.rental_id, r.start_date, r.end_date, r.status,
                         r.destination, r.total_amount, r.payment_status,
                         u.full_name AS customer_name,
                         CONCAT(v.brand, ' ', v.model) AS vehicle_name,
                         v.plate_no,
                         COALESCE(du.full_name, 'Self-Drive') AS driver_name
                  FROM rentals r
                  JOIN users u   ON r.customer_id = u.user_id
                  JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                  LEFT JOIN drivers d  ON r.driver_id = d.driver_id
                  LEFT JOIN users du   ON d.user_id = du.user_id
                  WHERE r.start_date <= @end AND r.end_date >= @start
                  ORDER BY r.start_date ASC", conn);
            cmd.Parameters.AddWithValue("@start", NpgsqlTypes.NpgsqlDbType.Date, startRange);
            cmd.Parameters.AddWithValue("@end",   NpgsqlTypes.NpgsqlDbType.Date, endRange);
            cmd.CommandTimeout = 30;

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                events.Add(new {
                    rentalId     = reader.GetInt32(reader.GetOrdinal("rental_id")),
                    startDate    = reader.GetDateTime(reader.GetOrdinal("start_date")).ToString("yyyy-MM-dd"),
                    endDate      = reader.IsDBNull(reader.GetOrdinal("end_date")) ? null
                                   : reader.GetDateTime(reader.GetOrdinal("end_date")).ToString("yyyy-MM-dd"),
                    status       = reader["status"]?.ToString() ?? "pending",
                    destination  = reader.IsDBNull(reader.GetOrdinal("destination")) ? null : reader["destination"].ToString(),
                    totalAmount  = Convert.ToDecimal(reader["total_amount"]),
                    paymentStatus = reader["payment_status"]?.ToString(),
                    customerName = reader["customer_name"]?.ToString(),
                    vehicleName  = reader["vehicle_name"]?.ToString(),
                    plateNo      = reader["plate_no"]?.ToString(),
                    driverName   = reader["driver_name"]?.ToString()
                });
            }
            return Ok(events);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }
    private List<Rental> ReadRentals(string? whereClause = null, int? id = null, string? orderBy = null)
    {
        var rentals = new List<Rental>();

        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using (var alterCmd = new NpgsqlCommand(@"
            ALTER TABLE users ADD COLUMN IF NOT EXISTS avatar_base64 TEXT;
            ALTER TABLE users ADD COLUMN IF NOT EXISTS signature_base64 TEXT;
            ALTER TABLE users ADD COLUMN IF NOT EXISTS id_photo_url TEXT;
            ALTER TABLE rentals ADD COLUMN IF NOT EXISTS late_penalty_fee NUMERIC(10,2) DEFAULT 0;
            ALTER TABLE rentals ADD COLUMN IF NOT EXISTS damage_fee NUMERIC(10,2) DEFAULT 0;
            ALTER TABLE rentals ADD COLUMN IF NOT EXISTS damage_notes TEXT;
            ALTER TABLE rentals ADD COLUMN IF NOT EXISTS damage_photos TEXT;
            ALTER TABLE rentals ADD COLUMN IF NOT EXISTS rental_code VARCHAR(30);
            ALTER TABLE rentals ADD COLUMN IF NOT EXISTS agreement_signed_at TIMESTAMP;
            ALTER TABLE rentals ADD COLUMN IF NOT EXISTS agreement_signature_url TEXT;
            ALTER TABLE rentals ADD COLUMN IF NOT EXISTS return_inspection_notes TEXT;
        ", connection))
        {
            try { alterCmd.ExecuteNonQuery(); } catch { }
        }

        using var command = new NpgsqlCommand(BuildRentalQuery(whereClause, orderBy), connection);
        if (id.HasValue)
        {
            command.Parameters.AddWithValue("@id", id.Value);
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rentals.Add(MapRental(reader));
        }

        return rentals;
    }

    private static string BuildRentalQuery(string? whereClause, string? orderBy)
    {
        var sql =
            @"SELECT
                r.rental_id,
                r.customer_id,
                r.vehicle_id,
                r.driver_id,
                r.start_date,
                r.end_date,
                r.destination,
                r.status,
                r.total_amount,
                r.payment_method,
                r.payment_status,
                r.created_at,
                COALESCE(customer.full_name, 'Customer #' || r.customer_id) AS customer_name,
                customer.phone AS customer_phone,
                customer.email AS customer_email,
                COALESCE(NULLIF(customer.avatar_base64, ''), NULLIF(customer.id_photo_url, '')) AS customer_avatar,
                COALESCE(NULLIF(customer.signature_base64, ''), NULLIF(customer.signature_url, '')) AS customer_signature,
                COALESCE(CONCAT(v.brand, ' ', v.model), 'Vehicle #' || r.vehicle_id) AS vehicle_name,
                v.plate_no AS vehicle_plate_no,
                v.rate_per_day AS vehicle_rate,
                COALESCE(v.current_odometer, 0) AS vehicle_odometer,
                COALESCE(v.fuel_level_pct, 100) AS vehicle_fuel_level_pct,
                driver_user.full_name AS driver_name,

                driver_user.phone AS driver_phone,
                COALESCE(NULLIF(driver_user.avatar_base64, ''), NULLIF(driver_user.id_photo_url, ''), NULLIF(d.license_photo_url, '')) AS driver_avatar,
                COALESCE(NULLIF(driver_user.signature_base64, ''), NULLIF(driver_user.signature_url, '')) AS driver_signature
              FROM rentals r
              LEFT JOIN users customer ON r.customer_id = customer.user_id
              LEFT JOIN vehicles v ON r.vehicle_id = v.vehicle_id
              LEFT JOIN drivers d ON r.driver_id = d.driver_id
              LEFT JOIN users driver_user ON d.user_id = driver_user.user_id ";

        if (!string.IsNullOrWhiteSpace(whereClause))
        {
            sql += whereClause + " ";
        }

        sql += string.IsNullOrWhiteSpace(orderBy)
            ? "ORDER BY r.created_at DESC"
            : orderBy;

        return sql;
    }

    private static string? FormatImageUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();
        if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
        {
            return raw;
        }
        return "data:image/png;base64," + raw;
    }

    private static Rental MapRental(NpgsqlDataReader reader)
    {
        return new Rental
        {
            RentalId       = Convert.ToInt32(reader["rental_id"], CultureInfo.InvariantCulture),
            CustomerId     = Convert.ToInt32(reader["customer_id"], CultureInfo.InvariantCulture),
            VehicleId      = Convert.ToInt32(reader["vehicle_id"], CultureInfo.InvariantCulture),
            DriverId       = reader["driver_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["driver_id"], CultureInfo.InvariantCulture),
            StartDate      = Convert.ToDateTime(reader["start_date"], CultureInfo.InvariantCulture),
            EndDate        = reader["end_date"] == DBNull.Value ? null : Convert.ToDateTime(reader["end_date"], CultureInfo.InvariantCulture),
            Destination    = reader["destination"] == DBNull.Value ? null : reader["destination"].ToString(),
            Status         = reader["status"]?.ToString() ?? "pending",
            TotalAmount    = reader["total_amount"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["total_amount"], CultureInfo.InvariantCulture),
            PaymentMethod  = reader["payment_method"]?.ToString() ?? "cash",
            PaymentStatus  = reader["payment_status"]?.ToString() ?? "unpaid",
            CreatedAt      = reader["created_at"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["created_at"], CultureInfo.InvariantCulture),
            CustomerName   = reader["customer_name"] == DBNull.Value ? null : reader["customer_name"].ToString(),
            CustomerPhone  = reader["customer_phone"] == DBNull.Value ? null : reader["customer_phone"].ToString(),
            CustomerEmail  = reader["customer_email"] == DBNull.Value ? null : reader["customer_email"].ToString(),
            CustomerAvatar = FormatImageUrl(reader["customer_avatar"] == DBNull.Value ? null : reader["customer_avatar"].ToString()),
            CustomerSignatureBase64 = FormatImageUrl(reader["customer_signature"] == DBNull.Value ? null : reader["customer_signature"].ToString()),
            VehicleName    = reader["vehicle_name"] == DBNull.Value ? null : reader["vehicle_name"].ToString(),
            VehiclePlateNo = reader["vehicle_plate_no"] == DBNull.Value ? null : reader["vehicle_plate_no"].ToString(),
            VehicleRate    = reader["vehicle_rate"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(reader["vehicle_rate"], CultureInfo.InvariantCulture),
            VehicleOdometer = reader["vehicle_odometer"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(reader["vehicle_odometer"], CultureInfo.InvariantCulture),
            VehicleFuelLevelPct = reader["vehicle_fuel_level_pct"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["vehicle_fuel_level_pct"], CultureInfo.InvariantCulture),
            DriverName     = reader["driver_name"] == DBNull.Value ? null : reader["driver_name"].ToString(),

            DriverPhone    = reader["driver_phone"] == DBNull.Value ? null : reader["driver_phone"].ToString(),
            DriverAvatar   = FormatImageUrl(reader["driver_avatar"] == DBNull.Value ? null : reader["driver_avatar"].ToString()),
            DriverSignatureBase64 = FormatImageUrl(reader["driver_signature"] == DBNull.Value ? null : reader["driver_signature"].ToString())
        };
    }

    private static void ExecuteStatusUpdate(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, int id)
    {
        using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }

    private static string NormalizeLower(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim().ToLowerInvariant();
    }

    private static RentalStatusSnapshot? GetRentalStatusSnapshot(NpgsqlConnection connection, NpgsqlTransaction transaction, int rentalId)
    {
        using var command = new NpgsqlCommand(
            @"SELECT
                r.customer_id,
                r.driver_id,
                r.vehicle_id,
                LOWER(COALESCE(r.status, '')) AS rental_status,
                d.user_id AS driver_user_id
              FROM rentals r
              LEFT JOIN drivers d ON d.driver_id = r.driver_id
              WHERE r.rental_id = @id
              LIMIT 1",
            connection,
            transaction);
        command.Parameters.AddWithValue("@id", rentalId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new RentalStatusSnapshot
        {
            CustomerId   = Convert.ToInt32(reader["customer_id"], CultureInfo.InvariantCulture),
            DriverId     = reader["driver_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["driver_id"], CultureInfo.InvariantCulture),
            DriverUserId = reader["driver_user_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["driver_user_id"], CultureInfo.InvariantCulture),
            VehicleId    = Convert.ToInt32(reader["vehicle_id"], CultureInfo.InvariantCulture),
            Status       = reader["rental_status"]?.ToString() ?? string.Empty
        };
    }

    private sealed class RentalStatusSnapshot
    {
        public int CustomerId { get; set; }
        public int VehicleId { get; set; }
        public int? DriverId { get; set; }
        public int? DriverUserId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    // POST /api/rentals/{id}/split  — Barkada Mode split payments
    [HttpPost("{id:int}/split")]
    public async Task<IActionResult> CreateSplit(int id, [FromBody] SplitRequest req)
    {
        try {
            await using var conn = await _ds.OpenConnectionAsync();
            foreach (var share in req.Shares) {
                await using var cmd = new NpgsqlCommand(
                    "INSERT INTO split_payments (rental_id, email, share_amount, payment_status) VALUES (@rid, @email, @amt, 'pending')", conn);
                cmd.Parameters.AddWithValue("@rid",   id);
                cmd.Parameters.AddWithValue("@email", share.Email);
                cmd.Parameters.AddWithValue("@amt",   share.Amount);
                await cmd.ExecuteNonQueryAsync();
            }
            return Ok(new { message = "Split payments initialized.", rentalId = id, count = req.Shares.Count });
        } catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // GET /api/rentals/{id}/split
    [HttpGet("{id:int}/split")]
    public async Task<IActionResult> GetSplit(int id)
    {
        try {
            var list = new List<object>();
            await using var conn = await _ds.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT split_payment_id, email, share_amount, payment_status, paid_at FROM split_payments WHERE rental_id = @rid ORDER BY split_payment_id", conn);
            cmd.Parameters.AddWithValue("@rid", id);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) {
                list.Add(new {
                    splitId       = reader.GetInt32(0),
                    email         = reader.GetString(1),
                    shareAmount   = reader.GetDecimal(2),
                    paymentStatus = reader.GetString(3),
                    paidAt        = reader.IsDBNull(4) ? null : reader.GetDateTime(4).ToString("yyyy-MM-dd HH:mm")
                });
            }
            return Ok(list);
        } catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    private async Task<RentalAgreementEmailData?> FetchAgreementDataAsync(int rentalId, string? overrideAdminName = null)
    {
        await using var connection = await _ds.OpenConnectionAsync();

        string adminName = overrideAdminName?.Trim() ?? string.Empty;
        string? adminSig = null;
        try
        {
            await using var adminCmd = new NpgsqlCommand("SELECT full_name, signature_base64 FROM users WHERE LOWER(role) = 'admin' ORDER BY user_id ASC LIMIT 1", connection);
            await using var aReader = await adminCmd.ExecuteReaderAsync();
            if (await aReader.ReadAsync())
            {
                if (string.IsNullOrWhiteSpace(adminName))
                {
                    adminName = aReader["full_name"]?.ToString() ?? "Raymart Quirante";
                }
                adminSig = aReader["signature_base64"] == DBNull.Value ? null : aReader["signature_base64"]?.ToString();
            }
        }
        catch { }
        if (string.IsNullOrWhiteSpace(adminName)) adminName = "Raymart Quirante";

        const string sql = @"
            SELECT 
                r.rental_id,
                r.start_date,
                r.end_date,
                r.destination,
                r.status,
                r.total_amount,
                r.payment_method,
                r.payment_status,
                r.created_at,
                customer.full_name AS customer_name,
                customer.phone AS customer_phone,
                customer.email AS customer_email,
                customer.signature_base64 AS customer_signature,
                CONCAT(v.brand, ' ', v.model) AS vehicle_name,
                v.plate_no AS vehicle_plate_no,
                driver_user.full_name AS driver_name,
                driver_user.phone AS driver_phone
            FROM rentals r
            JOIN users customer ON r.customer_id = customer.user_id
            JOIN vehicles v ON r.vehicle_id = v.vehicle_id
            LEFT JOIN drivers d ON r.driver_id = d.driver_id
            LEFT JOIN users driver_user ON d.user_id = driver_user.user_id
            WHERE r.rental_id = @id
            LIMIT 1";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", rentalId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var startDate = Convert.ToDateTime(reader["start_date"], CultureInfo.InvariantCulture);
        var endDate = reader["end_date"] == DBNull.Value ? startDate.AddDays(1) : Convert.ToDateTime(reader["end_date"], CultureInfo.InvariantCulture);
        var duration = Math.Max(1, (int)Math.Ceiling((endDate - startDate).TotalDays));

        var totalAmount = reader["total_amount"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["total_amount"], CultureInfo.InvariantCulture);
        var dailyRate = duration > 0 ? (totalAmount > 0 ? totalAmount / duration : 3000m) : 3000m;
        var dailyTotal = dailyRate * duration;
        var insurance = 500m;
        var vat = Math.Round((dailyTotal + insurance) * 0.12m, 2);

        var createdAt = reader["created_at"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["created_at"], CultureInfo.InvariantCulture);
        var agreementCode = $"RN-{createdAt:yyMMdd}-{rentalId:D3}";

        return new RentalAgreementEmailData
        {
            AgreementCode = agreementCode,
            AdminName = adminName,
            AdminSignatureBase64 = adminSig,
            CustomerSignatureBase64 = reader["customer_signature"] == DBNull.Value ? null : reader["customer_signature"]?.ToString(),
            CustomerName = reader["customer_name"]?.ToString() ?? "Valued Customer",
            CustomerPhone = reader["customer_phone"]?.ToString() ?? "",
            CustomerEmail = reader["customer_email"]?.ToString() ?? "",
            VehicleName = reader["vehicle_name"]?.ToString() ?? "Rental Vehicle",
            PlateNo = reader["vehicle_plate_no"]?.ToString() ?? "—",
            VehicleColor = "Standard",
            PickupDate = startDate.ToString("MMM dd, yyyy (hh:mm tt)", CultureInfo.InvariantCulture),
            DropoffDate = endDate.ToString("MMM dd, yyyy (hh:mm tt)", CultureInfo.InvariantCulture),
            DurationDays = duration,
            DailyRate = dailyRate,
            DailyTotal = dailyTotal,
            InsuranceFee = insurance,
            VatAmount = vat,
            TotalAmount = totalAmount > 0 ? totalAmount : (dailyTotal + insurance + vat),
            Destination = reader["destination"] == DBNull.Value ? "Metro Manila / Regional" : reader["destination"]?.ToString() ?? "",
            DriverName = reader["driver_name"] == DBNull.Value ? "" : reader["driver_name"]?.ToString() ?? "",
            DriverPhone = reader["driver_phone"] == DBNull.Value ? "" : reader["driver_phone"]?.ToString() ?? "",
            PaymentStatus = reader["payment_status"] == DBNull.Value ? "Unpaid" : reader["payment_status"]?.ToString() ?? "Paid",
            PaymentMethod = reader["payment_method"] == DBNull.Value ? "Cash" : reader["payment_method"]?.ToString() ?? "Cash",
            CreatedDate = createdAt.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture),
            VerificationUrl = $"{NetworkHelper.GetServerBaseUrl(_configuration)}/api/Rentals/verify/{agreementCode}",
            CompanyAddress = _configuration?["CompanyInfo:Address"] ?? "DriveAndGo Inc., CSJDM | Norzagaray, Bulacan, Philippines",
            CompanyPhone = _configuration?["CompanyInfo:Phone"] ?? "+63 935 966 7178",
            CompanyEmail = _configuration?["CompanyInfo:Email"] ?? "support@driveandgo.com"
        };
    }

    private async Task<VehicleReturnEmailData?> FetchVehicleReturnDataAsync(int rentalId, string? overrideAdminName = null)
    {
        await using var connection = await _ds.OpenConnectionAsync();

        string adminName = overrideAdminName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(adminName))
        {
            try
            {
                await using var adminCmd = new NpgsqlCommand("SELECT full_name FROM users WHERE LOWER(role) = 'admin' ORDER BY user_id ASC LIMIT 1", connection);
                var aVal = await adminCmd.ExecuteScalarAsync();
                if (aVal != null) adminName = aVal.ToString()!;
            }
            catch { }
        }
        if (string.IsNullOrWhiteSpace(adminName)) adminName = "Raymart Quirante (Admin)";

        const string sql = @"
            SELECT 
                r.rental_id,
                r.start_date,
                r.end_date,
                r.return_date,
                r.status,
                r.total_amount,
                r.penalty_fee,
                r.damage_fee,
                COALESCE(r.return_odometer, v.current_odometer, 0) AS return_odometer,
                COALESCE(v.current_odometer, 0) AS start_odometer,
                COALESCE(r.return_fuel_level, 'full') AS return_fuel_level,
                r.return_notes,
                customer.full_name AS customer_name,
                customer.phone AS customer_phone,
                customer.email AS customer_email,
                CONCAT(v.brand, ' ', v.model) AS vehicle_name,
                v.plate_no AS vehicle_plate_no,
                v.photo_url AS vehicle_image_url
            FROM rentals r
            JOIN users customer ON r.customer_id = customer.user_id
            JOIN vehicles v ON r.vehicle_id = v.vehicle_id
            WHERE r.rental_id = @id
            LIMIT 1";



        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", rentalId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var startDate  = Convert.ToDateTime(reader["start_date"], CultureInfo.InvariantCulture);
        var returnDate = reader["return_date"] == DBNull.Value ? (reader["end_date"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(reader["end_date"])) : Convert.ToDateTime(reader["return_date"]);
        int duration   = Math.Max(1, (int)Math.Ceiling((returnDate.Date - startDate.Date).TotalDays));

        decimal totalAmount = reader["total_amount"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["total_amount"], CultureInfo.InvariantCulture);
        decimal penaltyFee  = reader["penalty_fee"]  == DBNull.Value ? 0m : Convert.ToDecimal(reader["penalty_fee"], CultureInfo.InvariantCulture);
        decimal damageFee   = reader["damage_fee"]   == DBNull.Value ? 0m : Convert.ToDecimal(reader["damage_fee"], CultureInfo.InvariantCulture);
        decimal startOdo    = reader["start_odometer"]  == DBNull.Value ? 0m : Convert.ToDecimal(reader["start_odometer"], CultureInfo.InvariantCulture);
        decimal returnOdo   = reader["return_odometer"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["return_odometer"], CultureInfo.InvariantCulture);

        string rawStatus = reader["status"]?.ToString() ?? "";
        string retFuelRaw = reader["return_fuel_level"]?.ToString() ?? "Full";
        string retFuelDisplay = retFuelRaw.Trim() switch
        {
            "full"  => "100% Full",
            "3/4"   => "75% (3/4)",
            "1/2"   => "50% (1/2)",
            "1/4"   => "25% (1/4)",
            "empty" => "Empty (0%)",
            _       => retFuelRaw
        };

        string agreementCode = $"RN-{rentalId:D6}";
        string certCode      = $"VR-{rentalId:D6}";
        string serverBase    = NetworkHelper.GetServerBaseUrl(_configuration);

        return new VehicleReturnEmailData
        {
            ReturnCertCode   = certCode,
            AgreementCode    = agreementCode,
            CustomerName     = reader["customer_name"]?.ToString() ?? "Valued Customer",
            CustomerEmail    = reader["customer_email"]?.ToString() ?? "",
            CustomerPhone    = reader["customer_phone"]?.ToString() ?? "",
            VehicleName      = reader["vehicle_name"]?.ToString() ?? "Rental Vehicle",
            PlateNo          = reader["vehicle_plate_no"]?.ToString() ?? "—",
            VehicleImageUrl  = reader["vehicle_image_url"] == DBNull.Value ? null : reader["vehicle_image_url"]?.ToString(),
            PickupDate       = startDate.ToString("MMM dd, yyyy (hh:mm tt)", CultureInfo.InvariantCulture),
            ReturnDate       = returnDate.ToString("MMM dd, yyyy (hh:mm tt)", CultureInfo.InvariantCulture),
            DurationDays     = duration,
            StartOdometer    = startOdo,
            ReturnOdometer   = returnOdo,
            ReturnFuel       = retFuelDisplay,
            InspectionStatus = string.Equals(rawStatus, "completed", StringComparison.OrdinalIgnoreCase) ? "COMPLETED" : "PASSED",
            HasDamage        = damageFee > 0,
            BaseAmount       = totalAmount,
            PenaltyFee       = penaltyFee,
            DamageFee        = damageFee,
            TotalSettled     = totalAmount + penaltyFee + damageFee,
            AdminName        = adminName,
            VerificationUrl  = $"{serverBase}/api/Rentals/verify/{agreementCode}",
            PdfDownloadUrl   = $"{serverBase}/api/Rentals/{rentalId}/return-certificate",
            CompanyAddress   = _configuration?["CompanyInfo:Address"] ?? "San Jose del Monte, Bulacan, Philippines",
            CompanyPhone     = _configuration?["CompanyInfo:Phone"] ?? "+63 935 966 7178",
            CompanyEmail     = _configuration?["CompanyInfo:Email"] ?? "support@driveandgo.com"
        };
    }

    private static string GetReturnVerificationHtml(VehicleReturnEmailData data, string code)
    {
        string logoUrl = "https://raw.githubusercontent.com/martquirante/DriveAndGo_Project/main/DriveAndGo_Admin/WebAssets/logo.png";
        string odoDiffStr = (data.ReturnOdometer.HasValue && data.StartOdometer.HasValue) ? $"+{(data.ReturnOdometer.Value - data.StartOdometer.Value):N0} km" : "N/A";

        return $@"<!DOCTYPE html>
        <html lang='en' class='dark'>
        <head>
          <meta charset='UTF-8'>
          <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'>
          <title>Vehicle Return Certificate Verified - {data.AgreementCode}</title>
          <link rel='icon' type='image/png' href='{logoUrl}'>
          <link rel='shortcut icon' type='image/png' href='{logoUrl}'>
          <link rel='apple-touch-icon' href='{logoUrl}'>
          <script src='https://cdn.tailwindcss.com'></script>
          <script>
            tailwind.config = {{
              darkMode: 'class',
              theme: {{
                extend: {{
                  colors: {{
                    brand: '#FF6B00',
                    'brand-dark': '#E85F00',
                    navy: '#0B192C'
                  }}
                }}
              }}
            }};
          </script>
          <style>
            @keyframes pulseGlow {{
              0%, 100% {{ transform: scale(1); opacity: 0.9; }}
              50% {{ transform: scale(1.05); opacity: 1; }}
            }}
            .pulse-anim {{ animation: pulseGlow 2s infinite ease-in-out; }}
            @keyframes scanLine {{
              0% {{ top: 0%; opacity: 0; }}
              30% {{ opacity: 1; }}
              100% {{ top: 100%; opacity: 0; }}
            }}
            .scanner-line {{
              position: absolute;
              left: 0; right: 0; height: 2px;
              background: linear-gradient(90deg, transparent, #10B981, transparent);
              animation: scanLine 1.8s infinite ease-in-out;
            }}
            .theme-transition {{
              transition: background-color 0.3s ease, color 0.3s ease, border-color 0.3s ease;
            }}
          </style>
        </head>
        <body class='theme-transition bg-slate-100 dark:bg-[#070B14] text-slate-800 dark:text-slate-100 min-h-screen flex items-center justify-center p-3 sm:p-6 font-sans select-none'>

          <!-- Loading Screen -->
          <div id='loading-screen' class='fixed inset-0 z-50 bg-[#070B14] flex flex-col items-center justify-center p-6 text-center transition-opacity duration-500'>
            <div class='relative mb-6'>
              <img src='{logoUrl}' alt='Drive&amp;Go' class='h-14 w-auto object-contain mx-auto pulse-anim' />
              <div class='scanner-line'></div>
            </div>
            <div class='relative w-16 h-16 mb-4'>
              <div class='w-16 h-16 rounded-full border-4 border-slate-800 border-t-emerald-500 animate-spin'></div>
              <div class='absolute inset-0 flex items-center justify-center text-emerald-400'>
                <svg class='w-6 h-6' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2.5' d='M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z'></path></svg>
              </div>
            </div>
            <div class='text-sm font-black uppercase tracking-widest text-emerald-400 mb-1'>Authenticating Return Settlement</div>
            <div class='text-xs text-slate-400 font-mono'>{data.AgreementCode} &bull; {data.ReturnCertCode}</div>
          </div>

          <!-- Main Settlement Card -->
          <div id='content-card' class='max-w-lg w-full bg-white dark:bg-[#0E1626] border border-slate-200 dark:border-slate-800/80 rounded-3xl overflow-hidden shadow-2xl transition-all duration-500 opacity-0 transform scale-95'>
            
            <!-- Top App Bar & Theme Toggle -->
            <div class='px-5 py-4 border-b border-slate-100 dark:border-slate-800/80 flex items-center justify-between bg-slate-50/50 dark:bg-[#0B111E]'>
              <img src='{logoUrl}' alt='Drive&amp;Go' class='h-8 w-auto object-contain' />
              <button onclick='toggleTheme()' class='p-2 rounded-xl bg-slate-200/70 dark:bg-slate-800 text-slate-700 dark:text-slate-300 hover:text-brand transition-colors text-xs flex items-center gap-1.5 font-bold'>
                <span id='theme-icon-container'>
                  <svg class='w-3.5 h-3.5' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z'></path></svg>
                </span>
                <span class='text-[10px] hidden sm:inline' id='theme-label'>Dark</span>
              </button>
            </div>

            <!-- Verification Banner -->
            <div class='p-5 bg-gradient-to-r from-emerald-500/10 via-teal-500/10 to-emerald-500/5 dark:from-emerald-950/40 dark:via-teal-950/30 dark:to-emerald-950/20 border-b border-emerald-500/20'>
              <div class='flex items-start justify-between gap-3'>
                <div>
                  <div class='inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-[10px] font-black uppercase tracking-wider bg-emerald-500/15 dark:bg-emerald-500/20 text-emerald-600 dark:text-emerald-400 border border-emerald-500/30'>
                    <svg class='w-3 h-3' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='3' d='M5 13l4 4L19 7'></path></svg>
                    <span>Vehicle Returned &amp; Settled</span>
                  </div>
                  <h1 class='text-xl font-black text-slate-900 dark:text-white mt-2 font-mono tracking-tight'>{data.AgreementCode}</h1>
                  <p class='text-[11px] text-slate-400 font-mono mt-0.5'>Cert: {data.ReturnCertCode}</p>
                </div>
                <div class='text-right'>
                  <span class='inline-block px-2.5 py-1 rounded-lg text-[10px] font-black uppercase tracking-wider bg-emerald-500 text-white shadow-md shadow-emerald-500/20'>
                    COMPLETED
                  </span>
                  <div class='text-[10px] text-slate-500 dark:text-slate-400 mt-1 font-medium'>{data.ReturnDate}</div>
                </div>
              </div>
            </div>

            <!-- Body Details -->
            <div class='p-5 sm:p-6 space-y-4 text-xs'>
              
              <!-- Customer & Vehicle Card -->
              <div class='grid grid-cols-1 sm:grid-cols-2 gap-3'>
                <div class='p-3.5 bg-slate-50 dark:bg-[#131E33] rounded-2xl border border-slate-200/80 dark:border-slate-800/80'>
                  <div class='text-[9px] uppercase font-black tracking-wider text-slate-400 mb-1'>Lessee / Customer</div>
                  <div class='font-bold text-slate-900 dark:text-white text-sm truncate'>{data.CustomerName}</div>
                  <div class='text-slate-500 dark:text-slate-400 text-[11px] mt-0.5'>{data.CustomerPhone}</div>
                </div>
                <div class='p-3.5 bg-slate-50 dark:bg-[#131E33] rounded-2xl border border-slate-200/80 dark:border-slate-800/80'>
                  <div class='text-[9px] uppercase font-black tracking-wider text-slate-400 mb-1'>Returned Vehicle</div>
                  <div class='font-bold text-slate-900 dark:text-white text-sm truncate'>{data.VehicleName}</div>
                  <div class='font-mono font-bold text-brand text-[11px] mt-0.5'>{data.PlateNo}</div>
                </div>
              </div>

              <!-- Inspection 3-Column Summary -->
              <div class='p-3.5 bg-slate-50 dark:bg-[#131E33] rounded-2xl border border-slate-200/80 dark:border-slate-800/80 grid grid-cols-3 gap-2 text-center'>
                <div>
                  <div class='text-[8.5px] uppercase font-bold text-brand'>Ending Odometer</div>
                  <div class='font-extrabold text-slate-900 dark:text-white text-xs mt-1'>{data.ReturnOdometer:N0} km</div>
                  <div class='text-[9px] text-slate-400 mt-0.5'>{odoDiffStr}</div>
                </div>
                <div class='border-x border-slate-200 dark:border-slate-800'>
                  <div class='text-[8.5px] uppercase font-bold text-brand'>Return Fuel</div>
                  <div class='font-extrabold text-slate-900 dark:text-white text-xs mt-1'>{data.ReturnFuel}</div>
                  <div class='text-[9px] text-emerald-500 font-bold mt-0.5'>Cleared</div>
                </div>
                <div>
                  <div class='text-[8.5px] uppercase font-bold text-brand'>Inspection</div>
                  <div class='font-extrabold text-emerald-500 text-xs mt-1'>{data.InspectionStatus}</div>
                  <div class='text-[9px] text-slate-400 mt-0.5'>Passed</div>
                </div>
              </div>

              <!-- Billing Settlement & Clearance -->
              <div class='p-4 bg-slate-50 dark:bg-[#131E33] rounded-2xl border border-slate-200/80 dark:border-slate-800/80 space-y-2 text-xs'>
                <div class='flex justify-between items-center text-slate-600 dark:text-slate-300'>
                  <span class='text-slate-400'>Base Rental Charge:</span>
                  <strong class='text-slate-900 dark:text-white'>PHP {data.BaseAmount:N2}</strong>
                </div>
                <div class='flex justify-between items-center text-slate-600 dark:text-slate-300'>
                  <span class='text-slate-400'>Late / Penalty Fees:</span>
                  <strong class='text-slate-900 dark:text-white'>PHP {data.PenaltyFee:N2}</strong>
                </div>
                <div class='flex justify-between items-center text-slate-600 dark:text-slate-300'>
                  <span class='text-slate-400'>Damage Liability:</span>
                  <strong class='text-slate-900 dark:text-white'>PHP {data.DamageFee:N2}</strong>
                </div>
                <div class='flex justify-between items-center text-slate-600 dark:text-slate-300 pt-1.5 border-t border-slate-200 dark:border-slate-800'>
                  <span class='font-bold text-slate-900 dark:text-white'>Total Settled:</span>
                  <strong class='text-brand text-sm font-black'>PHP {data.TotalSettled:N2}</strong>
                </div>
              </div>

              <!-- Green PAID IN FULL Badge -->
              <div class='p-3 bg-emerald-500/10 border-2 border-emerald-500/40 rounded-2xl text-center space-y-0.5'>
                <div class='text-emerald-500 font-extrabold text-sm uppercase tracking-wider flex items-center justify-center gap-1.5'>
                  <svg class='w-4 h-4' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='3' d='M5 13l4 4L19 7'></path></svg>
                  PAID IN FULL &bull; ZERO BALANCE CLEARED
                </div>
                <div class='text-[10px] text-emerald-600 dark:text-emerald-400 font-semibold'>No outstanding obligations under this booking.</div>
              </div>

              <!-- Download PDF Action -->
              <div class='pt-1'>
                <a href='{data.PdfDownloadUrl}' target='_blank' class='w-full py-3.5 px-4 bg-brand hover:bg-brand-dark text-white font-extrabold text-xs uppercase tracking-wider rounded-xl transition-all shadow-lg shadow-orange-500/25 flex items-center justify-center gap-2 active:scale-95'>
                  <svg class='w-4 h-4' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2.5' d='M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4'></path></svg>
                  <span>Download Official Return Certificate (PDF)</span>
                </a>
              </div>

              <!-- Footer -->
              <div class='border-t border-slate-200 dark:border-slate-800/80 pt-3 text-center text-[10.5px] text-slate-500 dark:text-slate-400 space-y-1'>
                <div>{data.CompanyAddress}</div>
                <div class='font-medium'>
                  Hotline: <a href='tel:{data.CompanyPhone}' class='text-brand hover:underline font-bold'>{data.CompanyPhone}</a> &bull; 
                  <a href='mailto:{data.CompanyEmail}' class='text-slate-600 dark:text-slate-300 hover:underline'>{data.CompanyEmail}</a>
                </div>
              </div>

            </div>
          </div>

          <script>
            const sunSvg = `<svg class='w-3.5 h-3.5' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z'></path></svg>`;
            const moonSvg = `<svg class='w-3.5 h-3.5' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z'></path></svg>`;

            setTimeout(() => {{
              const loader = document.getElementById('loading-screen');
              const content = document.getElementById('content-card');
              if (loader && content) {{
                loader.style.opacity = '0';
                setTimeout(() => loader.style.display = 'none', 500);
                content.classList.remove('opacity-0', 'scale-95');
                content.classList.add('opacity-100', 'scale-100');
              }}
            }}, 850);

            function toggleTheme() {{
              const html = document.documentElement;
              const iconContainer = document.getElementById('theme-icon-container');
              const label = document.getElementById('theme-label');
              
              if (html.classList.contains('dark')) {{
                html.classList.remove('dark');
                if (iconContainer) iconContainer.innerHTML = sunSvg;
                if (label) label.textContent = 'Light';
                localStorage.setItem('theme', 'light');
              }} else {{
                html.classList.add('dark');
                if (iconContainer) iconContainer.innerHTML = moonSvg;
                if (label) label.textContent = 'Dark';
                localStorage.setItem('theme', 'dark');
              }}
            }}

            if (localStorage.getItem('theme') === 'light' || (!('theme' in localStorage) && window.matchMedia('(prefers-color-scheme: light)').matches)) {{
              document.documentElement.classList.remove('dark');
              const iconContainer = document.getElementById('theme-icon-container');
              const label = document.getElementById('theme-label');
              if (iconContainer) iconContainer.innerHTML = sunSvg;
              if (label) label.textContent = 'Light';
            }}
          </script>
        </body>
        </html>";
    }

    private static int ExtractIdFromCode(string code)
    {
        if (int.TryParse(code, out var directId)) return directId;
        if (code.StartsWith("RN-", StringComparison.OrdinalIgnoreCase))
        {
            var parts = code.Split('-');
            if (parts.Length >= 3 && int.TryParse(parts[2], out var parsedId))
                return parsedId;
            if (parts.Length == 2 && int.TryParse(parts[1], out var pId))
                return pId;
        }
        return 0;
    }


    private static string GetVerificationHtml(RentalAgreementEmailData? data, string code)
    {
        string logoUrl = "https://raw.githubusercontent.com/martquirante/DriveAndGo_Project/main/DriveAndGo_Admin/WebAssets/logo.png";

        if (data == null)
        {
            return $@"<!DOCTYPE html>
            <html lang='en'>
            <head>
              <meta charset='UTF-8'>
              <meta name='viewport' content='width=device-width, initial-scale=1.0'>
              <title>Drive&amp;Go Contract Verification</title>
              <link rel='icon' type='image/png' href='{logoUrl}'>
              <link rel='shortcut icon' type='image/png' href='{logoUrl}'>
              <link rel='apple-touch-icon' href='{logoUrl}'>
              <script src='https://cdn.tailwindcss.com'></script>
              <script>
                tailwind.config = {{
                  darkMode: 'class',
                  theme: {{ extend: {{ colors: {{ brand: '#FF6B00', 'brand-dark': '#E85F00' }} }} }}
                }};
              </script>
            </head>
            <body class='bg-slate-900 text-slate-100 min-h-screen flex items-center justify-center p-4 font-sans'>
              <div class='max-w-md w-full bg-slate-800 border border-red-500/40 rounded-2xl p-6 shadow-2xl text-center'>
                <img src='{logoUrl}' alt='Drive&amp;Go' class='h-10 mx-auto mb-4 object-contain' />
                <div class='w-12 h-12 rounded-full bg-red-500/20 text-red-400 flex items-center justify-center mx-auto mb-3 text-2xl font-bold'>&times;</div>
                <h2 class='text-lg font-bold text-white mb-1'>Contract Not Found</h2>
                <p class='text-xs text-slate-400 mb-4'>No active or verified rental agreement matched code: <span class='font-mono font-bold text-red-400'>{code}</span></p>
                <div class='p-3 bg-slate-900/60 rounded-xl text-xs text-slate-400'>Please contact Drive&amp;Go customer hotline: +63 935 966 7178</div>
              </div>
            </body>
            </html>";
        }

        return $@"<!DOCTYPE html>
        <html lang='en' class='dark'>
        <head>
          <meta charset='UTF-8'>
          <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'>
          <title>Verified Rental Agreement - {data.AgreementCode}</title>
          <link rel='icon' type='image/png' href='{logoUrl}'>
          <link rel='shortcut icon' type='image/png' href='{logoUrl}'>
          <link rel='apple-touch-icon' href='{logoUrl}'>
          <script src='https://cdn.tailwindcss.com'></script>
          <script>
            tailwind.config = {{
              darkMode: 'class',
              theme: {{
                extend: {{
                  colors: {{
                    brand: '#FF6B00',
                    'brand-dark': '#E85F00',
                    navy: '#0B192C'
                  }}
                }}
              }}
            }};
          </script>
          <style>
            @keyframes pulseGlow {{
              0%, 100% {{ transform: scale(1); opacity: 0.9; }}
              50% {{ transform: scale(1.05); opacity: 1; }}
            }}
            .pulse-anim {{ animation: pulseGlow 2s infinite ease-in-out; }}
            
            @keyframes scanLine {{
              0% {{ top: 0%; opacity: 0; }}
              30% {{ opacity: 1; }}
              100% {{ top: 100%; opacity: 0; }}
            }}
            .scanner-line {{
              position: absolute;
              left: 0;
              right: 0;
              height: 2px;
              background: linear-gradient(90deg, transparent, #10B981, transparent);
              animation: scanLine 1.8s infinite ease-in-out;
            }}
            
            /* Smooth transitions */
            .theme-transition {{
              transition: background-color 0.3s ease, color 0.3s ease, border-color 0.3s ease;
            }}
          </style>
        </head>
        <body class='theme-transition bg-slate-100 dark:bg-[#070B14] text-slate-800 dark:text-slate-100 min-h-screen flex items-center justify-center p-3 sm:p-6 font-sans select-none'>

          <!-- ── Full-Screen Verification Loading Overlay ── -->
          <div id='loading-screen' class='fixed inset-0 z-50 bg-[#070B14] flex flex-col items-center justify-center p-6 text-center transition-opacity duration-500'>
            <div class='relative mb-6'>
              <img src='{logoUrl}' alt='Drive&amp;Go Logo' class='h-14 w-auto object-contain mx-auto pulse-anim' />
              <div class='scanner-line'></div>
            </div>
            
            <div class='relative w-16 h-16 mb-4'>
              <div class='w-16 h-16 rounded-full border-4 border-slate-800 border-t-emerald-500 animate-spin'></div>
              <div class='absolute inset-0 flex items-center justify-center text-emerald-400'>
                <svg class='w-6 h-6' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2.5' d='M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z'></path></svg>
              </div>
            </div>

            <div class='text-sm font-black uppercase tracking-widest text-emerald-400 mb-1'>Authenticating Contract</div>
            <div class='text-xs text-slate-400 font-mono'>{data.AgreementCode}</div>
            <div class='text-[11px] text-slate-500 mt-3'>Verifying digital signature and blockchain stamp...</div>
          </div>

          <!-- ── Main Verified Agreement Container ── -->
          <div id='content-card' class='max-w-lg w-full bg-white dark:bg-[#0E1626] border border-slate-200 dark:border-slate-800/80 rounded-3xl overflow-hidden shadow-2xl transition-all duration-500 opacity-0 transform scale-95'>
            
            <!-- Top App Bar & Theme Toggle -->
            <div class='px-5 py-4 border-b border-slate-100 dark:border-slate-800/80 flex items-center justify-between bg-slate-50/50 dark:bg-[#0B111E]'>
              <img src='{logoUrl}' alt='Drive&amp;Go' class='h-8 w-auto object-contain' />
              
              <div class='flex items-center gap-2'>
                <button onclick='toggleTheme()' class='p-2 rounded-xl bg-slate-200/70 dark:bg-slate-800 text-slate-700 dark:text-slate-300 hover:text-brand transition-colors text-xs flex items-center gap-1.5 font-bold' title='Toggle Light/Dark Theme'>
                  <span id='theme-icon-container'>
                    <svg class='w-3.5 h-3.5' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z'></path></svg>
                  </span>
                  <span class='text-[10px] hidden sm:inline' id='theme-label'>Dark</span>
                </button>
              </div>
            </div>

            <!-- Header Verification Banner -->
            <div class='p-5 bg-gradient-to-r from-emerald-500/10 via-teal-500/10 to-emerald-500/5 dark:from-emerald-950/40 dark:via-teal-950/30 dark:to-emerald-950/20 border-b border-emerald-500/20'>
              <div class='flex items-start justify-between gap-3'>
                <div>
                  <div class='inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-[10px] font-black uppercase tracking-wider bg-emerald-500/15 dark:bg-emerald-500/20 text-emerald-600 dark:text-emerald-400 border border-emerald-500/30'>
                    <svg class='w-3 h-3' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='3' d='M5 13l4 4L19 7'></path></svg>
                    <span>Official &amp; Verified Contract</span>
                  </div>
                  <h1 class='text-xl font-black text-slate-900 dark:text-white mt-2 font-mono tracking-tight'>{data.AgreementCode}</h1>
                </div>
                
                <div class='text-right'>
                  <span class='inline-block px-2.5 py-1 rounded-lg text-[10px] font-black uppercase tracking-wider bg-emerald-500 text-white shadow-md shadow-emerald-500/20'>
                    {data.PaymentStatus.ToUpper()}
                  </span>
                  <div class='text-[10px] text-slate-500 dark:text-slate-400 mt-1 font-medium'>{data.CreatedDate}</div>
                </div>
              </div>
            </div>

            <!-- Information Body -->
            <div class='p-5 sm:p-6 space-y-4 text-xs'>
              
              <!-- 2-Col Specs Grid -->
              <div class='grid grid-cols-1 sm:grid-cols-2 gap-3'>
                <!-- Customer Card -->
                <div class='p-3.5 bg-slate-50 dark:bg-[#131E33] rounded-2xl border border-slate-200/80 dark:border-slate-800/80'>
                  <div class='text-[9px] uppercase font-black tracking-wider text-slate-400 mb-1'>Lessee / Customer</div>
                  <div class='font-bold text-slate-900 dark:text-white text-sm truncate'>{data.CustomerName}</div>
                  <div class='text-slate-500 dark:text-slate-400 mt-0.5'>{data.CustomerPhone}</div>
                  <div class='text-slate-400 text-[10.5px] truncate'>{data.CustomerEmail}</div>
                </div>

                <!-- Vehicle Card -->
                <div class='p-3.5 bg-slate-50 dark:bg-[#131E33] rounded-2xl border border-slate-200/80 dark:border-slate-800/80'>
                  <div class='text-[9px] uppercase font-black tracking-wider text-slate-400 mb-1'>Vehicle Details</div>
                  <div class='font-bold text-slate-900 dark:text-white text-sm truncate'>{data.VehicleName}</div>
                  <div class='text-brand font-mono font-black mt-0.5'>{data.PlateNo}</div>
                  <div class='text-slate-500 dark:text-slate-400 text-[10.5px]'>Color: {data.VehicleColor}</div>
                </div>
              </div>

              <!-- Schedule Timeline -->
              <div class='p-4 bg-slate-50 dark:bg-[#131E33] rounded-2xl border border-slate-200/80 dark:border-slate-800/80 space-y-2 text-xs'>
                <div class='flex justify-between items-center text-slate-600 dark:text-slate-300'>
                  <span class='text-slate-400'>Pick-Up Schedule:</span>
                  <strong class='text-slate-900 dark:text-emerald-400 font-bold'>{data.PickupDate}</strong>
                </div>
                <div class='flex justify-between items-center text-slate-600 dark:text-slate-300'>
                  <span class='text-slate-400'>Return Schedule:</span>
                  <strong class='text-slate-900 dark:text-red-400 font-bold'>{data.DropoffDate}</strong>
                </div>
                <div class='flex justify-between items-center text-slate-600 dark:text-slate-300 pt-1 border-t border-slate-200 dark:border-slate-800'>
                  <span class='text-slate-400'>Authorized Duration:</span>
                  <strong class='text-brand font-black'>{data.DurationDays} Day(s)</strong>
                </div>
                <div class='flex justify-between items-center text-slate-600 dark:text-slate-300'>
                  <span class='text-slate-400'>Authorized Destination:</span>
                  <strong class='text-slate-900 dark:text-white font-medium'>{data.Destination}</strong>
                </div>
              </div>

              <!-- Total Rental Value Pill -->
              <div class='p-4 bg-orange-500/10 dark:bg-orange-500/15 border border-orange-500/30 rounded-2xl flex items-center justify-between'>
                <span class='font-bold text-slate-700 dark:text-slate-200 text-xs'>Total Rental Value:</span>
                <span class='text-lg font-black text-brand'>PHP {data.TotalAmount:N2}</span>
              </div>

              <!-- Official Signatures Block -->
              <div class='grid grid-cols-2 gap-3 pt-2 text-center'>
                <div class='p-2.5 bg-slate-50 dark:bg-[#131E33] rounded-2xl border border-slate-200/80 dark:border-slate-800/80 relative flex flex-col justify-between min-h-[95px]'>
                  <div class='text-[7.5px] uppercase font-black tracking-wider text-slate-400 mb-1'>Rented By</div>
                  <div class='relative h-8 flex items-center justify-center my-auto'>
                    {(!string.IsNullOrWhiteSpace(data.CustomerSignatureBase64) ? $"<img src='{(data.CustomerSignatureBase64.StartsWith("data:") ? data.CustomerSignatureBase64 : $"data:image/png;base64,{data.CustomerSignatureBase64}")}' alt='Customer Signature' class='max-h-8 max-w-[110px] object-contain' />" : "<div class='text-slate-300 dark:text-slate-600 text-[9px] italic select-none'>(Physical Sign Here)</div>")}
                  </div>
                  <div>
                    <div class='font-bold text-[11px] text-slate-900 dark:text-white truncate mb-0.5'>{data.CustomerName}</div>
                    <div class='border-t border-slate-800 dark:border-slate-500 pt-1 text-[7.5px] font-bold text-slate-600 dark:text-slate-400'>Customer Signature</div>
                  </div>
                </div>
                <div class='p-2.5 bg-slate-50 dark:bg-[#131E33] rounded-2xl border border-slate-200/80 dark:border-slate-800/80 relative flex flex-col justify-between min-h-[95px]'>
                  <div class='text-[7.5px] uppercase font-black tracking-wider text-slate-400 mb-1'>Approved &amp; Dispatched By</div>
                  <div class='relative h-8 flex items-center justify-center my-auto'>
                    {(!string.IsNullOrWhiteSpace(data.AdminSignatureBase64) ? $"<img src='{(data.AdminSignatureBase64.StartsWith("data:") ? data.AdminSignatureBase64 : $"data:image/png;base64,{data.AdminSignatureBase64}")}' alt='Admin Signature' class='max-h-8 max-w-[110px] object-contain' />" : "")}
                  </div>
                  <div>
                    <div class='font-bold text-[11px] text-slate-900 dark:text-white truncate mb-0.5'>{data.AdminName}</div>
                    <div class='border-t border-slate-800 dark:border-slate-500 pt-1 text-[7.5px] font-bold text-slate-600 dark:text-slate-400'>Drive&amp;Go Administrator</div>
                  </div>
                </div>
              </div>

              <!-- Download PDF Action -->
              <div class='pt-1'>
                <a href='/api/Rentals/code/{data.AgreementCode}/pdf' target='_blank' class='w-full py-3.5 px-4 bg-brand hover:bg-brand-dark text-white font-extrabold text-xs uppercase tracking-wider rounded-xl transition-all shadow-lg shadow-orange-500/25 flex items-center justify-center gap-2 active:scale-95'>
                  <svg class='w-4 h-4' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2.5' d='M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4'></path></svg>
                  <span>Download Official PDF Agreement</span>
                </a>
              </div>

              <!-- Footer Hotline & Address -->
              <div class='border-t border-slate-200 dark:border-slate-800/80 pt-3 text-center text-[10.5px] text-slate-500 dark:text-slate-400 space-y-1'>
                <div>{data.CompanyAddress}</div>
                <div class='font-medium'>
                  Hotline: <a href='tel:{data.CompanyPhone}' class='text-brand hover:underline font-bold'>{data.CompanyPhone}</a> &bull; 
                  <a href='mailto:{data.CompanyEmail}' class='text-slate-600 dark:text-slate-300 hover:underline'>{data.CompanyEmail}</a>
                </div>
              </div>

            </div>
          </div>

          <script>
            const sunSvg = `<svg class='w-3.5 h-3.5' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z'></path></svg>`;
            const moonSvg = `<svg class='w-3.5 h-3.5' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z'></path></svg>`;

            // Unveil after sleek verification simulation
            setTimeout(() => {{
              const loader = document.getElementById('loading-screen');
              const content = document.getElementById('content-card');
              if (loader && content) {{
                loader.style.opacity = '0';
                setTimeout(() => loader.style.display = 'none', 500);
                content.classList.remove('opacity-0', 'scale-95');
                content.classList.add('opacity-100', 'scale-100');
              }}
            }}, 850);

            // Light & Dark mode toggle
            function toggleTheme() {{
              const html = document.documentElement;
              const iconContainer = document.getElementById('theme-icon-container');
              const label = document.getElementById('theme-label');
              
              if (html.classList.contains('dark')) {{
                html.classList.remove('dark');
                if (iconContainer) iconContainer.innerHTML = sunSvg;
                if (label) label.textContent = 'Light';
                localStorage.setItem('theme', 'light');
              }} else {{
                html.classList.add('dark');
                if (iconContainer) iconContainer.innerHTML = moonSvg;
                if (label) label.textContent = 'Dark';
                localStorage.setItem('theme', 'dark');
              }}
            }}

            // Initial theme setup
            if (localStorage.getItem('theme') === 'light' || (!('theme' in localStorage) && window.matchMedia('(prefers-color-scheme: light)').matches)) {{
              document.documentElement.classList.remove('dark');
              const iconContainer = document.getElementById('theme-icon-container');
              const label = document.getElementById('theme-label');
              if (iconContainer) iconContainer.innerHTML = sunSvg;
              if (label) label.textContent = 'Light';
            }}
        </body>
        </html>";
    }

    private static string GetCustomerResponseSuccessHtml(RentalAgreementEmailData data, string actionType)
    {
        string logoUrl = "https://raw.githubusercontent.com/martquirante/DriveAndGo_Project/main/DriveAndGo_Admin/WebAssets/logo.png";
        
        string iconSvg;
        string title;
        string subtitle;
        string badgeClass;
        string badgeText;

        if (actionType == "accept" || actionType == "custom-reschedule")
        {
            iconSvg = @"<div class='w-16 h-16 rounded-2xl bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center text-emerald-400 mx-auto mb-4'>
                <svg width='32' height='32' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2.5' stroke-linecap='round' stroke-linejoin='round'><path d='M20 6L9 17l-5-5'/></svg>
            </div>";
            title = actionType == "custom-reschedule" ? "Custom Schedule Confirmed!" : "Schedule Confirmed & Ready!";
            subtitle = "Thank you for confirming your updated travel schedule. Your vehicle is ready for dispatch.";
            badgeClass = "bg-emerald-500/10 text-emerald-400 border border-emerald-500/30";
            badgeText = "CONFIRMED & ACTIVE";
        }
        else
        {
            iconSvg = @"<div class='w-16 h-16 rounded-2xl bg-red-500/10 border border-red-500/30 flex items-center justify-center text-red-400 mx-auto mb-4'>
                <svg width='32' height='32' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2.5' stroke-linecap='round' stroke-linejoin='round'><circle cx='12' cy='12' r='10'/><line x1='15' y1='9' x2='9' y2='15'/><line x1='9' y1='9' x2='15' y2='15'/></svg>
            </div>";
            title = "Booking Cancelled & Refund Initiated";
            subtitle = "Your booking has been cancelled per your request. A 100% full reimbursement has been queued to your original payment method.";
            badgeClass = "bg-red-500/10 text-red-400 border border-red-500/30";
            badgeText = "CANCELLED / REFUND PENDING";
        }

        return $@"<!DOCTYPE html>
        <html lang='en' class='dark'>
        <head>
          <meta charset='UTF-8'>
          <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'>
          <title>{title} - Drive&amp;Go</title>
          <link rel='icon' type='image/png' href='{logoUrl}'>
          <script src='https://cdn.tailwindcss.com'></script>
          <script>
            tailwind.config = {{
              darkMode: 'class',
              theme: {{ extend: {{ colors: {{ brand: '#FF6B00', 'brand-dark': '#E85F00' }} }} }}
            }};
          </script>
        </head>
        <body class='bg-[#0B192C] text-slate-100 min-h-screen flex items-center justify-center p-4 font-sans'>
          <div class='max-w-md w-full bg-[#142338] border border-slate-700/60 rounded-3xl p-6 sm:p-8 shadow-2xl text-center animate-fadeIn'>
            <img src='{logoUrl}' alt='Drive&amp;Go' class='h-10 mx-auto mb-5 object-contain' />
            {iconSvg}
            <span class='inline-block px-3 py-1 rounded-full text-[11px] font-black tracking-wider uppercase mb-3 {badgeClass}'>{badgeText}</span>
            <h1 class='text-xl sm:text-2xl font-black text-white mb-2 tracking-tight'>{title}</h1>
            <p class='text-xs sm:text-sm text-slate-400 mb-6 leading-relaxed'>{subtitle}</p>

            <div class='p-4 bg-[#0B192C]/80 rounded-2xl border border-slate-700/50 text-left space-y-2.5 text-xs mb-6'>
              <div class='flex justify-between items-center text-slate-300'>
                <span class='text-slate-500 font-medium'>Booking Ref:</span>
                <span class='font-mono font-bold text-white'>{data.AgreementCode}</span>
              </div>
              <div class='flex justify-between items-center text-slate-300'>
                <span class='text-slate-500 font-medium'>Customer:</span>
                <span class='font-bold text-white'>{data.CustomerName}</span>
              </div>
              <div class='flex justify-between items-center text-slate-300'>
                <span class='text-slate-500 font-medium'>Vehicle:</span>
                <span class='font-bold text-white'>{data.VehicleName} ({data.PlateNo})</span>
              </div>
              <div class='flex justify-between items-center text-slate-300'>
                <span class='text-slate-500 font-medium'>Pick-Up:</span>
                <span class='font-bold text-emerald-400'>{data.PickupDate}</span>
              </div>
              <div class='flex justify-between items-center text-slate-300'>
                <span class='text-slate-500 font-medium'>Return:</span>
                <span class='font-bold text-red-400'>{data.DropoffDate}</span>
              </div>
            </div>

            <div class='space-y-2.5'>
              <a href='{data.VerificationUrl}' class='block w-full py-3 rounded-xl bg-brand hover:bg-brand-dark text-white font-extrabold text-xs shadow-lg shadow-orange-500/25 transition-all text-center'>
                View Live Verified Contract
              </a>
              <div class='text-[11px] text-slate-500 pt-2'>
                Need help? Contact Drive&amp;Go Hotline: <strong class='text-slate-400'>+63 935 966 7178</strong>
              </div>
            </div>
          </div>
        </body>
        </html>";
    }

    private static string GetCustomerRescheduleFormHtml(RentalAgreementEmailData data)
    {
        string logoUrl = "https://raw.githubusercontent.com/martquirante/DriveAndGo_Project/main/DriveAndGo_Admin/WebAssets/logo.png";
        var defaultStart = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm");
        var minStart = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm");

        return $@"<!DOCTYPE html>
        <html lang='en' class='dark'>
        <head>
          <meta charset='UTF-8'>
          <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'>
          <title>Choose Custom Dates - {data.AgreementCode}</title>
          <link rel='icon' type='image/png' href='{logoUrl}'>
          <script src='https://cdn.tailwindcss.com'></script>
          <script>
            tailwind.config = {{
              darkMode: 'class',
              theme: {{ extend: {{ colors: {{ brand: '#FF6B00', 'brand-dark': '#E85F00' }} }} }}
            }};
          </script>
        </head>
        <body class='bg-[#0B192C] text-slate-100 min-h-screen flex items-center justify-center p-4 font-sans'>
          <div class='max-w-md w-full bg-[#142338] border border-slate-700/60 rounded-3xl p-6 sm:p-8 shadow-2xl text-center animate-fadeIn'>
            <img src='{logoUrl}' alt='Drive&amp;Go' class='h-10 mx-auto mb-4 object-contain' />
            <div class='w-14 h-14 rounded-2xl bg-brand/10 border border-brand/30 flex items-center justify-center text-brand mx-auto mb-3'>
              <svg width='28' height='28' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><rect x='3' y='4' width='18' height='18' rx='2' ry='2'/><line x1='16' y1='2' x2='16' y2='6'/><line x1='8' y1='2' x2='8' y2='6'/><line x1='3' y1='10' x2='21' y2='10'/></svg>
            </div>
            <h1 class='text-xl font-black text-white mb-1.5'>Choose Your Desired Dates</h1>
            <p class='text-xs text-slate-400 mb-5'>Select your preferred new pick-up date. Your {data.DurationDays}-day duration will automatically be applied with zero fees.</p>

            <form method='GET' action='/api/Rentals/respond/{data.AgreementCode}' class='text-left space-y-4 text-xs'>
              <input type='hidden' name='action' value='reschedule' />
              
              <div>
                <label class='block font-bold text-slate-300 mb-1.5'>New Pick-Up Date &amp; Time</label>
                <input type='datetime-local' name='startDate' value='{defaultStart}' min='{minStart}' required class='w-full px-3.5 py-2.5 rounded-xl bg-[#0B192C] border border-slate-700 text-white font-mono text-xs focus:outline-none focus:border-brand' />
              </div>

              <div class='p-3.5 bg-[#0B192C]/70 rounded-xl border border-slate-700/50 space-y-1.5 text-slate-400'>
                <div class='flex justify-between'>
                  <span>Vehicle Model:</span>
                  <strong class='text-white'>{data.VehicleName}</strong>
                </div>
                <div class='flex justify-between'>
                  <span>Package Duration:</span>
                  <strong class='text-brand font-bold'>{data.DurationDays} Days</strong>
                </div>
                <div class='flex justify-between'>
                  <span>Rebooking Fee:</span>
                  <strong class='text-emerald-400 font-bold'>FREE (WAIVED)</strong>
                </div>
              </div>

              <button type='submit' class='w-full py-3 rounded-xl bg-brand hover:bg-brand-dark text-white font-black text-xs shadow-lg shadow-orange-500/25 transition-all text-center mt-2'>
                Confirm New Dates &amp; Activate
              </button>
            </form>
          </div>
        </body>
        </html>";
    }


    public class HandoverRequest
    {
        public decimal? OdometerMileage { get; set; }
        public string? FuelLevel { get; set; }
    }



    public class CompleteRentalRequest
    {
        public decimal? ReturnOdometer    { get; set; }
        public string?  ReturnFuelLevel   { get; set; }
        public decimal? PenaltyFee        { get; set; }
        public decimal? DamageFee         { get; set; }
        public string?  ReturnNotes       { get; set; }
        public bool     HasDamage         { get; set; } = false;
        public string?  DamageSeverity    { get; set; }
        public string?  DamageDescription { get; set; }
        public List<string>? DamagePhotos { get; set; } = new();

        // Customer Email Override (editable by admin)
        public string?  RecipientEmail    { get; set; }

        // Thank-You promo gift (optional, chosen by admin)
        public bool    IncludePromoGift  { get; set; } = false;
        public string? SelectedPromoCode { get; set; }
    }


}

public class SplitRequest
{
    public List<SplitShare> Shares { get; set; } = new();
}

public class SplitShare
{
    public string  Email  { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

