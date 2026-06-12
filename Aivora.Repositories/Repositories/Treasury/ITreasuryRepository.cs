using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Repositories.Treasury;

public interface ITreasuryRepository
{
    Task<Milestone?> GetMilestoneWithProjectAsync(Guid milestoneId);
    Task<Wallet?> GetWalletByUserIdAsync(Guid userId);
    Task<Payment?> GetPaymentByMilestoneAsync(Guid milestoneId);
    Task<Payment?> GetPaymentByMilestoneAndStatusAsync(Guid milestoneId, PaymentStatus status);
    Task<Payment?> GetHeldOrFrozenPaymentByMilestoneAsync(Guid milestoneId);
    Task<Project?> GetProjectWithMilestonesAndJobAsync(Guid projectId);
    Task<Project?> GetProjectByIdAsync(Guid projectId);
    Task AddPaymentAsync(Payment payment);
    void AddWalletTransaction(WalletTransaction walletTransaction);
    Task SaveChangesAsync();
}
