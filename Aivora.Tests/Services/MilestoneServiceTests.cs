using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.MilestoneService;
using Aivora.Services.Treasury;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class MilestoneServiceTests
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
    public async Task FundMilestoneAsync_Succeeds_WhenBalanceIsEnough()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var wallet = new Wallet { UserId = clientId, AvailableBalance = 1000, Currency = "AICOIN" };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 0, Currency = "AICOIN" };
        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.PENDING_PAYMENT, Currency = "AICOIN" };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Amount = 300, Status = MilestoneStatus.CREATED, Title = "Milestone 1", Currency = "AICOIN" };

        dbContext.Wallets.AddRange(wallet, expertWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        // Finance setup
        var treasury = new Aivora.Services.Treasury.Treasury(dbContext, Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(), Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService());
        var service = new Service(dbContext, treasury, Mock.Of<Aivora.Services.NotificationService.IService>());

        // Act
        var result = await service.FundMilestoneAsync(clientId, milestoneId);

        // Assert
        result.Milestone.Status.Should().Be(MilestoneStatus.IN_PROGRESS);
        result.Wallet.AvailableBalance.Should().Be(910); // 1000 - 30% of 300
        result.Wallet.HeldBalance.Should().Be(0);

        var updatedProject = await dbContext.Projects.FindAsync(projectId);
        updatedProject!.Status.Should().Be(ProjectStatus.ACTIVE);

        var payment = await dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.RELEASED);
        payment.Amount.Should().Be(90);
    }

    [Fact]
    public async Task ApproveMilestoneAsync_ReleasesFundsToExpert()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var clientWallet = new Wallet { UserId = clientId, AvailableBalance = 700, HeldBalance = 0, Currency = "AICOIN" };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 90, TotalEarned = 90, Currency = "AICOIN" };
        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE, Currency = "AICOIN" };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Amount = 300, Status = MilestoneStatus.SUBMITTED, Title = "Milestone 1", Currency = "AICOIN" };
        
        // Mock the initial deposit payment so treasury can find it
        var depositPayment = new Payment { MilestoneId = milestoneId, ProjectId = projectId, PayerId = clientId, PayeeId = expertId, Amount = 90, Status = PaymentStatus.RELEASED, Currency = "AICOIN" };

        dbContext.Wallets.AddRange(clientWallet, expertWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.Payments.Add(depositPayment);
        await dbContext.SaveChangesAsync();

        // Finance setup
        var treasury = new Aivora.Services.Treasury.Treasury(dbContext, Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(), Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService());
        var service = new Service(dbContext, treasury, Mock.Of<Aivora.Services.NotificationService.IService>());

        // Act
        var result = await service.ApproveMilestoneAsync(clientId, milestoneId);

        // Assert
        result.Status.Should().Be(MilestoneStatus.RELEASED);

        var updatedClientWallet = await dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == clientId);
        updatedClientWallet!.AvailableBalance.Should().Be(490); // 700 - 210

        var updatedExpertWallet = await dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == expertId);
        updatedExpertWallet!.AvailableBalance.Should().Be(300); // 90 + 210
        updatedExpertWallet!.TotalEarned.Should().Be(300); // 90 + 210

        // In PayRemainingAsync, a new payment is created for the remaining 70%
        var payments = await dbContext.Payments.Where(p => p.MilestoneId == milestoneId).ToListAsync();
        payments.Count.Should().Be(2);
        payments.All(p => p.Status == PaymentStatus.RELEASED).Should().BeTrue();
    }
}
