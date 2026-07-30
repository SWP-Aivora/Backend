using Aivora.Services.AIJobAssistantService.Providers;
using Aivora.Services.AIJobRefinementService.Parsing;
using Aivora.Services.AIJobRefinementService.Prompting;
using Aivora.Services.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aivora.Services.AIJobRefinementService.Providers;

public class GeminiAIJobRefinementProvider : GeminiProviderBase, IAIJobRefinementProvider
{
    private readonly AIJobRefinementPromptBuilder _promptBuilder;
    private readonly AIJobRefinementParser _parser;
    private readonly MockAIJobRefinementProvider _fallbackProvider;

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
    }

    public Task<AIJobRefinementDraft> RefineJobAsync(JobService.Response.JobResponse current, string message, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            buildPrompt: () => _promptBuilder.Build(current, message),
            parse: providerText => _parser.Parse(providerText, current),
            mockFallback: ct => _fallbackProvider.RefineJobAsync(current, message, ct),
            logNoun: "job refinement",
            errorNoun: "refinement",
            cancellationToken,
            responseSchema: AIJobRefinementPromptBuilder.ResponseSchema);
    }
}
