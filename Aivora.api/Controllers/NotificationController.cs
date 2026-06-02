using Aivora.api.Extensions;
using Aivora.Services.NotificationService;
using Aivora.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly IService _notificationService;

    public NotificationController(IService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] Aivora.Services.Base.Request.PageRequest pageRequest)
    {
        var userId = this.GetUserId();
        var result = await _notificationService.GetUserNotificationsAsync(userId, pageRequest);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Notifications retrieved", HttpContext.TraceIdentifier));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = this.GetUserId();
        var result = await _notificationService.GetUnreadCountAsync(userId);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Unread count retrieved", HttpContext.TraceIdentifier));
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = this.GetUserId();
        await _notificationService.MarkAsReadAsync(userId, id);
        return Ok(ApiResponseFactory.SuccessResponse(true, "Notification marked as read", HttpContext.TraceIdentifier));
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = this.GetUserId();
        await _notificationService.MarkAllAsReadAsync(userId);
        return Ok(ApiResponseFactory.SuccessResponse(true, "All notifications marked as read", HttpContext.TraceIdentifier));
    }
}
