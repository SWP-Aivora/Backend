using Aivora.Repositories.Enums;

namespace Aivora.Services.VerificationService;

public class ExpertVerificationResponse
{
    public Guid Id { get; set; }
    public Guid ExpertId { get; set; }
    public string ExpertName { get; set; } = null!;
    public VerificationStatus Status { get; set; }
    public int TotalScore { get; set; }
    public int ProfileScore { get; set; }
    public int SkillsScore { get; set; }
    public int CertificatesScore { get; set; }
    public decimal ProfileWeight { get; set; }
    public decimal SkillsWeight { get; set; }
    public decimal CertificatesWeight { get; set; }
    public bool IsPassed { get; set; }
    public string? Feedback { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }
    public string? FailureReason { get; set; }
    public int CurrentStage { get; set; }
    public string? ProcessingStatus { get; set; }
    public DateTime? LastProcessedAt { get; set; }
    public bool IsProfileProcessed { get; set; }
    public bool IsSkillsProcessed { get; set; }
    public bool IsCertificatesProcessed { get; set; }
}

public class VerificationProgressResponse
{
    public Guid ExpertId { get; set; }
    public string ExpertName { get; set; } = null!;
    public int CurrentStage { get; set; }
    public string ProcessingStatus { get; set; } = null!;
    public int ProfileProgress { get; set; }
    public int SkillsProgress { get; set; }
    public int CertificatesProgress { get; set; }
    public int TotalProgress { get; set; }
    public string? StageMessage { get; set; }
}

public class VerificationQueueStatusResponse
{
    public int PendingCount { get; set; }
    public int ProcessingCount { get; set; }
    public int FailedCount { get; set; }
    public int RetryableCount { get; set; }
    public List<FailedVerificationItem> FailedItems { get; set; } = new();
}

public class FailedVerificationItem
{
    public Guid ExpertId { get; set; }
    public string ExpertName { get; set; } = null!;
    public string FailureReason { get; set; } = null!;
    public int RetryCount { get; set; }
    public DateTime LastFailedAt { get; set; }
}

public class CertificateResponse
{
    public Guid Id { get; set; }
    public Guid ExpertId { get; set; }
    public string CertificateName { get; set; } = null!;
    public string IssuingOrganization { get; set; } = null!;
    public string CertificateUrl { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int Score { get; set; }
    public bool IsVerified { get; set; }
}

public class VerificationAnalyticsResponse
{
    public int TotalExperts { get; set; }
    public int PendingVerifications { get; set; }
    public int PassedVerifications { get; set; }
    public int FailedVerifications { get; set; }
    public decimal PassRate { get; set; }
    public int AverageProcessingTimeMinutes { get; set; }
    public int AverageRetryCount { get; set; }
    public Dictionary<string, int> FailureReasons { get; set; } = new();
    public Dictionary<string, int> ScoreDistribution { get; set; } = new();
}