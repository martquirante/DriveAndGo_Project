using System.Text;
using DriveAndGo_API.Data;
using DriveAndGo_API.Services;
using DriveAndGo_API.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

// ─────────────────────────────────────────────────────────────
//  1.  Load .env file (before anything else reads configuration)
// ─────────────────────────────────────────────────────────────
// TraversePath() searches upwards from the current directory to find the .env file.
// This is more robust for Visual Studio, IIS Express, and testing scenarios.
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

var logsDir = Path.Combine(builder.Environment.ContentRootPath, "logs");
if (!Directory.Exists(logsDir))
{
    Directory.CreateDirectory(logsDir);
}

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(logsDir, "driveandgo-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// ─────────────────────────────────────────────────────────────
//  1b. Override Configuration with .env secrets dynamically
// ─────────────────────────────────────────────────────────────
foreach (System.Collections.DictionaryEntry env in Environment.GetEnvironmentVariables())
{
    string? key = env.Key?.ToString();
    string? val = env.Value?.ToString();
    if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(val))
    {
        builder.Configuration[key] = val;
    }
}

var envDefaultConn = Environment.GetEnvironmentVariable("DEFAULT_CONNECTION")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
if (!string.IsNullOrWhiteSpace(envDefaultConn))
{
    builder.Configuration["ConnectionStrings:DefaultConnection"] = envDefaultConn;
}

var envJwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? Environment.GetEnvironmentVariable("Jwt__SecretKey");
if (!string.IsNullOrWhiteSpace(envJwtSecret))
{
    builder.Configuration["Jwt:SecretKey"] = envJwtSecret;
}

var envFirebaseProj = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID")
    ?? Environment.GetEnvironmentVariable("Firebase__ProjectId");
if (!string.IsNullOrWhiteSpace(envFirebaseProj))
{
    builder.Configuration["Firebase:ProjectId"] = envFirebaseProj;
}

var envFirebaseUrl = Environment.GetEnvironmentVariable("FIREBASE_DATABASE_URL")
    ?? Environment.GetEnvironmentVariable("Firebase__DatabaseUrl");
if (!string.IsNullOrWhiteSpace(envFirebaseUrl))
{
    builder.Configuration["Firebase:DatabaseUrl"] = envFirebaseUrl;
}

var envSmtpEmail = Environment.GetEnvironmentVariable("SMTP_EMAIL")
    ?? Environment.GetEnvironmentVariable("Smtp__Email");
if (!string.IsNullOrWhiteSpace(envSmtpEmail))
{
    builder.Configuration["Smtp:Email"] = envSmtpEmail;
}

var envSmtpPass = Environment.GetEnvironmentVariable("SMTP_APP_PASSWORD")
    ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD")
    ?? Environment.GetEnvironmentVariable("Smtp__AppPassword");
if (!string.IsNullOrWhiteSpace(envSmtpPass))
{
    builder.Configuration["Smtp:AppPassword"] = envSmtpPass;
}

var envSupabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
    ?? Environment.GetEnvironmentVariable("Supabase__Url");
if (!string.IsNullOrWhiteSpace(envSupabaseUrl))
{
    builder.Configuration["Supabase:Url"] = envSupabaseUrl;
}

var envSupabaseKey = Environment.GetEnvironmentVariable("SUPABASE_SECRET_KEY")
    ?? Environment.GetEnvironmentVariable("Supabase__SecretKey");
if (!string.IsNullOrWhiteSpace(envSupabaseKey))
{
    builder.Configuration["Supabase:SecretKey"] = envSupabaseKey;
}

// ─────────────────────────────────────────────────────────────
//  2.  Controllers & Swagger
// ─────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // Serialize null properties so mediaType/mediaUrl always appear in JSON
        opts.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never;
    });
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "DriveAndGo API", Version = "v1" });

    c.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

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
//  2b. FormOptions — allow large file uploads (up to 500 MB)
// ─────────────────────────────────────────────────────────────
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit    = long.MaxValue;
    options.ValueLengthLimit            = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
    options.MemoryBufferThreshold       = int.MaxValue;
});
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.Limits.MaxRequestBodySize = null; // Unlimited max request body size

    // Azure App Service / Container Apps dynamic port detection
    var portEnv = Environment.GetEnvironmentVariable("PORT")
               ?? Environment.GetEnvironmentVariable("WEBSITES_PORT");

    int port = 5233; // Default local testing port
    if (!string.IsNullOrWhiteSpace(portEnv) && int.TryParse(portEnv, out int customPort))
    {
        port = customPort;
    }

    kestrel.ListenAnyIP(port);
});

