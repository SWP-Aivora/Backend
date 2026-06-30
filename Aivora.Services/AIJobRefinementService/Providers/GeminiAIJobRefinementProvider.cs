using Aivora.Services.AIJobAssistantService.Providers;
using Aivora.Services.AIJobRefinementService.Parsing;
using Aivora.Services.AIJobRefinementService.Prompting;
using Aivora.Services.Exceptions;
using Aivora.Services.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aivora.Services.AIJobRefinementService.Providers;

public class GeminiAIJobRefinementProvider : IAIJobRefinementProvider
{
    private readonly GeminiProviderClient _client;
    private readonly AIProviderOptions _options;
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
    {
        _client = client;
        _options = options.Value;
        _promptBuilder = promptBuilder;
        _parser = parser;
        _fallbackProvider = fallbackProvider;
        _logger = logger;
    }

    public async Task<AIJobRefinementDraft> RefineJobAsync(JobService.Response.JobResponse current, string message, CancellationToken cancellationToken = default)
    {
        if (!_client.HasApiKey && _options.EnableFallback)
        {
            _logger.LogWarning("Gemini API key is missing; using mock AI job refinement provider fallback.");
            return await _fallbackProvider.RefineJobAsync(current, message, cancellationToken);
        }

        try
        {
            var providerText = await _client.GenerateAsync(_promptBuilder.Build(current, message), cancellationToken);
            return _parser.Parse(providerText, current);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (_options.EnableFallback)
        {
            _logger.LogWarning(ex, "Gemini job refinement provider failed; using mock fallback.");
            return await _fallbackProvider.RefineJobAsync(current, message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini job refinement provider failed and fallback is disabled.");
            throw new ValidationException("AI refinement provider failed. Please try again later.");
        }
    }
}
