using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.NotificationService;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.ProposalService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;
    private readonly NotificationService.IService _notificationService;

    public Service(AivoraDbContext dbContext, NotificationService.IService notificationService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
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

        if (job.ClientId == expertId) throw new ValidationException("You cannot submit a proposal to your own job.");

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

        // Send notification to the Client about the new proposal
        try
        {
            await _notificationService.SendNotificationAsync(
                job.ClientId,
                "New proposal received",
                $"An expert has submitted a proposal for the job \"{job.Title}\".",
                "PROPOSAL",
                $"/jobs/{job.Id}/proposals"
            );
        }
        catch
        {
            // Notification failure should not block the main business flow
        }

        return await GetProposalByIdAsync(proposal.Id);
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
    public async Task<Response.ProposalResponse> UpdateProposalAsync(Guid expertId, Guid proposalId, Request.UpdateProposalRequest request)
    {
        if (request is null) throw new ValidationException("Request body is required.");

        var proposal = await _dbContext.Proposals
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal == null) throw new NotFoundException("Proposal not found.");

        if (proposal.ExpertId != expertId)
            throw new UnauthorizedException("You can only edit your own proposal.");

        if (proposal.Status != ProposalStatus.SUBMITTED && proposal.Status != ProposalStatus.SHORTLISTED)
            throw new ValidationException("Proposal can only be edited when it is submitted or shortlisted.");

        if (request.ProposedBudget <= 0)
            throw new ValidationException("ProposedBudget must be greater than 0.");

        if (string.IsNullOrWhiteSpace(request.CoverLetter))
            throw new ValidationException("CoverLetter is required.");

        var validatedMilestones = NormalizeMilestones(request.Milestones);

        proposal.CoverLetter = request.CoverLetter.Trim();
        proposal.ProposedBudget = request.ProposedBudget;
        proposal.ProposedTimelineDays = request.ProposedTimelineDays;
        proposal.UpdatedAt = DateTimeOffset.UtcNow;

        // Delete old milestones via DbSet, then insert new ones
        var oldMilestones = await _dbContext.ProposalMilestones
            .Where(m => m.ProposalId == proposalId)
            .ToListAsync();
        _dbContext.ProposalMilestones.RemoveRange(oldMilestones);
        var newMilestones = validatedMilestones.Select(m => new ProposalMilestone
        {
            ProposalId = proposalId,
            Title = m.Title,
            Description = m.Description,
            Amount = m.Amount,
            DueDays = m.DueDays,
            AcceptanceCriteria = m.AcceptanceCriteria,
            OrderIndex = m.OrderIndex
        }).ToList();
        await _dbContext.ProposalMilestones.AddRangeAsync(newMilestones);

        await _dbContext.SaveChangesAsync();

        return await GetProposalByIdAsync(proposal.Id);
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

    private static List<Request.UpdateProposalMilestoneRequest> NormalizeMilestones(IEnumerable<Request.UpdateProposalMilestoneRequest>? milestones)
    {
        var normalized = (milestones ?? Enumerable.Empty<Request.UpdateProposalMilestoneRequest>())
            .Select((milestone, index) => new Request.UpdateProposalMilestoneRequest
            {
                Title = string.IsNullOrWhiteSpace(milestone.Title) ? string.Format("Milestone {0}", index + 1) : milestone.Title.Trim(),
                Description = milestone.Description,
                Amount = milestone.Amount,
                DueDays = milestone.DueDays,
                AcceptanceCriteria = milestone.AcceptanceCriteria,
                OrderIndex = milestone.OrderIndex < 0 ? index : milestone.OrderIndex
            })
            .ToList();

        foreach (var milestone in normalized)
        {
            if (milestone.Amount <= 0)
            {
                throw new ValidationException("Milestone amounts must be greater than 0.");
            }

            if (milestone.DueDays < 1 || milestone.DueDays > 3650)
            {
                throw new ValidationException("Milestone due days must be between 1 and 3650.");
            }
        }

        return normalized;
    }
}
