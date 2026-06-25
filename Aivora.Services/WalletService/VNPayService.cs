using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Aivora.Services.WalletService;

public class VNPayService : IVNPayService
{
    private readonly IConfiguration _configuration;
    private readonly AivoraDbContext _dbContext;

    public VNPayService(IConfiguration configuration, AivoraDbContext dbContext)
    {
        _configuration = configuration;
        _dbContext = dbContext;
    }

    public string CreatePaymentUrl(Guid userId, decimal amount, string orderInfo)
    {
        var tmnCode = _configuration["VNPay:TmnCode"]
            ?? throw new InvalidOperationException("VNPay:TmnCode is not configured.");
        var hashSecret = _configuration["VNPay:HashSecret"]
            ?? throw new InvalidOperationException("VNPay:HashSecret is not configured.");
        var baseUrl = _configuration["VNPay:BaseUrl"]
            ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        var returnUrl = _configuration["VNPay:ReturnUrl"]
            ?? throw new InvalidOperationException("VNPay:ReturnUrl is not configured.");

        var txnRef = $"{userId:N}_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        var vnTime = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
        var createDate = vnTime.ToString("yyyyMMddHHmmss");

        var vnpParams = new SortedDictionary<string, string>
        {
            { "vnp_Version", "2.1.0" },
            { "vnp_Command", "pay" },
            { "vnp_TmnCode", tmnCode },
            { "vnp_Amount", ((long)(amount * 100)).ToString() },
            { "vnp_CreateDate", createDate },
            { "vnp_CurrCode", "VND" },
            { "vnp_IpAddr", "127.0.0.1" },
            { "vnp_Locale", "vn" },
            { "vnp_OrderInfo", orderInfo },
            { "vnp_OrderType", "other" },
            { "vnp_ReturnUrl", returnUrl },
            { "vnp_TxnRef", txnRef }
        };

        var signData = new StringBuilder();
        foreach (var kvp in vnpParams)
        {
            if (!string.IsNullOrEmpty(kvp.Value))
            {
                if (signData.Length > 0) signData.Append('&');
                signData.Append(kvp.Key);
                signData.Append('=');
                signData.Append(Uri.EscapeDataString(kvp.Value));
            }
        }

        var secureHash = ComputeHmacSha512(hashSecret, signData.ToString());
        vnpParams["vnp_SecureHash"] = secureHash;

        var queryString = string.Join('&', vnpParams.Select(kvp =>
            $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));

        return $"{baseUrl}?{queryString}";
    }

    public async Task<VnPayIpnResult> ProcessIpnCallbackAsync(Dictionary<string, string> queryParams)
    {
        var hashSecret = _configuration["VNPay:HashSecret"]
            ?? throw new InvalidOperationException("VNPay:HashSecret is not configured.");

        // 1. Verify secure hash
        if (!queryParams.TryGetValue("vnp_SecureHash", out var receivedHash))
            return new VnPayIpnResult { IsSuccess = false, Message = "Missing vnp_SecureHash." };

        var signData = new StringBuilder();
        foreach (var kvp in queryParams
                     .Where(kvp => kvp.Key.StartsWith("vnp_") && kvp.Key != "vnp_SecureHash")
                     .OrderBy(kvp => kvp.Key))
        {
            if (!string.IsNullOrEmpty(kvp.Value))
            {
                if (signData.Length > 0) signData.Append('&');
                signData.Append(kvp.Key);
                signData.Append('=');
                signData.Append(Uri.EscapeDataString(kvp.Value));
            }
        }

        var computedHash = ComputeHmacSha512(hashSecret, signData.ToString());
        if (!string.Equals(computedHash, receivedHash, StringComparison.OrdinalIgnoreCase))
            return new VnPayIpnResult { IsSuccess = false, Message = "Invalid signature." };

        // 2. Check response code
        if (!queryParams.TryGetValue("vnp_ResponseCode", out var responseCode) || responseCode != "00")
            return new VnPayIpnResult { IsSuccess = false, Message = $"Payment not successful. ResponseCode: {responseCode}" };

        // 3. Extract data
        var txnRef = queryParams.GetValueOrDefault("vnp_TxnRef", string.Empty);
        if (!queryParams.TryGetValue("vnp_Amount", out var amountStr))
            return new VnPayIpnResult { IsSuccess = false, Message = "Missing vnp_Amount." };

        var amount = decimal.Parse(amountStr) / 100m;

        // Parse userId from txnRef: "{userId}_{timestamp}"
        var underscoreIndex = txnRef.IndexOf('_');
        if (underscoreIndex < 0 || !Guid.TryParse(txnRef[..underscoreIndex], out var userId))
            return new VnPayIpnResult { IsSuccess = false, Message = "Invalid vnp_TxnRef format." };

        // 4. Check duplicate via exact match on known description format
        var expectedDescription = $"VNPay deposit. TxnRef: {txnRef}";
        var isDuplicate = await _dbContext.WalletTransactions
            .AnyAsync(t => t.Description == expectedDescription);

        if (isDuplicate)
            return new VnPayIpnResult
            {
                IsSuccess = true,
                Message = "Duplicate transaction - already processed.",
                UserId = userId,
                Amount = amount,
                TxnRef = txnRef,
                IsDuplicate = true
            };

        // 5. Process payment
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var wallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null)
            {
                await transaction.RollbackAsync();
                return new VnPayIpnResult { IsSuccess = false, Message = "Wallet not found." };
            }

            decimal balanceBefore = wallet.AvailableBalance;
            wallet.AvailableBalance += amount;

            var walletTx = new WalletTransaction
            {
                WalletId = wallet.Id,
                UserId = userId,
                Amount = amount,
                Type = WalletTransactionType.DEPOSIT,
                Direction = TransactionDirection.CREDIT,
                Description = $"VNPay deposit. TxnRef: {txnRef}",
                BalanceBefore = balanceBefore,
                BalanceAfter = wallet.AvailableBalance
            };

            _dbContext.WalletTransactions.Add(walletTx);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return new VnPayIpnResult
            {
                IsSuccess = true,
                Message = "Payment processed successfully.",
                UserId = userId,
                Amount = amount,
                TxnRef = txnRef,
                IsDuplicate = false
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return new VnPayIpnResult { IsSuccess = false, Message = $"Transaction failed: {ex.Message}" };
        }
    }

    private static string ComputeHmacSha512(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
