using Microsoft.Extensions.FileProviders;
using DriveAndGo_API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<DriveAndGo_API.Services.DbService>();

// Configurations para sa Firebase
builder.Services.Configure<FirebaseBridgeOptions>(builder.Configuration.GetSection("FirebaseBridge"));

// ITO ANG TAMA AT NAG-IISANG SYNC SERVICE NA DAPAT TUMAKBO:
builder.Services.AddHostedService<FirebaseSyncService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");

var uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads");
if (!Directory.Exists(uploadsPath))
    Directory.CreateDirectory(uploadsPath);

app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "wwwroot")),
    RequestPath = ""
});

app.UseAuthorization();
app.MapControllers();
app.Run();