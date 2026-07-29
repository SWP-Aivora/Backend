using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.HiringService;
using Aivora.Services.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class HiringServiceTests
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
    public async Task AcceptProposalAsync_CreatesProjectAndRejectsSiblings()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var expert2Id = Guid.NewGuid();

        var job = new JobPost { Id = Guid.NewGuid(), ClientId = clientId, Title = "Test Job", Status = JobStatus.OPEN, OriginalDescription = "X" };
        var proposal1 = new Proposal { Id = Guid.NewGuid(), JobId = job.Id, ExpertId = expertId, Status = ProposalStatus.SUBMITTED, ProposedBudget = 1000, Currency = "AICOIN", CoverLetter = "L1" };
        var proposal2 = new Proposal { Id = Guid.NewGuid(), JobId = job.Id, ExpertId = expert2Id, Status = ProposalStatus.SUBMITTED, ProposedBudget = 1200, Currency = "AICOIN", CoverLetter = "L2" };

        dbContext.JobPosts.Add(job);
        dbContext.Proposals.AddRange(proposal1, proposal2);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.HiringService.HiringService(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService());

        // Act
        var result = await service.AcceptProposalAsync(clientId, proposal1.Id);

        // Assert
        result.ProjectId.Should().NotBeEmpty();

        var acceptedP = await dbContext.Proposals.FindAsync(proposal1.Id);
        acceptedP!.Status.Should().Be(ProposalStatus.ACCEPTED);

        var rejectedP = await dbContext.Proposals.FindAsync(proposal2.Id);
        rejectedP!.Status.Should().Be(ProposalStatus.REJECTED);

        var updatedJob = await dbContext.JobPosts.FindAsync(job.Id);
        updatedJob!.Status.Should().Be(JobStatus.IN_PROGRESS);

        var project = await dbContext.Projects.FirstOrDefaultAsync(p => p.AcceptedProposalId == proposal1.Id);
        project.Should().NotBeNull();
        project!.ExpertId.Should().Be(expertId);
    }

    [Fact]
    public async Task AcceptProposalAsync_ExpiresPendingJobInvitesForThatJobInTheSameTransaction()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var invitedExpertId = Guid.NewGuid();
        var respondedInvitedExpertId = Guid.NewGuid();

        var job = new JobPost { Id = Guid.NewGuid(), ClientId = clientId, Title = "Test Job", Status = JobStatus.OPEN, OriginalDescription = "X" };
        var proposal = new Proposal { Id = Guid.NewGuid(), JobId = job.Id, ExpertId = expertId, Status = ProposalStatus.SUBMITTED, ProposedBudget = 1000, Currency = "AICOIN", CoverLetter = "L1" };
        var pendingInvite = new JobInvite { Id = Guid.NewGuid(), JobId = job.Id, ExpertId = invitedExpertId, ClientId = clientId, Status = JobInviteStatus.PENDING };
        var acceptedInvite = new JobInvite { Id = Guid.NewGuid(), JobId = job.Id, ExpertId = respondedInvitedExpertId, ClientId = clientId, Status = JobInviteStatus.ACCEPTED, RespondedAt = DateTimeOffset.UtcNow };

        dbContext.JobPosts.Add(job);
        dbContext.Proposals.Add(proposal);
        dbContext.JobInvites.AddRange(pendingInvite, acceptedInvite);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.HiringService.HiringService(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService());

        // Act
        await service.AcceptProposalAsync(clientId, proposal.Id);

        // Assert
        var updatedPendingInvite = await dbContext.JobInvites.FindAsync(pendingInvite.Id);
        updatedPendingInvite!.Status.Should().Be(JobInviteStatus.EXPIRED);

        // An invite the Expert already responded to must not be touched by the auto-expire.
        var updatedAcceptedInvite = await dbContext.JobInvites.FindAsync(acceptedInvite.Id);
        updatedAcceptedInvite!.Status.Should().Be(JobInviteStatus.ACCEPTED);
    }

    [Fact]
    public async Task ShortlistProposalAsync_UpdatesStatus()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var job = new JobPost { Id = Guid.NewGuid(), ClientId = clientId, Title = "J", Status = JobStatus.OPEN, OriginalDescription = "X" };
        var proposal = new Proposal { Id = Guid.NewGuid(), JobId = job.Id, ExpertId = Guid.NewGuid(), Status = ProposalStatus.SUBMITTED, CoverLetter = "L" };

        dbContext.JobPosts.Add(job);
        dbContext.Proposals.Add(proposal);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.HiringService.HiringService(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService());

        // Act
        await service.ShortlistProposalAsync(clientId, proposal.Id);

        // Assert
        var updated = await dbContext.Proposals.FindAsync(proposal.Id);
        updated!.Status.Should().Be(ProposalStatus.SHORTLISTED);
    }

    [Fact]
    public async Task UnshortlistProposalAsync_UpdatesStatus()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var job = new JobPost { Id = Guid.NewGuid(), ClientId = clientId, Title = "J", Status = JobStatus.OPEN, OriginalDescription = "X" };
        var proposal = new Proposal { Id = Guid.NewGuid(), JobId = job.Id, ExpertId = Guid.NewGuid(), Status = ProposalStatus.SHORTLISTED, CoverLetter = "L" };

        dbContext.JobPosts.Add(job);
        dbContext.Proposals.Add(proposal);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.HiringService.HiringService(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService());

        // Act
        await service.UnshortlistProposalAsync(clientId, proposal.Id);

        // Assert
        var updated = await dbContext.Proposals.FindAsync(proposal.Id);
        updated!.Status.Should().Be(ProposalStatus.SUBMITTED);
    }

    [Fact]
    public async Task UnshortlistProposalAsync_ThrowsWhenNotShortlisted()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var job = new JobPost { Id = Guid.NewGuid(), ClientId = clientId, Title = "J", Status = JobStatus.OPEN, OriginalDescription = "X" };
        var proposal = new Proposal { Id = Guid.NewGuid(), JobId = job.Id, ExpertId = Guid.NewGuid(), Status = ProposalStatus.SUBMITTED, CoverLetter = "L" };

        dbContext.JobPosts.Add(job);
        dbContext.Proposals.Add(proposal);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.HiringService.HiringService(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService());

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UnshortlistProposalAsync(clientId, proposal.Id));
    }

    [Fact]
    public async Task UnshortlistProposalAsync_ThrowsWhenForbidden()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var anotherClientId = Guid.NewGuid();
        var job = new JobPost { Id = Guid.NewGuid(), ClientId = clientId, Title = "J", Status = JobStatus.OPEN, OriginalDescription = "X" };
        var proposal = new Proposal { Id = Guid.NewGuid(), JobId = job.Id, ExpertId = Guid.NewGuid(), Status = ProposalStatus.SHORTLISTED, CoverLetter = "L" };

        dbContext.JobPosts.Add(job);
        dbContext.Proposals.Add(proposal);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.HiringService.HiringService(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService());

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.UnshortlistProposalAsync(anotherClientId, proposal.Id));
    }

    [Fact]
    public async Task AcceptProposalAsync_SetsMilestoneDueDatesFromDueDays()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();

        var job = new JobPost { Id = Guid.NewGuid(), ClientId = clientId, Title = "Test Job", Status = JobStatus.OPEN, OriginalDescription = "X" };
        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            ExpertId = expertId,
            Status = ProposalStatus.SUBMITTED,
            ProposedBudget = 1000,
            Currency = "AICOIN",
            CoverLetter = "L1",
            Milestones = new List<ProposalMilestone>
            {
                new() { Title = "M1", Amount = 100, OrderIndex = 0, DueDays = 7 },
                new() { Title = "M2", Amount = 200, OrderIndex = 1, DueDays = 14 },
                new() { Title = "M3", Amount = 300, OrderIndex = 2, DueDays = 30 },
            }
        };

        dbContext.JobPosts.Add(job);
        dbContext.Proposals.Add(proposal);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.HiringService.HiringService(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var result = await service.AcceptProposalAsync(clientId, proposal.Id);

        // Assert
        var project = await dbContext.Projects
            .Include(p => p.Milestones)
            .FirstAsync(p => p.Id == result.ProjectId);

        var m1 = project.Milestones.Single(m => m.Title == "M1");
        var m2 = project.Milestones.Single(m => m.Title == "M2");
        var m3 = project.Milestones.Single(m => m.Title == "M3");

        m1.DueDate.Should().Be(today.AddDays(7));
        m2.DueDate.Should().Be(today.AddDays(14));
        m3.DueDate.Should().Be(today.AddDays(30));

        // Other fields unaffected
        m1.Amount.Should().Be(100);
        m1.OrderIndex.Should().Be(0);
        m2.Amount.Should().Be(200);
        m2.OrderIndex.Should().Be(1);
    }

    [Fact]
    public async Task AcceptProposalAsync_CallsRealtimeService()
    {
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var job = new JobPost { Id = Guid.NewGuid(), ClientId = clientId, Title = "T", Status = JobStatus.OPEN, OriginalDescription = "X" };
        var proposal = new Proposal { Id = Guid.NewGuid(), JobId = job.Id, ExpertId = expertId, Status = ProposalStatus.SUBMITTED, CoverLetter = "L", ProposedBudget = 100 };
        dbContext.JobPosts.Add(job);
        dbContext.Proposals.Add(proposal);
        await dbContext.SaveChangesAsync();

        var mockRealtime = new Mock<Aivora.Services.RealtimeService.IService>();
        var service = new Aivora.Services.HiringService.HiringService(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), mockRealtime.Object);

        await service.AcceptProposalAsync(clientId, proposal.Id);

        mockRealtime.Verify(r => r.SendJobStatusUpdateToUsersAsync(It.Is<IEnumerable<Guid>>(ids => ids.Contains(clientId) && ids.Contains(expertId)), job.Id, Aivora.Repositories.Enums.JobStatus.IN_PROGRESS, "T"), Times.Once);
    }
}
