namespace Aivora.Services.AIMilestoneStepAssistantService;

public class Response
{
    public class SuggestedStep
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public int EstimatedDays { get; set; }
    }
}
