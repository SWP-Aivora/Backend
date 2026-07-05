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
}

