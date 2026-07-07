using Aivora.Services.AIJobAssistantService.Providers;
using Aivora.Services.AIMilestoneStepAssistantService.Parsing;
using Aivora.Services.AIMilestoneStepAssistantService.Prompting;
using Aivora.Services.Exceptions;
using Aivora.Services.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aivora.Services.AIMilestoneStepAssistantService.Providers;

public class GeminiAIMilestoneStepSuggestionProvider : IAIMilestoneStepSuggestionProvider
{
    private readonly GeminiProviderClient _client;
    private readonly AIProviderOptions _options;
    private readonly AIMilestoneStepSuggestionPromptBuilder _promptBuilder;
    private readonly AIMilestoneStepSuggestionParser _parser;
    private readonly MockAIMilestoneStepSuggestionProvider _fallbackProvider;
    private readonly ILogger<GeminiAIMilestoneStepSuggestionProvider> _logger;

    public GeminiAIMilestoneStepSuggestionProvider(
        GeminiProviderClient client,
        IOptions<AIProviderOptions> options,
        AIMilestoneStepSuggestionPromptBuilder promptBuilder,
        AIMilestoneStepSuggestionParser parser,
        MockAIMilestoneStepSuggestionProvider fallbackProvider,
        ILogger<GeminiAIMilestoneStepSuggestionProvider> logger)
    {
        _client = client;
        _options = options.Value;
        _promptBuilder = promptBuilder;
        _parser = parser;
        _fallbackProvider = fallbackProvider;
        _logger = logger;
    }

    public async Task<AIMilestoneStepSuggestionDraft> GenerateSuggestionAsync(Request.SuggestMilestoneStepsRequest request, CancellationToken cancellationToken = default)
    {
        if (!_client.HasApiKey && _options.EnableFallback)
        {
            _logger.LogWarning("Gemini API key is missing; using mock AI milestone step suggestion provider fallback.");
            return await _fallbackProvider.GenerateSuggestionAsync(request, cancellationToken);
        }

        try
        {
            var providerText = await _client.GenerateAsync(_promptBuilder.Build(request), cancellationToken);
            return _parser.Parse(providerText, request);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (_options.EnableFallback)
        {
            _logger.LogWarning(ex, "Gemini milestone step suggestion provider failed; using mock fallback.");
            return await _fallbackProvider.GenerateSuggestionAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini milestone step suggestion provider failed and fallback is disabled.");
            throw new ValidationException("AI suggestion provider failed. Please try again later.");
        }
    }
}
