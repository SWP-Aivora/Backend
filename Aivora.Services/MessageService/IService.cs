namespace Aivora.Services.MessageService;

public interface IService
{
    Task<Response.ConversationResponse> GetOrCreateConversationAsync(Guid clientId, Guid expertId, Guid? jobId = null, Guid? projectId = null);
    Task<Base.Response.PageResult<Response.ConversationResponse>> GetUserConversationsAsync(Guid userId, Base.Request.PageRequest pageRequest);
    Task<Base.Response.PageResult<Response.MessageResponse>> GetConversationMessagesAsync(Guid userId, Guid conversationId, Base.Request.PageRequest pageRequest);
    Task<Response.MessageResponse> SendMessageAsync(Guid senderId, Request.SendMessageRequest request);
    Task MarkAsReadAsync(Guid userId, Guid conversationId);
}
