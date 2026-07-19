using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.ServiceCatalogService;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aivora.Tests.Services;

public class ServiceCatalogServiceTests
{
    private AivoraDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AivoraDbContext(options);
    }

    // GetServiceByIdAsync Include(s => s.Expert) is a required navigation — EF InMemory's Include
    // translates a required FK as an inner join, so the row disappears unless the Expert row exists.
    private static Guid SeedExpert(AivoraDbContext dbContext)
    {
        var expert = new User { FullName = "Test Expert", Email = $"{Guid.NewGuid()}@test.com", PasswordHash = "hash" };
        dbContext.Users.Add(expert);
        dbContext.SaveChanges();
        return expert.Id;
    }

    private static Request.CreateServiceRequest ValidCreateRequest() => new()
    {
        Title = "Landing page in a week",
        Description = "I will build a landing page for you.",
        Packages = new List<Request.PackageRequest>
        {
            new() { Tier = PackageTier.BASIC, Title = "Basic", Price = 100, DeliveryDays = 3 }
        },
        Faqs = new List<Request.FaqRequest>
        {
            new() { Question = "What do I get?", Answer = "A landing page." }
        }
    };

    [Fact]
    public async Task CreateServiceAsync_WithValidRequest_CreatesDraftWithPackagesAndFaqs()
    {
        var dbContext = GetDbContext();
        var service = new Service(dbContext);
        var expertId = SeedExpert(dbContext);

        var result = await service.CreateServiceAsync(expertId, ValidCreateRequest());

        result.Status.Should().Be(ServiceStatus.DRAFT);
        result.Packages.Should().HaveCount(1);
        result.Faqs.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateServiceAsync_WithNoPackages_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var service = new Service(dbContext);
        var request = ValidCreateRequest();
        request.Packages = new List<Request.PackageRequest>();

        var act = () => service.CreateServiceAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task PublishServiceAsync_WithValidDraft_SetsStatusPublished()
    {
        var dbContext = GetDbContext();
        var service = new Service(dbContext);
        var expertId = SeedExpert(dbContext);
        var created = await service.CreateServiceAsync(expertId, ValidCreateRequest());

        var result = await service.PublishServiceAsync(expertId, created.Id);

        result.Status.Should().Be(ServiceStatus.PUBLISHED);
        result.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetServiceByIdAsync_WithDraftAndNonOwnerViewer_ThrowsNotFound()
    {
        var dbContext = GetDbContext();
        var service = new Service(dbContext);
        var expertId = SeedExpert(dbContext);
        var created = await service.CreateServiceAsync(expertId, ValidCreateRequest());

        var act = () => service.GetServiceByIdAsync(created.Id, Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetServiceByIdAsync_WithDraftAndOwnerViewer_ReturnsService()
    {
        var dbContext = GetDbContext();
        var service = new Service(dbContext);
        var expertId = SeedExpert(dbContext);
        var created = await service.CreateServiceAsync(expertId, ValidCreateRequest());

        var result = await service.GetServiceByIdAsync(created.Id, expertId);

        result.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetServiceByIdAsync_WithPublishedService_ReturnsServiceForAnyViewer()
    {
        var dbContext = GetDbContext();
        var service = new Service(dbContext);
        var expertId = SeedExpert(dbContext);
        var created = await service.CreateServiceAsync(expertId, ValidCreateRequest());
        await service.PublishServiceAsync(expertId, created.Id);

        var result = await service.GetServiceByIdAsync(created.Id, null);

        result.Status.Should().Be(ServiceStatus.PUBLISHED);
    }

    [Fact]
    public async Task GetMyServicesAsync_WithMultipleExperts_ReturnsOnlyOwnServices()
    {
        var dbContext = GetDbContext();
        var service = new Service(dbContext);
        var expertId = SeedExpert(dbContext);
        var otherExpertId = SeedExpert(dbContext);
        await service.CreateServiceAsync(expertId, ValidCreateRequest());
        await service.CreateServiceAsync(otherExpertId, ValidCreateRequest());

        var result = await service.GetMyServicesAsync(expertId);

        result.Should().HaveCount(1);
        result[0].ExpertId.Should().Be(expertId);
    }

    [Fact]
    public async Task UpdateServiceAsync_WithNewPackages_ReplacesExistingPackages()
    {
        var dbContext = GetDbContext();
        var service = new Service(dbContext);
        var expertId = SeedExpert(dbContext);
        var created = await service.CreateServiceAsync(expertId, ValidCreateRequest());

        var result = await service.UpdateServiceAsync(expertId, created.Id, new Request.UpdateServiceRequest
        {
            Packages = new List<Request.PackageRequest>
            {
                new() { Tier = PackageTier.PREMIUM, Title = "Premium", Price = 500, DeliveryDays = 7 }
            }
        });

        result.Packages.Should().HaveCount(1);
        result.Packages[0].Tier.Should().Be(PackageTier.PREMIUM);
    }

    [Fact]
    public async Task UpdateServiceAsync_WithNonOwnerExpert_ThrowsNotFound()
    {
        var dbContext = GetDbContext();
        var service = new Service(dbContext);
        var expertId = SeedExpert(dbContext);
        var created = await service.CreateServiceAsync(expertId, ValidCreateRequest());

        var act = () => service.UpdateServiceAsync(Guid.NewGuid(), created.Id, new Request.UpdateServiceRequest { Title = "Hacked" });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateServiceAsync_WithEmptyPackagesOnPublishedService_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var service = new Service(dbContext);
        var expertId = SeedExpert(dbContext);
        var created = await service.CreateServiceAsync(expertId, ValidCreateRequest());
        await service.PublishServiceAsync(expertId, created.Id);

        var act = () => service.UpdateServiceAsync(expertId, created.Id, new Request.UpdateServiceRequest
        {
            Packages = new List<Request.PackageRequest>()
        });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UpdateServiceAsync_WithEmptyFaqsOnPublishedService_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var service = new Service(dbContext);
        var expertId = SeedExpert(dbContext);
        var created = await service.CreateServiceAsync(expertId, ValidCreateRequest());
        await service.PublishServiceAsync(expertId, created.Id);

        var act = () => service.UpdateServiceAsync(expertId, created.Id, new Request.UpdateServiceRequest
        {
            Faqs = new List<Request.FaqRequest>()
        });

        await act.Should().ThrowAsync<ValidationException>();
    }
}
