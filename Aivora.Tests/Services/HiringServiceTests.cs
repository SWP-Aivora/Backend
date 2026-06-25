using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.HiringService;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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

        var service = new Aivora.Services.HiringService.HiringService(dbContext);

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

        var service = new Aivora.Services.HiringService.HiringService(dbContext);

        // Act
        await service.ShortlistProposalAsync(clientId, proposal.Id);

        // Assert
        var updated = await dbContext.Proposals.FindAsync(proposal.Id);
        updated!.Status.Should().Be(ProposalStatus.SHORTLISTED);
    }
}
