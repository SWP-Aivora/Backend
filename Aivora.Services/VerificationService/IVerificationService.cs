using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;

namespace Aivora.Services.VerificationService;

public interface IVerificationService
{
    // Trigger verification process
    Task<ExpertVerification> StartVerificationAsync(Guid expertId);

    // AI scoring components
    Task<int> ScoreProfileAsync(Guid expertId);
    Task<int> ScoreSkillsAsync(Guid expertId);
    Task<int> ScoreCertificatesAsync(Guid expertId);

    // Main verification orchestration
    Task<ExpertVerification> ProcessVerificationAsync(Guid expertId);
    Task<ExpertVerification> RetryFailedVerificationAsync(Guid expertId);

    // Appeal handling
    Task<ExpertVerification> SubmitAppealAsync(Guid expertId, string reason);
    Task<ExpertVerification> ReviewAppealAsync(Guid appealId, bool approved, string? adminFeedback);

    // Admin operations
    Task<ExpertVerification> AdminApproveAsync(Guid expertId);
    Task<ExpertVerification> AdminRejectAsync(Guid expertId, string reason);

    // Query operations
    Task<ExpertVerification?> GetVerificationAsync(Guid expertId);
    Task<IEnumerable<ExpertVerification>> GetPendingVerificationsAsync();
    Task<IEnumerable<ExpertVerification>> GetFailedVerificationsAsync();

    // Analytics
    Task<VerificationAnalytics> GetAnalyticsAsync();
}

public class VerificationAnalytics
{
    public int TotalExperts { get; set; }
    public int PendingVerifications { get; set; }
    public int PassedVerifications { get; set; }
    public int FailedVerifications { get; set; }
    public decimal PassRate => TotalExperts > 0 ? (decimal)PassedVerifications / TotalExperts * 100 : 0;
    public int AverageProcessingTimeMinutes { get; set; }
    public int AverageRetryCount { get; set; }
    public Dictionary<string, int> FailureReasons { get; set; } = new();
}