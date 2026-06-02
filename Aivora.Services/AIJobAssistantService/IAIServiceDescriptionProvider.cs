namespace Aivora.Services.AIJobAssistantService;

public interface IAIServiceDescriptionProvider
{
    Task<AIServiceDescriptionDraft> GenerateServiceDescriptionAsync(Request.GenerateServiceDescriptionRequest request, CancellationToken cancellationToken = default);
}
