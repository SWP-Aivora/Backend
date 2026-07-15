namespace Aivora.Services.AIJobAssistantService;

public interface IAIJobRefinementProvider
{
    Task<AIJobRefinementDraft> RefineSuggestionAsync(Response.SuggestionResponse current, Request.RefineSuggestionRequest request, CancellationToken cancellationToken = default);
}
