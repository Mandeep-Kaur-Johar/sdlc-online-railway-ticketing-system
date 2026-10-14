using RailwayTicketing.Api.Contracts;
using RailwayTicketing.Api.Domain;

namespace RailwayTicketing.Api.Services;

public interface INotificationService
{
    Task SendBookingNotificationAsync(Booking booking, NotificationType type, CancellationToken ct = default);

    Task<IReadOnlyList<NotificationDto>> ListAsync(string? bookingReference, CancellationToken ct = default);
}
