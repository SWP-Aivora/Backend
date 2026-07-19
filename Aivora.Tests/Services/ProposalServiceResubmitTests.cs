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

public class ProposalServiceResubmitTests
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

    private static (User, JobPost) SetupExpertAndJob(AivoraDbContext dbContext, JobStatus jobStatus = JobStatus.OPEN)
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
            Status = jobStatus
        };
        dbContext.JobPosts.Add(job);
        dbContext.SaveChanges();

        return (expert, job);
    }

    private Service CreateService(AivoraDbContext dbContext)
    {
        return new Service(dbContext, _notificationServiceMock.Object);
    }

    private static Request.UpdateProposalRequest ValidRequest()
    {
        return new Request.UpdateProposalRequest
        {
            CoverLetter = "Resubmitted cover letter",
            ProposedBudget = 800,
            ProposedTimelineDays = 20,
            Milestones = new List<Request.UpdateProposalMilestoneRequest>
            {
                new() { Title = "M1", Amount = 800, DueDays = 20, OrderIndex = 0 }
            }
        };
    }

    [Fact]
    public async Task ResubmitProposalAsync_ProposalNotFound_ThrowsNotFoundException()
    {
        var dbContext = GetDbContext();
        var service = CreateService(dbContext);

        Func<Task> act = async () => await service.ResubmitProposalAsync(Guid.NewGuid(), Guid.NewGuid(), ValidRequest());
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ResubmitProposalAsync_NotOwner_ThrowsForbiddenException()
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

        Func<Task> act = async () => await service.ResubmitProposalAsync(Guid.NewGuid(), proposal.Id, ValidRequest());
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ResubmitProposalAsync_NotWithdrawn_ThrowsValidationException()
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

        Func<Task> act = async () => await service.ResubmitProposalAsync(expert.Id, proposal.Id, ValidRequest());
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Only withdrawn or rejected proposals can be resubmitted.");
    }

    [Fact]
    public async Task ResubmitProposalAsync_Accepted_ThrowsValidationException()
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

        Func<Task> act = async () => await service.ResubmitProposalAsync(expert.Id, proposal.Id, ValidRequest());
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Only withdrawn or rejected proposals can be resubmitted.");
    }

    [Fact]
    public async Task ResubmitProposalAsync_JobNotOpen_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var (expert, job) = SetupExpertAndJob(dbContext, JobStatus.IN_PROGRESS);
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

        Func<Task> act = async () => await service.ResubmitProposalAsync(expert.Id, proposal.Id, ValidRequest());
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Job is no longer open for proposals.");
    }

    [Fact]
    public async Task ResubmitProposalAsync_InvalidBudget_ThrowsValidationException()
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

        var request = ValidRequest();
        request.ProposedBudget = 0;

        Func<Task> act = async () => await service.ResubmitProposalAsync(expert.Id, proposal.Id, request);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("ProposedBudget must be greater than 0.");
    }

    [Fact]
    public async Task ResubmitProposalAsync_WithdrawnAndJobOpen_ResubmitsAndNotifiesClient()
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
            Status = ProposalStatus.WITHDRAWN,
            WithdrawnAt = DateTimeOffset.UtcNow,
            Milestones = new List<ProposalMilestone>
            {
                new() { Title = "Old M1", Amount = 250, DueDays = 7, OrderIndex = 0 }
            }
        };
        dbContext.Proposals.Add(proposal);
        await dbContext.SaveChangesAsync();

        var request = ValidRequest();

        var result = await service.ResubmitProposalAsync(expert.Id, proposal.Id, request);

        result.Id.Should().Be(proposal.Id);
        result.Status.Should().Be(ProposalStatus.SUBMITTED);
        result.CoverLetter.Should().Be(request.CoverLetter);
        result.ProposedBudget.Should().Be(request.ProposedBudget);
        result.Milestones.Should().HaveCount(1);

        var stored = await dbContext.Proposals.FindAsync(proposal.Id);
        stored!.WithdrawnAt.Should().BeNull();

        _notificationServiceMock.Verify(n => n.SendNotificationAsync(
            job.ClientId,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ResubmitProposalAsync_RejectedAndJobOpen_ResubmitsAndNotifiesClient()
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
            Status = ProposalStatus.REJECTED,
            Milestones = new List<ProposalMilestone>
            {
                new() { Title = "Old M1", Amount = 250, DueDays = 7, OrderIndex = 0 }
            }
        };
        dbContext.Proposals.Add(proposal);
        await dbContext.SaveChangesAsync();

        var request = ValidRequest();

        var result = await service.ResubmitProposalAsync(expert.Id, proposal.Id, request);

        result.Id.Should().Be(proposal.Id);
        result.Status.Should().Be(ProposalStatus.SUBMITTED);
        result.CoverLetter.Should().Be(request.CoverLetter);
        result.ProposedBudget.Should().Be(request.ProposedBudget);
        result.Milestones.Should().HaveCount(1);

        _notificationServiceMock.Verify(n => n.SendNotificationAsync(
            job.ClientId,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ResubmitProposalAsync_RejectedButJobInProgress_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var (expert, job) = SetupExpertAndJob(dbContext, JobStatus.IN_PROGRESS);
        var service = CreateService(dbContext);

        var proposal = new Proposal
        {
            ExpertId = expert.Id,
            JobId = job.Id,
            CoverLetter = "Cover",
            ProposedBudget = 500,
            Status = ProposalStatus.REJECTED
        };
        dbContext.Proposals.Add(proposal);
        await dbContext.SaveChangesAsync();

        Func<Task> act = async () => await service.ResubmitProposalAsync(expert.Id, proposal.Id, ValidRequest());
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Job is no longer open for proposals.");
    }
}
