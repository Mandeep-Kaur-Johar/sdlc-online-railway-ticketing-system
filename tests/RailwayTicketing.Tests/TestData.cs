using RailwayTicketing.Api.Contracts;
using RailwayTicketing.Api.Domain;

namespace RailwayTicketing.Tests;

public static class TestData
{
    public static CreateBookingRequest BookingFor(
        int scheduleId,
        int passengerCount,
        string userId = "user-001",
        TravelClass travelClass = TravelClass.ThirdAc)
    {
        var passengers = Enumerable.Range(1, passengerCount)
            .Select(i => new BookingPassengerRequest($"Passenger {i}", 25 + i, i % 2 == 0 ? "Female" : "Male", null))
            .ToList();

        return new CreateBookingRequest(
            userId,
            scheduleId,
            travelClass,
            "passenger@example.com",
            "+440000000000",
            passengers);
    }
}
