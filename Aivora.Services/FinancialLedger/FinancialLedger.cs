using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.FinancialLedger;

public class FinancialLedger : IFinancialLedger
{
    private readonly AivoraDbContext _dbContext;

    public FinancialLedger(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EscrowFundsAsync(Guid payerId, Guid milestoneId, decimal amount, string description)
    {
        var wallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == payerId);
        if (wallet == null) throw new NotFoundException("Payer wallet not found.");
        if (wallet.AvailableBalance < amount) throw new ValidationException("Insufficient balance.");

        var milestone = await _dbContext.Milestones.Include(m => m.Project).FirstOrDefaultAsync(m => m.Id == milestoneId);
        if (milestone == null) throw new NotFoundException("Milestone not found.");

        wallet.AvailableBalance -= amount;
        wallet.HeldBalance += amount;

        var payment = new Payment
        {
            MilestoneId = milestoneId,
            ProjectId = milestone.ProjectId,
            PayerId = payerId,
            PayeeId = milestone.Project.ExpertId,
            Amount = amount,
            Currency = wallet.Currency,
            Status = PaymentStatus.HELD,
            HeldAt = DateTimeOffset.UtcNow
        };

        _dbContext.Payments.Add(payment);

        _dbContext.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id,
            UserId = payerId,
            Amount = amount,
            Type = WalletTransactionType.ESCROW_HOLD,
            Direction = TransactionDirection.DEBIT,
            Description = description,
            BalanceBefore = wallet.AvailableBalance + amount,
            BalanceAfter = wallet.AvailableBalance,
            PaymentId = payment.Id
        });

        await _dbContext.SaveChangesAsync();
    }

    public async Task ReleaseFundsAsync(Guid milestoneId, decimal amount, string description)
    {
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId && p.Status == PaymentStatus.HELD);
        if (payment == null) throw new NotFoundException("Held payment not found for this milestone.");

        var payerWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == payment.PayerId);
        var payeeWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == payment.PayeeId);

        if (payerWallet == null || payeeWallet == null) throw new NotFoundException("Wallets not found.");
        if (payerWallet.HeldBalance < amount) throw new ValidationException("Insufficient held funds.");

        payerWallet.HeldBalance -= amount;
        payeeWallet.AvailableBalance += amount;
        payeeWallet.TotalEarned += amount;

        payment.Status = PaymentStatus.RELEASED;
        payment.ReleasedAt = DateTimeOffset.UtcNow;

        _dbContext.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = payerWallet.Id,
            UserId = payerWallet.UserId,
            Amount = amount,
            Type = WalletTransactionType.PAYMENT_RELEASE,
            Direction = TransactionDirection.DEBIT,
            Description = description,
            BalanceBefore = payerWallet.HeldBalance + amount,
            BalanceAfter = payerWallet.HeldBalance,
            PaymentId = payment.Id
        });

        _dbContext.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = payeeWallet.Id,
            UserId = payeeWallet.UserId,
            Amount = amount,
            Type = WalletTransactionType.PAYMENT_RELEASE,
            Direction = TransactionDirection.CREDIT,
            Description = description,
            BalanceBefore = payeeWallet.AvailableBalance - amount,
            BalanceAfter = payeeWallet.AvailableBalance,
            PaymentId = payment.Id
        });

        await _dbContext.SaveChangesAsync();
    }

    public async Task RefundFundsAsync(Guid milestoneId, decimal amount, string description)
    {
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId && (p.Status == PaymentStatus.HELD || p.Status == PaymentStatus.FROZEN));
        if (payment == null) throw new NotFoundException("Held/Frozen payment not found.");

        var payerWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == payment.PayerId);
        if (payerWallet == null) throw new NotFoundException("Payer wallet not found.");

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
            Description = description,
            BalanceBefore = payerWallet.AvailableBalance - amount,
            BalanceAfter = payerWallet.AvailableBalance,
            PaymentId = payment.Id
        });

        await _dbContext.SaveChangesAsync();
    }

    public async Task SplitFundsAsync(Guid milestoneId, decimal releaseAmount, decimal refundAmount, string description)
    {
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId && (p.Status == PaymentStatus.HELD || p.Status == PaymentStatus.FROZEN));
        if (payment == null) throw new NotFoundException("Held/Frozen payment not found.");

        if (releaseAmount + refundAmount != payment.Amount)
            throw new ValidationException("Total split amounts must equal payment amount.");

        var payerWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == payment.PayerId);
        var payeeWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == payment.PayeeId);

        payerWallet!.HeldBalance -= (releaseAmount + refundAmount);
        payerWallet.AvailableBalance += refundAmount;
        
        payeeWallet!.AvailableBalance += releaseAmount;
        payeeWallet.TotalEarned += releaseAmount;

        payment.Status = PaymentStatus.RELEASED; // Marking as released overall
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();
    }
}
