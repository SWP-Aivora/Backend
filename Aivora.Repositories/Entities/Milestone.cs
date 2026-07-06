using Aivora.Repositories.Abstractions;
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class Milestone : AuditableBaseEntity
{
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? AcceptanceCriteria { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "AICOIN";
    public DateOnly? DueDate { get; set; }
    public int OrderIndex { get; set; }
    public MilestoneStatus Status { get; set; } = MilestoneStatus.CREATED;
    public DateTimeOffset? FundedAt { get; set; }
    public DateTimeOffset? DepositPaidAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }

    // Navigation Properties
    public virtual Project Project { get; set; } = null!;
    public virtual ICollection<Deliverable> Deliverables { get; set; } = new List<Deliverable>();
    public virtual ICollection<MilestoneStep> Steps { get; set; } = new List<MilestoneStep>();
}

