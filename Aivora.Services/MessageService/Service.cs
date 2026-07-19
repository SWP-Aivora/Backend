using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
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
            .Include(c => c.Client)
            .Include(c => c.Expert)
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

            // Load navigation properties for the new conversation
            conversation.Client = await _dbContext.Users.FindAsync(clientId);
            conversation.Expert = await _dbContext.Users.FindAsync(expertId);
        }

        return await MapToConversationResponse(conversation, clientId, isUnreadApplicable: true); // Client is never admin
    }

    public async Task<Base.Response.PageResult<Response.ConversationResponse>> GetUserConversationsAsync(Guid userId, Base.Request.PageRequest pageRequest)
    {
        var query = _dbContext.Conversations
            .Where(c => c.ClientId == userId || c.ExpertId == userId);

        var totalItems = await query.CountAsync();
        var responses = await query
            .OrderByDescending(c => c.UpdatedAt)
            .Skip((pageRequest.PageIndex - 1) * pageRequest.PageSize)
            .Take(pageRequest.PageSize)
            .Select(c => new Response.ConversationResponse
            {
                Id = c.Id,
                JobId = c.JobId,
                ProjectId = c.ProjectId,
                ClientId = c.ClientId,
                ClientName = c.Client.FullName ?? "Unknown",
                ClientAvatar = c.Client.AvatarUrl,
                ExpertId = c.ExpertId,
                ExpertName = c.Expert.FullName ?? "Unknown",
                ExpertAvatar = c.Expert.AvatarUrl,
                LastMessage = c.Messages.OrderByDescending(m => m.CreatedAt)
                    .Select(m => m.Content ?? (m.AttachmentUrl != null ? "[Attachment]" : "No messages yet"))
                    .FirstOrDefault() ?? "No messages yet",
                UpdatedAt = c.UpdatedAt ?? DateTimeOffset.UtcNow,
                UnreadCount = c.Messages.Count(m => m.SenderId != userId && !m.IsRead)
            })
            .ToListAsync();

        return new Base.Response.PageResult<Response.ConversationResponse>
        {
            Items = responses,
            PageIndex = pageRequest.PageIndex,
            PageSize = pageRequest.PageSize,
            TotalItems = totalItems
        };
    }

    public async Task<Base.Response.PageResult<Response.MessageResponse>> GetConversationMessagesAsync(Guid userId, UserRole userRole, Guid conversationId, Base.Request.PageRequest pageRequest)
    {
        await EnsureConversationParticipantAsync(userId, userRole, conversationId);

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
            SenderRole = m.Sender.Role,
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

    public async Task EnsureConversationParticipantAsync(Guid userId, UserRole userRole, Guid conversationId)
    {
        var conversation = await _dbContext.Conversations.FindAsync(conversationId);
        if (conversation == null) throw new NotFoundException("Conversation not found.");

        if (conversation.ClientId == userId || conversation.ExpertId == userId)
        {
            return;
        }

        if (userRole == UserRole.ADMIN)
        {
            await EnsureAdminCanViewConversationAsync(conversation);
            return;
        }

        throw new ForbiddenException("You are not a participant in this conversation.");
    }

    private async Task EnsureAdminCanViewConversationAsync(Conversation conversation)
    {

        if (conversation == null) throw new NotFoundException("Conversation not found.");

        // Check if there is an active dispute between the participants
        var hasOpenDispute = conversation.ProjectId.HasValue && await _dbContext.Disputes.AnyAsync(d => d.ProjectId == conversation.ProjectId.Value
            && (d.Status == DisputeStatus.OPEN || d.Status == DisputeStatus.UNDER_REVIEW)
            && ((d.OpenedBy == conversation.ClientId && d.AgainstUserId == conversation.ExpertId)
                || (d.OpenedBy == conversation.ExpertId && d.AgainstUserId == conversation.ClientId)));

        if (hasOpenDispute)
        {
            return;
        }

        // Allow access if the conversation has Job/Project context AND the participants are associated with real users
        var hasJobOrProject = conversation.JobId.HasValue || conversation.ProjectId.HasValue;
        var hasUserAssociation = await _dbContext.Users.AnyAsync(u => u.Id == conversation.ClientId || u.Id == conversation.ExpertId);

        if (!hasJobOrProject || !hasUserAssociation)
        {
            throw new ForbiddenException("Admin is not authorized to view this conversation.");
        }
    }

    public async Task<Response.MessageResponse> SendMessageAsync(Guid senderId, UserRole senderRole, Request.SendMessageRequest request)
    {
        var conversation = await _dbContext.Conversations
            .Include(c => c.Client)
            .Include(c => c.Expert)
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId);

        if (conversation == null) throw new NotFoundException("Conversation not found.");

        string senderName = "Unknown";
        if (senderRole == UserRole.ADMIN)
        {
            await EnsureAdminCanViewConversationAsync(conversation);

            var adminUser = await _dbContext.Users.FindAsync(senderId);
            senderName = adminUser?.FullName ?? "Admin User";
        }
        else
        {
            if (conversation.ClientId != senderId && conversation.ExpertId != senderId)
            {
                throw new ForbiddenException("You are not a participant in this conversation.");
            }

            senderName = conversation.ClientId == senderId
                ? (conversation.Client?.FullName ?? "Client")
                : (conversation.Expert?.FullName ?? "Expert");
        }

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

        return new Response.MessageResponse
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            SenderName = senderName,
            SenderRole = senderRole,
            Content = message.Content,
            AttachmentUrl = message.AttachmentUrl,
            IsRead = message.IsRead,
            CreatedAt = message.CreatedAt
        };
    }

    public async Task MarkAsReadAsync(Guid userId, Guid conversationId)
    {
        var conversation = await _dbContext.Conversations.FindAsync(conversationId);
        if (conversation == null) throw new NotFoundException("Conversation not found.");

        if (conversation.ClientId != userId && conversation.ExpertId != userId)
        {
            throw new ForbiddenException("You are not a participant in this conversation.");
        }

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

    public async Task<Base.Response.PageResult<Response.ConversationResponse>> GetAdminConversationsAsync(Guid? jobId, Guid? projectId, Guid? userId, Base.Request.PageRequest pageRequest)
    {
        var query = _dbContext.Conversations.AsQueryable();

        if (jobId.HasValue)
        {
            query = query.Where(c => c.JobId == jobId.Value);
        }

        if (projectId.HasValue)
        {
            query = query.Where(c => c.ProjectId == projectId.Value);
        }

        if (userId.HasValue)
        {
            query = query.Where(c => c.ClientId == userId.Value || c.ExpertId == userId.Value);
        }

        var totalItems = await query.CountAsync();
        var responses = await query
            .OrderByDescending(c => c.UpdatedAt)
            .Skip((pageRequest.PageIndex - 1) * pageRequest.PageSize)
            .Take(pageRequest.PageSize)
            .Select(c => new Response.ConversationResponse
            {
                Id = c.Id,
                JobId = c.JobId,
                ProjectId = c.ProjectId,
                ClientId = c.ClientId,
                ClientName = c.Client.FullName ?? "Unknown",
                ClientAvatar = c.Client.AvatarUrl,
                ExpertId = c.ExpertId,
                ExpertName = c.Expert.FullName ?? "Unknown",
                ExpertAvatar = c.Expert.AvatarUrl,
                LastMessage = c.Messages.OrderByDescending(m => m.CreatedAt)
                    .Select(m => m.Content ?? (m.AttachmentUrl != null ? "[Attachment]" : "No messages yet"))
                    .FirstOrDefault() ?? "No messages yet",
                UpdatedAt = c.UpdatedAt ?? DateTimeOffset.UtcNow,
                UnreadCount = 0
            })
            .ToListAsync();

        return new Base.Response.PageResult<Response.ConversationResponse>
        {
            Items = responses,
            PageIndex = pageRequest.PageIndex,
            PageSize = pageRequest.PageSize,
            TotalItems = totalItems
        };
    }

    private async Task<Response.ConversationResponse> MapToConversationResponse(Conversation c, Guid currentUserId, bool? isUnreadApplicable = null)
    {
        // Force load navigation properties if they are null (due to potential missing includes in some calls)
        if (c.Client == null)
        {
            var clientUser = await _dbContext.Users.FindAsync(c.ClientId);
            if (clientUser != null)
            {
                c.Client = clientUser;
            }
        }
        if (c.Expert == null)
        {
            var expertUser = await _dbContext.Users.FindAsync(c.ExpertId);
            if (expertUser != null)
            {
                c.Expert = expertUser;
            }
        }

        var lastMsg = c.Messages != null && c.Messages.Any()
            ? c.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault()
            : await _dbContext.Messages
                .Where(m => m.ConversationId == c.Id)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();

        bool unreadApplicable;
        if (isUnreadApplicable.HasValue)
        {
            unreadApplicable = isUnreadApplicable.Value;
        }
        else
        {
            var user = currentUserId != Guid.Empty ? await _dbContext.Users.FindAsync(currentUserId) : null;
            unreadApplicable = user != null && user.Role != Aivora.Repositories.Enums.UserRole.ADMIN;
        }

        var unreadCount = unreadApplicable
            ? await _dbContext.Messages.CountAsync(m => m.ConversationId == c.Id && m.SenderId != currentUserId && !m.IsRead)
            : 0;

        return new Response.ConversationResponse
        {
            Id = c.Id,
            JobId = c.JobId,
            ProjectId = c.ProjectId,
            ClientId = c.ClientId,
            ClientName = c.Client?.FullName ?? "Unknown",
            ClientAvatar = c.Client?.AvatarUrl,
            ExpertId = c.ExpertId,
            ExpertName = c.Expert?.FullName ?? "Unknown",
            ExpertAvatar = c.Expert?.AvatarUrl,
            LastMessage = lastMsg?.Content ?? (lastMsg?.AttachmentUrl != null ? "[Attachment]" : "No messages yet"),
            UpdatedAt = c.UpdatedAt ?? DateTimeOffset.UtcNow,
            UnreadCount = unreadCount
        };
    }
}
