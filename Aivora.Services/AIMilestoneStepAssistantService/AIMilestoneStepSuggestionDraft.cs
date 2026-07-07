namespace Aivora.Services.AIMilestoneStepAssistantService;

public class AIMilestoneStepSuggestionDraft
{
    public List<Response.SuggestedStep> Steps { get; set; } = new();
    public string AIModel { get; set; } = "Aivora-Mock";
}
