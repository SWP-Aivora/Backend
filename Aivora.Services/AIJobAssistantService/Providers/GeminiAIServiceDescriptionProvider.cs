using Aivora.Services.AIJobAssistantService.Parsing;
using Aivora.Services.AIJobAssistantService.Prompting;
using Aivora.Services.Exceptions;
using Aivora.Services.Options;
using Microsoft.Extensions.Options;

namespace Aivora.Services.AIJobAssistantService.Providers;

public class GeminiAIServiceDescriptionProvider : IAIServiceDescriptionProvider
{
    private readonly GeminiProviderClient _client;
    private readonly AIProviderOptions _options;
    private readonly AIServiceDescriptionPromptBuilder _promptBuilder;
    private readonly AIServiceDescriptionParser _parser;
    private readonly MockAIServiceDescriptionProvider _fallbackProvider;

    public GeminiAIServiceDescriptionProvider(
        GeminiProviderClient client,
        IOptions<AIProviderOptions> options,
        AIServiceDescriptionPromptBuilder promptBuilder,
        AIServiceDescriptionParser parser,
        MockAIServiceDescriptionProvider fallbackProvider)
    {
        _client = client;
        _options = options.Value;
        _promptBuilder = promptBuilder;
        _parser = parser;
        _fallbackProvider = fallbackProvider;
    }

    public async Task<AIServiceDescriptionDraft> GenerateServiceDescriptionAsync(Request.GenerateServiceDescriptionRequest request, CancellationToken cancellationToken = default)
    {
        if (!_client.HasApiKey && _options.EnableFallback)
        {
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
        catch (Exception) when (_options.EnableFallback)
        {
            return await _fallbackProvider.GenerateServiceDescriptionAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new ValidationException($"AI service description provider failed: {ex.Message}");
        }
    }
}
