using Aivora.Repositories.Enums;

namespace Aivora.Services.AIJobAssistantService;

public class Request
{
    public class GenerateSuggestionRequest
    {
        public string RawInput { get; set; } = null!;
        public string? BusinessDomain { get; set; }
        public string? ExpectedOutcome { get; set; }
        public BudgetType? BudgetType { get; set; }
        public string? Currency { get; set; }
        public decimal? BudgetMin { get; set; }
        public decimal? BudgetMax { get; set; }
        public int? TimelineDays { get; set; }
        public SkillLevel? ExperienceLevel { get; set; }
    }

    public class AcceptSuggestionRequest
    {
        public Guid? CategoryId { get; set; }
        public List<Guid>? SelectedSkillIds { get; set; }
    }

    public class RejectSuggestionRequest
    {
        public string? Reason { get; set; }
    }

    public class PatchSuggestionRequest
    {
        public string? SuggestedTitle { get; set; }
        public string? SuggestedDescription { get; set; }
        public string? BusinessDomain { get; set; }
        public string? ExpectedOutcome { get; set; }
        public BudgetType? BudgetType { get; set; }
        public string? Currency { get; set; }
        public decimal? SuggestedBudgetMin { get; set; }
        public decimal? SuggestedBudgetMax { get; set; }
        public int? SuggestedTimelineDays { get; set; }
        public SkillLevel? ExperienceLevel { get; set; }
        public List<string>? SuggestedSkills { get; set; }
        public List<Response.SuggestedMilestone>? SuggestedMilestones { get; set; }
        public List<string>? ClarifyingAnswers { get; set; }
    }

    public class RefineSuggestionRequest
    {
        public string Message { get; set; } = null!;
    }

    public class GenerateServiceDescriptionRequest
    {
        public string RawInput { get; set; } = null!;
        public List<string> Skills { get; set; } = new();
        public decimal PriceFrom { get; set; }
        public int DeliveryDays { get; set; }
        public string Tone { get; set; } = "professional";
        public string TargetClient { get; set; } = "startup";
        public string Language { get; set; } = "vi";
    }
}
