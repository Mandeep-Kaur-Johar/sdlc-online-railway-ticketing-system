namespace RailwayTicketing.Api.Domain;

public class Station
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public class Train
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SourceStationCode { get; set; } = string.Empty;
    public string DestinationStationCode { get; set; } = string.Empty;
    public ICollection<TrainSchedule> Schedules { get; set; } = new List<TrainSchedule>();
}

public class TrainSchedule
{
    public int Id { get; set; }
    public int TrainId { get; set; }
    public Train? Train { get; set; }
    public DateTime DepartureUtc { get; set; }
    public DateTime ArrivalUtc { get; set; }
    public string Status { get; set; } = "Scheduled";
    public ICollection<SeatInventory> SeatInventories { get; set; } = new List<SeatInventory>();
}

public class SeatInventory
{
    public int Id { get; set; }
    public int TrainScheduleId { get; set; }
    public TrainSchedule? TrainSchedule { get; set; }
    public TravelClass TravelClass { get; set; }
    public int TotalSeats { get; set; }
    public int AvailableSeats { get; set; }
    public decimal Fare { get; set; }
}

public class PassengerProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string IdDocumentMasked { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public class Booking
{
    public int Id { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public int TrainScheduleId { get; set; }
    public TrainSchedule? TrainSchedule { get; set; }
    public TravelClass TravelClass { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public decimal TotalFare { get; set; }
    public int SeatCount { get; set; }
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime HoldExpiresUtc { get; set; }
    public DateTime? ConfirmedUtc { get; set; }
    public DateTime? CancelledUtc { get; set; }
    public ICollection<BookingPassenger> Passengers { get; set; } = new List<BookingPassenger>();
    public Ticket? Ticket { get; set; }
    public Payment? Payment { get; set; }
    public Refund? Refund { get; set; }
}

public class BookingPassenger
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking? Booking { get; set; }
    public int? PassengerProfileId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string SeatNumber { get; set; } = string.Empty;
}

public class Ticket
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking? Booking { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public DateTime IssuedUtc { get; set; } = DateTime.UtcNow;
}

public class Payment
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking? Booking { get; set; }
    public string ProviderIntentId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Method { get; set; } = "Card";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? FailureReason { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SettledUtc { get; set; }
}

public class Refund
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking? Booking { get; set; }
    public decimal Amount { get; set; }
    public int Percentage { get; set; }
    public string Status { get; set; } = "Processed";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class NotificationRecord
{
    public int Id { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public NotificationChannel Channel { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool Delivered { get; set; }
    public DateTime SentUtc { get; set; } = DateTime.UtcNow;
}

public class SupportRequest
{
    public int Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? BookingReference { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SupportStatus Status { get; set; } = SupportStatus.Open;
    public string? Resolution { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public ICollection<SupportUpdate> Updates { get; set; } = new List<SupportUpdate>();
}

public class SupportUpdate
{
    public int Id { get; set; }
    public int SupportRequestId { get; set; }
    public SupportRequest? SupportRequest { get; set; }
    public SupportStatus Status { get; set; }
    public string Note { get; set; } = string.Empty;
    public string Author { get; set; } = "SupportAgent";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
