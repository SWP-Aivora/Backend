using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Aivora.Services.MessageService;
using Aivora.api.Extensions;

namespace Aivora.api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IService _messageService;

    public ChatHub(IService messageService)
    {
        _messageService = messageService;
    }

    public async Task JoinConversation(Guid conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, conversationId.ToString());
    }

    public async Task LeaveConversation(Guid conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId.ToString());
    }

    public async Task SendMessage(Request.SendMessageRequest request)
    {
        var senderId = Context.User!.GetUserId();
        var message = await _messageService.SendMessageAsync(senderId, request);

        // Broadcast to all participants in the conversation
        await Clients.Group(request.ConversationId.ToString()).SendAsync("ReceiveMessage", message);
    }
}
