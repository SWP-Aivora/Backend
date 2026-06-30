using Aivora.Repositories.Enums;

namespace Aivora.Services.AIJobRefinementService;

public class AIJobRefinementDraft
{
    public string? Title { get; set; }
    public string? FinalDescription { get; set; }
    public string? BusinessDomain { get; set; }
    public string? ExpectedOutcome { get; set; }
    public BudgetType? BudgetType { get; set; }
    public string? Currency { get; set; }
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public int? TimelineDays { get; set; }
    public SkillLevel? ExperienceLevel { get; set; }
    public List<string> Skills { get; set; } = new();
    public List<AIJobAssistantService.Response.SuggestedMilestone> Milestones { get; set; } = new();
    public string AIResponse { get; set; } = null!;
    public List<string> ChangedFields { get; set; } = new();
}
