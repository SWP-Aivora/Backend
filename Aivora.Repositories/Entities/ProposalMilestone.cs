using Aivora.Repositories.Abstractions;
namespace Aivora.Repositories.Entities;

public class ProposalMilestone : AuditableBaseEntity
{
    public Guid ProposalId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public int DueDays { get; set; }
    public string? AcceptanceCriteria { get; set; }
    public int OrderIndex { get; set; }

    // Navigation Properties
    public virtual Proposal Proposal { get; set; } = null!;
}

