using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.AIMilestoneStepAssistantService;
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
    private readonly IAIMilestoneStepSuggestionProvider _stepSuggestionProvider;

    public Service(
        AivoraDbContext dbContext,
        ITreasury treasury,
        NotificationService.IService notificationService,
        IAIMilestoneStepSuggestionProvider stepSuggestionProvider)
    {
        _dbContext = dbContext;
        _treasury = treasury;
        _notificationService = notificationService;
        _stepSuggestionProvider = stepSuggestionProvider;
    }

    public async Task<Response.MilestoneResponse> GetMilestoneByIdAsync(Guid userId, Guid milestoneId)
    {
        var milestone = await _dbContext.Milestones
            .AsNoTracking()
            .Include(m => m.Project)
            .Include(m => m.Steps)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");

        if (milestone.Project == null) throw new NotFoundException("Project not found.");

        if (milestone.Project.ClientId != userId && milestone.Project.ExpertId != userId)
            throw new ForbiddenException("Access denied.");

        return MapToResponse(milestone);
    }

    public async Task<Response.MilestoneResponse> CreateMilestoneAsync(Guid userId, Guid projectId, Request.CreateMilestoneRequest request)
    {
        var project = await _dbContext.Projects.FindAsync(projectId);
        if (project == null) throw new NotFoundException("Project not found.");
        if (project.ClientId != userId) throw new ForbiddenException("Only the client can add milestones.");
        if (project.IsClosed)
            throw new ValidationException("Cannot add milestones to a completed or cancelled project.");

        ValidateLength(request.Title, 255, "Title");
        ValidateLength(request.Description, 1000, "Description");
        ValidateLength(request.AcceptanceCriteria, 2000, "AcceptanceCriteria");

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ValidationException("Title is required.");

        if (request.Amount <= 0)
            throw new ValidationException("Amount must be greater than 0.");

        if (project.TotalBudget.HasValue)
        {
            var existingAmount = await _dbContext.Milestones
                .Where(m => m.ProjectId == projectId)
                .SumAsync(m => m.Amount);

            if (existingAmount + request.Amount > project.TotalBudget.Value)
                throw new ValidationException("Total milestone amount exceeds the project's total budget.");
        }

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
            Status = MilestoneStatus.CREATED,
            Steps = new List<MilestoneStep>
            {
                new MilestoneStep
                {
                    Title = "Created",
                    Description = "Milestone created",
                    OrderIndex = 0,
                    Status = MilestoneStepStatus.COMPLETED,
                    CompletedAt = DateTimeOffset.UtcNow,
                    CompletedByUserId = userId
                }
            }
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
        if (milestone.Project.ClientId != userId) throw new ForbiddenException("Only the client can update milestones.");

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

        ValidateLength(request.Title, 255, "Title");
        ValidateLength(request.Description, 1000, "Description");
        ValidateLength(request.AcceptanceCriteria, 2000, "AcceptanceCriteria");

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
        var result = await _treasury.PayDepositAsync(userId, milestoneId);
        var payment = result.Payments.Single();
        var clientWallet = result.ClientWallet;

        return new Response.FundResultResponse
        {
            Milestone = MapToResponse(result.Milestone),
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
        // Mọi luật chặn release (SUBMITTED, active dispute) sống trong Treasury.
        var result = await _treasury.PayRemainingAsync(userId, milestoneId);
        return MapToResponse(result.Milestone);
    }

    public async Task<Response.MilestoneResponse> RequestRevisionAsync(Guid userId, Guid milestoneId, string reason)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
            .Include(m => m.Steps)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");
        if (milestone.Project.ClientId != userId) throw new ForbiddenException("Only the client can request revisions.");
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
            .AsNoTracking()
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");

        if (milestone.Project == null) throw new NotFoundException("Project not found.");

        if (milestone.Project.ClientId != userId && milestone.Project.ExpertId != userId)
            throw new ForbiddenException("Access denied.");

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
                CompletedByUserId = s.CompletedByUserId,
                BlockedReason = s.BlockedReason
            })
            .ToListAsync();
    }

    public async Task<Response.MilestoneStepSuggestionResponse> SuggestMilestoneStepsAsync(Guid userId, Guid milestoneId, CancellationToken cancellationToken = default)
    {
        // Eagerly load Project to verify ClientId authorization and prevent N+1 queries
        var milestone = await _dbContext.Milestones
            .AsNoTracking()
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == milestoneId, cancellationToken);

        if (milestone == null) throw new NotFoundException("Milestone not found.");

        var project = milestone.Project;
        if (project == null) throw new NotFoundException("Project not found.");

        if (project.ExpertId != userId) throw new ForbiddenException("Only the expert can manage milestone steps.");

        var draft = await _stepSuggestionProvider.GenerateSuggestionAsync(
            new AIMilestoneStepAssistantService.Request.SuggestMilestoneStepsRequest
            {
                Title = milestone.Title,
                Description = milestone.Description,
                AcceptanceCriteria = milestone.AcceptanceCriteria
            },
            cancellationToken);

        var steps = draft?.Steps != null
            ? draft.Steps.Select(s => new Response.SuggestedMilestoneStep { Title = s.Title, Description = s.Description }).ToList()
            : new List<Response.SuggestedMilestoneStep>();

        return new Response.MilestoneStepSuggestionResponse
        {
            Steps = steps,
            AIModel = draft?.AIModel ?? "Unknown"
        };
    }

    private static void ValidateLength(string? value, int maxLength, string fieldName)
    {
        if (value != null && value.Length > maxLength)
            throw new ValidationException($"{fieldName} must not exceed {maxLength} characters.");
    }

    private static bool IsSystemDefaultStep(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        var trimmed = title.Trim();
        return trimmed.Equals("Created", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("Funded", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("Completed", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<Response.MilestoneStepResponse> AddMilestoneStepAsync(Guid userId, Guid milestoneId, Request.CreateMilestoneStepRequest request)
    {
        if (IsSystemDefaultStep(request.Title))
            throw new ValidationException("Cannot create custom steps with reserved system titles ('Created', 'Funded', 'Completed').");

        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");

        var project = milestone.Project;
        if (project == null) throw new NotFoundException("Project not found.");

        if (project.ExpertId != userId) throw new ForbiddenException("Only the expert can manage milestone steps.");

        if (project.IsClosed)
            throw new ValidationException("Cannot add steps to a completed or cancelled project.");

        if (milestone.IsFinalized)
            throw new ValidationException("Cannot add steps to a finalized milestone.");

        ValidateLength(request.Title, 255, "Title");
        ValidateLength(request.Description, 1000, "Description");

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

        if (step == null || step.Milestone == null || step.Milestone.Project == null)
            throw new NotFoundException("Milestone step or project not found.");

        var project = step.Milestone.Project;

        if (project.ExpertId != userId)
            throw new ForbiddenException("Only the expert can manage milestone steps.");

        if (IsSystemDefaultStep(step.Title))
            throw new ValidationException("Cannot modify or delete default system milestone steps.");

        if (request.Title != null && IsSystemDefaultStep(request.Title))
            throw new ValidationException("Cannot rename custom step to a reserved system title.");

        if (project.IsClosed)
            throw new ValidationException("Cannot modify steps in a completed or cancelled project.");

        if (step.Milestone.IsFinalized)
            throw new ValidationException("Cannot modify steps for a finalized milestone.");

        ValidateLength(request.Title, 255, "Title");
        if (request.IsDescriptionSet) ValidateLength(request.Description, 1000, "Description");

        if (request.Title != null) step.Title = request.Title;
        if (request.IsDescriptionSet) step.Description = request.Description;
        if (request.IsDueDateSet) step.DueDate = request.DueDate;
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
            CompletedByUserId = step.CompletedByUserId,
            BlockedReason = step.BlockedReason
        };
    }

    public async Task DeleteMilestoneStepAsync(Guid userId, Guid stepId)
    {
        var step = await _dbContext.MilestoneSteps
            .Include(s => s.Milestone)
                .ThenInclude(m => m.Project)
            .FirstOrDefaultAsync(s => s.Id == stepId);

        if (step == null || step.Milestone == null || step.Milestone.Project == null)
            throw new NotFoundException("Milestone step or project not found.");

        var project = step.Milestone.Project;

        if (project.ExpertId != userId)
            throw new ForbiddenException("Only the expert can manage milestone steps.");

        if (IsSystemDefaultStep(step.Title))
            throw new ValidationException("Cannot modify or delete default system milestone steps.");

        if (project.IsClosed)
            throw new ValidationException("Cannot modify steps in a completed or cancelled project.");

        if (step.Milestone.IsFinalized)
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

        if (step == null || step.Milestone == null || step.Milestone.Project == null)
            throw new NotFoundException("Milestone step or project not found.");

        var project = step.Milestone.Project;

        // Unblocking is the one step action that belongs to the client (they're the one
        // who has to resolve whatever the expert flagged); every other transition is the
        // expert's to make.
        var isUnblockAction = step.Status == MilestoneStepStatus.BLOCKED && request.Status == MilestoneStepStatus.IN_PROGRESS;
        if (isUnblockAction)
        {
            if (project.ClientId != userId)
                throw new ForbiddenException("Only the client can unblock a step.");
        }
        else if (project.ExpertId != userId)
        {
            throw new ForbiddenException("Only the expert can manage milestone steps.");
        }

        if (IsSystemDefaultStep(step.Title))
            throw new ValidationException("Cannot modify or delete default system milestone steps.");

        if (project.IsClosed)
            throw new ValidationException("Cannot modify steps in a completed or cancelled project.");

        if (step.Milestone.IsFinalized)
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

            if (step.Status == MilestoneStepStatus.BLOCKED)
            {
                if (request.Status != MilestoneStepStatus.IN_PROGRESS)
                    throw new ValidationException("A blocked step can only be unblocked back to IN_PROGRESS.");

                step.BlockedReason = null;
            }
            else if (request.Status == MilestoneStepStatus.BLOCKED)
            {
                if (step.Status != MilestoneStepStatus.IN_PROGRESS)
                    throw new ValidationException("Only an in-progress step can be blocked.");

                if (string.IsNullOrWhiteSpace(request.Reason))
                    throw new ValidationException("A reason is required to block a step.");

                ValidateLength(request.Reason, 1000, "Reason");

                step.BlockedReason = request.Reason;
            }
            else if (request.Status == MilestoneStepStatus.COMPLETED)
            {
                step.CompletedAt = DateTimeOffset.UtcNow;
                step.CompletedByUserId = userId;
            }
            else if (request.Status != MilestoneStepStatus.IN_PROGRESS && request.Status != MilestoneStepStatus.SKIPPED)
            {
                throw new ValidationException("Invalid status transition.");
            }

            var wasBlocked = step.Status == MilestoneStepStatus.BLOCKED;
            step.Status = request.Status;
            await _dbContext.SaveChangesAsync();

            if (request.Status == MilestoneStepStatus.BLOCKED)
            {
                try
                {
                    await _notificationService.SendNotificationAsync(
                        project.ClientId,
                        "Step blocked",
                        $"The expert has blocked step \"{step.Title}\" on milestone \"{step.Milestone.Title}\". Reason: {step.BlockedReason}",
                        "MILESTONE",
                        $"/projects/{project.Id}/milestones/{step.MilestoneId}"
                    );
                }
                catch
                {
                    // Notification failure should not block the main business flow
                }
            }
            else if (wasBlocked)
            {
                try
                {
                    await _notificationService.SendNotificationAsync(
                        project.ExpertId,
                        "Step unblocked",
                        $"The client has unblocked step \"{step.Title}\" on milestone \"{step.Milestone.Title}\".",
                        "MILESTONE",
                        $"/projects/{project.Id}/milestones/{step.MilestoneId}"
                    );
                }
                catch
                {
                    // Notification failure should not block the main business flow
                }
            }
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
            CompletedByUserId = step.CompletedByUserId,
            BlockedReason = step.BlockedReason
        };
    }

    public async Task ReorderMilestoneStepsAsync(Guid userId, Guid milestoneId, List<Guid> stepIds)
    {
        var milestone = await _dbContext.Milestones
            .AsNoTracking()
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");
        if (milestone.Project == null) throw new NotFoundException("Project not found.");

        if (milestone.Project.ExpertId != userId)
            throw new ForbiddenException("Only the expert can manage milestone steps.");

        if (stepIds == null || stepIds.Count == 0)
            throw new ValidationException("Step IDs list cannot be empty.");

        if (milestone.Project.IsClosed)
            throw new ValidationException("Cannot modify steps in a completed or cancelled project.");

        if (milestone.IsFinalized)
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
                CompletedByUserId = s.CompletedByUserId,
                BlockedReason = s.BlockedReason
            }).ToList()
        };
    }
}
