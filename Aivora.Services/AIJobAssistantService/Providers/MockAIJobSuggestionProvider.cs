using Aivora.Repositories.Enums;
using Aivora.Services.AIJobAssistantService.Parsing;

namespace Aivora.Services.AIJobAssistantService.Providers;

public class MockAIJobSuggestionProvider : IAIJobSuggestionProvider
{
    public Task<AIJobSuggestionDraft> GenerateSuggestionAsync(Request.GenerateSuggestionRequest request, CancellationToken cancellationToken = default)
    {
        var questions = new List<string>
        {
            "Do you have specific API preferences?",
            "What is the expected load?"
        };

        var draft = new AIJobSuggestionDraft
        {
            SuggestedTitle = BuildTitle(request.RawInput),
            SuggestedDescription = $"This is an AI enhanced description for: {request.RawInput}",
            BusinessDomain = request.BusinessDomain ?? "Software Development",
            ExpectedOutcome = request.ExpectedOutcome ?? "Deliver a working solution that meets the core business workflow.",
            BudgetType = request.BudgetType ?? BudgetType.FIXED,
            Currency = AIJsonParser.NormalizeCurrency(request.Currency),
            SuggestedBudgetMin = request.BudgetMin ?? 500,
            SuggestedBudgetMax = request.BudgetMax ?? 1500,
            SuggestedTimelineDays = request.TimelineDays ?? 14,
            ExperienceLevel = request.ExperienceLevel ?? SkillLevel.INTERMEDIATE,
            SuggestedSkills = new List<string> { "AI Chatbot", "Prompt Engineering" },
            SuggestedMilestones = new List<Response.SuggestedMilestone>
            {
                new() { Title = "Initial Design", Amount = 200, DueDays = 3, Description = "Design the AI flow" },
                new() { Title = "Implementation", Amount = 800, DueDays = 11, Description = "Full development" }
            },
            ClarifyingQuestions = questions,
            ClarifyingAnswers = questions.Select(_ => string.Empty).ToList(),
            RiskWarnings = new List<string> { "API costs may vary", "Complexity of UI might affect timeline" },
            AIModel = "Aivora-Mock"
        };

        return Task.FromResult(draft);
    }

    private static string BuildTitle(string rawInput)
    {
        var prefix = rawInput[..Math.Min(30, rawInput.Length)];
        return $"AI Enhanced: {prefix}...";
    }
}
