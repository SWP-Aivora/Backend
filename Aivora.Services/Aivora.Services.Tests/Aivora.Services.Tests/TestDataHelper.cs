using Aivora.Repositories.Entities;

namespace Aivora.Services.VerificationService.Tests;

public static class TestDataHelper
{
    public static ExpertProfile CreateExpertProfile(Guid id, string fullName)
    {
        return new ExpertProfile
        {
            Id = id,
            User = new User
            {
                Id = id,
                FullName = fullName,
                Email = $"expert{id}@example.com"
            },
            VerificationStatus = VerificationStatus.PENDING
        };
    }

    public static ExpertVerification CreateVerification(Guid expertId)
    {
        return new ExpertVerification
        {
            ExpertId = expertId,
            Status = VerificationStatus.PENDING,
            TotalScore = 0,
            ProfileScore = 0,
            SkillsScore = 0,
            CertificatesScore = 0,
            IsPassed = false,
            RetryCount = 0,
            MaxRetries = 3,
            CurrentStage = 0,
            ProcessingStatus = "pending"
        };
    }
}