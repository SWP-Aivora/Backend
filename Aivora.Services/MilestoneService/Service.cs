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
            .Include(m => m.Steps)
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
            .Include(m => m.Steps)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");
        if (milestone.Project.ClientId != userId) throw new UnauthorizedException("Only the client can update milestones.");

        if (milestone.Status != MilestoneStatus.CREATED)
        {
            if (request.Title != null ||
                request.Description != null ||
                request.AcceptanceCriteria != null ||
                request.Amount.HasValue ||
                request.OrderIndex.HasValue)
            {
                throw new ValidationException("Only DueDate can be updated on active milestones.");
            }
        }

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
        // Sử dụng Treasury để xử lý logic phức tạp (PayDepositAsync)
        await _treasury.PayDepositAsync(userId, milestoneId);

        // Lấy dữ liệu từ change tracker sau khi Treasury xử lý xong.
        // Treasury đã load/create các entity này trong cùng một DbContext (scoped),
        // nên chúng đã được tracked. Dùng .Local thay vì query DB để tránh
        // lỗi "Sequence contains no elements" do connection pool / transaction visibility.
        var milestone = _dbContext.Milestones.Local
            .FirstOrDefault(m => m.Id == milestoneId);
        var clientWallet = _dbContext.Wallets.Local
            .FirstOrDefault(w => w.UserId == userId);
        var payment = _dbContext.Payments.Local
            .FirstOrDefault(p => p.MilestoneId == milestoneId && p.Status == PaymentStatus.RELEASED);

        if (milestone == null)
            throw new InvalidOperationException($"Milestone {milestoneId} not tracked after funding.");

        if (!_dbContext.Entry(milestone).Collection(m => m.Steps).IsLoaded)
        {
            await _dbContext.Entry(milestone).Collection(m => m.Steps).LoadAsync();
        }
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
        await _treasury.PayRemainingAsync(userId, milestoneId);

        // Read from the change tracker rather than re-querying the DB. Treasury loads/
        // mutates this same tracked entity in the same scoped DbContext, so .Local already
        // reflects the committed state. A fresh query here risks the same "Sequence
        // contains no elements" failure that FundMilestoneAsync hit (see commit eae5221).
        milestone = _dbContext.Milestones.Local.FirstOrDefault(m => m.Id == milestoneId);
        if (milestone == null)
            throw new InvalidOperationException($"Milestone {milestoneId} not tracked after release.");

        if (!_dbContext.Entry(milestone).Collection(m => m.Steps).IsLoaded)
        {
            await _dbContext.Entry(milestone).Collection(m => m.Steps).LoadAsync();
        }
        return MapToResponse(milestone);
    }

    public async Task<Response.MilestoneResponse> RequestRevisionAsync(Guid userId, Guid milestoneId, string reason)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
            .Include(m => m.Steps)
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

    public async Task<List<Response.MilestoneStepResponse>> GetMilestoneStepsAsync(Guid userId, Guid milestoneId)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");

        if (milestone.Project.ClientId != userId && milestone.Project.ExpertId != userId)
            throw new UnauthorizedException("Access denied.");

        return await _dbContext.MilestoneSteps
            .AsNoTracking()
            .Where(s => s.MilestoneId == milestoneId)
            .OrderBy(s => s.OrderIndex)
            .Select(s => new Response.MilestoneStepResponse
            {
                Id = s.Id,
                MilestoneId = s.MilestoneId,
                Title = s.Title,
                Description = s.Description,
                OrderIndex = s.OrderIndex,
                Status = s.Status,
                DueDate = s.DueDate,
                CompletedAt = s.CompletedAt,
                CompletedByUserId = s.CompletedByUserId
            })
            .ToListAsync();
    }

    public async Task<Response.MilestoneStepResponse> AddMilestoneStepAsync(Guid userId, Guid milestoneId, Request.CreateMilestoneStepRequest request)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");

        var project = milestone.Project;
        if (project == null) throw new NotFoundException("Project not found.");

        if (project.ClientId != userId) throw new UnauthorizedException("Only the client can add milestone steps.");

        if (project.Status == ProjectStatus.COMPLETED || project.Status == ProjectStatus.CANCELLED)
            throw new ValidationException("Cannot add steps to a completed or cancelled project.");

        if (milestone.Status == MilestoneStatus.APPROVED ||
            milestone.Status == MilestoneStatus.RELEASED ||
            milestone.Status == MilestoneStatus.COMPLETED ||
            milestone.Status == MilestoneStatus.REFUNDED)
            throw new ValidationException("Cannot add steps to a finalized milestone.");

        var step = new MilestoneStep
        {
            MilestoneId = milestoneId,
            Title = request.Title,
            Description = request.Description,
            OrderIndex = request.OrderIndex,
            Status = MilestoneStepStatus.PENDING,
            DueDate = request.DueDate
        };

        _dbContext.MilestoneSteps.Add(step);
        await _dbContext.SaveChangesAsync();

        return new Response.MilestoneStepResponse
        {
            Id = step.Id,
            MilestoneId = step.MilestoneId,
            Title = step.Title,
            Description = step.Description,
            OrderIndex = step.OrderIndex,
            Status = step.Status,
            DueDate = step.DueDate
        };
    }

    public async Task<Response.MilestoneStepResponse> UpdateMilestoneStepAsync(Guid userId, Guid stepId, Request.UpdateMilestoneStepRequest request)
    {
        var step = await _dbContext.MilestoneSteps
            .Include(s => s.Milestone)
            .ThenInclude(m => m.Project)
            .FirstOrDefaultAsync(s => s.Id == stepId);

        if (step == null) throw new NotFoundException("Milestone step not found.");

        if (step.Milestone.Project.ClientId != userId)
            throw new UnauthorizedException("Only the client can update milestone steps.");

        if (step.Milestone.Project.Status == ProjectStatus.COMPLETED || step.Milestone.Project.Status == ProjectStatus.CANCELLED)
            throw new ValidationException("Cannot modify steps in a completed or cancelled project.");

        if (step.Milestone.Status == MilestoneStatus.APPROVED ||
            step.Milestone.Status == MilestoneStatus.RELEASED ||
            step.Milestone.Status == MilestoneStatus.COMPLETED ||
            step.Milestone.Status == MilestoneStatus.REFUNDED)
            throw new ValidationException("Cannot modify steps for a finalized milestone.");

        if (request.Title != null) step.Title = request.Title;
        if (request.Description != null) step.Description = request.Description;
        if (request.DueDate.HasValue) step.DueDate = request.DueDate.Value;
        if (request.OrderIndex.HasValue) step.OrderIndex = request.OrderIndex.Value;

        await _dbContext.SaveChangesAsync();

        return new Response.MilestoneStepResponse
        {
            Id = step.Id,
            MilestoneId = step.MilestoneId,
            Title = step.Title,
            Description = step.Description,
            OrderIndex = step.OrderIndex,
            Status = step.Status,
            DueDate = step.DueDate,
            CompletedAt = step.CompletedAt,
            CompletedByUserId = step.CompletedByUserId
        };
    }

    public async Task DeleteMilestoneStepAsync(Guid userId, Guid stepId)
    {
        var step = await _dbContext.MilestoneSteps
            .Include(s => s.Milestone)
            .ThenInclude(m => m.Project)
            .FirstOrDefaultAsync(s => s.Id == stepId);

        if (step == null) throw new NotFoundException("Milestone step not found.");

        if (step.Milestone.Project.ClientId != userId)
            throw new UnauthorizedException("Only the client can delete milestone steps.");

        if (step.Milestone.Project.Status == ProjectStatus.COMPLETED || step.Milestone.Project.Status == ProjectStatus.CANCELLED)
            throw new ValidationException("Cannot modify steps in a completed or cancelled project.");

        if (step.Milestone.Status == MilestoneStatus.APPROVED ||
            step.Milestone.Status == MilestoneStatus.RELEASED ||
            step.Milestone.Status == MilestoneStatus.COMPLETED ||
            step.Milestone.Status == MilestoneStatus.REFUNDED)
            throw new ValidationException("Cannot modify steps for a finalized milestone.");

        _dbContext.MilestoneSteps.Remove(step);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<Response.MilestoneStepResponse> UpdateStepStatusAsync(Guid userId, Guid stepId, Request.UpdateStepStatusRequest request)
    {
        var step = await _dbContext.MilestoneSteps
            .Include(s => s.Milestone)
            .ThenInclude(m => m.Project)
            .FirstOrDefaultAsync(s => s.Id == stepId);

        if (step == null) throw new NotFoundException("Milestone step not found.");

        var project = step.Milestone.Project;

        if (project.ClientId != userId && project.ExpertId != userId)
            throw new UnauthorizedException("Access denied.");

        if (project.Status == ProjectStatus.COMPLETED || project.Status == ProjectStatus.CANCELLED)
            throw new ValidationException("Cannot modify steps in a completed or cancelled project.");

        if (step.Milestone.Status == MilestoneStatus.APPROVED ||
            step.Milestone.Status == MilestoneStatus.RELEASED ||
            step.Milestone.Status == MilestoneStatus.COMPLETED ||
            step.Milestone.Status == MilestoneStatus.REFUNDED)
            throw new ValidationException("Cannot modify steps for a finalized milestone.");

        if (request.Status != step.Status)
        {
            if (step.Status == MilestoneStepStatus.COMPLETED || step.Status == MilestoneStepStatus.SKIPPED)
            {
                throw new ValidationException("Cannot change status of a completed or skipped step.");
            }

            if (request.Status == MilestoneStepStatus.PENDING)
            {
                throw new ValidationException("Cannot transition step back to PENDING.");
            }

            if (request.Status == MilestoneStepStatus.IN_PROGRESS || request.Status == MilestoneStepStatus.COMPLETED)
            {
                if (project.ExpertId != userId)
                    throw new UnauthorizedException("Only the expert can start or complete steps.");

                if (request.Status == MilestoneStepStatus.COMPLETED)
                {
                    step.CompletedAt = DateTimeOffset.UtcNow;
                    step.CompletedByUserId = userId;
                }
            }
            else if (request.Status == MilestoneStepStatus.SKIPPED)
            {
                if (project.ClientId != userId)
                    throw new UnauthorizedException("Only the client can skip steps.");
            }
            else
            {
                throw new ValidationException("Invalid status transition.");
            }

            step.Status = request.Status;
            await _dbContext.SaveChangesAsync();
        }

        return new Response.MilestoneStepResponse
        {
            Id = step.Id,
            MilestoneId = step.MilestoneId,
            Title = step.Title,
            Description = step.Description,
            OrderIndex = step.OrderIndex,
            Status = step.Status,
            DueDate = step.DueDate,
            CompletedAt = step.CompletedAt,
            CompletedByUserId = step.CompletedByUserId
        };
    }

    public async Task ReorderMilestoneStepsAsync(Guid userId, Guid milestoneId, List<Guid> stepIds)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");

        if (milestone.Project.ClientId != userId)
            throw new UnauthorizedException("Only the client can reorder steps.");

        if (milestone.Project.Status == ProjectStatus.COMPLETED || milestone.Project.Status == ProjectStatus.CANCELLED)
            throw new ValidationException("Cannot modify steps in a completed or cancelled project.");

        if (milestone.Status == MilestoneStatus.APPROVED ||
            milestone.Status == MilestoneStatus.RELEASED ||
            milestone.Status == MilestoneStatus.COMPLETED ||
            milestone.Status == MilestoneStatus.REFUNDED)
            throw new ValidationException("Cannot modify steps for a finalized milestone.");

        var steps = await _dbContext.MilestoneSteps
            .Where(s => s.MilestoneId == milestoneId)
            .ToListAsync();

        var dbStepIds = steps.Select(s => s.Id).ToHashSet();

        if (stepIds.Distinct().Count() != dbStepIds.Count || !stepIds.All(dbStepIds.Contains))
        {
            throw new ValidationException("All step IDs must be provided for reordering.");
        }

        for (int i = 0; i < stepIds.Count; i++)
        {
            var stepId = stepIds[i];
            var step = steps.FirstOrDefault(s => s.Id == stepId);
            if (step != null)
            {
                step.OrderIndex = i + 1;
            }
        }

        await _dbContext.SaveChangesAsync();
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
            FundedAt = m.FundedAt,
            DepositPaidAt = m.DepositPaidAt,
            SubmittedAt = m.SubmittedAt,
            ApprovedAt = m.ApprovedAt,
            PaidAt = m.PaidAt,
            ReleasedAt = m.ReleasedAt,
            Steps = m.Steps?.Select(s => new Response.MilestoneStepResponse
            {
                Id = s.Id,
                MilestoneId = s.MilestoneId,
                Title = s.Title,
                Description = s.Description,
                OrderIndex = s.OrderIndex,
                Status = s.Status,
                DueDate = s.DueDate,
                CompletedAt = s.CompletedAt,
                CompletedByUserId = s.CompletedByUserId
            }).ToList()
        };
    }
}
