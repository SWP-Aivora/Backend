using Aivora.Services.AIJobAssistantService.Parsing;
using Aivora.Services.AIJobAssistantService.Prompting;
using Aivora.Services.Exceptions;
using Aivora.Services.Options;
using Microsoft.Extensions.Options;

namespace Aivora.Services.AIJobAssistantService.Providers;

public class GeminiAIJobRefinementProvider : IAIJobRefinementProvider
{
    private readonly GeminiProviderClient _client;
    private readonly AIProviderOptions _options;
    private readonly AIJobRefinementPromptBuilder _promptBuilder;
    private readonly AIJobRefinementParser _parser;
    private readonly MockAIJobRefinementProvider _fallbackProvider;

    public GeminiAIJobRefinementProvider(
        GeminiProviderClient client,
        IOptions<AIProviderOptions> options,
        AIJobRefinementPromptBuilder promptBuilder,
        AIJobRefinementParser parser,
        MockAIJobRefinementProvider fallbackProvider)
    {
        _client = client;
        _options = options.Value;
        _promptBuilder = promptBuilder;
        _parser = parser;
        _fallbackProvider = fallbackProvider;
    }

    public async Task<AIJobRefinementDraft> RefineSuggestionAsync(Response.SuggestionResponse current, string message, CancellationToken cancellationToken = default)
    {
        if (!_client.HasApiKey && _options.EnableFallback)
        {
            return await _fallbackProvider.RefineSuggestionAsync(current, message, cancellationToken);
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
        catch (Exception) when (_options.EnableFallback)
        {
            return await _fallbackProvider.RefineSuggestionAsync(current, message, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new ValidationException($"AI refinement provider failed: {ex.Message}");
        }
    }
}
