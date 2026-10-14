using Microsoft.AspNetCore.Mvc;
using RailwayTicketing.Api.Services;

namespace RailwayTicketing.Api.Controllers;

/// <summary>User Story 3009 - booking notification history and delivery receipts.</summary>
[ApiController]
[Route("api/v1/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] string? bookingReference, CancellationToken ct)
        => Ok(await _notifications.ListAsync(bookingReference, ct));
}
