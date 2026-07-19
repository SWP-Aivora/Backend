using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.ServiceRequestService;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class ServiceRequestServiceTests
{
    private AivoraDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AivoraDbContext(options);
    }

    private static (ServiceListing service, ServicePackage package) SeedPublishedService(AivoraDbContext dbContext, Guid expertId)
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
        return (service, package);
    }

    private static Guid SeedUser(AivoraDbContext dbContext)
    {
        var user = new User { FullName = "Test User", Email = $"{Guid.NewGuid()}@test.com", PasswordHash = "hash" };
        dbContext.Users.Add(user);
        dbContext.SaveChanges();
        return user.Id;
    }

    [Fact]
    public async Task CreateRequestAsync_WithPublishedServiceAndValidPackage_CreatesRequestWithSnapshot()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var (service, package) = SeedPublishedService(dbContext, expertId);
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());

        var result = await serviceRequestService.CreateRequestAsync(clientId, service.Id, new Request.CreateServiceRequestRequest
        {
            PackageId = package.Id,
            Note = "Please build it fast."
        });

        result.Status.Should().Be(ServiceRequestStatus.PENDING);
        result.PackagePrice.Should().Be(100);
        result.PackageTitle.Should().Be("Basic");
    }

    [Fact]
    public async Task CreateRequestAsync_WithNonExistentService_ThrowsNotFound()
    {
        var dbContext = GetDbContext();
        var clientId = SeedUser(dbContext);
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());

        var act = () => serviceRequestService.CreateRequestAsync(clientId, Guid.NewGuid(), new Request.CreateServiceRequestRequest { PackageId = Guid.NewGuid() });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateRequestAsync_WithPackageFromAnotherService_ThrowsNotFound()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var (service, _) = SeedPublishedService(dbContext, expertId);
        var (_, foreignPackage) = SeedPublishedService(dbContext, expertId);
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());

        var act = () => serviceRequestService.CreateRequestAsync(clientId, service.Id, new Request.CreateServiceRequestRequest { PackageId = foreignPackage.Id });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateRequestAsync_WithDraftService_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var (service, package) = SeedPublishedService(dbContext, expertId);
        service.Status = ServiceStatus.DRAFT;
        await dbContext.SaveChangesAsync();
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());

        var act = () => serviceRequestService.CreateRequestAsync(clientId, service.Id, new Request.CreateServiceRequestRequest { PackageId = package.Id });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateRequestAsync_WithOwnService_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var (service, package) = SeedPublishedService(dbContext, expertId);
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());

        var act = () => serviceRequestService.CreateRequestAsync(expertId, service.Id, new Request.CreateServiceRequestRequest { PackageId = package.Id });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateRequestAsync_WithDuplicatePendingRequest_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var (service, package) = SeedPublishedService(dbContext, expertId);
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());
        await serviceRequestService.CreateRequestAsync(clientId, service.Id, new Request.CreateServiceRequestRequest { PackageId = package.Id });

        var act = () => serviceRequestService.CreateRequestAsync(clientId, service.Id, new Request.CreateServiceRequestRequest { PackageId = package.Id });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GetRequestsByServiceAsync_WithNonOwnerExpert_ThrowsForbidden()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var otherExpertId = SeedUser(dbContext);
        var (service, _) = SeedPublishedService(dbContext, expertId);
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());

        var act = () => serviceRequestService.GetRequestsByServiceAsync(otherExpertId, service.Id);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetMyRequestsForExpertAsync_WithStatusFilter_ReturnsOnlyMatchingRequests()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var (service, package) = SeedPublishedService(dbContext, expertId);
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>());
        await serviceRequestService.CreateRequestAsync(clientId, service.Id, new Request.CreateServiceRequestRequest { PackageId = package.Id });

        var pending = await serviceRequestService.GetMyRequestsForExpertAsync(expertId, ServiceRequestStatus.PENDING);
        var accepted = await serviceRequestService.GetMyRequestsForExpertAsync(expertId, ServiceRequestStatus.ACCEPTED);

        pending.Should().HaveCount(1);
        accepted.Should().BeEmpty();
    }
}
