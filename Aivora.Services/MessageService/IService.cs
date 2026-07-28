using Aivora.Repositories.Enums;

namespace Aivora.Services.MessageService;

public interface IService
{
    public const int MaxContentLength = 4000; // matches MessageConfiguration.cs varchar(4000)

    Task<Response.ConversationResponse> GetOrCreateConversationAsync(Guid clientId, Guid expertId, Guid? jobId = null, Guid? projectId = null, Guid? serviceRequestId = null);
    Task<Base.Response.PageResult<Response.ConversationResponse>> GetUserConversationsAsync(Guid userId, Base.Request.PageRequest pageRequest);
    Task<Base.Response.PageResult<Response.MessageResponse>> GetConversationMessagesAsync(Guid userId, UserRole userRole, Guid conversationId, Base.Request.PageRequest pageRequest);
    Task EnsureConversationParticipantAsync(Guid userId, UserRole userRole, Guid conversationId);
    Task<Response.MessageResponse> SendMessageAsync(Guid senderId, UserRole senderRole, Request.SendMessageRequest request);
    Task MarkAsReadAsync(Guid userId, Guid conversationId);
    Task<Base.Response.PageResult<Response.ConversationResponse>> GetAdminConversationsAsync(Guid? jobId, Guid? projectId, Guid? userId, Base.Request.PageRequest pageRequest);
}

