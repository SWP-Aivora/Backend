using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.NotificationService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aivora.Services.DisputeService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;
    private readonly NotificationService.IService _notificationService;
    private readonly ILogger<Service> _logger;

    public Service(AivoraDbContext dbContext, NotificationService.IService notificationService, ILogger<Service> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Response.DisputeResponse> OpenDisputeAsync(Guid userId, Request.OpenDisputeRequest request)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == request.MilestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");
        if (milestone.Project.ClientId != userId && milestone.Project.ExpertId != userId)
            throw new ForbiddenException("You are not authorized to open a dispute for this project.");

        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestone.Id && (p.Status == PaymentStatus.RELEASED || p.Status == PaymentStatus.HELD));
        if (payment == null) throw new ValidationException("Only funded milestones with released payments can be disputed.");

        // Block re-opening dispute after CLOSED
        var hasClosedDispute = await _dbContext.Disputes
            .AnyAsync(d => d.MilestoneId == milestone.Id && d.Status == DisputeStatus.CLOSED);
        if (hasClosedDispute)
            throw new ValidationException("A dispute for this milestone was already closed. Cannot open a new dispute.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var againstUserId = (userId == milestone.Project.ClientId) ? milestone.Project.ExpertId : milestone.Project.ClientId;

            var dispute = new Dispute
            {
                ProjectId = milestone.ProjectId,
                MilestoneId = milestone.Id,
                PaymentId = payment.Id,
                OpenedBy = userId,
                AgainstUserId = againstUserId,
                Reason = request.Reason,
                Description = request.Description,
                Status = DisputeStatus.OPEN
            };

            // Gate: lock milestone and project
            milestone.Status = MilestoneStatus.DISPUTED;
            milestone.Project.Status = ProjectStatus.DISPUTED;


            _dbContext.Disputes.Add(dispute);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            // Send notification to the respondent
            try
            {
                await _notificationService.SendNotificationAsync(
                    againstUserId,
                    "A dispute has been opened",
                    $"A dispute has been opened regarding your project. Reason: {request.Reason}",
                    "DISPUTE",
                    $"/disputes/{dispute.Id}"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send dispute opened notification to user {AgainstUserId}", againstUserId);
            }

            return await GetDisputeByIdAsync(userId, dispute.Id);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Response.DisputeResponse> GetDisputeByIdAsync(Guid userId, Guid disputeId)
    {
        var dispute = await _dbContext.Disputes
            .Include(d => d.Project)
            .Include(d => d.Milestone)
            .Include(d => d.Opener)
            .Include(d => d.Admin)
            .Include(d => d.Milestone.Project.Expert)
            .Include(d => d.Milestone.Project.Client)
            .FirstOrDefaultAsync(d => d.Id == disputeId);

        if (dispute == null) throw new NotFoundException("Dispute not found.");

        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null) throw new UnauthorizedException("User not found.");
        if (user.Role != UserRole.ADMIN && dispute.OpenedBy != userId && dispute.AgainstUserId != userId)
            throw new ForbiddenException("You are not authorized to view this dispute.");

        var againstUser = await _dbContext.Users.FindAsync(dispute.AgainstUserId);
        var evidence = await _dbContext.DisputeEvidences
            .Include(e => e.SubmittedByUser)
            .Where(e => e.DisputeId == disputeId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

        return new Response.DisputeResponse
        {
            Id = dispute.Id,
            ProjectId = dispute.ProjectId,
            ProjectTitle = dispute.Project.Title,
            MilestoneId = dispute.MilestoneId,
            MilestoneTitle = dispute.Milestone.Title,
            OpenedBy = dispute.OpenedBy,
            OpenerName = dispute.Opener.FullName,
            AgainstUserId = dispute.AgainstUserId,
            AgainstUserName = againstUser?.FullName ?? "Unknown User",
            Reason = dispute.Reason,
            Description = dispute.Description,
            Status = dispute.Status.ToString(),
            ResolutionNote = dispute.ResolutionNote,
            ResolvedAt = dispute.ResolvedAt,
            CreatedAt = dispute.CreatedAt,
            Evidence = evidence.Select(e => new Response.DisputeEvidenceResponse
            {
                Id = e.Id,
                SubmittedBy = e.SubmittedBy,
                SubmittedByName = e.SubmittedByUser.FullName,
                Content = e.Content ?? "",
                FileUrl = e.FileUrl,
                CreatedAt = e.CreatedAt
            }).ToList()
        };
    }

    public async Task<Base.Response.PageResult<Response.DisputeResponse>> GetDisputesAsync(Guid userId, string role, Base.Request.PageRequest pageRequest)
    {
        IQueryable<Dispute> query = _dbContext.Disputes
            .Include(d => d.Project)
            .Include(d => d.Milestone)
            .Include(d => d.Opener);

        if (role != UserRole.ADMIN.ToString())
        {
            query = query.Where(d => d.OpenedBy == userId || d.AgainstUserId == userId);
        }

        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((pageRequest.PageIndex - 1) * pageRequest.PageSize)
            .Take(pageRequest.PageSize)
            .ToListAsync();

        var responses = new List<Response.DisputeResponse>();
        foreach (var d in items)
        {
            responses.Add(new Response.DisputeResponse
            {
                Id = d.Id,
                ProjectId = d.ProjectId,
                ProjectTitle = d.Project.Title,
                MilestoneId = d.MilestoneId,
                MilestoneTitle = d.Milestone.Title,
                OpenedBy = d.OpenedBy,
                OpenerName = d.Opener.FullName,
                Reason = d.Reason,
                Status = d.Status.ToString(),
                CreatedAt = d.CreatedAt
            });
        }

        return new Base.Response.PageResult<Response.DisputeResponse>
        {
            Items = responses,
            TotalItems = totalItems,
            PageIndex = pageRequest.PageIndex,
            PageSize = pageRequest.PageSize
        };
    }

    public async Task<Response.DisputeEvidenceResponse> AddEvidenceAsync(Guid userId, Guid disputeId, Request.AddEvidenceRequest request)
    {
        var dispute = await _dbContext.Disputes.FindAsync(disputeId);
        if (dispute == null) throw new NotFoundException("Dispute not found.");
        if (dispute.Status == DisputeStatus.RESOLVED || dispute.Status == DisputeStatus.CLOSED)
            throw new ValidationException("Cannot add evidence to a closed dispute.");

        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null) throw new UnauthorizedException("User not found.");
        if (user.Role != UserRole.ADMIN && dispute.OpenedBy != userId && dispute.AgainstUserId != userId)
            throw new ForbiddenException("You are not authorized to add evidence to this dispute.");

        var evidence = new DisputeEvidence
        {
            DisputeId = disputeId,
            SubmittedBy = userId,
            Content = request.Content,
            FileUrl = request.FileUrl
        };

        _dbContext.DisputeEvidences.Add(evidence);
        await _dbContext.SaveChangesAsync();

        return new Response.DisputeEvidenceResponse
        {
            Id = evidence.Id,
            SubmittedBy = userId,
            SubmittedByName = user.FullName,
            Content = evidence.Content ?? "",
            FileUrl = evidence.FileUrl,
            CreatedAt = evidence.CreatedAt
        };
    }

    public async Task<Response.DisputeResponse> ResolveDisputeAsync(Guid adminId, Guid disputeId, Request.ResolveDisputeRequest request)
    {
        var dispute = await _dbContext.Disputes
            .Include(d => d.Milestone)
            .Include(d => d.Project)
            .FirstOrDefaultAsync(d => d.Id == disputeId);

        if (dispute == null) throw new NotFoundException("Dispute not found.");
        if (dispute.Status == DisputeStatus.RESOLVED) throw new ValidationException("Dispute is already resolved.");

        var project = dispute.Project;
        var milestone = dispute.Milestone;

        // Update dispute status
        dispute.Status = DisputeStatus.RESOLVED;
        dispute.ResolutionNote = request.ResolutionNote;
        dispute.ResolvedAt = DateTimeOffset.UtcNow;
        dispute.AdminId = adminId;

        // Unlock milestone: only reopen Approve & Pay if a deliverable was actually submitted
        milestone.Status = milestone.SubmittedAt != null
            ? MilestoneStatus.SUBMITTED
            : MilestoneStatus.IN_PROGRESS;

        // Recalculate project status
        var hasDisputed = await _dbContext.Milestones.AnyAsync(m => m.ProjectId == project.Id && m.Id != milestone.Id && m.Status == MilestoneStatus.DISPUTED);
        if (!hasDisputed)
        {
            project.Status = ProjectStatus.ACTIVE;
        }

        await _dbContext.SaveChangesAsync();

        try
        {
            await _notificationService.SendNotificationAsync(
                dispute.OpenedBy,
                "Dispute resolved",
                $"Your dispute for project {project.Title} has been resolved.",
                "DISPUTE_RESOLVED",
                $"/disputes/{dispute.Id}"
            );

            await _notificationService.SendNotificationAsync(
                dispute.AgainstUserId,
                "Dispute resolved",
                $"The dispute for project {project.Title} has been resolved.",
                "DISPUTE_RESOLVED",
                $"/disputes/{dispute.Id}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send dispute resolved notification for dispute {DisputeId}", dispute.Id);
        }

        return await GetDisputeByIdAsync(adminId, dispute.Id);
    }

    public async Task<Response.DisputeResponse> CloseDisputeAsync(Guid userId, Guid disputeId)
    {
        var dispute = await _dbContext.Disputes
            .Include(d => d.Project)
            .Include(d => d.Milestone)
            .FirstOrDefaultAsync(d => d.Id == disputeId);

        if (dispute == null) throw new NotFoundException("Dispute not found.");
        if (dispute.OpenedBy != userId) throw new ForbiddenException("Only the user who opened the dispute can close it.");
        if (dispute.Status == DisputeStatus.RESOLVED) throw new ValidationException("Dispute is already resolved.");
        if (dispute.Status == DisputeStatus.CLOSED) throw new ValidationException("Dispute is already closed.");

        var project = dispute.Project;
        var milestone = dispute.Milestone;
        dispute.Status = DisputeStatus.CLOSED;
        dispute.ResolvedAt = DateTimeOffset.UtcNow;

        // Revert milestone status
        milestone.Status = MilestoneStatus.IN_PROGRESS;

        // Recalculate project status
        var hasDisputed = await _dbContext.Milestones.AnyAsync(m => m.ProjectId == project.Id && m.Id != milestone.Id && m.Status == MilestoneStatus.DISPUTED);
        if (!hasDisputed)
        {
            project.Status = ProjectStatus.ACTIVE;
        }

        await _dbContext.SaveChangesAsync();

        try
        {
            await _notificationService.SendNotificationAsync(
                dispute.AgainstUserId,
                "Dispute closed",
                $"The dispute for project {project.Title} was closed by the user.",
                "DISPUTE_CLOSED",
                $"/disputes/{dispute.Id}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send dispute closed notification for dispute {DisputeId}", dispute.Id);
        }

        return await GetDisputeByIdAsync(userId, dispute.Id);
    }

    public async Task<Response.DisputeResponse> RequestEvidenceAsync(Guid adminId, Guid disputeId, Request.RequestEvidenceRequest request)
    {
        var dispute = await _dbContext.Disputes
            .Include(d => d.Project)
            .FirstOrDefaultAsync(d => d.Id == disputeId);

        if (dispute == null) throw new NotFoundException("Dispute not found.");
        if (dispute.Status == DisputeStatus.RESOLVED || dispute.Status == DisputeStatus.CLOSED)
            throw new ValidationException("Dispute is already resolved or closed.");

        dispute.Status = DisputeStatus.UNDER_REVIEW;
        await _dbContext.SaveChangesAsync();

        try
        {
            await _notificationService.SendNotificationAsync(
                dispute.OpenedBy,
                "Additional evidence requested",
                $"An admin has requested additional evidence for your dispute: {request.Note}",
                "DISPUTE_EVIDENCE_REQUESTED",
                $"/disputes/{dispute.Id}"
            );

            await _notificationService.SendNotificationAsync(
                dispute.AgainstUserId,
                "Additional evidence requested",
                $"An admin has requested additional evidence for the dispute against you: {request.Note}",
                "DISPUTE_EVIDENCE_REQUESTED",
                $"/disputes/{dispute.Id}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send additional evidence requested notification for dispute {DisputeId}", dispute.Id);
        }

        return await GetDisputeByIdAsync(adminId, dispute.Id);
    }

    public async Task DeleteEvidenceAsync(Guid userId, Guid disputeId, Guid evidenceId)
    {
        var dispute = await _dbContext.Disputes.FindAsync(disputeId);
        if (dispute == null) throw new NotFoundException("Dispute not found.");
        if (dispute.Status == DisputeStatus.RESOLVED || dispute.Status == DisputeStatus.CLOSED)
            throw new ValidationException("Cannot delete evidence from a closed dispute.");

        var evidence = await _dbContext.DisputeEvidences.FindAsync(evidenceId);
        if (evidence == null || evidence.DisputeId != disputeId) throw new NotFoundException("Evidence not found.");

        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null) throw new UnauthorizedException("User not found.");
        if (user.Role != UserRole.ADMIN && evidence.SubmittedBy != userId)
            throw new ForbiddenException("You are not authorized to delete this evidence.");

        _dbContext.DisputeEvidences.Remove(evidence);
        await _dbContext.SaveChangesAsync();
    }
}
