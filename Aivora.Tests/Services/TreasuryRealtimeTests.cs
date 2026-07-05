using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Treasury;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class TreasuryRealtimeTests
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
    public async Task SyncProjectStatusAsync_WhenAllSettled_CallsRealtimeServiceWithCompleted()
    {
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();

        var job = new JobPost { Title = "Job Title", Status = JobStatus.IN_PROGRESS, ClientId = clientId, OriginalDescription = "D" };
        var project = new Project { Job = job, ClientId = clientId, ExpertId = expertId, Status = ProjectStatus.ACTIVE, Title = "Project Title" };
        var milestone = new Milestone { Project = project, Title = "M1", Amount = 100, Status = MilestoneStatus.RELEASED };

        dbContext.JobPosts.Add(job);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var mockRealtime = new Mock<Aivora.Services.RealtimeService.IService>();
        var treasury = new Aivora.Services.Treasury.Treasury(
            dbContext,
            Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(),
            Mock.Of<Aivora.Services.NotificationService.IService>(),
            mockRealtime.Object
        );

        await treasury.SyncProjectStatusAsync(project.Id);

        mockRealtime.Verify(r => r.SendJobStatusUpdateToUsersAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.Contains(clientId) && ids.Contains(expertId)),
            job.Id,
            JobStatus.COMPLETED,
            "Job Title"
        ), Times.Once);
    }

    [Fact]
    public async Task PayDepositAsync_Should_Transfer_30Percent_Directly()
    {
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();

        var clientWallet = new Wallet { UserId = clientId, AvailableBalance = 1000, HeldBalance = 0, Currency = "VND" };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 0, HeldBalance = 0, Currency = "VND" };
        var project = new Project { ClientId = clientId, ExpertId = expertId, Title = "P1" };
        var milestone = new Milestone { Project = project, Amount = 1000, Status = MilestoneStatus.CREATED, Title = "M1" };

        dbContext.Wallets.AddRange(clientWallet, expertWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var treasury = new Aivora.Services.Treasury.Treasury(
            dbContext,
            Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(),
            Mock.Of<Aivora.Services.NotificationService.IService>(),
            Mock.Of<Aivora.Services.RealtimeService.IService>()
        );

        await treasury.PayDepositAsync(clientId, milestone.Id);

        // 30% of 1000 = 300
        clientWallet.AvailableBalance.Should().Be(700);
        expertWallet.AvailableBalance.Should().Be(300);

        var payment = await dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestone.Id);
        payment.Should().NotBeNull();
        payment.Amount.Should().Be(300);
        payment.Status.Should().Be(PaymentStatus.RELEASED);

        milestone.Status.Should().Be(MilestoneStatus.IN_PROGRESS);
    }

    [Fact]
    public async Task PayRemainingAsync_Should_Transfer_70Percent_Directly()
    {
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();

        var clientWallet = new Wallet { UserId = clientId, AvailableBalance = 1000, HeldBalance = 0, Currency = "VND" };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 300, HeldBalance = 0, Currency = "VND" };
        var project = new Project { ClientId = clientId, ExpertId = expertId, Title = "P1" };
        var milestone = new Milestone { Project = project, Amount = 1000, Status = MilestoneStatus.SUBMITTED, Title = "M1" };

        dbContext.Wallets.AddRange(clientWallet, expertWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var treasury = new Aivora.Services.Treasury.Treasury(
            dbContext,
            Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(),
            Mock.Of<Aivora.Services.NotificationService.IService>(),
            Mock.Of<Aivora.Services.RealtimeService.IService>()
        );

        await treasury.PayRemainingAsync(clientId, milestone.Id);

        // 70% of 1000 = 700
        clientWallet.AvailableBalance.Should().Be(300);
        expertWallet.AvailableBalance.Should().Be(1000);

        var payment = await dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestone.Id && p.Amount == 700);
        payment.Should().NotBeNull();
        payment.Status.Should().Be(PaymentStatus.RELEASED);

        milestone.Status.Should().Be(MilestoneStatus.RELEASED);
    }

    [Fact]
    public async Task RefundMilestoneAsync_Should_Log_Distinct_Transactions_For_Each_Payment()
    {
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();

        var clientWallet = new Wallet { UserId = clientId, AvailableBalance = 1000, HeldBalance = 0, Currency = "AICOIN" };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 500, HeldBalance = 0, Currency = "AICOIN" };
        var project = new Project { ClientId = clientId, ExpertId = expertId, Title = "Refund Project" };
        var milestone = new Milestone { Project = project, Amount = 500, Status = MilestoneStatus.RELEASED, Title = "M1" };

        var payment1 = new Payment { Id = Guid.NewGuid(), MilestoneId = milestone.Id, ProjectId = project.Id, PayerId = clientId, PayeeId = expertId, Amount = 150, Status = PaymentStatus.RELEASED, Currency = "AICOIN" };
        var payment2 = new Payment { Id = Guid.NewGuid(), MilestoneId = milestone.Id, ProjectId = project.Id, PayerId = clientId, PayeeId = expertId, Amount = 350, Status = PaymentStatus.RELEASED, Currency = "AICOIN" };

        dbContext.Wallets.AddRange(clientWallet, expertWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.Payments.AddRange(payment1, payment2);
        await dbContext.SaveChangesAsync();

        var treasury = new Aivora.Services.Treasury.Treasury(
            dbContext,
            Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(),
            Mock.Of<Aivora.Services.NotificationService.IService>(),
            Mock.Of<Aivora.Services.RealtimeService.IService>()
        );

        await treasury.RefundMilestoneAsync(Guid.NewGuid(), milestone.Id, "Disputed result");

        clientWallet.AvailableBalance.Should().Be(1500); // 1000 + 150 + 350
        expertWallet.AvailableBalance.Should().Be(0); // 500 - 500

        var transactions = await dbContext.WalletTransactions.Where(t => t.Type == WalletTransactionType.REFUND).ToListAsync();
        transactions.Count.Should().Be(4); // 2 transactions per payment (one credit client, one debit expert)

        var p1Txns = transactions.Where(t => t.PaymentId == payment1.Id).ToList();
        p1Txns.Count.Should().Be(2);
        p1Txns.Any(t => t.Direction == TransactionDirection.CREDIT && t.UserId == clientId && t.Amount == 150).Should().BeTrue();
        p1Txns.Any(t => t.Direction == TransactionDirection.DEBIT && t.UserId == expertId && t.Amount == 150).Should().BeTrue();

        var p2Txns = transactions.Where(t => t.PaymentId == payment2.Id).ToList();
        p2Txns.Count.Should().Be(2);
        p2Txns.Any(t => t.Direction == TransactionDirection.CREDIT && t.UserId == clientId && t.Amount == 350).Should().BeTrue();
        p2Txns.Any(t => t.Direction == TransactionDirection.DEBIT && t.UserId == expertId && t.Amount == 350).Should().BeTrue();
    }

    [Fact]
    public void Wallet_Debit_Should_Throw_InvalidOperationException_When_DebtLimit_Exceeded()
    {
        // Arrange
        var wallet = new Wallet { AvailableBalance = 0, Debt = 0, Currency = "AICOIN" };

        // Act
        var act = () => wallet.Debit(1500, bypassDebtLimit: false);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Clawback failed. Operation would exceed the maximum debt limit of 1000 AICOIN.");
    }

    [Fact]
    public void Wallet_Debit_Should_Allow_Exceeding_DebtLimit_When_Bypassed()
    {
        // Arrange
        var wallet = new Wallet { AvailableBalance = 0, Debt = 0, Currency = "AICOIN" };

        // Act
        wallet.Debit(1500, bypassDebtLimit: true);

        // Assert
        wallet.Debt.Should().Be(1500);
        wallet.AvailableBalance.Should().Be(0);
    }

    [Fact]
    public void Wallet_Debit_Should_Throw_InvalidOperationException_When_SystemDebtLimit_Exceeded_Even_If_Bypassed()
    {
        // Arrange
        var wallet = new Wallet { AvailableBalance = 0, Debt = 0, Currency = "AICOIN" };

        // Act
        var act = () => wallet.Debit(6000, bypassDebtLimit: true);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Clawback failed. Operation would exceed the maximum system debt limit of 5000 AICOIN.");
    }

    [Fact]
    public async Task PayRemainingAsync_Should_Throw_ValidationException_When_Milestone_Is_Disputed()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();

        var clientWallet = new Wallet { UserId = clientId, AvailableBalance = 1000, HeldBalance = 0, Currency = "AICOIN" };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 0, HeldBalance = 0, Currency = "AICOIN" };
        var project = new Project { ClientId = clientId, ExpertId = expertId, Title = "Disputed Project" };
        var milestone = new Milestone { Project = project, Amount = 1000, Status = MilestoneStatus.DISPUTED, Title = "M1" };

        dbContext.Wallets.AddRange(clientWallet, expertWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var treasury = new Aivora.Services.Treasury.Treasury(
            dbContext,
            Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(),
            Mock.Of<Aivora.Services.NotificationService.IService>(),
            Mock.Of<Aivora.Services.RealtimeService.IService>()
        );

        // Act
        Func<Task> act = async () => await treasury.PayRemainingAsync(clientId, milestone.Id);

        // Assert
        await act.Should().ThrowAsync<Aivora.Services.Exceptions.ValidationException>()
            .WithMessage("Cannot release remaining funds while the milestone is disputed.");
    }
}

