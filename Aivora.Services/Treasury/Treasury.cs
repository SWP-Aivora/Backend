using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aivora.Services.Treasury;

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
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");
        if (milestone.Project.ClientId != clientId) throw new UnauthorizedException("Access denied.");
        if (milestone.Status != MilestoneStatus.CREATED) throw new ValidationException("Milestone is already funded or processed.");

        var wallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == clientId);
        if (wallet == null) throw new NotFoundException("Client wallet not found.");
        if (wallet.AvailableBalance < milestone.Amount) throw new ValidationException("Insufficient balance.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // 1. Cập nhật số dư ví
            decimal balanceBefore = wallet.AvailableBalance;
            wallet.AvailableBalance -= milestone.Amount;
            wallet.HeldBalance += milestone.Amount;

            // 2. Tạo bản ghi Thanh toán (Payment) ở trạng thái HELD
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

            // 3. Ghi log giao dịch ví
            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = wallet.Id,
                UserId = clientId,
                Amount = milestone.Amount,
                Type = WalletTransactionType.ESCROW_HOLD,
                Direction = TransactionDirection.DEBIT,
                Description = $"Funding milestone: {milestone.Title}",
                BalanceBefore = balanceBefore,
                BalanceAfter = wallet.AvailableBalance,
                PaymentId = payment.Id
            });

            // 4. Cập nhật trạng thái Milestone
            milestone.Status = MilestoneStatus.FUNDED;
            milestone.FundedAt = DateTimeOffset.UtcNow;

            // 5. Tự động kích hoạt Project nếu cần
            if (milestone.Project.Status == ProjectStatus.PENDING_PAYMENT)
            {
                milestone.Project.Status = ProjectStatus.ACTIVE;
                milestone.Project.StartDate = DateOnly.FromDateTime(DateTime.UtcNow);
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            
            _logger?.LogInformation("✅ Milestone {MilestoneId} funded successfully by Client {ClientId}", milestoneId, clientId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger?.LogError(ex, "❌ Failed to fund milestone {MilestoneId}", milestoneId);
            throw;
        }
    }

    public async Task ReleaseMilestoneAsync(Guid clientId, Guid milestoneId)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project).ThenInclude(p => p.Milestones)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");
        if (milestone.Project.ClientId != clientId) throw new UnauthorizedException("Only the client can approve and release funds.");
        if (milestone.Status != MilestoneStatus.SUBMITTED) throw new ValidationException("Milestone must be in SUBMITTED status to be released.");

        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId && p.Status == PaymentStatus.HELD);
        if (payment == null) throw new NotFoundException("Held payment not found.");

        var payerWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == payment.PayerId);
        var payeeWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == payment.PayeeId);

        if (payerWallet == null || payeeWallet == null) throw new NotFoundException("Wallets not found.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // 1. Luân chuyển tiền
            payerWallet.HeldBalance -= payment.Amount;
            payeeWallet.AvailableBalance += payment.Amount;
            payeeWallet.TotalEarned += payment.Amount;

            // 2. Cập nhật trạng thái Payment
            payment.Status = PaymentStatus.RELEASED;
            payment.ReleasedAt = DateTimeOffset.UtcNow;

            // 3. Ghi log giao dịch (Debit cho Client, Credit cho Expert)
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

            // 4. Cập nhật Milestone
            milestone.Status = MilestoneStatus.PAID;
            milestone.ApprovedAt = DateTimeOffset.UtcNow;
            milestone.PaidAt = DateTimeOffset.UtcNow;

            // 5. Tự động hoàn thành Project nếu là milestone cuối cùng
            if (milestone.Project.Milestones.All(m => m.Status == MilestoneStatus.PAID))
            {
                milestone.Project.Status = ProjectStatus.COMPLETED;
                milestone.Project.CompletedAt = DateTimeOffset.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger?.LogInformation("✅ Funds released for Milestone {MilestoneId}", milestoneId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger?.LogError(ex, "❌ Failed to release funds for Milestone {MilestoneId}", milestoneId);
            throw;
        }
    }

    public async Task RefundMilestoneAsync(Guid clientId, Guid milestoneId, decimal amount, string reason)
    {
        // Tương tự logic Refund hiện có nhưng tập trung vào Treasury
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId && (p.Status == PaymentStatus.HELD || p.Status == PaymentStatus.FROZEN));
        if (payment == null) throw new NotFoundException("Held/Frozen payment not found.");

        var payerWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == payment.PayerId);
        if (payerWallet == null) throw new NotFoundException("Payer wallet not found.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            payerWallet.HeldBalance -= amount;
            payerWallet.AvailableBalance += amount;

            payment.Status = PaymentStatus.REFUNDED;
            payment.RefundedAt = DateTimeOffset.UtcNow;

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

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task SplitMilestoneFundsAsync(Guid milestoneId, decimal releaseToExpertAmount, decimal refundToClientAmount, string reason)
    {
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId && (p.Status == PaymentStatus.HELD || p.Status == PaymentStatus.FROZEN));
        if (payment == null) throw new NotFoundException("Held/Frozen payment not found.");

        if (releaseToExpertAmount + refundToClientAmount != payment.Amount)
            throw new ValidationException("Total split amounts must equal payment amount.");

        var payerWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == payment.PayerId);
        var payeeWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == payment.PayeeId);

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // Release to Expert
            if (releaseToExpertAmount > 0)
            {
                payerWallet!.HeldBalance -= releaseToExpertAmount;
                payeeWallet!.AvailableBalance += releaseToExpertAmount;
                payeeWallet.TotalEarned += releaseToExpertAmount;
            }

            // Refund to Client
            if (refundToClientAmount > 0)
            {
                payerWallet!.HeldBalance -= refundToClientAmount;
                payerWallet.AvailableBalance += refundToClientAmount;
            }

            payment.Status = PaymentStatus.RELEASED; // Or a specific status like SETTLED_BY_ADMIN
            payment.UpdatedAt = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
