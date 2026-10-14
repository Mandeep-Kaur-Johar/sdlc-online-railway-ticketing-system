using Microsoft.AspNetCore.Mvc;
using RailwayTicketing.Api.Contracts;
using RailwayTicketing.Api.Services;

namespace RailwayTicketing.Api.Controllers;

[ApiController]
[Route("api/v1/trains")]
public class TrainsController : ControllerBase
{
    private readonly ITrainSearchService _search;

    public TrainsController(ITrainSearchService search)
    {
        _search = search;
    }

    /// <summary>User Story 3000 - search trains between stations for a travel date.</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<TrainSearchResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] string source,
        [FromQuery] string destination,
        [FromQuery] DateTime date,
        CancellationToken ct)
    {
        var results = await _search.SearchAsync(source, destination, date, ct);
        return Ok(results);
    }

    /// <summary>User Story 3001 - seat availability by travel class for a schedule.</summary>
    [HttpGet("schedules/{scheduleId:int}/availability")]
    [ProducesResponseType(typeof(IReadOnlyList<SeatAvailabilityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AvailabilityAsync([FromRoute] int scheduleId, CancellationToken ct)
    {
        var availability = await _search.GetAvailabilityAsync(scheduleId, ct);
        return Ok(availability);
    }
}
