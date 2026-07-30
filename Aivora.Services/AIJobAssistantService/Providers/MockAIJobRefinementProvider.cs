using System.Globalization;
using System.Text.RegularExpressions;
using Aivora.Services.AIJobAssistantService.Parsing;

namespace Aivora.Services.AIJobAssistantService.Providers;

public class MockAIJobRefinementProvider : IAIJobRefinementProvider
{
    public Task<AIJobRefinementDraft> RefineSuggestionAsync(Response.SuggestionResponse current, Request.RefineSuggestionRequest request, CancellationToken cancellationToken = default)
    {
        var trimmedMessage = request.Message.Trim();
        var lower = trimmedMessage.ToLowerInvariant();
        var updated = AIJobSuggestionDraft.FromResponse(current);
        updated.AIModel = "Aivora-Mock";

        while (updated.ClarifyingAnswers.Count < updated.ClarifyingQuestions.Count)
        {
            updated.ClarifyingAnswers.Add(string.Empty);
        }

        var clarifyingAnswer = ParseClarifyingAnswer(trimmedMessage);
        if (clarifyingAnswer is not null)
        {
            var (index, answer) = clarifyingAnswer.Value;
            if (index >= 0 && index < updated.ClarifyingQuestions.Count)
            {
                updated.ClarifyingAnswers[index] = answer;
                return Task.FromResult(BuildResult(updated, "I saved your clarifying answer."));
            }
        }

        if (IsAdvisory(lower))
        {
            return Task.FromResult(BuildResult(updated, BuildAdvisoryResponse(current)));
        }

        if (TryApplyBudget(lower, updated))
        {
            return Task.FromResult(BuildResult(updated, "I updated the suggested budget."));
        }

        if (TryApplyTimeline(lower, updated))
        {
            return Task.FromResult(BuildResult(updated, "I updated the suggested timeline."));
        }

        if (TryApplyExperience(lower, updated))
        {
            return Task.FromResult(BuildResult(updated, "I updated the suggested experience level."));
        }

        if (TryApplyBudgetType(lower, updated))
        {
            return Task.FromResult(BuildResult(updated, "I updated the budget type."));
        }

        if (TryApplyCurrency(lower, updated))
        {
            return Task.FromResult(BuildResult(updated, "I updated the currency."));
        }

        if (TryApplySkill(trimmedMessage, updated))
        {
            return Task.FromResult(BuildResult(updated, "I added the requested skill."));
        }

        return Task.FromResult(BuildResult(updated, "I noted the request. Please specify which job field you want to change."));
    }

    private static AIJobRefinementDraft BuildResult(AIJobSuggestionDraft updated, string aiResponse)
    {
        return new AIJobRefinementDraft
        {
            Suggestion = updated,
            AIResponse = aiResponse
        };
    }

    private static (int Index, string Answer)? ParseClarifyingAnswer(string message)
    {
        var match = Regex.Match(message, @"(?:question|cau hoi)\s*(\d+)\s*[:=\-]?\s*(.+)", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        var index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) - 1;
        var answer = match.Groups[2].Value.Trim();
        return string.IsNullOrWhiteSpace(answer) ? null : (index, answer);
    }

    private static bool IsAdvisory(string lower) => MockRefineHelpers.IsAdvisory(lower);

    private static bool TryApplyBudget(string lower, AIJobSuggestionDraft updated)
    {
        var budget = MockRefineHelpers.TryParseBudget(lower);
        if (budget is null)
        {
            return false;
        }

        updated.SuggestedBudgetMin = budget.Value.Min;
        updated.SuggestedBudgetMax = budget.Value.Max;
        return true;
    }

    private static bool TryApplyTimeline(string lower, AIJobSuggestionDraft updated)
    {
        var days = MockRefineHelpers.TryParseTimelineDays(lower);
        if (days is null)
        {
            return false;
        }

        updated.SuggestedTimelineDays = days;
        return true;
    }

    private static bool TryApplyExperience(string lower, AIJobSuggestionDraft updated)
    {
        var level = MockRefineHelpers.TryParseExperienceLevel(lower);
        if (level is null)
        {
            return false;
        }

        updated.ExperienceLevel = level;
        return true;
    }

    private static bool TryApplyBudgetType(string lower, AIJobSuggestionDraft updated)
    {
        var budgetType = MockRefineHelpers.TryParseBudgetType(lower);
        if (budgetType is null)
        {
            return false;
        }

        updated.BudgetType = budgetType.Value;
        return true;
    }

    private static bool TryApplyCurrency(string lower, AIJobSuggestionDraft updated)
    {
        var currency = MockRefineHelpers.TryParseCurrency(lower);
        if (currency is null)
        {
            return false;
        }

        updated.Currency = currency;
        return true;
    }

    private static bool TryApplySkill(string message, AIJobSuggestionDraft updated)
    {
        var skill = MockRefineHelpers.TryParseSkill(message);
        if (skill is null)
        {
            return false;
        }

        if (!updated.SuggestedSkills.Any(s => string.Equals(s, skill, StringComparison.OrdinalIgnoreCase)))
        {
            updated.SuggestedSkills.Add(skill);
        }

        return true;
    }

    private static string BuildAdvisoryResponse(Response.SuggestionResponse current)
    {
        return $"For \"{current.SuggestedTitle ?? "this job"}\", review the budget, timeline, required skills, and expected outcome before publishing.";
    }
}
