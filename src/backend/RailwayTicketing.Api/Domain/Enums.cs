namespace RailwayTicketing.Api.Domain;

public enum TravelClass
{
    SecondSitting = 0,
    SleeperClass = 1,
    ThirdAc = 2,
    SecondAc = 3,
    FirstAc = 4
}

public enum BookingStatus
{
    Pending = 0,
    Confirmed = 1,
    Cancelled = 2,
    Failed = 3,
    Expired = 4
}

public enum PaymentStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Refunded = 3
}

public enum SupportStatus
{
    Open = 0,
    InProgress = 1,
    Resolved = 2,
    Closed = 3
}

public enum NotificationType
{
    BookingConfirmed = 0,
    BookingCancelled = 1,
    ScheduleChanged = 2
}

public enum NotificationChannel
{
    Email = 0,
    Sms = 1
}
