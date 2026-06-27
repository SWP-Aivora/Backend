using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Aivora.Services.WalletService;

public class VNPayService : IVNPayService
{
    private readonly IConfiguration _configuration;
    private readonly AivoraDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public VNPayService(IConfiguration configuration, AivoraDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _configuration = configuration;
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    private void ValidateVnPayConfiguration()
    {
        _ = _configuration["VNPay:TmnCode"]
            ?? throw new InvalidOperationException("VNPay:TmnCode is not configured.");

        _ = _configuration["VNPay:HashSecret"]
            ?? throw new InvalidOperationException("VNPay:HashSecret is not configured.");

        _ = _configuration["VNPay:ReturnUrl"]
            ?? throw new InvalidOperationException("VNPay:ReturnUrl is not configured.");

        _ = _configuration["VNPay:IpnUrl"]
            ?? throw new InvalidOperationException("VNPay:IpnUrl is not configured.");
    }

    public VnPayPaymentResult CreatePaymentUrl(Guid userId, decimal amount, string orderInfo, Wallet wallet)
    {
        ValidateVnPayConfiguration();

        var tmnCode = _configuration["VNPay:TmnCode"];
        var hashSecret = _configuration["VNPay:HashSecret"];
        var baseUrl = _configuration["VNPay:BaseUrl"]
            ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        var returnUrl = _configuration["VNPay:ReturnUrl"];
        var ipnUrl = _configuration["VNPay:IpnUrl"];

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
            { "vnp_IpAddr", GetClientIpAddress() },
            { "vnp_Locale", "vn" },
            { "vnp_OrderInfo", orderInfo },
            { "vnp_OrderType", "other" },
            { "vnp_ReturnUrl", returnUrl },
            { "vnp_TxnRef", txnRef }
        };

        // Only include IpnUrl if configured; VNPAY sandbox may reject custom IpnUrl
        if (!string.IsNullOrEmpty(ipnUrl))
        {
            vnpParams["vnp_IpnUrl"] = ipnUrl;
        }

        var signData = new StringBuilder();
        foreach (var kvp in vnpParams)
        {
            if (!string.IsNullOrEmpty(kvp.Value))
            {
                if (signData.Length > 0) signData.Append('&');
                signData.Append(kvp.Key);
                signData.Append('=');
                signData.Append(VnPayUrlEncode(kvp.Value));
            }
        }

        var secureHash = ComputeHmacSha512(hashSecret, signData.ToString());
        vnpParams["vnp_SecureHash"] = secureHash;

        var queryString = string.Join('&', vnpParams.Select(kvp =>
            $"{kvp.Key}={VnPayUrlEncode(kvp.Value)}"));

        return new VnPayPaymentResult
        {
            PaymentUrl = $"{baseUrl}?{queryString}",
            TxnRef = txnRef
        };
    }

    public async Task<VnPayIpnResult> ProcessIpnCallbackAsync(Dictionary<string, string?> queryParams)
    {
        ValidateVnPayConfiguration();

        var hashSecret = _configuration["VNPay:HashSecret"];

        // 1. Verify secure hash
        if (!queryParams.TryGetValue("vnp_SecureHash", out var receivedHash) || string.IsNullOrEmpty(receivedHash))
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
                signData.Append(VnPayUrlEncode(kvp.Value));
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
        if (string.IsNullOrEmpty(txnRef))
            return new VnPayIpnResult { IsSuccess = false, Message = "Missing vnp_TxnRef." };

        if (!queryParams.TryGetValue("vnp_Amount", out var amountStr) || string.IsNullOrEmpty(amountStr))
            return new VnPayIpnResult { IsSuccess = false, Message = "Missing vnp_Amount." };

        if (!decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amountRaw))
            return new VnPayIpnResult { IsSuccess = false, Message = "Invalid vnp_Amount format." };

        var amount = amountRaw / 100m;

        // Parse userId from txnRef: "{userId}_{timestamp}"
        var underscoreIndex = txnRef.IndexOf('_');
        if (underscoreIndex < 0 || !Guid.TryParse(txnRef[..underscoreIndex], out var userId))
            return new VnPayIpnResult { IsSuccess = false, Message = "Invalid vnp_TxnRef format." };

        // 4. Process payment atomically (unique constraint on ExternalTxnRef prevents duplicates)
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // Check for existing transaction FIRST to prevent race conditions
            var existingTransaction = await _dbContext.WalletTransactions
                .FirstOrDefaultAsync(t => t.ExternalTxnRef == txnRef);

            if (existingTransaction != null)
            {
                await transaction.RollbackAsync();
                return new VnPayIpnResult
                {
                    IsSuccess = true,
                    Message = "Duplicate transaction - already processed.",
                    UserId = userId,
                    Amount = amount,
                    TxnRef = txnRef,
                    IsDuplicate = true
                };
            }

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
                ExternalTxnRef = txnRef,
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
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
        {
            await transaction.RollbackAsync();
            return new VnPayIpnResult
            {
                IsSuccess = true,
                Message = "Duplicate transaction - already processed.",
                UserId = userId,
                Amount = amount,
                TxnRef = txnRef,
                IsDuplicate = true
            };
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return new VnPayIpnResult { IsSuccess = false, Message = "Internal error processing payment." };
        }
    }

    /// <summary>
    /// URL-encodes a value using PHP-compatible urlencode (spaces as '+')
    /// Note: VNPAY server uses PHP urlencode which encodes spaces as '+'.
    /// We must NOT use Uri.EscapeDataString directly because it encodes spaces as '%20'.
    /// </summary>
    private static string VnPayUrlEncode(string value)
    {
        return Uri.EscapeDataString(value).Replace("%20", "+");
    }

    private static string ComputeHmacSha512(string key, string data)
    {
        return HashUtil.ComputeHmacSha512(key, data);
    }

    private string GetClientIpAddress()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return "127.0.0.1";

        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ip = forwardedFor.Split(',')[0].Trim();
            // VNPAY requires IPv4; map IPv6 loopback to IPv4
            return ip == "::1" ? "127.0.0.1" : ip;
        }

        var remoteIp = httpContext.Connection.RemoteIpAddress;
        if (remoteIp == null) return "127.0.0.1";

        // Map IPv6 loopback to IPv4 for VNPAY compatibility
        return remoteIp.ToString() == "::1" ? "127.0.0.1" : remoteIp.ToString();
    }
}
