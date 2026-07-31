using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.JobService;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class JobServiceUpdateTests
{
    private static AivoraDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AivoraDbContext(options);
    }

    private static (AivoraDbContext, User, Guid) SetupClient(AivoraDbContext dbContext)
    {
        var client = new User { FullName = "Test Client", Email = "test@test.com", PasswordHash = "hash" };
        dbContext.Users.Add(client);
        dbContext.SaveChanges();
        return (dbContext, client, client.Id);
    }

    [Fact]
    public async Task UpdateJobAsync_WithMilestones_ReplacesOldMilestones()
    {
        var dbContext = GetDbContext();
        var (_, _, clientId) = SetupClient(dbContext);

        var service = new Service(dbContext, new Aivora.Services.RealtimeService.NullRealtimeService(), Mock.Of<Aivora.Services.NotificationService.IService>());
        var categoryId = Guid.NewGuid();

        var job = new JobPost
        {
            ClientId = clientId,
            Title = "Test Job",
            OriginalDescription = "Desc",
            CategoryId = categoryId,
            Status = JobStatus.DRAFT,
            Milestones = new List<JobPostMilestone>
            {
                new() { Title = "Old M1", Amount = 100, DueDays = 5, OrderIndex = 0 },
                new() { Title = "Old M2", Amount = 200, DueDays = 10, OrderIndex = 1 }
            }
        };
        dbContext.JobPosts.Add(job);
        await dbContext.SaveChangesAsync();

        var request = new Request.UpdateJobRequest
        {
            Milestones = new List<Request.UpdateJobMilestoneRequest>
            {
                new() { Title = "New M1", Amount = 150, DueDays = 7, OrderIndex = 0 },
                new() { Title = "New M2", Amount = 250, DueDays = 14, OrderIndex = 1 },
                new() { Title = "New M3", Amount = 300, DueDays = 21, OrderIndex = 2 }
            }
        };

        var result = await service.UpdateJobAsync(clientId, job.Id, request);

        result.Milestones.Should().HaveCount(3);
        result.Milestones[0].Title.Should().Be("New M1");
        result.Milestones[0].Amount.Should().Be(150);
        result.Milestones[1].Title.Should().Be("New M2");
        result.Milestones[2].Title.Should().Be("New M3");
    }

    [Fact]
    public async Task UpdateJobAsync_WithoutMilestones_KeepsExistingMilestones()
    {
        var dbContext = GetDbContext();
        var (_, _, clientId) = SetupClient(dbContext);

        var service = new Service(dbContext, new Aivora.Services.RealtimeService.NullRealtimeService(), Mock.Of<Aivora.Services.NotificationService.IService>());

        var job = new JobPost
        {
            ClientId = clientId,
            Title = "Test Job",
            OriginalDescription = "Desc",
            CategoryId = Guid.NewGuid(),
            Status = JobStatus.DRAFT,
            Milestones = new List<JobPostMilestone>
            {
                new() { Title = "Keep Me", Amount = 100, DueDays = 5, OrderIndex = 0 }
            }
        };
        dbContext.JobPosts.Add(job);
        await dbContext.SaveChangesAsync();

        var request = new Request.UpdateJobRequest { Title = "Updated Title" };

        var result = await service.UpdateJobAsync(clientId, job.Id, request);

        result.Title.Should().Be("Updated Title");
        result.Milestones.Should().HaveCount(1);
        result.Milestones[0].Title.Should().Be("Keep Me");
    }

    [Fact]
    public async Task UpdateJobAsync_WithInvalidMilestoneAmount_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var (_, _, clientId) = SetupClient(dbContext);
        var service = new Service(dbContext, new Aivora.Services.RealtimeService.NullRealtimeService(), Mock.Of<Aivora.Services.NotificationService.IService>());

        var job = new JobPost
        {
            ClientId = clientId,
            Title = "Test Job",
            OriginalDescription = "Desc",
            CategoryId = Guid.NewGuid(),
            Status = JobStatus.DRAFT
        };
        dbContext.JobPosts.Add(job);
        await dbContext.SaveChangesAsync();

        var request = new Request.UpdateJobRequest
        {
            Milestones = new List<Request.UpdateJobMilestoneRequest>
            {
                new() { Title = "Bad", Amount = 0, DueDays = 5, OrderIndex = 0 }
            }
        };

        Func<Task> act = async () => await service.UpdateJobAsync(clientId, job.Id, request);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Milestone amounts must be greater than 0.");
    }

    [Fact]
    public async Task UpdateJobAsync_TwiceKeepingSameSkill_DoesNotThrow()
    {
        var dbContext = GetDbContext();
        var (_, _, clientId) = SetupClient(dbContext);
        var service = new Service(dbContext, new Aivora.Services.RealtimeService.NullRealtimeService(), Mock.Of<Aivora.Services.NotificationService.IService>());

        var skillA = Guid.NewGuid();
        var skillB = Guid.NewGuid();
        var skillC = Guid.NewGuid();

        var job = new JobPost
        {
            ClientId = clientId,
            Title = "Test Job",
            OriginalDescription = "Desc",
            CategoryId = Guid.NewGuid(),
            Status = JobStatus.DRAFT
        };
        dbContext.JobPosts.Add(job);
        await dbContext.SaveChangesAsync();

        // First update sets skills [A, B]
        await service.UpdateJobAsync(clientId, job.Id, new Request.UpdateJobRequest
        {
            SkillIds = new List<Guid> { skillA, skillB }
        });

        // Second update keeps A, drops B, adds C -> A gets removed+re-added with same JobId+SkillId
        Func<Task> act = async () => await service.UpdateJobAsync(clientId, job.Id, new Request.UpdateJobRequest
        {
            SkillIds = new List<Guid> { skillA, skillC }
        });

        await act.Should().NotThrowAsync();

        var result = await service.GetJobByIdAsync(job.Id);
        result.Skills.Select(s => s.Id).Should().BeEquivalentTo(new[] { skillA, skillC });
    }

    [Fact]
    public async Task UpdateJobAsync_ReAddingPreviouslyRemovedSkill_DoesNotThrow()
    {
        var dbContext = GetDbContext();
        var (_, _, clientId) = SetupClient(dbContext);
        var service = new Service(dbContext, new Aivora.Services.RealtimeService.NullRealtimeService(), Mock.Of<Aivora.Services.NotificationService.IService>());

        var skillA = Guid.NewGuid();
        var skillB = Guid.NewGuid();

        var job = new JobPost
        {
            ClientId = clientId,
            Title = "Test Job",
            OriginalDescription = "Desc",
            CategoryId = Guid.NewGuid(),
            Status = JobStatus.DRAFT
        };
        dbContext.JobPosts.Add(job);
        await dbContext.SaveChangesAsync();

        // Set [A, B], then drop B, then re-add B (B row was soft-deleted in between)
        await service.UpdateJobAsync(clientId, job.Id, new Request.UpdateJobRequest { SkillIds = new List<Guid> { skillA, skillB } });
        await service.UpdateJobAsync(clientId, job.Id, new Request.UpdateJobRequest { SkillIds = new List<Guid> { skillA } });

        Func<Task> act = async () => await service.UpdateJobAsync(clientId, job.Id, new Request.UpdateJobRequest { SkillIds = new List<Guid> { skillA, skillB } });

        await act.Should().NotThrowAsync();

        var result = await service.GetJobByIdAsync(job.Id);
        result.Skills.Select(s => s.Id).Should().BeEquivalentTo(new[] { skillA, skillB });
    }

    [Fact]
    public async Task UpdateJobAsync_NotOwner_ThrowsNotFoundException()
    {
        var dbContext = GetDbContext();
        var service = new Service(dbContext, new Aivora.Services.RealtimeService.NullRealtimeService(), Mock.Of<Aivora.Services.NotificationService.IService>());

        var job = new JobPost
        {
            ClientId = Guid.NewGuid(),
            Title = "Test Job",
            OriginalDescription = "Desc",
            CategoryId = Guid.NewGuid(),
            Status = JobStatus.DRAFT
        };
        dbContext.JobPosts.Add(job);
        await dbContext.SaveChangesAsync();

        var request = new Request.UpdateJobRequest { Title = "Hacked" };

        Func<Task> act = async () => await service.UpdateJobAsync(Guid.NewGuid(), job.Id, request);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task PublishJobAsync_CallsRealtimeService()
    {
        var dbContext = GetDbContext();
        var client = new User { FullName = "Client", Email = "c@test.com", PasswordHash = "h" };
        dbContext.Users.Add(client);
        var job = new JobPost { ClientId = client.Id, Title = "J", Status = JobStatus.DRAFT, OriginalDescription = "X", CategoryId = Guid.NewGuid() };
        dbContext.JobPosts.Add(job);
        await dbContext.SaveChangesAsync();

        var mockRealtime = new Mock<Aivora.Services.RealtimeService.IService>();
        var service = new Service(dbContext, mockRealtime.Object, Mock.Of<Aivora.Services.NotificationService.IService>());

        await service.PublishJobAsync(client.Id, job.Id);

        mockRealtime.Verify(r => r.SendJobStatusUpdateAsync(client.Id, job.Id, Aivora.Repositories.Enums.JobStatus.OPEN, "J"), Times.Once);
    }

    [Fact]
    public async Task CancelJobAsync_NotOwner_ThrowsNotFoundException()
    {
        var dbContext = GetDbContext();
        var service = new Service(dbContext, new Aivora.Services.RealtimeService.NullRealtimeService(), Mock.Of<Aivora.Services.NotificationService.IService>());

        var job = new JobPost
        {
            ClientId = Guid.NewGuid(),
            Title = "Test Job",
            OriginalDescription = "Desc",
            CategoryId = Guid.NewGuid(),
            Status = JobStatus.OPEN
        };
        dbContext.JobPosts.Add(job);
        await dbContext.SaveChangesAsync();

        Func<Task> act = async () => await service.CancelJobAsync(Guid.NewGuid(), job.Id, "Not my job");
        await act.Should().ThrowAsync<NotFoundException>();

        var dbJob = await dbContext.JobPosts.FindAsync(job.Id);
        dbJob!.Status.Should().Be(JobStatus.OPEN);
    }

    [Fact]
    public async Task CancelJobAsync_ExpiresPendingJobInvitesForThatJob()
    {
        var dbContext = GetDbContext();
        var (_, client, clientId) = SetupClient(dbContext);
        var expert = new User { FullName = "Expert", Email = "expert@test.com", PasswordHash = "hash" };
        dbContext.Users.Add(expert);

        var job = new JobPost
        {
            ClientId = clientId,
            Title = "Test Job",
            OriginalDescription = "Desc",
            CategoryId = Guid.NewGuid(),
            Status = JobStatus.OPEN
        };
        dbContext.JobPosts.Add(job);
        await dbContext.SaveChangesAsync();

        var invite = new JobInvite
        {
            JobId = job.Id,
            ExpertId = expert.Id,
            ClientId = clientId,
            Status = JobInviteStatus.PENDING
        };
        dbContext.JobInvites.Add(invite);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, new Aivora.Services.RealtimeService.NullRealtimeService(), Mock.Of<Aivora.Services.NotificationService.IService>());
        await service.CancelJobAsync(clientId, job.Id, "No longer needed");

        var dbInvite = await dbContext.JobInvites.FindAsync(invite.Id);
        dbInvite!.Status.Should().Be(JobInviteStatus.EXPIRED);
    }

    [Fact]
    public async Task GetMyJobsAsync_WithNullClientId_ReturnsJobsFromAllClientsIncludingDrafts()
    {
        var dbContext = GetDbContext();
        var (_, _, clientId) = SetupClient(dbContext);
        var otherClient = new User { FullName = "Other Client", Email = "other@test.com", PasswordHash = "hash" };
        dbContext.Users.Add(otherClient);

        dbContext.JobPosts.AddRange(
            new JobPost { ClientId = clientId, Title = "Draft Job", OriginalDescription = "Desc", CategoryId = Guid.NewGuid(), Status = JobStatus.DRAFT },
            new JobPost { ClientId = otherClient.Id, Title = "Open Job", OriginalDescription = "Desc", CategoryId = Guid.NewGuid(), Status = JobStatus.OPEN }
        );
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, new Aivora.Services.RealtimeService.NullRealtimeService(), Mock.Of<Aivora.Services.NotificationService.IService>());

        var result = await service.GetMyJobsAsync(null, new Aivora.Services.Base.Request.PageRequest { PageIndex = 1, PageSize = 10 });

        result.Items.Should().HaveCount(2);
        result.Items.Select(j => j.Title).Should().BeEquivalentTo(new[] { "Draft Job", "Open Job" });
    }

    [Fact]
    public async Task GetMyJobsAsync_WithNullClientIdAndStatusFilter_OnlyReturnsMatchingStatus()
    {
        var dbContext = GetDbContext();
        var (_, _, clientId) = SetupClient(dbContext);

        dbContext.JobPosts.AddRange(
            new JobPost { ClientId = clientId, Title = "Draft Job", OriginalDescription = "Desc", CategoryId = Guid.NewGuid(), Status = JobStatus.DRAFT },
            new JobPost { ClientId = clientId, Title = "Open Job", OriginalDescription = "Desc", CategoryId = Guid.NewGuid(), Status = JobStatus.OPEN }
        );
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, new Aivora.Services.RealtimeService.NullRealtimeService(), Mock.Of<Aivora.Services.NotificationService.IService>());

        var result = await service.GetMyJobsAsync(null, new Aivora.Services.Base.Request.PageRequest { PageIndex = 1, PageSize = 10 }, JobStatus.DRAFT);

        result.Items.Should().ContainSingle();
        result.Items[0].Title.Should().Be("Draft Job");
    }

    [Fact]
    public async Task UpdateJobAsync_OpenJobWithActiveProposals_NotifiesOnlyNonRejectedExperts()
    {
        // Arrange
        var dbContext = GetDbContext();
        var (_, _, clientId) = SetupClient(dbContext);
        var submittedExpertId = Guid.NewGuid();
        var rejectedExpertId = Guid.NewGuid();
        var withdrawnExpertId = Guid.NewGuid();

        var job = new JobPost { ClientId = clientId, Title = "Job", Status = JobStatus.OPEN, OriginalDescription = "X", CategoryId = Guid.NewGuid() };
        dbContext.JobPosts.Add(job);
        await dbContext.SaveChangesAsync();

        dbContext.Proposals.AddRange(
            new Proposal { JobId = job.Id, ExpertId = submittedExpertId, CoverLetter = "a", ProposedBudget = 1, Status = ProposalStatus.SUBMITTED },
            new Proposal { JobId = job.Id, ExpertId = rejectedExpertId, CoverLetter = "b", ProposedBudget = 1, Status = ProposalStatus.REJECTED },
            new Proposal { JobId = job.Id, ExpertId = withdrawnExpertId, CoverLetter = "c", ProposedBudget = 1, Status = ProposalStatus.WITHDRAWN });
        await dbContext.SaveChangesAsync();

        var notificationMock = new Mock<Aivora.Services.NotificationService.IService>();
        var service = new Service(dbContext, new Aivora.Services.RealtimeService.NullRealtimeService(), notificationMock.Object);

        // Act
        await service.UpdateJobAsync(clientId, job.Id, new Request.UpdateJobRequest { Title = "Job v2" });

        // Assert
        notificationMock.Verify(n => n.SendInBackground(submittedExpertId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        notificationMock.Verify(n => n.SendInBackground(rejectedExpertId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        notificationMock.Verify(n => n.SendInBackground(withdrawnExpertId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateJobAsync_DraftJob_DoesNotNotify()
    {
        // Arrange
        var dbContext = GetDbContext();
        var (_, _, clientId) = SetupClient(dbContext);
        var job = new JobPost { ClientId = clientId, Title = "Job", Status = JobStatus.DRAFT, OriginalDescription = "X", CategoryId = Guid.NewGuid() };
        dbContext.JobPosts.Add(job);
        await dbContext.SaveChangesAsync();

        var notificationMock = new Mock<Aivora.Services.NotificationService.IService>();
        var service = new Service(dbContext, new Aivora.Services.RealtimeService.NullRealtimeService(), notificationMock.Object);

        // Act
        await service.UpdateJobAsync(clientId, job.Id, new Request.UpdateJobRequest { Title = "Job v2" });

        // Assert
        notificationMock.Verify(n => n.SendInBackground(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateJobAsync_NoActualChanges_DoesNotNotify()
    {
        // Arrange: empty request body changes nothing
        var dbContext = GetDbContext();
        var (_, _, clientId) = SetupClient(dbContext);
        var job = new JobPost { ClientId = clientId, Title = "Job", Status = JobStatus.OPEN, OriginalDescription = "X", CategoryId = Guid.NewGuid() };
        dbContext.JobPosts.Add(job);
        await dbContext.SaveChangesAsync();

        dbContext.Proposals.Add(new Proposal { JobId = job.Id, ExpertId = Guid.NewGuid(), CoverLetter = "a", ProposedBudget = 1, Status = ProposalStatus.SUBMITTED });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var notificationMock = new Mock<Aivora.Services.NotificationService.IService>();
        var service = new Service(dbContext, new Aivora.Services.RealtimeService.NullRealtimeService(), notificationMock.Object);

        // Act
        await service.UpdateJobAsync(clientId, job.Id, new Request.UpdateJobRequest());

        // Assert
        notificationMock.Verify(n => n.SendInBackground(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
