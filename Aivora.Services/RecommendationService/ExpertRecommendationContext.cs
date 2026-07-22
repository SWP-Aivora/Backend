namespace Aivora.Services.RecommendationService;

public class ExpertRecommendationContext
{
    public string JobTitle { get; set; } = null!;
    public string JobDescription { get; set; } = null!;
    public List<string> RequiredSkills { get; set; } = new();
    public string BudgetType { get; set; } = null!;
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public List<CandidateExpert> Candidates { get; set; } = new();
}

// Snapshot cua 1 expert candidate + diem scorer da tinh, dua vao prompt de AI re-rank.
public class CandidateExpert
{
    public Guid ExpertId { get; set; }
    public List<string> Skills { get; set; } = new();
    public decimal Rating { get; set; }
    public decimal? HourlyRate { get; set; }
    public string AvailabilityStatus { get; set; } = null!;
    public decimal SuccessRate { get; set; }
    public int CompletedProjects { get; set; }
    public int DisputeCount { get; set; }
    public decimal OverdueRate { get; set; }
    public decimal ScorerTotalScore { get; set; }
    public string ScorerExplanation { get; set; } = null!;
}
