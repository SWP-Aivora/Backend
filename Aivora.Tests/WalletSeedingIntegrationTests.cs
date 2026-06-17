using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.WalletService;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aivora.Tests;

public class WalletSeedingIntegrationTests
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
    public async Task SeedData_InitializesWalletsForAllUsers()
    {
        // Arrange
        var dbContext = GetDbContext();

        // Act
        await SeedData.Initialize(dbContext, forceReset: true);

        // Assert
        var users = await dbContext.Users.Include(u => u.Wallet).ToListAsync();
        users.Should().NotBeEmpty();

        foreach (var user in users)
        {
            user.Wallet.Should().NotBeNull($"User {user.Email} should have a wallet after seeding");
            user.Wallet!.UserId.Should().Be(user.Id);
            
            if (user.Role == UserRole.CLIENT)
            {
                user.Wallet.AvailableBalance.Should().Be(10000m);
            }
            else
            {
                user.Wallet.AvailableBalance.Should().Be(0m);
            }
        }
    }

    [Fact]
    public async Task WalletService_CanRetrieveSeededWallet()
    {
        // Arrange
        var dbContext = GetDbContext();
        await SeedData.Initialize(dbContext, forceReset: true);
        var clientStartup = await dbContext.Users.FirstAsync(u => u.Email == "client.startup@demo.com");
        var walletService = new Service(dbContext);

        // Act
        var wallet = await walletService.GetWalletAsync(clientStartup.Id);

        // Assert
        wallet.Should().NotBeNull();
        wallet.UserId.Should().Be(clientStartup.Id);
        wallet.AvailableBalance.Should().Be(10000m);
    }
}
