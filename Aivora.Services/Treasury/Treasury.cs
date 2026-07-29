using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.NotificationService;
using Microsoft.EntityFrameworkCore;
using Aivora.Services.Extensions;
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
    private readonly EscrowOptions _escrow;

    public Treasury(
        AivoraDbContext dbContext,
        ICommissionCalculator commissionCalculator,
        ILogger<Treasury> logger,
        NotificationService.IService notificationService,
        RealtimeService.IService realtimeService,
        IOptions<EscrowOptions> escrowOptions)
    {
        _dbContext = dbContext;
        _commissionCalculator = commissionCalculator;
        _logger = logger;
        _notificationService = notificationService;
        _realtimeService = realtimeService;
        _escrow = escrowOptions.Value;
    }

    private const string MilestoneMustBeCreatedError = "Milestone must be CREATED to pay deposit.";
    private const string MilestoneMustBeSubmittedError = "Milestone must be in SUBMITTED status to release remaining funds.";
    private const string MilestoneConcurrentlyModifiedError = "Milestone was already modified by a concurrent operation. Please retry.";

    public async Task<TreasuryResult> PayDepositAsync(Guid clientId, Guid milestoneId)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);

        if (milestone.Project.ClientId != clientId) throw new ForbiddenException("Access denied.");
        if (milestone.Status != MilestoneStatus.CREATED) throw new ValidationException(MilestoneMustBeCreatedError);

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var wallets = await GetWalletsForUpdateAsync(clientId, milestone.Project.ExpertId);
            var clientWallet = wallets.First(w => w.UserId == clientId);
            var expertWallet = wallets.First(w => w.UserId == milestone.Project.ExpertId);

            var depositAmount = milestone.Amount * _escrow.DepositRate;

            if (clientWallet.AvailableBalance < depositAmount) throw new ValidationException("Insufficient balance for deposit.");

            // Claim the milestone before moving any money. Status is a concurrency token, so if a
            // concurrent PayDepositAsync call already flipped CREATED -> IN_PROGRESS and committed,
            // this throws DbUpdateConcurrencyException here — before any wallet debit or Payment is staged.
            milestone.Status = MilestoneStatus.IN_PROGRESS;
            milestone.DepositPaidAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync();

            var clientBalanceBefore = clientWallet.AvailableBalance;
            var expertBalanceBefore = expertWallet.AvailableBalance;

            // 1. Move money directly
            if (!clientWallet.CanDebit(depositAmount, out var debitError))
            {
                throw new ValidationException(debitError!);
            }
            clientWallet.Debit(depositAmount);
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
                Description = $"Paid {_escrow.DepositRate * 100:0}% deposit for milestone: {milestone.Title}",
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
                Description = $"Received {_escrow.DepositRate * 100:0}% deposit for milestone: {milestone.Title}",
                BalanceBefore = expertBalanceBefore,
                BalanceAfter = expertWallet.AvailableBalance,
                PaymentId = payment.Id
            });

            // 4. Milestone Step + Project status (Milestone.Status/DepositPaidAt already claimed above)
            var hasFundedStep = milestone.Steps?.Any(s => s.Title == "Funded") == true ||
                await _dbContext.MilestoneSteps.AnyAsync(s => s.MilestoneId == milestoneId && s.Title == "Funded");

            if (!hasFundedStep)
            {
                var depositMaxOrderIndex = await _dbContext.MilestoneSteps
                    .Where(s => s.MilestoneId == milestoneId)
                    .Select(s => (int?)s.OrderIndex)
                    .MaxAsync() ?? 0;

                _dbContext.MilestoneSteps.Add(new MilestoneStep
                {
                    MilestoneId = milestoneId,
                    Title = "Funded",
                    Description = "Milestone funded",
                    OrderIndex = depositMaxOrderIndex + 1,
                    Status = MilestoneStepStatus.COMPLETED,
                    CompletedAt = DateTimeOffset.UtcNow,
                    CompletedByUserId = clientId
                });
            }

            if (milestone.Project.Status == ProjectStatus.PENDING_PAYMENT)
            {
                milestone.Project.Status = ProjectStatus.ACTIVE;
                milestone.Project.StartDate = DateOnly.FromDateTime(DateTime.UtcNow);
            }
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _realtimeService.SendMilestoneUpdatedAsync(milestone.ProjectId, milestoneId);

            _notificationService.SendInBackground(
                milestone.Project.ExpertId,
                "Milestone deposit paid",
                $"The client has paid the {_escrow.DepositRate * 100:0}% deposit for milestone \"{milestone.Title}\". You can start working on it.",
                "MILESTONE",
                $"/projects/{milestone.ProjectId}/milestones/{milestoneId}"
            );
            _logger.LogInformation("✅ Milestone {MilestoneId} {DepositRatePercent}% deposit paid by Client {ClientId}", milestoneId, _escrow.DepositRate * 100, clientId);
            return new TreasuryResult(milestone, clientWallet, expertWallet, [payment]);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Milestone.Status is a concurrency token: a concurrent PayDepositAsync call already
            // flipped CREATED -> IN_PROGRESS and committed first. Lose gracefully with the same
            // error the caller would get from a normal sequential double-fund attempt.
            await transaction.RollbackAsync();
            _logger.LogWarning("⚠️ Lost the fund race for milestone {MilestoneId}: already claimed by a concurrent request", milestoneId);
            throw new ValidationException(MilestoneMustBeCreatedError);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ Failed to pay deposit for milestone {MilestoneId}", milestoneId);
            throw;
        }
    }
    public async Task<TreasuryResult> PayRemainingAsync(Guid clientId, Guid milestoneId)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);

        if (milestone.Project.ClientId != clientId) throw new ForbiddenException("Only the client can approve and release funds.");
        if (milestone.Status == MilestoneStatus.DISPUTED)
            throw new ValidationException("Cannot release remaining funds while the milestone is disputed.");
        if (milestone.Status != MilestoneStatus.SUBMITTED)
            throw new ValidationException(MilestoneMustBeSubmittedError);

        var hasActiveDispute = await _dbContext.Disputes
            .AnyAsync(d => d.MilestoneId == milestoneId &&
                          (d.Status == DisputeStatus.OPEN || d.Status == DisputeStatus.UNDER_REVIEW));
        if (hasActiveDispute)
            throw new ValidationException("Cannot release remaining funds while there is an active dispute.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var wallets = await GetWalletsForUpdateAsync(clientId, milestone.Project.ExpertId, SystemConstants.SystemUserId);
            var clientWallet = wallets.First(w => w.UserId == clientId);
            var expertWallet = wallets.First(w => w.UserId == milestone.Project.ExpertId);
            var platformWallet = wallets.First(w => w.UserId == SystemConstants.SystemUserId);

            var remainingAmount = milestone.Amount * _escrow.RemainingRate;
            var commissionAmount = _commissionCalculator.CalculateCommission(milestone.Amount);
            var expertAmount = remainingAmount - commissionAmount;

            if (clientWallet.AvailableBalance < remainingAmount) throw new ValidationException("Insufficient balance for remaining payment.");

            // Claim the milestone before moving any money (same pattern as PayDepositAsync):
            // a concurrent release already committed first shows up here as
            // DbUpdateConcurrencyException, before any wallet debit/credit is staged.
            milestone.Status = MilestoneStatus.RELEASED;
            milestone.ApprovedAt = DateTimeOffset.UtcNow;
            milestone.PaidAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync();

            var clientBalanceBefore = clientWallet.AvailableBalance;
            var expertBalanceBefore = expertWallet.AvailableBalance;

            // 1. Move money directly
            if (!clientWallet.CanDebit(remainingAmount, out var debitError))
            {
                throw new ValidationException(debitError!);
            }
            clientWallet.Debit(remainingAmount);
            expertWallet.Credit(expertAmount);
            platformWallet.Credit(commissionAmount);
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
                Description = $"Paid {_escrow.RemainingRate * 100:0}% remaining for milestone: {milestone.Title}",
                BalanceBefore = clientBalanceBefore,
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
                BalanceBefore = expertBalanceBefore,
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
                    Description = $"{_commissionCalculator.Rate * 100:0}% Platform fee for milestone: {milestone.Title}",
                    BalanceBefore = platformWallet.AvailableBalance - commissionAmount,
                    BalanceAfter = platformWallet.AvailableBalance,
                    PaymentId = payment.Id
                });
            }

            // Milestone.Status was already claimed as RELEASED above, before any money moved.

            var hasCompletedStep = milestone.Steps?.Any(s => s.Title == "Completed") == true ||
                await _dbContext.MilestoneSteps.AnyAsync(s => s.MilestoneId == milestoneId && s.Title == "Completed");

            if (!hasCompletedStep)
            {
                var completedMaxOrderIndex = milestone.Steps?.Max(s => (int?)s.OrderIndex) ?? 0;

                _dbContext.MilestoneSteps.Add(new MilestoneStep
                {
                    MilestoneId = milestoneId,
                    Title = "Completed",
                    Description = "Milestone completed",
                    OrderIndex = completedMaxOrderIndex + 1,
                    Status = MilestoneStepStatus.COMPLETED,
                    CompletedAt = DateTimeOffset.UtcNow,
                    CompletedByUserId = clientId
                });
            }

            // Data is already loaded with .Include(m => m.Steps) inside GetMilestoneWithProjectAsync
            CompleteRemainingSteps(milestone.Steps, DateTimeOffset.UtcNow);

            await SyncProjectStatusAsync(milestone.ProjectId);

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _realtimeService.SendMilestoneUpdatedAsync(milestone.ProjectId, milestoneId);

            _notificationService.SendInBackground(
                milestone.Project.ExpertId,
                "Milestone approved and paid",
                $"The client has approved milestone \"{milestone.Title}\" and the remaining {_escrow.RemainingRate * 100:0}% has been released to your wallet.",
                "MILESTONE",
                $"/projects/{milestone.ProjectId}/milestones/{milestoneId}"
            );
            _logger.LogInformation("✅ Remaining {RemainingRatePercent}% funds released for Milestone {MilestoneId}", _escrow.RemainingRate * 100, milestoneId);
            return new TreasuryResult(milestone, clientWallet, expertWallet, [payment]);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Same TOCTOU shape as PayDepositAsync: a concurrent operation already changed
            // this milestone's Status and committed first. Fail with the same validation
            // error a sequential double-click on approve would get, not a raw 500.
            await transaction.RollbackAsync();
            _logger.LogWarning("⚠️ Lost the release race for milestone {MilestoneId}: already modified by a concurrent request", milestoneId);
            throw new ValidationException(MilestoneMustBeSubmittedError);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ Failed to release remaining funds for Milestone {MilestoneId}", milestoneId);
            throw;
        }
    }
    public async Task<TreasuryResult> RefundMilestoneAsync(Guid adminId, Guid milestoneId, string reason)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);
        if (milestone.Status == MilestoneStatus.REFUNDED) throw new ValidationException(MilestoneConcurrentlyModifiedError);

        var payments = await _dbContext.Payments.Where(p => p.MilestoneId == milestoneId && p.Status == PaymentStatus.RELEASED).ToListAsync();
        if (!payments.Any()) throw new NotFoundException("Payment not found for refund.");

        var amount = payments.Sum(p => p.Amount);

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var payerId = payments.First().PayerId;
            var payeeId = payments.First().PayeeId;
            var wallets = await GetWalletsForUpdateAsync(payerId, payeeId);
            var payerWallet = wallets.First(w => w.UserId == payerId);
            var payeeWallet = wallets.First(w => w.UserId == payeeId);

            // Claim the milestone before moving any money (same pattern as PayDepositAsync):
            // a concurrent refund/split/release already committed first shows up here as
            // DbUpdateConcurrencyException, before any wallet debit/credit is staged.
            milestone.Status = MilestoneStatus.REFUNDED;
            await _dbContext.SaveChangesAsync();

            foreach (var payment in payments)
            {
                var payerBalanceBefore = payerWallet.AvailableBalance;
                var payeeBalanceBefore = payeeWallet.AvailableBalance;

                // Enforce safe debt limit check to prevent bad debt and prompt manual review
                var maxDebt = _commissionCalculator.MaxDebtLimit;
                if (!payeeWallet.CanDebit(payment.Amount, maxDebt, out var debitError))
                {
                    throw new ValidationException($"Refund failed: Expert's wallet has insufficient funds (Available: {payeeWallet.AvailableBalance} {payeeWallet.Currency}, Debt: {payeeWallet.Debt} {payeeWallet.Currency}). Processing this refund of {payment.Amount} {payeeWallet.Currency} would exceed the safe debt limit of {maxDebt} {payeeWallet.Currency} and requires manual review. Details: {debitError}");
                }

                // 1. Move money back (Clawback from expert to client)
                payeeWallet.Debit(payment.Amount, maxDebt);
                payerWallet.Credit(payment.Amount);

                // Adjust TotalEarned: expert only received payment.Amount - commission
                // NOTE: equality check against a config-driven rate. If DepositRate/RemainingRate
                // changes after payments already exist in DB, old payments can misclassify here
                // (refund would compute commission wrong). Root fix needs a payment-kind column — out of scope.
                var isRemainingPayment = (payment.Amount == milestone.Amount * _escrow.RemainingRate);
                var commissionAmount = isRemainingPayment ? _commissionCalculator.CalculateCommission(milestone.Amount) : 0m;
                var expertActualEarned = payment.Amount - commissionAmount;
                payeeWallet.TotalEarned = Math.Max(0, payeeWallet.TotalEarned - expertActualEarned);

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

            // Milestone.Status was already claimed as REFUNDED above, before any money moved.
            await SyncProjectStatusAsync(milestone.ProjectId);

            await transaction.CommitAsync();

            _logger.LogInformation("✅ Refunded {Amount} for Milestone {MilestoneId}", amount, milestoneId);
            return new TreasuryResult(milestone, ClientWallet: payerWallet, ExpertWallet: payeeWallet, payments);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            _logger.LogWarning("⚠️ Lost the refund race for milestone {MilestoneId}: already modified by a concurrent request", milestoneId);
            throw new ValidationException(MilestoneConcurrentlyModifiedError);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ Failed to refund milestone {MilestoneId}", milestoneId);
            throw;
        }
    }
    public async Task<TreasuryResult> SplitMilestoneFundsAsync(Guid milestoneId, decimal releaseToExpertAmount, decimal refundToClientAmount, string reason)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);
        if (milestone.Status == MilestoneStatus.RELEASED) throw new ValidationException(MilestoneConcurrentlyModifiedError);

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

            // Claim the milestone before moving any money (same pattern as PayDepositAsync):
            // a concurrent refund/split/release already committed first shows up here as
            // DbUpdateConcurrencyException, before any wallet debit/credit is staged.
            milestone.Status = MilestoneStatus.RELEASED;
            milestone.ApprovedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync();

            // In split direct deposit, the expert already holds the full payment amount.
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

                    // Enforce safe debt limit check to prevent bad debt and prompt manual review
                    var maxDebt = _commissionCalculator.MaxDebtLimit;
                    if (!payeeWallet.CanDebit(refundAllocation, maxDebt, out var debitError))
                    {
                        throw new ValidationException($"Split failed: Expert's wallet has insufficient funds (Available: {payeeWallet.AvailableBalance} {payeeWallet.Currency}, Debt: {payeeWallet.Debt} {payeeWallet.Currency}). Processing this clawback of {refundAllocation} {payeeWallet.Currency} would exceed the safe debt limit of {maxDebt} {payeeWallet.Currency} and requires manual review. Details: {debitError}");
                    }

                    payeeWallet.Debit(refundAllocation, maxDebt);
                    payerWallet.Credit(refundAllocation);

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

                // Adjust TotalEarned once outside the loop
                var commissionAmount = _commissionCalculator.CalculateCommission(milestone.Amount);
                var expertEarnedBefore = totalAmount - commissionAmount;
                var delta = releaseToExpertAmount - expertEarnedBefore;
                payeeWallet.TotalEarned += delta;
                payeeWallet.TotalEarned = Math.Max(0, payeeWallet.TotalEarned);
            }

            // 2. Update Payment
            foreach (var payment in payments)
            {
                payment.Status = PaymentStatus.RELEASED;
            }
            // Milestone.Status was already claimed as RELEASED above, before any money moved.

            // Data is already loaded with .Include(m => m.Steps) inside GetMilestoneWithProjectAsync
            CompleteRemainingSteps(milestone.Steps, DateTimeOffset.UtcNow);

            await SyncProjectStatusAsync(milestone.ProjectId);

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("✅ Split {MilestoneId} funds: {ReleaseAmount} released, {RefundAmount} refunded", milestoneId, releaseToExpertAmount, refundToClientAmount);
            return new TreasuryResult(milestone, ClientWallet: payerWallet, ExpertWallet: payeeWallet, payments);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            _logger.LogWarning("⚠️ Lost the split race for milestone {MilestoneId}: already modified by a concurrent request", milestoneId);
            throw new ValidationException(MilestoneConcurrentlyModifiedError);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ Failed to split funds for milestone {MilestoneId}", milestoneId);
            throw;
        }
    }

    public async Task SyncProjectStatusAsync(Guid projectId)
    {
        var project = await _dbContext.Projects
            .Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
        {
            _logger.LogWarning("⚠️ SyncProjectStatusAsync: Project {ProjectId} not found in database.", projectId);
            return;
        }

        await SyncProjectStateAsync(project);
    }

    private async Task SyncProjectStateAsync(Project project)
    {
        // Terminal milestones are PAID or REFUNDED
        var allSettled = project.Milestones.All(m => m.IsSettled);

        if (allSettled && project.Milestones.Any())
        {
            project.Status = ProjectStatus.COMPLETED;
            project.CompletedAt = DateTimeOffset.UtcNow;

            // Load job optionally if it exists in the database to prevent inner join failure in tests
            if (project.JobId.HasValue && project.Job == null)
            {
                project.Job = await _dbContext.JobPosts.FirstOrDefaultAsync(j => j.Id == project.JobId.Value);
            }

            // Sync Job status
            if (project.Job != null)
            {
                project.Job.Status = JobStatus.COMPLETED;
                project.Job.UpdatedAt = DateTimeOffset.UtcNow;
            }
            _logger.LogInformation("🏆 Project {ProjectId} marked as COMPLETED because all milestones are settled.", project.Id);

            // Projects created from a Service offer have no JobId, so there is no Job realtime update to send.
            if (project.JobId.HasValue)
            {
                var affectedUsers = new[] { project.ClientId, project.ExpertId };
                await _realtimeService.SendJobStatusUpdateToUsersAsync(affectedUsers, project.JobId.Value, JobStatus.COMPLETED, project.Job?.Title);
            }
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
    private async Task<Milestone> GetMilestoneWithProjectAsync(Guid milestoneId)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project).ThenInclude(p => p.Milestones)
            .Include(m => m.Steps)
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

    private Task<Wallet> GetWalletForUpdateAsync(Guid userId)
    {
        return _dbContext.GetWalletForUpdateAsync(userId);
    }

    private void CompleteRemainingSteps(ICollection<MilestoneStep>? steps, DateTimeOffset completionTime)
    {
        if (steps == null) return;

        var pendingSteps = steps.Where(s => s.Status == MilestoneStepStatus.PENDING || s.Status == MilestoneStepStatus.IN_PROGRESS || s.Status == MilestoneStepStatus.BLOCKED);
        foreach (var step in pendingSteps)
        {
            step.Status = MilestoneStepStatus.COMPLETED;
            step.CompletedAt = completionTime;
        }
    }
}
