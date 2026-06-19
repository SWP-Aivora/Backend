using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.MessageService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;

    public Service(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Response.ConversationResponse> GetOrCreateConversationAsync(Guid clientId, Guid expertId, Guid? jobId = null, Guid? projectId = null)
    {
        var conversation = await _dbContext.Conversations
            .FirstOrDefaultAsync(c => c.ClientId == clientId && c.ExpertId == expertId && c.JobId == jobId && c.ProjectId == projectId);

        if (conversation == null)
        {
            conversation = new Conversation
            {
                ClientId = clientId,
                ExpertId = expertId,
                JobId = jobId,
                ProjectId = projectId
            };
            _dbContext.Conversations.Add(conversation);
            await _dbContext.SaveChangesAsync();
        }

        return await MapToConversationResponse(conversation, clientId); // Default to client for unread
    }

    public async Task<Base.Response.PageResult<Response.ConversationResponse>> GetUserConversationsAsync(Guid userId, Base.Request.PageRequest pageRequest)
    {
        var query = _dbContext.Conversations
            .Include(c => c.Client)
            .Include(c => c.Expert)
            .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
            .Where(c => c.ClientId == userId || c.ExpertId == userId);

        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.UpdatedAt)
            .Skip((pageRequest.PageIndex - 1) * pageRequest.PageSize)
            .Take(pageRequest.PageSize)
            .ToListAsync();

        var responses = new List<Response.ConversationResponse>();
        foreach (var c in items)
        {
            responses.Add(await MapToConversationResponse(c, userId));
        }

        return new Base.Response.PageResult<Response.ConversationResponse>
        {
            Items = responses,
            PageIndex = pageRequest.PageIndex,
            PageSize = pageRequest.PageSize,
            TotalItems = totalItems
        };
    }

    public async Task<Base.Response.PageResult<Response.MessageResponse>> GetConversationMessagesAsync(Guid userId, Guid conversationId, Base.Request.PageRequest pageRequest)
    {
        await EnsureConversationParticipantAsync(userId, conversationId);

        var query = _dbContext.Messages
            .Include(m => m.Sender)
            .Where(m => m.ConversationId == conversationId);

        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((pageRequest.PageIndex - 1) * pageRequest.PageSize)
            .Take(pageRequest.PageSize)
            .ToListAsync();

        var responses = items.Select(m => new Response.MessageResponse
        {
            Id = m.Id,
            ConversationId = m.ConversationId,
            SenderId = m.SenderId,
            SenderName = m.Sender.FullName,
            Content = m.Content,
            AttachmentUrl = m.AttachmentUrl,
            IsRead = m.IsRead,
            CreatedAt = m.CreatedAt
        }).Reverse().ToList(); // Back to chronological order for the page

        return new Base.Response.PageResult<Response.MessageResponse>
        {
            Items = responses,
            PageIndex = pageRequest.PageIndex,
            PageSize = pageRequest.PageSize,
            TotalItems = totalItems
        };
    }

    public async Task EnsureConversationParticipantAsync(Guid userId, Guid conversationId)
    {
        var conversation = await _dbContext.Conversations.FindAsync(conversationId);
        if (conversation == null) throw new NotFoundException("Conversation not found.");
        if (conversation.ClientId != userId && conversation.ExpertId != userId)
            throw new UnauthorizedException("You are not a participant in this conversation.");
    }

    public async Task<Response.MessageResponse> SendMessageAsync(Guid senderId, Request.SendMessageRequest request)
    {
        var conversation = await _dbContext.Conversations
            .Include(c => c.Client)
            .Include(c => c.Expert)
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId);

        if (conversation == null) throw new NotFoundException("Conversation not found.");
        if (conversation.ClientId != senderId && conversation.ExpertId != senderId)
            throw new UnauthorizedException("You are not a participant in this conversation.");

        var message = new Message
        {
            ConversationId = request.ConversationId,
            SenderId = senderId,
            Content = request.Content,
            AttachmentUrl = request.AttachmentUrl
        };

        conversation.UpdatedAt = DateTimeOffset.UtcNow; // Trigger updated time
        _dbContext.Messages.Add(message);
        await _dbContext.SaveChangesAsync();

        var sender = await _dbContext.Users.FindAsync(senderId);

        return new Response.MessageResponse
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            SenderName = sender!.FullName,
            Content = message.Content,
            AttachmentUrl = message.AttachmentUrl,
            IsRead = message.IsRead,
            CreatedAt = message.CreatedAt
        };
    }

    public async Task MarkAsReadAsync(Guid userId, Guid conversationId)
    {
        await EnsureConversationParticipantAsync(userId, conversationId);

        var messages = await _dbContext.Messages
            .Where(m => m.ConversationId == conversationId && m.SenderId != userId && !m.IsRead)
            .ToListAsync();

        if (messages.Any())
        {
            foreach (var m in messages)
            {
                m.IsRead = true;
                m.ReadAt = DateTimeOffset.UtcNow;
            }
            await _dbContext.SaveChangesAsync();
        }
    }

    private async Task<Response.ConversationResponse> MapToConversationResponse(Conversation c, Guid currentUserId)
    {
        // Force load navigation properties if they are null (due to potential missing includes in some calls)
        if (c.Client == null) await _dbContext.Entry(c).Reference(x => x.Client).LoadAsync();
        if (c.Expert == null) await _dbContext.Entry(c).Reference(x => x.Expert).LoadAsync();

        var lastMsg = await _dbContext.Messages
            .Where(m => m.ConversationId == c.Id)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync();

        var unreadCount = await _dbContext.Messages
            .CountAsync(m => m.ConversationId == c.Id && m.SenderId != currentUserId && !m.IsRead);

        return new Response.ConversationResponse
        {
            Id = c.Id,
            JobId = c.JobId,
            ProjectId = c.ProjectId,
            ClientId = c.ClientId,
            ClientName = c.Client!.FullName,
            ClientAvatar = c.Client.AvatarUrl,
            ExpertId = c.ExpertId,
            ExpertName = c.Expert!.FullName,
            ExpertAvatar = c.Expert.AvatarUrl,
            LastMessage = lastMsg?.Content ?? (lastMsg?.AttachmentUrl != null ? "[Attachment]" : "No messages yet"),
            UpdatedAt = c.UpdatedAt ?? DateTimeOffset.UtcNow,
            UnreadCount = unreadCount
        };
    }
}
