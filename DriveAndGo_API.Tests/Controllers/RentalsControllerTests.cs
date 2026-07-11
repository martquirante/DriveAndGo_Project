using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DriveAndGo_API.Data;
using DriveAndGo_API.Models;
using DriveAndGo_API.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DriveAndGo_API.Tests.Controllers
{
    [Collection("TestDb")]
    public class RentalsControllerTests : IAsyncLifetime
    {
        private readonly TestDbFixture _fixture;
        private readonly HttpClient _client;

        public RentalsControllerTests(TestDbFixture fixture)
        {
            _fixture = fixture;
            _client = _fixture.CreateClient();
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            // Reset the database to keep tests isolated
            await _fixture.ResetDatabaseAsync();
        }

        [Fact]
        public async Task AddRental_WithInvalidVehicleId_RollsBackTransactionAndFails()
        {
            // Arrange
            using var scope = _fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Seed a customer
            var customer = new User
            {
                FullName = "Test Customer",
                Email = "test_customer@driveandgo.com",
                PasswordHash = "hashed_password",
                Phone = "1234567890",
                Role = "customer"
            };
            db.Users.Add(customer);
            await db.SaveChangesAsync();

            // Create a rental with a non-existent vehicle ID (99999) to force a DB foreign key constraint failure
            var rentalPayload = new Rental
            {
                CustomerId = customer.UserId,
                VehicleId = 99999, // Non-existent vehicle
                StartDate = DateTime.UtcNow.AddDays(1),
                EndDate = DateTime.UtcNow.AddDays(5),
                Destination = "Manila",
                TotalAmount = 500.00m,
                PaymentMethod = "cash"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/rentals", rentalPayload);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound); // Handled response by controller

            // Confirm that the database remains clean and no orphan rental was written
            db.Rentals.Count(r => r.CustomerId == customer.UserId).Should().Be(0);
        }

        [Fact]
        public async Task ApproveRental_ConcurrentRequests_OnlyOneSucceeds()
        {
            // Arrange
            using var scope = _fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Seed a customer
            var customer = new User
            {
                FullName = "Test Customer",
                Email = "concurrency_customer@driveandgo.com",
                PasswordHash = "hashed_password",
                Phone = "1234567890",
                Role = "customer"
            };
            db.Users.Add(customer);

            // Seed a vehicle
            var vehicle = new Vehicle
            {
                Brand = "Toyota",
                Model = "Vios",
                PlateNo = "CON-123",
                Status = "available",
                RatePerDay = 1500.00m,
                RateWithDriver = 2000.00m,
                CreatedAt = DateTime.UtcNow
            };
            db.Vehicles.Add(vehicle);
            await db.SaveChangesAsync();

            // Seed a pending rental
            var rental = new Rental
            {
                CustomerId = customer.UserId,
                VehicleId = vehicle.VehicleId,
                StartDate = DateTime.UtcNow.AddDays(1),
                EndDate = DateTime.UtcNow.AddDays(3),
                Status = "pending",
                TotalAmount = 3000.00m,
                PaymentMethod = "cash",
                PaymentStatus = "unpaid"
            };
            db.Rentals.Add(rental);
            await db.SaveChangesAsync();

            // Act - Send concurrent approval requests
            var request1 = _client.PatchAsync($"/api/rentals/{rental.RentalId}/approve", null);
            var request2 = _client.PatchAsync($"/api/rentals/{rental.RentalId}/approve", null);

            var responses = await Task.WhenAll(request1, request2);

            var statusCodes = responses.Select(r => r.StatusCode).ToList();

            // Assert
            // One request must succeed (200 OK)
            statusCodes.Should().Contain(HttpStatusCode.OK);

            // The other request must return Bad Request (400) because it was already approved
            // Or Conflict (409) if it hits concurrency limits
            statusCodes.Should().Contain(code => code == HttpStatusCode.BadRequest || code == HttpStatusCode.Conflict);

            // Refresh context and verify database state
            db.ChangeTracker.Clear();
            var updatedRental = await db.Rentals.FindAsync(rental.RentalId);
            updatedRental!.Status.Should().Be("approved");
        }
    }
}
