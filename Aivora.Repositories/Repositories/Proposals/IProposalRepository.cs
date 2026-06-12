using Aivora.Repositories.Entities;

namespace Aivora.Repositories.Repositories.Proposals;

public interface IProposalRepository
{
    Task<Proposal?> GetDetailedByIdAsync(Guid id);
    Task<bool> ExistsForJobAndExpertAsync(Guid jobId, Guid expertId);
    Task<List<Proposal>> ListByJobIdAsync(Guid jobId);
    Task<List<Proposal>> ListByExpertIdAsync(Guid expertId);
    Task AddAsync(Proposal proposal);
    Task SaveChangesAsync();
}
