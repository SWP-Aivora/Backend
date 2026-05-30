using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class WalletTransaction : BaseEntity
{
    public Guid WalletId { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid UserId { get; set; }
    public WalletTransactionType Type { get; set; }
    public TransactionDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Description { get; set; }

    // Navigation Properties
    public virtual Wallet Wallet { get; set; } = null!;
    public virtual Payment? Payment { get; set; }
    public virtual User User { get; set; } = null!;
}
