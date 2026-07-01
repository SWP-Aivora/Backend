using Aivora.Repositories.Abstractions;

namespace Aivora.Repositories.Entities;

public class VerificationCertificate : AuditableBaseEntity
{
    public Guid ExpertId { get; set; }
    public string CertificateName { get; set; } = null!;
    public string IssuingOrganization { get; set; } = null!;
    public string CertificateUrl { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int Score { get; set; } // 0-100 from AI validation
    public bool IsVerified { get; set; }
    public string? VerificationNotes { get; set; }

    // Navigation
    public virtual ExpertProfile Expert { get; set; } = null!;
}