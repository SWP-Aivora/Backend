using Aivora.Repositories.Abstractions;
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class Proposal : AuditableBaseEntity
{
    public Guid JobId { get; set; }
    public Guid ExpertId { get; set; }
    public string CoverLetter { get; set; } = null!;
    public decimal ProposedBudget { get; set; }
    public int? ProposedTimelineDays { get; set; }
    public string Currency { get; set; } = "AICOIN";
    public ProposalStatus Status { get; set; } = ProposalStatus.SUBMITTED;
    public DateTimeOffset? WithdrawnAt { get; set; }

    // Navigation Properties
    public virtual JobPost Job { get; set; } = null!;
    public virtual User Expert { get; set; } = null!;
    public virtual ICollection<ProposalMilestone> Milestones { get; set; } = new List<ProposalMilestone>();
}

