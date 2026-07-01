using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Services.JwtService;
using Aivora.Services.VerificationService;

namespace Aivora.Services.VerificationService;

public class MockVerificationService : IVerificationService
{
    private readonly AivoraDbContext _dbContext;

    public MockVerificationService(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ExpertVerification> StartVerificationAsync(Guid expertId)
    {
        // Check if expert exists
        var expert = await _dbContext.ExpertProfiles.FindAsync(expertId);
        if (expert == null)
            throw new Exception("Expert profile not found");

        // Check if verification already exists
        var existingVerification = await _dbContext.ExpertVerifications
            .FirstOrDefaultAsync(v => v.ExpertId == expertId);

        if (existingVerification != null)
        {
            return existingVerification;
        }

        // Create new verification
        var verification = new ExpertVerification
        {
            ExpertId = expertId,
            Status = VerificationStatus.PENDING,
            TotalScore = 0,
            ProfileScore = 0,
            SkillsScore = 0,
            CertificatesScore = 0,
            ProfileWeight = 0.3m,
            SkillsWeight = 0.5m,
            CertificatesWeight = 0.2m,
            IsPassed = false,
            RetryCount = 0,
            MaxRetries = 3,
            CurrentStage = 0,
            ProcessingStatus = "pending"
        };

        _dbContext.ExpertVerifications.Add(verification);
        await _dbContext.SaveChangesAsync();

        return verification;
    }

    // Mock implementations for other methods
    public Task<int> ScoreProfileAsync(Guid expertId)
    {
        // Simple mock scoring logic
        // Returns a score between 70-100 for complete profiles
        return Task.FromResult(85);
    }
    public Task<int> ScoreSkillsAsync(Guid expertId) => Task.FromResult(0);
    public Task<int> ScoreCertificatesAsync(Guid expertId) => Task.FromResult(0);
    public async Task<ExpertVerification> ProcessVerificationAsync(Guid expertId)
    {
        // Get existing verification
        var verification = await _dbContext.ExpertVerifications
            .FirstOrDefaultAsync(v => v.ExpertId == expertId);

        if (verification == null)
            throw new Exception("Verification not found");

        // Mock scoring - random score between 60-100
        var random = new Random();
        var totalScore = random.Next(60, 101);

        // Update verification
        verification.TotalScore = totalScore;
        verification.ProfileScore = random.Next(70, 101);
        verification.SkillsScore = random.Next(60, 101);
        verification.CertificatesScore = random.Next(80, 101);
        verification.Status = totalScore >= 70 ? VerificationStatus.VERIFIED : VerificationStatus.REJECTED;
        verification.IsPassed = totalScore >= 70;
        verification.ProcessingStatus = "completed";

        await _dbContext.SaveChangesAsync();

        return verification;
    }
    public Task<ExpertVerification> RetryFailedVerificationAsync(Guid expertId) => throw new NotImplementedException();
    public async Task<ExpertVerification> SubmitAppealAsync(Guid expertId, string reason)
    {
        // Get existing verification
        var verification = await _dbContext.ExpertVerifications
            .FirstOrDefaultAsync(v => v.ExpertId == expertId);

        if (verification == null)
            throw new Exception("Verification not found");

        // Update to appeal pending
        verification.Status = VerificationStatus.APPEAL_PENDING;
        verification.AppealReason = reason;
        verification.AppealSubmissionTime = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return verification;
    }
    public Task<ExpertVerification> ReviewAppealAsync(Guid appealId, bool approved, string? adminFeedback) => throw new NotImplementedException();
    public Task<ExpertVerification> AdminApproveAsync(Guid expertId) => throw new NotImplementedException();
    public Task<ExpertVerification> AdminRejectAsync(Guid expertId, string reason) => throw new NotImplementedException();
    public Task<ExpertVerification?> GetVerificationAsync(Guid expertId) => throw new NotImplementedException();
    public Task<IEnumerable<ExpertVerification>> GetPendingVerificationsAsync() => throw new NotImplementedException();
    public Task<IEnumerable<ExpertVerification>> GetFailedVerificationsAsync() => throw new NotImplementedException();
    public Task<VerificationAnalytics> GetAnalyticsAsync() => throw new NotImplementedException();
}