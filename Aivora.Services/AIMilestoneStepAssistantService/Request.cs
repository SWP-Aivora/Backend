namespace Aivora.Services.AIMilestoneStepAssistantService;

public class Request
{
    public class SuggestMilestoneStepsRequest
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string? AcceptanceCriteria { get; set; }
    }
}
