using Aivora.Repositories.Entities;

namespace Aivora.Services.WalletService;

public interface IVNPayService
{
    /// <summary>
    /// Tạo URL thanh toán VNPay Sandbox
    /// </summary>
    VnPayPaymentResult CreatePaymentUrl(Guid userId, decimal amount, string orderInfo, Wallet wallet);

    /// <summary>
    /// Xác thực chữ ký và xử lý IPN callback từ VNPay
    /// </summary>
    Task<VnPayIpnResult> ProcessIpnCallbackAsync(Dictionary<string, string?> queryParams);
}

public class VnPayPaymentResult
{
    public string PaymentUrl { get; set; } = string.Empty;
    public string TxnRef { get; set; } = string.Empty;
}

public class VnPayIpnResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public decimal? Amount { get; set; }
    public string? TxnRef { get; set; }
    public bool IsDuplicate { get; set; }
}
