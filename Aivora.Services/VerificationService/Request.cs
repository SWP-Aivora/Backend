namespace Aivora.Services.VerificationService;

public class StartVerificationRequest
{
    public Guid ExpertId { get; set; }
}

public class RetryVerificationRequest
{
    public Guid ExpertId { get; set; }
}

public class SubmitAppealRequest
{
    public Guid ExpertId { get; set; }
    public string Reason { get; set; } = null!;
}

public class ReviewAppealRequest
{
    public Guid AppealId { get; set; }
    public bool Approved { get; set; }
    public string? AdminFeedback { get; set; }
}

public class AdminVerificationActionRequest
{
    public Guid ExpertId { get; set; }
    public string? Reason { get; set; }
}

public class CertificateVerificationRequest
{
    public Guid ExpertId { get; set; }
    public string CertificateUrl { get; set; } = null!;
}