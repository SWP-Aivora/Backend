using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using IMessageService = Aivora.Services.MessageService.IService;
using MessageService = Aivora.Services.MessageService.Service;
using Aivora.api.Extensions;

namespace Aivora.api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private const int MaxContentLength = MessageService.MaxContentLength;

    private readonly IMessageService _messageService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IMessageService messageService, ILogger<ChatHub> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        if (Context.User == null)
        {
            throw new HubException("User is not authenticated.");
        }
        return Context.User.GetUserId();
    }

    private Guid? TryGetCurrentUserId()
    {
        try
        {
            if (Context.User == null) return null;
            var userIdClaim = Context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : null;
        }
        catch
        {
            return null;
        }
    }

    public override async Task OnConnectedAsync()
    {
        var userId = TryGetCurrentUserId();
        _logger.LogInformation("User {UserId} connected to ChatHub (ConnectionId: {ConnectionId})", userId?.ToString() ?? "Anonymous", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = TryGetCurrentUserId();
        _logger.LogInformation("User {UserId} disconnected from ChatHub (ConnectionId: {ConnectionId})", userId?.ToString() ?? "Anonymous", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinConversation(Guid conversationId)
    {
        var userId = GetCurrentUserId();
        var userRole = Context.User!.GetUserRole();
        await _messageService.EnsureConversationParticipantAsync(userId, userRole, conversationId);
        await Groups.AddToGroupAsync(Context.ConnectionId, conversationId.ToString());
        _logger.LogInformation("User {UserId} ({Role}) joined conversation {ConversationId}", userId, userRole, conversationId);
    }

    public async Task LeaveConversation(Guid conversationId)
    {
        var userId = GetCurrentUserId();
        var userRole = Context.User?.GetUserRole();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId.ToString());
        _logger.LogInformation("User {UserId} ({Role}) left conversation {ConversationId}", userId, userRole, conversationId);
    }

    public async Task SendMessage(Aivora.Services.MessageService.Request.SendMessageRequest request)
    {
        if (request.Content is { Length: > MaxContentLength })
        {
            throw new HubException($"Message content exceeds the {MaxContentLength} character limit.");
        }

        var senderId = GetCurrentUserId();
        var senderRole = Context.User!.GetUserRole();
        var message = await _messageService.SendMessageAsync(senderId, senderRole, request);

        // Broadcast to all participants in the conversation
        await Clients.Group(request.ConversationId.ToString()).SendAsync("ReceiveMessage", message);
    }

    /// <summary>
    /// Broadcast typing indicator to other participants in the conversation.
    /// </summary>
    public async Task UserTyping(Guid conversationId, bool isTyping)
    {
        var userId = GetCurrentUserId();
        var userRole = Context.User!.GetUserRole();
        await _messageService.EnsureConversationParticipantAsync(userId, userRole, conversationId);

        // Broadcast to others in the group (exclude sender)
        await Clients.OthersInGroup(conversationId.ToString()).SendAsync("UserTyping", new
        {
            ConversationId = conversationId,
            UserId = userId,
            IsTyping = isTyping,
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Mark messages as read and broadcast read confirmation to conversation participants.
    /// </summary>
    public async Task MarkAsRead(Guid conversationId)
    {
        var userId = GetCurrentUserId();
        await _messageService.MarkAsReadAsync(userId, conversationId);

        // Broadcast read confirmation to other participants
        await Clients.OthersInGroup(conversationId.ToString()).SendAsync("ReadConfirmation", new
        {
            ConversationId = conversationId,
            UserId = userId,
            Timestamp = DateTimeOffset.UtcNow
        });
    }
}
