using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Options;

namespace Aivora.Services.RecommendationService;

public sealed record RequiredSkill(Guid SkillId, string SkillName);

public sealed record RecommendationScore(
    decimal SkillScore,
    decimal PortfolioScore,
    decimal RatingScore,
    decimal BudgetScore,
    decimal AvailabilityScore,
    decimal CompletionScore,
    decimal DisputeRate,
    decimal DisputePenalty,
    decimal OverdueRate,
    decimal OverduePenalty,
    decimal TotalScore,
    string Explanation);

// Cong thuc cham diem recommendation, tach khoi Service de test truc tiep
// bang POCO (JobPost/ExpertProfile dung tay), khong can DbContext.
public static class RecommendationScorer
{
    public static RecommendationScore Score(
        JobPost job,
        List<RequiredSkill> requiredSkills,
        ExpertProfile expert,
        int disputeCount,
        int totalMilestoneCount,
        int overdueMilestoneCount,
        RecommendationOptions options)
    {
        var skillScore = CalculateSkillScore(requiredSkills, expert, out var matchedSkillNames);
        var budgetScore = CalculateBudgetScore(job, expert);
        var ratingScore = Math.Round(expert.Rating * 20, 2);
        var availabilityScore = expert.AvailabilityStatus == AvailabilityStatus.AVAILABLE ? 100m : 50m;
        var completionScore = expert.SuccessRate > 0 ? expert.SuccessRate : 80m;
        var portfolioScore = 0m;
        var totalScore = Math.Round(
            (skillScore * options.SkillWeight)
            + (budgetScore * options.BudgetWeight)
            + (ratingScore * options.RatingWeight)
            + (availabilityScore * options.AvailabilityWeight)
            + (completionScore * options.CompletionWeight),
            2);

        // Rationale for dispute penalty:
        // - Count all opened disputes: The system no longer performs financial dispute resolution, so there is no reliable "resolution type" to infer who is right/wrong.
        // - Denominator = COMPLETED projects: Unfinished/cancelled projects provide an unfair basis for evaluation.
        // - Minimum threshold of 3 projects: Prevents a single dispute on the first project from "killing" a new expert's score (which would be a 100% dispute rate with too small a sample size).
        var disputeRate = expert.CompletedProjects >= 3 && expert.CompletedProjects > 0
            ? (decimal)disputeCount / expert.CompletedProjects
            : 0m;

        // Rationale for penalty calculation:
        // - 1.5x penalty factor, capped at 50%: A dispute rate >= 33% results in the maximum deduction.
        // - The 50% cap ensures the score never reaches absolute 0, because other axes (skill, rating, etc.) still hold reference value.
        var penalty = Math.Min(disputeRate * options.DisputePenaltyFactor, options.MaxDisputePenalty);

        var overdueRate = totalMilestoneCount > 0
            ? (decimal)overdueMilestoneCount / totalMilestoneCount
            : 0m;

        var overduePenalty = Math.Min(overdueRate * options.OverduePenaltyFactor, options.MaxOverduePenalty);
        totalScore = Math.Round(totalScore * (1 - penalty) * (1 - overduePenalty), 2);

        return new RecommendationScore(
            SkillScore: skillScore,
            PortfolioScore: portfolioScore,
            RatingScore: ratingScore,
            BudgetScore: budgetScore,
            AvailabilityScore: availabilityScore,
            CompletionScore: completionScore,
            DisputeRate: Math.Round(disputeRate, 4),
            DisputePenalty: Math.Round(penalty, 4),
            OverdueRate: Math.Round(overdueRate, 4),
            OverduePenalty: Math.Round(overduePenalty, 4),
            TotalScore: totalScore,
            Explanation: BuildExplanation(requiredSkills.Count, matchedSkillNames, expert, budgetScore, penalty, overduePenalty));
    }

    public static decimal CalculateSkillScore(List<RequiredSkill> requiredSkills, ExpertProfile expert, out List<string> matchedSkillNames)
    {
        matchedSkillNames = new List<string>();
        if (requiredSkills.Count == 0)
        {
            return 100;
        }

        var expertSkillMap = expert.ExpertSkills
            .GroupBy(es => es.SkillId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(es => SkillWeight(es.Level)).First());

        var matchedPoints = 0m;
        foreach (var requiredSkill in requiredSkills)
        {
            if (!expertSkillMap.TryGetValue(requiredSkill.SkillId, out var expertSkill))
            {
                continue;
            }

            matchedSkillNames.Add(requiredSkill.SkillName);
            matchedPoints += SkillWeight(expertSkill.Level);
        }

        return Math.Round(matchedPoints / requiredSkills.Count * 100, 2);
    }

    private static decimal SkillWeight(SkillLevel level)
    {
        return level switch
        {
            SkillLevel.BEGINNER => 0.5m,
            SkillLevel.INTERMEDIATE => 0.75m,
            SkillLevel.ADVANCED => 0.9m,
            SkillLevel.EXPERT => 1.0m,
            _ => 0.75m
        };
    }

    public static decimal CalculateBudgetScore(JobPost job, ExpertProfile expert)
    {
        var hourlyRate = expert.HourlyRate ?? 25m;
        var budgetMin = job.BudgetMin ?? 0m;
        var budgetMax = job.BudgetMax is > 0 ? job.BudgetMax.Value : 999999m;
        var comparedCost = job.BudgetType == BudgetType.HOURLY
            ? hourlyRate
            : hourlyRate * (job.TimelineDays ?? 14) * 6;

        if (comparedCost >= budgetMin && comparedCost <= budgetMax)
        {
            return 100;
        }

        if (comparedCost < budgetMin)
        {
            return 95;
        }

        var excess = comparedCost - budgetMax;
        return Math.Round(Math.Max(0, 100 - (excess / budgetMax) * 100), 2);
    }

    private static string BuildExplanation(
        int requiredSkillCount,
        List<string> matchedSkillNames,
        ExpertProfile expert,
        decimal budgetScore,
        decimal disputePenalty,
        decimal overduePenalty)
    {
        var explanation = matchedSkillNames.Count > 0
            ? $"Matches {matchedSkillNames.Count}/{requiredSkillCount} required skills ({string.Join(", ", matchedSkillNames)}). "
            : "No direct required skill match yet, but the expert profile is still scored on budget, rating, availability, and completion. ";

        if (expert.Rating >= 4.5m)
        {
            explanation += $"Strong average rating ({expert.Rating}). ";
        }

        explanation += budgetScore >= 90
            ? "Proposed cost fits the expected budget. "
            : "Proposed cost is above the expected budget. ";

        if (expert.AvailabilityStatus == AvailabilityStatus.AVAILABLE)
        {
            explanation += "Expert is available now. ";
        }

        if (disputePenalty > 0)
        {
            explanation += $"Score reduced by {disputePenalty * 100:0.#}% due to high dispute rate on past projects. ";
        }

        if (overduePenalty > 0)
        {
            explanation += $"Score reduced by {overduePenalty * 100:0.#}% due to overdue milestones on past projects. ";
        }

        return explanation.Trim();
    }
}
