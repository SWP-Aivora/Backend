using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.AIJobAssistantService.Parsing;
using Aivora.Services.Exceptions;
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
            .Include(j => j.JobSkills).ThenInclude(js => js.Skill)
            .Include(j => j.Milestones)
            .FirstOrDefaultAsync(j => j.Id == jobId && j.ClientId == clientId, cancellationToken);

        if (job == null) throw new NotFoundException("Job not found or access denied.");
        if (job.Status != JobStatus.DRAFT && job.Status != JobStatus.OPEN)
            throw new ValidationException("Job cannot be refined in its current status.");

        var current = MapToJobResponse(job);
        var draft = await _refinementProvider.RefineJobAsync(current, request.Message.Trim(), cancellationToken);

        if (draft.ChangedFields.Count > 0)
        {
            ApplyDraft(_dbContext, job, draft, _exchangeRates.Value.ToAicoin);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var updated = await _jobService.GetJobByIdAsync(job.Id);

        return new Response.JobRefinementResponse
        {
            Job = updated,
            AIResponse = draft.AIResponse,
            ChangedFields = draft.ChangedFields
        };
    }

    private static void ApplyDraft(AivoraDbContext dbContext, JobPost job, AIJobRefinementDraft draft, IReadOnlyDictionary<string, decimal> ratesToAicoin)
    {
        var (aicoinCurrency, aicoinBudgetMin, aicoinBudgetMax, aicoinMilestones) = CurrencyConverter.ConvertToAicoin(
            draft.Currency, draft.BudgetMin, draft.BudgetMax, draft.Milestones, ratesToAicoin);

        foreach (var field in draft.ChangedFields)
        {
            switch (field)
            {
                case "title":
                    if (!string.IsNullOrWhiteSpace(draft.Title))
                        job.Title = draft.Title.Length <= 255 ? draft.Title : draft.Title[..255];
                    break;
                case "finalDescription":
                    job.FinalDescription = draft.FinalDescription;
                    break;
                case "businessDomain":
                    job.BusinessDomain = draft.BusinessDomain?.Length > 100
                        ? draft.BusinessDomain[..100]
                        : draft.BusinessDomain;
                    break;
                case "expectedOutcome":
                    job.ExpectedOutcome = draft.ExpectedOutcome?.Length > 2000
                        ? draft.ExpectedOutcome[..2000]
                        : draft.ExpectedOutcome;
                    break;
                case "budgetType":
                    if (draft.BudgetType.HasValue)
                        job.BudgetType = draft.BudgetType.Value;
                    break;
                case "currency":
                    if (!string.IsNullOrWhiteSpace(draft.Currency))
                        job.Currency = aicoinCurrency;
                    break;
                case "budgetMin":
                case "budgetMax":
                    if (aicoinBudgetMin.HasValue) job.BudgetMin = aicoinBudgetMin.Value;
                    if (aicoinBudgetMax.HasValue) job.BudgetMax = aicoinBudgetMax.Value;
                    break;
                case "timelineDays":
                    if (draft.TimelineDays.HasValue)
                        job.TimelineDays = Math.Clamp(draft.TimelineDays.Value, 1, 3650);
                    break;
                case "experienceLevel":
                    if (draft.ExperienceLevel.HasValue)
                        job.ExperienceLevel = draft.ExperienceLevel.Value;
                    break;
                case "skills":
                    if (draft.Skills.Count > 0)
                    {
                        // Lookup existing skills by name — never create new Skill entities
                        // (skill creation is a separate concern handled by Skill API)
                        var skillNames = draft.Skills
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Select(s => s.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        var existingSkills = dbContext.Skills
                            .Where(s => skillNames.Contains(s.Name))
                            .ToList();
                        // Remove old JobSkill links
                        foreach (var old in job.JobSkills.ToList())
                        {
                            job.JobSkills.Remove(old);
                        }
                        // Link only to existing skills
                        foreach (var skill in existingSkills)
                        {
                            job.JobSkills.Add(new JobSkill { SkillId = skill.Id });
                        }
                    }
                    break;
                case "milestones":
                    if (aicoinMilestones.Count > 0)
                    {
                        // Delete old milestones via DbSet, then insert new ones
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
                            DueDays = Math.Clamp(m.DueDays, 1, 3650),
                            AcceptanceCriteria = m.AcceptanceCriteria,
                            OrderIndex = index
                        }).ToList();
                        dbContext.JobPostMilestones.AddRange(newMilestones);
                    }
                    break;
            }
        }
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
