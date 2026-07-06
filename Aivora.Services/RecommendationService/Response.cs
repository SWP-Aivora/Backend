namespace Aivora.Services.RecommendationService;

public class Response
{
    public class RecommendationResponse
    {
        public Guid Id { get; set; }
        public Guid JobId { get; set; }
        public Guid ExpertId { get; set; }
        public string ExpertName { get; set; } = null!;
        public string? ExpertTitle { get; set; }
        public decimal TotalScore { get; set; }
        public decimal SkillScore { get; set; }
        public decimal PortfolioScore { get; set; }
        public decimal RatingScore { get; set; }
        public decimal BudgetScore { get; set; }
        public decimal AvailabilityScore { get; set; }
        public decimal CompletionScore { get; set; }
        public decimal DisputePenalty { get; set; }
        public decimal DisputeRate { get; set; }
        public decimal OverdueRate { get; set; }
        public decimal OverduePenalty { get; set; }
        public string? Explanation { get; set; }
        public decimal Rating { get; set; }
        public int CompletedProjects { get; set; }
    }
}
