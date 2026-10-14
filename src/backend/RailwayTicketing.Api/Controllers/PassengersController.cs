using Microsoft.AspNetCore.Mvc;
using RailwayTicketing.Api.Contracts;
using RailwayTicketing.Api.Services;

namespace RailwayTicketing.Api.Controllers;

/// <summary>User Story 3004 - saved passenger profiles.</summary>
[ApiController]
[Route("api/v1/passengers")]
public class PassengersController : ControllerBase
{
    private readonly IPassengerProfileService _profiles;

    public PassengersController(IPassengerProfileService profiles)
    {
        _profiles = profiles;
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] string userId, CancellationToken ct)
        => Ok(await _profiles.ListAsync(userId, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAsync([FromRoute] int id, CancellationToken ct)
        => Ok(await _profiles.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] PassengerProfileRequest request, CancellationToken ct)
    {
        var created = await _profiles.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAsync), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] PassengerProfileRequest request, CancellationToken ct)
        => Ok(await _profiles.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeactivateAsync([FromRoute] int id, CancellationToken ct)
    {
        await _profiles.DeactivateAsync(id, ct);
        return NoContent();
    }
}
