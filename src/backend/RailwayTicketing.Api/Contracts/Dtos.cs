using RailwayTicketing.Api.Domain;

namespace RailwayTicketing.Api.Contracts;

public record TrainSearchRequest(string Source, string Destination, DateTime TravelDate);

public record SeatAvailabilityDto(string TravelClass, int TotalSeats, int AvailableSeats, decimal Fare, string Status);

public record TrainSearchResultDto(
    int ScheduleId,
    string TrainNumber,
    string TrainName,
    string Source,
    string Destination,
    DateTime DepartureUtc,
    DateTime ArrivalUtc,
    string Status,
    IReadOnlyList<SeatAvailabilityDto> Availability);

public record PassengerProfileRequest(string UserId, string FullName, int Age, string Gender, string Email, string Phone, string IdDocument);

public record PassengerProfileDto(int Id, string UserId, string FullName, int Age, string Gender, string Email, string Phone, string IdDocumentMasked, bool IsActive);

public record BookingPassengerRequest(string FullName, int Age, string Gender, int? PassengerProfileId);

public record CreateBookingRequest(
    string UserId,
    int ScheduleId,
    TravelClass TravelClass,
    string ContactEmail,
    string ContactPhone,
    List<BookingPassengerRequest> Passengers);

public record BookingPassengerDto(int Id, string FullName, int Age, string Gender, string SeatNumber);

public record BookingDto(
    string BookingReference,
    string Status,
    string TrainNumber,
    string TrainName,
    DateTime DepartureUtc,
    string TravelClass,
    int SeatCount,
    decimal TotalFare,
    DateTime? HoldExpiresUtc,
    string? TicketNumber,
    string PaymentStatus,
    decimal? RefundAmount,
    int? RefundPercentage,
    IReadOnlyList<BookingPassengerDto> Passengers);

public record CreatePaymentIntentRequest(string BookingReference, string Method);

public record PaymentIntentDto(string BookingReference, string ProviderIntentId, decimal Amount, string Status);

public record PaymentWebhookPayload(string ProviderIntentId, string Outcome, string? FailureReason);

public record CancelBookingRequest(string UserId, string? Reason);

public record RefundQuoteDto(string BookingReference, int Percentage, decimal Amount, DateTime DepartureUtc);

public record NotificationDto(int Id, string BookingReference, string Type, string Channel, string Recipient, string Subject, bool Delivered, DateTime SentUtc);

public record CreateSupportRequest(string UserId, string? BookingReference, string Subject, string Description);

public record SupportUpdateRequest(SupportStatus Status, string Note, string? Resolution);

public record SupportUpdateDto(string Status, string Note, string Author, DateTime CreatedUtc);

public record SupportRequestDto(
    string Reference,
    string UserId,
    string? BookingReference,
    string Subject,
    string Description,
    string Status,
    string? Resolution,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    IReadOnlyList<SupportUpdateDto> Updates);

public record ApiError(string Code, string Message);
