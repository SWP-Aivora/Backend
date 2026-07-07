namespace Aivora.Services.AIMilestoneStepAssistantService;

public interface IAIMilestoneStepSuggestionProvider
{
    Task<AIMilestoneStepSuggestionDraft> GenerateSuggestionAsync(Request.SuggestMilestoneStepsRequest request, CancellationToken cancellationToken = default);
}
