namespace Aivora.Repositories.Entities;

public class DisputeEvidence : BaseEntity
{
    public Guid DisputeId { get; set; }
    public Guid SubmittedBy { get; set; }
    public string? Content { get; set; }
    public string? FileUrl { get; set; }

    // Navigation Properties
    public virtual Dispute Dispute { get; set; } = null!;
    public virtual User SubmittedByUser { get; set; } = null!;
}
