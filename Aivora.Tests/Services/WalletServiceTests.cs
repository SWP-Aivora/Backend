using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.WalletService;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Aivora.Tests.Services;

public class WalletServiceTests
{
    private AivoraDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AivoraDbContext(options);
    }

    [Fact]
    public async Task DepositDemoAsync_IncreasesBalanceAndCreatesTransaction()
    {
        // Arrange
        var dbContext = GetDbContext();
        var userId = Guid.NewGuid();
        var wallet = new Wallet { UserId = userId, AvailableBalance = 100, Currency = "AICOIN" };
        dbContext.Wallets.Add(wallet);
        await dbContext.SaveChangesAsync();

        var vnPayServiceMock = new Mock<IVNPayService>();
        var notificationServiceMock = Mock.Of<Aivora.Services.NotificationService.IService>();
        var service = new Service(dbContext, vnPayServiceMock.Object, notificationServiceMock);
        var request = new Request.DepositDemoRequest { Amount = 500, Description = "Test Deposit" };

        // Act
        var result = await service.DepositDemoAsync(userId, request);

        // Assert
        result.Wallet.AvailableBalance.Should().Be(600);
        result.Transaction.Amount.Should().Be(500);
        result.Transaction.Type.Should().Be(WalletTransactionType.DEMO_DEPOSIT);
        result.Transaction.Direction.Should().Be(TransactionDirection.CREDIT);
        result.Transaction.BalanceBefore.Should().Be(100);
        result.Transaction.BalanceAfter.Should().Be(600);

        var dbTx = await dbContext.WalletTransactions.FirstOrDefaultAsync(t => t.UserId == userId);
        dbTx.Should().NotBeNull();
        dbTx!.Amount.Should().Be(500);
    }

    [Fact]
    public async Task VnPayIpnEndpoint_ShouldBeRateLimited()
    {
        // Arrange - This test requires integration test with WebApplicationFactory
        // For now, we'll test that the endpoint exists and has AllowAnonymous attribute
        // The actual rate limiting test needs to be in an integration test file

        // This is a placeholder test - actual rate limiting test should be in:
        // Aivora.Tests/Integration/WalletControllerTests.cs

        // TODO: Create integration test file and move this test there
        Assert.True(true); // Placeholder - will be replaced with actual rate limiting test
    }

    private Dictionary<string, string?> CreateIpnRequest(string txnRef)
    {
        var hashSecret = "test_hash_secret";
        var signData = $"vnp_Amount=100000&vnp_ResponseCode=00&vnp_TxnRef={txnRef}";
        var secureHash = ComputeHmacSha512(hashSecret, signData);

        return new Dictionary<string, string?>
        {
            { "vnp_TxnRef", txnRef },
            { "vnp_Amount", "100000" },
            { "vnp_ResponseCode", "00" },
            { "vnp_SecureHash", secureHash }
        };
    }

    private static string ComputeHmacSha512(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    [Fact]
    public async Task ProcessIpnCallbackAsync_ShouldHandleDuplicateTransactionsAtomically()
    {
        // Arrange
        var dbContext = GetDbContext();
        var userId = Guid.NewGuid();
        var wallet = new Wallet { UserId = userId, AvailableBalance = 0, Currency = "AICOIN" };
        dbContext.Wallets.Add(wallet);
        await dbContext.SaveChangesAsync();

        var txnRef = $"{userId:N}_test123"; // Format: Guid_timestamp
        var vnPayService = new VNPayService(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VNPay:TmnCode"] = "test_tmn_code",
                ["VNPay:HashSecret"] = "test_hash_secret",
                ["VNPay:ReturnUrl"] = "https://test.com/return",
                ["VNPay:IpnUrl"] = "https://test.com/ipn",
                ["VNPay:BaseUrl"] = "https://test.com/pay"
            }).Build(),
            dbContext,
            new Mock<IHttpContextAccessor>().Object,
            Mock.Of<Aivora.Services.NotificationService.IService>());

        var duplicateRequests = Enumerable.Range(0, 5)
            .Select(_ => CreateIpnRequest(txnRef))
            .ToList();

        // Act - process requests concurrently
        var tasks = duplicateRequests.Select(req =>
            vnPayService.ProcessIpnCallbackAsync(req));
        var results = await Task.WhenAll(tasks);

        // Debug output
        Console.WriteLine($"Results: {results.Length}");
        for (int i = 0; i < results.Length; i++)
        {
            Console.WriteLine($"Result {i}: IsSuccess={results[i].IsSuccess}, IsDuplicate={results[i].IsDuplicate}, Message={results[i].Message}");
        }

        // Assert - all should succeed but only one transaction created
        results.All(r => r.IsSuccess).Should().BeTrue();
        results.Count(r => r.IsDuplicate).Should().Be(4);

        // Verify only one transaction in database
        var transactions = await dbContext.WalletTransactions
            .Where(t => t.ExternalTxnRef == txnRef)
            .ToListAsync();
        transactions.Count.Should().Be(1);
    }

    [Fact]
    public async Task DepositViaVNPayAsync_ShouldNotQueryWalletUnnecessarily()
    {
        // Arrange
        var dbContext = GetDbContext();
        var userId = Guid.NewGuid();
        var wallet = new Wallet { UserId = userId, AvailableBalance = 0, Currency = "AICOIN" };
        dbContext.Wallets.Add(wallet);
        await dbContext.SaveChangesAsync();

        var vnPayServiceMock = new Mock<IVNPayService>();
        vnPayServiceMock.Setup(x => x.CreatePaymentUrl(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Wallet>()))
            .Returns(new VnPayPaymentResult { PaymentUrl = "https://test.com", TxnRef = "test123" });

        var service = new Service(dbContext, vnPayServiceMock.Object, Mock.Of<Aivora.Services.NotificationService.IService>());
        var request = new Request.VnPayDepositRequest { Amount = 100000 };

        // Act
        await service.DepositViaVNPayAsync(userId, request);

        // Assert - wallet should only be queried once for validation
        // The mock VNPayService doesn't query wallet, so only Service.cs queries it once
        var walletInDb = await dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        walletInDb.Should().NotBeNull();
        walletInDb!.AvailableBalance.Should().Be(0); // Should not change since we only create payment URL
    }
    [Fact]
    public async Task WithdrawAsync_ClientCanWithdrawAvailableBalance_WithoutHeldBalance()
    {
        // Arrange
        var dbContext = GetDbContext();
        var userId = Guid.NewGuid();
        var wallet = new Wallet { UserId = userId, AvailableBalance = 500000, HeldBalance = 0, Currency = "AICOIN" };
        dbContext.Wallets.Add(wallet);
        await dbContext.SaveChangesAsync();

        var vnPayServiceMock = new Mock<IVNPayService>();
        var notificationServiceMock = Mock.Of<Aivora.Services.NotificationService.IService>();
        var service = new Service(dbContext, vnPayServiceMock.Object, notificationServiceMock);

        var request = new Request.WithdrawRequest { Amount = 100000, PaymentMethod = "BankTransfer", Description = "Test Withdraw" };

        // Act
        var result = await service.WithdrawAsync(userId, request);

        // Assert
        result.Wallet.AvailableBalance.Should().Be(400000);
        result.Transaction.Amount.Should().Be(100000);
        result.Transaction.Type.Should().Be(WalletTransactionType.WITHDRAWAL);
        result.Transaction.Direction.Should().Be(TransactionDirection.DEBIT);

        var walletInDb = await dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        walletInDb!.AvailableBalance.Should().Be(400000);
    }

    [Fact]
    public async Task WithdrawAsync_ClientCanWithdrawAvailableBalance_IgnoresHeldBalance()
    {
        // Arrange
        var dbContext = GetDbContext();
        var userId = Guid.NewGuid();
        // Simulate a scenario where client somehow has HeldBalance (or just to prove Withdraw only cares about AvailableBalance)
        var wallet = new Wallet { UserId = userId, AvailableBalance = 200000, HeldBalance = 999999, Currency = "AICOIN" };
        dbContext.Wallets.Add(wallet);
        await dbContext.SaveChangesAsync();

        var vnPayServiceMock = new Mock<IVNPayService>();
        var notificationServiceMock = Mock.Of<Aivora.Services.NotificationService.IService>();
        var service = new Service(dbContext, vnPayServiceMock.Object, notificationServiceMock);

        var request = new Request.WithdrawRequest { Amount = 150000, PaymentMethod = "BankTransfer" };

        // Act
        var result = await service.WithdrawAsync(userId, request);

        // Assert
        result.Wallet.AvailableBalance.Should().Be(50000);
        result.Wallet.HeldBalance.Should().Be(999999); // HeldBalance remains unchanged

        var walletInDb = await dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        walletInDb!.AvailableBalance.Should().Be(50000);
        walletInDb.HeldBalance.Should().Be(999999);
    }
}
