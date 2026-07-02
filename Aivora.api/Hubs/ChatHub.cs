using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Aivora.Services.MessageService;
using Aivora.api.Extensions;

namespace Aivora.api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IService _messageService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IService messageService, ILogger<ChatHub> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User!.GetUserId();
        _logger.LogInformation("User {UserId} connected to ChatHub (ConnectionId: {ConnectionId})", userId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User!.GetUserId();
        _logger.LogInformation("User {UserId} disconnected from ChatHub (ConnectionId: {ConnectionId})", userId, Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinConversation(Guid conversationId)
    {
        var userId = Context.User!.GetUserId();
        await _messageService.EnsureConversationParticipantAsync(userId, conversationId);
        await Groups.AddToGroupAsync(Context.ConnectionId, conversationId.ToString());
        _logger.LogInformation("User {UserId} joined conversation {ConversationId}", userId, conversationId);
    }

    public async Task LeaveConversation(Guid conversationId)
    {
        var userId = Context.User!.GetUserId();
        await _messageService.EnsureConversationParticipantAsync(userId, conversationId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId.ToString());
        _logger.LogInformation("User {UserId} left conversation {ConversationId}", userId, conversationId);
    }

    public async Task SendMessage(Request.SendMessageRequest request)
    {
        var senderId = Context.User!.GetUserId();
        var message = await _messageService.SendMessageAsync(senderId, request);

        // Broadcast to all participants in the conversation
        await Clients.Group(request.ConversationId.ToString()).SendAsync("ReceiveMessage", message);
    }

    /// <summary>
    /// Broadcast typing indicator to other participants in the conversation.
    /// </summary>
    public async Task UserTyping(Guid conversationId, bool isTyping)
    {
        var userId = Context.User!.GetUserId();
        await _messageService.EnsureConversationParticipantAsync(userId, conversationId);

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
        var userId = Context.User!.GetUserId();
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
