using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Options;
using Aivora.Services.RecommendationService;
using FluentAssertions;
using Xunit;

namespace Aivora.Tests.Services;

// Unit thuan cho RecommendationScorer - khong dung DbContext, chi POCO.
public class RecommendationScorerTests
{
    private static readonly RecommendationOptions DefaultOptions = new();

    private static JobPost MakeJob(BudgetType budgetType, decimal? budgetMin, decimal? budgetMax, int? timelineDays) =>
        new()
        {
            BudgetType = budgetType,
            BudgetMin = budgetMin,
            BudgetMax = budgetMax,
            TimelineDays = timelineDays
        };

    private static ExpertProfile MakeExpert(
        decimal? hourlyRate = 25m,
        decimal rating = 4m,
        decimal successRate = 80m,
        AvailabilityStatus availability = AvailabilityStatus.AVAILABLE,
        int completedProjects = 0,
        List<ExpertSkill>? skills = null) =>
        new()
        {
            UserId = Guid.NewGuid(),
            HourlyRate = hourlyRate,
            Rating = rating,
            SuccessRate = successRate,
            AvailabilityStatus = availability,
            CompletedProjects = completedProjects,
            ExpertSkills = skills ?? new List<ExpertSkill>()
        };

    [Fact]
    public void CalculateSkillScore_NoRequiredSkills_ReturnsFullScore()
    {
        var score = RecommendationScorer.CalculateSkillScore(new List<RequiredSkill>(), MakeExpert(), out var matched);

        score.Should().Be(100);
        matched.Should().BeEmpty();
    }

    [Fact]
    public void CalculateSkillScore_ExpertLevelScoresHigherThanBeginner()
    {
        var skillId = Guid.NewGuid();
        var required = new List<RequiredSkill> { new(skillId, "Python") };

        var beginnerScore = RecommendationScorer.CalculateSkillScore(
            required, MakeExpert(skills: new List<ExpertSkill> { new() { SkillId = skillId, Level = SkillLevel.BEGINNER } }), out _);
        var expertScore = RecommendationScorer.CalculateSkillScore(
            required, MakeExpert(skills: new List<ExpertSkill> { new() { SkillId = skillId, Level = SkillLevel.EXPERT } }), out var matchedNames);

        expertScore.Should().BeGreaterThan(beginnerScore);
        matchedNames.Should().Contain("Python");
    }

    [Fact]
    public void CalculateBudgetScore_HourlyWithinRange_Returns100()
    {
        var job = MakeJob(BudgetType.HOURLY, budgetMin: 50, budgetMax: 100, timelineDays: 10);
        var expert = MakeExpert(hourlyRate: 75m);

        RecommendationScorer.CalculateBudgetScore(job, expert).Should().Be(100);
    }

    [Fact]
    public void CalculateBudgetScore_FixedWithinRange_Returns100()
    {
        var job = MakeJob(BudgetType.FIXED, budgetMin: 500, budgetMax: 600, timelineDays: 2);
        var expert = MakeExpert(hourlyRate: 50m);

        RecommendationScorer.CalculateBudgetScore(job, expert).Should().Be(100);
    }

    [Fact]
    public void CalculateBudgetScore_AboveMax_ReturnsLessThan100()
    {
        var job = MakeJob(BudgetType.HOURLY, budgetMin: 10, budgetMax: 20, timelineDays: 10);
        var expert = MakeExpert(hourlyRate: 100m);

        RecommendationScorer.CalculateBudgetScore(job, expert).Should().BeLessThan(100);
    }

    [Fact]
    public void Score_AvailabilityRatingAndCompletion_MatchExpectedWeights()
    {
        var job = MakeJob(BudgetType.HOURLY, budgetMin: 10, budgetMax: 100, timelineDays: 10);
        var expert = MakeExpert(hourlyRate: 25m, rating: 4.5m, successRate: 92m, availability: AvailabilityStatus.BUSY);

        var score = RecommendationScorer.Score(job, new List<RequiredSkill>(), expert, disputeCount: 0, totalMilestoneCount: 0, overdueMilestoneCount: 0, DefaultOptions);

        score.RatingScore.Should().Be(90);
        score.AvailabilityScore.Should().Be(50);
        score.CompletionScore.Should().Be(92);
        score.PortfolioScore.Should().Be(0);
    }

    [Fact]
    public void Score_DisputeRateBelowThreeCompletedProjects_NoPenaltyApplied()
    {
        var job = MakeJob(BudgetType.HOURLY, budgetMin: 10, budgetMax: 100, timelineDays: 10);
        var expert = MakeExpert(completedProjects: 2);

        var score = RecommendationScorer.Score(job, new List<RequiredSkill>(), expert, disputeCount: 1, totalMilestoneCount: 0, overdueMilestoneCount: 0, DefaultOptions);

        score.DisputeRate.Should().Be(0);
        score.DisputePenalty.Should().Be(0);
    }

    [Fact]
    public void Score_HighDisputeRate_AppliesCappedPenalty()
    {
        var job = MakeJob(BudgetType.HOURLY, budgetMin: 10, budgetMax: 100, timelineDays: 10);
        var expert = MakeExpert(completedProjects: 3);

        var score = RecommendationScorer.Score(job, new List<RequiredSkill>(), expert, disputeCount: 3, totalMilestoneCount: 0, overdueMilestoneCount: 0, DefaultOptions);

        score.DisputeRate.Should().Be(1m);
        score.DisputePenalty.Should().Be(DefaultOptions.MaxDisputePenalty);
    }

    [Fact]
    public void Score_OverdueMilestones_AppliesOverduePenalty()
    {
        var job = MakeJob(BudgetType.HOURLY, budgetMin: 10, budgetMax: 100, timelineDays: 10);
        var expert = MakeExpert();

        var score = RecommendationScorer.Score(job, new List<RequiredSkill>(), expert, disputeCount: 0, totalMilestoneCount: 4, overdueMilestoneCount: 2, DefaultOptions);

        score.OverdueRate.Should().Be(0.5m);
        score.OverduePenalty.Should().Be(0.15m); // 0.5 * OverduePenaltyFactor(0.3)
    }

    [Fact]
    public void Score_Explanation_MentionsMatchedSkills()
    {
        var skillId = Guid.NewGuid();
        var job = MakeJob(BudgetType.HOURLY, budgetMin: 10, budgetMax: 100, timelineDays: 10);
        var expert = MakeExpert(skills: new List<ExpertSkill> { new() { SkillId = skillId, Level = SkillLevel.EXPERT } });

        var score = RecommendationScorer.Score(job, new List<RequiredSkill> { new(skillId, "Python") }, expert, disputeCount: 0, totalMilestoneCount: 0, overdueMilestoneCount: 0, DefaultOptions);

        score.Explanation.Should().Contain("Python");
    }
}
