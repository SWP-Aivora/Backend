using System.Globalization;
using System.Text.RegularExpressions;
using Aivora.Repositories.Enums;
using Aivora.Services.AIJobAssistantService;

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

    private static bool IsAdvisory(string lower)
    {
        var markers = new[] { "why", "what is", "what are", "should", "recommend", "explain", "compare", "advice", "tu van", "giai thich", "co nen" };
        return markers.Any(lower.Contains);
    }

    private static bool TryApplyBudget(string lower, AIJobRefinementDraft updated, List<string> changedFields)
    {
        if (!lower.Contains("budget") && !lower.Contains("ngan sach"))
            return false;

        var numbers = Regex.Matches(lower, @"\d+(?:\.\d+)?")
            .Select(m => decimal.Parse(m.Value, CultureInfo.InvariantCulture))
            .ToList();
        if (numbers.Count == 0) return false;

        updated.BudgetMin = numbers[0];
        updated.BudgetMax = numbers.Count > 1 ? numbers[1] : numbers[0];
        changedFields.AddRange(new[] { "budgetMin", "budgetMax" });
        return true;
    }

    private static bool TryApplyTimeline(string lower, AIJobRefinementDraft updated, List<string> changedFields)
    {
        if (!lower.Contains("day") && !lower.Contains("timeline") && !lower.Contains("ngay"))
            return false;

        var match = Regex.Match(lower, @"\d+");
        if (!match.Success) return false;

        updated.TimelineDays = int.Parse(match.Value, CultureInfo.InvariantCulture);
        changedFields.Add("timelineDays");
        return true;
    }

    private static bool TryApplyExperience(string lower, AIJobRefinementDraft updated, List<string> changedFields)
    {
        SkillLevel? level = null;
        if (lower.Contains("beginner") || lower.Contains("entry") || lower.Contains("junior")) level = SkillLevel.BEGINNER;
        if (lower.Contains("intermediate") || lower.Contains("middle")) level = SkillLevel.INTERMEDIATE;
        if (lower.Contains("advanced") || lower.Contains("senior")) level = SkillLevel.ADVANCED;
        if (lower.Contains("expert")) level = SkillLevel.EXPERT;
        if (level is null) return false;

        updated.ExperienceLevel = level;
        changedFields.Add("experienceLevel");
        return true;
    }

    private static bool TryApplyBudgetType(string lower, AIJobRefinementDraft updated, List<string> changedFields)
    {
        if (lower.Contains("hourly") || lower.Contains("theo gio"))
        {
            updated.BudgetType = BudgetType.HOURLY;
            changedFields.Add("budgetType");
            return true;
        }
        if (lower.Contains("fixed") || lower.Contains("tron goi"))
        {
            updated.BudgetType = BudgetType.FIXED;
            changedFields.Add("budgetType");
            return true;
        }
        return false;
    }

    private static bool TryApplyCurrency(string lower, AIJobRefinementDraft updated, List<string> changedFields)
    {
        string? currency = null;
        if (lower.Contains("vnd")) currency = "VND";
        if (lower.Contains("usd")) currency = "USD";
        if (lower.Contains("aicoin")) currency = "AICOIN";
        if (currency is null) return false;

        updated.Currency = currency;
        changedFields.Add("currency");
        return true;
    }

    private static bool TryApplySkill(string message, AIJobRefinementDraft updated, List<string> changedFields)
    {
        var match = Regex.Match(message, @"(?:add skill|them ky nang)\s*[:=\-]?\s*(.+)", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        var skill = match.Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(skill)) return false;

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
