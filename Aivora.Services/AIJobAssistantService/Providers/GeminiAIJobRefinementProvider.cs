using Aivora.Services.AIJobAssistantService.Parsing;
using Aivora.Services.AIJobAssistantService.Prompting;
using Aivora.Services.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aivora.Services.AIJobAssistantService.Providers;

public class GeminiAIJobRefinementProvider : GeminiProviderBase, IAIJobRefinementProvider
{
    private readonly AIJobRefinementPromptBuilder _promptBuilder;
    private readonly AIJobRefinementParser _parser;
    private readonly MockAIJobRefinementProvider _fallbackProvider;
    private readonly ILogger<GeminiAIJobRefinementProvider> _logger;

    public GeminiAIJobRefinementProvider(
        GeminiProviderClient client,
        IOptions<AIProviderOptions> options,
        AIJobRefinementPromptBuilder promptBuilder,
        AIJobRefinementParser parser,
        MockAIJobRefinementProvider fallbackProvider,
        ILogger<GeminiAIJobRefinementProvider> logger)
        : base(client, options.Value, logger)
    {
        _promptBuilder = promptBuilder;
        _parser = parser;
        _fallbackProvider = fallbackProvider;
        _logger = logger;
    }

    public Task<AIJobRefinementDraft> RefineSuggestionAsync(Response.SuggestionResponse current, Request.RefineSuggestionRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            buildPrompt: () => _promptBuilder.Build(current, request),
            parse: providerText => _parser.Parse(providerText, current, _logger),
            mockFallback: ct => _fallbackProvider.RefineSuggestionAsync(current, request, ct),
            logNoun: "job refinement",
            errorNoun: "refinement",
            cancellationToken);
    }
}
