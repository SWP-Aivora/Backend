namespace Aivora.Services.RecommendationService;

public interface IExpertRecommendationProvider
{
    Task<ExpertRecommendationDraft> RankAsync(ExpertRecommendationContext context, CancellationToken cancellationToken = default);
}
