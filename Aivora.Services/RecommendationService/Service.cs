using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.RecommendationService;

public class RecommendationApplicationService : IService
{
    private readonly AivoraDbContext _dbContext;

    public RecommendationApplicationService(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
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

        var recommendations = experts
            .Select(expert => BuildRecommendation(job, requiredSkills, expert))
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
                CompletedProjects = r.Expert.ExpertProfile.CompletedProjects
            })
            .ToListAsync();
    }

    private static RecommendationResult BuildRecommendation(JobPost job, List<RequiredSkill> requiredSkills, ExpertProfile expert)
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
            TotalScore = totalScore,
            Explanation = BuildExplanation(requiredSkills.Count, matchedSkillNames, expert, budgetScore)
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

    private static string BuildExplanation(int requiredSkillCount, List<string> matchedSkillNames, ExpertProfile expert, decimal budgetScore)
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
            explanation += "Expert is available now.";
        }

        return explanation;
    }

    private sealed record RequiredSkill(Guid SkillId, string SkillName);
}
