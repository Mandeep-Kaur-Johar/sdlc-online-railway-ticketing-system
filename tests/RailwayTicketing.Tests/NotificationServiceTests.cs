using System.Text.Json;
using RailwayTicketing.Api.Contracts;
using RailwayTicketing.Api.Services;
using Xunit;

namespace RailwayTicketing.Tests;

/// <summary>User Story 3009 - notifications for booking and cancellation events.</summary>
public class NotificationServiceTests
{
    private const string Secret = "unit-test-secret";

    [Fact]
    public async Task Confirmation_notifications_are_sent_on_email_and_sms()
    {
        using var harness = new TestHarness(Secret);
        var scheduleId = harness.SeedSchedule();
        var booking = await harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 1), "idem-notify");
        var intent = await harness.Payments.CreateIntentAsync(new CreatePaymentIntentRequest(booking.BookingReference, "Card"));

        var body = JsonSerializer.Serialize(
            new PaymentWebhookPayload(intent.ProviderIntentId, "succeeded", null),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await harness.Payments.HandleWebhookAsync(PaymentSignature.Compute(body, Secret), body);

        var notifications = await harness.Notifications.ListAsync(booking.BookingReference);

        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, n => n.Channel == "Email" && n.Type == "BookingConfirmed");
        Assert.Contains(notifications, n => n.Channel == "Sms" && n.Type == "BookingConfirmed");
        Assert.All(notifications, n => Assert.True(n.Delivered));
    }

    [Fact]
    public async Task Cancellation_notifications_are_recorded()
    {
        using var harness = new TestHarness(Secret);
        var scheduleId = harness.SeedSchedule();
        var booking = await harness.Bookings.CreateAsync(TestData.BookingFor(scheduleId, 1), "idem-notify-cancel");

        await harness.Bookings.CancelAsync(booking.BookingReference, new CancelBookingRequest("user-001", "Change of plan"));

        var notifications = await harness.Notifications.ListAsync(booking.BookingReference);

        Assert.Contains(notifications, n => n.Type == "BookingCancelled");
    }
}
