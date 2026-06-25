using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aivora.Services.Treasury;

/// <summary>
/// The Treasury (Kho bạc) - Deep Module chịu trách nhiệm duy nhất về tính toàn vẹn tài chính mô phỏng.
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
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);

        if (milestone.Project.ClientId != clientId) throw new UnauthorizedException("Access denied.");
        if (milestone.Status != MilestoneStatus.CREATED) throw new ValidationException("Milestone is already funded or processed.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var wallet = await GetWalletAsync(clientId);
            if (wallet.AvailableBalance < milestone.Amount) throw new ValidationException("Insufficient balance.");

            // 1. Update Wallet
            wallet.AvailableBalance -= milestone.Amount;
            wallet.HeldBalance += milestone.Amount;

            // 2. Create Payment (HELD)
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

            // 3. Log Transaction
            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = wallet.Id,
                UserId = clientId,
                Amount = milestone.Amount,
                Type = WalletTransactionType.ESCROW_HOLD,
                Direction = TransactionDirection.DEBIT,
                Description = $"Recording direct transfer for milestone: {milestone.Title}",
                BalanceBefore = wallet.AvailableBalance + milestone.Amount,
                BalanceAfter = wallet.AvailableBalance,
                PaymentId = payment.Id
            });

            // 4. Update Milestone & Project status
            milestone.Status = MilestoneStatus.FUNDED;
            milestone.FundedAt = DateTimeOffset.UtcNow;

            if (milestone.Project.Status == ProjectStatus.PENDING_PAYMENT)
            {
                milestone.Project.Status = ProjectStatus.ACTIVE;
                milestone.Project.StartDate = DateOnly.FromDateTime(DateTime.UtcNow);
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("✅ Milestone {MilestoneId} direct transfer recorded by Client {ClientId}", milestoneId, clientId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ Failed to record direct transfer for milestone {MilestoneId}", milestoneId);
            throw;
        }
    }

    public async Task ReleaseMilestoneAsync(Guid clientId, Guid milestoneId)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);

        if (milestone.Project.ClientId != clientId) throw new UnauthorizedException("Only the client can approve milestone and complete direct transfer.");
        if (milestone.Status != MilestoneStatus.SUBMITTED) throw new ValidationException("Milestone must be in SUBMITTED status to be approved and completed.");

        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId && p.Status == PaymentStatus.HELD);
        if (payment == null) throw new NotFoundException("Direct transfer tracking record not found for this milestone.");
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var payerWallet = await GetWalletAsync(payment.PayerId);
            var payeeWallet = await GetWalletAsync(payment.PayeeId);

            if (payerWallet.HeldBalance < payment.Amount) throw new ValidationException("Insufficient held balance in payer wallet.");

            // 1. Move money
            payerWallet.HeldBalance -= payment.Amount;
            payeeWallet.AvailableBalance += payment.Amount;
            payeeWallet.TotalEarned += payment.Amount;

            // 2. Update Payment
            payment.Status = PaymentStatus.RELEASED;
            payment.ReleasedAt = DateTimeOffset.UtcNow;

            // 3. Log Transactions
            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = payerWallet.Id,
                UserId = payerWallet.UserId,
                Amount = payment.Amount,
                Type = WalletTransactionType.PAYMENT_RELEASE,
                Direction = TransactionDirection.DEBIT,
                Description = $"Direct transfer completed for milestone: {milestone.Title}",
                BalanceBefore = payerWallet.HeldBalance + payment.Amount,
                BalanceAfter = payerWallet.HeldBalance,
                PaymentId = payment.Id
            });

            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = payeeWallet.Id,
                UserId = payeeWallet.UserId,
                Amount = payment.Amount,
                Type = WalletTransactionType.PAYMENT_RELEASE,
                Direction = TransactionDirection.CREDIT,
                Description = $"Direct transfer received for milestone: {milestone.Title}",
                BalanceBefore = payeeWallet.AvailableBalance - payment.Amount,
                BalanceAfter = payeeWallet.AvailableBalance,
                PaymentId = payment.Id
            });

            // 4. Update Milestone & Project
            milestone.Status = MilestoneStatus.RELEASED;
            milestone.ApprovedAt = DateTimeOffset.UtcNow;
            milestone.PaidAt = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync();
            await SyncProjectStatusAsync(milestone.ProjectId);

            await transaction.CommitAsync();

            _logger.LogInformation("✅ Direct transfer completed for Milestone {MilestoneId}", milestoneId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ Failed to complete direct transfer for Milestone {MilestoneId}", milestoneId);
            throw;
        }
    }

    public async Task RefundMilestoneAsync(Guid adminId, Guid milestoneId, decimal amount, string reason)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId && (p.Status == PaymentStatus.HELD || p.Status == PaymentStatus.FROZEN));

        if (payment == null) throw new NotFoundException("Direct transfer tracking record not found for reversal.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var payerWallet = await GetWalletAsync(payment.PayerId);
            if (payerWallet.HeldBalance < amount) throw new ValidationException("Insufficient held balance for transfer reversal.");

            // 1. Move money back
            payerWallet.HeldBalance -= amount;
            payerWallet.AvailableBalance += amount;

            // 2. Update Payment
            payment.Status = PaymentStatus.REFUNDED;
            payment.RefundedAt = DateTimeOffset.UtcNow;

            // 3. Log Transaction
            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = payerWallet.Id,
                UserId = payerWallet.UserId,
                Amount = amount,
                Type = WalletTransactionType.REFUND,
                Direction = TransactionDirection.CREDIT,
                Description = $"Direct transfer reversal for milestone: {reason}",
                BalanceBefore = payerWallet.AvailableBalance - amount,
                BalanceAfter = payerWallet.AvailableBalance,
                PaymentId = payment.Id
            });

            // 4. Update Milestone
            milestone.Status = MilestoneStatus.REFUNDED;

            await _dbContext.SaveChangesAsync();
            await SyncProjectStatusAsync(milestone.ProjectId);

            await transaction.CommitAsync();

            _logger.LogInformation("✅ Reversed transfer of {Amount} for Milestone {MilestoneId}", amount, milestoneId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ Failed to reverse transfer for milestone {MilestoneId}", milestoneId);
            throw;
        }
    }

    public async Task SplitMilestoneFundsAsync(Guid milestoneId, decimal releaseToExpertAmount, decimal refundToClientAmount, string reason)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId && (p.Status == PaymentStatus.HELD || p.Status == PaymentStatus.FROZEN));

        if (payment == null) throw new NotFoundException("Direct transfer tracking record not found for split resolution.");
        if (releaseToExpertAmount + refundToClientAmount != payment.Amount)
            throw new ValidationException("Total split amounts must equal transaction amount.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var payerWallet = await GetWalletAsync(payment.PayerId);
            var payeeWallet = await GetWalletAsync(payment.PayeeId);

            // 1. Move money
            payerWallet.HeldBalance -= (releaseToExpertAmount + refundToClientAmount);
            payerWallet.AvailableBalance += refundToClientAmount;

            payeeWallet.AvailableBalance += releaseToExpertAmount;
            payeeWallet.TotalEarned += releaseToExpertAmount;

            // 2. Update Payment (Marking as released overall for MVP simplicity, or could add PARTIAL)
            payment.Status = PaymentStatus.RELEASED;
            payment.ReleasedAt = DateTimeOffset.UtcNow;
            payment.UpdatedAt = DateTimeOffset.UtcNow;

            // 3. Log Transactions (Simplified summary logs)
            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = payerWallet.Id,
                UserId = payerWallet.UserId,
                Amount = refundToClientAmount,
                Type = WalletTransactionType.REFUND,
                Direction = TransactionDirection.CREDIT,
                Description = $"Dispute split: Reversed part. {reason}",
                BalanceAfter = payerWallet.AvailableBalance,
                PaymentId = payment.Id
            });

            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = payeeWallet.Id,
                UserId = payeeWallet.UserId,
                Amount = releaseToExpertAmount,
                Type = WalletTransactionType.PAYMENT_RELEASE,
                Direction = TransactionDirection.CREDIT,
                Description = $"Dispute split: Completed part. {reason}",
                BalanceAfter = payeeWallet.AvailableBalance,
                PaymentId = payment.Id
            });

            // 4. Update Milestone
            milestone.Status = releaseToExpertAmount > 0 ? MilestoneStatus.RELEASED : MilestoneStatus.REFUNDED;

            await _dbContext.SaveChangesAsync();
            await SyncProjectStatusAsync(milestone.ProjectId);

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ Failed to split transaction for milestone {MilestoneId}", milestoneId);
            throw;
        }
    }

    public async Task FreezeFundsAsync(Guid milestoneId, string reason)
    {
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId);
        if (payment == null) throw new NotFoundException("Direct transfer tracking record not found.");
        if (payment.Status != PaymentStatus.HELD) throw new ValidationException("Only HELD transfer records can be frozen.");

        payment.Status = PaymentStatus.FROZEN;
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();
        await MarkProjectDisputedAsync(payment.ProjectId);

        _logger.LogInformation("❄️ Direct transfer record frozen for Milestone {MilestoneId}. Reason: {Reason}", milestoneId, reason);
    }

    public async Task UnfreezeFundsAsync(Guid milestoneId, string reason)
    {
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId);
        if (payment == null) throw new NotFoundException("Direct transfer tracking record not found.");
        if (payment.Status != PaymentStatus.FROZEN) throw new ValidationException("Only FROZEN transfer records can be unfrozen.");

        payment.Status = PaymentStatus.HELD;
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();
        await SyncProjectStatusAsync(payment.ProjectId);

        _logger.LogInformation("🔥 Direct transfer record unfrozen for Milestone {MilestoneId}. Reason: {Reason}", milestoneId, reason);
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
