using Aivora.Repositories.Abstractions;
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class Dispute : AuditableBaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid MilestoneId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid OpenedBy { get; set; }
    public Guid AgainstUserId { get; set; }
    public string Reason { get; set; } = null!;
    public string? Description { get; set; }
    public DisputeStatus Status { get; set; } = DisputeStatus.OPEN;
    public Guid? AdminId { get; set; }
    public DisputeResolutionType? ResolutionType { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    // Navigation Properties
    public virtual Project Project { get; set; } = null!;
    public virtual Milestone Milestone { get; set; } = null!;
    public virtual Payment Payment { get; set; } = null!;
    public virtual User Opener { get; set; } = null!;
    public virtual User? Admin { get; set; }
}

