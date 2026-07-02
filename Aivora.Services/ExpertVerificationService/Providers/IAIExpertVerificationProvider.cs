using Aivora.Repositories.Enums;

namespace Aivora.Services.ExpertVerificationService.Providers;

public interface IAIExpertVerificationProvider
{
    Task<AIVerificationResult> AnalyzeEvidenceAsync(AnalyzeEvidenceRequest request, CancellationToken cancellationToken = default);
}

public class AnalyzeEvidenceRequest
{
    public byte[] FileBytes { get; set; } = Array.Empty<byte>();
    public string MimeType { get; set; } = null!;
    public string ExpertFullName { get; set; } = null!;
    public string ClaimedSkillName { get; set; } = null!;
}

public class AIVerificationResult
{
    public ExpertVerificationStatus Outcome { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string Reasoning { get; set; } = null!;
}
