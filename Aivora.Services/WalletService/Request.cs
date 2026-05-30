namespace Aivora.Services.WalletService;

public class Request
{
    public class DepositDemoRequest
    {
        public decimal Amount { get; set; }
        public string? Description { get; set; }
    }
}
