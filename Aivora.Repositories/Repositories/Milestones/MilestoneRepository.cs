using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Repositories.Repositories.Milestones;

public class MilestoneRepository : IMilestoneRepository
{
    private readonly AivoraDbContext _dbContext;

    public MilestoneRepository(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Milestone?> GetByIdAsync(Guid id)
    {
        return _dbContext.Milestones.FirstOrDefaultAsync(m => m.Id == id);
    }

    public Task<Milestone?> GetWithProjectAsync(Guid id)
    {
        return _dbContext.Milestones
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public Task<Project?> GetProjectByIdAsync(Guid id)
    {
        return _dbContext.Projects.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task AddAsync(Milestone milestone)
    {
        await _dbContext.Milestones.AddAsync(milestone);
    }

    public Task SaveChangesAsync()
    {
        return _dbContext.SaveChangesAsync();
    }
}
