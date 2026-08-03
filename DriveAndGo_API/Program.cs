using System.Text;
using DriveAndGo_API.Data;
using DriveAndGo_API.Services;
using DriveAndGo_API.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// ─────────────────────────────────────────────────────────────
//  1.  Load .env file (before anything else reads configuration)
// ─────────────────────────────────────────────────────────────
// TraversePath() searches upwards from the current directory to find the .env file.
// This is more robust for Visual Studio, IIS Express, and testing scenarios.
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────
//  2.  Controllers & Swagger
// ─────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "DriveAndGo API", Version = "v1" });

    // Add JWT Bearer to Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter: Bearer {your_jwt_token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ─────────────────────────────────────────────────────────────
//  3.  PostgreSQL / EF Core (Supabase / local Docker)
// ─────────────────────────────────────────────────────────────
// Smart Environment-Based Database Routing
var connectionString = builder.Environment.IsDevelopment()
    ? (Environment.GetEnvironmentVariable("LOCAL_DB_CONNECTION") 
       ?? builder.Configuration.GetConnectionString("DefaultConnection"))
    : (Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING") 
       ?? builder.Configuration.GetConnectionString("DefaultConnection"));

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("No database connection string found in .env or appsettings.");
}

// Initialize Database Tables
DatabaseInitializer.Initialize(connectionString);

// Register NpgsqlDataSource with retry-on-failure for Supabase PgBouncer resilience
var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.UseLoggerFactory(LoggerFactory.Create(b => b.AddConsole()));
var dataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(dataSource);
builder.Services.AddSingleton<Npgsql.NpgsqlDataSource>(dataSource);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(dataSource));

// ─────────────────────────────────────────────────────────────
//  4.  Application Services (DI)
// ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();
builder.Services.AddScoped<DbService>();
builder.Services.AddScoped<NotificationWriter>();
builder.Services.AddScoped<IFirebaseService, FirebaseService>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IStorageService, StorageService>();
builder.Services.AddScoped<DriveAndGo_API.Services.AuditService>();
builder.Services.AddHostedService<DriveAndGo_API.Services.RentalComplianceWorker>();

// ── Fleet & Driver Operations Service (Phase 3) ──────────────────────
builder.Services.AddScoped<DriveAndGo_API.Services.Operations.IFleetOperationsService,
                           DriveAndGo_API.Services.Operations.FleetOperationsService>();

// ── Risk & Security Services (Phase 4) ──────────────────────────────
builder.Services.AddScoped<DriveAndGo_API.Services.Risk.IAiVisionService,
                           DriveAndGo_API.Services.Risk.AiVisionService>();
builder.Services.AddScoped<DriveAndGo_API.Services.Risk.IFinanceRiskService,
                           DriveAndGo_API.Services.Risk.FinanceRiskService>();

// ── AI Copilot Engine ──────────────────────────────────────────────
builder.Services.AddScoped<DriveAndGo_API.Services.Ai.AiToolsService>();
builder.Services.AddScoped<DriveAndGo_API.Services.Ai.IAiOrchestrationService,
                           DriveAndGo_API.Services.Ai.AiOrchestrationService>();


// ─────────────────────────────────────────────────────────────
//  5.  JWT Authentication
// ─────────────────────────────────────────────────────────────
var jwtKey     = builder.Configuration["Jwt:SecretKey"]
    ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? "DriveAndGo-FallbackKey-MustChangeInProduction!";
var jwtIssuer  = builder.Configuration["Jwt:Issuer"]   ?? "DriveAndGoAPI";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "DriveAndGoClients";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtIssuer,
            ValidAudience            = jwtAudience,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew                = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ─────────────────────────────────────────────────────────────
//  6.  CORS
// ─────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ─────────────────────────────────────────────────────────────
//  7.  Build & Middleware Pipeline
// ─────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");

var uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, "wwwroot")),
    RequestPath  = ""
});

// ── Authentication must come before Authorization ──
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<DriveAndGo_API.Hubs.AdminHub>("/hubs/admin");
app.MapHealthChecks("/api/health");
app.MapControllers();
app.Run();
