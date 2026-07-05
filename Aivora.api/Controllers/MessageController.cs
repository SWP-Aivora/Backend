using Aivora.api.Extensions;
using Aivora.Services.MessageService;
using Aivora.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/conversations")]
[Authorize]
[EnableRateLimiting("General")]
public class MessageController : ControllerBase
{
    private readonly IService _messageService;

    public MessageController(IService messageService)
    {
        _messageService = messageService;
    }

    [HttpGet]
    public async Task<IActionResult> GetConversations([FromQuery] Aivora.Services.Base.Request.PageRequest pageRequest)
    {
        var userId = this.GetUserId();
        var result = await _messageService.GetUserConversationsAsync(userId, pageRequest);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Conversations retrieved", HttpContext.TraceIdentifier));
    }

    [HttpGet("{id}/messages")]
    [Authorize(Policy = JwtExtensions.AdminOrParticipantPolicy)]
    public async Task<IActionResult> GetMessages(Guid id, [FromQuery] Aivora.Services.Base.Request.PageRequest pageRequest)
    {
        var userId = this.GetUserId();
        var isAdmin = this.GetUserRole() == Aivora.Repositories.Enums.UserRole.ADMIN;
        var result = await _messageService.GetConversationMessagesAsync(userId, id, pageRequest, isAdmin);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Messages retrieved", HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var userId = this.GetUserId();
        await _messageService.MarkAsReadAsync(userId, id);
        return Ok(ApiResponseFactory.SuccessResponse(null, "Messages marked as read", HttpContext.TraceIdentifier));
    }

    [HttpPost("init")]
    public async Task<IActionResult> InitConversation([FromQuery] Guid expertId, [FromQuery] Guid? jobId, [FromQuery] Guid? projectId)
    {
        var clientId = this.GetUserId();
        var result = await _messageService.GetOrCreateConversationAsync(clientId, expertId, jobId, projectId);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Conversation initialized", HttpContext.TraceIdentifier));
    }
}
