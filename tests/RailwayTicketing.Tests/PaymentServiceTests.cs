using System.Text.Json;
using RailwayTicketing.Api.Contracts;
using RailwayTicketing.Api.Domain;
using RailwayTicketing.Api.Services;
using Xunit;

namespace RailwayTicketing.Tests;

/// <summary>User Story 3006 - successful payments confirm bookings, failed payments never issue a ticket.</summary>
public class PaymentServiceTests
{
    private const string Secret = "unit-test-secret";

    [Fact]
    public async Task Successful_settlement_confirms_the_booking_and_issues_a_ticket()
    {
        using var harness = new TestHarness(Secret);
        var scheduleId = harness.SeedSchedule(thirdAcSeats: 4);
        var booking = await harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 2), "idem-pay-ok");

        var intent = await harness.Payments.CreateIntentAsync(new CreatePaymentIntentRequest(booking.BookingReference, "Card"));
        var confirmed = await SendWebhookAsync(harness, intent.ProviderIntentId, "succeeded");

        Assert.Equal("Confirmed", confirmed.Status);
        Assert.False(string.IsNullOrWhiteSpace(confirmed.TicketNumber));
        Assert.Equal("Succeeded", confirmed.PaymentStatus);
        Assert.Equal(2, harness.AvailableSeats(scheduleId, TravelClass.ThirdAc));
    }

    [Fact]
    public async Task Declined_settlement_never_confirms_a_ticket_and_returns_the_seats()
    {
        using var harness = new TestHarness(Secret);
        var scheduleId = harness.SeedSchedule(thirdAcSeats: 4);
        var booking = await harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 2), "idem-pay-fail");

        var intent = await harness.Payments.CreateIntentAsync(new CreatePaymentIntentRequest(booking.BookingReference, "Card"));
        var failed = await SendWebhookAsync(harness, intent.ProviderIntentId, "failed", "Insufficient funds");

        Assert.Equal("Failed", failed.Status);
        Assert.Null(failed.TicketNumber);
        Assert.Equal("Failed", failed.PaymentStatus);
        Assert.Equal(4, harness.AvailableSeats(scheduleId, TravelClass.ThirdAc));
    }

    [Fact]
    public async Task Webhook_with_an_invalid_signature_is_rejected_with_401()
    {
        using var harness = new TestHarness(Secret);
        var scheduleId = harness.SeedSchedule();
        var booking = await harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 1), "idem-pay-sig");
        var intent = await harness.Payments.CreateIntentAsync(new CreatePaymentIntentRequest(booking.BookingReference, "Card"));

        var body = JsonSerializer.Serialize(new PaymentWebhookPayload(intent.ProviderIntentId, "succeeded", null));

        var ex = await Assert.ThrowsAsync<DomainException>(() => harness.Payments.HandleWebhookAsync("deadbeef", body));

        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("INVALID_SIGNATURE", ex.Code);
        Assert.Equal("Pending", (await harness.Bookings.GetAsync(booking.BookingReference)).Status);
    }

    [Fact]
    public async Task Duplicate_settlement_webhook_does_not_double_confirm()
    {
        using var harness = new TestHarness(Secret);
        var scheduleId = harness.SeedSchedule(thirdAcSeats: 4);
        var booking = await harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 1), "idem-pay-dup");
        var intent = await harness.Payments.CreateIntentAsync(new CreatePaymentIntentRequest(booking.BookingReference, "Card"));

        var first = await SendWebhookAsync(harness, intent.ProviderIntentId, "succeeded");
        var second = await SendWebhookAsync(harness, intent.ProviderIntentId, "succeeded");

        Assert.Equal(first.TicketNumber, second.TicketNumber);
        Assert.Equal(1, harness.Db.Tickets.Count());
    }

    [Fact]
    public async Task Payment_intent_cannot_be_created_after_the_hold_expires()
    {
        using var harness = new TestHarness(Secret);
        var scheduleId = harness.SeedSchedule();
        var booking = await harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 1), "idem-pay-hold");

        var entity = harness.Db.Bookings.Single(b => b.BookingReference == booking.BookingReference);
        entity.HoldExpiresUtc = DateTime.UtcNow.AddMinutes(-1);
        harness.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => harness.Payments.CreateIntentAsync(new CreatePaymentIntentRequest(booking.BookingReference, "Card")));

        Assert.Equal("HOLD_EXPIRED", ex.Code);
    }

    private static Task<BookingDto> SendWebhookAsync(TestHarness harness, string intentId, string outcome, string? reason = null)
    {
        var body = JsonSerializer.Serialize(
            new PaymentWebhookPayload(intentId, outcome, reason),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var signature = PaymentSignature.Compute(body, Secret);
        return harness.Payments.HandleWebhookAsync(signature, body);
    }
}
