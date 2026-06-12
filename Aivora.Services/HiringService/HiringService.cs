using Aivora.Repositories.Abstractions;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Repositories.Repositories.Projects;
using Aivora.Repositories.Repositories.Proposals;
using Aivora.Services.Exceptions;

namespace Aivora.Services.HiringService;

public class HiringService : IHiringService
{
    private readonly IProposalRepository _proposalRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUnitOfWork _unitOfWork;

    public HiringService(
        IProposalRepository proposalRepository,
        IProjectRepository projectRepository,
        IUnitOfWork unitOfWork)
    {
        _proposalRepository = proposalRepository;
        _projectRepository = projectRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Response.HiringResultResponse> AcceptProposalAsync(Guid clientId, Guid proposalId)
    {
        var proposal = await _proposalRepository.GetForHiringAsync(proposalId);

        if (proposal == null) throw new NotFoundException("Proposal not found.");
        if (proposal.Job.ClientId != clientId) throw new UnauthorizedException("Only the job owner can accept proposals.");
        if (proposal.Job.Status != JobStatus.OPEN) throw new ValidationException("Job is no longer open.");
        if (proposal.Status != ProposalStatus.SUBMITTED && proposal.Status != ProposalStatus.SHORTLISTED)
            throw new ValidationException("Proposal is not in a valid state to be accepted.");

        Project? project = null;
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // 1. Accept target proposal
            proposal.Status = ProposalStatus.ACCEPTED;
            proposal.UpdatedAt = DateTimeOffset.UtcNow;

            // 2. Reject sibling proposals
            var otherProposals = await _proposalRepository.ListPendingSiblingsAsync(proposal.JobId, proposalId);

            foreach (var p in otherProposals)
            {
                p.Status = ProposalStatus.REJECTED;
                p.UpdatedAt = DateTimeOffset.UtcNow;
            }

            // 3. Update Job
            proposal.Job.Status = JobStatus.IN_PROGRESS;
            proposal.Job.UpdatedAt = DateTimeOffset.UtcNow;

            // 4. Create Project
            project = new Project
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

            await _projectRepository.AddAsync(project);
            await _projectRepository.SaveChangesAsync();
        });

        return new Response.HiringResultResponse
        {
            ProjectId = project!.Id,
            JobId = proposal.JobId,
            AcceptedProposalId = proposal.Id,
            Status = project!.Status.ToString()
        };
    }

    public async Task<bool> ShortlistProposalAsync(Guid clientId, Guid proposalId)
    {
        var proposal = await GetProposalWithOwnerCheckAsync(clientId, proposalId);

        if (proposal.Status != ProposalStatus.SUBMITTED)
            throw new ValidationException("Only submitted proposals can be shortlisted.");

        proposal.Status = ProposalStatus.SHORTLISTED;
        proposal.UpdatedAt = DateTimeOffset.UtcNow;

        await _proposalRepository.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectProposalAsync(Guid clientId, Guid proposalId)
    {
        var proposal = await GetProposalWithOwnerCheckAsync(clientId, proposalId);

        if (proposal.Status == ProposalStatus.ACCEPTED || proposal.Status == ProposalStatus.WITHDRAWN)
            throw new ValidationException("Cannot reject an accepted or withdrawn proposal.");

        proposal.Status = ProposalStatus.REJECTED;
        proposal.UpdatedAt = DateTimeOffset.UtcNow;

        await _proposalRepository.SaveChangesAsync();
        return true;
    }

    public async Task<bool> WithdrawProposalAsync(Guid expertId, Guid proposalId)
    {
        var proposal = await _proposalRepository.GetByIdAsync(proposalId);

        if (proposal == null) throw new NotFoundException("Proposal not found.");
        if (proposal.ExpertId != expertId) throw new UnauthorizedException("You can only withdraw your own proposal.");

        if (proposal.Status == ProposalStatus.ACCEPTED || proposal.Status == ProposalStatus.REJECTED)
            throw new ValidationException("Terminal proposals cannot be withdrawn.");

        proposal.Status = ProposalStatus.WITHDRAWN;
        proposal.WithdrawnAt = DateTimeOffset.UtcNow;
        proposal.UpdatedAt = DateTimeOffset.UtcNow;

        await _proposalRepository.SaveChangesAsync();
        return true;
    }

    private async Task<Proposal> GetProposalWithOwnerCheckAsync(Guid clientId, Guid proposalId)
    {
        var proposal = await _proposalRepository.GetWithJobAsync(proposalId);

        if (proposal == null) throw new NotFoundException("Proposal not found.");
        if (proposal.Job.ClientId != clientId) throw new UnauthorizedException("Access denied.");

        return proposal;
    }
}
