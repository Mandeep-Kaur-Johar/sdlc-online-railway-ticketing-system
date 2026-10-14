using RailwayTicketing.Api.Domain;
using RailwayTicketing.Api.Services;
using Xunit;

namespace RailwayTicketing.Tests;

/// <summary>User Story 3000 and User Story 3001.</summary>
public class TrainSearchServiceTests
{
    [Fact]
    public async Task Search_returns_matching_trains_with_schedule_and_fares()
    {
        using var harness = new TestHarness();
        var departure = DateTime.UtcNow.Date.AddDays(5).AddHours(9);
        harness.SeedSchedule(departureUtc: departure);

        var results = await harness.Search.SearchAsync("ndls", "bct", departure.Date);

        var train = Assert.Single(results);
        Assert.Equal("NDLS", train.Source);
        Assert.Equal("BCT", train.Destination);
        Assert.Equal(departure, train.DepartureUtc);
        Assert.Contains(train.Availability, a => a.TravelClass == nameof(TravelClass.ThirdAc) && a.Fare == 1000m);
    }

    [Fact]
    public async Task Search_on_a_different_date_returns_no_results()
    {
        using var harness = new TestHarness();
        var departure = DateTime.UtcNow.Date.AddDays(5).AddHours(9);
        harness.SeedSchedule(departureUtc: departure);

        var results = await harness.Search.SearchAsync("NDLS", "BCT", departure.Date.AddDays(1));

        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_rejects_identical_source_and_destination()
    {
        using var harness = new TestHarness();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => harness.Search.SearchAsync("NDLS", "NDLS", DateTime.UtcNow.Date));

        Assert.Equal("INVALID_SEARCH", ex.Code);
    }

    [Fact]
    public async Task Availability_is_reported_by_travel_class_and_reflects_current_inventory()
    {
        using var harness = new TestHarness();
        var scheduleId = harness.SeedSchedule(thirdAcSeats: 4);

        var before = await harness.Search.GetAvailabilityAsync(scheduleId);
        Assert.Equal(2, before.Count);
        Assert.Equal(4, before.Single(a => a.TravelClass == nameof(TravelClass.ThirdAc)).AvailableSeats);

        await harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 2), "idem-availability");

        var after = await harness.Search.GetAvailabilityAsync(scheduleId);
        Assert.Equal(2, after.Single(a => a.TravelClass == nameof(TravelClass.ThirdAc)).AvailableSeats);
        Assert.Equal("Filling Fast", after.Single(a => a.TravelClass == nameof(TravelClass.ThirdAc)).Status);
    }

    [Fact]
    public async Task Availability_for_unknown_schedule_is_not_found()
    {
        using var harness = new TestHarness();

        var ex = await Assert.ThrowsAsync<DomainException>(() => harness.Search.GetAvailabilityAsync(987654));

        Assert.Equal(404, ex.StatusCode);
    }
}
