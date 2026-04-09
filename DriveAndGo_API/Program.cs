using DriveAndGo_API.Services; // ── DAGDAG ITO SA TAAS (Para makilala ang Service) ──

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddHostedService<FirebaseSyncService>();

// ── CORS para makapag-connect ang Admin app at Mobile app ──
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ── DAGDAG ITO: Buhayin ang Firebase Auto-Sync Background Worker ──
builder.Services.AddHostedService<FirebaseSyncService>();

var app = builder.Build();

// Laging naka-on ang Swagger
app.UseSwagger();
app.UseSwaggerUI();

// ── CORS middleware, dapat bago UseAuthorization ──
app.UseCors();

// Automatic redirect para iwas 404 Error
app.MapGet("/", context =>
{
    context.Response.Redirect("/swagger");
    return Task.CompletedTask;
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();