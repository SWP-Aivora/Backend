using Aivora.Services.Exceptions;
using Aivora.Services.Options;
using Microsoft.Extensions.Logging;

namespace Aivora.Services.AIJobAssistantService.Providers;

public abstract class GeminiProviderBase
{
    private readonly GeminiProviderClient _client;
    private readonly AIProviderOptions _options;
    private readonly ILogger _logger;

    protected GeminiProviderBase(GeminiProviderClient client, AIProviderOptions options, ILogger logger)
    {
        _client = client;
        _options = options;
        _logger = logger;
    }

    protected async Task<T> ExecuteAsync<T>(
        Func<string> buildPrompt,
        Func<string, T> parse,
        Func<CancellationToken, Task<T>> mockFallback,
        string logNoun,
        string errorNoun,
        CancellationToken cancellationToken)
    {
        if (!_client.HasApiKey && _options.EnableFallback)
        {
            _logger.LogWarning($"Gemini API key is missing; using mock AI {logNoun} provider fallback.");
            return await mockFallback(cancellationToken);
        }

        try
        {
            var providerText = await _client.GenerateAsync(buildPrompt(), cancellationToken);
            return parse(providerText);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (_options.EnableFallback)
        {
            _logger.LogWarning(ex, $"Gemini {logNoun} provider failed; using mock fallback.");
            return await mockFallback(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Gemini {logNoun} provider failed and fallback is disabled.");
            throw new ValidationException($"AI {errorNoun} provider failed. Please try again later.");
        }
    }
}
