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
        CancellationToken cancellationToken,
        double? temperature = null,
        object? responseSchema = null)
    {
        if (!_client.HasApiKey && _options.EnableFallback)
        {
            _logger.LogWarning("Gemini API key is missing; using mock AI {LogNoun} provider fallback.", logNoun);
            return await mockFallback(cancellationToken);
        }

        try
        {
            var providerText = await _client.GenerateAsync(buildPrompt(), Array.Empty<(string, byte[])>(), cancellationToken, temperature, responseSchema);
            return parse(providerText);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (_options.EnableFallback)
        {
            // LogError, not LogWarning: a fallback here means the caller silently gets a Mock
            // answer indistinguishable from a real one unless someone reads the logs.
            _logger.LogError(ex, "Gemini {LogNoun} provider failed; using mock fallback.", logNoun);
            return await mockFallback(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini {LogNoun} provider failed and fallback is disabled.", logNoun);
            throw new ValidationException($"AI {errorNoun} provider failed. Please try again later.");
        }
    }
}
