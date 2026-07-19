using Aivora.Repositories.Enums;
using Aivora.Services.JobService;

namespace Aivora.Services.AIJobAssistantService;

public class Response
{
    public class SuggestionResponse
    {
        public Guid Id { get; set; }
        public Guid? JobId { get; set; }
        public Guid ClientId { get; set; }
        public string RawInput { get; set; } = null!;
        public string? SuggestedTitle { get; set; }
        public string? SuggestedDescription { get; set; }
        public string? BusinessDomain { get; set; }
        public string? ExpectedOutcome { get; set; }
        public BudgetType BudgetType { get; set; }
        public string Currency { get; set; } = "AICOIN";
        public Guid? CategoryId { get; set; }
        public decimal? SuggestedBudgetMin { get; set; }
        public decimal? SuggestedBudgetMax { get; set; }
        public int? SuggestedTimelineDays { get; set; }
        public SkillLevel? ExperienceLevel { get; set; }
        public List<string> SuggestedSkills { get; set; } = new();
        public List<SuggestedMilestone> SuggestedMilestones { get; set; } = new();
        public List<string> ClarifyingQuestions { get; set; } = new();
        public List<string> ClarifyingAnswers { get; set; } = new();
        public List<string> RiskWarnings { get; set; } = new();
        public string? AIModel { get; set; }
        public string Status { get; set; } = null!;
        public string? RejectionReason { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class SuggestedMilestone
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public int DueDays { get; set; }
        public string? AcceptanceCriteria { get; set; }
    }

    public class AcceptResultResponse
    {
        public Aivora.Services.JobService.Response.JobResponse Job { get; set; } = null!;
    }

    public class RefineSuggestionResponse
    {
        public SuggestionResponse Suggestion { get; set; } = null!;
        public string AIResponse { get; set; } = null!;
        public List<string> ChangedFields { get; set; } = new();
    }

    public class ServiceDescriptionResponse
    {
        public string SuggestedTitle { get; set; } = null!;
        public string SuggestedDescription { get; set; } = null!;
        public List<ServicePackageResponse> Packages { get; set; } = new();
        public List<ServiceFaqResponse> Faqs { get; set; } = new();
        public string Provider { get; set; } = null!;
    }

    public class ServicePackageResponse
    {
        public string Name { get; set; } = null!;
        public string? Title { get; set; }
        public decimal Price { get; set; }
        public int DeliveryDays { get; set; }
        public string Description { get; set; } = null!;
        public List<string> Features { get; set; } = new();
    }

    public class ServiceFaqResponse
    {
        public string Question { get; set; } = null!;
        public string Answer { get; set; } = null!;
    }
}
