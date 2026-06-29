using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aivora.Services.Treasury;

/// <summary>
/// The Treasury (Kho bạc) - Deep Module chịu trách nhiệm duy nhất về tính toàn vẹn tài chính.
/// Hợp nhất toàn bộ logic từ FinancialLedger cũ để đảm bảo tính Locality và Leverage.
/// </summary>
public class Treasury : ITreasury
{
    private readonly AivoraDbContext _dbContext;
    private readonly ILogger<Treasury> _logger;

    public Treasury(AivoraDbContext dbContext, ILogger<Treasury> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task FundMilestoneAsync(Guid clientId, Guid milestoneId)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            await FundMilestoneCoreAsync(clientId, milestoneId);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task FundMilestoneCoreAsync(Guid clientId, Guid milestoneId)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);

        if (milestone.Project.ClientId != clientId) throw new UnauthorizedException("Access denied.");
        if (milestone.Status != MilestoneStatus.CREATED) throw new ValidationException("Milestone is already funded or processed.");

        var wallet = await GetWalletAsync(clientId);
        if (wallet.AvailableBalance < milestone.Amount) throw new ValidationException("Insufficient balance.");

        wallet.AvailableBalance -= milestone.Amount;
        wallet.HeldBalance += milestone.Amount;

        var payment = new Payment
        {
            MilestoneId = milestoneId,
            ProjectId = milestone.ProjectId,
            PayerId = clientId,
            PayeeId = milestone.Project.ExpertId,
            Amount = milestone.Amount,
            Currency = wallet.Currency,
            Status = PaymentStatus.HELD,
            HeldAt = DateTimeOffset.UtcNow
        };
        _dbContext.Payments.Add(payment);

