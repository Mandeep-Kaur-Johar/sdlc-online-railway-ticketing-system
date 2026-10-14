using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RailwayTicketing.Api.Contracts;
using RailwayTicketing.Api.Data;
using RailwayTicketing.Api.Domain;
using RailwayTicketing.Api.Options;

namespace RailwayTicketing.Api.Services;

/// <summary>
/// Implements User Story 3003 (book railway tickets), User Story 3007
/// (cancellation and refunds) and the seat-hold orchestration described in
/// Section 4.5 of the System Design Document.
/// </summary>
public class BookingService : IBookingService
{
    private readonly RailwayDbContext _db;
    private readonly INotificationService _notifications;
    private readonly BookingOptions _bookingOptions;
    private readonly RefundPolicyOptions _refundPolicy;

    public BookingService(
        RailwayDbContext db,
        INotificationService notifications,
        IOptions<BookingOptions> bookingOptions,
        IOptions<RefundPolicyOptions> refundPolicy)
    {
        _db = db;
        _notifications = notifications;
        _bookingOptions = bookingOptions.Value;
        _refundPolicy = refundPolicy.Value;
    }

    public async Task<BookingDto> CreateAsync(CreateBookingRequest request, string? idempotencyKey, CancellationToken ct = default)
    {
        if (request is null || request.Passengers is null || request.Passengers.Count == 0)
        {
            throw new DomainException("INVALID_BOOKING", "At least one passenger is required.");
        }

        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            throw new DomainException("INVALID_BOOKING", "UserId is required.");
        }

        if (request.Passengers.Count > _bookingOptions.MaxPassengersPerBooking)
        {
            throw new DomainException(
                "TOO_MANY_PASSENGERS",
                $"A booking may contain at most {_bookingOptions.MaxPassengersPerBooking} passengers.");
        }

        if (request.Passengers.Any(p => string.IsNullOrWhiteSpace(p.FullName)))
        {
            throw new DomainException("INVALID_BOOKING", "Every passenger must have a full name.");
        }

        if (string.IsNullOrWhiteSpace(request.ContactEmail) || !request.ContactEmail.Contains('@'))
        {
            throw new DomainException("INVALID_BOOKING", "A valid contact email is required for notifications.");
        }

        var key = string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString("N") : idempotencyKey.Trim();

        var existing = await LoadBookingQuery().FirstOrDefaultAsync(b => b.IdempotencyKey == key, ct);
        if (existing is not null)
        {
            return Map(existing);
        }

        var schedule = await _db.TrainSchedules
            .Include(s => s.Train)
            .Include(s => s.SeatInventories)
            .FirstOrDefaultAsync(s => s.Id == request.ScheduleId, ct)
            ?? throw new DomainException("SCHEDULE_NOT_FOUND", $"Train schedule {request.ScheduleId} was not found.", 404);

        if (schedule.DepartureUtc <= DateTime.UtcNow)
        {
            throw new DomainException("DEPARTED", "The selected train has already departed.");
        }

        var inventory = schedule.SeatInventories.FirstOrDefault(i => i.TravelClass == request.TravelClass)
            ?? throw new DomainException("CLASS_NOT_AVAILABLE", "The requested travel class is not offered on this train.");

        var seatCount = request.Passengers.Count;
        if (inventory.AvailableSeats < seatCount)
        {
            throw new DomainException(
                "INSUFFICIENT_SEATS",
                $"Only {inventory.AvailableSeats} seat(s) remain in {inventory.TravelClass}.",
                409);
        }

        var firstSeatIndex = inventory.TotalSeats - inventory.AvailableSeats + 1;
        inventory.AvailableSeats -= seatCount;

        var booking = new Booking
        {
            BookingReference = GenerateBookingReference(),
            UserId = request.UserId.Trim(),
            TrainScheduleId = schedule.Id,
            TravelClass = request.TravelClass,
            Status = BookingStatus.Pending,
            SeatCount = seatCount,
            TotalFare = inventory.Fare * seatCount,
            ContactEmail = request.ContactEmail.Trim(),
            ContactPhone = request.ContactPhone?.Trim() ?? string.Empty,
            IdempotencyKey = key,
            CreatedUtc = DateTime.UtcNow,
            HoldExpiresUtc = DateTime.UtcNow.AddMinutes(_bookingOptions.SeatHoldMinutes)
        };

