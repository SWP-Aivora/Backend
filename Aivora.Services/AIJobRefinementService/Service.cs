using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.AIJobAssistantService.Parsing;
using Aivora.Services.Constants;
using Aivora.Services.Exceptions;
using Aivora.Services.Extensions;
using Aivora.Services.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aivora.Services.AIJobRefinementService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;
    private readonly JobService.IService _jobService;
    private readonly IAIJobRefinementProvider _refinementProvider;
    private readonly IOptions<ExchangeRateOptions> _exchangeRates;

    public Service(
        AivoraDbContext dbContext,
        JobService.IService jobService,
        IAIJobRefinementProvider refinementProvider,
        IOptions<ExchangeRateOptions> exchangeRates)
    {
        _dbContext = dbContext;
        _jobService = jobService;
        _refinementProvider = refinementProvider;
        _exchangeRates = exchangeRates;
    }

    public async Task<Response.JobRefinementResponse> RefineJobAsync(Guid clientId, Guid jobId, Request.RefineJobRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ValidationException("Request body is required.");
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Trim().Length < 3)
        {
            throw new ValidationException("Message must be at least 3 characters long.");
        }

        var job = await _dbContext.JobPosts
            .Include(j => j.Client)
            .Include(j => j.Category)
            .IncludeSkills()
            .Include(j => j.Milestones)
            .FirstOrDefaultAsync(j => j.Id == jobId && j.ClientId == clientId, cancellationToken);

        if (job == null) throw new NotFoundException("Job not found or access denied.");
        if (job.Status != JobStatus.DRAFT && job.Status != JobStatus.OPEN)
            throw new ValidationException("Job cannot be refined in its current status.");

        var current = MapToJobResponse(job);
        var draft = await _refinementProvider.RefineJobAsync(current, request.Message.Trim(), cancellationToken);

        // The AI's own claim of "what changed" isn't trustworthy (JSON shape/completeness
        // varies run to run) — diff the parsed draft against the entity ourselves and only
        // write/report fields that actually differ.
        var changedFields = ApplyDraft(_dbContext, job, draft, _exchangeRates.Value.ToAicoin);

        if (changedFields.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var updated = await _jobService.GetJobByIdAsync(job.Id);

        return new Response.JobRefinementResponse
        {
            Job = updated,
            AIResponse = draft.AIResponse,
            ChangedFields = changedFields
        };
    }

    private static List<string> ApplyDraft(AivoraDbContext dbContext, JobPost job, AIJobRefinementDraft draft, IReadOnlyDictionary<string, decimal> ratesToAicoin)
    {
        var changed = new List<string>();

        // Hallucinated/unsupported currency code from the AI (typo, or a currency outside the
        // configured rate table): treat as "no currency change requested" rather than throwing
        // on an advisory-only chat message.
        var requestedCurrency = draft.Currency;
        if (!string.IsNullOrWhiteSpace(requestedCurrency)
            && requestedCurrency != CurrencyConverter.BaseCurrency
            && !ratesToAicoin.ContainsKey(requestedCurrency))
        {
            requestedCurrency = job.Currency;
        }

        var (aicoinCurrency, aicoinBudgetMin, aicoinBudgetMax, aicoinMilestones) = CurrencyConverter.ConvertToAicoin(
            requestedCurrency, draft.BudgetMin, draft.BudgetMax, draft.Milestones, ratesToAicoin);

        if (!string.IsNullOrWhiteSpace(draft.Title))
        {
            var title = draft.Title.Length <= 255 ? draft.Title : draft.Title[..255];
            if (title != job.Title)
            {
                job.Title = title;
                changed.Add("title");
            }
        }

        if (draft.FinalDescription != job.FinalDescription)
        {
            job.FinalDescription = draft.FinalDescription;
            changed.Add("finalDescription");
        }

        var businessDomain = draft.BusinessDomain?.Length > 100 ? draft.BusinessDomain[..100] : draft.BusinessDomain;
        if (businessDomain != job.BusinessDomain)
        {
            job.BusinessDomain = businessDomain;
            changed.Add("businessDomain");
        }

        var expectedOutcome = draft.ExpectedOutcome?.Length > 2000 ? draft.ExpectedOutcome[..2000] : draft.ExpectedOutcome;
        if (expectedOutcome != job.ExpectedOutcome)
        {
            job.ExpectedOutcome = expectedOutcome;
            changed.Add("expectedOutcome");
        }

        if (draft.BudgetType.HasValue && draft.BudgetType.Value != job.BudgetType)
        {
            job.BudgetType = draft.BudgetType.Value;
            changed.Add("budgetType");
        }

        if (aicoinCurrency != job.Currency)
        {
            job.Currency = aicoinCurrency;
            changed.Add("currency");
        }

        if (aicoinBudgetMin.HasValue && aicoinBudgetMin.Value != job.BudgetMin)
        {
            job.BudgetMin = aicoinBudgetMin.Value;
            changed.Add("budgetMin");
        }

        if (aicoinBudgetMax.HasValue && aicoinBudgetMax.Value != job.BudgetMax)
        {
            job.BudgetMax = aicoinBudgetMax.Value;
            changed.Add("budgetMax");
        }

        if (draft.TimelineDays.HasValue)
        {
            var timelineDays = Math.Clamp(draft.TimelineDays.Value, ValidationLimits.MinDurationDays, ValidationLimits.MaxDurationDays);
            if (timelineDays != job.TimelineDays)
            {
                job.TimelineDays = timelineDays;
                changed.Add("timelineDays");
            }
        }

        if (draft.ExperienceLevel.HasValue && draft.ExperienceLevel.Value != job.ExperienceLevel)
        {
            job.ExperienceLevel = draft.ExperienceLevel.Value;
            changed.Add("experienceLevel");
        }

        // Empty list from the AI means "not mentioned", not "clear everything" — ApplyDraft
        // previously guarded on Count > 0 for the same reason; an empty draft list must never
        // be reported as a change we can't actually apply.
        if (draft.Skills.Count > 0)
        {
            var skillNames = draft.Skills
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            // Lookup existing skills by name — never create new Skill entities
            // (skill creation is a separate concern handled by Skill API)
            var existingSkills = dbContext.Skills
                .Where(s => skillNames.Contains(s.Name))
                .ToList();

            var currentSkillIds = job.JobSkills.Select(js => js.SkillId).ToHashSet();
            var newSkillIds = existingSkills.Select(s => s.Id).ToHashSet();

            if (!newSkillIds.SetEquals(currentSkillIds))
            {
                foreach (var old in job.JobSkills.ToList())
                {
                    job.JobSkills.Remove(old);
                }
                foreach (var skill in existingSkills)
                {
                    job.JobSkills.Add(new JobSkill { SkillId = skill.Id });
                }
                changed.Add("skills");
            }
        }

        if (aicoinMilestones.Count > 0 && !MilestonesEqual(job.Milestones, aicoinMilestones))
        {
            var oldMilestones = dbContext.JobPostMilestones
                .Where(m => m.JobPostId == job.Id)
                .ToList();
            dbContext.JobPostMilestones.RemoveRange(oldMilestones);
            var newMilestones = aicoinMilestones.Select((m, index) => new JobPostMilestone
            {
                JobPostId = job.Id,
                Title = m.Title,
                Description = m.Description,
                Amount = Math.Max(m.Amount, 1),
                DueDays = Math.Clamp(m.DueDays, ValidationLimits.MinDurationDays, ValidationLimits.MaxDurationDays),
                AcceptanceCriteria = m.AcceptanceCriteria,
                OrderIndex = index
            }).ToList();
            dbContext.JobPostMilestones.AddRange(newMilestones);
            changed.Add("milestones");
        }

        return changed;
    }

    private static bool MilestonesEqual(ICollection<JobPostMilestone> current, List<AIJobAssistantService.Response.SuggestedMilestone> proposed)
    {
        var ordered = current.OrderBy(m => m.OrderIndex).ToList();
        if (ordered.Count != proposed.Count) return false;

        for (var i = 0; i < ordered.Count; i++)
        {
            var a = ordered[i];
            var b = proposed[i];
            if (a.Title != b.Title
                || a.Description != b.Description
                || a.Amount != Math.Max(b.Amount, 1)
                || a.DueDays != Math.Clamp(b.DueDays, ValidationLimits.MinDurationDays, ValidationLimits.MaxDurationDays)
                || a.AcceptanceCriteria != b.AcceptanceCriteria)
            {
                return false;
            }
        }

        return true;
    }

    private static JobService.Response.JobResponse MapToJobResponse(JobPost job)
    {
        return new JobService.Response.JobResponse
        {
            Id = job.Id,
            Title = job.Title,
            OriginalDescription = job.OriginalDescription,
            FinalDescription = job.FinalDescription,
            BusinessDomain = job.BusinessDomain,
            ExpectedOutcome = job.ExpectedOutcome,
            ClientId = job.ClientId,
            ClientName = job.Client?.FullName ?? "Unknown",
            CategoryId = job.CategoryId,
            CategoryName = job.Category?.Name,
            BudgetType = job.BudgetType,
            BudgetMin = job.BudgetMin,
            BudgetMax = job.BudgetMax,
            Currency = job.Currency,
            TimelineDays = job.TimelineDays,
            Deadline = job.Deadline,
            ExperienceLevel = job.ExperienceLevel,
            Status = job.Status,
            Visibility = job.Visibility,
            CreatedAt = job.CreatedAt,
            PublishedAt = job.PublishedAt,
            Skills = job.JobSkills.Select(js => new JobService.Response.SkillInfo
            {
                Id = js.SkillId,
                Name = js.Skill?.Name ?? "Unknown"
            }).ToList(),
            Milestones = job.Milestones.Select(m => new JobService.Response.JobMilestoneResponse
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                Amount = m.Amount,
                DueDays = m.DueDays,
                AcceptanceCriteria = m.AcceptanceCriteria,
                OrderIndex = m.OrderIndex
            }).OrderBy(m => m.OrderIndex).ToList()
        };
    }
}