        _dbContext.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id,
            UserId = clientId,
            Amount = milestone.Amount,
            Type = WalletTransactionType.ESCROW_HOLD,
            Direction = TransactionDirection.DEBIT,
            Description = $"Funding milestone: {milestone.Title}",
            BalanceBefore = wallet.AvailableBalance + milestone.Amount,
            BalanceAfter = wallet.AvailableBalance,
            PaymentId = payment.Id
        });

        milestone.Status = MilestoneStatus.FUNDED;
        milestone.FundedAt = DateTimeOffset.UtcNow;

        if (milestone.Project.Status == ProjectStatus.PENDING_PAYMENT)
        {
            milestone.Project.Status = ProjectStatus.ACTIVE;
            milestone.Project.StartDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("✅ Milestone {MilestoneId} funded successfully by Client {ClientId}", milestoneId, clientId);
    }

    public async Task ReleaseMilestoneAsync(Guid clientId, Guid milestoneId)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            await ReleaseMilestoneCoreAsync(clientId, milestoneId);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task ReleaseMilestoneCoreAsync(Guid clientId, Guid milestoneId)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);

        if (milestone.Project.ClientId != clientId) throw new UnauthorizedException("Only the client can approve and release funds.");
        if (milestone.Status != MilestoneStatus.SUBMITTED && milestone.Status != MilestoneStatus.DISPUTED)
            throw new ValidationException("Milestone must be in SUBMITTED or DISPUTED status to be released.");

        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId && (p.Status == PaymentStatus.HELD || p.Status == PaymentStatus.FROZEN));
        if (payment == null) throw new NotFoundException("Held or frozen payment not found for this milestone.");

        var payerWallet = await GetWalletAsync(payment.PayerId);
        var payeeWallet = await GetWalletAsync(payment.PayeeId);

        if (payerWallet.HeldBalance < payment.Amount) throw new ValidationException("Insufficient held funds in payer wallet.");

        payerWallet.HeldBalance -= payment.Amount;
        payeeWallet.AvailableBalance += payment.Amount;
        payeeWallet.TotalEarned += payment.Amount;

        payment.Status = PaymentStatus.RELEASED;
        payment.ReleasedAt = DateTimeOffset.UtcNow;

        _dbContext.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = payerWallet.Id, UserId = payerWallet.UserId, Amount = payment.Amount,
            Type = WalletTransactionType.PAYMENT_RELEASE, Direction = TransactionDirection.DEBIT,
            Description = $"Payment released for milestone: {milestone.Title}",
            BalanceBefore = payerWallet.HeldBalance + payment.Amount, BalanceAfter = payerWallet.HeldBalance,
            PaymentId = payment.Id
        });

        _dbContext.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = payeeWallet.Id, UserId = payeeWallet.UserId, Amount = payment.Amount,
            Type = WalletTransactionType.PAYMENT_RELEASE, Direction = TransactionDirection.CREDIT,
            Description = $"Payment received for milestone: {milestone.Title}",
            BalanceBefore = payeeWallet.AvailableBalance - payment.Amount, BalanceAfter = payeeWallet.AvailableBalance,
            PaymentId = payment.Id
        });

        milestone.Status = MilestoneStatus.RELEASED;
        milestone.ApprovedAt = DateTimeOffset.UtcNow;
        milestone.PaidAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();
        await SyncProjectStatusAsync(milestone.ProjectId);

        _logger.LogInformation("✅ Funds released for Milestone {MilestoneId}", milestoneId);
    }

    public async Task RefundMilestoneAsync(Guid adminId, Guid milestoneId, decimal amount, string reason)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            await RefundMilestoneCoreAsync(adminId, milestoneId, amount, reason);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task RefundMilestoneCoreAsync(Guid adminId, Guid milestoneId, decimal amount, string reason)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId && (p.Status == PaymentStatus.HELD || p.Status == PaymentStatus.FROZEN));

        if (payment == null) throw new NotFoundException("Held/Frozen payment not found for refund.");

        var payerWallet = await GetWalletAsync(payment.PayerId);
        if (payerWallet.HeldBalance < amount) throw new ValidationException("Insufficient held funds for refund.");

        payerWallet.HeldBalance -= amount;
        payerWallet.AvailableBalance += amount;

        payment.Status = PaymentStatus.REFUNDED;
        payment.RefundedAt = DateTimeOffset.UtcNow;

        _dbContext.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = payerWallet.Id, UserId = payerWallet.UserId, Amount = amount,
            Type = WalletTransactionType.REFUND, Direction = TransactionDirection.CREDIT,
            Description = $"Refund for milestone: {reason}",
            BalanceBefore = payerWallet.AvailableBalance - amount, BalanceAfter = payerWallet.AvailableBalance,
            PaymentId = payment.Id
        });

        milestone.Status = MilestoneStatus.REFUNDED;

        await _dbContext.SaveChangesAsync();
        await SyncProjectStatusAsync(milestone.ProjectId);

        _logger.LogInformation("✅ Refunded {Amount} for Milestone {MilestoneId}", amount, milestoneId);
    }

    public async Task SplitMilestoneFundsAsync(Guid milestoneId, decimal releaseToExpertAmount, decimal refundToClientAmount, string reason)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            await SplitMilestoneFundsCoreAsync(milestoneId, releaseToExpertAmount, refundToClientAmount, reason);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task SplitMilestoneFundsCoreAsync(Guid milestoneId, decimal releaseToExpertAmount, decimal refundToClientAmount, string reason)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId && (p.Status == PaymentStatus.HELD || p.Status == PaymentStatus.FROZEN));

        if (payment == null) throw new NotFoundException("Held/Frozen payment not found for split.");
        if (releaseToExpertAmount + refundToClientAmount != payment.Amount)
            throw new ValidationException("Total split amounts must equal payment amount.");

        var payerWallet = await GetWalletAsync(payment.PayerId);
        var payeeWallet = await GetWalletAsync(payment.PayeeId);

        payerWallet.HeldBalance -= (releaseToExpertAmount + refundToClientAmount);
        payerWallet.AvailableBalance += refundToClientAmount;

        payeeWallet.AvailableBalance += releaseToExpertAmount;
        payeeWallet.TotalEarned += releaseToExpertAmount;

        payment.Status = PaymentStatus.RELEASED;
        payment.ReleasedAt = DateTimeOffset.UtcNow;
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        _dbContext.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = payerWallet.Id, UserId = payerWallet.UserId, Amount = refundToClientAmount,
            Type = WalletTransactionType.REFUND, Direction = TransactionDirection.CREDIT,
            Description = $"Dispute split: Refunded part. {reason}",
            BalanceAfter = payerWallet.AvailableBalance, PaymentId = payment.Id
        });

        _dbContext.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = payeeWallet.Id, UserId = payeeWallet.UserId, Amount = releaseToExpertAmount,
            Type = WalletTransactionType.PAYMENT_RELEASE, Direction = TransactionDirection.CREDIT,
            Description = $"Dispute split: Released part. {reason}",
            BalanceAfter = payeeWallet.AvailableBalance, PaymentId = payment.Id
        });

        milestone.Status = releaseToExpertAmount > 0 ? MilestoneStatus.RELEASED : MilestoneStatus.REFUNDED;

        await _dbContext.SaveChangesAsync();
        await SyncProjectStatusAsync(milestone.ProjectId);
    }

    public async Task FreezeFundsAsync(Guid milestoneId, string reason)
    {
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId);
        if (payment == null) throw new NotFoundException("Payment not found.");
        if (payment.Status != PaymentStatus.HELD) throw new ValidationException("Only HELD payments can be frozen.");

        payment.Status = PaymentStatus.FROZEN;
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();
        await MarkProjectDisputedAsync(payment.ProjectId);

        _logger.LogInformation("❄️ Funds frozen for Milestone {MilestoneId}. Reason: {Reason}", milestoneId, reason);
    }

    public async Task UnfreezeFundsAsync(Guid milestoneId, string reason)
    {
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId);
        if (payment == null) throw new NotFoundException("Payment not found.");
        if (payment.Status != PaymentStatus.FROZEN) throw new ValidationException("Only FROZEN payments can be unfrozen.");

        payment.Status = PaymentStatus.HELD;
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();
        await SyncProjectStatusAsync(payment.ProjectId);

        _logger.LogInformation("🔥 Funds unfrozen for Milestone {MilestoneId}. Reason: {Reason}", milestoneId, reason);
    }

    public async Task RequestRevisionAsync(Guid milestoneId, string reason)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            await RequestRevisionCoreAsync(milestoneId, reason);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task RequestRevisionCoreAsync(Guid milestoneId, string reason)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);

        if (milestone.Status != MilestoneStatus.DISPUTED && milestone.Status != MilestoneStatus.FUNDED)
            throw new ValidationException("Only disputed or funded milestones can be revised.");

        milestone.Status = MilestoneStatus.REVISION_REQUESTED;
        milestone.UpdatedAt = DateTimeOffset.UtcNow;

        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId);
        if (payment != null)
        {
            payment.Status = PaymentStatus.HELD; // Back to HELD for revision
            payment.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await SyncProjectStatusAsync(milestone.ProjectId);

        _logger.LogInformation("🔄 Revision requested for Milestone {MilestoneId}. Reason: {Reason}", milestoneId, reason);
    }

    public async Task SyncProjectStatusAsync(Guid projectId)
    {
        var project = await _dbContext.Projects
            .Include(p => p.Milestones)
            .Include(p => p.Job)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null) return;

        // Terminal milestones are PAID or REFUNDED
        var allSettled = project.Milestones.All(m => m.Status == MilestoneStatus.RELEASED || m.Status == MilestoneStatus.REFUNDED);

        if (allSettled && project.Milestones.Any())
        {
            project.Status = ProjectStatus.COMPLETED;
            project.CompletedAt = DateTimeOffset.UtcNow;

            // Sync Job status
            if (project.Job != null)
            {
                project.Job.Status = JobStatus.COMPLETED;
                project.Job.UpdatedAt = DateTimeOffset.UtcNow;
            }

            _logger.LogInformation("🏆 Project {ProjectId} marked as COMPLETED because all milestones are settled.", projectId);
        }
        else if (project.Milestones.Any(m => m.Status == MilestoneStatus.DISPUTED))
        {
            project.Status = ProjectStatus.DISPUTED;
        }
        else if (project.Milestones.Any(m => m.Status == MilestoneStatus.FUNDED || m.Status == MilestoneStatus.SUBMITTED || m.Status == MilestoneStatus.REVISION_REQUESTED))
        {
            project.Status = ProjectStatus.ACTIVE;
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task MarkProjectDisputedAsync(Guid projectId)
    {
        var project = await _dbContext.Projects.FindAsync(projectId);
        if (project != null && project.Status != ProjectStatus.DISPUTED)
        {
            project.Status = ProjectStatus.DISPUTED;
            await _dbContext.SaveChangesAsync();
            _logger.LogWarning("⚠️ Project {ProjectId} status set to DISPUTED.", projectId);
        }
    }

    private async Task<Milestone> GetMilestoneWithProjectAsync(Guid milestoneId)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project).ThenInclude(p => p.Milestones)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        return milestone ?? throw new NotFoundException("Milestone not found.");
    }

    private async Task<Wallet> GetWalletAsync(Guid userId)
    {
        var wallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        return wallet ?? throw new NotFoundException($"Wallet for user {userId} not found.");
    }
}
