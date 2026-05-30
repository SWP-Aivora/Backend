using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.HiringWorkflowService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;

    public Service(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Response.HiringResultResponse> AcceptProposalAsync(Guid currentUserId, Guid proposalId)
    {
        var proposal = await _dbContext.Proposals
            .Include(p => p.Job)
            .Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal == null) throw new NotFoundException("Proposal not found.");
        if (proposal.Job.ClientId != currentUserId) throw new UnauthorizedException("Only the job owner can accept proposals.");
        if (proposal.Job.Status != JobStatus.OPEN) throw new ValidationException("Job is not open.");
        if (proposal.Status != ProposalStatus.SUBMITTED && proposal.Status != ProposalStatus.SHORTLISTED)
            throw new ValidationException("Proposal is not in a valid state to be accepted.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // 1. Accept target proposal
            proposal.Status = ProposalStatus.ACCEPTED;

            // 2. Reject others
            var otherProposals = await _dbContext.Proposals
                .Where(p => p.JobId == proposal.JobId && p.Id != proposalId && 
                           (p.Status == ProposalStatus.SUBMITTED || p.Status == ProposalStatus.SHORTLISTED))
                .ToListAsync();
            
            foreach (var p in otherProposals) p.Status = ProposalStatus.REJECTED;

            // 3. Update Job
            proposal.Job.Status = JobStatus.IN_PROGRESS;

            // 4. Create Project
            var project = new Project
            {
                JobId = proposal.JobId,
                AcceptedProposalId = proposal.Id,
                ClientId = proposal.Job.ClientId,
                ExpertId = proposal.ExpertId,
                Title = proposal.Job.Title,
                Description = proposal.Job.FinalDescription ?? proposal.Job.OriginalDescription,
                TotalBudget = proposal.ProposedBudget,
                Currency = proposal.Currency,
                Status = ProjectStatus.PENDING_PAYMENT,
                Milestones = proposal.Milestones.Select(pm => new Milestone
                {
                    Title = pm.Title,
                    Description = pm.Description,
                    Amount = pm.Amount,
                    OrderIndex = pm.OrderIndex,
                    Status = MilestoneStatus.CREATED,
                    Currency = proposal.Currency
                }).ToList()
            };

            _dbContext.Projects.Add(project);
            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();

            return new Response.HiringResultResponse
            {
                ProjectId = project.Id,
                JobId = proposal.JobId,
                AcceptedProposalId = proposal.Id,
                Status = project.Status.ToString()
            };
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
