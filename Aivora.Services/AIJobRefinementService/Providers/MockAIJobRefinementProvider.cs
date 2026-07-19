using System.Globalization;
using System.Text.RegularExpressions;
using Aivora.Repositories.Enums;
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
        var changedFields = new List<string>();

        if (IsAdvisory(lower))
        {
            return Task.FromResult(new AIJobRefinementDraft
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
                AIResponse = $"For \"{current.Title}\", review the budget, timeline, required skills, and expected outcome before publishing.",
                ChangedFields = new List<string>()
            });
        }

        if (TryApplyBudget(lower, updated, changedFields))
        {
            return Task.FromResult(BuildResult(updated, "I updated the budget.", changedFields));
        }

        if (TryApplyTimeline(lower, updated, changedFields))
        {
            return Task.FromResult(BuildResult(updated, "I updated the timeline.", changedFields));
        }

        if (TryApplyExperience(lower, updated, changedFields))
        {
            return Task.FromResult(BuildResult(updated, "I updated the experience level.", changedFields));
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

        if (TryApplyTitle(trimmedMessage, updated, changedFields))
        {
            return Task.FromResult(BuildResult(updated, "I updated the job title.", changedFields));
        }

        if (TryApplyMilestones(trimmedMessage, updated, changedFields))
        {
            return Task.FromResult(BuildResult(updated, "I updated the milestones.", changedFields));
        }

        return Task.FromResult(new AIJobRefinementDraft
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
            AIResponse = "I noted the request. Please specify which job field you want to change (budget, timeline, skills, title, milestones, experience level, budget type, or currency).",
            ChangedFields = new List<string>()
        });
    }

    private static AIJobRefinementDraft BuildResult(AIJobRefinementDraft updated, string aiResponse, List<string> changedFields)
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
            AIResponse = aiResponse,
            ChangedFields = changedFields.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
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
            AIResponse = string.Empty,
            ChangedFields = new List<string>()
        };
    }

    private static bool IsAdvisory(string lower) => MockRefineHelpers.IsAdvisory(lower);

    private static bool TryApplyBudget(string lower, AIJobRefinementDraft updated, List<string> changedFields)
    {
        var budget = MockRefineHelpers.TryParseBudget(lower);
        if (budget is null) return false;

        updated.BudgetMin = budget.Value.Min;
        updated.BudgetMax = budget.Value.Max;
        changedFields.AddRange(new[] { "budgetMin", "budgetMax" });
        return true;
    }

    private static bool TryApplyTimeline(string lower, AIJobRefinementDraft updated, List<string> changedFields)
    {
        var days = MockRefineHelpers.TryParseTimelineDays(lower);
        if (days is null) return false;

        updated.TimelineDays = days;
        changedFields.Add("timelineDays");
        return true;
    }

    private static bool TryApplyExperience(string lower, AIJobRefinementDraft updated, List<string> changedFields)
    {
        var level = MockRefineHelpers.TryParseExperienceLevel(lower);
        if (level is null) return false;

        updated.ExperienceLevel = level;
        changedFields.Add("experienceLevel");
        return true;
    }

    private static bool TryApplyBudgetType(string lower, AIJobRefinementDraft updated, List<string> changedFields)
    {
        var budgetType = MockRefineHelpers.TryParseBudgetType(lower);
        if (budgetType is null) return false;

        updated.BudgetType = budgetType;
        changedFields.Add("budgetType");
        return true;
    }

    private static bool TryApplyCurrency(string lower, AIJobRefinementDraft updated, List<string> changedFields)
    {
        var currency = MockRefineHelpers.TryParseCurrency(lower);
        if (currency is null) return false;

        updated.Currency = currency;
        changedFields.Add("currency");
        return true;
    }

    private static bool TryApplySkill(string message, AIJobRefinementDraft updated, List<string> changedFields)
    {
        var skill = MockRefineHelpers.TryParseSkill(message);
        if (skill is null) return false;

        if (!updated.Skills.Any(s => string.Equals(s, skill, StringComparison.OrdinalIgnoreCase)))
            updated.Skills.Add(skill);

        changedFields.Add("skills");
        return true;
    }

    private static bool TryApplyTitle(string message, AIJobRefinementDraft updated, List<string> changedFields)
    {
        var match = Regex.Match(message, @"(?:title|rename|tieu de)\s*[:=\-]?\s*(.+)", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        var title = match.Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 255) return false;

        updated.Title = title;
        changedFields.Add("title");
        return true;
    }

    private static bool TryApplyMilestones(string message, AIJobRefinementDraft updated, List<string> changedFields)
    {
        if (!message.Contains("milestone", StringComparison.OrdinalIgnoreCase))
            return false;

        if (updated.Milestones.Count == 0) return false;

        updated.Milestones[0].Description = "Updated: " + message.Trim();
        changedFields.Add("milestones");
        return true;
    }
}
