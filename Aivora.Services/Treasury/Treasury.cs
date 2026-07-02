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

    public Treasury(AivoraDbContext dbContext, ILogger<Treasury> logger, NotificationService.IService notificationService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _notificationService = notificationService;
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
                Description = $"Funding milestone: {milestone.Title}",
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

            // Send notification to the Expert that the milestone has been funded
            try
            {
                await _notificationService.SendNotificationAsync(
                    milestone.Project.ExpertId,
                    "Milestone has been funded",
                    $"The client has funded milestone \"{milestone.Title}\". You can start working on it.",
                    "MILESTONE",
                    $"/projects/{milestone.ProjectId}/milestones/{milestoneId}"
                );
            }
            catch
            {
                // Notification failure should not block the main business flow
            }

            _logger.LogInformation("✅ Milestone {MilestoneId} funded successfully by Client {ClientId}", milestoneId, clientId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ Failed to fund milestone {MilestoneId}", milestoneId);
            throw;
        }
    }

    public async Task ReleaseMilestoneAsync(Guid clientId, Guid milestoneId)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);

        if (milestone.Project.ClientId != clientId) throw new UnauthorizedException("Only the client can approve and release funds.");
        // SUBMITTED: normal client-approve path (MilestoneService.ApproveMilestoneAsync).
        // DISPUTED: admin dispute-resolution path only (DisputeService.ResolveDisputeAsync
        // calls this directly, bypassing MilestoneService, which deliberately blocks
        // approving a DISPUTED milestone itself).
        if (milestone.Status != MilestoneStatus.SUBMITTED && milestone.Status != MilestoneStatus.DISPUTED)
            throw new ValidationException("Milestone must be in SUBMITTED or DISPUTED status to be released.");

        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId && (p.Status == PaymentStatus.HELD || p.Status == PaymentStatus.FROZEN));
        if (payment == null) throw new NotFoundException("Held or frozen payment not found for this milestone.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var payerWallet = await GetWalletAsync(payment.PayerId);
            var payeeWallet = await GetWalletAsync(payment.PayeeId);

            if (payerWallet.HeldBalance < payment.Amount) throw new ValidationException("Insufficient held funds in payer wallet.");

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
                Description = $"Payment released for milestone: {milestone.Title}",
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
                Description = $"Payment received for milestone: {milestone.Title}",
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

            // Send notification to the Expert that the milestone has been approved and paid
            try
            {
                await _notificationService.SendNotificationAsync(
                    milestone.Project.ExpertId,
                    "Milestone approved and paid",
                    $"The client has approved milestone \"{milestone.Title}\" and the payment has been released to your wallet.",
                    "MILESTONE",
                    $"/projects/{milestone.ProjectId}/milestones/{milestoneId}"
                );
            }
            catch
            {
                // Notification failure should not block the main business flow
            }

            _logger.LogInformation("✅ Funds released for Milestone {MilestoneId}", milestoneId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ Failed to release funds for Milestone {MilestoneId}", milestoneId);
            throw;
        }
    }

    public async Task RefundMilestoneAsync(Guid adminId, Guid milestoneId, decimal amount, string reason)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId && (p.Status == PaymentStatus.HELD || p.Status == PaymentStatus.FROZEN));

        if (payment == null) throw new NotFoundException("Held/Frozen payment not found for refund.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var payerWallet = await GetWalletAsync(payment.PayerId);
            if (payerWallet.HeldBalance < amount) throw new ValidationException("Insufficient held funds for refund.");

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
                Description = $"Refund for milestone: {reason}",
                BalanceBefore = payerWallet.AvailableBalance - amount,
                BalanceAfter = payerWallet.AvailableBalance,
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
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId && (p.Status == PaymentStatus.HELD || p.Status == PaymentStatus.FROZEN));

        if (payment == null) throw new NotFoundException("Held/Frozen payment not found for split.");
        if (releaseToExpertAmount + refundToClientAmount != payment.Amount)
            throw new ValidationException("Total split amounts must equal payment amount.");

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
                Description = $"Dispute split: Refunded part. {reason}",
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
                Description = $"Dispute split: Released part. {reason}",
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
            _logger.LogError(ex, "❌ Failed to split funds for milestone {MilestoneId}", milestoneId);
            throw;
        }
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
