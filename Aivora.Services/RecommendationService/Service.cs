using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aivora.Services.RecommendationService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;
    private readonly RecommendationOptions _options;

    public Service(AivoraDbContext dbContext, IOptions<RecommendationOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<List<Response.RecommendationResponse>> GenerateRecommendationsAsync(Guid clientId, Guid jobId)
    {
        var job = await _dbContext.JobPosts
            .Include(j => j.JobSkills).ThenInclude(js => js.Skill)
            .FirstOrDefaultAsync(j => j.Id == jobId && j.ClientId == clientId);

        if (job == null) throw new NotFoundException("Job not found or access denied.");
        if (job.Status != JobStatus.OPEN) throw new ValidationException("Job must be OPEN to generate recommendations.");

        var existing = await _dbContext.RecommendationResults.Where(r => r.JobId == jobId).ToListAsync();
        _dbContext.RecommendationResults.RemoveRange(existing);

        var requiredSkills = job.JobSkills
            .Select(js => new RequiredSkill(js.SkillId, js.Skill.Name))
            .DistinctBy(x => x.SkillId)
            .ToList();

        var experts = await _dbContext.ExpertProfiles
            .Include(e => e.User)
            .Include(e => e.ExpertSkills).ThenInclude(es => es.Skill)
            .Where(e => e.User.Role == UserRole.EXPERT && e.User.Status == UserStatus.ACTIVE)
            .ToListAsync();

        var expertIds = experts.Select(e => e.UserId).ToList();
        var disputeCounts = await _dbContext.Disputes
            .Where(d => expertIds.Contains(d.Project.ExpertId))
            .GroupBy(d => d.Project.ExpertId)
            .Select(g => new { ExpertId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ExpertId, x => x.Count);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var milestoneStats = await _dbContext.Milestones
            .Where(m => expertIds.Contains(m.Project.ExpertId)
                && (m.Project.Status == ProjectStatus.ACTIVE || m.Project.Status == ProjectStatus.DISPUTED))
            .GroupBy(m => m.Project.ExpertId)
            .Select(g => new
            {
                ExpertId = g.Key,
                TotalCount = g.Count(),
                OverdueCount = g.Count(m => m.DueDate != null
                    && m.DueDate < today
                    && m.Status != MilestoneStatus.COMPLETED
                    && m.Status != MilestoneStatus.RELEASED
                    && m.Status != MilestoneStatus.REFUNDED
                    && m.Status != MilestoneStatus.APPROVED)
            })
            .ToDictionaryAsync(x => x.ExpertId, x => (Total: x.TotalCount, Overdue: x.OverdueCount));

        var recommendations = experts
            .Select(expert =>
            {
                var disputeCount = disputeCounts.GetValueOrDefault(expert.UserId, 0);
                var milestoneStat = milestoneStats.GetValueOrDefault(expert.UserId, (Total: 0, Overdue: 0));
                return BuildRecommendation(
                    job,
                    requiredSkills,
                    expert,
                    disputeCount,
                    milestoneStat.Total,
                    milestoneStat.Overdue);
            })
            .OrderByDescending(r => r.TotalScore)
            .Take(5)
            .ToList();

        _dbContext.RecommendationResults.AddRange(recommendations);
        await _dbContext.SaveChangesAsync();

        return await GetRecommendationsAsync(clientId, jobId);
    }

    public async Task<List<Response.RecommendationResponse>> GetRecommendationsAsync(Guid clientId, Guid jobId)
    {
        var job = await _dbContext.JobPosts.AnyAsync(j => j.Id == jobId && j.ClientId == clientId);
        if (!job) throw new NotFoundException("Job not found or access denied.");

        return await _dbContext.RecommendationResults
            .Include(r => r.Expert).ThenInclude(u => u.ExpertProfile)
            .Where(r => r.JobId == jobId)
            .OrderByDescending(r => r.TotalScore)
            .Select(r => new Response.RecommendationResponse
            {
                Id = r.Id,
                JobId = r.JobId,
                ExpertId = r.ExpertId,
                ExpertName = r.Expert.FullName,
                ExpertTitle = r.Expert.ExpertProfile!.Title,
                TotalScore = r.TotalScore,
                SkillScore = r.SkillScore,
                PortfolioScore = r.PortfolioScore,
                RatingScore = r.RatingScore,
                BudgetScore = r.BudgetScore,
                AvailabilityScore = r.AvailabilityScore,
                CompletionScore = r.CompletionScore,
                Explanation = r.Explanation,
                Rating = r.Expert.ExpertProfile.Rating,
                CompletedProjects = r.Expert.ExpertProfile.CompletedProjects,
                DisputeRate = r.DisputeRate,
                DisputePenalty = r.DisputePenalty,
                OverdueRate = r.OverdueRate,
                OverduePenalty = r.OverduePenalty
            })
            .ToListAsync();
    }

    private RecommendationResult BuildRecommendation(
        JobPost job,
        List<RequiredSkill> requiredSkills,
        ExpertProfile expert,
        int disputeCount,
        int totalMilestoneCount,
        int overdueMilestoneCount)
    {
        var skillScore = CalculateSkillScore(requiredSkills, expert, out var matchedSkillNames);
        var budgetScore = CalculateBudgetScore(job, expert);
        var ratingScore = Math.Round(expert.Rating * 20, 2);
        var availabilityScore = expert.AvailabilityStatus == AvailabilityStatus.AVAILABLE ? 100m : 50m;
        var completionScore = expert.SuccessRate > 0 ? expert.SuccessRate : 80m;
        var portfolioScore = 0m;
        var totalScore = Math.Round(
            (skillScore * 0.40m)
            + (budgetScore * 0.20m)
            + (ratingScore * 0.20m)
            + (availabilityScore * 0.10m)
            + (completionScore * 0.10m),
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
        var penalty = Math.Min(disputeRate * _options.DisputePenaltyFactor, _options.MaxDisputePenalty);

        var overdueRate = totalMilestoneCount > 0
            ? (decimal)overdueMilestoneCount / totalMilestoneCount
            : 0m;

        var overduePenalty = Math.Min(overdueRate * _options.OverduePenaltyFactor, _options.MaxOverduePenalty);
        totalScore = Math.Round(totalScore * (1 - penalty) * (1 - overduePenalty), 2);

        return new RecommendationResult
        {
            JobId = job.Id,
            ExpertId = expert.UserId,
            SkillScore = skillScore,
            PortfolioScore = portfolioScore,
            RatingScore = ratingScore,
            BudgetScore = budgetScore,
            AvailabilityScore = availabilityScore,
            CompletionScore = completionScore,
            DisputeRate = Math.Round(disputeRate, 4),
            DisputePenalty = Math.Round(penalty, 4),
            OverdueRate = Math.Round(overdueRate, 4),
            OverduePenalty = Math.Round(overduePenalty, 4),
            TotalScore = totalScore,
            Explanation = BuildExplanation(requiredSkills.Count, matchedSkillNames, expert, budgetScore, penalty, overduePenalty)
        };
    }

    private static decimal CalculateSkillScore(List<RequiredSkill> requiredSkills, ExpertProfile expert, out List<string> matchedSkillNames)
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

    private static decimal CalculateBudgetScore(JobPost job, ExpertProfile expert)
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

    private sealed record RequiredSkill(Guid SkillId, string SkillName);
}
