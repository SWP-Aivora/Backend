namespace Aivora.Services.AIJobAssistantService;

public interface IAIJobSuggestionProvider
{
    Task<AIJobSuggestionDraft> GenerateSuggestionAsync(Request.GenerateSuggestionRequest request, CancellationToken cancellationToken = default);
}
