using Aivora.Repositories.Entities;

namespace Aivora.Repositories.Repositories.Jobs;

public interface IJobRepository
{
    Task<JobPost?> GetByIdAsync(Guid id);
    Task<JobPost?> GetDetailedByIdAsync(Guid id);
    Task<JobPost?> GetOwnedByIdAsync(Guid id, Guid clientId);
    Task<JobPost?> GetOwnedForUpdateAsync(Guid id, Guid clientId);
    Task<(List<JobPost> Items, int TotalItems)> GetOpenPublicAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm,
        Guid? categoryId);
    Task AddAsync(JobPost job);
    void Remove(JobPost job);
    void RemoveSkills(IEnumerable<JobSkill> jobSkills);
    Task SaveChangesAsync();
}
