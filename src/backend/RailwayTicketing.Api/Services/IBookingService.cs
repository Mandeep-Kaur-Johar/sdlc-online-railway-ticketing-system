using RailwayTicketing.Api.Contracts;

namespace RailwayTicketing.Api.Services;

public interface IBookingService
{
    Task<BookingDto> CreateAsync(CreateBookingRequest request, string? idempotencyKey, CancellationToken ct = default);

    Task<BookingDto> GetAsync(string bookingReference, CancellationToken ct = default);

    Task<IReadOnlyList<BookingDto>> ListAsync(string userId, CancellationToken ct = default);

    Task<BookingDto> ConfirmAsync(string bookingReference, CancellationToken ct = default);

    Task<BookingDto> MarkFailedAsync(string bookingReference, string reason, CancellationToken ct = default);

    Task<BookingDto> CancelAsync(string bookingReference, CancelBookingRequest request, CancellationToken ct = default);

    Task<RefundQuoteDto> QuoteRefundAsync(string bookingReference, CancellationToken ct = default);

    Task<int> ExpireHeldBookingsAsync(CancellationToken ct = default);
}
