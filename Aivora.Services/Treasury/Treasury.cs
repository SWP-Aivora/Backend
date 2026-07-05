using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.NotificationService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aivora.Repositories.Constants;
using Aivora.Services.Options;

namespace Aivora.Services.Treasury;

/// <summary>
/// The Treasury (Kho bạc) - Deep Module chịu trách nhiệm duy nhất về tính toàn vẹn tài chính.
/// Hợp nhất toàn bộ logic từ FinancialLedger cũ để đảm bảo tính Locality và Leverage.
/// </summary>
public class Treasury : ITreasury
{
    private readonly AivoraDbContext _dbContext;
    private readonly ICommissionCalculator _commissionCalculator;
    private readonly ILogger<Treasury> _logger;
    private readonly NotificationService.IService _notificationService;
    private readonly RealtimeService.IService _realtimeService;

    public Treasury(AivoraDbContext dbContext, ICommissionCalculator commissionCalculator, ILogger<Treasury> logger, NotificationService.IService notificationService, RealtimeService.IService realtimeService)
    {
        _dbContext = dbContext;
        _commissionCalculator = commissionCalculator;
        _logger = logger;
        _notificationService = notificationService;
        _realtimeService = realtimeService;
    }
    public async Task PayDepositAsync(Guid clientId, Guid milestoneId)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);

        if (milestone.Project.ClientId != clientId) throw new UnauthorizedException("Access denied.");
        if (milestone.Status != MilestoneStatus.CREATED) throw new ValidationException("Milestone must be CREATED to pay deposit.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var wallets = await GetWalletsForUpdateAsync(clientId, milestone.Project.ExpertId);
            var clientWallet = wallets.First(w => w.UserId == clientId);
            var expertWallet = wallets.First(w => w.UserId == milestone.Project.ExpertId);

            var depositAmount = milestone.Amount * 0.3m; // 30%

            if (clientWallet.AvailableBalance < depositAmount) throw new ValidationException("Insufficient balance for deposit.");

            // 1. Move money directly
            clientWallet.AvailableBalance -= depositAmount;
            AddFundsToWallet(expertWallet, depositAmount);
            expertWallet.TotalEarned += depositAmount;

            // 2. Create Payment (RELEASED immediately)
            var payment = new Payment
            {
                MilestoneId = milestoneId,
                ProjectId = milestone.ProjectId,
                PayerId = clientId,
                PayeeId = milestone.Project.ExpertId,
                Amount = depositAmount,
                Currency = clientWallet.Currency,
                Status = PaymentStatus.RELEASED,
                ReleasedAt = DateTimeOffset.UtcNow
            };
            _dbContext.Payments.Add(payment);

            // 3. Log Transactions
            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = clientWallet.Id,
                UserId = clientId,
                Amount = depositAmount,
                Type = WalletTransactionType.PAYMENT_RELEASE, // Or maybe a new type for deposit
                Direction = TransactionDirection.DEBIT,
                Description = $"Paid 30% deposit for milestone: {milestone.Title}",
                BalanceBefore = clientWallet.AvailableBalance + depositAmount,
                BalanceAfter = clientWallet.AvailableBalance,
                PaymentId = payment.Id
            });

            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = expertWallet.Id,
                UserId = expertWallet.UserId,
                Amount = depositAmount,
                Type = WalletTransactionType.PAYMENT_RELEASE,
                Direction = TransactionDirection.CREDIT,
                Description = $"Received 30% deposit for milestone: {milestone.Title}",
                BalanceBefore = expertWallet.AvailableBalance - depositAmount,
                BalanceAfter = expertWallet.AvailableBalance,
                PaymentId = payment.Id
            });

            // 4. Update Milestone & Project status
            milestone.Status = MilestoneStatus.IN_PROGRESS;
            milestone.FundedAt = DateTimeOffset.UtcNow; // Can reuse this field for deposit paid

            if (milestone.Project.Status == ProjectStatus.PENDING_PAYMENT)
            {
                milestone.Project.Status = ProjectStatus.ACTIVE;
                milestone.Project.StartDate = DateOnly.FromDateTime(DateTime.UtcNow);
            }
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            try
            {
                await _notificationService.SendNotificationAsync(
                    milestone.Project.ExpertId,
                    "Milestone deposit paid",
                    $"The client has paid the 30% deposit for milestone \"{milestone.Title}\". You can start working on it.",
                    "MILESTONE",
                    $"/projects/{milestone.ProjectId}/milestones/{milestoneId}"
                );
            }
            catch { /* Notification failure should not block */ }
            _logger.LogInformation("✅ Milestone {MilestoneId} 30% deposit paid by Client {ClientId}", milestoneId, clientId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ Failed to pay deposit for milestone {MilestoneId}", milestoneId);
            throw;
        }
    }
    public async Task PayRemainingAsync(Guid clientId, Guid milestoneId)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);

        if (milestone.Project.ClientId != clientId && milestone.Status != MilestoneStatus.DISPUTED) throw new UnauthorizedException("Only the client can approve and release funds.");
        if (milestone.Status != MilestoneStatus.SUBMITTED && milestone.Status != MilestoneStatus.DISPUTED)
            throw new ValidationException("Milestone must be in SUBMITTED or DISPUTED status to release remaining funds.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var wallets = await GetWalletsForUpdateAsync(clientId, milestone.Project.ExpertId, SystemConstants.SystemUserId);
            var clientWallet = wallets.First(w => w.UserId == clientId);
            var expertWallet = wallets.First(w => w.UserId == milestone.Project.ExpertId);
            var platformWallet = wallets.First(w => w.UserId == SystemConstants.SystemUserId);

            var remainingAmount = milestone.Amount * 0.7m; // 70%
            var commissionAmount = _commissionCalculator.CalculateCommission(milestone.Amount);
            var expertAmount = remainingAmount - commissionAmount;

            if (clientWallet.AvailableBalance < remainingAmount) throw new ValidationException("Insufficient balance for remaining payment.");

            // 1. Move money directly
            clientWallet.AvailableBalance -= remainingAmount;
            AddFundsToWallet(expertWallet, expertAmount);
            AddFundsToWallet(platformWallet, commissionAmount);
            expertWallet.TotalEarned += expertAmount;
            platformWallet.TotalEarned += commissionAmount;

            // 2. Create Payment (RELEASED immediately)
            var payment = new Payment
            {
                MilestoneId = milestoneId,
                ProjectId = milestone.ProjectId,
                PayerId = clientId,
                PayeeId = milestone.Project.ExpertId,
                Amount = remainingAmount,
                Currency = clientWallet.Currency,
                Status = PaymentStatus.RELEASED,
                ReleasedAt = DateTimeOffset.UtcNow
            };
            _dbContext.Payments.Add(payment);

            // 3. Log Transactions
            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = clientWallet.Id,
                UserId = clientId,
                Amount = remainingAmount,
                Type = WalletTransactionType.PAYMENT_RELEASE,
                Direction = TransactionDirection.DEBIT,
                Description = $"Paid 70% remaining for milestone: {milestone.Title}",
                BalanceBefore = clientWallet.AvailableBalance + remainingAmount,
                BalanceAfter = clientWallet.AvailableBalance,
                PaymentId = payment.Id
            });

            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = expertWallet.Id,
                UserId = expertWallet.UserId,
                Amount = expertAmount,
                Type = WalletTransactionType.PAYMENT_RELEASE,
                Direction = TransactionDirection.CREDIT,
                Description = $"Received remaining payment (after fee) for milestone: {milestone.Title}",
                BalanceBefore = expertWallet.AvailableBalance - expertAmount,
                BalanceAfter = expertWallet.AvailableBalance,
                PaymentId = payment.Id
            });

            if (commissionAmount > 0)
            {
                _dbContext.WalletTransactions.Add(new WalletTransaction
                {
                    WalletId = platformWallet.Id,
                    UserId = platformWallet.UserId,
                    Amount = commissionAmount,
                    Type = WalletTransactionType.PLATFORM_FEE,
                    Direction = TransactionDirection.CREDIT,
                    Description = $"10% Platform fee for milestone: {milestone.Title}",
                    BalanceBefore = platformWallet.AvailableBalance - commissionAmount,
                    BalanceAfter = platformWallet.AvailableBalance,
                    PaymentId = payment.Id
                });
            }

            // 4. Update Milestone & Project
            milestone.Status = MilestoneStatus.RELEASED;
            milestone.ApprovedAt = DateTimeOffset.UtcNow;
            milestone.PaidAt = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync();
            await SyncProjectStatusAsync(milestone.ProjectId);

            await transaction.CommitAsync();

            try
            {
                await _notificationService.SendNotificationAsync(
                    milestone.Project.ExpertId,
                    "Milestone approved and paid",
                    $"The client has approved milestone \"{milestone.Title}\" and the remaining 70% has been released to your wallet.",
                    "MILESTONE",
                    $"/projects/{milestone.ProjectId}/milestones/{milestoneId}"
                );
            }
            catch { /* Notification failure should not block */ }
            _logger.LogInformation("✅ Remaining 70% funds released for Milestone {MilestoneId}", milestoneId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ Failed to release remaining funds for Milestone {MilestoneId}", milestoneId);
            throw;
        }
    }
    public async Task RefundMilestoneAsync(Guid adminId, Guid milestoneId, string reason)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);
        var payments = await _dbContext.Payments.Where(p => p.MilestoneId == milestoneId && p.Status == PaymentStatus.RELEASED).ToListAsync();

        if (!payments.Any()) throw new NotFoundException("Payment not found for refund.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var payerId = payments.First().PayerId;
            var payeeId = payments.First().PayeeId;
            var wallets = await GetWalletsForUpdateAsync(payerId, payeeId);
            var payerWallet = wallets.First(w => w.UserId == payerId);
            var payeeWallet = wallets.First(w => w.UserId == payeeId);

            var amount = payments.Sum(p => p.Amount);
            var commissionAmount = _commissionCalculator.CalculateCommission(milestone.Amount);
            // Expert only received amount - commissionAmount (the rest went to platform wallet).
            var expertActualEarned = amount - commissionAmount;
            // We do NOT check for insufficient funds because dispute clawbacks may cause a negative balance.

            // 1. Move money back (Clawback from expert to client)
            ClawbackFromWallet(payeeWallet, amount);
            AddFundsToWallet(payerWallet, amount);
            payeeWallet.TotalEarned = Math.Max(0, payeeWallet.TotalEarned - expertActualEarned);

            // 2. Update Payment
            foreach (var payment in payments)
            {
                payment.Status = PaymentStatus.REFUNDED;
                payment.RefundedAt = DateTimeOffset.UtcNow;
            }
            // 3. Log Transaction
            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = payerWallet.Id,
                UserId = payerWallet.UserId,
                Amount = amount,
                Type = WalletTransactionType.REFUND,
                Direction = TransactionDirection.CREDIT,
                Description = $"Refund for milestone: {reason}",
                BalanceBefore = payerWallet.AvailableBalance - amount,
                BalanceAfter = payerWallet.AvailableBalance,
                PaymentId = payments.First().Id
            });

            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = payeeWallet.Id,
                UserId = payeeWallet.UserId,
                Amount = amount,
                Type = WalletTransactionType.REFUND,
                Direction = TransactionDirection.DEBIT,
                Description = $"Clawback for milestone refund: {reason}",
                BalanceBefore = payeeWallet.AvailableBalance + amount,
                BalanceAfter = payeeWallet.AvailableBalance,
                PaymentId = payments.First().Id
            });

            // 4. Update Milestone
            milestone.Status = MilestoneStatus.REFUNDED;

            await _dbContext.SaveChangesAsync();
            await SyncProjectStatusAsync(milestone.ProjectId);

            await transaction.CommitAsync();

            _logger.LogInformation("✅ Refunded {Amount} for Milestone {MilestoneId}", amount, milestoneId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ Failed to refund milestone {MilestoneId}", milestoneId);
            throw;
        }
    }
    public async Task SplitMilestoneFundsAsync(Guid milestoneId, decimal releaseToExpertAmount, decimal refundToClientAmount, string reason)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);
        var payments = await _dbContext.Payments.Where(p => p.MilestoneId == milestoneId && p.Status == PaymentStatus.RELEASED).ToListAsync();

        if (!payments.Any()) throw new NotFoundException("Payment not found for split.");

        var totalAmount = payments.Sum(p => p.Amount);
        if (releaseToExpertAmount + refundToClientAmount != totalAmount)
            throw new ValidationException($"Total split amounts ({releaseToExpertAmount + refundToClientAmount}) must equal actually paid amount ({totalAmount}).");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var payerId = payments.First().PayerId;
            var payeeId = payments.First().PayeeId;
            var wallets = await GetWalletsForUpdateAsync(payerId, payeeId);
            var payerWallet = wallets.First(w => w.UserId == payerId);
            var payeeWallet = wallets.First(w => w.UserId == payeeId);

            // In 30/70 direct deposit, the expert already holds the full payment amount.
            // We claw back the refundToClientAmount from the expert.

            // 1. Move money
            ClawbackFromWallet(payeeWallet, refundToClientAmount);
            AddFundsToWallet(payerWallet, refundToClientAmount);

            // Adjust TotalEarned: expert originally earned (milestone.Amount - commission).
            // After split, they keep releaseToExpertAmount. Compute the delta correctly.
            var commissionAmount = _commissionCalculator.CalculateCommission(milestone.Amount);
            var expertEarnedBefore = totalAmount - commissionAmount;
            var delta = releaseToExpertAmount - expertEarnedBefore;
            payeeWallet.TotalEarned += delta;
            payeeWallet.TotalEarned = Math.Max(0, payeeWallet.TotalEarned);

            // 2. Update Payment
            foreach (var payment in payments)
            {
                payment.Status = PaymentStatus.RELEASED;
            }
            // 3. Log Transactions (just for the refund portion)
            if (refundToClientAmount > 0)
            {
                _dbContext.WalletTransactions.Add(new WalletTransaction
                {
                    WalletId = payerWallet.Id,
                    UserId = payerWallet.UserId,
                    Amount = refundToClientAmount,
                    Type = WalletTransactionType.REFUND,
                    Direction = TransactionDirection.CREDIT,
                    Description = $"Split resolution refund: {reason}",
                    BalanceBefore = payerWallet.AvailableBalance - refundToClientAmount,
                    BalanceAfter = payerWallet.AvailableBalance,
                    PaymentId = payments.First().Id
                });

                _dbContext.WalletTransactions.Add(new WalletTransaction
                {
                    WalletId = payeeWallet.Id,
                    UserId = payeeWallet.UserId,
                    Amount = refundToClientAmount,
                    Type = WalletTransactionType.REFUND,
                    Direction = TransactionDirection.DEBIT,
                    BalanceBefore = payeeWallet.AvailableBalance + refundToClientAmount,
                    BalanceAfter = payeeWallet.AvailableBalance,
                    PaymentId = payments.First().Id
                });
            }
            // 4. Update Milestone
            milestone.Status = MilestoneStatus.RELEASED;
            milestone.ApprovedAt = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync();
            await SyncProjectStatusAsync(milestone.ProjectId);

            await transaction.CommitAsync();

            _logger.LogInformation("✅ Split {MilestoneId} funds: {ReleaseAmount} released, {RefundAmount} refunded", milestoneId, releaseToExpertAmount, refundToClientAmount);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ Failed to split funds for milestone {MilestoneId}", milestoneId);
            throw;
        }
    }
    // Removed FreezeFundsAsync and UnfreezeFundsAsync per new model

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

            var affectedUsers = new[] { project.ClientId, project.ExpertId };
            await _realtimeService.SendJobStatusUpdateToUsersAsync(affectedUsers, project.JobId, JobStatus.COMPLETED, project.Job?.Title);
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

    /// <summary>
    /// Locks multiple wallets in a consistent order (by UserId ascending) to prevent deadlocks
    /// when concurrent transactions lock the same wallets in different order.
    /// </summary>
    private async Task<Wallet[]> GetWalletsForUpdateAsync(params Guid[] userIds)
    {
        var distinct = userIds.Distinct().OrderBy(id => id).ToArray();
        var wallets = new List<Wallet>(distinct.Length);
        foreach (var userId in distinct)
        {
            wallets.Add(await GetWalletForUpdateAsync(userId));
        }
        return wallets.ToArray();
    }

    private async Task<Wallet> GetWalletForUpdateAsync(Guid userId)
    {
        Wallet? wallet = null;
        var isPostgres = (_dbContext.Database.ProviderName?.Contains("Npgsql") ?? false)
                       || (_dbContext.Database.ProviderName?.Contains("PostgreSQL") ?? false);
        if (isPostgres)
        {
            wallet = await _dbContext.Wallets
                .FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"UserId\" = {0} FOR UPDATE", userId)
                .FirstOrDefaultAsync();
        }
        else
        {
            wallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        }
        return wallet ?? throw new NotFoundException($"Wallet for user {userId} not found.");
    }

    private void AddFundsToWallet(Wallet wallet, decimal amount)
    {
        if (wallet.Debt > 0)
        {
            var debtPayment = Math.Min(amount, wallet.Debt);
            wallet.Debt -= debtPayment;
            amount -= debtPayment;
        }
        wallet.AvailableBalance += amount;
    }

    private void ClawbackFromWallet(Wallet wallet, decimal amount)
    {
        if (wallet.AvailableBalance >= amount)
        {
            wallet.AvailableBalance -= amount;
        }
        else
        {
            var deficit = amount - wallet.AvailableBalance;
            var maxDebt = _commissionCalculator.MaxDebtLimit;
            if (maxDebt > 0 && wallet.Debt + deficit > maxDebt)
            {
                throw new ValidationException(
                    $"Cannot claw back {amount}: expert's debt would exceed the limit of {maxDebt}. " +
                    $"Current debt: {wallet.Debt}, available: {wallet.AvailableBalance}.");
            }
            wallet.AvailableBalance = 0;
            wallet.Debt += deficit;
        }
    }
}
