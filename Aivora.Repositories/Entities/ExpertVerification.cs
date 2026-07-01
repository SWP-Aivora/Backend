using Aivora.Repositories.Abstractions;
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class ExpertVerification : AuditableBaseEntity
{
    public Guid ExpertId { get; set; }
    public VerificationStatus Status { get; set; } = VerificationStatus.PENDING;
    public int TotalScore { get; set; }
    public int ProfileScore { get; set; }
    public int SkillsScore { get; set; }
    public int CertificatesScore { get; set; }
    public decimal ProfileWeight { get; set; } = 0.3m;
    public decimal SkillsWeight { get; set; } = 0.5m;
    public decimal CertificatesWeight { get; set; } = 0.2m;
    public bool IsPassed { get; set; } // Computed property would need change tracking
    public string? Feedback { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public string? AiProcessingId { get; set; }
    public DateTime? LastProcessedAt { get; set; }
    public string? FailureReason { get; set; }

    // Navigation Properties
    public virtual ExpertProfile Expert { get; set; } = null!;

    // Navigation to certificates
    public virtual ICollection<VerificationCertificate> Certificates { get; set; } = new List<VerificationCertificate>();

    // For tracking processing stages
    public bool IsProfileProcessed { get; set; }
    public bool IsSkillsProcessed { get; set; }
    public bool IsCertificatesProcessed { get; set; }

    // For appeal process
    public Guid? AppealAdminId { get; set; }
    public string? AppealReason { get; set; }
    public DateTime? AppealRequestedAt { get; set; }
    public string? AppealAdminFeedback { get; set; }

    // Progress tracking
    public int CurrentStage { get; set; } // 0: pending, 1: profile, 2: skills, 3: certificates
    public string? ProcessingStatus { get; set; } // "queued", "processing", "completed", "failed"
}