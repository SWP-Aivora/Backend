namespace Aivora.Services.RecommendationService;

public interface IService
{
    Task<List<Response.RecommendationResponse>> GenerateRecommendationsAsync(Guid clientId, Guid jobId);
    Task<List<Response.RecommendationResponse>> GetRecommendationsAsync(Guid clientId, Guid jobId);
}
