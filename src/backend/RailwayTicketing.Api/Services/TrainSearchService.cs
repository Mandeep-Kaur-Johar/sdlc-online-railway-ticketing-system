using Microsoft.EntityFrameworkCore;
using RailwayTicketing.Api.Contracts;
using RailwayTicketing.Api.Data;
using RailwayTicketing.Api.Domain;

namespace RailwayTicketing.Api.Services;

/// <summary>
/// Implements User Story 3000 (search trains between stations) and
/// User Story 3001 (seat availability by travel class).
/// </summary>
public class TrainSearchService : ITrainSearchService
{
    private readonly RailwayDbContext _db;

    public TrainSearchService(RailwayDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TrainSearchResultDto>> SearchAsync(
        string source,
        string destination,
        DateTime travelDate,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination))
        {
            throw new DomainException("INVALID_SEARCH", "Source and destination stations are required.");
        }

        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("INVALID_SEARCH", "Source and destination stations must be different.");
        }

        var src = source.Trim().ToUpperInvariant();
        var dst = destination.Trim().ToUpperInvariant();
        var dayStart = travelDate.Date;
        var dayEnd = dayStart.AddDays(1);

        var schedules = await _db.TrainSchedules
            .Include(s => s.Train)
            .Include(s => s.SeatInventories)
            .Where(s => s.Train != null
                        && s.Train.SourceStationCode == src
                        && s.Train.DestinationStationCode == dst
                        && s.DepartureUtc >= dayStart
                        && s.DepartureUtc < dayEnd)
            .OrderBy(s => s.DepartureUtc)
            .ToListAsync(ct);

        return schedules.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<SeatAvailabilityDto>> GetAvailabilityAsync(int scheduleId, CancellationToken ct = default)
    {
        var schedule = await _db.TrainSchedules
            .Include(s => s.SeatInventories)
            .FirstOrDefaultAsync(s => s.Id == scheduleId, ct)
            ?? throw new DomainException("SCHEDULE_NOT_FOUND", $"Train schedule {scheduleId} was not found.", 404);

        return schedule.SeatInventories
            .OrderBy(i => i.TravelClass)
            .Select(MapInventory)
            .ToList();
    }

    private static TrainSearchResultDto Map(TrainSchedule schedule)
    {
        return new TrainSearchResultDto(
            schedule.Id,
            schedule.Train?.Number ?? string.Empty,
            schedule.Train?.Name ?? string.Empty,
            schedule.Train?.SourceStationCode ?? string.Empty,
            schedule.Train?.DestinationStationCode ?? string.Empty,
            schedule.DepartureUtc,
            schedule.ArrivalUtc,
            schedule.Status,
            schedule.SeatInventories.OrderBy(i => i.TravelClass).Select(MapInventory).ToList());
    }

    private static SeatAvailabilityDto MapInventory(SeatInventory inventory)
    {
        var status = inventory.AvailableSeats switch
        {
            0 => "Waitlist",
            <= 10 => "Filling Fast",
            _ => "Available"
        };

        return new SeatAvailabilityDto(
            inventory.TravelClass.ToString(),
            inventory.TotalSeats,
            inventory.AvailableSeats,
            inventory.Fare,
            status);
    }
}
