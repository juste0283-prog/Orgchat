using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrgChat.Core.Contracts.Notifications;
using OrgChat.Server.Services;
using System.Security.Claims;

namespace OrgChat.Server.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly NotificationService _notificationService;

    public NotificationsController(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userIdClaim != null ? int.Parse(userIdClaim) : 0;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] int take = 100, [FromQuery] int skip = 0)
    {
        var notifications = await _notificationService.GetForUserAsync(GetCurrentUserId(), take, skip);
        return Ok(notifications);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteNotification(int id)
    {
        var deleted = await _notificationService.DeleteAsync(id, GetCurrentUserId());
        return deleted ? NoContent() : NotFound(new { error = "Notification introuvable." });
    }

    [HttpDelete]
    public async Task<IActionResult> ClearNotifications()
    {
        var count = await _notificationService.ClearAsync(GetCurrentUserId());
        return Ok(new { deletedCount = count });
    }

    [HttpPost("purge")]
    public async Task<IActionResult> PurgeRead([FromQuery] int days = 30)
    {
        days = Math.Clamp(days, 1, 3650);
        var count = await _notificationService.PurgeReadBeforeAsync(
            GetCurrentUserId(), DateTime.UtcNow.AddDays(-days));
        return Ok(new { deletedCount = count });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var count = await _notificationService.GetUnreadCountAsync(GetCurrentUserId());
        return Ok(new NotificationCountDto { UnreadCount = count });
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var notification = await _notificationService.MarkAsReadAsync(id, GetCurrentUserId());
        if (notification is null)
            return NotFound(new { error = "Notification introuvable." });

        return Ok(notification);
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var count = await _notificationService.MarkAllAsReadAsync(GetCurrentUserId());
        return Ok(new { markedCount = count, unreadCount = 0 });
    }
}
