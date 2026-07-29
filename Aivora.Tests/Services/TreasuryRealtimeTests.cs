using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aivora.api.Hubs;
using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Repositories.Constants;
using Aivora.Services.Options;
using Aivora.Services.Treasury;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        var commissionOptions = Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.CommissionOptions { Rate = 0.10m });
        var treasury = new Treasury(dbContext, new Aivora.Services.Treasury.CommissionCalculator(commissionOptions), Mock.Of<ILogger<Treasury>>(), Mock.Of<Aivora.Services.NotificationService.IService>(), mockRealtime.Object, Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.EscrowOptions()));

        await treasury.SyncProjectStatusAsync(project.Id);

        mockRealtime.Verify(r => r.SendJobStatusUpdateToUsersAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.Contains(clientId) && ids.Contains(expertId)),
            job.Id,
            JobStatus.COMPLETED,
            "Job Title"
        ), Times.Once);
    }

    [Fact]
    public async Task SyncProjectStatusAsync_WhenAllSettled_AndProjectHasNoJobId_CompletesWithoutRealtimeCall()
    {
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();

        var project = new Project { ClientId = clientId, ExpertId = expertId, Status = ProjectStatus.ACTIVE, Title = "Service Project", ServiceRequestId = Guid.NewGuid() };
        var milestone = new Milestone { Project = project, Title = "M1", Amount = 100, Status = MilestoneStatus.RELEASED };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var mockRealtime = new Mock<Aivora.Services.RealtimeService.IService>();
        var commissionOptions = Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.CommissionOptions { Rate = 0.10m });
        var treasury = new Treasury(dbContext, new Aivora.Services.Treasury.CommissionCalculator(commissionOptions), Mock.Of<ILogger<Treasury>>(), Mock.Of<Aivora.Services.NotificationService.IService>(), mockRealtime.Object, Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.EscrowOptions()));

        await treasury.SyncProjectStatusAsync(project.Id);

        var updated = await dbContext.Projects.FindAsync(project.Id);
        updated!.Status.Should().Be(ProjectStatus.COMPLETED);

        mockRealtime.Verify(r => r.SendJobStatusUpdateToUsersAsync(
            It.IsAny<IEnumerable<Guid>>(),
            It.IsAny<Guid>(),
            It.IsAny<JobStatus>(),
            It.IsAny<string?>()
        ), Times.Never);
    }

    [Fact]
    public async Task SyncProjectStatusAsync_WhenProjectCancelled_DoesNotOverwriteStatus()
    {
        // Arrange: project is CANCELLED (terminal) but still has a DISPUTED milestone lying around
        // (e.g. from before the cancel-disputed flow closed it out). Sync must not resurrect it.
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();

        var project = new Project { ClientId = clientId, ExpertId = expertId, Status = ProjectStatus.CANCELLED, Title = "Cancelled Project" };
        var milestone = new Milestone { Project = project, Title = "M1", Amount = 100, Status = MilestoneStatus.DISPUTED };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var mockRealtime = new Mock<Aivora.Services.RealtimeService.IService>();
        var commissionOptions = Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.CommissionOptions { Rate = 0.10m });
        var treasury = new Treasury(dbContext, new Aivora.Services.Treasury.CommissionCalculator(commissionOptions), Mock.Of<ILogger<Treasury>>(), Mock.Of<Aivora.Services.NotificationService.IService>(), mockRealtime.Object, Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.EscrowOptions()));

        await treasury.SyncProjectStatusAsync(project.Id);

        var updated = await dbContext.Projects.FindAsync(project.Id);
        updated!.Status.Should().Be(ProjectStatus.CANCELLED);
    }

    [Fact]
    public async Task PayDepositAsync_Should_Transfer_30Percent_Directly()
    {
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();

        var clientWallet = new Wallet { UserId = clientId, AvailableBalance = 1000, HeldBalance = 0, Currency = CurrencyConstants.VND };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 0, HeldBalance = 0, Currency = CurrencyConstants.VND };
        var project = new Project { ClientId = clientId, ExpertId = expertId, Title = "P1" };
        var milestone = new Milestone { Project = project, Amount = 1000, Status = MilestoneStatus.CREATED, Title = "M1" };

        dbContext.Wallets.AddRange(clientWallet, expertWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var treasury = new Aivora.Services.Treasury.Treasury(
            dbContext,
            new Aivora.Services.Treasury.CommissionCalculator(Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.CommissionOptions { Rate = 0.10m })),
            Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(),
            Mock.Of<Aivora.Services.NotificationService.IService>(),
            Mock.Of<Aivora.Services.RealtimeService.IService>(),
            Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.EscrowOptions())
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

        var clientWallet = new Wallet { UserId = clientId, AvailableBalance = 1000, HeldBalance = 0, Currency = CurrencyConstants.VND };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 300, HeldBalance = 0, Currency = CurrencyConstants.VND };
        var project = new Project { ClientId = clientId, ExpertId = expertId, Title = "P1" };
        var milestone = new Milestone { Project = project, Amount = 1000, Status = MilestoneStatus.SUBMITTED, Title = "M1" };

        var systemPlatformWallet = new Wallet { UserId = Aivora.Repositories.Constants.SystemConstants.SystemUserId, AvailableBalance = 0, Currency = CurrencyConstants.VND };
        dbContext.Wallets.AddRange(clientWallet, expertWallet, systemPlatformWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var treasury = new Aivora.Services.Treasury.Treasury(
            dbContext,
            new Aivora.Services.Treasury.CommissionCalculator(Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.CommissionOptions { Rate = 0.10m })),
            Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(),
            Mock.Of<Aivora.Services.NotificationService.IService>(),
            Mock.Of<Aivora.Services.RealtimeService.IService>(),
            Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.EscrowOptions())
        );

        await treasury.PayRemainingAsync(clientId, milestone.Id);

        // 70% of 1000 = 700. Fee = 100. Expert gets 600.
        clientWallet.AvailableBalance.Should().Be(300);
        expertWallet.AvailableBalance.Should().Be(900); // 300 + 600

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

        var commissionOptions = Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.CommissionOptions { Rate = 0.10m, MaxDebtLimit = 1000m });
        var treasury = new Aivora.Services.Treasury.Treasury(
            dbContext,
            new CommissionCalculator(commissionOptions),
            Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(),
            Mock.Of<Aivora.Services.NotificationService.IService>(),
            Mock.Of<Aivora.Services.RealtimeService.IService>(),
            Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.EscrowOptions())
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
        var act = () => wallet.Debit(1500);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*would exceed the maximum debt limit of 1000 AICOIN*");
    }

    [Fact]
    public async Task RefundMilestoneAsync_Should_Throw_ValidationException_When_SafeDebtLimit_Exceeded()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();

        var clientWallet = new Wallet { UserId = clientId, AvailableBalance = 1000, HeldBalance = 0, Currency = "AICOIN" };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 0, Debt = 0, HeldBalance = 0, Currency = "AICOIN" };
        var project = new Project { ClientId = clientId, ExpertId = expertId, Title = "Safe limit Project" };
        var milestone = new Milestone { Project = project, Amount = 1500, Status = MilestoneStatus.RELEASED, Title = "M1" };

        var payment = new Payment { Id = Guid.NewGuid(), MilestoneId = milestone.Id, ProjectId = project.Id, PayerId = clientId, PayeeId = expertId, Amount = 1500, Status = PaymentStatus.RELEASED, Currency = "AICOIN" };

        dbContext.Wallets.AddRange(clientWallet, expertWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var commissionOptions = Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.CommissionOptions { Rate = 0.10m, MaxDebtLimit = 1000m });
        var treasury = new Aivora.Services.Treasury.Treasury(
            dbContext,
            new CommissionCalculator(commissionOptions),
            Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(),
            Mock.Of<Aivora.Services.NotificationService.IService>(),
            Mock.Of<Aivora.Services.RealtimeService.IService>(),
            Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.EscrowOptions())
        );

        // Act
        Func<Task> act = async () => await treasury.RefundMilestoneAsync(Guid.NewGuid(), milestone.Id, "Disputed result");

        // Assert
        await act.Should().ThrowAsync<Aivora.Services.Exceptions.ValidationException>()
            .WithMessage("*would exceed the safe debt limit of 1000*");
    }

    [Fact]
    public async Task SplitMilestoneFundsAsync_Should_Throw_ValidationException_When_SafeDebtLimit_Exceeded()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();

        var clientWallet = new Wallet { UserId = clientId, AvailableBalance = 1000, HeldBalance = 0, Currency = "AICOIN" };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 0, Debt = 0, HeldBalance = 0, Currency = "AICOIN" };
        var project = new Project { ClientId = clientId, ExpertId = expertId, Title = "Safe limit Project 2" };
        var milestone = new Milestone { Project = project, Amount = 1500, Status = MilestoneStatus.DISPUTED, Title = "M1" };

        var payment = new Payment { Id = Guid.NewGuid(), MilestoneId = milestone.Id, ProjectId = project.Id, PayerId = clientId, PayeeId = expertId, Amount = 1500, Status = PaymentStatus.RELEASED, Currency = "AICOIN" };

        dbContext.Wallets.AddRange(clientWallet, expertWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var commissionOptions = Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.CommissionOptions { Rate = 0.10m, MaxDebtLimit = 1000m });
        var treasury = new Aivora.Services.Treasury.Treasury(
            dbContext,
            new CommissionCalculator(commissionOptions),
            Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(),
            Mock.Of<Aivora.Services.NotificationService.IService>(),
            Mock.Of<Aivora.Services.RealtimeService.IService>(),
            Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.EscrowOptions())
        );

        // Act
        Func<Task> act = async () => await treasury.SplitMilestoneFundsAsync(milestone.Id, 200, 1300, "Split resolution");

        // Assert
        await act.Should().ThrowAsync<Aivora.Services.Exceptions.ValidationException>()
            .WithMessage("*would exceed the safe debt limit of 1000*");
    }

    [Fact]
    public async Task PayRemainingAsync_Should_Throw_ValidationException_When_Active_Dispute_Record_Exists()
    {
        // Arrange: milestone is SUBMITTED but an OPEN dispute record exists
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();

        var clientWallet = new Wallet { UserId = clientId, AvailableBalance = 1000, HeldBalance = 0, Currency = "AICOIN" };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 0, HeldBalance = 0, Currency = "AICOIN" };
        var project = new Project { ClientId = clientId, ExpertId = expertId, Title = "Active Dispute Project" };
        var milestone = new Milestone { Project = project, Amount = 1000, Status = MilestoneStatus.SUBMITTED, Title = "M1" };
        var payment = new Payment { MilestoneId = milestone.Id, ProjectId = project.Id, PayerId = clientId, PayeeId = expertId, Amount = 300, Status = PaymentStatus.RELEASED, Currency = "AICOIN" };
        var dispute = new Dispute { ProjectId = project.Id, MilestoneId = milestone.Id, PaymentId = payment.Id, OpenedBy = clientId, AgainstUserId = expertId, Reason = "Quality issue", Status = DisputeStatus.OPEN };

        dbContext.Wallets.AddRange(clientWallet, expertWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.Payments.Add(payment);
        dbContext.Disputes.Add(dispute);
        await dbContext.SaveChangesAsync();

        var commissionOptions = Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.CommissionOptions { Rate = 0.10m, MaxDebtLimit = 1000m });
        var treasury = new Aivora.Services.Treasury.Treasury(
            dbContext,
            new CommissionCalculator(commissionOptions),
            Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(),
            Mock.Of<Aivora.Services.NotificationService.IService>(),
            Mock.Of<Aivora.Services.RealtimeService.IService>(),
            Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.EscrowOptions())
        );

        // Act
        Func<Task> act = async () => await treasury.PayRemainingAsync(clientId, milestone.Id);

        // Assert
        await act.Should().ThrowAsync<Aivora.Services.Exceptions.ValidationException>()
            .WithMessage("*active dispute*");
    }

    [Fact]
    public async Task PayDepositAsync_WhenMilestoneBroadcastThrows_StillCommitsAndReturnsResult()
    {
        // Arrange: a real RealtimeService backed by a hub context that throws as soon as it's used.
        // PayDepositAsync's fire-and-forget realtime call must not let that exception escape into
        // the transaction's try/catch (which would otherwise call RollbackAsync on an already-committed transaction).
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();

        var clientWallet = new Wallet { UserId = clientId, AvailableBalance = 1000, HeldBalance = 0, Currency = CurrencyConstants.VND };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 0, HeldBalance = 0, Currency = CurrencyConstants.VND };
        var project = new Project { ClientId = clientId, ExpertId = expertId, Title = "P1" };
        var milestone = new Milestone { Project = project, Amount = 1000, Status = MilestoneStatus.CREATED, Title = "M1" };

        dbContext.Wallets.AddRange(clientWallet, expertWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var mockHubContext = new Mock<IHubContext<ChatHub>>();
        mockHubContext.Setup(h => h.Clients).Throws(new InvalidOperationException("hub is down"));
        var realtimeService = new Aivora.Services.RealtimeService.Service(mockHubContext.Object);

        var treasury = new Aivora.Services.Treasury.Treasury(
            dbContext,
            new Aivora.Services.Treasury.CommissionCalculator(Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.CommissionOptions { Rate = 0.10m })),
            Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(),
            Mock.Of<Aivora.Services.NotificationService.IService>(),
            realtimeService,
            Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.EscrowOptions())
        );

        // Act
        var result = await treasury.PayDepositAsync(clientId, milestone.Id);

        // Assert: the fund transaction completed normally despite the broadcast blowing up.
        result.Should().NotBeNull();
        milestone.Status.Should().Be(MilestoneStatus.IN_PROGRESS);
        clientWallet.AvailableBalance.Should().Be(700);
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

        var commissionOptions = Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.CommissionOptions { Rate = 0.10m, MaxDebtLimit = 1000m });
        var treasury = new Aivora.Services.Treasury.Treasury(
            dbContext,
            new CommissionCalculator(commissionOptions),
            Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(),
            Mock.Of<Aivora.Services.NotificationService.IService>(),
            Mock.Of<Aivora.Services.RealtimeService.IService>(),
            Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.EscrowOptions())
        );

        // Act
        Func<Task> act = async () => await treasury.PayRemainingAsync(clientId, milestone.Id);

        // Assert
        await act.Should().ThrowAsync<Aivora.Services.Exceptions.ValidationException>()
            .WithMessage("Cannot release remaining funds while the milestone is disputed.");
    }

    [Fact]
    public async Task PayDepositAsync_Should_Throw_ValidationException_When_Project_Is_Disputed()
    {
        // Arrange: milestone A is DISPUTED (project-level DISPUTED), milestone B is still CREATED.
        // Funding milestone B must be rejected because the project has an active dispute.
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();

        var clientWallet = new Wallet { UserId = clientId, AvailableBalance = 1000, HeldBalance = 0, Currency = "AICOIN" };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 0, HeldBalance = 0, Currency = "AICOIN" };
        var project = new Project { ClientId = clientId, ExpertId = expertId, Title = "Disputed Project", Status = ProjectStatus.DISPUTED };
        var disputedMilestone = new Milestone { Project = project, Amount = 500, Status = MilestoneStatus.DISPUTED, Title = "M1" };
        var milestoneToFund = new Milestone { Project = project, Amount = 500, Status = MilestoneStatus.CREATED, Title = "M2" };

        dbContext.Wallets.AddRange(clientWallet, expertWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.AddRange(disputedMilestone, milestoneToFund);
        await dbContext.SaveChangesAsync();

        var treasury = new Aivora.Services.Treasury.Treasury(
            dbContext,
            new CommissionCalculator(Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.CommissionOptions { Rate = 0.10m })),
            Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(),
            Mock.Of<Aivora.Services.NotificationService.IService>(),
            Mock.Of<Aivora.Services.RealtimeService.IService>(),
            Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.EscrowOptions())
        );

        // Act
        Func<Task> act = async () => await treasury.PayDepositAsync(clientId, milestoneToFund.Id);

        // Assert - rejected, no wallet mutation
        await act.Should().ThrowAsync<Aivora.Services.Exceptions.ValidationException>()
            .WithMessage("Cannot fund a milestone while there is an active dispute.");
        clientWallet.AvailableBalance.Should().Be(1000);
        expertWallet.AvailableBalance.Should().Be(0);
    }

    [Fact]
    public async Task PayDepositAsync_Should_Succeed_After_Dispute_Is_Closed()
    {
        // Arrange: project was DISPUTED but the dispute has since been closed (project back to ACTIVE).
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();

        var clientWallet = new Wallet { UserId = clientId, AvailableBalance = 1000, HeldBalance = 0, Currency = "AICOIN" };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 0, HeldBalance = 0, Currency = "AICOIN" };
        var project = new Project { ClientId = clientId, ExpertId = expertId, Title = "Formerly Disputed Project", Status = ProjectStatus.ACTIVE };
        var milestoneToFund = new Milestone { Project = project, Amount = 500, Status = MilestoneStatus.CREATED, Title = "M2" };

        dbContext.Wallets.AddRange(clientWallet, expertWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestoneToFund);
        await dbContext.SaveChangesAsync();

        var treasury = new Aivora.Services.Treasury.Treasury(
            dbContext,
            new CommissionCalculator(Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.CommissionOptions { Rate = 0.10m })),
            Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(),
            Mock.Of<Aivora.Services.NotificationService.IService>(),
            Mock.Of<Aivora.Services.RealtimeService.IService>(),
            Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.EscrowOptions())
        );

        // Act
        var result = await treasury.PayDepositAsync(clientId, milestoneToFund.Id);

        // Assert
        result.Should().NotBeNull();
        milestoneToFund.Status.Should().Be(MilestoneStatus.IN_PROGRESS);
    }
}


