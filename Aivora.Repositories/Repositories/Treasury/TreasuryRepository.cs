using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Repositories.Repositories.Treasury;

public class TreasuryRepository : ITreasuryRepository
{
    private readonly AivoraDbContext _dbContext;

    public TreasuryRepository(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Milestone?> GetMilestoneWithProjectAsync(Guid milestoneId)
    {
        return _dbContext.Milestones
            .Include(m => m.Project).ThenInclude(p => p.Milestones)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);
    }

    public Task<Wallet?> GetWalletByUserIdAsync(Guid userId)
    {
        return _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
    }

    public Task<Payment?> GetPaymentByMilestoneAsync(Guid milestoneId)
    {
        return _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId);
    }

    public Task<Payment?> GetPaymentByMilestoneAndStatusAsync(Guid milestoneId, PaymentStatus status)
    {
        return _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId && p.Status == status);
    }

    public Task<Payment?> GetHeldOrFrozenPaymentByMilestoneAsync(Guid milestoneId)
    {
        return _dbContext.Payments.FirstOrDefaultAsync(p =>
            p.MilestoneId == milestoneId &&
            (p.Status == PaymentStatus.HELD || p.Status == PaymentStatus.FROZEN));
    }

    public Task<Project?> GetProjectWithMilestonesAndJobAsync(Guid projectId)
    {
        return _dbContext.Projects
            .Include(p => p.Milestones)
            .Include(p => p.Job)
            .FirstOrDefaultAsync(p => p.Id == projectId);
    }

    public Task<Project?> GetProjectByIdAsync(Guid projectId)
    {
        return _dbContext.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
    }

    public async Task AddPaymentAsync(Payment payment)
    {
        await _dbContext.Payments.AddAsync(payment);
    }

    public void AddWalletTransaction(WalletTransaction walletTransaction)
    {
        _dbContext.WalletTransactions.Add(walletTransaction);
    }

    public Task SaveChangesAsync()
    {
        return _dbContext.SaveChangesAsync();
    }
}
