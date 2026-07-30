using System.Text.Json;
using Aivora.Services.AIJobAssistantService.Parsing;
using Microsoft.Extensions.Logging;

namespace Aivora.Services.RecommendationService.Parsing;

public class ExpertRecommendationParser
{
    private const int MaxRanked = 5;
    private const int MaxReasoningLength = 2000; // khop HasMaxLength(2000) cua RecommendationResult.Explanation
    private const string DefaultModelName = "gemini-2.5-flash"; // khop default cua AIProviderOptions.Model

    public ExpertRecommendationDraft Parse(string providerText, ExpertRecommendationContext context, string modelName = DefaultModelName, ILogger? logger = null)
    {
        var fallback = BuildScorerOrderDraft(context);
        fallback.AIModel = modelName;

        using var document = AIJsonParser.ParseObject(providerText);
        var root = document.RootElement;
        if (!AIJsonParser.TryGetProperty(root, "ranked", out var rankedElement) || rankedElement.ValueKind != JsonValueKind.Array)
        {
            logger?.LogWarning("Gemini expert recommendation response missing 'ranked' array; using scorer order fallback.");
            return fallback;
        }

        var validExpertIds = context.Candidates.Select(c => c.ExpertId).ToHashSet();
        var seen = new HashSet<Guid>();
        var ranked = new List<RankedExpert>();

        foreach (var item in rankedElement.EnumerateArray())
        {
            if (ranked.Count >= MaxRanked || item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var expertIdText = AIJsonParser.GetString(item, "expertId");
            if (expertIdText == null || !Guid.TryParse(expertIdText, out var expertId))
            {
                continue;
            }

            if (!validExpertIds.Contains(expertId) || !seen.Add(expertId))
            {
                continue;
            }

            var reasoning = AIJsonParser.GetString(item, "reasoning") ?? "Matches the job requirements.";
            if (reasoning.Length > MaxReasoningLength)
            {
                reasoning = reasoning[..MaxReasoningLength];
            }

            ranked.Add(new RankedExpert { ExpertId = expertId, Reasoning = reasoning });
        }

        if (ranked.Count == 0)
        {
            logger?.LogWarning("Gemini expert recommendation response contained no valid candidate; using scorer order fallback.");
            return fallback;
        }

        // Gemini can drop individual candidates (hallucinated/duplicate expertId) while still
        // returning a mostly-valid list — backfill from the scorer order instead of silently
        // returning fewer than MaxRanked recommendations.
        if (ranked.Count < MaxRanked)
        {
            var backfill = context.Candidates
                .Where(c => !seen.Contains(c.ExpertId))
                .Take(MaxRanked - ranked.Count)
                .Select(c => new RankedExpert { ExpertId = c.ExpertId, Reasoning = c.ScorerExplanation })
                .ToList();
            if (backfill.Count > 0)
            {
                logger?.LogWarning("Gemini expert recommendation response returned only {Count} valid candidates; backfilled {BackfillCount} from scorer order.", ranked.Count, backfill.Count);
                ranked.AddRange(backfill);
            }
        }

        return new ExpertRecommendationDraft { Ranked = ranked, AIModel = modelName };
    }

    // Deterministic ranking = scorer order. Dung lam fallback khi AI loi/rong,
    // va lam toan bo hanh vi cua MockExpertRecommendationProvider.
    public static ExpertRecommendationDraft BuildScorerOrderDraft(ExpertRecommendationContext context)
    {
        var ranked = context.Candidates
            .Take(MaxRanked)
            .Select(c => new RankedExpert
            {
                ExpertId = c.ExpertId,
                Reasoning = c.ScorerExplanation
            })
            .ToList();

        return new ExpertRecommendationDraft { Ranked = ranked };
    }
}
