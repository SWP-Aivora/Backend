namespace Aivora.Services.AIJobAssistantService;

public interface IService
{
    Task<Response.SuggestionResponse> GenerateSuggestionAsync(Guid clientId, Request.GenerateSuggestionRequest request, CancellationToken cancellationToken = default);
    Task<Response.SuggestionResponse> GetSuggestionAsync(Guid clientId, Guid suggestionId, CancellationToken cancellationToken = default);
    Task<Response.SuggestionResponse> PatchSuggestionAsync(Guid clientId, Guid suggestionId, Request.PatchSuggestionRequest request, CancellationToken cancellationToken = default);
    Task<Response.RefineSuggestionResponse> RefineSuggestionAsync(Guid clientId, Guid suggestionId, Request.RefineSuggestionRequest request, CancellationToken cancellationToken = default);
    Task<Response.AcceptResultResponse> AcceptSuggestionAsync(Guid clientId, Guid suggestionId, Request.AcceptSuggestionRequest request, CancellationToken cancellationToken = default);
    Task<Response.SuggestionResponse> RejectSuggestionAsync(Guid clientId, Guid suggestionId, Request.RejectSuggestionRequest request, CancellationToken cancellationToken = default);
    Task<Response.ServiceDescriptionResponse> GenerateServiceDescriptionAsync(Guid expertId, Request.GenerateServiceDescriptionRequest request, CancellationToken cancellationToken = default);
}
