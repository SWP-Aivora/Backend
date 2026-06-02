namespace Aivora.Services.AIJobAssistantService;

public interface IAIJobRefinementProvider
{
    Task<AIJobRefinementDraft> RefineSuggestionAsync(Response.SuggestionResponse current, string message, CancellationToken cancellationToken = default);
}
