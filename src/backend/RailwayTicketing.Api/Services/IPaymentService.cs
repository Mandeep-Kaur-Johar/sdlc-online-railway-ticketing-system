using RailwayTicketing.Api.Contracts;

namespace RailwayTicketing.Api.Services;

public interface IPaymentService
{
    Task<PaymentIntentDto> CreateIntentAsync(CreatePaymentIntentRequest request, CancellationToken ct = default);

    Task<BookingDto> HandleWebhookAsync(string? signature, string rawBody, CancellationToken ct = default);

    bool VerifySignature(string? signature, string rawBody);
}