// ─────────────────────────────────────────────────────────────
bool useLocalDb = string.Equals(Environment.GetEnvironmentVariable("USE_LOCAL_DB"), "true", StringComparison.OrdinalIgnoreCase);
bool useBackupDb = string.Equals(Environment.GetEnvironmentVariable("USE_BACKUP_DB"), "true", StringComparison.OrdinalIgnoreCase);

var connectionString = useLocalDb
    ? (Environment.GetEnvironmentVariable("LOCAL_DB_CONNECTION")
       ?? "Host=localhost;Port=5432;Database=driveandgo_test_db;Username=postgres;Password=postgres_local_password;")
    : (useBackupDb
       ? (Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING") ?? Environment.GetEnvironmentVariable("DEFAULT_CONNECTION"))
       : (Environment.GetEnvironmentVariable("AZURE_POSTGRES_CONNECTION_STRING")
          ?? Environment.GetEnvironmentVariable("DEFAULT_CONNECTION")
          ?? Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING")
          ?? builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;

if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("YOUR_DB_PASSWORD"))
{
    throw new InvalidOperationException("No valid database connection string found in .env or appsettings.");
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
//  Application Services & Enterprise Infrastructure (DI)
// ─────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("AuthPolicy", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.AddSlidingWindowLimiter("GeneralPolicy", opt =>
    {
        opt.PermitLimit = 120;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow = 4;
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });
});

// Dual-Cloud Automatic Failover Engine & Blockchain Ledger Service
builder.Services.AddSingleton<IDbFailoverEngine, DbFailoverEngine>();
builder.Services.AddHostedService(sp => (DbFailoverEngine)sp.GetRequiredService<IDbFailoverEngine>());
builder.Services.AddScoped<IBlockchainService, BlockchainService>();

builder.Services.AddHealthChecks();
builder.Services.AddScoped<DbService>();
builder.Services.AddScoped<NotificationWriter>();
builder.Services.AddScoped<IFirebaseService, FirebaseService>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<PdfService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<IStorageService, StorageService>();
builder.Services.AddScoped<DriveAndGo_API.Services.AuditService>();
builder.Services.AddHostedService<DriveAndGo_API.Services.RentalComplianceWorker>();
builder.Services.AddHostedService<DriveAndGo_API.Services.TrafficClosureWorker>();

// ── Traffic & Flood Incident Intelligence Service ─────────────────────
builder.Services.AddScoped<DriveAndGo_API.Services.ITrafficIncidentAggregatorService,
                           DriveAndGo_API.Services.TrafficIncidentAggregatorService>();

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
//  JWT Authentication
// ─────────────────────────────────────────────────────────────
var jwtKey     = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? builder.Configuration["Jwt:SecretKey"]
    ?? "DriveAndGo-FallbackKey-MustChangeInProduction!";
var jwtIssuer  = Environment.GetEnvironmentVariable("JWT_ISSUER")
    ?? builder.Configuration["Jwt:Issuer"]
    ?? "DriveAndGoAPI";
var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
    ?? builder.Configuration["Jwt:Audience"]
    ?? "DriveAndGoClients";

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
//  CORS
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
//  Build & Middleware Pipeline
// ─────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseSerilogRequestLogging();
app.UseRateLimiter();

// Enable Swagger in Development/Testing, or when ENABLE_SWAGGER="true"
if (app.Environment.IsDevelopment() || string.Equals(Environment.GetEnvironmentVariable("ENABLE_SWAGGER"), "true", StringComparison.OrdinalIgnoreCase))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("AllowAll");

var uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

var fileProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
fileProvider.Mappings[".jfif"] = "image/jpeg";
fileProvider.Mappings[".webp"] = "image/webp";
fileProvider.Mappings[".pdf"] = "application/pdf";

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath  = "/uploads",
    ContentTypeProvider = fileProvider,
    ServeUnknownFileTypes = true,
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Headers", "*");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "*");
    }
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, "wwwroot")),
    RequestPath  = "",
    ContentTypeProvider = fileProvider,
    ServeUnknownFileTypes = true
});

// ── Authentication must come before Authorization ──
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<DriveAndGo_API.Hubs.AdminHub>("/hubs/admin");
app.MapHealthChecks("/api/health");
app.MapControllers();
app.Run();
