using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class Deliverable : BaseEntity
{
    public Guid MilestoneId { get; set; }
    public Guid ExpertId { get; set; }
    public string? Description { get; set; }
    public string? FileUrl { get; set; }
    public string? DemoUrl { get; set; }
    public string? SourceCodeUrl { get; set; }
    public string? Note { get; set; }
    public int RevisionNumber { get; set; } = 1;
    public DeliverableStatus Status { get; set; } = DeliverableStatus.SUBMITTED;
    public DateTimeOffset? ReviewedAt { get; set; }

    // Navigation Properties
    public virtual Milestone Milestone { get; set; } = null!;
    public virtual User Expert { get; set; } = null!;
}
