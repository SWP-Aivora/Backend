using Aivora.Repositories.Entities;

namespace Aivora.Repositories.Repositories.Proposals;

public interface IProposalRepository
{
    Task<Proposal?> GetByIdAsync(Guid id);
    Task<Proposal?> GetDetailedByIdAsync(Guid id);
    Task<Proposal?> GetForHiringAsync(Guid id);
    Task<Proposal?> GetWithJobAsync(Guid id);
    Task<bool> ExistsForJobAndExpertAsync(Guid jobId, Guid expertId);
    Task<List<Proposal>> ListByJobIdAsync(Guid jobId);
    Task<List<Proposal>> ListByExpertIdAsync(Guid expertId);
    Task<List<Proposal>> ListPendingSiblingsAsync(Guid jobId, Guid acceptedProposalId);
    Task AddAsync(Proposal proposal);
    Task SaveChangesAsync();
}
