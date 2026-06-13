using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Repositories.Repositories.Jobs;

public class JobRepository : IJobRepository
{
    private readonly AivoraDbContext _dbContext;

    public JobRepository(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<JobPost?> GetByIdAsync(Guid id)
    {
        return _dbContext.JobPosts.FirstOrDefaultAsync(j => j.Id == id);
    }

    public Task<JobPost?> GetDetailedByIdAsync(Guid id)
    {
        return _dbContext.JobPosts
            .Include(j => j.Client)
            .Include(j => j.Category)
            .Include(j => j.JobSkills).ThenInclude(js => js.Skill)
            .Include(j => j.Milestones)
            .FirstOrDefaultAsync(j => j.Id == id);
    }

    public Task<JobPost?> GetOwnedByIdAsync(Guid id, Guid clientId)
    {
        return _dbContext.JobPosts.FirstOrDefaultAsync(j => j.Id == id && j.ClientId == clientId);
    }

    public Task<JobPost?> GetOwnedForUpdateAsync(Guid id, Guid clientId)
    {
        return _dbContext.JobPosts
            .Include(j => j.JobSkills)
            .FirstOrDefaultAsync(j => j.Id == id && j.ClientId == clientId);
    }

    public async Task<(List<JobPost> Items, int TotalItems)> GetOpenPublicAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm,
        Guid? categoryId)
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

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(j => j.Title.Contains(searchTerm) || j.FinalDescription!.Contains(searchTerm));
        }

        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(j => j.PublishedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalItems);
    }

    public async Task AddAsync(JobPost job)
    {
        await _dbContext.JobPosts.AddAsync(job);
    }

    public void Remove(JobPost job)
    {
        _dbContext.JobPosts.Remove(job);
    }

    public void RemoveSkills(IEnumerable<JobSkill> jobSkills)
    {
        _dbContext.JobSkills.RemoveRange(jobSkills);
    }

    public Task SaveChangesAsync()
    {
        return _dbContext.SaveChangesAsync();
    }
}
