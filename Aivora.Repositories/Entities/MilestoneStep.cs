using Aivora.Repositories.Abstractions;
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class MilestoneStep : AuditableBaseEntity
{
    public Guid MilestoneId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public int OrderIndex { get; set; }
    public MilestoneStepStatus Status { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public string? BlockedReason { get; set; }

    // Navigation Properties
    public virtual Milestone Milestone { get; set; } = null!;
    public virtual User? CompletedByUser { get; set; }
}
