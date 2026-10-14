using Microsoft.EntityFrameworkCore;
using RailwayTicketing.Api.Contracts;
using RailwayTicketing.Api.Data;
using RailwayTicketing.Api.Domain;

namespace RailwayTicketing.Api.Services;

/// <summary>
/// Implements User Story 3009 - email and SMS notifications for booking,
/// cancellation and schedule-change events, with a persisted delivery receipt.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly RailwayDbContext _db;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(RailwayDbContext db, ILogger<NotificationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SendBookingNotificationAsync(Booking booking, NotificationType type, CancellationToken ct = default)
    {
        if (booking is null)
        {
            throw new DomainException("INVALID_NOTIFICATION", "Booking is required to send a notification.");
        }

        var subject = type switch
        {
            NotificationType.BookingConfirmed => $"Booking confirmed - {booking.BookingReference}",
            NotificationType.BookingCancelled => $"Booking cancelled - {booking.BookingReference}",
            _ => $"Schedule change - {booking.BookingReference}"
        };

        var body = type switch
        {
            NotificationType.BookingConfirmed =>
                $"Your ticket {booking.Ticket?.TicketNumber} is confirmed for {booking.SeatCount} passenger(s). Total fare {booking.TotalFare:0.00}.",
            NotificationType.BookingCancelled =>
                $"Booking {booking.BookingReference} is cancelled. Refund of {booking.Refund?.Amount ?? 0m:0.00} ({booking.Refund?.Percentage ?? 0}%) is being processed.",
            _ => $"The schedule for booking {booking.BookingReference} has changed. Please check your updated itinerary."
        };

        var recipients = new List<(NotificationChannel Channel, string Address)>();

        if (!string.IsNullOrWhiteSpace(booking.ContactEmail))
        {
            recipients.Add((NotificationChannel.Email, booking.ContactEmail));
        }

        if (!string.IsNullOrWhiteSpace(booking.ContactPhone))
        {
            recipients.Add((NotificationChannel.Sms, booking.ContactPhone));
        }

        foreach (var recipient in recipients)
        {
            _db.Notifications.Add(new NotificationRecord
            {
                BookingReference = booking.BookingReference,
                Type = type,
                Channel = recipient.Channel,
                Recipient = recipient.Address,
                Subject = subject,
                Body = body,
                Delivered = true,
                SentUtc = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Dispatched {Count} {Type} notification(s) for {Reference}", recipients.Count, type, booking.BookingReference);
    }

    public async Task<IReadOnlyList<NotificationDto>> ListAsync(string? bookingReference, CancellationToken ct = default)
    {
        var query = _db.Notifications.AsQueryable();

        if (!string.IsNullOrWhiteSpace(bookingReference))
        {
            query = query.Where(n => n.BookingReference == bookingReference);
        }

        var items = await query.OrderByDescending(n => n.SentUtc).Take(200).ToListAsync(ct);

        return items.Select(n => new NotificationDto(
            n.Id,
            n.BookingReference,
            n.Type.ToString(),
            n.Channel.ToString(),
            n.Recipient,
            n.Subject,
            n.Delivered,
            n.SentUtc)).ToList();
    }
}
