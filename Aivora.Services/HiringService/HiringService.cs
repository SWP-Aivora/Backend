using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.NotificationService;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.HiringService;

public class HiringService : IHiringService
{
    private readonly AivoraDbContext _dbContext;
    private readonly NotificationService.IService _notificationService;
    private readonly RealtimeService.IService _realtimeService;

    public HiringService(AivoraDbContext dbContext, NotificationService.IService notificationService, RealtimeService.IService realtimeService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _realtimeService = realtimeService;
    }

    public async Task<Response.HiringResultResponse> AcceptProposalAsync(Guid clientId, Guid proposalId)
    {
        var proposal = await _dbContext.Proposals
            .Include(p => p.Job)
            .Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal == null) throw new NotFoundException("Proposal not found.");
        if (proposal.Job.ClientId != clientId) throw new UnauthorizedException("Only the job owner can accept proposals.");
        if (proposal.Job.Status != JobStatus.OPEN) throw new ValidationException("Job is no longer open.");
        if (proposal.Status != ProposalStatus.SUBMITTED && proposal.Status != ProposalStatus.SHORTLISTED)
            throw new ValidationException("Proposal is not in a valid state to be accepted.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // 1. Accept target proposal
            proposal.Status = ProposalStatus.ACCEPTED;
            proposal.UpdatedAt = DateTimeOffset.UtcNow;

            // 2. Reject sibling proposals
            var otherProposals = await _dbContext.Proposals
                .Where(p => p.JobId == proposal.JobId && p.Id != proposalId &&
                           (p.Status == ProposalStatus.SUBMITTED || p.Status == ProposalStatus.SHORTLISTED))
                .ToListAsync();

            foreach (var p in otherProposals)
            {
                p.Status = ProposalStatus.REJECTED;
                p.UpdatedAt = DateTimeOffset.UtcNow;
            }

            // 3. Update Job
            proposal.Job.Status = JobStatus.IN_PROGRESS;
            proposal.Job.UpdatedAt = DateTimeOffset.UtcNow;

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
                    Currency = proposal.Currency,
                    Steps = new List<MilestoneStep>
                    {
                        new MilestoneStep
                        {
                            Title = "Created",
                            Description = "Milestone created",
                            OrderIndex = 0,
                            Status = MilestoneStepStatus.COMPLETED,
                            CompletedAt = DateTimeOffset.UtcNow,
                            CompletedByUserId = proposal.Job.ClientId
                        }
                    }
                }).ToList()
            };

            _dbContext.Projects.Add(project);
            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();

            var affectedUsers = new[] { clientId, proposal.ExpertId };
            await _realtimeService.SendJobStatusUpdateToUsersAsync(affectedUsers, proposal.JobId, JobStatus.IN_PROGRESS, proposal.Job.Title);

            // Send notification to the accepted Expert
            try
            {
                await _notificationService.SendNotificationAsync(
                    proposal.ExpertId,
                    "Proposal accepted",
                    $"Congratulations! Your proposal for the job \"{proposal.Job.Title}\" has been accepted.",
                    "PROPOSAL",
                    $"/projects/{project.Id}"
                );
            }
            catch
            {
                // Notification failure should not block the main business flow
            }

            // Send notification to rejected Experts (another proposal was accepted)
            foreach (var p in otherProposals)
            {
                try
                {
                    await _notificationService.SendNotificationAsync(
                        p.ExpertId,
                        "Proposal rejected",
                        $"Your proposal for the job \"{proposal.Job.Title}\" has been rejected because the client selected another expert.",
                        "PROPOSAL",
                        $"/jobs/{proposal.JobId}"
                    );
                }
                catch
                {
                    // Notification failure should not block the main business flow
                }
            }

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

    public async Task<bool> ShortlistProposalAsync(Guid clientId, Guid proposalId)
    {
        var proposal = await GetProposalWithOwnerCheckAsync(clientId, proposalId);

        if (proposal.Status != ProposalStatus.SUBMITTED)
            throw new ValidationException("Only submitted proposals can be shortlisted.");

        proposal.Status = ProposalStatus.SHORTLISTED;
        proposal.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectProposalAsync(Guid clientId, Guid proposalId)
    {
        var proposal = await GetProposalWithOwnerCheckAsync(clientId, proposalId);

        if (proposal.Status == ProposalStatus.ACCEPTED || proposal.Status == ProposalStatus.WITHDRAWN)
            throw new ValidationException("Cannot reject an accepted or withdrawn proposal.");

        proposal.Status = ProposalStatus.REJECTED;
        proposal.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();

        // Send notification to the rejected Expert
        try
        {
            await _notificationService.SendNotificationAsync(
                proposal.ExpertId,
                "Proposal rejected",
                $"Your proposal for the job \"{proposal.Job.Title}\" has been rejected.",
                "PROPOSAL",
                $"/jobs/{proposal.JobId}"
            );
        }
        catch
        {
            // Notification failure should not block the main business flow
        }

        return true;
    }

    public async Task<bool> WithdrawProposalAsync(Guid expertId, Guid proposalId)
    {
        var proposal = await _dbContext.Proposals.FindAsync(proposalId);

        if (proposal == null) throw new NotFoundException("Proposal not found.");
        if (proposal.ExpertId != expertId) throw new UnauthorizedException("You can only withdraw your own proposal.");

        if (proposal.Status == ProposalStatus.ACCEPTED || proposal.Status == ProposalStatus.REJECTED)
            throw new ValidationException("Terminal proposals cannot be withdrawn.");

        proposal.Status = ProposalStatus.WITHDRAWN;
        proposal.WithdrawnAt = DateTimeOffset.UtcNow;
        proposal.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();
        return true;
    }

    private async Task<Proposal> GetProposalWithOwnerCheckAsync(Guid clientId, Guid proposalId)
    {
        var proposal = await _dbContext.Proposals
            .Include(p => p.Job)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal == null) throw new NotFoundException("Proposal not found.");
        if (proposal.Job.ClientId != clientId) throw new UnauthorizedException("Access denied.");

        return proposal;
    }

    public async Task<bool> UnshortlistProposalAsync(Guid clientId, Guid proposalId)
    {
        var proposal = await GetProposalWithOwnerCheckAsync(clientId, proposalId);

        if (proposal.Status != ProposalStatus.SHORTLISTED)
            throw new ValidationException("Only shortlisted proposals can be unshortlisted.");

        proposal.Status = ProposalStatus.SUBMITTED;
        proposal.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();
        return true;
    }
}
