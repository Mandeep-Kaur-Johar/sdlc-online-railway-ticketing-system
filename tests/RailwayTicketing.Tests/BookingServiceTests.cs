using RailwayTicketing.Api.Domain;
using RailwayTicketing.Api.Services;
using Xunit;

namespace RailwayTicketing.Tests;

/// <summary>User Story 3003 - booking captures passenger details, reserves seats and generates a booking reference.</summary>
public class BookingServiceTests
{
    [Fact]
    public async Task Create_reserves_seats_captures_passengers_and_returns_reference()
    {
        using var harness = new TestHarness();
        var scheduleId = harness.SeedSchedule(thirdAcSeats: 4, thirdAcFare: 1000m);

        var booking = await harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 3), "idem-create");

        Assert.StartsWith("RTS-", booking.BookingReference);
        Assert.Equal("Pending", booking.Status);
        Assert.Equal(3, booking.SeatCount);
        Assert.Equal(3000m, booking.TotalFare);
        Assert.Equal(3, booking.Passengers.Count);
        Assert.All(booking.Passengers, p => Assert.StartsWith("3A-", p.SeatNumber));
        Assert.Equal(1, harness.AvailableSeats(scheduleId, TravelClass.ThirdAc));
        Assert.NotNull(booking.HoldExpiresUtc);
    }

    [Fact]
    public async Task Create_is_idempotent_for_a_repeated_idempotency_key()
    {
        using var harness = new TestHarness();
        var scheduleId = harness.SeedSchedule(thirdAcSeats: 4);

        var first = await harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 2), "idem-repeat");
        var second = await harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 2), "idem-repeat");

        Assert.Equal(first.BookingReference, second.BookingReference);
        Assert.Equal(2, harness.AvailableSeats(scheduleId, TravelClass.ThirdAc));
    }

    [Fact]
    public async Task Create_rejects_more_seats_than_remain_available()
    {
        using var harness = new TestHarness();
        var scheduleId = harness.SeedSchedule(thirdAcSeats: 2);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 3), "idem-oversell"));

        Assert.Equal("INSUFFICIENT_SEATS", ex.Code);
        Assert.Equal(2, harness.AvailableSeats(scheduleId, TravelClass.ThirdAc));
    }

    [Fact]
    public async Task Create_rejects_more_than_six_passengers()
    {
        using var harness = new TestHarness();
        var scheduleId = harness.SeedSchedule(thirdAcSeats: 20);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 7), "idem-max"));

        Assert.Equal("TOO_MANY_PASSENGERS", ex.Code);
    }

    [Fact]
    public async Task Create_rejects_a_departed_train()
    {
        using var harness = new TestHarness();
        var scheduleId = harness.SeedSchedule(departureUtc: DateTime.UtcNow.AddHours(-1));

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 1), "idem-departed"));

        Assert.Equal("DEPARTED", ex.Code);
    }

    [Fact]
    public async Task List_returns_bookings_for_the_owning_passenger_only()
    {
        using var harness = new TestHarness();
        var scheduleId = harness.SeedSchedule(thirdAcSeats: 6);

        await harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 1, "user-a"), "idem-a");
        await harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 1, "user-b"), "idem-b");

        var forUserA = await harness.Bookings.ListAsync("user-a");

        Assert.Single(forUserA);
    }

    [Fact]
    public async Task Expired_holds_release_inventory()
    {
        using var harness = new TestHarness();
        var scheduleId = harness.SeedSchedule(thirdAcSeats: 4);
        var booking = await harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 2), "idem-expire");

        var entity = harness.Db.Bookings.Single(b => b.BookingReference == booking.BookingReference);
        entity.HoldExpiresUtc = DateTime.UtcNow.AddMinutes(-1);
        harness.Db.SaveChanges();

        var expired = await harness.Bookings.ExpireHeldBookingsAsync();

        Assert.Equal(1, expired);
        Assert.Equal(4, harness.AvailableSeats(scheduleId, TravelClass.ThirdAc));
        Assert.Equal("Expired", (await harness.Bookings.GetAsync(booking.BookingReference)).Status);
    }

    [Fact]
    public async Task Confirm_without_a_settled_payment_is_rejected()
    {
        using var harness = new TestHarness();
        var scheduleId = harness.SeedSchedule();
        var booking = await harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 1), "idem-confirm");

        var ex = await Assert.ThrowsAsync<DomainException>(() => harness.Bookings.ConfirmAsync(booking.BookingReference));

        Assert.Equal("PAYMENT_NOT_SETTLED", ex.Code);
    }
}
