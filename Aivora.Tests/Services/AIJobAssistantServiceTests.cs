using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.AIJobAssistantService;
using Aivora.Services.Exceptions;
using Aivora.Services.Options;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class AIJobAssistantServiceTests
{
    private readonly Mock<Aivora.Services.JobService.IService> _jobServiceMock = new();
    private readonly Mock<IAIJobSuggestionProvider> _suggestionProviderMock = new();
    private readonly Mock<IAIJobRefinementProvider> _refinementProviderMock = new();
    private readonly Mock<IAIServiceDescriptionProvider> _serviceDescriptionProviderMock = new();
    private readonly Mock<Aivora.Services.CategoryService.IService> _categoryServiceMock = new();

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
        _categoryServiceMock.Setup(c => c.GetCachedCategoryDictionaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        return new Service(
            dbContext,
            _jobServiceMock.Object,
            _suggestionProviderMock.Object,
            _refinementProviderMock.Object,
            _serviceDescriptionProviderMock.Object,
            _categoryServiceMock.Object,
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<Service>(),
            Options.Create(new ExchangeRateOptions()));
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
        // draft.Currency is "USD" (see BuildDraft) — the service must convert it to AICOIN,
        // not pass it through, so this asserts the converted value, not draft.Currency.
        result.Currency.Should().Be("AICOIN");
        result.SuggestedBudgetMin.Should().Be(draft.SuggestedBudgetMin * 25);
        result.SuggestedBudgetMax.Should().Be(draft.SuggestedBudgetMax * 25);
        result.ExperienceLevel.Should().Be(draft.ExperienceLevel);
        result.ClarifyingAnswers.Should().HaveCount(draft.ClarifyingQuestions.Count);
        result.AIModel.Should().Be(draft.AIModel);

        var saved = await dbContext.AIJobSuggestions.FindAsync(result.Id);
        saved.Should().NotBeNull();
        saved!.SuggestedBudgetType.Should().Be(draft.BudgetType);
        saved.Currency.Should().Be("AICOIN");
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
        // A refined suggestion is always stored in AICOIN (both GenerateSuggestionAsync and
        // RefineSuggestionAsync convert unconditionally) — use a realistic starting currency so
        // the diff isn't tripped up by an unconverted fixture value.
        suggestion.Currency = "AICOIN";
        dbContext.AIJobSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync();

        var current = await service.GetSuggestionAsync(clientId, suggestion.Id);

        _refinementProviderMock
            .Setup(x => x.RefineSuggestionAsync(It.IsAny<Response.SuggestionResponse>(), It.Is<Request.RefineSuggestionRequest>(r => r.Message == "explain the budget"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIJobRefinementDraft
            {
                // Echo the current suggestion back unchanged, exactly like the real Mock/Gemini
                // providers do for an advisory-only message.
                Suggestion = AIJobSuggestionDraft.FromResponse(current),
                AIResponse = "Advice only"
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
                AIResponse = "Updated"
            });

        var result = await service.RefineSuggestionAsync(clientId, suggestion.Id, new Request.RefineSuggestionRequest { Message = "update everything" });

        result.ChangedFields.Should().NotBeEmpty();
        // draft.Currency is "USD" — the service must convert 2000 USD to AICOIN, not pass it through.
        result.Suggestion.SuggestedBudgetMin.Should().Be(2000 * 25);
        result.Suggestion.SuggestedTimelineDays.Should().Be(30);
        result.Suggestion.ExperienceLevel.Should().Be(SkillLevel.EXPERT);
        result.Suggestion.SuggestedSkills.Should().Contain("Stripe");
        result.Suggestion.BudgetType.Should().Be(BudgetType.HOURLY);
        result.Suggestion.Currency.Should().Be("AICOIN");
        result.Suggestion.ClarifyingAnswers.Should().Contain("Use Stripe");

        var saved = await dbContext.AIJobSuggestions.FindAsync(suggestion.Id);
        saved!.SuggestedBudgetMin.Should().Be(2000 * 25);
        saved.SuggestedTimelineDays.Should().Be(30);
        saved.SuggestedExperienceLevel.Should().Be(SkillLevel.EXPERT);
        saved.SuggestedBudgetType.Should().Be(BudgetType.HOURLY);
        saved.Currency.Should().Be("AICOIN");
    }

    [Fact]
    public async Task RefineSuggestionAsync_WithHallucinatedCurrency_DoesNotThrowAndKeepsCurrentCurrency()
    {
        var dbContext = GetDbContext();
        var service = CreateService(dbContext);
        var clientId = Guid.NewGuid();
        var suggestion = BuildSuggestion(clientId);
        suggestion.Currency = "AICOIN";
        dbContext.AIJobSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync();

        var current = await service.GetSuggestionAsync(clientId, suggestion.Id);
        // "EUR" is outside the configured rate table (AICOIN/USD/VND) — an AI hallucination,
        // not something the client asked to convert.
        var draft = AIJobSuggestionDraft.FromResponse(current);
        draft.Currency = "EUR";

        _refinementProviderMock
            .Setup(x => x.RefineSuggestionAsync(It.IsAny<Response.SuggestionResponse>(), It.Is<Request.RefineSuggestionRequest>(r => r.Message == "what currency should I use?"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIJobRefinementDraft { Suggestion = draft, AIResponse = "Advice only." });

        Func<Task> act = async () => await service.RefineSuggestionAsync(clientId, suggestion.Id, new Request.RefineSuggestionRequest { Message = "what currency should I use?" });

        await act.Should().NotThrowAsync();
        var saved = await dbContext.AIJobSuggestions.FindAsync(suggestion.Id);
        saved!.Currency.Should().Be("AICOIN");
    }

    [Fact]
    public async Task RefineSuggestionAsync_WithEmptySkillsList_DoesNotClearExistingSkills()
    {
        var dbContext = GetDbContext();
        var service = CreateService(dbContext);
        var clientId = Guid.NewGuid();
        var suggestion = BuildSuggestion(clientId);
        dbContext.AIJobSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync();

        // The AI didn't mention skills at all — an explicitly empty list must mean "not
        // mentioned", not "clear every skill".
        var draft = BuildDraft();
        draft.SuggestedTitle = "Renamed title";
        draft.SuggestedSkills = new List<string>();

        _refinementProviderMock
            .Setup(x => x.RefineSuggestionAsync(It.IsAny<Response.SuggestionResponse>(), It.Is<Request.RefineSuggestionRequest>(r => r.Message == "rename it"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIJobRefinementDraft { Suggestion = draft, AIResponse = "Renamed." });

        var result = await service.RefineSuggestionAsync(clientId, suggestion.Id, new Request.RefineSuggestionRequest { Message = "rename it" });

        result.ChangedFields.Should().NotContain("suggestedSkills");
        result.Suggestion.SuggestedSkills.Should().BeEquivalentTo(new[] { "React", "AI" });
    }

    [Fact]
    public async Task RefineSuggestionAsync_WithNewCategoryName_UpdatesCategoryId()
    {
        var dbContext = GetDbContext();
        var service = CreateService(dbContext);
        var clientId = Guid.NewGuid();
        var suggestion = BuildSuggestion(clientId);
        dbContext.AIJobSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync();

        var categoryId = Guid.NewGuid();
        _categoryServiceMock.Setup(c => c.GetCachedCategoryDictionaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [categoryId] = "Web Development" });

        var draft = BuildDraft();
        draft.CategoryName = "Web Development";

        _refinementProviderMock
            .Setup(x => x.RefineSuggestionAsync(It.IsAny<Response.SuggestionResponse>(), It.Is<Request.RefineSuggestionRequest>(r => r.Message == "set category to web dev"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIJobRefinementDraft { Suggestion = draft, AIResponse = "Category set." });

        var result = await service.RefineSuggestionAsync(clientId, suggestion.Id, new Request.RefineSuggestionRequest { Message = "set category to web dev" });

        result.ChangedFields.Should().Contain("categoryName");
        result.Suggestion.CategoryId.Should().Be(categoryId);
        var saved = await dbContext.AIJobSuggestions.FindAsync(suggestion.Id);
        saved!.SuggestedCategoryId.Should().Be(categoryId);
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
