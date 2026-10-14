using System.Text.Json;
using RailwayTicketing.Api.Contracts;
using RailwayTicketing.Api.Domain;
using RailwayTicketing.Api.Options;
using RailwayTicketing.Api.Services;
using Xunit;

namespace RailwayTicketing.Tests;

/// <summary>User Story 3007 - refunds are calculated per policy and cancellation status is visible.</summary>
public class RefundAndCancellationTests
{
    private const string Secret = "unit-test-secret";

    [Theory]
    [InlineData(72, 100, 1000)]
    [InlineData(48, 100, 1000)]
    [InlineData(30, 75, 750)]
    [InlineData(18, 50, 500)]
    [InlineData(6, 25, 250)]
    [InlineData(1, 0, 0)]
    public void Refund_bands_are_applied_as_configured(int hoursBeforeDeparture, int expectedPercentage, decimal expectedAmount)
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var departure = now.AddHours(hoursBeforeDeparture);

        var (percentage, amount) = RefundCalculator.Calculate(1000m, departure, now, new RefundPolicyOptions());

        Assert.Equal(expectedPercentage, percentage);
        Assert.Equal(expectedAmount, amount);
    }

    [Fact]
    public async Task Cancelling_a_confirmed_booking_refunds_per_policy_and_releases_seats()
    {
        using var harness = new TestHarness(Secret);
        var scheduleId = harness.SeedSchedule(departureUtc: DateTime.UtcNow.AddDays(10), thirdAcSeats: 4, thirdAcFare: 1000m);
        var booking = await harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 2), "idem-cancel");

        var intent = await harness.Payments.CreateIntentAsync(new CreatePaymentIntentRequest(booking.BookingReference, "Card"));
        var body = JsonSerializer.Serialize(
            new PaymentWebhookPayload(intent.ProviderIntentId, "succeeded", null),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await harness.Payments.HandleWebhookAsync(PaymentSignature.Compute(body, Secret), body);

        var cancelled = await harness.Bookings.CancelAsync(booking.BookingReference, new CancelBookingRequest("user-001", "Plans changed"));

        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal(100, cancelled.RefundPercentage);
        Assert.Equal(2000m, cancelled.RefundAmount);
        Assert.Equal(4, harness.AvailableSeats(scheduleId, TravelClass.ThirdAc));
    }

    [Fact]
    public async Task Refund_quote_is_visible_before_cancellation()
    {
        using var harness = new TestHarness(Secret);
        var scheduleId = harness.SeedSchedule(departureUtc: DateTime.UtcNow.AddHours(30), thirdAcFare: 1000m);
        var booking = await harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 1), "idem-quote");

        var quote = await harness.Bookings.QuoteRefundAsync(booking.BookingReference);

        Assert.Equal(75, quote.Percentage);
        Assert.Equal(750m, quote.Amount);
    }

    [Fact]
    public async Task A_booking_cannot_be_cancelled_by_another_passenger()
    {
        using var harness = new TestHarness(Secret);
        var scheduleId = harness.SeedSchedule();
        var booking = await harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 1, "user-owner"), "idem-owner");

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => harness.Bookings.CancelAsync(booking.BookingReference, new CancelBookingRequest("user-other", null)));

        Assert.Equal(403, ex.StatusCode);
    }
}
