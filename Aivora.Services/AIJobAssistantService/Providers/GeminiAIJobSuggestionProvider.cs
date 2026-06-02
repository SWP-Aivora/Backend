using Aivora.Services.AIJobAssistantService.Parsing;
using Aivora.Services.AIJobAssistantService.Prompting;
using Aivora.Services.Exceptions;
using Aivora.Services.Options;
using Microsoft.Extensions.Options;

namespace Aivora.Services.AIJobAssistantService.Providers;

public class GeminiAIJobSuggestionProvider : IAIJobSuggestionProvider
{
    private readonly GeminiProviderClient _client;
    private readonly AIProviderOptions _options;
    private readonly AIJobSuggestionPromptBuilder _promptBuilder;
    private readonly AIJobSuggestionParser _parser;
    private readonly MockAIJobSuggestionProvider _fallbackProvider;

    public GeminiAIJobSuggestionProvider(
        GeminiProviderClient client,
        IOptions<AIProviderOptions> options,
        AIJobSuggestionPromptBuilder promptBuilder,
        AIJobSuggestionParser parser,
        MockAIJobSuggestionProvider fallbackProvider)
    {
        _client = client;
        _options = options.Value;
        _promptBuilder = promptBuilder;
        _parser = parser;
        _fallbackProvider = fallbackProvider;
    }

    public async Task<AIJobSuggestionDraft> GenerateSuggestionAsync(Request.GenerateSuggestionRequest request, CancellationToken cancellationToken = default)
    {
        if (!_client.HasApiKey && _options.EnableFallback)
        {
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
        catch (Exception) when (_options.EnableFallback)
        {
            return await _fallbackProvider.GenerateSuggestionAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new ValidationException($"AI suggestion provider failed: {ex.Message}");
        }
    }
}
