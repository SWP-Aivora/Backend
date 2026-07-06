namespace Aivora.Services.MessageService;

public class Response
{
    public class ConversationResponse
    {
        public Guid Id { get; set; }
        public Guid? JobId { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid ClientId { get; set; }
        public string ClientName { get; set; } = null!;
        public string? ClientAvatar { get; set; }
        public Guid ExpertId { get; set; }
        public string ExpertName { get; set; } = null!;
        public string? ExpertAvatar { get; set; }
        public string? LastMessage { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public int UnreadCount { get; set; }
    }

    public class MessageResponse
    {
        public Guid Id { get; set; }
        public Guid ConversationId { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = null!;
        public string SenderRole { get; set; } = null!;
        public string? Content { get; set; }
        public string? AttachmentUrl { get; set; }
        public bool IsRead { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
