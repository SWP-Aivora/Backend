using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.JobService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;

    public Service(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Response.JobResponse> GetJobByIdAsync(Guid id)
    {
        var job = await _dbContext.JobPosts
            .Include(j => j.Client)
            .Include(j => j.Category)
            .Include(j => j.JobSkills).ThenInclude(js => js.Skill)
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null) throw new NotFoundException("Job not found.");

        return MapToResponse(job);
    }

    public async Task<Response.JobResponse> CreateJobAsync(Guid clientId, Request.CreateJobRequest request)
    {
        var job = new JobPost
        {
            ClientId = clientId,
            Title = request.Title,
            OriginalDescription = request.OriginalDescription,
            FinalDescription = request.FinalDescription,
            CategoryId = request.CategoryId,
            BudgetType = request.BudgetType,
            BudgetMin = request.BudgetMin,
            BudgetMax = request.BudgetMax,
            TimelineDays = request.TimelineDays,
            Deadline = request.Deadline,
            ExperienceLevel = request.ExperienceLevel,
            Visibility = request.Visibility,
            Status = JobStatus.DRAFT,
            Currency = "AICOIN"
        };

        if (request.SkillIds.Any())
        {
            job.JobSkills = request.SkillIds.Select(skillId => new JobSkill { SkillId = skillId }).ToList();
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

        if (request.Title != null) job.Title = request.Title;
        if (request.FinalDescription != null) job.FinalDescription = request.FinalDescription;
        if (request.CategoryId.HasValue) job.CategoryId = request.CategoryId.Value;
        if (request.BudgetType.HasValue) job.BudgetType = request.BudgetType.Value;
        if (request.BudgetMin.HasValue) job.BudgetMin = request.BudgetMin.Value;
        if (request.BudgetMax.HasValue) job.BudgetMax = request.BudgetMax.Value;
        if (request.TimelineDays.HasValue) job.TimelineDays = request.TimelineDays.Value;
        if (request.Deadline.HasValue) job.Deadline = request.Deadline.Value;
        if (request.ExperienceLevel.HasValue) job.ExperienceLevel = request.ExperienceLevel.Value;
        if (request.Visibility.HasValue) job.Visibility = request.Visibility.Value;

        if (request.SkillIds != null)
        {
            _dbContext.JobSkills.RemoveRange(job.JobSkills);
            job.JobSkills = request.SkillIds.Select(skillId => new JobSkill { SkillId = skillId }).ToList();
        }

        await _dbContext.SaveChangesAsync();

        return await GetJobByIdAsync(job.Id);
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

    public async Task<Response.JobResponse> PublishJobAsync(Guid clientId, Guid jobId)
    {
        var job = await _dbContext.JobPosts.FirstOrDefaultAsync(j => j.Id == jobId && j.ClientId == clientId);
        if (job == null) throw new NotFoundException("Job not found or access denied.");
        if (job.Status != JobStatus.DRAFT) throw new ValidationException("Job is already published or in progress.");

        job.Status = JobStatus.OPEN;
        job.PublishedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();

        return await GetJobByIdAsync(job.Id);
    }

    public async Task<Aivora.Services.Base.Response.PageResult<Response.JobResponse>> GetJobsAsync(Aivora.Services.Base.Request.PageRequest pageRequest, Guid? categoryId = null)
    {
        var query = _dbContext.JobPosts
            .Include(j => j.Client)
            .Include(j => j.Category)
            .Include(j => j.JobSkills).ThenInclude(js => js.Skill)
            .Where(j => j.Status == JobStatus.OPEN && j.Visibility == JobVisibility.PUBLIC);

        if (categoryId.HasValue)
        {
            query = query.Where(j => j.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(pageRequest.SearchTerm))
        {
            query = query.Where(j => j.Title.Contains(pageRequest.SearchTerm) || j.FinalDescription!.Contains(pageRequest.SearchTerm));
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

    private static Response.JobResponse MapToResponse(JobPost job)
    {
        return new Response.JobResponse
        {
            Id = job.Id,
            Title = job.Title,
            OriginalDescription = job.OriginalDescription,
            FinalDescription = job.FinalDescription,
            ClientId = job.ClientId,
            ClientName = job.Client.FullName,
            CategoryId = job.CategoryId,
            CategoryName = job.Category.Name,
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
            Skills = job.JobSkills.Select(js => new Response.SkillInfo
            {
                Id = js.SkillId,
                Name = js.Skill.Name
            }).ToList()
        };
    }
}
