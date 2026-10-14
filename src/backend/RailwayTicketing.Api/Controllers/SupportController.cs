using Microsoft.AspNetCore.Mvc;
using RailwayTicketing.Api.Contracts;
using RailwayTicketing.Api.Services;

namespace RailwayTicketing.Api.Controllers;

/// <summary>User Story 3010 - raise and track support requests.</summary>
[ApiController]
[Route("api/v1/support-requests")]
public class SupportController : ControllerBase
{
    private readonly ISupportService _support;

    public SupportController(ISupportService support)
    {
        _support = support;
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateSupportRequest request, CancellationToken ct)
    {
        var created = await _support.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAsync), new { reference = created.Reference }, created);
    }

    [HttpGet("{reference}")]
    public async Task<IActionResult> GetAsync([FromRoute] string reference, CancellationToken ct)
        => Ok(await _support.GetAsync(reference, ct));

    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] string userId, CancellationToken ct)
        => Ok(await _support.ListAsync(userId, ct));

    [HttpPatch("{reference}")]
    public async Task<IActionResult> UpdateAsync([FromRoute] string reference, [FromBody] SupportUpdateRequest request, CancellationToken ct)
        => Ok(await _support.UpdateAsync(reference, request, ct));
}
