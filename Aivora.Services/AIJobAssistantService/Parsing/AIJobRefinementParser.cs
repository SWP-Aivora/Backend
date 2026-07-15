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

        var changedFields = AIJsonParser.ReadStringList(root, "changedFields");
        var draft = changedFields.Count == 0
            ? fallback
            : AIJsonParser.ParseSuggestionDraft(suggestionElement, fallback, logger);

        draft.AIModel = current.AIModel ?? fallback.AIModel;

        return new AIJobRefinementDraft
        {
            Suggestion = draft,
            AIResponse = AIJsonParser.GetString(root, "aiResponse") ?? "I reviewed the current job suggestion.",
            ChangedFields = changedFields
        };
    }
}
