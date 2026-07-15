using Aivora.Repositories.Data;
using Aivora.Services.AIJobAssistantService;
using Aivora.Services.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class AIServiceGeneratorTests
{
    private readonly Mock<Aivora.Services.JobService.IService> _jobServiceMock = new();
    private readonly Mock<IAIJobSuggestionProvider> _suggestionProviderMock = new();
    private readonly Mock<IAIJobRefinementProvider> _refinementProviderMock = new();
    private readonly Mock<IAIServiceDescriptionProvider> _serviceDescriptionProviderMock = new();

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public async Task GenerateServiceDescriptionAsync_RejectsRawInputOutsideAllowedLength(string rawInput)
    {
        var service = CreateService();
        var request = BuildValidRequest();
        request.RawInput = rawInput;

        Func<Task> act = async () => await service.GenerateServiceDescriptionAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GenerateServiceDescriptionAsync_RejectsSkillCountOutsideOneToTwenty()
    {
        var service = CreateService();
        var request = BuildValidRequest();
        request.Skills = new List<string>();

        Func<Task> act = async () => await service.GenerateServiceDescriptionAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GenerateServiceDescriptionAsync_RejectsNullSkills()
    {
        var service = CreateService();
        var request = BuildValidRequest();
        request.Skills = null!;

        Func<Task> act = async () => await service.GenerateServiceDescriptionAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GenerateServiceDescriptionAsync_RejectsInvalidPriceAndDeliveryDays()
    {
        var service = CreateService();
        var request = BuildValidRequest();
        request.PriceFrom = 0;

        Func<Task> invalidPrice = async () => await service.GenerateServiceDescriptionAsync(Guid.NewGuid(), request);
        await invalidPrice.Should().ThrowAsync<ValidationException>();

        request = BuildValidRequest();
        request.DeliveryDays = 0;
        Func<Task> invalidDelivery = async () => await service.GenerateServiceDescriptionAsync(Guid.NewGuid(), request);
        await invalidDelivery.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GenerateServiceDescriptionAsync_RejectsInvalidToneTargetClientAndLanguage()
    {
        var service = CreateService();
        var request = BuildValidRequest();
        request.Tone = "casual";

        Func<Task> invalidTone = async () => await service.GenerateServiceDescriptionAsync(Guid.NewGuid(), request);
        await invalidTone.Should().ThrowAsync<ValidationException>();

        request = BuildValidRequest();
        request.TargetClient = "government";
        Func<Task> invalidTarget = async () => await service.GenerateServiceDescriptionAsync(Guid.NewGuid(), request);
        await invalidTarget.Should().ThrowAsync<ValidationException>();

        request = BuildValidRequest();
        request.Language = "fr";
        Func<Task> invalidLanguage = async () => await service.GenerateServiceDescriptionAsync(Guid.NewGuid(), request);
        await invalidLanguage.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GenerateServiceDescriptionAsync_UsesProviderOutput()
    {
        var service = CreateService();
        var request = BuildValidRequest();
        var draft = new AIServiceDescriptionDraft
        {
            SuggestedTitle = "AI automation service",
            SuggestedDescription = "I build AI automation systems.",
            Packages = new List<Response.ServicePackageResponse>
            {
                new() { Name = "Basic", Description = "Basic", Price = 100, DeliveryDays = 3, Features = new List<string> { "Setup" } },
                new() { Name = "Standard", Description = "Standard", Price = 200, DeliveryDays = 7, Features = new List<string> { "Build" } },
                new() { Name = "Premium", Description = "Premium", Price = 400, DeliveryDays = 14, Features = new List<string> { "Scale" } }
            },
            Faqs = new List<Response.ServiceFaqResponse>
            {
                new() { Question = "Start?", Answer = "Send requirements." }
            }
        };

        _serviceDescriptionProviderMock
            .Setup(x => x.GenerateServiceDescriptionAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);

        var result = await service.GenerateServiceDescriptionAsync(Guid.NewGuid(), request);

        result.SuggestedTitle.Should().Be(draft.SuggestedTitle);
        result.Packages.Select(x => x.Name).Should().Equal("Basic", "Standard", "Premium");
        result.Faqs.Should().ContainSingle();
        _serviceDescriptionProviderMock.Verify(x => x.GenerateServiceDescriptionAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    private Service CreateService()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new Service(
            new AivoraDbContext(options),
            _jobServiceMock.Object,
            _suggestionProviderMock.Object,
            _refinementProviderMock.Object,
            _serviceDescriptionProviderMock.Object,
            new Aivora.Services.CategoryService.Service(new AivoraDbContext(options)),
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<Service>());
    }

    private static Request.GenerateServiceDescriptionRequest BuildValidRequest()
    {
        return new Request.GenerateServiceDescriptionRequest
        {
            RawInput = "I build production-ready AI automation services for ecommerce teams.",
            Skills = new List<string> { "AI", "Automation" },
            PriceFrom = 100,
            DeliveryDays = 7,
            Tone = "professional",
            TargetClient = "startup",
            Language = "vi"
        };
    }
}
