using Aivora.Repositories.Entities;

namespace Aivora.Repositories.Repositories.Milestones;

public interface IMilestoneRepository
{
    Task<Milestone?> GetByIdAsync(Guid id);
    Task<Milestone?> GetWithProjectAsync(Guid id);
    Task<Project?> GetProjectByIdAsync(Guid id);
    Task AddAsync(Milestone milestone);
    Task SaveChangesAsync();
}
