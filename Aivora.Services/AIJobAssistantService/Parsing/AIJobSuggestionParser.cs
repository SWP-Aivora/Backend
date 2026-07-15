using Aivora.Repositories.Enums;

namespace Aivora.Services.AIJobAssistantService.Parsing;

public class AIJobSuggestionParser
{
    public AIJobSuggestionDraft Parse(string providerText, Request.GenerateSuggestionRequest request, Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        using var document = AIJsonParser.ParseObject(providerText);
        var fallback = new AIJobSuggestionDraft
        {
            SuggestedTitle = BuildFallbackTitle(request.RawInput),
            SuggestedDescription = $"This is an AI enhanced description for: {request.RawInput}",
            BusinessDomain = request.BusinessDomain,
            ExpectedOutcome = request.ExpectedOutcome,
            BudgetType = request.BudgetType ?? BudgetType.FIXED,
            Currency = AIJsonParser.NormalizeCurrency(request.Currency),
            SuggestedBudgetMin = request.BudgetMin ?? 500,
            SuggestedBudgetMax = request.BudgetMax ?? 1500,
            SuggestedTimelineDays = request.TimelineDays ?? 14,
            ExperienceLevel = request.ExperienceLevel ?? SkillLevel.INTERMEDIATE,
            SuggestedSkills = new List<string> { "AI Chatbot", "Prompt Engineering" },
            SuggestedMilestones = BuildFallbackMilestones(),
            ClarifyingQuestions = new List<string> { "Do you have specific API preferences?", "What is the expected load?" },
            RiskWarnings = new List<string> { "API costs may vary", "Complexity of UI might affect timeline" },
            AIModel = "Gemini 2.5 Flash"
        };

        var draft = AIJsonParser.ParseSuggestionDraft(document.RootElement, fallback, logger);
        draft.AIModel = "Gemini 2.5 Flash";
        return draft;
    }

    private static string BuildFallbackTitle(string rawInput)
    {
        var prefix = rawInput[..Math.Min(30, rawInput.Length)];
        return $"AI Enhanced: {prefix}...";
    }

    private static List<Response.SuggestedMilestone> BuildFallbackMilestones()
    {
        return new List<Response.SuggestedMilestone>
        {
            new() { Title = "Initial Design", Amount = 200, DueDays = 3, Description = "Design the AI flow" },
            new() { Title = "Implementation", Amount = 800, DueDays = 11, Description = "Full development" }
        };
    }
}
