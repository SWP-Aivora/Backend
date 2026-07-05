using System;
using System.Threading.Tasks;
using Aivora.Repositories.Constants;
using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Options;
using Aivora.Services.Treasury;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class TreasuryCommissionTests
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
    public async Task PayRemainingAsync_DeductsCommission_And_TransfersToPlatformWallet()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        // 1. Create Users
        var clientUser = new User { Id = clientId, Email = "client@a.com", FullName = "Client", Role = UserRole.CLIENT, Status = UserStatus.ACTIVE, PasswordHash = "x" };
        var expertUser = new User { Id = expertId, Email = "expert@a.com", FullName = "Expert", Role = UserRole.EXPERT, Status = UserStatus.ACTIVE, PasswordHash = "x" };
        var systemUser = new User { Id = SystemConstants.SystemUserId, Email = "system@aivora.com", FullName = "System Platform", Role = UserRole.SYSTEM, Status = UserStatus.ACTIVE, PasswordHash = "x" };

        // 2. Create Wallets
        var clientWallet = new Wallet { UserId = clientId, AvailableBalance = 2000, Currency = CurrencyConstants.AICOIN };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 300, TotalEarned = 300, Currency = CurrencyConstants.AICOIN }; // 30% deposit already paid
        var platformWallet = new Wallet { UserId = SystemConstants.SystemUserId, AvailableBalance = 0, TotalEarned = 0, Currency = CurrencyConstants.AICOIN };

        // 3. Create Project & Milestone
        var project = new Project
        {
            Id = projectId,
            JobId = Guid.NewGuid(),
            AcceptedProposalId = Guid.NewGuid(),
            ClientId = clientId,
            ExpertId = expertId,
            Title = "Test Project",
            Status = ProjectStatus.ACTIVE,
            Currency = CurrencyConstants.AICOIN,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var milestone = new Milestone
        {
            Id = milestoneId,
            ProjectId = projectId,
            Amount = 1000, // Total: 1000. Remaining: 700. Commission: 10% * 1000 = 100. Expert gets: 600.
            Status = MilestoneStatus.SUBMITTED, // Ready to release
            Title = "Test Milestone",
            Currency = CurrencyConstants.AICOIN,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Users.AddRange(clientUser, expertUser, systemUser);
        dbContext.Wallets.AddRange(clientWallet, expertWallet, platformWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var commissionOptions = Options.Create(new CommissionOptions { Rate = 0.10m });
        var treasury = new Treasury(
            dbContext,
            new CommissionCalculator(commissionOptions),
            Mock.Of<ILogger<Treasury>>(),
            Mock.Of<Aivora.Services.NotificationService.IService>(),
            new Aivora.Services.RealtimeService.NullRealtimeService()
        );

        // Act
        await treasury.PayRemainingAsync(clientId, milestoneId);

        // Assert
        var updatedClientWallet = await dbContext.Wallets.FirstAsync(w => w.UserId == clientId);
        var updatedExpertWallet = await dbContext.Wallets.FirstAsync(w => w.UserId == expertId);
        var updatedPlatformWallet = await dbContext.Wallets.FirstAsync(w => w.UserId == SystemConstants.SystemUserId);

        // Remaining 700 is deducted from client
        updatedClientWallet.AvailableBalance.Should().Be(1300); // 2000 - 700

        // Expert gets 60% (600) + previous 300 = 900
        updatedExpertWallet.AvailableBalance.Should().Be(900);
        updatedExpertWallet.TotalEarned.Should().Be(900);

        // Platform gets 10% (100)
        updatedPlatformWallet.AvailableBalance.Should().Be(100);
        updatedPlatformWallet.TotalEarned.Should().Be(100);

        // Ensure PLATFORM_FEE transaction log exists
        var feeTransaction = await dbContext.WalletTransactions
            .FirstOrDefaultAsync(t => t.UserId == SystemConstants.SystemUserId && t.Type == WalletTransactionType.PLATFORM_FEE);

        feeTransaction.Should().NotBeNull();
        feeTransaction!.Amount.Should().Be(100);
        feeTransaction.Direction.Should().Be(TransactionDirection.CREDIT);
    }
}

