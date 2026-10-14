using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RailwayTicketing.Api.Data;
using RailwayTicketing.Api.Domain;
using RailwayTicketing.Api.Options;
using RailwayTicketing.Api.Services;

namespace RailwayTicketing.Tests;

/// <summary>
/// Builds an isolated in-memory application context for each test so that
/// seat inventory assertions cannot leak between tests.
/// </summary>
public sealed class TestHarness : IDisposable
{
    public TestHarness(string? webhookSecret = null)
    {
        var options = new DbContextOptionsBuilder<RailwayDbContext>()
            .UseInMemoryDatabase($"railway-tests-{Guid.NewGuid():N}")
            .EnableSensitiveDataLogging()
            .Options;

        Db = new RailwayDbContext(options);
        Db.Database.EnsureCreated();

        BookingOptions = Microsoft.Extensions.Options.Options.Create(new BookingOptions());
        RefundPolicy = Microsoft.Extensions.Options.Options.Create(new RefundPolicyOptions());
        PaymentOptions = Microsoft.Extensions.Options.Options.Create(new PaymentOptions
        {
            WebhookSecret = webhookSecret ?? "unit-test-secret"
        });

        Notifications = new NotificationService(Db, NullLogger<NotificationService>.Instance);
        Search = new TrainSearchService(Db);
        Profiles = new PassengerProfileService(Db);
        Bookings = new BookingService(Db, Notifications, BookingOptions, RefundPolicy);
        Payments = new PaymentService(Db, Bookings, PaymentOptions);
        Support = new SupportService(Db);
    }

    public RailwayDbContext Db { get; }

    public IOptions<BookingOptions> BookingOptions { get; }

    public IOptions<RefundPolicyOptions> RefundPolicy { get; }

    public IOptions<PaymentOptions> PaymentOptions { get; }

    public INotificationService Notifications { get; }

    public ITrainSearchService Search { get; }

    public IPassengerProfileService Profiles { get; }

    public IBookingService Bookings { get; }

    public IPaymentService Payments { get; }

    public ISupportService Support { get; }

    /// <summary>Seeds one train with one schedule and returns the schedule id.</summary>
    public int SeedSchedule(
        string source = "NDLS",
        string destination = "BCT",
        DateTime? departureUtc = null,
        int thirdAcSeats = 4,
        decimal thirdAcFare = 1000m)
    {
        var departure = departureUtc ?? DateTime.UtcNow.AddDays(10);

        var train = new Train
        {
            Number = "T" + Random.Shared.Next(10000, 99999),
            Name = "Test Express",
            SourceStationCode = source,
            DestinationStationCode = destination
        };

        var schedule = new TrainSchedule
        {
            Train = train,
            DepartureUtc = departure,
            ArrivalUtc = departure.AddHours(12),
            Status = "Scheduled",
            SeatInventories = new List<SeatInventory>
            {
                new() { TravelClass = TravelClass.SleeperClass, TotalSeats = 10, AvailableSeats = 10, Fare = 500m },
                new() { TravelClass = TravelClass.ThirdAc, TotalSeats = thirdAcSeats, AvailableSeats = thirdAcSeats, Fare = thirdAcFare }
            }
        };

        Db.Trains.Add(train);
        Db.TrainSchedules.Add(schedule);
        Db.SaveChanges();
        return schedule.Id;
    }

    public int AvailableSeats(int scheduleId, TravelClass travelClass) => Db.SeatInventories
        .AsNoTracking()
        .Single(i => i.TrainScheduleId == scheduleId && i.TravelClass == travelClass)
        .AvailableSeats;

    public void Dispose() => Db.Dispose();
}
