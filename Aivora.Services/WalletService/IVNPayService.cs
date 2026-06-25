namespace Aivora.Services.WalletService;

public interface IVNPayService
{
    /// <summary>
    /// Tạo URL thanh toán VNPay Sandbox
    /// </summary>
    string CreatePaymentUrl(Guid userId, decimal amount, string orderInfo);

    /// <summary>
    /// Xác thực chữ ký và xử lý IPN callback từ VNPay
    /// </summary>
    Task<VnPayIpnResult> ProcessIpnCallbackAsync(Dictionary<string, string> queryParams);
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
