using RailwayTicketing.Api.Contracts;

namespace RailwayTicketing.Api.Services;

public interface ITrainSearchService
{
    Task<IReadOnlyList<TrainSearchResultDto>> SearchAsync(string source, string destination, DateTime travelDate, CancellationToken ct = default);

    Task<IReadOnlyList<SeatAvailabilityDto>> GetAvailabilityAsync(int scheduleId, CancellationToken ct = default);
}
