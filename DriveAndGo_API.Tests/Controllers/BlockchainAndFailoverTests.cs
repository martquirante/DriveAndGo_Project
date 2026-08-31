using System;
using System.Threading.Tasks;
using DriveAndGo_API.Services;
using DriveAndGo_API.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DriveAndGo_API.Tests.Controllers
{
    [Collection("TestDb")]
    public class BlockchainAndFailoverTests : IAsyncLifetime
    {
        private readonly TestDbFixture _fixture;

        public BlockchainAndFailoverTests(TestDbFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => Task.CompletedTask;
        public async Task DisposeAsync() => await _fixture.ResetDatabaseAsync();

        [Fact]
        public async Task BlockchainService_AppendAndVerifyChain_ReturnsValidIntegrity()
        {
            // Arrange
            using var scope = _fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DriveAndGo_API.Data.AppDbContext>();
            var blockchainService = scope.ServiceProvider.GetRequiredService<IBlockchainService>();

            var customer = new DriveAndGo_API.Models.User
            {
                FullName = "Blockchain Test User",
                Email = "blockchain_test@driveandgo.com",
                PasswordHash = "hash123",
                Phone = "09170000001",
                Role = "customer"
            };
            db.Users.Add(customer);

            var vehicle = new DriveAndGo_API.Models.Vehicle
            {
                Brand = "Honda",
                Model = "Civic",
                PlateNo = "BLK-999",
                Status = "available",
                RatePerDay = 2500m,
                CreatedAt = DateTime.UtcNow
            };
            db.Vehicles.Add(vehicle);
            await db.SaveChangesAsync();

            var rental = new DriveAndGo_API.Models.Rental
            {
                CustomerId = customer.UserId,
                VehicleId = vehicle.VehicleId,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(3),
                Status = "approved",
                TotalAmount = 7500m,
                PaymentMethod = "gcash",
                PaymentStatus = "paid"
            };
            db.Rentals.Add(rental);
            await db.SaveChangesAsync();

            int testRentalId = rental.RentalId;

            // Act - Append two sequential blocks for this rental
            string hash1 = await blockchainService.AppendBlockAsync(
                testRentalId, 
                "CONTRACT_CREATED", 
                new { terms = "Standard 3-day car rental", rate = 2500 });

            string hash2 = await blockchainService.AppendBlockAsync(
                testRentalId, 
                "PAYMENT_CONFIRMED", 
                new { amount = 7500, method = "GCash", refNo = "GC-987654" });

            // Assert
            hash1.Should().NotBeNullOrWhiteSpace().And.HaveLength(64);
            hash2.Should().NotBeNullOrWhiteSpace().And.HaveLength(64);
            hash1.Should().NotBe(hash2);

            // Verify the cryptographic chain
            var verification = await blockchainService.VerifyRentalChainAsync(testRentalId);
            verification.IsValid.Should().BeTrue();
            verification.TotalBlocks.Should().Be(2);
            verification.LatestHash.Should().Be(hash2);
            verification.Message.Should().Contain("verified");
        }

        [Fact]
        public void DbFailoverEngine_LocalMode_ResolvesLocalConnectionString()
        {
            // Arrange
            using var scope = _fixture.Services.CreateScope();
            var failoverEngine = scope.ServiceProvider.GetRequiredService<IDbFailoverEngine>();

            // Act
            var activeConn = failoverEngine.GetActiveConnectionString();

            // Assert
            activeConn.Should().NotBeNullOrWhiteSpace();
            failoverEngine.ActiveProviderName.Should().NotBeNullOrWhiteSpace();
        }
    }
}
