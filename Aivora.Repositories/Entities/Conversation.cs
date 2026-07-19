using Aivora.Repositories.Abstractions;
namespace Aivora.Repositories.Entities;

public class Conversation : AuditableBaseEntity
{
    public Guid? ProjectId { get; set; }
    public Guid? JobId { get; set; }
    public Guid? ServiceRequestId { get; set; }
    public Guid ClientId { get; set; }
    public Guid ExpertId { get; set; }

    // Navigation Properties
    public virtual Project? Project { get; set; }
    public virtual JobPost? Job { get; set; }
    public virtual ServiceRequest? ServiceRequest { get; set; }
    public virtual User Client { get; set; } = null!;
    public virtual User Expert { get; set; } = null!;
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
}

