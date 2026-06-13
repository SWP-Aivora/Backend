using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Repositories.Repositories.Projects;

public class ProjectRepository : IProjectRepository
{
    private readonly AivoraDbContext _dbContext;

    public ProjectRepository(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Project?> GetDetailedByIdAsync(Guid id)
    {
        return _dbContext.Projects
            .Include(p => p.Client)
            .Include(p => p.Expert)
            .Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public Task<Project?> GetOwnedWithMilestonesAsync(Guid id, Guid clientId)
    {
        return _dbContext.Projects
            .Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.Id == id && p.ClientId == clientId);
    }

    public Task<Project?> GetWithMilestonesAndJobAsync(Guid id)
    {
        return _dbContext.Projects
            .Include(p => p.Milestones)
            .Include(p => p.Job)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public Task<Project?> GetByIdAsync(Guid id)
    {
        return _dbContext.Projects.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<(List<Project> Items, int TotalItems)> ListForUserAsync(
        Guid userId,
        UserRole role,
        int pageIndex,
        int pageSize,
        string? searchTerm,
        ProjectStatus? status)
    {
        var query = _dbContext.Projects
            .Include(p => p.Client)
            .Include(p => p.Expert)
            .AsQueryable();

        if (role == UserRole.CLIENT)
        {
            query = query.Where(p => p.ClientId == userId);
        }
        else if (role == UserRole.EXPERT)
        {
            query = query.Where(p => p.ExpertId == userId);
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(p => p.Title.Contains(searchTerm));
        }

        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalItems);
    }

    public async Task AddAsync(Project project)
    {
        await _dbContext.Projects.AddAsync(project);
    }

    public Task SaveChangesAsync()
    {
        return _dbContext.SaveChangesAsync();
    }
}
