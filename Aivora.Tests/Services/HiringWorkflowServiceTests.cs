using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.HiringWorkflowService;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class HiringWorkflowServiceTests
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
    public async Task AcceptProposalAsync_CreatesProjectAndMilestones()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();

        var job = new JobPost { Id = jobId, ClientId = clientId, Title = "Test Job", Status = JobStatus.OPEN, OriginalDescription = "Desc", BudgetMin = 1000 };
        var proposal = new Proposal 
        { 
            Id = proposalId, 
            JobId = jobId, 
            ExpertId = expertId, 
            Status = ProposalStatus.SUBMITTED,
            ProposedBudget = 500,
            CoverLetter = "Letter",
            Milestones = new List<ProposalMilestone>
            {
                new ProposalMilestone { Title = "M1", Amount = 200, OrderIndex = 1 },
                new ProposalMilestone { Title = "M2", Amount = 300, OrderIndex = 2 }
            }
        };
        var otherProposal = new Proposal { Id = Guid.NewGuid(), JobId = jobId, ExpertId = Guid.NewGuid(), Status = ProposalStatus.SUBMITTED, CoverLetter = "Other", ProposedBudget = 400 };

        dbContext.JobPosts.Add(job);
        dbContext.Proposals.AddRange(proposal, otherProposal);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);

        // Act
        var result = await service.AcceptProposalAsync(clientId, proposalId);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ProjectStatus.PENDING_PAYMENT.ToString());
        
        var updatedJob = await dbContext.JobPosts.FindAsync(jobId);
        updatedJob!.Status.Should().Be(JobStatus.IN_PROGRESS);

        var updatedProposal = await dbContext.Proposals.FindAsync(proposalId);
        updatedProposal!.Status.Should().Be(ProposalStatus.ACCEPTED);

        var updatedOther = await dbContext.Proposals.FindAsync(otherProposal.Id);
        updatedOther!.Status.Should().Be(ProposalStatus.REJECTED);

        var project = await dbContext.Projects.Include(p => p.Milestones).FirstOrDefaultAsync(p => p.AcceptedProposalId == proposalId);
        project.Should().NotBeNull();
        project!.Milestones.Should().HaveCount(2);
    }
}
