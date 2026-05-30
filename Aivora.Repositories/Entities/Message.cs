namespace Aivora.Repositories.Entities;

public class Message : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string? Content { get; set; }
    public string? AttachmentUrl { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTimeOffset? ReadAt { get; set; }

    // Navigation Properties
    public virtual Conversation Conversation { get; set; } = null!;
    public virtual User Sender { get; set; } = null!;
}
