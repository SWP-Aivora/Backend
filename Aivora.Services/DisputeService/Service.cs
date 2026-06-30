using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.NotificationService;
using Aivora.Services.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.DisputeService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;
    private readonly ITreasury _treasury;
    private readonly NotificationService.IService _notificationService;

    public Service(AivoraDbContext dbContext, ITreasury treasury, NotificationService.IService notificationService)
    {
        _dbContext = dbContext;
        _treasury = treasury;
        _notificationService = notificationService;
    }

    public async Task<Response.DisputeResponse> OpenDisputeAsync(Guid userId, Request.OpenDisputeRequest request)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == request.MilestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");
        if (milestone.Project.ClientId != userId && milestone.Project.ExpertId != userId)
            throw new UnauthorizedException("You are not authorized to open a dispute for this project.");

        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestone.Id && p.Status == PaymentStatus.HELD);
        if (payment == null) throw new ValidationException("Only funded milestones with held payments can be disputed.");

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

            milestone.Status = MilestoneStatus.DISPUTED;
            milestone.Project.Status = ProjectStatus.DISPUTED;

            // Freeze funds when dispute is opened
            await _treasury.FreezeFundsAsync(milestone.Id, "Dispute opened");

            _dbContext.Disputes.Add(dispute);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            // Gửi thông báo cho bên bị khiếu nại
            try
            {
                await _notificationService.SendNotificationAsync(
                    againstUserId,
                    "Một tranh chấp đã được mở",
                    $"Một tranh chấp đã được mở liên quan đến dự án của bạn. Lý do: {request.Reason}",
                    "DISPUTE",
                    $"/disputes/{dispute.Id}"
                );
            }
            catch
            {
                // Notification failure should not block the main business flow
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
        if (user!.Role != UserRole.ADMIN && dispute.OpenedBy != userId && dispute.AgainstUserId != userId)
            throw new UnauthorizedException("You are not authorized to view this dispute.");

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
            AgainstUserName = againstUser!.FullName,
            Reason = dispute.Reason,
            Description = dispute.Description,
            Status = dispute.Status.ToString(),
            ResolutionType = dispute.ResolutionType?.ToString(),
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
        if (user!.Role != UserRole.ADMIN && dispute.OpenedBy != userId && dispute.AgainstUserId != userId)
            throw new UnauthorizedException("You are not authorized to add evidence to this dispute.");

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
            .Include(d => d.Payment)
            .FirstOrDefaultAsync(d => d.Id == disputeId);

        if (dispute == null) throw new NotFoundException("Dispute not found.");
        if (dispute.Status == DisputeStatus.RESOLVED) throw new ValidationException("Dispute is already resolved.");

        var project = dispute.Project;
        var milestone = dispute.Milestone;

        // Let Treasury handle the transaction - don't open nested transactions
        switch (request.ResolutionType)
        {
            case DisputeResolutionType.RELEASE_TO_EXPERT:
                await _treasury.ReleaseMilestoneAsync(milestone.Project.ClientId, milestone.Id);
                break;

            case DisputeResolutionType.REFUND_TO_CLIENT:
                await _treasury.RefundMilestoneAsync(adminId, milestone.Id, dispute.Payment.Amount, $"Dispute resolved: Refund to client. Ref: {dispute.Id}");
                break;

            case DisputeResolutionType.SPLIT_PAYMENT:
                await _treasury.SplitMilestoneFundsAsync(milestone.Id, request.ReleaseAmount ?? 0, request.RefundAmount ?? 0, $"Dispute resolved: Split payment. Ref: {dispute.Id}");
                break;

            default:
                throw new ValidationException($"'{request.ResolutionType}' is not a valid resolution type. Use RELEASE_TO_EXPERT, REFUND_TO_CLIENT, or SPLIT_PAYMENT.");
        }

        // Update dispute status after successful Treasury operation
        dispute.Status = DisputeStatus.RESOLVED;
        dispute.ResolutionType = request.ResolutionType;
        dispute.ResolutionNote = request.ResolutionNote;
        dispute.ResolvedAt = DateTimeOffset.UtcNow;
        dispute.AdminId = adminId;

        await _dbContext.SaveChangesAsync();

        // Gửi thông báo kết quả phân xử cho cả 2 bên
        var resolutionMessage = $"Tranh chấp đã được admin phân xử. Kết quả: {request.ResolutionType}. Ghi chú: {request.ResolutionNote ?? "Không có"}";
        try
        {
            await _notificationService.SendNotificationAsync(
                dispute.OpenedBy,
                "Tranh chấp đã được phân xử",
                resolutionMessage,
                "DISPUTE",
                $"/disputes/{disputeId}"
            );
        }
        catch { }

        try
        {
            await _notificationService.SendNotificationAsync(
                dispute.AgainstUserId,
                "Tranh chấp đã được phân xử",
                resolutionMessage,
                "DISPUTE",
                $"/disputes/{disputeId}"
            );
        }
        catch { }

        return await GetDisputeByIdAsync(adminId, disputeId);
    }

    public async Task<Response.DisputeResponse> CloseDisputeAsync(Guid userId, Guid disputeId)
    {
        var dispute = await _dbContext.Disputes
            .Include(d => d.Project)
            .Include(d => d.Milestone)
            .Include(d => d.Payment)
            .FirstOrDefaultAsync(d => d.Id == disputeId);

        if (dispute == null) throw new NotFoundException("Dispute not found.");
        if (dispute.OpenedBy != userId)
            throw new UnauthorizedException("You are not authorized to close this dispute.");
        if (dispute.Status == DisputeStatus.RESOLVED)
            throw new ValidationException("Dispute is already resolved.");
        if (dispute.Status == DisputeStatus.CLOSED)
            throw new ValidationException("Dispute is already closed.");

        // Unfreeze payment if it was frozen (backward compatible: old disputes have HELD)
        if (dispute.Payment.Status == PaymentStatus.FROZEN)
        {
            await _treasury.UnfreezeFundsAsync(dispute.MilestoneId, "Dispute closed");
        }

        dispute.Status = DisputeStatus.CLOSED;

        // Revert milestone from DISPUTED back to IN_PROGRESS
        // so the expert must resubmit the deliverable
        dispute.Milestone.Status = MilestoneStatus.IN_PROGRESS;

        await _dbContext.SaveChangesAsync();

        // Recalculate project status based on milestone states
        await _treasury.SyncProjectStatusAsync(dispute.ProjectId);

        // Gửi thông báo cho bên còn lại rằng tranh chấp đã được đóng
        try
        {
            await _notificationService.SendNotificationAsync(
                dispute.AgainstUserId,
                "Tranh chấp đã được đóng",
                "Tranh chấp đã được đóng bởi người khiếu nại. Bạn có thể tiếp tục làm việc trên milestone.",
                "DISPUTE",
                $"/disputes/{disputeId}"
            );
        }
        catch
        {
            // Notification failure should not block the main business flow
        }

        return await GetDisputeByIdAsync(userId, disputeId);
    }

    public async Task<Response.DisputeResponse> RequestEvidenceAsync(Guid adminId, Guid disputeId, Request.RequestEvidenceRequest request)
    {
        var dispute = await _dbContext.Disputes
            .Include(d => d.Project)
            .Include(d => d.Milestone)
            .FirstOrDefaultAsync(d => d.Id == disputeId);

        if (dispute == null) throw new NotFoundException("Dispute not found.");
        if (dispute.Status == DisputeStatus.RESOLVED)
            throw new ValidationException("Dispute is already resolved.");
        if (dispute.Status == DisputeStatus.CLOSED)
            throw new ValidationException("Dispute is already closed.");

        if (dispute.Status == DisputeStatus.OPEN)
        {
            dispute.Status = DisputeStatus.UNDER_REVIEW;
        }

        // Send notification to the dispute opener
        try
        {
            await _notificationService.SendNotificationAsync(
                dispute.OpenedBy,
                "Yêu cầu bổ sung bằng chứng",
                request.Note,
                "DISPUTE",
                $"/disputes/{disputeId}"
            );
        }
        catch
        {
            // Notification failure should not block the main business flow
        }

        await _dbContext.SaveChangesAsync();

        return await GetDisputeByIdAsync(adminId, disputeId);
    }

    public async Task DeleteEvidenceAsync(Guid userId, Guid disputeId, Guid evidenceId)
    {
        var evidence = await _dbContext.DisputeEvidences
            .Include(e => e.Dispute)
            .FirstOrDefaultAsync(e => e.Id == evidenceId);

        if (evidence == null || evidence.DisputeId != disputeId)
            throw new NotFoundException("Evidence not found.");

        if (evidence.Dispute.OpenedBy != userId && evidence.SubmittedBy != userId)
            throw new UnauthorizedException("You are not authorized to delete evidence from this dispute.");

        if (evidence.Dispute.Status == DisputeStatus.RESOLVED)
            throw new ValidationException("Cannot delete evidence from a closed dispute.");
        if (evidence.Dispute.Status == DisputeStatus.CLOSED)
            throw new ValidationException("Cannot delete evidence from a closed dispute.");

        _dbContext.DisputeEvidences.Remove(evidence);
        await _dbContext.SaveChangesAsync();
    }
}
