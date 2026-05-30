using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.FinancialLedger;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aivora.Tests.Services;

public class FinancialLedgerTests
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
    public async Task EscrowFundsAsync_ReducesAvailableAndIncreasesHeld()
    {
        // Arrange
        var dbContext = GetDbContext();
        var userId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var wallet = new Wallet { UserId = userId, AvailableBalance = 1000, HeldBalance = 0, Currency = "AICOIN" };
        var project = new Project { Id = Guid.NewGuid(), ClientId = userId, ExpertId = Guid.NewGuid(), Title = "P1" };
        var milestone = new Milestone { Id = milestoneId, ProjectId = project.Id, Amount = 400, Title = "M1" };

        dbContext.Wallets.Add(wallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var ledger = new FinancialLedger(dbContext);

        // Act
        await ledger.EscrowFundsAsync(userId, milestoneId, 400, "Test Escrow");

        // Assert
        var updatedWallet = await dbContext.Wallets.FirstAsync(w => w.UserId == userId);
        updatedWallet.AvailableBalance.Should().Be(600);
        updatedWallet.HeldBalance.Should().Be(400);

        var payment = await dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.HELD);
        
        var tx = await dbContext.WalletTransactions.FirstOrDefaultAsync(t => t.PaymentId == payment.Id);
        tx.Should().NotBeNull();
        tx!.Type.Should().Be(WalletTransactionType.ESCROW_HOLD);
    }

    [Fact]
    public async Task ReleaseFundsAsync_MovesHeldToPayeeAvailable()
    {
        // Arrange
        var dbContext = GetDbContext();
        var payerId = Guid.NewGuid();
        var payeeId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var payerWallet = new Wallet { UserId = payerId, AvailableBalance = 0, HeldBalance = 500, Currency = "AICOIN" };
        var payeeWallet = new Wallet { UserId = payeeId, AvailableBalance = 0, TotalEarned = 0, Currency = "AICOIN" };
        var payment = new Payment { MilestoneId = milestoneId, PayerId = payerId, PayeeId = payeeId, Amount = 500, Status = PaymentStatus.HELD };

        dbContext.Wallets.AddRange(payerWallet, payeeWallet);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var ledger = new FinancialLedger(dbContext);

        // Act
        await ledger.ReleaseFundsAsync(milestoneId, 500, "Test Release");

        // Assert
        var updatedPayer = await dbContext.Wallets.FirstAsync(w => w.UserId == payerId);
        updatedPayer.HeldBalance.Should().Be(0);

        var updatedPayee = await dbContext.Wallets.FirstAsync(w => w.UserId == payeeId);
        updatedPayee.AvailableBalance.Should().Be(500);
        updatedPayee.TotalEarned.Should().Be(500);

        var updatedPayment = await dbContext.Payments.FindAsync(payment.Id);
        updatedPayment!.Status.Should().Be(PaymentStatus.RELEASED);
    }
}
