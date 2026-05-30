namespace Aivora.Services.AIJobAssistantService;

public class Request
{
    public class GenerateSuggestionRequest
    {
        public string RawInput { get; set; } = null!;
        public string? BusinessDomain { get; set; }
        public string? ExpectedOutcome { get; set; }
        public decimal? BudgetMin { get; set; }
        public decimal? BudgetMax { get; set; }
        public int? TimelineDays { get; set; }
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
}
