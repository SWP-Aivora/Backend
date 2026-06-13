using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Repositories.Repositories.Proposals;

public class ProposalRepository : IProposalRepository
{
    private readonly AivoraDbContext _dbContext;

    public ProposalRepository(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Proposal?> GetByIdAsync(Guid id)
    {
        return _dbContext.Proposals.FirstOrDefaultAsync(p => p.Id == id);
    }

    public Task<Proposal?> GetDetailedByIdAsync(Guid id)
    {
        return _dbContext.Proposals
            .Include(p => p.Job).ThenInclude(j => j.Client)
            .Include(p => p.Expert)
            .Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public Task<Proposal?> GetForHiringAsync(Guid id)
    {
        return _dbContext.Proposals
            .Include(p => p.Job)
            .Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public Task<Proposal?> GetWithJobAsync(Guid id)
    {
        return _dbContext.Proposals
            .Include(p => p.Job)
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

    public Task<List<Proposal>> ListPendingSiblingsAsync(Guid jobId, Guid acceptedProposalId)
    {
        return _dbContext.Proposals
            .Where(p => p.JobId == jobId && p.Id != acceptedProposalId &&
                       (p.Status == ProposalStatus.SUBMITTED || p.Status == ProposalStatus.SHORTLISTED))
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
