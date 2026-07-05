using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.NotificationService;
using Microsoft.EntityFrameworkCore;
using Aivora.Services.Extensions;
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
            var firstId = clientId;
            var secondId = milestone.Project.ExpertId;
            if (firstId.CompareTo(secondId) > 0)
            {
                (firstId, secondId) = (secondId, firstId);
            }
            var wallet1 = await GetWalletForUpdateAsync(firstId);
            var wallet2 = await GetWalletForUpdateAsync(secondId);
            var clientWallet = (wallet1.UserId == clientId) ? wallet1 : wallet2;
            var expertWallet = (wallet2.UserId == milestone.Project.ExpertId) ? wallet2 : wallet1;

            var depositAmount = milestone.Amount * 0.3m; // 30%

            if (clientWallet.AvailableBalance < depositAmount) throw new ValidationException("Insufficient balance for deposit.");

            var clientBalanceBefore = clientWallet.AvailableBalance;
            var expertBalanceBefore = expertWallet.AvailableBalance;

            // 1. Move money directly
            clientWallet.Debit(depositAmount, bypassDebtLimit: false);
            expertWallet.Credit(depositAmount);
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
                BalanceBefore = clientBalanceBefore,
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
                BalanceBefore = expertBalanceBefore,
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send notification for milestone {MilestoneId}", milestoneId);
            }
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

        if (milestone.Project.ClientId != clientId) throw new UnauthorizedException("Only the client can approve and release funds.");
        if (milestone.Status == MilestoneStatus.DISPUTED)
            throw new ValidationException("Cannot release remaining funds while the milestone is disputed.");
        if (milestone.Status != MilestoneStatus.SUBMITTED)
            throw new ValidationException("Milestone must be in SUBMITTED status to release remaining funds.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var firstId = clientId;
            var secondId = milestone.Project.ExpertId;
            if (firstId.CompareTo(secondId) > 0)
            {
                (firstId, secondId) = (secondId, firstId);
            }
            var wallet1 = await GetWalletForUpdateAsync(firstId);
            var wallet2 = await GetWalletForUpdateAsync(secondId);
            var clientWallet = (wallet1.UserId == clientId) ? wallet1 : wallet2;
            var expertWallet = (wallet2.UserId == milestone.Project.ExpertId) ? wallet2 : wallet1;

            var remainingAmount = milestone.Amount * 0.7m; // 70%

            if (clientWallet.AvailableBalance < remainingAmount) throw new ValidationException("Insufficient balance for remaining payment.");

            var clientBalanceBefore = clientWallet.AvailableBalance;
            var expertBalanceBefore = expertWallet.AvailableBalance;

            // 1. Move money directly
            clientWallet.Debit(remainingAmount, bypassDebtLimit: false);
            expertWallet.Credit(remainingAmount);
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
                BalanceBefore = clientBalanceBefore,
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
                BalanceBefore = expertBalanceBefore,
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send notification for milestone {MilestoneId}", milestoneId);
            }
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

        var amount = payments.Sum(p => p.Amount);

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var payerId = payments.First().PayerId;
            var payeeId = payments.First().PayeeId;
            var firstId = payerId;
            var secondId = payeeId;
            if (firstId.CompareTo(secondId) > 0)
            {
                (firstId, secondId) = (secondId, firstId);
            }
            var wallet1 = await GetWalletForUpdateAsync(firstId);
            var wallet2 = await GetWalletForUpdateAsync(secondId);
            var payerWallet = (wallet1.UserId == payerId) ? wallet1 : wallet2;
            var payeeWallet = (wallet2.UserId == payeeId) ? wallet2 : wallet1;

            // Update and log transactions for each payment
            foreach (var payment in payments)
            {
                var payerBalanceBefore = payerWallet.AvailableBalance;
                var payeeBalanceBefore = payeeWallet.AvailableBalance;

                // 1. Move money back (Clawback from expert to client)
                payeeWallet.Debit(payment.Amount, bypassDebtLimit: true);
                payerWallet.Credit(payment.Amount);
                payeeWallet.TotalEarned -= payment.Amount;

                // 2. Update Payment
                payment.Status = PaymentStatus.REFUNDED;
                payment.RefundedAt = DateTimeOffset.UtcNow;

                // 3. Log Transactions
                _dbContext.WalletTransactions.Add(new WalletTransaction
                {
                    WalletId = payerWallet.Id,
                    UserId = payerWallet.UserId,
                    Amount = payment.Amount,
                    Type = WalletTransactionType.REFUND,
                    Direction = TransactionDirection.CREDIT,
                    Description = $"Refund for milestone payment: {reason}",
                    BalanceBefore = payerBalanceBefore,
                    BalanceAfter = payerWallet.AvailableBalance,
                    PaymentId = payment.Id
                });

                _dbContext.WalletTransactions.Add(new WalletTransaction
                {
                    WalletId = payeeWallet.Id,
                    UserId = payeeWallet.UserId,
                    Amount = payment.Amount,
                    Type = WalletTransactionType.REFUND,
                    Direction = TransactionDirection.DEBIT,
                    Description = $"Clawback for milestone refund: {reason}",
                    BalanceBefore = payeeBalanceBefore,
                    BalanceAfter = payeeWallet.AvailableBalance,
                    PaymentId = payment.Id
                });
            }

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
            var firstId = payerId;
            var secondId = payeeId;
            if (firstId.CompareTo(secondId) > 0)
            {
                (firstId, secondId) = (secondId, firstId);
            }
            var wallet1 = await GetWalletForUpdateAsync(firstId);
            var wallet2 = await GetWalletForUpdateAsync(secondId);
            var payerWallet = (wallet1.UserId == payerId) ? wallet1 : wallet2;
            var payeeWallet = (wallet2.UserId == payeeId) ? wallet2 : wallet1;

            // In 30/70 direct deposit, the expert already holds the full payment amount.
            // We claw back the refundToClientAmount from the expert.

            // 1. Move money & log transactions for the refund portion
            if (refundToClientAmount > 0)
            {
                var remainingRefund = refundToClientAmount;
                foreach (var payment in payments)
                {
                    if (remainingRefund <= 0) break;

                    var refundAllocation = Math.Min(payment.Amount, remainingRefund);
                    remainingRefund -= refundAllocation;

                    var payerBalanceBefore = payerWallet.AvailableBalance;
                    var payeeBalanceBefore = payeeWallet.AvailableBalance;

                    payeeWallet.Debit(refundAllocation, bypassDebtLimit: true);
                    payerWallet.Credit(refundAllocation);
                    payeeWallet.TotalEarned -= refundAllocation;

                    _dbContext.WalletTransactions.Add(new WalletTransaction
                    {
                        WalletId = payerWallet.Id,
                        UserId = payerWallet.UserId,
                        Amount = refundAllocation,
                        Type = WalletTransactionType.REFUND,
                        Direction = TransactionDirection.CREDIT,
                        Description = $"Split resolution refund: {reason}",
                        BalanceBefore = payerBalanceBefore,
                        BalanceAfter = payerWallet.AvailableBalance,
                        PaymentId = payment.Id
                    });

                    _dbContext.WalletTransactions.Add(new WalletTransaction
                    {
                        WalletId = payeeWallet.Id,
                        UserId = payeeWallet.UserId,
                        Amount = refundAllocation,
                        Type = WalletTransactionType.REFUND,
                        Direction = TransactionDirection.DEBIT,
                        Description = $"Split resolution refund clawback: {reason}",
                        BalanceBefore = payeeBalanceBefore,
                        BalanceAfter = payeeWallet.AvailableBalance,
                        PaymentId = payment.Id
                    });
                }
            }

            // 2. Update Payment
            foreach (var payment in payments)
            {
                payment.Status = PaymentStatus.RELEASED;
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

    private Task<Wallet> GetWalletForUpdateAsync(Guid userId)
    {
        return _dbContext.GetWalletForUpdateAsync(userId);
    }


}
