using Aivora.Services.AIJobAssistantService.Parsing;
using Aivora.Services.AIJobAssistantService.Prompting;
using Aivora.Services.Exceptions;
using Aivora.Services.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aivora.Services.AIJobAssistantService.Providers;

public class GeminiAIServiceDescriptionProvider : IAIServiceDescriptionProvider
{
    private readonly GeminiProviderClient _client;
    private readonly AIProviderOptions _options;
    private readonly AIServiceDescriptionPromptBuilder _promptBuilder;
    private readonly AIServiceDescriptionParser _parser;
    private readonly MockAIServiceDescriptionProvider _fallbackProvider;
    private readonly ILogger<GeminiAIServiceDescriptionProvider> _logger;

    public GeminiAIServiceDescriptionProvider(
        GeminiProviderClient client,
        IOptions<AIProviderOptions> options,
        AIServiceDescriptionPromptBuilder promptBuilder,
        AIServiceDescriptionParser parser,
        MockAIServiceDescriptionProvider fallbackProvider,
        ILogger<GeminiAIServiceDescriptionProvider> logger)
    {
        _client = client;
        _options = options.Value;
        _promptBuilder = promptBuilder;
        _parser = parser;
        _fallbackProvider = fallbackProvider;
        _logger = logger;
    }

    public async Task<AIServiceDescriptionDraft> GenerateServiceDescriptionAsync(Request.GenerateServiceDescriptionRequest request, CancellationToken cancellationToken = default)
    {
        if (!_client.HasApiKey && _options.EnableFallback)
        {
            _logger.LogWarning("Gemini API key is missing; using mock AI service description provider fallback.");
            return await _fallbackProvider.GenerateServiceDescriptionAsync(request, cancellationToken);
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
            _logger.LogWarning(ex, "Gemini service description provider failed; using mock fallback.");
            return await _fallbackProvider.GenerateServiceDescriptionAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini service description provider failed and fallback is disabled.");
            throw new ValidationException("AI service description provider failed. Please try again later.");
        }
    }
}
