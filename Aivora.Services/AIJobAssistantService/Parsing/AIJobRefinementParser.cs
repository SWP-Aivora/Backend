namespace Aivora.Services.AIJobAssistantService.Parsing;

public class AIJobRefinementParser
{
    public AIJobRefinementDraft Parse(string providerText, Response.SuggestionResponse current, Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        using var document = AIJsonParser.ParseObject(providerText);
        var root = document.RootElement;
        var fallback = AIJobSuggestionDraft.FromResponse(current);
        var suggestionElement = AIJsonParser.TryGetProperty(root, "updatedSuggestion", out var updatedSuggestion)
            ? updatedSuggestion
            : root;

        var draft = AIJsonParser.ParseSuggestionDraft(suggestionElement, fallback, logger);
        // This is real Gemini output — stamp it as such, same as the generation parser does.
        // Inheriting current.AIModel would keep showing "Aivora-Mock" (or a stale model name)
        // even though Gemini produced this specific refinement.
        draft.AIModel = "Gemini 2.5 Flash";

        return new AIJobRefinementDraft
        {
            Suggestion = draft,
            AIResponse = AIJsonParser.GetString(root, "aiResponse") ?? "I reviewed the current job suggestion."
        };
    }
}
