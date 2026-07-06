using Aivora.Repositories.Abstractions;
namespace Aivora.Repositories.Entities;

public class RecommendationResult : AuditableBaseEntity
{
    public Guid JobId { get; set; }
    public Guid ExpertId { get; set; }
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

    // Navigation Properties
    public virtual JobPost Job { get; set; } = null!;
    public virtual User Expert { get; set; } = null!;
}

