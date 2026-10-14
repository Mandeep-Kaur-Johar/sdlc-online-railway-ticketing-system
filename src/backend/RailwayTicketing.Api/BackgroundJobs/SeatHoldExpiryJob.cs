using RailwayTicketing.Api.Services;

namespace RailwayTicketing.Api.BackgroundJobs;

/// <summary>
/// Section 4.5 reconciliation job - expires seat holds older than the configured
/// hold window (default 15 minutes) and returns inventory.
/// </summary>
public class SeatHoldExpiryJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SeatHoldExpiryJob> _logger;

    public SeatHoldExpiryJob(IServiceScopeFactory scopeFactory, ILogger<SeatHoldExpiryJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var bookings = scope.ServiceProvider.GetRequiredService<IBookingService>();
                var expired = await bookings.ExpireHeldBookingsAsync(stoppingToken);

                if (expired > 0)
                {
                    _logger.LogInformation("Released seat holds for {Count} expired booking(s)", expired);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Seat hold expiry job failed");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}
