using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.ProposalService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;

    public Service(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Response.ProposalResponse> GetProposalByIdAsync(Guid id)
    {
        var proposal = await _dbContext.Proposals
            .Include(p => p.Job).ThenInclude(j => j.Client)
            .Include(p => p.Expert)
            .Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (proposal == null) throw new NotFoundException("Proposal not found.");

        return MapToResponse(proposal);
    }

    public async Task<Response.ProposalResponse> CreateProposalAsync(Guid expertId, Request.CreateProposalRequest request)
    {
        var job = await _dbContext.JobPosts.FindAsync(request.JobId);
        if (job == null) throw new NotFoundException("Job not found.");
        if (job.Status != JobStatus.OPEN) throw new ValidationException("Job is no longer open for proposals.");

        var existingProposal = await _dbContext.Proposals.AnyAsync(p => p.JobId == request.JobId && p.ExpertId == expertId);
        if (existingProposal) throw new ValidationException("You have already submitted a proposal for this job.");

        var proposal = new Proposal
        {
            JobId = request.JobId,
            ExpertId = expertId,
            CoverLetter = request.CoverLetter,
            ProposedBudget = request.ProposedBudget,
            ProposedTimelineDays = request.ProposedTimelineDays,
            Currency = "AICOIN",
            Status = ProposalStatus.SUBMITTED,
            Milestones = request.Milestones.Select(m => new ProposalMilestone
            {
                Title = m.Title,
                Description = m.Description,
                Amount = m.Amount,
                DueDays = m.DueDays,
                AcceptanceCriteria = m.AcceptanceCriteria,
                OrderIndex = m.OrderIndex
            }).ToList()
        };

        _dbContext.Proposals.Add(proposal);
        await _dbContext.SaveChangesAsync();

        return await GetProposalByIdAsync(proposal.Id);
    }

    public async Task<Response.ProposalResponse> UpdateProposalStatusAsync(Guid userId, Guid proposalId, ProposalStatus status)
    {
        var proposal = await _dbContext.Proposals
            .Include(p => p.Job)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal == null) throw new NotFoundException("Proposal not found.");

        // If client is updating (shortlist/reject/accept)
        if (proposal.Job.ClientId == userId)
        {
            if (status != ProposalStatus.SHORTLISTED && status != ProposalStatus.REJECTED && status != ProposalStatus.ACCEPTED)
                throw new ValidationException("Invalid status update for client.");
        }
        // If expert is updating (withdraw)
        else if (proposal.ExpertId == userId)
        {
            if (status != ProposalStatus.WITHDRAWN)
                throw new ValidationException("Invalid status update for expert.");
        }
        else
        {
            throw new UnauthorizedException("Access denied.");
        }

        proposal.Status = status;
        if (status == ProposalStatus.WITHDRAWN) proposal.WithdrawnAt = DateTimeOffset.UtcNow;

        // If accepted, handle logic to reject others might be needed, but we'll do that in Project creation logic or here.
        // For MVP, we'll just update this status.

        await _dbContext.SaveChangesAsync();
        return await GetProposalByIdAsync(proposalId);
    }

    public async Task<List<Response.ProposalResponse>> GetProposalsByJobIdAsync(Guid userId, Guid jobId)
    {
        var job = await _dbContext.JobPosts.FindAsync(jobId);
        if (job == null) throw new NotFoundException("Job not found.");
        if (job.ClientId != userId) throw new UnauthorizedException("Only the job owner can view proposals.");

        var proposals = await _dbContext.Proposals
            .Include(p => p.Expert)
            .Include(p => p.Milestones)
            .Where(p => p.JobId == jobId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return proposals.Select(MapToResponse).ToList();
    }

    public async Task<List<Response.ProposalResponse>> GetExpertProposalsAsync(Guid expertId)
    {
        var proposals = await _dbContext.Proposals
            .Include(p => p.Job)
            .Include(p => p.Milestones)
            .Where(p => p.ExpertId == expertId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return proposals.Select(MapToResponse).ToList();
    }

    private static Response.ProposalResponse MapToResponse(Proposal proposal)
    {
        return new Response.ProposalResponse
        {
            Id = proposal.Id,
            JobId = proposal.JobId,
            JobTitle = proposal.Job?.Title ?? "N/A",
            ExpertId = proposal.ExpertId,
            ExpertName = proposal.Expert?.FullName ?? "N/A",
            CoverLetter = proposal.CoverLetter,
            ProposedBudget = proposal.ProposedBudget,
            ProposedTimelineDays = proposal.ProposedTimelineDays,
            Currency = proposal.Currency,
            Status = proposal.Status,
            SubmittedAt = proposal.CreatedAt,
            Milestones = proposal.Milestones.Select(m => new Response.ProposalMilestoneResponse
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                Amount = m.Amount,
                DueDays = m.DueDays,
                AcceptanceCriteria = m.AcceptanceCriteria,
                OrderIndex = m.OrderIndex
            }).OrderBy(m => m.OrderIndex).ToList()
        };
    }
}
