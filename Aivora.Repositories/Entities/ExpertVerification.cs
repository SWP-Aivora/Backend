using Aivora.Repositories.Abstractions;
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class ExpertVerification : AuditableBaseEntity
{
    public Guid ExpertSkillId { get; set; }
    public string EvidenceFileUrl { get; set; } = null!;
    public string EvidencePublicId { get; set; } = null!;

    public ExpertVerificationStatus Status { get; set; }

    public decimal? AIConfidenceScore { get; set; }
    public string? AIReasoning { get; set; }

    public Guid? AdminId { get; set; }
    public string? AdminDecisionReason { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    // Navigation Properties
    public virtual ExpertSkill ExpertSkill { get; set; } = null!;
}
