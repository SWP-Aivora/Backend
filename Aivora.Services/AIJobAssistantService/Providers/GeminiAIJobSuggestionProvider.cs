using Aivora.Services.AIJobAssistantService.Parsing;
using Aivora.Services.AIJobAssistantService.Prompting;
using Aivora.Services.Exceptions;
using Aivora.Services.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aivora.Services.AIJobAssistantService.Providers;

public class GeminiAIJobSuggestionProvider : IAIJobSuggestionProvider
{
    private readonly GeminiProviderClient _client;
    private readonly AIProviderOptions _options;
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
    {
        _client = client;
        _options = options.Value;
        _promptBuilder = promptBuilder;
        _parser = parser;
        _fallbackProvider = fallbackProvider;
        _logger = logger;
    }

    public async Task<AIJobSuggestionDraft> GenerateSuggestionAsync(Request.GenerateSuggestionRequest request, CancellationToken cancellationToken = default)
    {
        if (!_client.HasApiKey && _options.EnableFallback)
        {
            _logger.LogWarning("Gemini API key is missing; using mock AI job suggestion provider fallback.");
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
            _logger.LogWarning(ex, "Gemini job suggestion provider failed; using mock fallback.");
            return await _fallbackProvider.GenerateSuggestionAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini job suggestion provider failed and fallback is disabled.");
            throw new ValidationException("AI suggestion provider failed. Please try again later.");
        }
    }
}
