namespace Aivora.Services.AIJobAssistantService;

public interface IService
{
    Task<Response.SuggestionResponse> GenerateSuggestionAsync(Guid clientId, Request.GenerateSuggestionRequest request);
    Task<Response.AcceptResultResponse> AcceptSuggestionAsync(Guid clientId, Guid suggestionId, Request.AcceptSuggestionRequest request);
    Task<bool> RejectSuggestionAsync(Guid clientId, Guid suggestionId, Request.RejectSuggestionRequest request);
}
