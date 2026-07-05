using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.NotificationService;
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
    private readonly NotificationService.IService _notificationService;
    private readonly RealtimeService.IService _realtimeService;

    public Treasury(AivoraDbContext dbContext, ILogger<Treasury> logger, NotificationService.IService notificationService, RealtimeService.IService realtimeService)
    {
        _dbContext = dbContext;
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
            var clientWallet = await GetWalletAsync(clientId);
            var expertWallet = await GetWalletAsync(milestone.Project.ExpertId);
            
            var depositAmount = milestone.Amount * 0.3m; // 30%

            if (clientWallet.AvailableBalance < depositAmount) throw new ValidationException("Insufficient balance for deposit.");

            // 1. Move money directly
            clientWallet.AvailableBalance -= depositAmount;
            expertWallet.AvailableBalance += depositAmount;
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
            var clientWallet = await GetWalletAsync(clientId);
            var expertWallet = await GetWalletAsync(milestone.Project.ExpertId);
            
            var remainingAmount = milestone.Amount * 0.7m; // 70%

            if (clientWallet.AvailableBalance < remainingAmount) throw new ValidationException("Insufficient balance for remaining payment.");

            // 1. Move money directly
            clientWallet.AvailableBalance -= remainingAmount;
            expertWallet.AvailableBalance += remainingAmount;
            expertWallet.TotalEarned += remainingAmount;

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
                Amount = remainingAmount,
                Type = WalletTransactionType.PAYMENT_RELEASE,
                Direction = TransactionDirection.CREDIT,
                Description = $"Received 70% remaining for milestone: {milestone.Title}",
                BalanceBefore = expertWallet.AvailableBalance - remainingAmount,
                BalanceAfter = expertWallet.AvailableBalance,
                PaymentId = payment.Id
            });

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

    public async Task RefundMilestoneAsync(Guid adminId, Guid milestoneId, decimal amount, string reason)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId && p.Status == PaymentStatus.RELEASED);

        if (payment == null) throw new NotFoundException("Payment not found for refund.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var payerWallet = await GetWalletAsync(payment.PayerId);
            var payeeWallet = await GetWalletAsync(payment.PayeeId);
            
            if (payeeWallet.AvailableBalance < amount) throw new ValidationException("Expert has insufficient funds for refund.");

            // 1. Move money back (Clawback from expert to client)
            payeeWallet.AvailableBalance -= amount;
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
                Description = $"Refund for milestone: {reason}",
                BalanceBefore = payerWallet.AvailableBalance - amount,
                BalanceAfter = payerWallet.AvailableBalance,
                PaymentId = payment.Id
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
                PaymentId = payment.Id
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
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId && p.Status == PaymentStatus.RELEASED);

        if (payment == null) throw new NotFoundException("Payment not found for split.");
        if (releaseToExpertAmount + refundToClientAmount != payment.Amount)
            throw new ValidationException("Total split amounts must equal payment amount.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var payerWallet = await GetWalletAsync(payment.PayerId);
            var payeeWallet = await GetWalletAsync(payment.PayeeId);

            // In 30/70 direct deposit, the expert already holds the full payment amount.
            // We just need to claw back the refundToClientAmount from the expert.
            if (payeeWallet.AvailableBalance < refundToClientAmount) throw new ValidationException("Expert has insufficient funds for refund split.");

            // 1. Move money
            payeeWallet.AvailableBalance -= refundToClientAmount;
            payerWallet.AvailableBalance += refundToClientAmount;
            // Note: TotalEarned doesn't need adjustment if it was already credited when deposit was paid, 
            // except we should reduce it if we claw back. Let's adjust it by the clawed back amount.
            payeeWallet.TotalEarned -= refundToClientAmount;

            // 2. Update Payment
            payment.Status = PaymentStatus.RELEASED;

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
                    PaymentId = payment.Id
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
                    PaymentId = payment.Id
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
}
