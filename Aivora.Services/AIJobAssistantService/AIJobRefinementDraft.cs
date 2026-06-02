namespace Aivora.Services.AIJobAssistantService;

public class AIJobRefinementDraft
{
    public AIJobSuggestionDraft Suggestion { get; set; } = null!;
    public string AIResponse { get; set; } = null!;
    public List<string> ChangedFields { get; set; } = new();
}
