using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.WalletService;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
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

        var service = new Service(dbContext);
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
}
