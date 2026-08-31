using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using DriveAndGo_API.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Xunit;

namespace DriveAndGo_API.Tests.Fixtures
{
    public class TestDbFixture : WebApplicationFactory<Program>, IAsyncLifetime
    {
        public string ConnectionString { get; private set; } = null!;
        private Respawner _respawner = null!;
        private NpgsqlConnection _dbConnection = null!;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Set the environment to Testing (so it doesn't load appsettings.Development.json by default, 
            // but we will build configuration from our test files)
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: true);
                // Force test db credentials
                config.AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", ConnectionString)
                });
            });

            builder.ConfigureServices(services =>
            {
                // Remove existing AppDbContext and NpgsqlDataSource registrations
                var dbContextDescriptors = services.Where(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                         d.ServiceType == typeof(AppDbContext)).ToList();
                foreach (var descriptor in dbContextDescriptors)
                {
                    services.Remove(descriptor);
                }

                var dataSourceDescriptors = services.Where(
                    d => d.ServiceType == typeof(NpgsqlDataSource) ||
                         d.ServiceType == typeof(Npgsql.NpgsqlDataSource)).ToList();
                foreach (var descriptor in dataSourceDescriptors)
                {
                    services.Remove(descriptor);
                }

                // Register DataSource and DbContext pointing to our local test Docker database
                var dataSource = NpgsqlDataSource.Create(ConnectionString);
                services.AddSingleton(dataSource);
                services.AddSingleton<NpgsqlDataSource>(dataSource);

                services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(dataSource));
            });
        }

        public async Task InitializeAsync()
        {
            // Set up test connection string (localhost Docker database)
            ConnectionString = "Host=localhost;Port=5432;Database=driveandgo_test_db;Username=postgres;Password=postgres_local_password;";

            // Assert connection string is safe and isolated
            EnvironmentGuard.AssertSafeEnvironment(ConnectionString);

            // Establish database connection to configure Respawn
            _dbConnection = new NpgsqlConnection(ConnectionString);
            await _dbConnection.OpenAsync();

            // Run database migrations on startup to build the schema
            using (var scope = Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await dbContext.Database.MigrateAsync();
            }

            DriveAndGo_API.Services.DatabaseInitializer.Initialize(ConnectionString);

            // Create Respawner
            _respawner = await Respawner.CreateAsync(_dbConnection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                TablesToIgnore = new[]
                {
                    new Respawn.Graph.Table("__EFMigrationsHistory")
                }
            });
        }

        public async Task ResetDatabaseAsync()
        {
            await _respawner.ResetAsync(_dbConnection);
            DriveAndGo_API.Services.DatabaseInitializer.Initialize(ConnectionString);
        }

        public new async Task DisposeAsync()
        {
            if (_dbConnection != null)
            {
                await _dbConnection.DisposeAsync();
            }
            await base.DisposeAsync();
        }
    }

    [CollectionDefinition("TestDb")]
    public class TestDbCollection : ICollectionFixture<TestDbFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
