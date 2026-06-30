using System.Text.Json;
using Aivora.Repositories.Enums;
using Aivora.Services.AIJobAssistantService;
using Aivora.Services.AIJobAssistantService.Parsing;

namespace Aivora.Services.AIJobRefinementService.Parsing;

public class AIJobRefinementParser
{
    public AIJobRefinementDraft Parse(string providerText, JobService.Response.JobResponse current)
    {
        using var document = AIJsonParser.ParseObject(providerText);
        var root = document.RootElement;
        var fallback = DraftFromJobResponse(current);

        var changedFields = AIJsonParser.ReadStringList(root, "changedFields");
        var jobElement = AIJsonParser.TryGetProperty(root, "updatedJob", out var updatedJob)
            ? updatedJob
            : root;

        var draft = changedFields.Count == 0
            ? fallback
            : ParseJobDraft(jobElement, fallback);

        return new AIJobRefinementDraft
        {
            Title = draft.Title,
            FinalDescription = draft.FinalDescription,
            BusinessDomain = draft.BusinessDomain,
            ExpectedOutcome = draft.ExpectedOutcome,
            BudgetType = draft.BudgetType,
            Currency = draft.Currency,
            BudgetMin = draft.BudgetMin,
            BudgetMax = draft.BudgetMax,
            TimelineDays = draft.TimelineDays,
            ExperienceLevel = draft.ExperienceLevel,
            Skills = draft.Skills,
            Milestones = draft.Milestones,
            AIResponse = AIJsonParser.GetString(root, "aiResponse") ?? "I reviewed the current job post.",
            ChangedFields = changedFields
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

    private static AIJobRefinementDraft ParseJobDraft(JsonElement element, AIJobRefinementDraft fallback)
    {
        return new AIJobRefinementDraft
        {
            Title = AIJsonParser.GetString(element, "title") ?? fallback.Title,
            FinalDescription = AIJsonParser.GetString(element, "finalDescription") ?? fallback.FinalDescription,
            BusinessDomain = AIJsonParser.GetString(element, "businessDomain") ?? fallback.BusinessDomain,
            ExpectedOutcome = AIJsonParser.GetString(element, "expectedOutcome") ?? fallback.ExpectedOutcome,
            BudgetType = AIJsonParser.GetBudgetType(element) ?? fallback.BudgetType,
            Currency = AIJsonParser.NormalizeCurrency(AIJsonParser.GetString(element, "currency") ?? fallback.Currency ?? "AICOIN"),
            BudgetMin = AIJsonParser.GetDecimal(element, "budgetMin") ?? fallback.BudgetMin,
            BudgetMax = AIJsonParser.GetDecimal(element, "budgetMax") ?? fallback.BudgetMax,
            TimelineDays = AIJsonParser.GetInt(element, "timelineDays") ?? fallback.TimelineDays,
            ExperienceLevel = AIJsonParser.GetSkillLevel(element) ?? fallback.ExperienceLevel,
            Skills = AIJsonParser.ReadStringList(element, "skills", fallback.Skills),
            Milestones = AIJsonParser.ReadMilestones(element, fallback.Milestones),
            AIResponse = fallback.AIResponse,
            ChangedFields = fallback.ChangedFields
        };
    }
}
