namespace RailwayTicketing.Api.Options;

public class BookingOptions
{
    public int SeatHoldMinutes { get; set; } = 15;
    public int MaxPassengersPerBooking { get; set; } = 6;
    public int SearchCacheTtlSeconds { get; set; } = 60;
}

public class RefundBand
{
    public int MinHoursBeforeDeparture { get; set; }
    public int RefundPercentage { get; set; }
}

public class RefundPolicyOptions
{
    public List<RefundBand> Bands { get; set; } = new()
    {
        new RefundBand { MinHoursBeforeDeparture = 48, RefundPercentage = 100 },
        new RefundBand { MinHoursBeforeDeparture = 24, RefundPercentage = 75 },
        new RefundBand { MinHoursBeforeDeparture = 12, RefundPercentage = 50 },
        new RefundBand { MinHoursBeforeDeparture = 4, RefundPercentage = 25 }
    };
}

public class PaymentOptions
{
    public string ProviderBaseUrl { get; set; } = "https://psp.example.invalid";
    public string WebhookSecret { get; set; } = "local-development-only-secret";
}
