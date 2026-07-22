using Aivora.Services.RecommendationService.Parsing;

namespace Aivora.Services.RecommendationService.Providers;

public class MockExpertRecommendationProvider : IExpertRecommendationProvider
{
    public Task<ExpertRecommendationDraft> RankAsync(ExpertRecommendationContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ExpertRecommendationParser.BuildScorerOrderDraft(context));
    }
}
