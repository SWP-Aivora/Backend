using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.NotificationService;
using Aivora.Services.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.MilestoneService;

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

    public async Task<Response.MilestoneResponse> GetMilestoneByIdAsync(Guid userId, Guid milestoneId)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");

        if (milestone.Project.ClientId != userId && milestone.Project.ExpertId != userId)
            throw new UnauthorizedException("Access denied.");

        return MapToResponse(milestone);
    }

    public async Task<Response.MilestoneResponse> CreateMilestoneAsync(Guid userId, Guid projectId, Request.CreateMilestoneRequest request)
    {
        var project = await _dbContext.Projects.FindAsync(projectId);
        if (project == null) throw new NotFoundException("Project not found.");
        if (project.ClientId != userId) throw new UnauthorizedException("Only the client can add milestones.");
        if (project.Status == ProjectStatus.COMPLETED || project.Status == ProjectStatus.CANCELLED)
            throw new ValidationException("Cannot add milestones to a completed or cancelled project.");

        var milestone = new Milestone
        {
            ProjectId = projectId,
            Title = request.Title,
            Description = request.Description,
            AcceptanceCriteria = request.AcceptanceCriteria,
            Amount = request.Amount,
            Currency = request.Currency,
            DueDate = request.DueDate,
            OrderIndex = request.OrderIndex,
            Status = MilestoneStatus.CREATED
        };

        _dbContext.Milestones.Add(milestone);
        await _dbContext.SaveChangesAsync();

        return MapToResponse(milestone);
    }

    public async Task<Response.MilestoneResponse> UpdateMilestoneAsync(Guid userId, Guid milestoneId, Request.UpdateMilestoneRequest request)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");
        if (milestone.Project.ClientId != userId) throw new UnauthorizedException("Only the client can update milestones.");
        if (milestone.Status != MilestoneStatus.CREATED)
            throw new ValidationException("Only CREATED milestones can be updated.");

        if (request.Title != null) milestone.Title = request.Title;
        if (request.Description != null) milestone.Description = request.Description;
        if (request.AcceptanceCriteria != null) milestone.AcceptanceCriteria = request.AcceptanceCriteria;
        if (request.Amount.HasValue) milestone.Amount = request.Amount.Value;
        if (request.DueDate.HasValue) milestone.DueDate = request.DueDate.Value;
        if (request.OrderIndex.HasValue) milestone.OrderIndex = request.OrderIndex.Value;

        await _dbContext.SaveChangesAsync();
        return MapToResponse(milestone);
    }

    public async Task<Response.FundResultResponse> FundMilestoneAsync(Guid userId, Guid milestoneId)
    {
        // Sử dụng Treasury để xử lý logic phức tạp
        await _treasury.FundMilestoneAsync(userId, milestoneId);

        // Lấy dữ liệu từ change tracker sau khi Treasury xử lý xong.
        // Treasury đã load/create các entity này trong cùng một DbContext (scoped),
        // nên chúng đã được tracked. Dùng .Local thay vì query DB để tránh
        // lỗi "Sequence contains no elements" do connection pool / transaction visibility.
        var milestone = _dbContext.Milestones.Local
            .FirstOrDefault(m => m.Id == milestoneId);
        var clientWallet = _dbContext.Wallets.Local
            .FirstOrDefault(w => w.UserId == userId);
        var payment = _dbContext.Payments.Local
            .FirstOrDefault(p => p.MilestoneId == milestoneId && p.Status == PaymentStatus.HELD);

        if (milestone == null)
            throw new InvalidOperationException($"Milestone {milestoneId} not tracked after funding.");
        if (clientWallet == null)
            throw new InvalidOperationException($"Wallet for user {userId} not tracked after funding.");
        if (payment == null)
            throw new InvalidOperationException($"Payment for milestone {milestoneId} not tracked after funding.");

        return new Response.FundResultResponse
        {
            Milestone = MapToResponse(milestone),
            Payment = new Response.PaymentInfo
            {
                Id = payment.Id,
                ProjectId = payment.ProjectId,
                MilestoneId = payment.MilestoneId,
                PayerId = payment.PayerId,
                PayeeId = payment.PayeeId,
                Amount = payment.Amount,
                Currency = payment.Currency,
                Status = payment.Status.ToString(),
                HeldAt = payment.HeldAt
            },
            Wallet = new Response.WalletInfo
            {
                AvailableBalance = clientWallet.AvailableBalance,
                HeldBalance = clientWallet.HeldBalance,
                Currency = clientWallet.Currency
            }
        };
    }

    public async Task<Response.MilestoneResponse> ApproveMilestoneAsync(Guid userId, Guid milestoneId)
    {
        // Validate milestone status at service layer before delegating to Treasury.
        // Deliberately narrower than Treasury.ReleaseMilestoneAsync's own check (SUBMITTED
        // or DISPUTED): a client is only allowed to approve directly while SUBMITTED. Once
        // DISPUTED, release must go through admin dispute resolution
        // (DisputeService.ResolveDisputeAsync calls Treasury.ReleaseMilestoneAsync directly).
        var milestone = await _dbContext.Milestones.FirstOrDefaultAsync(m => m.Id == milestoneId);
        if (milestone == null) throw new NotFoundException("Milestone not found.");
        if (milestone.Status != MilestoneStatus.SUBMITTED)
            throw new ValidationException("Milestone must be in SUBMITTED status to be approved.");

        var hasActiveDispute = await _dbContext.Disputes
            .AnyAsync(d => d.MilestoneId == milestoneId &&
                          (d.Status == DisputeStatus.OPEN || d.Status == DisputeStatus.UNDER_REVIEW));

        if (hasActiveDispute)
            throw new ValidationException("Cannot approve milestone while there is an active dispute.");

        // Delegate to Treasury which handles all persistence internally
        // (opens its own transaction, calls SaveChangesAsync, commits).
        await _treasury.ReleaseMilestoneAsync(userId, milestoneId);

        // Read from the change tracker rather than re-querying the DB. Treasury loads/
        // mutates this same tracked entity in the same scoped DbContext, so .Local already
        // reflects the committed state. A fresh query here risks the same "Sequence
        // contains no elements" failure that FundMilestoneAsync hit (see commit eae5221).
        milestone = _dbContext.Milestones.Local.FirstOrDefault(m => m.Id == milestoneId);
        if (milestone == null)
            throw new InvalidOperationException($"Milestone {milestoneId} not tracked after release.");
        return MapToResponse(milestone);
    }

    public async Task<Response.MilestoneResponse> RequestRevisionAsync(Guid userId, Guid milestoneId, string reason)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");
        if (milestone.Project.ClientId != userId) throw new UnauthorizedException("Only the client can request revisions.");
        if (milestone.Status != MilestoneStatus.SUBMITTED)
            throw new ValidationException("Milestone must be SUBMITTED to request revision.");

        // No Treasury/Payment involvement here: funds stay HELD for the entire
        // SUBMITTED <-> REVISION_REQUESTED cycle (nothing has been released yet), so there
        // is no money movement to record — only the milestone/project status changes.
        milestone.Status = MilestoneStatus.REVISION_REQUESTED;

        await _dbContext.SaveChangesAsync();

        // Send notification to the Expert that the client requested a revision
        try
        {
            await _notificationService.SendNotificationAsync(
                milestone.Project.ExpertId,
                "Client requested a revision",
                $"The client has requested a revision for milestone \"{milestone.Title}\". Reason: {reason}",
                "MILESTONE",
                $"/projects/{milestone.ProjectId}/milestones/{milestoneId}"
            );
        }
        catch
        {
            // Notification failure should not block the main business flow
        }

        await _treasury.SyncProjectStatusAsync(milestone.ProjectId);

        return MapToResponse(milestone);
    }

    private static Response.MilestoneResponse MapToResponse(Milestone m)
    {
        return new Response.MilestoneResponse
        {
            Id = m.Id,
            ProjectId = m.ProjectId,
            Title = m.Title,
            Description = m.Description,
            AcceptanceCriteria = m.AcceptanceCriteria,
            Amount = m.Amount,
            Currency = m.Currency,
            Status = m.Status,
            DueDate = m.DueDate,
            OrderIndex = m.OrderIndex,
            CreatedAt = m.CreatedAt,
            FundedAt = m.FundedAt
        };
    }
}
