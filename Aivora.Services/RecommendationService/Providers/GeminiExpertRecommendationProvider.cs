using Aivora.Services.AIJobAssistantService.Providers;
using Aivora.Services.Options;
using Aivora.Services.RecommendationService.Parsing;
using Aivora.Services.RecommendationService.Prompting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aivora.Services.RecommendationService.Providers;

public class GeminiExpertRecommendationProvider : GeminiProviderBase, IExpertRecommendationProvider
{
    private readonly ExpertRecommendationPromptBuilder _promptBuilder;
    private readonly ExpertRecommendationParser _parser;
    private readonly MockExpertRecommendationProvider _fallbackProvider;
    private readonly ILogger<GeminiExpertRecommendationProvider> _logger;

    public GeminiExpertRecommendationProvider(
        GeminiProviderClient client,
        IOptions<AIProviderOptions> options,
        ExpertRecommendationPromptBuilder promptBuilder,
        ExpertRecommendationParser parser,
        MockExpertRecommendationProvider fallbackProvider,
        ILogger<GeminiExpertRecommendationProvider> logger)
        : base(client, options.Value, logger)
    {
        _promptBuilder = promptBuilder;
        _parser = parser;
        _fallbackProvider = fallbackProvider;
        _logger = logger;
    }

    public Task<ExpertRecommendationDraft> RankAsync(ExpertRecommendationContext context, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            buildPrompt: () => _promptBuilder.Build(context),
            parse: providerText => _parser.Parse(providerText, context, _logger),
            mockFallback: ct => _fallbackProvider.RankAsync(context, ct),
            logNoun: "expert recommendation",
            errorNoun: "expert recommendation",
            cancellationToken,
            temperature: 0);
    }
}
