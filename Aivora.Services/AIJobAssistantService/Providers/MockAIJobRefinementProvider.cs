using System.Globalization;
using System.Text.RegularExpressions;
using Aivora.Repositories.Enums;
using Aivora.Services.AIJobAssistantService.Parsing;

namespace Aivora.Services.AIJobAssistantService.Providers;

public class MockAIJobRefinementProvider : IAIJobRefinementProvider
{
    public Task<AIJobRefinementDraft> RefineSuggestionAsync(Response.SuggestionResponse current, Request.RefineSuggestionRequest request, CancellationToken cancellationToken = default)
    {
        var trimmedMessage = request.Message.Trim();
        var lower = trimmedMessage.ToLowerInvariant();
        var updated = AIJobSuggestionDraft.FromResponse(current);
        var changedFields = new List<string>();

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
                changedFields.Add("clarifyingAnswers");
                return Task.FromResult(BuildResult(updated, "I saved your clarifying answer.", changedFields));
            }
        }

        if (IsAdvisory(lower))
        {
            return Task.FromResult(new AIJobRefinementDraft
            {
                Suggestion = updated,
                AIResponse = BuildAdvisoryResponse(current),
                ChangedFields = new List<string>()
            });
        }

        if (TryApplyBudget(lower, updated, changedFields))
        {
            return Task.FromResult(BuildResult(updated, "I updated the suggested budget.", changedFields));
        }

        if (TryApplyTimeline(lower, updated, changedFields))
        {
            return Task.FromResult(BuildResult(updated, "I updated the suggested timeline.", changedFields));
        }

        if (TryApplyExperience(lower, updated, changedFields))
        {
            return Task.FromResult(BuildResult(updated, "I updated the suggested experience level.", changedFields));
        }

        if (TryApplyBudgetType(lower, updated, changedFields))
        {
            return Task.FromResult(BuildResult(updated, "I updated the budget type.", changedFields));
        }

        if (TryApplyCurrency(lower, updated, changedFields))
        {
            return Task.FromResult(BuildResult(updated, "I updated the currency.", changedFields));
        }

        if (TryApplySkill(trimmedMessage, updated, changedFields))
        {
            return Task.FromResult(BuildResult(updated, "I added the requested skill.", changedFields));
        }

        return Task.FromResult(new AIJobRefinementDraft
        {
            Suggestion = updated,
            AIResponse = "I noted the request. Please specify which job field you want to change.",
            ChangedFields = new List<string>()
        });
    }

    private static AIJobRefinementDraft BuildResult(AIJobSuggestionDraft updated, string aiResponse, List<string> changedFields)
    {
        return new AIJobRefinementDraft
        {
            Suggestion = updated,
            AIResponse = aiResponse,
            ChangedFields = changedFields.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
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

    private static bool TryApplyBudget(string lower, AIJobSuggestionDraft updated, List<string> changedFields)
    {
        var budget = MockRefineHelpers.TryParseBudget(lower);
        if (budget is null)
        {
            return false;
        }

        updated.SuggestedBudgetMin = budget.Value.Min;
        updated.SuggestedBudgetMax = budget.Value.Max;
        changedFields.AddRange(new[] { "suggestedBudgetMin", "suggestedBudgetMax" });
        return true;
    }

    private static bool TryApplyTimeline(string lower, AIJobSuggestionDraft updated, List<string> changedFields)
    {
        var days = MockRefineHelpers.TryParseTimelineDays(lower);
        if (days is null)
        {
            return false;
        }

        updated.SuggestedTimelineDays = days;
        changedFields.Add("suggestedTimelineDays");
        return true;
    }

    private static bool TryApplyExperience(string lower, AIJobSuggestionDraft updated, List<string> changedFields)
    {
        var level = MockRefineHelpers.TryParseExperienceLevel(lower);
        if (level is null)
        {
            return false;
        }

        updated.ExperienceLevel = level;
        changedFields.Add("experienceLevel");
        return true;
    }

    private static bool TryApplyBudgetType(string lower, AIJobSuggestionDraft updated, List<string> changedFields)
    {
        var budgetType = MockRefineHelpers.TryParseBudgetType(lower);
        if (budgetType is null)
        {
            return false;
        }

        updated.BudgetType = budgetType.Value;
        changedFields.Add("budgetType");
        return true;
    }

    private static bool TryApplyCurrency(string lower, AIJobSuggestionDraft updated, List<string> changedFields)
    {
        var currency = MockRefineHelpers.TryParseCurrency(lower);
        if (currency is null)
        {
            return false;
        }

        updated.Currency = currency;
        changedFields.Add("currency");
        return true;
    }

    private static bool TryApplySkill(string message, AIJobSuggestionDraft updated, List<string> changedFields)
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

        changedFields.Add("suggestedSkills");
        return true;
    }

    private static string BuildAdvisoryResponse(Response.SuggestionResponse current)
    {
        return $"For \"{current.SuggestedTitle ?? "this job"}\", review the budget, timeline, required skills, and expected outcome before publishing.";
    }
}
