using System.Globalization;
using System.Text.RegularExpressions;
using Aivora.Services.AIJobAssistantService;
using Aivora.Services.AIJobAssistantService.Providers;

namespace Aivora.Services.AIJobRefinementService.Providers;

public class MockAIJobRefinementProvider : IAIJobRefinementProvider
{
    public Task<AIJobRefinementDraft> RefineJobAsync(JobService.Response.JobResponse current, string message, CancellationToken cancellationToken = default)
    {
        var trimmedMessage = message.Trim();
        var lower = trimmedMessage.ToLowerInvariant();
        var updated = DraftFromJobResponse(current);

        if (IsAdvisory(lower))
        {
            return Task.FromResult(BuildResult(updated, $"For \"{current.Title}\", review the budget, timeline, required skills, and expected outcome before publishing."));
        }

        if (TryApplyBudget(lower, updated))
        {
            return Task.FromResult(BuildResult(updated, "I updated the budget."));
        }

        if (TryApplyTimeline(lower, updated))
        {
            return Task.FromResult(BuildResult(updated, "I updated the timeline."));
        }

        if (TryApplyExperience(lower, updated))
        {
            return Task.FromResult(BuildResult(updated, "I updated the experience level."));
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

        if (TryApplyTitle(trimmedMessage, updated))
        {
            return Task.FromResult(BuildResult(updated, "I updated the job title."));
        }

        if (TryApplyMilestones(trimmedMessage, updated))
        {
            return Task.FromResult(BuildResult(updated, "I updated the milestones."));
        }

        return Task.FromResult(BuildResult(updated, "I noted the request. Please specify which job field you want to change (budget, timeline, skills, title, milestones, experience level, budget type, or currency)."));
    }

    private static AIJobRefinementDraft BuildResult(AIJobRefinementDraft updated, string aiResponse)
    {
        return new AIJobRefinementDraft
        {
            Title = updated.Title,
            FinalDescription = updated.FinalDescription,
            BusinessDomain = updated.BusinessDomain,
            ExpectedOutcome = updated.ExpectedOutcome,
            BudgetType = updated.BudgetType,
            Currency = updated.Currency,
            BudgetMin = updated.BudgetMin,
            BudgetMax = updated.BudgetMax,
            TimelineDays = updated.TimelineDays,
            ExperienceLevel = updated.ExperienceLevel,
            Skills = updated.Skills,
            Milestones = updated.Milestones,
            AIResponse = aiResponse
        };
    }

    private static AIJobRefinementDraft DraftFromJobResponse(JobService.Response.JobResponse current)
    {
        return new AIJobRefinementDraft
        {
            Title = current.Title,
            FinalDescription = current.FinalDescription,
            BusinessDomain = current.BusinessDomain,
            ExpectedOutcome = current.ExpectedOutcome,
            BudgetType = current.BudgetType,
            Currency = current.Currency,
            BudgetMin = current.BudgetMin,
            BudgetMax = current.BudgetMax,
            TimelineDays = current.TimelineDays,
            ExperienceLevel = current.ExperienceLevel,
            Skills = current.Skills.Select(s => s.Name).ToList(),
            Milestones = current.Milestones.Select(m => new AIJobAssistantService.Response.SuggestedMilestone
            {
                Title = m.Title,
                Description = m.Description,
                Amount = m.Amount,
                DueDays = m.DueDays,
                AcceptanceCriteria = m.AcceptanceCriteria
            }).ToList(),
            AIResponse = string.Empty
        };
    }

    private static bool IsAdvisory(string lower) => MockRefineHelpers.IsAdvisory(lower);

    private static bool TryApplyBudget(string lower, AIJobRefinementDraft updated)
    {
        var budget = MockRefineHelpers.TryParseBudget(lower);
        if (budget is null) return false;

        updated.BudgetMin = budget.Value.Min;
        updated.BudgetMax = budget.Value.Max;
        return true;
    }

    private static bool TryApplyTimeline(string lower, AIJobRefinementDraft updated)
    {
        var days = MockRefineHelpers.TryParseTimelineDays(lower);
        if (days is null) return false;

        updated.TimelineDays = days;
        return true;
    }

    private static bool TryApplyExperience(string lower, AIJobRefinementDraft updated)
    {
        var level = MockRefineHelpers.TryParseExperienceLevel(lower);
        if (level is null) return false;

        updated.ExperienceLevel = level;
        return true;
    }

    private static bool TryApplyBudgetType(string lower, AIJobRefinementDraft updated)
    {
        var budgetType = MockRefineHelpers.TryParseBudgetType(lower);
        if (budgetType is null) return false;

        updated.BudgetType = budgetType;
        return true;
    }

    private static bool TryApplyCurrency(string lower, AIJobRefinementDraft updated)
    {
        var currency = MockRefineHelpers.TryParseCurrency(lower);
        if (currency is null) return false;

        updated.Currency = currency;
        return true;
    }

    private static bool TryApplySkill(string message, AIJobRefinementDraft updated)
    {
        var skill = MockRefineHelpers.TryParseSkill(message);
        if (skill is null) return false;

        if (!updated.Skills.Any(s => string.Equals(s, skill, StringComparison.OrdinalIgnoreCase)))
            updated.Skills.Add(skill);

        return true;
    }

    private static bool TryApplyTitle(string message, AIJobRefinementDraft updated)
    {
        var match = Regex.Match(message, @"(?:title|rename|tieu de)\s*[:=\-]?\s*(.+)", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        var title = match.Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 255) return false;

        updated.Title = title;
        return true;
    }

    private static bool TryApplyMilestones(string message, AIJobRefinementDraft updated)
    {
        if (!message.Contains("milestone", StringComparison.OrdinalIgnoreCase))
            return false;

        if (updated.Milestones.Count == 0) return false;

        updated.Milestones[0].Description = "Updated: " + message.Trim();
        return true;
    }
}
