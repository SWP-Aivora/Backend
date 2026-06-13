using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Repositories.Projects;

public interface IProjectRepository
{
    Task<Project?> GetDetailedByIdAsync(Guid id);
    Task<Project?> GetOwnedWithMilestonesAsync(Guid id, Guid clientId);
    Task<Project?> GetWithMilestonesAndJobAsync(Guid id);
    Task<Project?> GetByIdAsync(Guid id);
    Task<(List<Project> Items, int TotalItems)> ListForUserAsync(
        Guid userId,
        UserRole role,
        int pageIndex,
        int pageSize,
        string? searchTerm,
        ProjectStatus? status);
    Task AddAsync(Project project);
    Task SaveChangesAsync();
}
