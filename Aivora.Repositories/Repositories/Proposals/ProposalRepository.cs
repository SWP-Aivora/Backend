using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Repositories.Repositories.Proposals;

public class ProposalRepository : IProposalRepository
{
    private readonly AivoraDbContext _dbContext;

    public ProposalRepository(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Proposal?> GetDetailedByIdAsync(Guid id)
    {
        return _dbContext.Proposals
            .Include(p => p.Job).ThenInclude(j => j.Client)
            .Include(p => p.Expert)
            .Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public Task<bool> ExistsForJobAndExpertAsync(Guid jobId, Guid expertId)
    {
        return _dbContext.Proposals.AnyAsync(p => p.JobId == jobId && p.ExpertId == expertId);
    }

    public Task<List<Proposal>> ListByJobIdAsync(Guid jobId)
    {
        return _dbContext.Proposals
            .Include(p => p.Expert)
            .Include(p => p.Milestones)
            .Where(p => p.JobId == jobId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public Task<List<Proposal>> ListByExpertIdAsync(Guid expertId)
    {
        return _dbContext.Proposals
            .Include(p => p.Job)
            .Include(p => p.Milestones)
            .Where(p => p.ExpertId == expertId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(Proposal proposal)
    {
        await _dbContext.Proposals.AddAsync(proposal);
    }

    public Task SaveChangesAsync()
    {
        return _dbContext.SaveChangesAsync();
    }
}
