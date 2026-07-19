using Aivora.Repositories.Abstractions;
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class JobInvite : AuditableBaseEntity
{
    public Guid JobId { get; set; }
    public Guid ExpertId { get; set; }
    public Guid ClientId { get; set; }
    public JobInviteStatus Status { get; set; } = JobInviteStatus.PENDING;
    public DateTimeOffset? RespondedAt { get; set; }

    // Navigation Properties
    public virtual JobPost Job { get; set; } = null!;
    public virtual User Expert { get; set; } = null!;
    public virtual User Client { get; set; } = null!;
}
