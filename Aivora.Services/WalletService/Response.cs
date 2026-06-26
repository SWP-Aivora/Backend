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
        public string BalanceType => "SIMULATED_DEMO_BALANCE";
        public string Explanation => "This wallet displays simulated demo balances for platform testing and preview purposes only.";
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
        public string BusinessMeaning => Type switch
        {
            WalletTransactionType.ESCROW_HOLD => "Simulated Direct Transfer Initiated",
            WalletTransactionType.PAYMENT_RELEASE => "Receipt/payment record completed",
            WalletTransactionType.DEMO_DEPOSIT => "Demo balance deposited",
            WalletTransactionType.REFUND => "Simulated transfer reversed",
            _ => Type.ToString()
        };
        public string Explanation => "This ledger tracks simulated events and demo-balance movements.";
    }

    public class DepositResultResponse
    {
        public WalletResponse Wallet { get; set; } = null!;
        public TransactionResponse Transaction { get; set; } = null!;
    }

    public class VnPayDepositResponse
    {
        public string PaymentUrl { get; set; } = null!;
        public string TxnRef { get; set; } = null!;
    }

    /// <summary>
    ///   VNPay IPN callback response format.
    ///   Required by VNPay protocol: {RspCode, Message}.
    ///   Intentionally does NOT use ApiResponse wrapper.
    /// </summary>
    public class VnPayIpnResponse
    {
        public string RspCode { get; set; } = null!;
        public string Message { get; set; } = null!;
    }
}
