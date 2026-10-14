using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RailwayTicketing.Api.Contracts;
using RailwayTicketing.Api.Data;
using RailwayTicketing.Api.Domain;
using RailwayTicketing.Api.Options;

namespace RailwayTicketing.Api.Services;

/// <summary>
/// Implements User Story 3006 - successful payments confirm bookings and failed
/// payments never create a confirmed ticket. Webhook callbacks are verified with
/// HMAC-SHA256 as required by Section 4.1 of the System Design Document.
/// </summary>
public class PaymentService : IPaymentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RailwayDbContext _db;
    private readonly IBookingService _bookings;
    private readonly PaymentOptions _options;

    public PaymentService(RailwayDbContext db, IBookingService bookings, IOptions<PaymentOptions> options)
    {
        _db = db;
        _bookings = bookings;
        _options = options.Value;
    }

    public async Task<PaymentIntentDto> CreateIntentAsync(CreatePaymentIntentRequest request, CancellationToken ct = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.BookingReference))
        {
            throw new DomainException("INVALID_PAYMENT", "BookingReference is required.");
        }

        var booking = await _db.Bookings
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.BookingReference == request.BookingReference, ct)
            ?? throw new DomainException("BOOKING_NOT_FOUND", $"Booking {request.BookingReference} was not found.", 404);

        if (booking.Status != BookingStatus.Pending)
        {
            throw new DomainException("INVALID_STATE", $"Payment cannot be started for a booking in state {booking.Status}.");
        }

        if (booking.HoldExpiresUtc < DateTime.UtcNow)
        {
            throw new DomainException("HOLD_EXPIRED", "The 15 minute seat hold has expired. Please search and book again.", 409);
        }

        if (booking.Payment is not null && booking.Payment.Status == PaymentStatus.Pending)
        {
            return new PaymentIntentDto(booking.BookingReference, booking.Payment.ProviderIntentId, booking.Payment.Amount, booking.Payment.Status.ToString());
        }

        var payment = new Payment
        {
            BookingId = booking.Id,
            Amount = booking.TotalFare,
            Method = string.IsNullOrWhiteSpace(request.Method) ? "Card" : request.Method.Trim(),
            ProviderIntentId = "pi_" + Guid.NewGuid().ToString("N")[..16],
            Status = PaymentStatus.Pending,
            CreatedUtc = DateTime.UtcNow
        };

        booking.Payment = payment;
        await _db.SaveChangesAsync(ct);

        return new PaymentIntentDto(booking.BookingReference, payment.ProviderIntentId, payment.Amount, payment.Status.ToString());
    }

    public async Task<BookingDto> HandleWebhookAsync(string? signature, string rawBody, CancellationToken ct = default)
    {
        if (!VerifySignature(signature, rawBody))
        {
            throw new DomainException("INVALID_SIGNATURE", "The payment webhook signature could not be verified.", 401);
        }

        PaymentWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<PaymentWebhookPayload>(rawBody, JsonOptions);
        }
        catch (JsonException)
        {
            throw new DomainException("INVALID_PAYLOAD", "The payment webhook payload is not valid JSON.");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.ProviderIntentId))
        {
            throw new DomainException("INVALID_PAYLOAD", "ProviderIntentId is required in the webhook payload.");
        }

        var payment = await _db.Payments
            .Include(p => p.Booking)
            .FirstOrDefaultAsync(p => p.ProviderIntentId == payload.ProviderIntentId, ct)
            ?? throw new DomainException("PAYMENT_NOT_FOUND", $"Payment intent {payload.ProviderIntentId} was not found.", 404);

        var bookingReference = payment.Booking?.BookingReference
            ?? throw new DomainException("BOOKING_NOT_FOUND", "The payment is not linked to a booking.", 404);

        if (payment.Status != PaymentStatus.Pending)
        {
            return await _bookings.GetAsync(bookingReference, ct);
        }

        if (string.Equals(payload.Outcome, "succeeded", StringComparison.OrdinalIgnoreCase))
        {
            payment.Status = PaymentStatus.Succeeded;
            payment.SettledUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return await _bookings.ConfirmAsync(bookingReference, ct);
        }

        payment.Status = PaymentStatus.Failed;
        payment.FailureReason = payload.FailureReason ?? "Payment declined by provider.";
        await _db.SaveChangesAsync(ct);
        return await _bookings.MarkFailedAsync(bookingReference, payment.FailureReason, ct);
    }

    public bool VerifySignature(string? signature, string rawBody)
    {
        if (string.IsNullOrWhiteSpace(signature) || rawBody is null)
        {
            return false;
        }

        var expected = ComputeSignature(rawBody, _options.WebhookSecret);
        var provided = signature.Trim();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(provided.Length == expected.Length ? provided : expected.Length > 0 ? new string('0', expected.Length) : string.Empty));
    }

    public static string ComputeSignature(string rawBody, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
