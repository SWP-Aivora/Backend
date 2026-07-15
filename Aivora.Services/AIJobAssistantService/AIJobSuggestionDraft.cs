using Aivora.Repositories.Enums;

namespace Aivora.Services.AIJobAssistantService;

public class AIJobSuggestionDraft
{
    public string SuggestedTitle { get; set; } = null!;
    public string SuggestedDescription { get; set; } = null!;
    public string? BusinessDomain { get; set; }
    public string? ExpectedOutcome { get; set; }
    public BudgetType BudgetType { get; set; } = BudgetType.FIXED;
    public string Currency { get; set; } = "AICOIN";
    public string? CategoryName { get; set; }
    public decimal? SuggestedBudgetMin { get; set; }
    public decimal? SuggestedBudgetMax { get; set; }
    public int? SuggestedTimelineDays { get; set; }
    public SkillLevel? ExperienceLevel { get; set; }
    public List<string> SuggestedSkills { get; set; } = new();
    public List<Response.SuggestedMilestone> SuggestedMilestones { get; set; } = new();
    public List<string> ClarifyingQuestions { get; set; } = new();
    public List<string> ClarifyingAnswers { get; set; } = new();
    public List<string> RiskWarnings { get; set; } = new();
    public string AIModel { get; set; } = "Aivora-Mock";

    public static AIJobSuggestionDraft FromResponse(Response.SuggestionResponse current)
    {
        return new AIJobSuggestionDraft
        {
            SuggestedTitle = current.SuggestedTitle ?? "New Job from AI",
            SuggestedDescription = current.SuggestedDescription ?? current.RawInput,
            BusinessDomain = current.BusinessDomain,
            ExpectedOutcome = current.ExpectedOutcome,
            BudgetType = current.BudgetType,
            Currency = current.Currency,
            SuggestedBudgetMin = current.SuggestedBudgetMin,
            SuggestedBudgetMax = current.SuggestedBudgetMax,
            SuggestedTimelineDays = current.SuggestedTimelineDays,
            ExperienceLevel = current.ExperienceLevel,
            SuggestedSkills = current.SuggestedSkills.ToList(),
            SuggestedMilestones = current.SuggestedMilestones.ToList(),
            ClarifyingQuestions = current.ClarifyingQuestions.ToList(),
            ClarifyingAnswers = current.ClarifyingAnswers.ToList(),
            RiskWarnings = current.RiskWarnings.ToList(),
            AIModel = current.AIModel ?? "Aivora-Mock"
        };
    }
}
