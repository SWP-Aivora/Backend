using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Repositories.Repositories.Jobs;
using Aivora.Repositories.Repositories.Proposals;
using Aivora.Services.Exceptions;

namespace Aivora.Services.ProposalService;

public class ProposalApplicationService : IService
{
    private readonly IProposalRepository _proposalRepository;
    private readonly IJobRepository _jobRepository;

    public ProposalApplicationService(IProposalRepository proposalRepository, IJobRepository jobRepository)
    {
        _proposalRepository = proposalRepository;
        _jobRepository = jobRepository;
    }

    public async Task<Response.ProposalResponse> GetProposalByIdAsync(Guid id)
    {
        var proposal = await _proposalRepository.GetDetailedByIdAsync(id);

        if (proposal == null) throw new NotFoundException("Proposal not found.");

        return MapToResponse(proposal);
    }

    public async Task<Response.ProposalResponse> CreateProposalAsync(Guid expertId, Request.CreateProposalRequest request)
    {
        var job = await _jobRepository.GetByIdAsync(request.JobId);
        if (job == null) throw new NotFoundException("Job not found.");
        if (job.Status != JobStatus.OPEN) throw new ValidationException("Job is no longer open for proposals.");

        if (job.ClientId == expertId) throw new ValidationException("You cannot submit a proposal to your own job.");

        var existingProposal = await _proposalRepository.ExistsForJobAndExpertAsync(request.JobId, expertId);
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

        await _proposalRepository.AddAsync(proposal);
        await _proposalRepository.SaveChangesAsync();

        return await GetProposalByIdAsync(proposal.Id);
    }

    public async Task<List<Response.ProposalResponse>> GetProposalsByJobIdAsync(Guid userId, Guid jobId)
    {
        var job = await _jobRepository.GetByIdAsync(jobId);
        if (job == null) throw new NotFoundException("Job not found.");
        if (job.ClientId != userId) throw new UnauthorizedException("Only the job owner can view proposals.");

        var proposals = await _proposalRepository.ListByJobIdAsync(jobId);

        return proposals.Select(MapToResponse).ToList();
    }

    public async Task<List<Response.ProposalResponse>> GetExpertProposalsAsync(Guid expertId)
    {
        var proposals = await _proposalRepository.ListByExpertIdAsync(expertId);

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
