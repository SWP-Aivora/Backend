using System.Net;
using System.Text;
using System.Text.Json;
using Aivora.Repositories.Enums;
using Aivora.Services.AIJobAssistantService;
using Aivora.Services.AIJobAssistantService.Parsing;
using Aivora.Services.AIJobAssistantService.Prompting;
using Aivora.Services.AIJobAssistantService.Providers;
using Aivora.Services.Exceptions;
using Aivora.Services.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aivora.Tests.Services;

public class AIJobAssistantProviderTests
{
    [Fact]
    public void SuggestionParser_DefaultsMissingCurrencyToAicoin()
    {
        var parser = new AIJobSuggestionParser();
        var draft = parser.Parse(
            """
            {
              "suggestedTitle": "Build chatbot",
              "suggestedDescription": "Build chatbot",
              "suggestedSkills": ["AI"]
            }
            """,
            new Request.GenerateSuggestionRequest { RawInput = "Build an AI chatbot for ecommerce." });

        draft.Currency.Should().Be("AICOIN");
        draft.BudgetType.Should().Be(BudgetType.FIXED);
        draft.SuggestedSkills.Should().Contain("AI");
    }

    [Fact]
    public void SuggestionParser_StripsMarkdownJsonFence()
    {
        var parser = new AIJobSuggestionParser();
        var draft = parser.Parse(
            """
            ```json
            {
              "suggestedTitle": "Fenced title",
              "suggestedDescription": "Fenced description"
            }
            ```
            """,
            new Request.GenerateSuggestionRequest { RawInput = "Build a web application with authentication." });

        draft.SuggestedTitle.Should().Be("Fenced title");
    }

    [Fact]
    public void ServiceDescriptionParser_ReturnsThreePackages()
    {
        var parser = new AIServiceDescriptionParser();
        var draft = parser.Parse(
            """
            {
              "suggestedTitle": "Service",
              "suggestedDescription": "Description",
              "packages": [
                { "name": "Starter", "description": "Starter tier", "price": 100, "deliveryDays": 2, "features": ["Setup"] }
              ],
              "faqs": []
            }
            """,
            BuildServiceRequest());

        draft.Packages.Should().HaveCount(3);
        draft.Packages.Select(x => x.Name).Should().Equal("Basic", "Standard", "Premium");
    }

    [Fact]
    public void RefinementParser_ReturnsChangedFields()
    {
        var parser = new AIJobRefinementParser();
        var result = parser.Parse(
            """
            {
              "updatedSuggestion": {
                "suggestedTitle": "Updated",
                "suggestedDescription": "Updated description",
                "suggestedBudgetMin": 200,
                "suggestedBudgetMax": 300
              },
              "aiResponse": "Updated budget",
              "changedFields": ["suggestedBudgetMin", "suggestedBudgetMax"]
            }
            """,
            BuildCurrentSuggestion());

        result.ChangedFields.Should().Equal("suggestedBudgetMin", "suggestedBudgetMax");
        result.Suggestion.SuggestedBudgetMin.Should().Be(200);
    }

    [Fact]
    public async Task GeminiSuggestionProvider_ParsesValidJsonWithoutRealNetwork()
    {
        var provider = new GeminiAIJobSuggestionProvider(
            BuildClient(GeminiResponse(
                """
                {
                  "suggestedTitle": "Gemini title",
                  "suggestedDescription": "Gemini description",
                  "currency": "USD"
                }
                """)),
            Options.Create(new AIProviderOptions { Provider = "Gemini", ApiKey = "test-key", EnableFallback = false }),
            new AIJobSuggestionPromptBuilder(),
            new AIJobSuggestionParser(),
            new MockAIJobSuggestionProvider());

        var draft = await provider.GenerateSuggestionAsync(new Request.GenerateSuggestionRequest { RawInput = "Build a chatbot for ecommerce." });

        draft.SuggestedTitle.Should().Be("Gemini title");
        draft.Currency.Should().Be("USD");
        draft.AIModel.Should().Be("Gemini 2.5 Flash");
    }

    [Fact]
    public async Task GeminiRefinementProvider_FallsBackWhenConfiguredAndHttpFails()
    {
        var provider = new GeminiAIJobRefinementProvider(
            BuildClient(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("failed") }),
            Options.Create(new AIProviderOptions { Provider = "Gemini", ApiKey = "test-key", EnableFallback = true }),
            new AIJobRefinementPromptBuilder(),
            new AIJobRefinementParser(),
            new MockAIJobRefinementProvider());

        var result = await provider.RefineSuggestionAsync(BuildCurrentSuggestion(), "budget 100 200");

        result.ChangedFields.Should().Contain("suggestedBudgetMin");
        result.Suggestion.SuggestedBudgetMin.Should().Be(100);
    }

    [Fact]
    public async Task GeminiServiceDescriptionProvider_ThrowsWhenFallbackDisabledAndHttpFails()
    {
        var provider = new GeminiAIServiceDescriptionProvider(
            BuildClient(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("failed") }),
            Options.Create(new AIProviderOptions { Provider = "Gemini", ApiKey = "test-key", EnableFallback = false }),
            new AIServiceDescriptionPromptBuilder(),
            new AIServiceDescriptionParser(),
            new MockAIServiceDescriptionProvider());

        Func<Task> act = async () => await provider.GenerateServiceDescriptionAsync(BuildServiceRequest());

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GeminiSuggestionProvider_PropagatesCancellationWhenFallbackIsEnabled()
    {
        var provider = new GeminiAIJobSuggestionProvider(
            BuildClient(new CancelingHandler()),
            Options.Create(new AIProviderOptions { Provider = "Gemini", ApiKey = "test-key", EnableFallback = true }),
            new AIJobSuggestionPromptBuilder(),
            new AIJobSuggestionParser(),
            new MockAIJobSuggestionProvider());

        Func<Task> act = async () => await provider.GenerateSuggestionAsync(
            new Request.GenerateSuggestionRequest { RawInput = "Build a chatbot for ecommerce." },
            CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static GeminiProviderClient BuildClient(HttpResponseMessage response)
    {
        return BuildClient(new FakeHandler(response));
    }

    private static GeminiProviderClient BuildClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        return new GeminiProviderClient(httpClient, Options.Create(new AIProviderOptions { Provider = "Gemini", ApiKey = "test-key" }));
    }

    private static HttpResponseMessage GeminiResponse(string text)
    {
        var body = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[] { new { text } }
                    }
                }
            }
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private static Response.SuggestionResponse BuildCurrentSuggestion()
    {
        return new Response.SuggestionResponse
        {
            Id = Guid.NewGuid(),
            ClientId = Guid.NewGuid(),
            RawInput = "Build a chatbot",
            SuggestedTitle = "Current",
            SuggestedDescription = "Current description",
            BudgetType = BudgetType.FIXED,
            Currency = "AICOIN",
            SuggestedBudgetMin = 500,
            SuggestedBudgetMax = 1000,
            SuggestedTimelineDays = 14,
            ExperienceLevel = SkillLevel.INTERMEDIATE,
            ClarifyingQuestions = new List<string> { "Which platform?" },
            ClarifyingAnswers = new List<string> { string.Empty },
            AIModel = "Test",
            Status = "GENERATED",
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static Request.GenerateServiceDescriptionRequest BuildServiceRequest()
    {
        return new Request.GenerateServiceDescriptionRequest
        {
            RawInput = "I build production-ready AI automation services for ecommerce teams.",
            Skills = new List<string> { "AI", "Automation" },
            PriceFrom = 100,
            DeliveryDays = 7
        };
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public FakeHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }

    private sealed class CancelingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }
}
