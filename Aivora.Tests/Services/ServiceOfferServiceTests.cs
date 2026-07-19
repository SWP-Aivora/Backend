using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.ServiceOfferService;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class ServiceOfferServiceTests
{
    private AivoraDbContext GetDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AivoraDbContext(options);
    }

    private static Guid SeedUser(AivoraDbContext dbContext)
    {
        var user = new User { FullName = "Test User", Email = $"{Guid.NewGuid()}@test.com", PasswordHash = "hash" };
        dbContext.Users.Add(user);
        dbContext.SaveChanges();
        return user.Id;
    }

    private static ServiceRequest SeedAcceptedServiceRequest(AivoraDbContext dbContext, Guid expertId, Guid clientId)
    {
        var package = new ServicePackage { Tier = PackageTier.BASIC, Title = "Basic", Price = 100, DeliveryDays = 3 };
        var service = new ServiceListing
        {
            ExpertId = expertId,
            Title = "Landing page",
            Description = "A landing page.",
            Status = ServiceStatus.PUBLISHED,
            Packages = new List<ServicePackage> { package }
        };
        dbContext.Services.Add(service);
        dbContext.SaveChanges();

        var request = new ServiceRequest
        {
            ServiceId = service.Id,
            ClientId = clientId,
            PackageId = package.Id,
            Status = ServiceRequestStatus.ACCEPTED,
            PackageTitle = package.Title,
            PackagePrice = package.Price,
            PackageDeliveryDays = package.DeliveryDays
        };
        dbContext.ServiceRequests.Add(request);
        dbContext.SaveChanges();
        return request;
    }

    private static Request.CreateServiceOfferRequest BuildOfferRequest()
    {
        return new Request.CreateServiceOfferRequest
        {
            Amount = 500,
            Milestones = new List<Request.CreateServiceOfferMilestoneRequest>
            {
                new() { Title = "Milestone 1", Amount = 500, DueDays = 5, OrderIndex = 0 }
            }
        };
    }

    [Fact]
    public async Task CreateOfferAsync_WithAcceptedServiceRequest_CreatesOfferWithMilestones()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var request = SeedAcceptedServiceRequest(dbContext, expertId, clientId);
        var service = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());

        var result = await service.CreateOfferAsync(expertId, request.Id, BuildOfferRequest());

        result.Status.Should().Be(ServiceOfferStatus.PENDING);
        result.Amount.Should().Be(500);
        result.Milestones.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateOfferAsync_ByNonOwnerExpert_ThrowsForbidden()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var otherExpertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var request = SeedAcceptedServiceRequest(dbContext, expertId, clientId);
        var service = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());

        var act = () => service.CreateOfferAsync(otherExpertId, request.Id, BuildOfferRequest());

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CreateOfferAsync_WhenServiceRequestNotAccepted_ThrowsValidation()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var request = SeedAcceptedServiceRequest(dbContext, expertId, clientId);
        request.Status = ServiceRequestStatus.PENDING;
        await dbContext.SaveChangesAsync();
        var service = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());

        var act = () => service.CreateOfferAsync(expertId, request.Id, BuildOfferRequest());

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateOfferAsync_WithNoMilestones_ThrowsValidation()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var request = SeedAcceptedServiceRequest(dbContext, expertId, clientId);
        var service = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());

        var act = () => service.CreateOfferAsync(expertId, request.Id, new Request.CreateServiceOfferRequest { Amount = 500 });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task AcceptOfferAsync_ByClient_CreatesProjectWithMilestoneAndStep()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var serviceRequest = SeedAcceptedServiceRequest(dbContext, expertId, clientId);
        var service = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());
        var offer = await service.CreateOfferAsync(expertId, serviceRequest.Id, BuildOfferRequest());

        var result = await service.AcceptOfferAsync(clientId, offer.Id);

        result.Status.Should().Be(ServiceOfferStatus.ACCEPTED.ToString());
        var project = await dbContext.Projects
            .Include(p => p.Milestones).ThenInclude(m => m.Steps)
            .FirstOrDefaultAsync(p => p.Id == result.ProjectId);
        project.Should().NotBeNull();
        project!.ServiceRequestId.Should().Be(serviceRequest.Id);
        project.JobId.Should().BeNull();
        project.AcceptedProposalId.Should().BeNull();
        project.ClientId.Should().Be(clientId);
        project.ExpertId.Should().Be(expertId);
        project.Milestones.Should().HaveCount(1);
        project.Milestones.First().Steps.Should().ContainSingle(s => s.Title == "Created");
    }

    [Fact]
    public async Task AcceptOfferAsync_ByNonClient_ThrowsForbidden()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var outsiderId = SeedUser(dbContext);
        var serviceRequest = SeedAcceptedServiceRequest(dbContext, expertId, clientId);
        var service = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());
        var offer = await service.CreateOfferAsync(expertId, serviceRequest.Id, BuildOfferRequest());

        var act = () => service.AcceptOfferAsync(outsiderId, offer.Id);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task AcceptOfferAsync_TwiceSequentially_OnlyCreatesOneProject()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var serviceRequest = SeedAcceptedServiceRequest(dbContext, expertId, clientId);
        var service = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());
        var offer = await service.CreateOfferAsync(expertId, serviceRequest.Id, BuildOfferRequest());

        await service.AcceptOfferAsync(clientId, offer.Id);
        var act = () => service.AcceptOfferAsync(clientId, offer.Id);

        await act.Should().ThrowAsync<ValidationException>();
        var projects = await dbContext.Projects.Where(p => p.ServiceRequestId == serviceRequest.Id).ToListAsync();
        projects.Should().HaveCount(1);
    }

    // Note: a genuine concurrent-race test (two separate DbContexts racing on the same PENDING
    // offer) was tried here and does not pass — EF Core's InMemory provider does not enforce the
    // filtered unique index on Projects.ServiceRequestId (confirmed empirically: both accepts
    // succeed, creating 2 Projects). That DB-level constraint can only be verified against real
    // Postgres, which this repo's test suite does not run. The sequential test above proves the
    // in-process guard (Status != PENDING) the InMemory provider *can* verify; the unique index
    // itself is a code-reviewed, untested-in-CI backstop for the true concurrent case.

    [Fact]
    public async Task AcceptOfferAsync_DoesNotClosePublishedService()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var serviceRequest = SeedAcceptedServiceRequest(dbContext, expertId, clientId);
        var service = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());
        var offer = await service.CreateOfferAsync(expertId, serviceRequest.Id, BuildOfferRequest());

        await service.AcceptOfferAsync(clientId, offer.Id);

        var listing = await dbContext.Services.FirstAsync(s => s.Id == serviceRequest.ServiceId);
        listing.Status.Should().Be(ServiceStatus.PUBLISHED);
    }
}
