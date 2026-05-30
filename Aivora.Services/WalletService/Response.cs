using Aivora.Repositories.Enums;

namespace Aivora.Services.WalletService;

public class Response
{
    public class WalletResponse
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public decimal AvailableBalance { get; set; }
        public decimal HeldBalance { get; set; }
        public decimal TotalEarned { get; set; }
        public string Currency { get; set; } = null!;
        public DateTimeOffset? UpdatedAt { get; set; }
    }

    public class TransactionResponse
    {
        public Guid Id { get; set; }
        public Guid WalletId { get; set; }
        public Guid? PaymentId { get; set; }
        public WalletTransactionType Type { get; set; }
        public TransactionDirection Direction { get; set; }
        public decimal Amount { get; set; }
        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }
        public string? Description { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class DepositResultResponse
    {
        public WalletResponse Wallet { get; set; } = null!;
        public TransactionResponse Transaction { get; set; } = null!;
    }
}
