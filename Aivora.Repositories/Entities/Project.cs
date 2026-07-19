using Aivora.Repositories.Abstractions;
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class Project : AuditableBaseEntity
{
    public Guid? JobId { get; set; }
    public Guid? AcceptedProposalId { get; set; }
    public Guid? ServiceRequestId { get; set; }
    public Guid ClientId { get; set; }
    public Guid ExpertId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal? TotalBudget { get; set; }
    public string Currency { get; set; } = "AICOIN";
    public ProjectStatus Status { get; set; } = ProjectStatus.PENDING_PAYMENT;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    public bool IsClosed => Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED;

    // Navigation Properties
    public virtual JobPost? Job { get; set; }
    public virtual Proposal? AcceptedProposal { get; set; }
    public virtual ServiceRequest? ServiceRequest { get; set; }
    public virtual User Client { get; set; } = null!;
    public virtual User Expert { get; set; } = null!;
    public virtual ICollection<Milestone> Milestones { get; set; } = new List<Milestone>();
}

