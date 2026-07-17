using Aivora.Services.AIJobAssistantService.Parsing;
using Aivora.Services.AIJobAssistantService.Prompting;
using Aivora.Services.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aivora.Services.AIJobAssistantService.Providers;

public class GeminiAIServiceDescriptionProvider : GeminiProviderBase, IAIServiceDescriptionProvider
{
    private readonly AIServiceDescriptionPromptBuilder _promptBuilder;
    private readonly AIServiceDescriptionParser _parser;
    private readonly MockAIServiceDescriptionProvider _fallbackProvider;

    public GeminiAIServiceDescriptionProvider(
        GeminiProviderClient client,
        IOptions<AIProviderOptions> options,
        AIServiceDescriptionPromptBuilder promptBuilder,
        AIServiceDescriptionParser parser,
        MockAIServiceDescriptionProvider fallbackProvider,
        ILogger<GeminiAIServiceDescriptionProvider> logger)
        : base(client, options.Value, logger)
    {
        _promptBuilder = promptBuilder;
        _parser = parser;
        _fallbackProvider = fallbackProvider;
    }

    public Task<AIServiceDescriptionDraft> GenerateServiceDescriptionAsync(Request.GenerateServiceDescriptionRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            buildPrompt: () => _promptBuilder.Build(request),
            parse: providerText => _parser.Parse(providerText, request),
            mockFallback: ct => _fallbackProvider.GenerateServiceDescriptionAsync(request, ct),
            logNoun: "service description",
            errorNoun: "service description",
            cancellationToken);
    }
}
