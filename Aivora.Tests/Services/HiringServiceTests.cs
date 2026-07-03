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
    public async Task UnshortlistProposalAsync_ThrowsWhenUnauthorized()
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
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.UnshortlistProposalAsync(anotherClientId, proposal.Id));
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
