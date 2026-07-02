namespace Aivora.Services.ExpertVerificationService;

public class Response
{
    public class ExpertVerificationResponse
    {
        public Guid Id { get; set; }
        public Guid ExpertSkillId { get; set; }
        public string? SkillName { get; set; }
        public Guid ExpertId { get; set; }
        public string EvidenceFileUrl { get; set; } = null!;
        public string Status { get; set; } = null!;
        public decimal? AIConfidenceScore { get; set; }
        public string? AIReasoning { get; set; }
        public Guid? AdminId { get; set; }
        public string? AdminDecisionReason { get; set; }
        public DateTimeOffset? ReviewedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public bool CanEscalate { get; set; }
    }
}
