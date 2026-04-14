using System.Text.Json.Serialization;

namespace DriveAndGo_App.Models;

public sealed class RentalItem
{
    [JsonPropertyName("rentalId")]
    public int RentalId { get; set; }

    [JsonPropertyName("customerId")]
    public int CustomerId { get; set; }

    [JsonPropertyName("vehicleId")]
    public int VehicleId { get; set; }

    [JsonPropertyName("driverId")]
    public int? DriverId { get; set; }

    [JsonPropertyName("destination")]
    public string? Destination { get; set; }

    [JsonPropertyName("startDate")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime? EndDate { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("paymentMethod")]
    public string PaymentMethod { get; set; } = "cash";

    [JsonPropertyName("paymentStatus")]
    public string PaymentStatus { get; set; } = "unpaid";

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("customerName")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("customerPhone")]
    public string? CustomerPhone { get; set; }

    [JsonPropertyName("customerEmail")]
    public string? CustomerEmail { get; set; }

    [JsonPropertyName("vehicleName")]
    public string? VehicleName { get; set; }

    [JsonPropertyName("vehiclePlateNo")]
    public string? VehiclePlateNo { get; set; }

    [JsonPropertyName("driverName")]
    public string? DriverName { get; set; }

    [JsonPropertyName("driverPhone")]
    public string? DriverPhone { get; set; }

    public int RentalDays => Math.Max(1, ((EndDate?.Date ?? StartDate.Date) - StartDate.Date).Days);
    public string DateRangeLabel => $"{StartDate:MMM dd} - {(EndDate ?? StartDate):MMM dd, yyyy}";
    public string TotalAmountLabel => $"PHP {TotalAmount:N0}";
    public string StatusLabel => Status switch
    {
        "rejected" => "Cancelled",
        _ => string.IsNullOrWhiteSpace(Status) ? "Unknown" : char.ToUpper(Status[0]) + Status[1..]
    };
    public bool CanBeCancelled => Status is "pending" or "approved";
    public bool CanBeRated => string.Equals(Status, "completed", StringComparison.OrdinalIgnoreCase);
    public bool NeedsPayment => !string.Equals(PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase);
}
