var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Ito na 'yung tatawag sa in-install nating Swagger UI!
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Laging naka-on ang Swagger
app.UseSwagger();
app.UseSwaggerUI();

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