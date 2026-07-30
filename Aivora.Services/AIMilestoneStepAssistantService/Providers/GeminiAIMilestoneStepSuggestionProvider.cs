using Aivora.Services.AIJobAssistantService.Providers;
using Aivora.Services.AIMilestoneStepAssistantService.Parsing;
using Aivora.Services.AIMilestoneStepAssistantService.Prompting;
using Aivora.Services.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aivora.Services.AIMilestoneStepAssistantService.Providers;

public class GeminiAIMilestoneStepSuggestionProvider : GeminiProviderBase, IAIMilestoneStepSuggestionProvider
{
    private readonly AIMilestoneStepSuggestionPromptBuilder _promptBuilder;
    private readonly AIMilestoneStepSuggestionParser _parser;
    private readonly MockAIMilestoneStepSuggestionProvider _fallbackProvider;

    public GeminiAIMilestoneStepSuggestionProvider(
        GeminiProviderClient client,
        IOptions<AIProviderOptions> options,
        AIMilestoneStepSuggestionPromptBuilder promptBuilder,
        AIMilestoneStepSuggestionParser parser,
        MockAIMilestoneStepSuggestionProvider fallbackProvider,
        ILogger<GeminiAIMilestoneStepSuggestionProvider> logger)
        : base(client, options.Value, logger)
    {
        _promptBuilder = promptBuilder;
        _parser = parser;
        _fallbackProvider = fallbackProvider;
    }

    public Task<AIMilestoneStepSuggestionDraft> GenerateSuggestionAsync(Request.SuggestMilestoneStepsRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            buildPrompt: () => _promptBuilder.Build(request),
            parse: providerText => _parser.Parse(providerText, request),
            mockFallback: ct => _fallbackProvider.GenerateSuggestionAsync(request, ct),
            logNoun: "milestone step suggestion",
            errorNoun: "suggestion",
            cancellationToken,
            responseSchema: AIMilestoneStepSuggestionPromptBuilder.ResponseSchema);
    }
}
