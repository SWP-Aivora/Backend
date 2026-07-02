using Aivora.Repositories.Enums;

namespace Aivora.Services.ExpertVerificationService.Providers;

public class MockAIExpertVerificationProvider : IAIExpertVerificationProvider
{
    public Task<AIVerificationResult> AnalyzeEvidenceAsync(AnalyzeEvidenceRequest request, CancellationToken cancellationToken = default)
    {
        var result = new AIVerificationResult
        {
            Outcome = ExpertVerificationStatus.APPROVED,
            ConfidenceScore = 95,
            Reasoning = $"Mock verification: evidence accepted for '{request.ClaimedSkillName}' (no Gemini API key configured)."
        };

        return Task.FromResult(result);
    }
}
