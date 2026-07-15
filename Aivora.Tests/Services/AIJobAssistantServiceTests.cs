using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.AIJobAssistantService;
using Aivora.Services.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class AIJobAssistantServiceTests
{
    private readonly Mock<Aivora.Services.JobService.IService> _jobServiceMock = new();
    private readonly Mock<IAIJobSuggestionProvider> _suggestionProviderMock = new();
    private readonly Mock<IAIJobRefinementProvider> _refinementProviderMock = new();
    private readonly Mock<IAIServiceDescriptionProvider> _serviceDescriptionProviderMock = new();

    private static AivoraDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AivoraDbContext(options);
    }

    private Service CreateService(AivoraDbContext dbContext)
    {
        return new Service(
            dbContext,
            _jobServiceMock.Object,
            _suggestionProviderMock.Object,
            _refinementProviderMock.Object,
            _serviceDescriptionProviderMock.Object,
            new Aivora.Services.CategoryService.Service(dbContext, new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())),
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<Service>());
    }

    [Fact]
    public async Task GenerateSuggestionAsync_UsesProviderOutput_AndPersistsStructuredFields()
    {
        var dbContext = GetDbContext();
        var service = CreateService(dbContext);
        var clientId = Guid.NewGuid();
        var request = new Request.GenerateSuggestionRequest { RawInput = "Build a React AI chatbot for ecommerce." };
        var draft = BuildDraft();

        _suggestionProviderMock
            .Setup(x => x.GenerateSuggestionAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);

        var result = await service.GenerateSuggestionAsync(clientId, request);

        result.SuggestedTitle.Should().Be(draft.SuggestedTitle);
        result.BusinessDomain.Should().Be(draft.BusinessDomain);
        result.ExpectedOutcome.Should().Be(draft.ExpectedOutcome);
        result.BudgetType.Should().Be(draft.BudgetType);
        result.Currency.Should().Be(draft.Currency);
        result.ExperienceLevel.Should().Be(draft.ExperienceLevel);
        result.ClarifyingAnswers.Should().HaveCount(draft.ClarifyingQuestions.Count);
        result.AIModel.Should().Be(draft.AIModel);

        var saved = await dbContext.AIJobSuggestions.FindAsync(result.Id);
        saved.Should().NotBeNull();
        saved!.SuggestedBudgetType.Should().Be(draft.BudgetType);
        saved.Currency.Should().Be(draft.Currency);
        saved.SuggestedBusinessDomain.Should().Be(draft.BusinessDomain);
        saved.SuggestedExpectedOutcome.Should().Be(draft.ExpectedOutcome);
        saved.SuggestedExperienceLevel.Should().Be(draft.ExperienceLevel);
        saved.ClarifyingAnswersJson.Should().NotBeNullOrWhiteSpace();
        _suggestionProviderMock.Verify(x => x.GenerateSuggestionAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSuggestionAsync_ReturnsOwnedSuggestion_AndRejectsNonOwner()
    {
        var dbContext = GetDbContext();
        var service = CreateService(dbContext);
        var clientId = Guid.NewGuid();
        var suggestion = BuildSuggestion(clientId);
        dbContext.AIJobSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync();

        var result = await service.GetSuggestionAsync(clientId, suggestion.Id);
        result.Id.Should().Be(suggestion.Id);

        Func<Task> act = async () => await service.GetSuggestionAsync(Guid.NewGuid(), suggestion.Id);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task PatchSuggestionAsync_UpdatesAllowedGeneratedFields()
    {
        var dbContext = GetDbContext();
        var service = CreateService(dbContext);
        var clientId = Guid.NewGuid();
        var suggestion = BuildSuggestion(clientId);
        dbContext.AIJobSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync();

        var result = await service.PatchSuggestionAsync(clientId, suggestion.Id, new Request.PatchSuggestionRequest
        {
            SuggestedTitle = "Updated title",
            BusinessDomain = "Retail",
            BudgetType = BudgetType.HOURLY,
            Currency = "usd",
            ExperienceLevel = SkillLevel.EXPERT,
            ClarifyingAnswers = new List<string> { "Use Shopify" }
        });

        result.SuggestedTitle.Should().Be("Updated title");
        result.BusinessDomain.Should().Be("Retail");
        result.BudgetType.Should().Be(BudgetType.HOURLY);
        result.Currency.Should().Be("USD");
        result.ExperienceLevel.Should().Be(SkillLevel.EXPERT);
        result.ClarifyingAnswers.Should().ContainSingle().Which.Should().Be("Use Shopify");
        result.Status.Should().Be(AIJobSuggestionStatus.GENERATED.ToString());
    }

    [Fact]
    public async Task PatchSuggestionAsync_RejectsAcceptedSuggestion()
    {
        var dbContext = GetDbContext();
        var service = CreateService(dbContext);
        var clientId = Guid.NewGuid();
        var suggestion = BuildSuggestion(clientId);
        suggestion.Status = AIJobSuggestionStatus.ACCEPTED;
        dbContext.AIJobSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync();

        Func<Task> act = async () => await service.PatchSuggestionAsync(clientId, suggestion.Id, new Request.PatchSuggestionRequest { SuggestedTitle = "x" });
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task RefineSuggestionAsync_AdvisoryMessageDoesNotMutateSuggestion()
    {
        var dbContext = GetDbContext();
        var service = CreateService(dbContext);
        var clientId = Guid.NewGuid();
        var suggestion = BuildSuggestion(clientId);
        dbContext.AIJobSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync();

        _refinementProviderMock
            .Setup(x => x.RefineSuggestionAsync(It.IsAny<Response.SuggestionResponse>(), It.Is<Request.RefineSuggestionRequest>(r => r.Message == "explain the budget"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIJobRefinementDraft
            {
                Suggestion = AIJobSuggestionDraft.FromResponse(new Response.SuggestionResponse
                {
                    RawInput = suggestion.RawInput,
                    SuggestedTitle = suggestion.SuggestedTitle,
                    SuggestedDescription = suggestion.SuggestedDescription,
                    BudgetType = suggestion.SuggestedBudgetType,
                    Currency = suggestion.Currency,
                    AIModel = suggestion.AIModel,
                    Status = suggestion.Status.ToString()
                }),
                AIResponse = "Advice only",
                ChangedFields = new List<string>()
            });

        var result = await service.RefineSuggestionAsync(clientId, suggestion.Id, new Request.RefineSuggestionRequest { Message = "explain the budget" });

        result.ChangedFields.Should().BeEmpty();
        result.AIResponse.Should().Be("Advice only");
        var saved = await dbContext.AIJobSuggestions.FindAsync(suggestion.Id);
        saved!.SuggestedTitle.Should().Be(suggestion.SuggestedTitle);
    }

    [Fact]
    public async Task RefineSuggestionAsync_UpdatesChangedFields()
    {
        var dbContext = GetDbContext();
        var service = CreateService(dbContext);
        var clientId = Guid.NewGuid();
        var suggestion = BuildSuggestion(clientId);
        dbContext.AIJobSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync();

        var draft = BuildDraft();
        draft.SuggestedBudgetMin = 2000;
        draft.SuggestedBudgetMax = 3000;
        draft.SuggestedTimelineDays = 30;
        draft.ExperienceLevel = SkillLevel.EXPERT;
        draft.SuggestedSkills.Add("Stripe");
        draft.BudgetType = BudgetType.HOURLY;
        draft.Currency = "USD";
        draft.ClarifyingAnswers = new List<string> { "Use Stripe" };

        _refinementProviderMock
            .Setup(x => x.RefineSuggestionAsync(It.IsAny<Response.SuggestionResponse>(), It.Is<Request.RefineSuggestionRequest>(r => r.Message == "update everything"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIJobRefinementDraft
            {
                Suggestion = draft,
                AIResponse = "Updated",
                ChangedFields = new List<string> { "suggestedBudgetMin", "suggestedBudgetMax", "suggestedTimelineDays", "experienceLevel", "suggestedSkills", "budgetType", "currency", "clarifyingAnswers" }
            });

        var result = await service.RefineSuggestionAsync(clientId, suggestion.Id, new Request.RefineSuggestionRequest { Message = "update everything" });

        result.ChangedFields.Should().NotBeEmpty();
        result.Suggestion.SuggestedBudgetMin.Should().Be(2000);
        result.Suggestion.SuggestedTimelineDays.Should().Be(30);
        result.Suggestion.ExperienceLevel.Should().Be(SkillLevel.EXPERT);
        result.Suggestion.SuggestedSkills.Should().Contain("Stripe");
        result.Suggestion.BudgetType.Should().Be(BudgetType.HOURLY);
        result.Suggestion.Currency.Should().Be("USD");
        result.Suggestion.ClarifyingAnswers.Should().Contain("Use Stripe");

        var saved = await dbContext.AIJobSuggestions.FindAsync(suggestion.Id);
        saved!.SuggestedBudgetMin.Should().Be(2000);
        saved.SuggestedTimelineDays.Should().Be(30);
        saved.SuggestedExperienceLevel.Should().Be(SkillLevel.EXPERT);
        saved.SuggestedBudgetType.Should().Be(BudgetType.HOURLY);
        saved.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task RefineSuggestionAsync_RejectsProcessedSuggestion()
    {
        var dbContext = GetDbContext();
        var service = CreateService(dbContext);
        var clientId = Guid.NewGuid();
        var suggestion = BuildSuggestion(clientId);
        suggestion.Status = AIJobSuggestionStatus.REJECTED;
        dbContext.AIJobSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync();

        Func<Task> act = async () => await service.RefineSuggestionAsync(clientId, suggestion.Id, new Request.RefineSuggestionRequest { Message = "change budget" });
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task AcceptSuggestionAsync_RequiresValidCategoryId()
    {
        var dbContext = GetDbContext();
        var service = CreateService(dbContext);
        var clientId = Guid.NewGuid();
        var suggestion = BuildSuggestion(clientId);
        dbContext.AIJobSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync();

        Func<Task> act = async () => await service.AcceptSuggestionAsync(clientId, suggestion.Id, new Request.AcceptSuggestionRequest());
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("CategoryId is required to accept an AI suggestion.");
    }

    [Fact]
    public async Task AcceptSuggestionAsync_MapsStructuredFieldsIntoDraftJobRequest()
    {
        var dbContext = GetDbContext();
        var service = CreateService(dbContext);
        var clientId = Guid.NewGuid();
        var suggestion = BuildSuggestion(clientId);
        dbContext.AIJobSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync();

        Aivora.Services.JobService.Request.CreateJobRequest? capturedRequest = null;
        var jobId = Guid.NewGuid();
        _jobServiceMock
            .Setup(x => x.CreateJobAsync(clientId, It.IsAny<Aivora.Services.JobService.Request.CreateJobRequest>()))
            .Callback<Guid, Aivora.Services.JobService.Request.CreateJobRequest>((_, request) => capturedRequest = request)
            .ReturnsAsync(new Aivora.Services.JobService.Response.JobResponse { Id = jobId, Status = JobStatus.DRAFT });

        var result = await service.AcceptSuggestionAsync(clientId, suggestion.Id, new Request.AcceptSuggestionRequest { CategoryId = Guid.NewGuid() });

        result.Job.Id.Should().Be(jobId);
        capturedRequest.Should().NotBeNull();
        capturedRequest!.BusinessDomain.Should().Be(suggestion.SuggestedBusinessDomain);
        capturedRequest.ExpectedOutcome.Should().Be(suggestion.SuggestedExpectedOutcome);
        capturedRequest.BudgetType.Should().Be(suggestion.SuggestedBudgetType);
        capturedRequest.Currency.Should().Be(suggestion.Currency);
        capturedRequest.ExperienceLevel.Should().Be(suggestion.SuggestedExperienceLevel);
        capturedRequest.Visibility.Should().Be(JobVisibility.PRIVATE);
        (await dbContext.AIJobSuggestions.FindAsync(suggestion.Id))!.Status.Should().Be(AIJobSuggestionStatus.ACCEPTED);
    }

    [Fact]
    public async Task RejectSuggestionAsync_StoresTrimmedReason()
    {
        var dbContext = GetDbContext();
        var service = CreateService(dbContext);
        var clientId = Guid.NewGuid();
        var suggestion = BuildSuggestion(clientId);
        dbContext.AIJobSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync();

        var result = await service.RejectSuggestionAsync(clientId, suggestion.Id, new Request.RejectSuggestionRequest { Reason = "  too expensive  " });

        result.Status.Should().Be(AIJobSuggestionStatus.REJECTED.ToString());
        result.RejectionReason.Should().Be("too expensive");
        (await dbContext.AIJobSuggestions.FindAsync(suggestion.Id))!.RejectionReason.Should().Be("too expensive");
    }

    [Theory]
    [InlineData("")]
    [InlineData("no")]
    public async Task RejectSuggestionAsync_RequiresReasonBetween3And500Characters(string reason)
    {
        var dbContext = GetDbContext();
        var service = CreateService(dbContext);
        var clientId = Guid.NewGuid();
        var suggestion = BuildSuggestion(clientId);
        dbContext.AIJobSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync();

        Func<Task> act = async () => await service.RejectSuggestionAsync(clientId, suggestion.Id, new Request.RejectSuggestionRequest { Reason = reason });
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task RejectSuggestionAsync_BlocksAcceptedSuggestion()
    {
        var dbContext = GetDbContext();
        var service = CreateService(dbContext);
        var clientId = Guid.NewGuid();
        var suggestion = BuildSuggestion(clientId);
        suggestion.Status = AIJobSuggestionStatus.ACCEPTED;
        dbContext.AIJobSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync();

        Func<Task> act = async () => await service.RejectSuggestionAsync(clientId, suggestion.Id, new Request.RejectSuggestionRequest { Reason = "bad fit" });
        await act.Should().ThrowAsync<ValidationException>();
    }

    private static AIJobSuggestion BuildSuggestion(Guid clientId)
    {
        return new AIJobSuggestion
        {
            ClientId = clientId,
            RawInput = "Build an ecommerce chatbot.",
            SuggestedTitle = "AI Ecommerce Chatbot",
            SuggestedDescription = "Build chatbot",
            SuggestedBusinessDomain = "Ecommerce",
            SuggestedExpectedOutcome = "Increase conversion",
            SuggestedBudgetType = BudgetType.FIXED,
            Currency = "USD",
            SuggestedBudgetMin = 1000,
            SuggestedBudgetMax = 2000,
            SuggestedTimelineDays = 14,
            SuggestedExperienceLevel = SkillLevel.INTERMEDIATE,
            SuggestedSkillsJson = "[\"React\",\"AI\"]",
            SuggestedMilestonesJson = "[{\"title\":\"Build\",\"amount\":1000,\"dueDays\":14}]",
            ClarifyingQuestionsJson = "[\"Which platform?\"]",
            ClarifyingAnswersJson = "[\"\"]",
            RiskWarningsJson = "[\"Scope may grow\"]",
            AIModel = "TestModel",
            Status = AIJobSuggestionStatus.GENERATED
        };
    }

    private static AIJobSuggestionDraft BuildDraft()
    {
        return new AIJobSuggestionDraft
        {
            SuggestedTitle = "Provider title",
            SuggestedDescription = "Provider description",
            BusinessDomain = "Ecommerce",
            ExpectedOutcome = "Increase conversion",
            BudgetType = BudgetType.FIXED,
            Currency = "USD",
            SuggestedBudgetMin = 1000,
            SuggestedBudgetMax = 2000,
            SuggestedTimelineDays = 14,
            ExperienceLevel = SkillLevel.INTERMEDIATE,
            SuggestedSkills = new List<string> { "React", "AI" },
            SuggestedMilestones = new List<Response.SuggestedMilestone>
            {
                new() { Title = "Build", Description = "Build core", Amount = 1000, DueDays = 14 }
            },
            ClarifyingQuestions = new List<string> { "Which platform?" },
            ClarifyingAnswers = new List<string> { string.Empty },
            RiskWarnings = new List<string> { "Scope may grow" },
            AIModel = "ProviderModel"
        };
    }
}
