using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.ProposalService;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class ProposalServiceUpdateTests
{
    private readonly Mock<Aivora.Services.NotificationService.IService> _notificationServiceMock = new();

    private static AivoraDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AivoraDbContext(options);
    }

    private static (User, JobPost) SetupExpertAndJob(AivoraDbContext dbContext)
    {
        var expert = new User { FullName = "Test Expert", Email = "expert@test.com", PasswordHash = "hash" };
        var client = new User { FullName = "Test Client", Email = "client@test.com", PasswordHash = "hash" };
        dbContext.Users.Add(expert);
        dbContext.Users.Add(client);
        dbContext.SaveChanges();

        var job = new JobPost
        {
            ClientId = client.Id,
            Client = client,
            Title = "Test Job",
            OriginalDescription = "Desc",
            CategoryId = Guid.NewGuid(),
            Status = JobStatus.OPEN
        };
        dbContext.JobPosts.Add(job);
        dbContext.SaveChanges();

        return (expert, job);
    }

    private Service CreateService(AivoraDbContext dbContext)
    {
        return new Service(dbContext, _notificationServiceMock.Object);
    }

    [Fact]
    public async Task UpdateProposalAsync_WithValidData_UpdatesProposal()
    {
        var dbContext = GetDbContext();
        var (expert, job) = SetupExpertAndJob(dbContext);
        var service = CreateService(dbContext);

        var proposal = new Proposal
        {
            ExpertId = expert.Id,
            Expert = expert,
            JobId = job.Id,
            Job = job,
            CoverLetter = "Old cover letter",
            ProposedBudget = 500,
            Status = ProposalStatus.SUBMITTED,
            Milestones = new List<ProposalMilestone>
            {
                new() { Title = "Old M1", Amount = 250, DueDays = 7, OrderIndex = 0 }
            }
        };
        dbContext.Proposals.Add(proposal);
        await dbContext.SaveChangesAsync();

        var request = new Request.UpdateProposalRequest
        {
            CoverLetter = "Updated cover letter",
            ProposedBudget = 1000,
            ProposedTimelineDays = 30,
            Milestones = new List<Request.UpdateProposalMilestoneRequest>
            {
                new() { Title = "New M1", Amount = 500, DueDays = 14, OrderIndex = 0 },
                new() { Title = "New M2", Amount = 500, DueDays = 14, OrderIndex = 1 }
            }
        };

        var result = await service.UpdateProposalAsync(expert.Id, proposal.Id, request);

        result.CoverLetter.Should().Be("Updated cover letter");
        result.ProposedBudget.Should().Be(1000);
        result.ProposedTimelineDays.Should().Be(30);
        result.Milestones.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateProposalAsync_NotOwner_ThrowsForbiddenException()
    {
        var dbContext = GetDbContext();
        var (expert, job) = SetupExpertAndJob(dbContext);
        var service = CreateService(dbContext);

        var proposal = new Proposal
        {
            ExpertId = expert.Id,
            JobId = job.Id,
            CoverLetter = "Cover",
            ProposedBudget = 500,
            Status = ProposalStatus.SUBMITTED
        };
        dbContext.Proposals.Add(proposal);
        await dbContext.SaveChangesAsync();

        var request = new Request.UpdateProposalRequest { CoverLetter = "Hacked", ProposedBudget = 1 };

        Func<Task> act = async () => await service.UpdateProposalAsync(Guid.NewGuid(), proposal.Id, request);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateProposalAsync_WrongStatus_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var (expert, job) = SetupExpertAndJob(dbContext);
        var service = CreateService(dbContext);

        var proposal = new Proposal
        {
            ExpertId = expert.Id,
            JobId = job.Id,
            CoverLetter = "Cover",
            ProposedBudget = 500,
            Status = ProposalStatus.ACCEPTED
        };
        dbContext.Proposals.Add(proposal);
        await dbContext.SaveChangesAsync();

        var request = new Request.UpdateProposalRequest { CoverLetter = "Updated", ProposedBudget = 1000 };

        Func<Task> act = async () => await service.UpdateProposalAsync(expert.Id, proposal.Id, request);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Proposal can only be edited when it is submitted or shortlisted.");
    }

    [Fact]
    public async Task UpdateProposalAsync_InvalidMilestoneAmount_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var (expert, job) = SetupExpertAndJob(dbContext);
        var service = CreateService(dbContext);

        var proposal = new Proposal
        {
            ExpertId = expert.Id,
            JobId = job.Id,
            CoverLetter = "Cover",
            ProposedBudget = 500,
            Status = ProposalStatus.SUBMITTED
        };
        dbContext.Proposals.Add(proposal);
        await dbContext.SaveChangesAsync();

        var request = new Request.UpdateProposalRequest
        {
            CoverLetter = "Updated",
            ProposedBudget = 1000,
            Milestones = new List<Request.UpdateProposalMilestoneRequest>
            {
                new() { Title = "Bad", Amount = -5, DueDays = 5, OrderIndex = 0 }
            }
        };

        Func<Task> act = async () => await service.UpdateProposalAsync(expert.Id, proposal.Id, request);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Milestone amounts must be greater than 0.");
    }

    [Fact]
    public async Task UpdateProposalAsync_ZeroBudget_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var (expert, job) = SetupExpertAndJob(dbContext);
        var service = CreateService(dbContext);

        var proposal = new Proposal
        {
            ExpertId = expert.Id,
            JobId = job.Id,
            CoverLetter = "Cover",
            ProposedBudget = 500,
            Status = ProposalStatus.SUBMITTED
        };
        dbContext.Proposals.Add(proposal);
        await dbContext.SaveChangesAsync();

        var request = new Request.UpdateProposalRequest { CoverLetter = "Updated", ProposedBudget = 0 };

        Func<Task> act = async () => await service.UpdateProposalAsync(expert.Id, proposal.Id, request);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("ProposedBudget must be greater than 0.");
    }

    [Fact]
    public async Task UpdateProposalAsync_WithdrawnProposal_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var (expert, job) = SetupExpertAndJob(dbContext);
        var service = CreateService(dbContext);

        var proposal = new Proposal
        {
            ExpertId = expert.Id,
            JobId = job.Id,
            CoverLetter = "Cover",
            ProposedBudget = 500,
            Status = ProposalStatus.WITHDRAWN
        };
        dbContext.Proposals.Add(proposal);
        await dbContext.SaveChangesAsync();

        var request = new Request.UpdateProposalRequest { CoverLetter = "Updated", ProposedBudget = 1000 };

        Func<Task> act = async () => await service.UpdateProposalAsync(expert.Id, proposal.Id, request);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Proposal can only be edited when it is submitted or shortlisted.");
    }

    [Fact]
    public async Task UpdateProposalAsync_ReplacingMilestones_DoesNotLeakSoftDeletedOldMilestone()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        Guid expertId, proposalId;
        using (var seedContext = new AivoraDbContext(options))
        {
            var (expert, job) = SetupExpertAndJob(seedContext);
            expertId = expert.Id;

            var proposal = new Proposal
            {
                ExpertId = expert.Id,
                JobId = job.Id,
                CoverLetter = "Old cover letter",
                ProposedBudget = 500,
                Status = ProposalStatus.SUBMITTED,
                Milestones = new List<ProposalMilestone>
                {
                    new() { Title = "Old M1", Amount = 250, DueDays = 7, OrderIndex = 0 }
                }
            };
            seedContext.Proposals.Add(proposal);
            await seedContext.SaveChangesAsync();
            proposalId = proposal.Id;
        }

        // Fresh DbContext, mirroring a real request scope where the proposal/milestones
        // are not already tracked/loaded before the service call runs.
        using var dbContext = new AivoraDbContext(options);
        var service = CreateService(dbContext);

        var request = new Request.UpdateProposalRequest
        {
            CoverLetter = "New cover letter",
            ProposedBudget = 900,
            Milestones = new List<Request.UpdateProposalMilestoneRequest>
            {
                new() { Title = "New M1", Amount = 900, DueDays = 12, OrderIndex = 0 }
            }
        };

        var result = await service.UpdateProposalAsync(expertId, proposalId, request);

        result.Milestones.Should().HaveCount(1);
        result.Milestones.Single().Title.Should().Be("New M1");
    }

    [Fact]
    public async Task UpdateProposalAsync_NullRequest_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var (expert, job) = SetupExpertAndJob(dbContext);
        var service = CreateService(dbContext);

        var proposal = new Proposal
        {
            ExpertId = expert.Id,
            JobId = job.Id,
            CoverLetter = "Cover",
            ProposedBudget = 500,
            Status = ProposalStatus.SUBMITTED
        };
        dbContext.Proposals.Add(proposal);
        await dbContext.SaveChangesAsync();

        Func<Task> act = async () => await service.UpdateProposalAsync(expert.Id, proposal.Id, null!);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Request body is required.");
    }
}
