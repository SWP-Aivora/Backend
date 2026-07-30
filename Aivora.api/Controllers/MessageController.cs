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
        var userRole = this.GetUserRole();
        var result = await _messageService.GetConversationMessagesAsync(userId, userRole, id, pageRequest);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Messages retrieved", HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var userId = this.GetUserId();
        await _messageService.MarkAsReadAsync(userId, id);
        return Ok(ApiResponseFactory.SuccessResponse(null, "Messages marked as read", HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Gets or creates a conversation with the given counterpart.
    /// </summary>
    /// <param name="expertId">The counterpart's user id: the expert when called by a client, the client when called by an expert.</param>
    /// <param name="jobId">Optional job post to scope the conversation to. Required for an expert without a shared project.</param>
    /// <param name="projectId">Optional shared project to scope the conversation to.</param>
    /// <returns>The existing or newly created conversation. Expert callers need a non-rejected proposal on the job or a shared project, otherwise 403.</returns>
    [HttpPost("init")]
    public async Task<IActionResult> InitConversation([FromQuery] Guid expertId, [FromQuery] Guid? jobId, [FromQuery] Guid? projectId)
    {
        // "expertId" is the counterpart's id: for a CLIENT caller it is the expert,
        // for an EXPERT caller it is the client (param name kept for back-compat).
        var userId = this.GetUserId();
        var userRole = this.GetUserRole();

        var result = userRole == Aivora.Repositories.Enums.UserRole.EXPERT
            ? await _messageService.GetOrCreateConversationAsync(expertId, userId, jobId, projectId, expertInitiated: true)
            : await _messageService.GetOrCreateConversationAsync(userId, expertId, jobId, projectId);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Conversation initialized", HttpContext.TraceIdentifier));
    }

    [HttpGet("admin")]
    [Authorize(Policy = JwtExtensions.AdminPolicy)]
    public async Task<IActionResult> GetAdminConversations(
        [FromQuery] Guid? jobId,
        [FromQuery] Guid? projectId,
        [FromQuery] Guid? userId,
        [FromQuery] Aivora.Services.Base.Request.PageRequest pageRequest)
    {
        var result = await _messageService.GetAdminConversationsAsync(jobId, projectId, userId, pageRequest);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Conversations retrieved for admin", HttpContext.TraceIdentifier));
    }
}
