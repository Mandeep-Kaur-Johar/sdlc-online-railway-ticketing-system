using Microsoft.EntityFrameworkCore;
using RailwayTicketing.Api.BackgroundJobs;
using RailwayTicketing.Api.Data;
using RailwayTicketing.Api.Middleware;
using RailwayTicketing.Api.Options;
using RailwayTicketing.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<BookingOptions>(builder.Configuration.GetSection("Booking"));
builder.Services.Configure<RefundPolicyOptions>(builder.Configuration.GetSection("RefundPolicy"));
builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection("Payments"));

var connectionString = builder.Configuration.GetConnectionString("RailwayDb");

builder.Services.AddDbContext<RailwayDbContext>(options =>
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseInMemoryDatabase("railwaydb");
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

builder.Services.AddScoped<ITrainSearchService, TrainSearchService>();
builder.Services.AddScoped<IPassengerProfileService, PassengerProfileService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ISupportService, SupportService>();
builder.Services.AddHostedService<SeatHoldExpiryJob>();

const string ClientCorsPolicy = "client";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy(ClientCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RailwayDbContext>();
    db.Database.EnsureCreated();
    SeedData.Apply(db);
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors(ClientCorsPolicy);

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", utc = DateTime.UtcNow }));
app.MapGet("/health/ready", async (RailwayDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    return canConnect
        ? Results.Ok(new { status = "Ready" })
        : Results.StatusCode(503);
});

app.MapControllers();

app.Run();

public partial class Program
{
}
