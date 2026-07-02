using Aivora.Services.AIJobAssistantService.Providers;
using Aivora.Services.Exceptions;
using Aivora.Services.ExpertVerificationService.Parsing;
using Aivora.Services.ExpertVerificationService.Prompting;
using Microsoft.Extensions.Logging;

namespace Aivora.Services.ExpertVerificationService.Providers;

public class GeminiAIExpertVerificationProvider : IAIExpertVerificationProvider
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan[] RetryDelays = { TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(600) };

    private readonly GeminiProviderClient _client;
    private readonly AIExpertVerificationPromptBuilder _promptBuilder;
    private readonly AIExpertVerificationParser _parser;
    private readonly ILogger<GeminiAIExpertVerificationProvider> _logger;

    public GeminiAIExpertVerificationProvider(
        GeminiProviderClient client,
        AIExpertVerificationPromptBuilder promptBuilder,
        AIExpertVerificationParser parser,
        ILogger<GeminiAIExpertVerificationProvider> logger)
    {
        _client = client;
        _promptBuilder = promptBuilder;
        _parser = parser;
        _logger = logger;
    }

    /// <summary>
    /// Deliberately does not fall back to Mock on Gemini failure — a fabricated verdict for a real
    /// certificate would be worse than surfacing the outage. Infrastructure failures (HTTP errors,
    /// timeouts, unparsable responses) are retried a couple of times, then surfaced as
    /// ServiceUnavailableException so the caller can skip persisting a bogus record.
    /// </summary>
    public async Task<AIVerificationResult> AnalyzeEvidenceAsync(AnalyzeEvidenceRequest request, CancellationToken cancellationToken = default)
    {
        if (!_client.HasApiKey)
        {
            throw new ValidationException("AI provider API key is missing.");
        }

        var prompt = _promptBuilder.Build(request);
        var attachments = new[] { (request.MimeType, request.FileBytes) };

        Exception lastError = new ServiceUnavailableException("Verification system is busy. Please try again shortly.");

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            try
            {
                var providerText = await _client.GenerateAsync(prompt, attachments, cancellationToken);
                return _parser.Parse(providerText);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger.LogWarning(ex, "Gemini expert verification attempt {Attempt}/{MaxAttempts} failed.", attempt + 1, MaxAttempts);

                if (attempt < MaxAttempts - 1)
                {
                    await Task.Delay(RetryDelays[attempt], cancellationToken);
                }
            }
        }

        _logger.LogError(lastError, "Gemini expert verification failed after {MaxAttempts} attempts.", MaxAttempts);
        throw new ServiceUnavailableException("Verification system is busy. Please try again shortly.");
    }
}
