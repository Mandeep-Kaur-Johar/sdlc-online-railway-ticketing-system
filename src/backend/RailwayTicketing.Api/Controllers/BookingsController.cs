using Microsoft.AspNetCore.Mvc;
using RailwayTicketing.Api.Contracts;
using RailwayTicketing.Api.Services;

namespace RailwayTicketing.Api.Controllers;

/// <summary>User Stories 3003 (booking) and 3007 (cancellation and refunds).</summary>
[ApiController]
[Route("api/v1/bookings")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookings;

    public BookingsController(IBookingService bookings)
    {
        _bookings = bookings;
    }

    [HttpPost]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateBookingRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        var booking = await _bookings.CreateAsync(request, idempotencyKey, ct);
        return CreatedAtAction(nameof(GetAsync), new { bookingReference = booking.BookingReference }, booking);
    }

    [HttpGet("{bookingReference}")]
    public async Task<IActionResult> GetAsync([FromRoute] string bookingReference, CancellationToken ct)
        => Ok(await _bookings.GetAsync(bookingReference, ct));

    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] string userId, CancellationToken ct)
        => Ok(await _bookings.ListAsync(userId, ct));

    [HttpGet("{bookingReference}/refund-quote")]
    public async Task<IActionResult> RefundQuoteAsync([FromRoute] string bookingReference, CancellationToken ct)
        => Ok(await _bookings.QuoteRefundAsync(bookingReference, ct));

    [HttpPost("{bookingReference}/cancel")]
    public async Task<IActionResult> CancelAsync(
        [FromRoute] string bookingReference,
        [FromBody] CancelBookingRequest request,
        CancellationToken ct)
        => Ok(await _bookings.CancelAsync(bookingReference, request, ct));
}
