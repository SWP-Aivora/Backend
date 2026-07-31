using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Constants;
using Aivora.Services.Exceptions;
using Aivora.Services.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.JobService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;
    private readonly RealtimeService.IService _realtimeService;
    private readonly NotificationService.IService _notificationService;

    public Service(AivoraDbContext dbContext, RealtimeService.IService realtimeService, NotificationService.IService notificationService)
    {
        _dbContext = dbContext;
        _realtimeService = realtimeService;
        _notificationService = notificationService;
    }

    public async Task<Response.JobResponse> GetJobByIdAsync(Guid id)
    {
        var job = await _dbContext.JobPosts
            .Include(j => j.Client)
            .Include(j => j.Category)
            .IncludeSkills()
            .Include(j => j.Milestones)
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null) throw new NotFoundException("Job not found.");

        return MapToResponse(job);
    }

    public async Task<Response.JobResponse> CreateJobAsync(Guid clientId, Request.CreateJobRequest request)
    {
        if (request is null) throw new ValidationException("Request body is required.");

        if (request.CategoryId == Guid.Empty)
        {
            throw new ValidationException("CategoryId is required.");
        }

        ValidateJobFields(request.BudgetMin, request.BudgetMax, request.TimelineDays, request.Deadline);
        var skillIds = NormalizeGuidList(request.SkillIds);
        var milestones = NormalizeMilestones(request.Milestones);

        var job = new JobPost
        {
            ClientId = clientId,
            Title = NormalizeRequired(request.Title, "Untitled job", 255),
            OriginalDescription = request.OriginalDescription,
            FinalDescription = request.FinalDescription,
            BusinessDomain = NormalizeLimited(request.BusinessDomain, 100),
            ExpectedOutcome = NormalizeLimited(request.ExpectedOutcome, 2000),
            CategoryId = request.CategoryId,
            BudgetType = request.BudgetType,
            BudgetMin = request.BudgetMin,
            BudgetMax = request.BudgetMax,
            Currency = NormalizeCurrency(request.Currency),
            TimelineDays = request.TimelineDays,
            Deadline = request.Deadline,
            ExperienceLevel = request.ExperienceLevel,
            Visibility = request.Visibility,
            Status = JobStatus.DRAFT,
            Milestones = milestones.Select(m => new JobPostMilestone
            {
                Title = m.Title,
                Description = m.Description,
                Amount = m.Amount,
                DueDays = m.DueDays,
                AcceptanceCriteria = m.AcceptanceCriteria,
                OrderIndex = m.OrderIndex
            }).ToList()
        };

        if (skillIds.Any())
        {
            job.JobSkills = skillIds.Select(skillId => new JobSkill { SkillId = skillId }).ToList();
        }

        _dbContext.JobPosts.Add(job);
        await _dbContext.SaveChangesAsync();

        return await GetJobByIdAsync(job.Id);
    }

    public async Task<Response.JobResponse> UpdateJobAsync(Guid clientId, Guid jobId, Request.UpdateJobRequest request)
    {
        var job = await _dbContext.JobPosts
            .Include(j => j.JobSkills)
            .FirstOrDefaultAsync(j => j.Id == jobId && j.ClientId == clientId);

        if (job == null) throw new NotFoundException("Job not found or access denied.");
        if (job.Status != JobStatus.DRAFT && job.Status != JobStatus.OPEN)
            throw new ValidationException("Cannot update job in its current status.");

        if (request.Title != null) job.Title = NormalizeRequired(request.Title, job.Title, 255);
        if (request.FinalDescription != null) job.FinalDescription = request.FinalDescription;
        if (request.BusinessDomain != null) job.BusinessDomain = NormalizeLimited(request.BusinessDomain, 100);
        if (request.ExpectedOutcome != null) job.ExpectedOutcome = NormalizeLimited(request.ExpectedOutcome, 2000);
        if (request.CategoryId.HasValue)
        {
            if (request.CategoryId.Value == Guid.Empty)
            {
                throw new ValidationException("CategoryId is required.");
            }

            job.CategoryId = request.CategoryId.Value;
        }
        if (request.BudgetType.HasValue) job.BudgetType = request.BudgetType.Value;
        if (request.BudgetMin.HasValue) job.BudgetMin = request.BudgetMin.Value;
        if (request.BudgetMax.HasValue) job.BudgetMax = request.BudgetMax.Value;
        if (request.Currency != null && !string.IsNullOrWhiteSpace(request.Currency)) job.Currency = NormalizeCurrency(request.Currency);
        if (request.TimelineDays.HasValue) job.TimelineDays = request.TimelineDays.Value;
        if (request.Deadline.HasValue) job.Deadline = request.Deadline.Value;
        if (request.ExperienceLevel.HasValue) job.ExperienceLevel = request.ExperienceLevel.Value;
        if (request.Visibility.HasValue) job.Visibility = request.Visibility.Value;

        ValidateJobFields(job.BudgetMin, job.BudgetMax, job.TimelineDays, job.Deadline);

        if (request.SkillIds != null)
        {
            var targetSkillIds = NormalizeGuidList(request.SkillIds);
            var currentSkillIds = job.JobSkills.Select(js => js.SkillId).ToList();

            foreach (var skill in job.JobSkills.Where(js => !targetSkillIds.Contains(js.SkillId)).ToList())
            {
                _dbContext.JobSkills.Remove(skill);
            }

            var skillIdsToAdd = targetSkillIds.Except(currentSkillIds).ToList();
            if (skillIdsToAdd.Count > 0)
            {
                // Revive rows that were soft-deleted in a previous update instead of inserting a
                // duplicate (JobId, SkillId) row, which would violate the unique index.
                var revivable = await _dbContext.JobSkills
                    .IgnoreQueryFilters()
                    .Where(js => js.JobId == jobId && js.IsDeleted && skillIdsToAdd.Contains(js.SkillId))
                    .ToListAsync();

                foreach (var skill in revivable)
                {
                    skill.IsDeleted = false;
                }

                foreach (var skillId in skillIdsToAdd.Except(revivable.Select(s => s.SkillId)))
                {
                    _dbContext.JobSkills.Add(new JobSkill { JobId = jobId, SkillId = skillId });
                }
            }
        }

        if (request.Milestones != null)
        {
            var validatedMilestones = NormalizeUpdateMilestones(request.Milestones);
            // Delete old milestones via DbSet to avoid InMemory EF tracking issues
            var oldMilestones = await _dbContext.JobPostMilestones
                .Where(m => m.JobPostId == jobId)
                .ToListAsync();
            _dbContext.JobPostMilestones.RemoveRange(oldMilestones);
            // Add new milestones via DbSet
            var newMilestones = validatedMilestones.Select(m => new JobPostMilestone
            {
                JobPostId = jobId,
                Title = m.Title,
                Description = m.Description,
                Amount = m.Amount,
                DueDays = m.DueDays,
                AcceptanceCriteria = m.AcceptanceCriteria,
                OrderIndex = m.OrderIndex
            }).ToList();
            await _dbContext.JobPostMilestones.AddRangeAsync(newMilestones);
        }

        var hasChanges = _dbContext.ChangeTracker.HasChanges();
        await _dbContext.SaveChangesAsync();

        if (hasChanges && job.Status == JobStatus.OPEN)
        {
            var expertIds = await _dbContext.Proposals
                .Where(p => p.JobId == jobId &&
                            p.Status != ProposalStatus.REJECTED &&
                            p.Status != ProposalStatus.WITHDRAWN)
                .Select(p => p.ExpertId)
                .Distinct()
                .ToListAsync();

            foreach (var expertId in expertIds)
            {
                // Fire-and-forget so a large proposal list cannot slow down the update response.
                _notificationService.SendInBackground(
                    expertId,
                    "Job post updated",
                    $"The job \"{job.Title}\" you submitted a proposal for has been updated by the client. Please review the changes.",
                    "PROPOSAL",
                    $"/jobs/{jobId}"
                );
            }
        }

        return await GetJobByIdAsync(job.Id);
    }

    public async Task<Response.JobResponse> PublishJobAsync(Guid clientId, Guid jobId)
    {
        var job = await _dbContext.JobPosts.FirstOrDefaultAsync(j => j.Id == jobId && j.ClientId == clientId);
        if (job == null) throw new NotFoundException("Job not found or access denied.");
        if (job.Status != JobStatus.DRAFT) throw new ValidationException("Job is already published or in progress.");

        job.Status = JobStatus.OPEN;
        job.PublishedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();

        await _realtimeService.SendJobStatusUpdateAsync(clientId, job.Id, JobStatus.OPEN, job.Title);
        // Notify users that a new job has been published
        await _realtimeService.SendNewJobPublishedAsync(job.Id, job.Title);

        return await GetJobByIdAsync(job.Id);
    }

    public async Task<Response.JobResponse> CancelJobAsync(Guid clientId, Guid jobId, string? reason)
    {
        var job = await _dbContext.JobPosts.FirstOrDefaultAsync(j => j.Id == jobId && j.ClientId == clientId);
        if (job == null) throw new NotFoundException("Job not found or access denied.");
        if (job.Status != JobStatus.DRAFT && job.Status != JobStatus.OPEN)
            throw new ValidationException("Cannot cancel job in its current status.");

        job.Status = JobStatus.CANCELLED;
        // Optionally store reason in a separate field or log

        var pendingInvites = await _dbContext.JobInvites
            .Where(i => i.JobId == job.Id && i.Status == JobInviteStatus.PENDING)
            .ToListAsync();

        foreach (var invite in pendingInvites)
        {
            invite.Status = JobInviteStatus.EXPIRED;
            invite.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        await _realtimeService.SendJobStatusUpdateAsync(clientId, job.Id, JobStatus.CANCELLED, job.Title);

        return await GetJobByIdAsync(job.Id);
    }

    public async Task<Aivora.Services.Base.Response.PageResult<Response.JobResponse>> GetJobsAsync(Aivora.Services.Base.Request.PageRequest pageRequest, Guid? categoryId = null, JobStatus? status = null)
    {
        var query = _dbContext.JobPosts
            .Include(j => j.Client)
            .Include(j => j.Category)
            .IncludeSkills()
            // This endpoint is anonymous-accessible, and Visibility=PUBLIC alone isn't enough to
            // gate it: it's the default when a client omits the field, so unpublished jobs (DRAFT,
            // or CANCELLED reached directly from DRAFT without ever passing through OPEN) commonly
            // carry it too. Requiring PublishedAt closes that for every status, not just the ones
            // we happen to think of - CANCELLED-from-DRAFT was missed by an earlier DRAFT-only guard.
            .Where(j => j.Status == (status ?? JobStatus.OPEN) && j.Visibility == JobVisibility.PUBLIC && j.PublishedAt != null);

        if (categoryId.HasValue)
        {
            query = query.Where(j => j.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(pageRequest.SearchTerm))
        {
            query = query.Where(j => j.Title.Contains(pageRequest.SearchTerm) || (j.FinalDescription != null && j.FinalDescription.Contains(pageRequest.SearchTerm)));
        }

        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(j => j.PublishedAt)
            .Skip((pageRequest.PageIndex - 1) * pageRequest.PageSize)
            .Take(pageRequest.PageSize)
            .Select(j => MapToResponse(j))
            .ToListAsync();

        return new Aivora.Services.Base.Response.PageResult<Response.JobResponse>
        {
            Items = items,
            TotalItems = totalItems,
            PageIndex = pageRequest.PageIndex,
            PageSize = pageRequest.PageSize
        };
    }

    public async Task<Aivora.Services.Base.Response.PageResult<Response.JobResponse>> GetMyJobsAsync(Guid? clientId, Aivora.Services.Base.Request.PageRequest pageRequest, Aivora.Repositories.Enums.JobStatus? status = null)
    {
        var query = _dbContext.JobPosts
            .Include(j => j.Client)
            .Include(j => j.Category)
            .IncludeSkills()
            .Include(j => j.Milestones)
            .AsQueryable();

        if (clientId.HasValue)
        {
            query = query.Where(j => j.ClientId == clientId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(j => j.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(pageRequest.SearchTerm))
        {
            query = query.Where(j => j.Title.Contains(pageRequest.SearchTerm) || (j.FinalDescription != null && j.FinalDescription.Contains(pageRequest.SearchTerm)));
        }

        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((pageRequest.PageIndex - 1) * pageRequest.PageSize)
            .Take(pageRequest.PageSize)
            .Select(j => MapToResponse(j))
            .ToListAsync();

        return new Aivora.Services.Base.Response.PageResult<Response.JobResponse>
        {
            Items = items,
            TotalItems = totalItems,
            PageIndex = pageRequest.PageIndex,
            PageSize = pageRequest.PageSize
        };
    }

    private static Response.JobResponse MapToResponse(JobPost job)
    {
        return new Response.JobResponse
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
            Skills = job.JobSkills.Select(js => new Response.SkillInfo
            {
                Id = js.SkillId,
                Name = js.Skill?.Name ?? "Unknown"
            }).ToList(),
            Milestones = job.Milestones?.Select(m => new Response.JobMilestoneResponse
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                Amount = m.Amount,
                DueDays = m.DueDays,
                AcceptanceCriteria = m.AcceptanceCriteria,
                OrderIndex = m.OrderIndex
            }).OrderBy(m => m.OrderIndex).ToList() ?? new List<Response.JobMilestoneResponse>()
        };
    }

    private static string NormalizeCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return "AICOIN";
        }

        var normalized = currency.Trim().ToUpperInvariant();
        if (normalized.Length > 10)
        {
            throw new ValidationException("Currency must be 10 characters or fewer.");
        }

        return normalized;
    }

    private static string? NormalizeLimited(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string NormalizeRequired(string? value, string fallback, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static void ValidateJobFields(decimal? budgetMin, decimal? budgetMax, int? timelineDays, DateOnly? deadline)
    {
        if (budgetMin.HasValue && budgetMin.Value <= 0)
        {
            throw new ValidationException("BudgetMin must be greater than 0.");
        }

        if (budgetMax.HasValue && budgetMax.Value <= 0)
        {
            throw new ValidationException("BudgetMax must be greater than 0.");
        }

        if (budgetMin.HasValue && budgetMax.HasValue && budgetMin.Value > budgetMax.Value)
        {
            throw new ValidationException("BudgetMin must be less than or equal to BudgetMax.");
        }

        if (timelineDays.HasValue && (timelineDays.Value < ValidationLimits.MinDurationDays || timelineDays.Value > ValidationLimits.MaxDurationDays))
        {
            throw new ValidationException($"TimelineDays must be between {ValidationLimits.MinDurationDays} and {ValidationLimits.MaxDurationDays}.");
        }

        if (deadline.HasValue && deadline.Value < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ValidationException("Deadline cannot be in the past.");
        }
    }

    private static List<Guid> NormalizeGuidList(IEnumerable<Guid>? values)
    {
        return (values ?? Enumerable.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
    }

    private static List<Request.CreateJobMilestoneRequest> NormalizeMilestones(IEnumerable<Request.CreateJobMilestoneRequest>? milestones)
    {
        var normalized = (milestones ?? Enumerable.Empty<Request.CreateJobMilestoneRequest>())
            .Select((milestone, index) => new Request.CreateJobMilestoneRequest
            {
                Title = NormalizeRequired(milestone.Title, $"Milestone {index + 1}", 255),
                Description = NormalizeLimited(milestone.Description, 2000),
                Amount = milestone.Amount,
                DueDays = milestone.DueDays,
                AcceptanceCriteria = NormalizeLimited(milestone.AcceptanceCriteria, 2000),
                OrderIndex = milestone.OrderIndex < 0 ? index : milestone.OrderIndex
            })
            .ToList();

        foreach (var milestone in normalized)
        {
            if (milestone.Amount <= 0)
            {
                throw new ValidationException("Milestone amounts must be greater than 0.");
            }

            if (milestone.DueDays < ValidationLimits.MinDurationDays || milestone.DueDays > ValidationLimits.MaxDurationDays)
            {
                throw new ValidationException($"Milestone due days must be between {ValidationLimits.MinDurationDays} and {ValidationLimits.MaxDurationDays}.");
            }
        }

        return normalized;
    }
    private static List<Request.UpdateJobMilestoneRequest> NormalizeUpdateMilestones(IEnumerable<Request.UpdateJobMilestoneRequest>? milestones)
    {
        var normalized = (milestones ?? Enumerable.Empty<Request.UpdateJobMilestoneRequest>())
            .Select((milestone, index) => new Request.UpdateJobMilestoneRequest
            {
                Title = NormalizeRequired(milestone.Title, string.Format("Milestone {0}", index + 1), 255),
                Description = NormalizeLimited(milestone.Description, 2000),
                Amount = milestone.Amount,
                DueDays = milestone.DueDays,
                AcceptanceCriteria = NormalizeLimited(milestone.AcceptanceCriteria, 2000),
                OrderIndex = milestone.OrderIndex < 0 ? index : milestone.OrderIndex
            })
            .ToList();

        foreach (var milestone in normalized)
        {
            if (milestone.Amount <= 0)
            {
                throw new ValidationException("Milestone amounts must be greater than 0.");
            }

            if (milestone.DueDays < ValidationLimits.MinDurationDays || milestone.DueDays > ValidationLimits.MaxDurationDays)
            {
                throw new ValidationException($"Milestone due days must be between {ValidationLimits.MinDurationDays} and {ValidationLimits.MaxDurationDays}.");
            }
        }

        return normalized;
    }

    public async Task<bool> DeleteJobAsync(Guid clientId, Guid jobId)
    {
        var job = await _dbContext.JobPosts.FirstOrDefaultAsync(j => j.Id == jobId && j.ClientId == clientId);
        if (job == null) throw new NotFoundException("Job not found or access denied.");
        if (job.Status != JobStatus.DRAFT) throw new ValidationException("Only draft jobs can be deleted.");

        _dbContext.JobPosts.Remove(job);
        await _dbContext.SaveChangesAsync();
        return true;
    }

}
