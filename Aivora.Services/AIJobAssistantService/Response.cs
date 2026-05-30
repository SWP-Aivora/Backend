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
        public decimal? SuggestedBudgetMin { get; set; }
        public decimal? SuggestedBudgetMax { get; set; }
        public int? SuggestedTimelineDays { get; set; }
        public List<string> SuggestedSkills { get; set; } = new();
        public List<SuggestedMilestone> SuggestedMilestones { get; set; } = new();
        public List<string> ClarifyingQuestions { get; set; } = new();
        public List<string> RiskWarnings { get; set; } = new();
        public string? AIModel { get; set; }
        public string Status { get; set; } = null!;
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
}