        var prefix = ClassPrefix(inventory.TravelClass);
        var offset = 0;

        foreach (var passenger in request.Passengers)
        {
            booking.Passengers.Add(new BookingPassenger
            {
                FullName = passenger.FullName.Trim(),
                Age = passenger.Age,
                Gender = passenger.Gender?.Trim() ?? string.Empty,
                PassengerProfileId = passenger.PassengerProfileId,
                SeatNumber = $"{prefix}-{firstSeatIndex + offset}"
            });

            offset++;
        }

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync(ct);

        var saved = await LoadBookingQuery().FirstAsync(b => b.Id == booking.Id, ct);
        return Map(saved);
    }

    public async Task<BookingDto> GetAsync(string bookingReference, CancellationToken ct = default)
    {
        var booking = await LoadBookingQuery().FirstOrDefaultAsync(b => b.BookingReference == bookingReference, ct)
            ?? throw new DomainException("BOOKING_NOT_FOUND", $"Booking {bookingReference} was not found.", 404);

        return Map(booking);
    }

    public async Task<IReadOnlyList<BookingDto>> ListAsync(string userId, CancellationToken ct = default)
    {
        var bookings = await LoadBookingQuery()
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedUtc)
            .ToListAsync(ct);

        return bookings.Select(Map).ToList();
    }

    public async Task<BookingDto> ConfirmAsync(string bookingReference, CancellationToken ct = default)
    {
        var booking = await LoadBookingQuery().FirstOrDefaultAsync(b => b.BookingReference == bookingReference, ct)
            ?? throw new DomainException("BOOKING_NOT_FOUND", $"Booking {bookingReference} was not found.", 404);

        if (booking.Status == BookingStatus.Confirmed)
        {
            return Map(booking);
        }

        if (booking.Status != BookingStatus.Pending)
        {
            throw new DomainException("INVALID_STATE", $"Booking {bookingReference} cannot be confirmed from state {booking.Status}.");
        }

        if (booking.Payment is null || booking.Payment.Status != PaymentStatus.Succeeded)
        {
            throw new DomainException("PAYMENT_NOT_SETTLED", "A booking can only be confirmed after a successful payment.");
        }

        booking.Status = BookingStatus.Confirmed;
        booking.ConfirmedUtc = DateTime.UtcNow;
        booking.Ticket = new Ticket
        {
            BookingId = booking.Id,
            TicketNumber = GenerateTicketNumber(),
            IssuedUtc = DateTime.UtcNow
        };

        await _db.SaveChangesAsync(ct);
        await _notifications.SendBookingNotificationAsync(booking, NotificationType.BookingConfirmed, ct);
        return Map(booking);
    }

    public async Task<BookingDto> MarkFailedAsync(string bookingReference, string reason, CancellationToken ct = default)
    {
        var booking = await LoadBookingQuery().FirstOrDefaultAsync(b => b.BookingReference == bookingReference, ct)
            ?? throw new DomainException("BOOKING_NOT_FOUND", $"Booking {bookingReference} was not found.", 404);

        if (booking.Status is BookingStatus.Confirmed or BookingStatus.Cancelled)
        {
            throw new DomainException("INVALID_STATE", $"Booking {bookingReference} cannot be failed from state {booking.Status}.");
        }

        booking.Status = BookingStatus.Failed;
        if (booking.Payment is not null)
        {
            booking.Payment.Status = PaymentStatus.Failed;
            booking.Payment.FailureReason = reason;
        }

        await ReleaseSeatsAsync(booking, ct);
        await _db.SaveChangesAsync(ct);
        return Map(booking);
    }

    public async Task<BookingDto> CancelAsync(string bookingReference, CancelBookingRequest request, CancellationToken ct = default)
    {
        var booking = await LoadBookingQuery().FirstOrDefaultAsync(b => b.BookingReference == bookingReference, ct)
            ?? throw new DomainException("BOOKING_NOT_FOUND", $"Booking {bookingReference} was not found.", 404);

        if (request is not null && !string.IsNullOrWhiteSpace(request.UserId)
            && !string.Equals(booking.UserId, request.UserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("FORBIDDEN", "A booking can only be cancelled by the passenger who created it.", 403);
        }

        if (booking.Status is BookingStatus.Cancelled)
        {
            return Map(booking);
        }

        if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.Pending))
        {
            throw new DomainException("INVALID_STATE", $"Booking {bookingReference} cannot be cancelled from state {booking.Status}.");
        }

        var departureUtc = booking.TrainSchedule?.DepartureUtc ?? DateTime.UtcNow;
        var paid = booking.Payment is { Status: PaymentStatus.Succeeded } ? booking.Payment.Amount : 0m;
        var (percentage, amount) = RefundCalculator.Calculate(paid, departureUtc, DateTime.UtcNow, _refundPolicy);

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledUtc = DateTime.UtcNow;
        booking.Refund = new Refund
        {
            BookingId = booking.Id,
            Amount = amount,
            Percentage = percentage,
            Status = amount > 0 ? "Processed" : "NotEligible",
            CreatedUtc = DateTime.UtcNow
        };

        if (booking.Payment is { Status: PaymentStatus.Succeeded } && amount > 0)
        {
            booking.Payment.Status = PaymentStatus.Refunded;
        }

        await ReleaseSeatsAsync(booking, ct);
        await _db.SaveChangesAsync(ct);
        await _notifications.SendBookingNotificationAsync(booking, NotificationType.BookingCancelled, ct);
        return Map(booking);
    }

    public async Task<RefundQuoteDto> QuoteRefundAsync(string bookingReference, CancellationToken ct = default)
    {
        var booking = await LoadBookingQuery().FirstOrDefaultAsync(b => b.BookingReference == bookingReference, ct)
            ?? throw new DomainException("BOOKING_NOT_FOUND", $"Booking {bookingReference} was not found.", 404);

        var departureUtc = booking.TrainSchedule?.DepartureUtc ?? DateTime.UtcNow;
        var paid = booking.Payment is { Status: PaymentStatus.Succeeded } ? booking.Payment.Amount : booking.TotalFare;
        var (percentage, amount) = RefundCalculator.Calculate(paid, departureUtc, DateTime.UtcNow, _refundPolicy);
        return new RefundQuoteDto(booking.BookingReference, percentage, amount, departureUtc);
    }

    public async Task<int> ExpireHeldBookingsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expired = await LoadBookingQuery()
            .Where(b => b.Status == BookingStatus.Pending && b.HoldExpiresUtc < now)
            .ToListAsync(ct);

        foreach (var booking in expired)
        {
            booking.Status = BookingStatus.Expired;
            await ReleaseSeatsAsync(booking, ct);
        }

        if (expired.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return expired.Count;
    }

    private async Task ReleaseSeatsAsync(Booking booking, CancellationToken ct)
    {
        var inventory = await _db.SeatInventories
            .FirstOrDefaultAsync(i => i.TrainScheduleId == booking.TrainScheduleId && i.TravelClass == booking.TravelClass, ct);

        if (inventory is null)
        {
            return;
        }

        inventory.AvailableSeats = Math.Min(inventory.TotalSeats, inventory.AvailableSeats + booking.SeatCount);
    }

    private IQueryable<Booking> LoadBookingQuery() => _db.Bookings
        .Include(b => b.Passengers)
        .Include(b => b.Ticket)
        .Include(b => b.Payment)
        .Include(b => b.Refund)
        .Include(b => b.TrainSchedule)
            .ThenInclude(s => s!.Train);

    private static string ClassPrefix(TravelClass travelClass) => travelClass switch
    {
        TravelClass.SecondSitting => "2S",
        TravelClass.SleeperClass => "SL",
        TravelClass.ThirdAc => "3A",
        TravelClass.SecondAc => "2A",
        _ => "1A"
    };

    private static string GenerateBookingReference() =>
        "RTS-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static string GenerateTicketNumber() =>
        "TKT-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    public static BookingDto Map(Booking b) => new(
        b.BookingReference,
        b.Status.ToString(),
        b.TrainSchedule?.Train?.Number ?? string.Empty,
        b.TrainSchedule?.Train?.Name ?? string.Empty,
        b.TrainSchedule?.DepartureUtc ?? default,
        b.TravelClass.ToString(),
        b.SeatCount,
        b.TotalFare,
        b.Status == BookingStatus.Pending ? b.HoldExpiresUtc : null,
        b.Ticket?.TicketNumber,
        b.Payment?.Status.ToString() ?? "None",
        b.Refund?.Amount,
        b.Refund?.Percentage,
        b.Passengers.Select(p => new BookingPassengerDto(p.Id, p.FullName, p.Age, p.Gender, p.SeatNumber)).ToList());
}
