using Microsoft.AspNetCore.Mvc;
using RailwayTicketing.Api.Contracts;
using RailwayTicketing.Api.Services;

namespace RailwayTicketing.Api.Controllers;

/// <summary>User Story 3006 - online payment for a held booking.</summary>
[ApiController]
[Route("api/v1/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _payments;

    public PaymentsController(IPaymentService payments)
    {
        _payments = payments;
    }

    [HttpPost("intents")]
    [ProducesResponseType(typeof(PaymentIntentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateIntentAsync([FromBody] CreatePaymentIntentRequest request, CancellationToken ct)
        => Ok(await _payments.CreateIntentAsync(request, ct));

    /// <summary>Signed provider callback. Unverified callbacks are rejected with HTTP 401.</summary>
    [HttpPost("webhook")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> WebhookAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["X-Payment-Signature"].ToString();
        var booking = await _payments.HandleWebhookAsync(signature, rawBody, ct);
        return Ok(booking);
    }
}
