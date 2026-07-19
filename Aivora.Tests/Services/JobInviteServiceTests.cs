using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.JobInviteService;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class JobInviteServiceTests
{
    private AivoraDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AivoraDbContext(options);
    }

    private static User SeedUser(AivoraDbContext dbContext, UserRole role)
    {
        var user = new User { FullName = "Test User", Email = $"{Guid.NewGuid()}@test.com", PasswordHash = "hash", Role = role };
        dbContext.Users.Add(user);
        dbContext.SaveChanges();
        return user;
    }

    private static JobPost SeedOpenJob(AivoraDbContext dbContext, Guid clientId)
    {
        var job = new JobPost { ClientId = clientId, Title = "AI chatbot", Status = JobStatus.OPEN, OriginalDescription = "X" };
        dbContext.JobPosts.Add(job);
        dbContext.SaveChanges();
        return job;
    }

    [Fact]
    public async Task CreateInviteAsync_WithOpenJobAndValidExpert_CreatesPendingInvite()
    {
        var dbContext = GetDbContext();
        var client = SeedUser(dbContext, UserRole.CLIENT);
        var expert = SeedUser(dbContext, UserRole.EXPERT);
        var job = SeedOpenJob(dbContext, client.Id);
        var service = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());

        var result = await service.CreateInviteAsync(client.Id, job.Id, new Request.CreateJobInviteRequest { ExpertId = expert.Id });

        result.Status.Should().Be(JobInviteStatus.PENDING);
        result.ExpertId.Should().Be(expert.Id);
        result.JobId.Should().Be(job.Id);
    }

    [Fact]
    public async Task CreateInviteAsync_ByNonOwnerClient_ThrowsForbidden()
    {
        var dbContext = GetDbContext();
        var client = SeedUser(dbContext, UserRole.CLIENT);
        var otherClient = SeedUser(dbContext, UserRole.CLIENT);
        var expert = SeedUser(dbContext, UserRole.EXPERT);
        var job = SeedOpenJob(dbContext, client.Id);
        var service = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());

        var act = () => service.CreateInviteAsync(otherClient.Id, job.Id, new Request.CreateJobInviteRequest { ExpertId = expert.Id });

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CreateInviteAsync_WithNonOpenJob_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var client = SeedUser(dbContext, UserRole.CLIENT);
        var expert = SeedUser(dbContext, UserRole.EXPERT);
        var job = SeedOpenJob(dbContext, client.Id);
        job.Status = JobStatus.IN_PROGRESS;
        await dbContext.SaveChangesAsync();
        var service = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());

        var act = () => service.CreateInviteAsync(client.Id, job.Id, new Request.CreateJobInviteRequest { ExpertId = expert.Id });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateInviteAsync_WithNonExpertUser_ThrowsNotFound()
    {
        var dbContext = GetDbContext();
        var client = SeedUser(dbContext, UserRole.CLIENT);
        var notExpert = SeedUser(dbContext, UserRole.CLIENT);
        var job = SeedOpenJob(dbContext, client.Id);
        var service = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());

        var act = () => service.CreateInviteAsync(client.Id, job.Id, new Request.CreateJobInviteRequest { ExpertId = notExpert.Id });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateInviteAsync_WithDuplicateInvite_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var client = SeedUser(dbContext, UserRole.CLIENT);
        var expert = SeedUser(dbContext, UserRole.EXPERT);
        var job = SeedOpenJob(dbContext, client.Id);
        var service = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());
        await service.CreateInviteAsync(client.Id, job.Id, new Request.CreateJobInviteRequest { ExpertId = expert.Id });

        var act = () => service.CreateInviteAsync(client.Id, job.Id, new Request.CreateJobInviteRequest { ExpertId = expert.Id });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateInviteAsync_AfterInviteWasDeclined_StillThrowsValidationException()
    {
        // The unique constraint is on (JobId, ExpertId) with no Status scoping (unlike the
        // ServiceRequest PENDING-only precedent), so a declined invite still blocks re-inviting
        // the same pair.
        var dbContext = GetDbContext();
        var client = SeedUser(dbContext, UserRole.CLIENT);
        var expert = SeedUser(dbContext, UserRole.EXPERT);
        var job = SeedOpenJob(dbContext, client.Id);
        var service = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());
        var created = await service.CreateInviteAsync(client.Id, job.Id, new Request.CreateJobInviteRequest { ExpertId = expert.Id });
        await service.DeclineInviteAsync(expert.Id, created.Id);

        var act = () => service.CreateInviteAsync(client.Id, job.Id, new Request.CreateJobInviteRequest { ExpertId = expert.Id });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task AcceptInviteAsync_ByInvitedExpert_SetsAcceptedWithRespondedAt()
    {
        var dbContext = GetDbContext();
        var client = SeedUser(dbContext, UserRole.CLIENT);
        var expert = SeedUser(dbContext, UserRole.EXPERT);
        var job = SeedOpenJob(dbContext, client.Id);
        var service = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());
        var created = await service.CreateInviteAsync(client.Id, job.Id, new Request.CreateJobInviteRequest { ExpertId = expert.Id });

        var result = await service.AcceptInviteAsync(expert.Id, created.Id);

        result.Status.Should().Be(JobInviteStatus.ACCEPTED);
        result.RespondedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AcceptInviteAsync_ByNonInvitedExpert_ThrowsForbidden()
    {
        var dbContext = GetDbContext();
        var client = SeedUser(dbContext, UserRole.CLIENT);
        var expert = SeedUser(dbContext, UserRole.EXPERT);
        var otherExpert = SeedUser(dbContext, UserRole.EXPERT);
        var job = SeedOpenJob(dbContext, client.Id);
        var service = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());
        var created = await service.CreateInviteAsync(client.Id, job.Id, new Request.CreateJobInviteRequest { ExpertId = expert.Id });

        var act = () => service.AcceptInviteAsync(otherExpert.Id, created.Id);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task AcceptInviteAsync_WhenNotPending_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var client = SeedUser(dbContext, UserRole.CLIENT);
        var expert = SeedUser(dbContext, UserRole.EXPERT);
        var job = SeedOpenJob(dbContext, client.Id);
        var service = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());
        var created = await service.CreateInviteAsync(client.Id, job.Id, new Request.CreateJobInviteRequest { ExpertId = expert.Id });
        await service.DeclineInviteAsync(expert.Id, created.Id);

        var act = () => service.AcceptInviteAsync(expert.Id, created.Id);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task DeclineInviteAsync_ByInvitedExpert_SetsDeclined()
    {
        var dbContext = GetDbContext();
        var client = SeedUser(dbContext, UserRole.CLIENT);
        var expert = SeedUser(dbContext, UserRole.EXPERT);
        var job = SeedOpenJob(dbContext, client.Id);
        var service = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());
        var created = await service.CreateInviteAsync(client.Id, job.Id, new Request.CreateJobInviteRequest { ExpertId = expert.Id });

        var result = await service.DeclineInviteAsync(expert.Id, created.Id);

        result.Status.Should().Be(JobInviteStatus.DECLINED);
    }

    [Fact]
    public async Task GetInvitesByJobAsync_ByNonOwnerClient_ThrowsForbidden()
    {
        var dbContext = GetDbContext();
        var client = SeedUser(dbContext, UserRole.CLIENT);
        var otherClient = SeedUser(dbContext, UserRole.CLIENT);
        var job = SeedOpenJob(dbContext, client.Id);
        var service = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());

        var act = () => service.GetInvitesByJobAsync(otherClient.Id, job.Id);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetMyInvitesForExpertAsync_WithStatusFilter_ReturnsOnlyMatchingInvites()
    {
        var dbContext = GetDbContext();
        var client = SeedUser(dbContext, UserRole.CLIENT);
        var expert = SeedUser(dbContext, UserRole.EXPERT);
        var job = SeedOpenJob(dbContext, client.Id);
        var service = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());
        await service.CreateInviteAsync(client.Id, job.Id, new Request.CreateJobInviteRequest { ExpertId = expert.Id });

        var pending = await service.GetMyInvitesForExpertAsync(expert.Id, JobInviteStatus.PENDING);
        var accepted = await service.GetMyInvitesForExpertAsync(expert.Id, JobInviteStatus.ACCEPTED);

        pending.Should().HaveCount(1);
        accepted.Should().BeEmpty();
    }
}
