using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.MilestoneService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;

    public Service(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
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
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");
        if (milestone.Project.ClientId != userId) throw new UnauthorizedException("Only the client can fund milestones.");
        if (milestone.Status != MilestoneStatus.CREATED)
            throw new ValidationException("Milestone is already funded or processed.");

        var clientWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (clientWallet == null) throw new NotFoundException("Client wallet not found.");

        if (clientWallet.AvailableBalance < milestone.Amount)
            throw new ValidationException("Insufficient balance to fund this milestone.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // 1. Update Wallet
            clientWallet.AvailableBalance -= milestone.Amount;
            clientWallet.HeldBalance += milestone.Amount;

            // 2. Create Payment
            var payment = new Payment
            {
                ProjectId = milestone.ProjectId,
                MilestoneId = milestone.Id,
                PayerId = userId,
                PayeeId = milestone.Project.ExpertId,
                Amount = milestone.Amount,
                Currency = milestone.Currency,
                Status = PaymentStatus.HELD,
                HeldAt = DateTimeOffset.UtcNow
            };
            _dbContext.Payments.Add(payment);

            // 3. Create Wallet Transaction for record
            var walletTx = new WalletTransaction
            {
                WalletId = clientWallet.Id,
                UserId = userId,
                Amount = milestone.Amount,
                Type = WalletTransactionType.ESCROW_HOLD,
                Direction = TransactionDirection.DEBIT,
                Description = $"Funded milestone: {milestone.Title}",
                BalanceBefore = clientWallet.AvailableBalance + milestone.Amount, // Current balance was already updated above
                BalanceAfter = clientWallet.AvailableBalance,
                PaymentId = payment.Id
            };
            _dbContext.WalletTransactions.Add(walletTx);

            // 4. Update Milestone
            milestone.Status = MilestoneStatus.FUNDED;
            milestone.FundedAt = DateTimeOffset.UtcNow;

            // 5. Update Project Status if needed
            if (milestone.Project.Status == ProjectStatus.PENDING_PAYMENT)
            {
                milestone.Project.Status = ProjectStatus.ACTIVE;
                milestone.Project.StartDate = DateOnly.FromDateTime(DateTime.UtcNow);
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

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
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Response.MilestoneResponse> ApproveMilestoneAsync(Guid userId, Guid milestoneId)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project).ThenInclude(p => p.Milestones)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");
        if (milestone.Project.ClientId != userId) throw new UnauthorizedException("Only the client can approve milestones.");
        if (milestone.Status != MilestoneStatus.SUBMITTED)
            throw new ValidationException("Milestone must be in SUBMITTED status to be approved.");

        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId && p.Status == PaymentStatus.HELD);
        if (payment == null) throw new NotFoundException("Held payment for this milestone not found.");

        var clientWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        var expertWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == milestone.Project.ExpertId);
        
        if (clientWallet == null || expertWallet == null) throw new NotFoundException("Wallet not found.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // 1. Update Wallets
            clientWallet.HeldBalance -= milestone.Amount;
            expertWallet.AvailableBalance += milestone.Amount;
            expertWallet.TotalEarned += milestone.Amount;

            // 2. Update Payment
            payment.Status = PaymentStatus.RELEASED;
            payment.ReleasedAt = DateTimeOffset.UtcNow;

            // 3. Create Wallet Transactions
            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = clientWallet.Id,
                UserId = userId,
                Amount = milestone.Amount,
                Type = WalletTransactionType.PAYMENT_RELEASE,
                Direction = TransactionDirection.DEBIT,
                Description = $"Payment released for milestone: {milestone.Title}",
                BalanceBefore = clientWallet.HeldBalance + milestone.Amount,
                BalanceAfter = clientWallet.HeldBalance,
                PaymentId = payment.Id
            });

            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = expertWallet.Id,
                UserId = expertWallet.UserId,
                Amount = milestone.Amount,
                Type = WalletTransactionType.PAYMENT_RELEASE,
                Direction = TransactionDirection.CREDIT,
                Description = $"Payment received for milestone: {milestone.Title}",
                BalanceBefore = expertWallet.AvailableBalance - milestone.Amount,
                BalanceAfter = expertWallet.AvailableBalance,
                PaymentId = payment.Id
            });

            // 4. Update Milestone
            milestone.Status = MilestoneStatus.PAID;
            milestone.ApprovedAt = DateTimeOffset.UtcNow;
            milestone.PaidAt = DateTimeOffset.UtcNow;

            // 5. Update Project Status if all milestones paid
            if (milestone.Project.Milestones.All(m => m.Status == MilestoneStatus.PAID))
            {
                milestone.Project.Status = ProjectStatus.COMPLETED;
                milestone.Project.CompletedAt = DateTimeOffset.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return MapToResponse(milestone);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
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

        milestone.Status = MilestoneStatus.REVISION_REQUESTED;
        // Optionally update latest deliverable status if we add that logic
        
        await _dbContext.SaveChangesAsync();
        return MapToResponse(milestone);
    }

    public async Task<bool> OpenDisputeAsync(Guid userId, Guid milestoneId, string reason)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");
        if (milestone.Project.ClientId != userId && milestone.Project.ExpertId != userId)
            throw new UnauthorizedException("Access denied.");

        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId && p.Status == PaymentStatus.HELD);
        if (payment == null) throw new ValidationException("Cannot open dispute for non-funded milestone.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            payment.Status = PaymentStatus.FROZEN;
            payment.FrozenAt = DateTimeOffset.UtcNow;

            milestone.Status = MilestoneStatus.DISPUTED;
            milestone.Project.Status = ProjectStatus.DISPUTED;

            // Create Dispute entity
            var dispute = new Dispute
            {
                ProjectId = milestone.ProjectId,
                MilestoneId = milestoneId,
                PaymentId = payment.Id,
                OpenedBy = userId,
                AgainstUserId = (userId == milestone.Project.ClientId) ? milestone.Project.ExpertId : milestone.Project.ClientId,
                Reason = reason,
                Status = DisputeStatus.OPEN
            };
            _dbContext.Disputes.Add(dispute);

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
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
