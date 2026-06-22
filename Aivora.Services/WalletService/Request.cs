namespace Aivora.Services.WalletService;

public class Request
{
    public class DepositDemoRequest
    {
        public decimal Amount { get; set; }
        public string? Description { get; set; }
    }

    public class WithdrawRequest
    {
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public string? PaymentMethod { get; set; } // "bank", "paypal", "crypto"
    }

    public class DepositRequest
    {
        public decimal Amount { get; set; }
        public string? PaymentMethod { get; set; } // "credit_card", "bank_transfer", "crypto"
        public string? PaymentToken { get; set; } // Token from payment gateway
        public string? Description { get; set; }
    }

    public class TransferRequest
    {
        public Guid RecipientId { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
    }
}
