using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
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
        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.PENDING_PAYMENT, Currency = "AICOIN" };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Amount = 300, Status = MilestoneStatus.CREATED, Title = "Milestone 1", Currency = "AICOIN" };

        dbContext.Wallets.Add(wallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        // Finance setup
        var treasury = new Aivora.Services.Treasury.Treasury(dbContext, Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>());
        var service = new Service(dbContext, treasury);

        // Act
        var result = await service.FundMilestoneAsync(clientId, milestoneId);

        // Assert
        result.Milestone.Status.Should().Be(MilestoneStatus.FUNDED);
        result.Wallet.AvailableBalance.Should().Be(700);
        result.Wallet.HeldBalance.Should().Be(300);
        
        var updatedProject = await dbContext.Projects.FindAsync(projectId);
        updatedProject!.Status.Should().Be(ProjectStatus.ACTIVE);

        var payment = await dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.HELD);
    }

    [Fact]
    public async Task FundMilestoneAsync_RejectsNonPositiveMilestoneAmount()
    {
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var wallet = new Wallet { UserId = clientId, AvailableBalance = 1000, Currency = "AICOIN" };
        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.PENDING_PAYMENT, Currency = "AICOIN" };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Amount = 0, Status = MilestoneStatus.CREATED, Title = "Invalid Milestone", Currency = "AICOIN" };

        dbContext.Wallets.Add(wallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var treasury = new Aivora.Services.Treasury.Treasury(dbContext, Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>());
        var service = new Service(dbContext, treasury);

        Func<Task> act = async () => await service.FundMilestoneAsync(clientId, milestoneId);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Milestone amount must be greater than 0.");
        wallet.AvailableBalance.Should().Be(1000);
        wallet.HeldBalance.Should().Be(0);
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

        var clientWallet = new Wallet { UserId = clientId, AvailableBalance = 700, HeldBalance = 300, Currency = "AICOIN" };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 0, TotalEarned = 0, Currency = "AICOIN" };
        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE, Currency = "AICOIN" };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Amount = 300, Status = MilestoneStatus.SUBMITTED, Title = "Milestone 1", Currency = "AICOIN" };
        var payment = new Payment { MilestoneId = milestoneId, ProjectId = projectId, PayerId = clientId, PayeeId = expertId, Amount = 300, Status = PaymentStatus.HELD, Currency = "AICOIN" };

        dbContext.Wallets.AddRange(clientWallet, expertWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        // Finance setup
        var treasury = new Aivora.Services.Treasury.Treasury(dbContext, Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>());
        var service = new Service(dbContext, treasury);

        // Act
        var result = await service.ApproveMilestoneAsync(clientId, milestoneId);

        // Assert
        result.Status.Should().Be(MilestoneStatus.PAID);
        
        var updatedClientWallet = await dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == clientId);
        updatedClientWallet!.HeldBalance.Should().Be(0);

        var updatedExpertWallet = await dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == expertId);
        updatedExpertWallet!.AvailableBalance.Should().Be(300);
        updatedExpertWallet!.TotalEarned.Should().Be(300);

        var updatedPayment = await dbContext.Payments.FindAsync(payment.Id);
        updatedPayment!.Status.Should().Be(PaymentStatus.RELEASED);
    }
}
