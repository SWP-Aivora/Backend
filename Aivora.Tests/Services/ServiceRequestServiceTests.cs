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
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.MessageService.IService>());

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
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.MessageService.IService>());

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
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.MessageService.IService>());

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
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.MessageService.IService>());

        var act = () => serviceRequestService.CreateRequestAsync(clientId, service.Id, new Request.CreateServiceRequestRequest { PackageId = package.Id });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateRequestAsync_WithOwnService_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var (service, package) = SeedPublishedService(dbContext, expertId);
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.MessageService.IService>());

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
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.MessageService.IService>());
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
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.MessageService.IService>());

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
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.MessageService.IService>());
        await serviceRequestService.CreateRequestAsync(clientId, service.Id, new Request.CreateServiceRequestRequest { PackageId = package.Id });

        var pending = await serviceRequestService.GetMyRequestsForExpertAsync(expertId, ServiceRequestStatus.PENDING);
        var accepted = await serviceRequestService.GetMyRequestsForExpertAsync(expertId, ServiceRequestStatus.ACCEPTED);

        pending.Should().HaveCount(1);
        accepted.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyRequestsForClientAsync_WithMultipleClients_ReturnsOnlyOwnRequests()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var clientA = SeedUser(dbContext);
        var clientB = SeedUser(dbContext);
        var (service, package) = SeedPublishedService(dbContext, expertId);
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.MessageService.IService>());
        await serviceRequestService.CreateRequestAsync(clientA, service.Id, new Request.CreateServiceRequestRequest { PackageId = package.Id });
        await serviceRequestService.CreateRequestAsync(clientB, service.Id, new Request.CreateServiceRequestRequest { PackageId = package.Id });

        var result = await serviceRequestService.GetMyRequestsForClientAsync(clientA, new Aivora.Services.Base.Request.PageRequest(), null);

        result.Items.Should().ContainSingle().Which.ClientId.Should().Be(clientA);
    }

    [Fact]
    public async Task GetMyRequestsForClientAsync_WithStatusFilter_ReturnsOnlyMatching()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var (service, package) = SeedPublishedService(dbContext, expertId);
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.MessageService.IService>());
        await serviceRequestService.CreateRequestAsync(clientId, service.Id, new Request.CreateServiceRequestRequest { PackageId = package.Id });

        var pending = await serviceRequestService.GetMyRequestsForClientAsync(clientId, new Aivora.Services.Base.Request.PageRequest(), ServiceRequestStatus.PENDING);
        var accepted = await serviceRequestService.GetMyRequestsForClientAsync(clientId, new Aivora.Services.Base.Request.PageRequest(), ServiceRequestStatus.ACCEPTED);

        pending.Items.Should().HaveCount(1);
        accepted.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyRequestsForClientAsync_WithExpertOnService_PopulatesExpertName()
    {
        var dbContext = GetDbContext();
        var expert = new User { FullName = "Expert Jane", Email = $"{Guid.NewGuid()}@test.com", PasswordHash = "hash" };
        dbContext.Users.Add(expert);
        dbContext.SaveChanges();
        var clientId = SeedUser(dbContext);
        var (service, package) = SeedPublishedService(dbContext, expert.Id);
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.MessageService.IService>());
        await serviceRequestService.CreateRequestAsync(clientId, service.Id, new Request.CreateServiceRequestRequest { PackageId = package.Id });

        var result = await serviceRequestService.GetMyRequestsForClientAsync(clientId, new Aivora.Services.Base.Request.PageRequest(), null);

        result.Items.Should().ContainSingle().Which.ExpertName.Should().Be("Expert Jane");
    }

    [Fact]
    public async Task GetMyRequestsForClientAsync_WithPageSizeSmallerThanTotal_ReturnsPagedResult()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var (service, package) = SeedPublishedService(dbContext, expertId);
        var (service2, package2) = SeedPublishedService(dbContext, expertId);
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.MessageService.IService>());
        await serviceRequestService.CreateRequestAsync(clientId, service.Id, new Request.CreateServiceRequestRequest { PackageId = package.Id });
        await serviceRequestService.CreateRequestAsync(clientId, service2.Id, new Request.CreateServiceRequestRequest { PackageId = package2.Id });

        var result = await serviceRequestService.GetMyRequestsForClientAsync(clientId, new Aivora.Services.Base.Request.PageRequest { PageSize = 1, PageIndex = 1 }, null);

        result.Items.Should().HaveCount(1);
        result.TotalItems.Should().Be(2);
        result.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task AcceptRequestAsync_ByOwnerExpert_SetsAcceptedAndCreatesConversation()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var (service, package) = SeedPublishedService(dbContext, expertId);
        var messageService = new Aivora.Services.MessageService.Service(dbContext);
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), messageService);
        var created = await serviceRequestService.CreateRequestAsync(clientId, service.Id, new Request.CreateServiceRequestRequest { PackageId = package.Id });

        var result = await serviceRequestService.AcceptRequestAsync(expertId, created.Id);

        result.Status.Should().Be(ServiceRequestStatus.ACCEPTED);
        var conversation = await dbContext.Conversations.FirstOrDefaultAsync(c => c.ServiceRequestId == created.Id);
        conversation.Should().NotBeNull();
        conversation!.ClientId.Should().Be(clientId);
        conversation.ExpertId.Should().Be(expertId);
    }

    [Fact]
    public async Task AcceptRequestAsync_ByNonOwnerExpert_ThrowsForbidden()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var otherExpertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var (service, package) = SeedPublishedService(dbContext, expertId);
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.MessageService.IService>());
        var created = await serviceRequestService.CreateRequestAsync(clientId, service.Id, new Request.CreateServiceRequestRequest { PackageId = package.Id });

        var act = () => serviceRequestService.AcceptRequestAsync(otherExpertId, created.Id);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task AcceptRequestAsync_WhenNotPending_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var (service, package) = SeedPublishedService(dbContext, expertId);
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.MessageService.IService>());
        var created = await serviceRequestService.CreateRequestAsync(clientId, service.Id, new Request.CreateServiceRequestRequest { PackageId = package.Id });
        await serviceRequestService.DeclineRequestAsync(expertId, created.Id);

        var act = () => serviceRequestService.AcceptRequestAsync(expertId, created.Id);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task DeclineRequestAsync_ByOwnerExpert_SetsDeclined()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var (service, package) = SeedPublishedService(dbContext, expertId);
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.MessageService.IService>());
        var created = await serviceRequestService.CreateRequestAsync(clientId, service.Id, new Request.CreateServiceRequestRequest { PackageId = package.Id });

        var result = await serviceRequestService.DeclineRequestAsync(expertId, created.Id);

        result.Status.Should().Be(ServiceRequestStatus.DECLINED);
    }

    [Fact]
    public async Task DeclineRequestAsync_ByNonOwnerExpert_ThrowsForbidden()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var otherExpertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var (service, package) = SeedPublishedService(dbContext, expertId);
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.MessageService.IService>());
        var created = await serviceRequestService.CreateRequestAsync(clientId, service.Id, new Request.CreateServiceRequestRequest { PackageId = package.Id });

        var act = () => serviceRequestService.DeclineRequestAsync(otherExpertId, created.Id);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task AcceptRequestAsync_WithExistingJobConversationForSamePair_CreatesSeparateConversation()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var clientId = SeedUser(dbContext);
        var (service, package) = SeedPublishedService(dbContext, expertId);
        var messageService = new Aivora.Services.MessageService.Service(dbContext);
        await messageService.GetOrCreateConversationAsync(clientId, expertId, jobId: Guid.NewGuid());
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), messageService);
        var created = await serviceRequestService.CreateRequestAsync(clientId, service.Id, new Request.CreateServiceRequestRequest { PackageId = package.Id });

        await serviceRequestService.AcceptRequestAsync(expertId, created.Id);

        var conversations = await dbContext.Conversations.Where(c => c.ClientId == clientId && c.ExpertId == expertId).ToListAsync();
        conversations.Should().HaveCount(2);
        conversations.Should().Contain(c => c.ServiceRequestId == created.Id);
        conversations.Should().Contain(c => c.JobId != null && c.ServiceRequestId == null);
    }

    [Fact]
    public async Task AcceptRequestAsync_WithNonExistentRequest_ThrowsNotFound()
    {
        var dbContext = GetDbContext();
        var expertId = SeedUser(dbContext);
        var serviceRequestService = new Service(dbContext, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.MessageService.IService>());

        var act = () => serviceRequestService.AcceptRequestAsync(expertId, Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
