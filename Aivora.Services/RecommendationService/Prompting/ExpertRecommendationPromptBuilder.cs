using System.Text.Json;

namespace Aivora.Services.RecommendationService.Prompting;

public class ExpertRecommendationPromptBuilder
{
    public string Build(ExpertRecommendationContext context)
    {
        return $$"""
            You are an AI Expert Recommendation engine for an AI-services freelance marketplace.
            From the candidate experts below, select and rank the best matches for this job.
            Each candidate already has a scorerTotalScore (0-100) computed by a rule-based system
            (skill match, budget fit, rating, availability, completion rate) — use it as a strong signal,
            but you may reorder candidates when the job title/description reveals a better fit.
            Job and candidates:
            {{JsonSerializer.Serialize(context)}}

            Return ONLY one JSON object with this schema:
            {
              "ranked": [
                { "expertId": "guid of a candidate above", "reasoning": "short reason this expert fits the job" }
              ]
            }
            Rank best first. Include at most 5 experts. Use only expertId values from the candidate list above.
            """;
    }
}
