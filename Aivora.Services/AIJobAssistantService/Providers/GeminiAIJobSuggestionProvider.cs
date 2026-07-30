using Aivora.Services.AIJobAssistantService.Parsing;
using Aivora.Services.AIJobAssistantService.Prompting;
using Aivora.Services.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aivora.Services.AIJobAssistantService.Providers;

public class GeminiAIJobSuggestionProvider : GeminiProviderBase, IAIJobSuggestionProvider
{
    private readonly AIJobSuggestionPromptBuilder _promptBuilder;
    private readonly AIJobSuggestionParser _parser;
    private readonly MockAIJobSuggestionProvider _fallbackProvider;
    private readonly ILogger<GeminiAIJobSuggestionProvider> _logger;

    public GeminiAIJobSuggestionProvider(
        GeminiProviderClient client,
        IOptions<AIProviderOptions> options,
        AIJobSuggestionPromptBuilder promptBuilder,
        AIJobSuggestionParser parser,
        MockAIJobSuggestionProvider fallbackProvider,
        ILogger<GeminiAIJobSuggestionProvider> logger)
        : base(client, options.Value, logger)
    {
        _promptBuilder = promptBuilder;
        _parser = parser;
        _fallbackProvider = fallbackProvider;
        _logger = logger;
    }

    public Task<AIJobSuggestionDraft> GenerateSuggestionAsync(Request.GenerateSuggestionRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            buildPrompt: () => _promptBuilder.Build(request),
            parse: providerText => _parser.Parse(providerText, request, _logger),
            mockFallback: ct => _fallbackProvider.GenerateSuggestionAsync(request, ct),
            logNoun: "job suggestion",
            errorNoun: "suggestion",
            cancellationToken,
            responseSchema: AIJobSuggestionPromptBuilder.ResponseSchema);
    }
}
