using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.VerificationService;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.VerificationService;

public class Service : IVerificationService
{
    private readonly AivoraDbContext _dbContext;

    public Service(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ExpertVerification> StartVerificationAsync(Guid expertId)
    {
        // Check if expert exists
        var expert = await _dbContext.ExpertProfiles.FindAsync(expertId);
        if (expert == null)
            throw new NotFoundException("Expert not found");

        // Create or get verification record
        var verification = await _dbContext.ExpertVerifications
            .FirstOrDefaultAsync(v => v.ExpertId == expertId);

        if (verification == null)
        {
            verification = new ExpertVerification
            {
                ExpertId = expertId,
                Status = VerificationStatus.PENDING,
                CurrentStage = 0,
                ProcessingStatus = "queued",
                RetryCount = 0,
                TotalScore = 0,
                ProfileScore = 0,
                SkillsScore = 0,
                CertificatesScore = 0,
                ProfileWeight = 0.3m,
                SkillsWeight = 0.5m,
                CertificatesWeight = 0.2m,
                IsPassed = false,
                MaxRetries = 3
            };
            _dbContext.ExpertVerifications.Add(verification);
        }
        else
        {
            verification.Status = VerificationStatus.PENDING;
            verification.CurrentStage = 0;
            verification.ProcessingStatus = "queued";
            verification.RetryCount = 0;
            verification.TotalScore = 0;

            // Ensure UpdatedAt is updated for existing entity
            verification.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync();
        return verification;
    }

    public Task<int> ScoreProfileAsync(Guid expertId)
    {
        // Return dummy score for testing
        return Task.FromResult(50);
    }

    public Task<int> ScoreSkillsAsync(Guid expertId)
    {
        // Return dummy score for testing
        return Task.FromResult(50);
    }

    public Task<int> ScoreCertificatesAsync(Guid expertId)
    {
        // Return dummy score for testing
        return Task.FromResult(50);
    }

    public async Task<ExpertVerification> ProcessVerificationAsync(Guid expertId)
    {
        // Get existing verification
        var verification = await _dbContext.ExpertVerifications
            .FirstOrDefaultAsync(v => v.ExpertId == expertId);

        if (verification == null)
            throw new NotFoundException("Verification not found");

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

    public async Task<ExpertVerification> RetryFailedVerificationAsync(Guid expertId)
    {
        // Get existing verification
        var verification = await _dbContext.ExpertVerifications
            .FirstOrDefaultAsync(v => v.ExpertId == expertId);

        if (verification == null)
            throw new NotFoundException("Verification not found");

        if (verification.Status != VerificationStatus.REJECTED || verification.RetryCount >= verification.MaxRetries)
            throw new ValidationException("Cannot retry this verification");

        // Reset for retry
        verification.Status = VerificationStatus.PENDING;
        verification.CurrentStage = 0;
        verification.ProcessingStatus = "queued";
        verification.RetryCount++;
        verification.TotalScore = 0;
        verification.ProfileScore = 0;
        verification.SkillsScore = 0;
        verification.CertificatesScore = 0;

        await _dbContext.SaveChangesAsync();

        return verification;
    }

    public async Task<ExpertVerification> SubmitAppealAsync(Guid expertId, string reason)
    {
        // Get existing verification
        var verification = await _dbContext.ExpertVerifications
            .FirstOrDefaultAsync(v => v.ExpertId == expertId);

        if (verification == null)
            throw new NotFoundException("Verification not found");

        // Only allow appeal from REJECTED status
        if (verification.Status != VerificationStatus.REJECTED)
            throw new ValidationException("Appeal can only be submitted for rejected verifications");

        // Update to appeal pending
        verification.Status = VerificationStatus.APPEAL_PENDING;
        verification.AppealReason = reason;
        verification.AppealRequestedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return verification;
    }

    public async Task<ExpertVerification> ReviewAppealAsync(Guid appealId, bool approved, string? adminFeedback)
    {
        // Get existing verification
        var verification = await _dbContext.ExpertVerifications
            .FirstOrDefaultAsync(v => v.Id == appealId);

        if (verification == null)
            throw new NotFoundException("Appeal not found");

        if (approved)
        {
            // Approve the appeal - expert becomes verified
            verification.Status = VerificationStatus.VERIFIED;
            verification.IsPassed = true;
        }
        else
        {
            // Reject the appeal - remains rejected
            verification.Status = VerificationStatus.REJECTED;
            verification.IsPassed = false;
        }

        // Update admin feedback
        verification.AppealAdminFeedback = adminFeedback;

        await _dbContext.SaveChangesAsync();

        return verification;
    }

    public async Task<ExpertVerification> AdminApproveAsync(Guid expertId)
    {
        // Get existing verification
        var verification = await _dbContext.ExpertVerifications
            .FirstOrDefaultAsync(v => v.ExpertId == expertId);

        if (verification == null)
            throw new NotFoundException("Verification not found");

        // Approve verification
        verification.Status = VerificationStatus.VERIFIED;
        verification.IsPassed = true;
        verification.Feedback = "Approved by administrator";
        verification.RetryCount = 0; // Reset retry count
        verification.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return verification;
    }

    public async Task<ExpertVerification> AdminRejectAsync(Guid expertId, string reason)
    {
        // Get existing verification
        var verification = await _dbContext.ExpertVerifications
            .FirstOrDefaultAsync(v => v.ExpertId == expertId);

        if (verification == null)
            throw new NotFoundException("Verification not found");

        // Reject verification
        verification.Status = VerificationStatus.REJECTED;
        verification.IsPassed = false;
        verification.FailureReason = reason ?? "Rejected by administrator";
        verification.Feedback = "Verification rejected: " + reason;
        verification.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return verification;
    }

    public Task<ExpertVerification?> GetVerificationAsync(Guid expertId)
    {
        return _dbContext.ExpertVerifications
            .FirstOrDefaultAsync(v => v.ExpertId == expertId);
    }

    public async Task<IEnumerable<ExpertVerification>> GetPendingVerificationsAsync()
    {
        return await _dbContext.ExpertVerifications
            .Where(v => v.Status == VerificationStatus.PENDING)
            .OrderBy(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ExpertVerification>> GetFailedVerificationsAsync()
    {
        return await _dbContext.ExpertVerifications
            .Where(v => v.Status == VerificationStatus.REJECTED)
            .OrderByDescending(v => v.UpdatedAt)
            .ToListAsync();
    }

    public async Task<VerificationAnalytics> GetAnalyticsAsync()
    {
        var totalExperts = await _dbContext.ExpertVerifications.CountAsync();
        var pendingVerifications = await _dbContext.ExpertVerifications
            .CountAsync(v => v.Status == VerificationStatus.PENDING);
        var passedVerifications = await _dbContext.ExpertVerifications
            .CountAsync(v => v.Status == VerificationStatus.VERIFIED);
        var failedVerifications = await _dbContext.ExpertVerifications
            .CountAsync(v => v.Status == VerificationStatus.REJECTED);

        var passRate = totalExperts > 0 ? (decimal)passedVerifications / totalExperts : 0;

        // Calculate average processing time
        var processedVerifications = await _dbContext.ExpertVerifications
            .Where(v => v.Status != VerificationStatus.PENDING && v.LastProcessedAt.HasValue)
            .ToListAsync();

        var averageProcessingTime = processedVerifications.Any()
            ? (decimal)(processedVerifications.Average(v => (v.LastProcessedAt - v.CreatedAt).Value.TotalMinutes))
            : 0;

        // Calculate average retry count
        var averageRetryCount = await _dbContext.ExpertVerifications
            .AverageAsync(v => v.RetryCount);

        // Get top failure reasons
        var failureReasons = await _dbContext.ExpertVerifications
            .Where(v => v.Status == VerificationStatus.REJECTED && !string.IsNullOrEmpty(v.FailureReason))
            .GroupBy(v => v.FailureReason)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new { Reason = g.Key, Count = g.Count() })
            .ToListAsync();

        return new VerificationAnalytics
        {
            TotalExperts = totalExperts,
            PendingVerifications = pendingVerifications,
            PassedVerifications = passedVerifications,
            FailedVerifications = failedVerifications,
            AverageProcessingTimeMinutes = (int)averageProcessingTime,
            AverageRetryCount = (int)averageRetryCount,
            FailureReasons = failureReasons.ToDictionary(fr => fr.Reason, fr => fr.Count)
        };
    }

    // Helper methods
    private int ExtractScoreFromResponse(string response)
    {
        return 50; // Dummy
    }

    private int CalculateFallbackProfileScore(ExpertProfile expert)
    {
        return 50; // Dummy
    }

    private int CalculateFallbackSkillsScore(ExpertProfile expert)
    {
        return 50; // Dummy
    }

    private int CalculateFallbackCertificatesScore(List<VerificationCertificate> certificates)
    {
        return 50; // Dummy
    }
}

// Mock provider for testing
public class MockAIJobSuggestionProvider
{
    public Task<string> SuggestJobAsync(string prompt)
    {
        return Task.FromResult("Mock AI response");
    }
}