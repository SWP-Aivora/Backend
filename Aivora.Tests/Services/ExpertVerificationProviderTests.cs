using System.Net;
using System.Text;
using System.Text.Json;
using Aivora.Repositories.Enums;
using Aivora.Services.AIJobAssistantService.Providers;
using Aivora.Services.Exceptions;
using Aivora.Services.ExpertVerificationService.Parsing;
using Aivora.Services.ExpertVerificationService.Prompting;
using Aivora.Services.ExpertVerificationService.Providers;
using Aivora.Services.Options;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aivora.Tests.Services;

public class ExpertVerificationProviderTests
{
    [Fact]
    public async Task GeminiProviderClient_MultimodalOverload_IncludesTextAndInlineDataParts()
    {
        var handler = new RecordingHandler(GeminiResponse("""{"ok":true}"""));
        var client = BuildClient(handler);
        var attachment = ("image/png", Encoding.UTF8.GetBytes("fake-bytes"));

        await client.GenerateAsync("Analyze this.", new[] { attachment });

        handler.LastRequestBody.Should().NotBeNull();
        using var document = JsonDocument.Parse(handler.LastRequestBody!);
        var parts = document.RootElement.GetProperty("contents")[0].GetProperty("parts");

        parts.GetArrayLength().Should().Be(2);
        parts[0].GetProperty("text").GetString().Should().Be("Analyze this.");
        parts[1].GetProperty("inline_data").GetProperty("mime_type").GetString().Should().Be("image/png");
        parts[1].GetProperty("inline_data").GetProperty("data").GetString()
            .Should().Be(Convert.ToBase64String(attachment.Item2));
    }

    [Fact]
    public async Task GeminiAIExpertVerificationProvider_ParsesValidJsonWithoutRealNetwork()
    {
        var provider = BuildProvider(GeminiResponse(
            """
            {
              "outcome": "APPROVED",
              "confidenceScore": 92,
              "reasoning": "Name and skill match."
            }
            """));

        var result = await provider.AnalyzeEvidenceAsync(BuildRequest());

        result.Outcome.Should().Be(ExpertVerificationStatus.APPROVED);
        result.ConfidenceScore.Should().Be(92);
        result.Reasoning.Should().Be("Name and skill match.");
    }

    [Fact]
    public async Task GeminiAIExpertVerificationProvider_RetriesThenThrowsServiceUnavailable_WhenAlwaysFailing()
    {
        var handler = new AlwaysFailingHandler();
        var provider = BuildProvider(handler);

        Func<Task> act = () => provider.AnalyzeEvidenceAsync(BuildRequest());

        await act.Should().ThrowAsync<ServiceUnavailableException>();
        handler.CallCount.Should().Be(3);
    }

    [Fact]
    public async Task GeminiAIExpertVerificationProvider_SucceedsAfterTransientFailure()
    {
        var handler = new FailThenSucceedHandler(
            failCount: 1,
            successResponse: GeminiResponse(
                """
                {
                  "outcome": "NEEDS_REVIEW",
                  "confidenceScore": 40,
                  "reasoning": "Image is blurry."
                }
                """));

        var provider = BuildProvider(handler);

        var result = await provider.AnalyzeEvidenceAsync(BuildRequest());

        result.Outcome.Should().Be(ExpertVerificationStatus.NEEDS_REVIEW);
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task GeminiAIExpertVerificationProvider_PropagatesCancellation()
    {
        var provider = BuildProvider(new CancelingHandler());

        Func<Task> act = () => provider.AnalyzeEvidenceAsync(BuildRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static AnalyzeEvidenceRequest BuildRequest()
    {
        return new AnalyzeEvidenceRequest
        {
            FileBytes = Encoding.UTF8.GetBytes("fake-evidence"),
            MimeType = "image/png",
            ExpertFullName = "Nguyen Van A",
            ClaimedSkillName = "React"
        };
    }

    private static GeminiAIExpertVerificationProvider BuildProvider(HttpResponseMessage response)
    {
        return BuildProvider(new RecordingHandler(response));
    }

    private static GeminiAIExpertVerificationProvider BuildProvider(HttpMessageHandler handler)
    {
        return new GeminiAIExpertVerificationProvider(
            BuildClient(handler),
            new AIExpertVerificationPromptBuilder(),
            new AIExpertVerificationParser(),
            NullLogger<GeminiAIExpertVerificationProvider>.Instance);
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

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public string? LastRequestBody { get; private set; }

        public RecordingHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return _response;
        }
    }

    private sealed class AlwaysFailingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("failed") });
        }
    }

    private sealed class FailThenSucceedHandler : HttpMessageHandler
    {
        private readonly int _failCount;
        private readonly HttpResponseMessage _successResponse;
        public int CallCount { get; private set; }

        public FailThenSucceedHandler(int failCount, HttpResponseMessage successResponse)
        {
            _failCount = failCount;
            _successResponse = successResponse;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            if (CallCount <= _failCount)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("busy") });
            }

            return Task.FromResult(_successResponse);
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
