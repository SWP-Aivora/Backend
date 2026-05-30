using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.RecommendationService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;

    public Service(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Response.RecommendationResponse>> GenerateRecommendationsAsync(Guid clientId, Guid jobId)
    {
        var job = await _dbContext.JobPosts
            .Include(j => j.JobSkills)
            .FirstOrDefaultAsync(j => j.Id == jobId && j.ClientId == clientId);

        if (job == null) throw new NotFoundException("Job not found or access denied.");
        if (job.Status != JobStatus.OPEN) throw new ValidationException("Job must be OPEN to generate recommendations.");

        // Clear existing recommendations for this job
        var existing = await _dbContext.RecommendationResults.Where(r => r.JobId == jobId).ToListAsync();
        _dbContext.RecommendationResults.RemoveRange(existing);

        var requiredSkillIds = job.JobSkills.Select(js => js.SkillId).ToList();

        // Simple algorithm: 
        // 1. Find experts with at least one matching skill
        // 2. Calculate scores
        var matchingExperts = await _dbContext.ExpertProfiles
            .Include(e => e.User)
            .Include(e => e.ExpertSkills)
            .Where(e => e.ExpertSkills.Any(es => requiredSkillIds.Contains(es.SkillId)))
            .ToListAsync();

        var recommendations = new List<RecommendationResult>();

        foreach (var expert in matchingExperts)
        {
            var matchedSkillsCount = expert.ExpertSkills.Count(es => requiredSkillIds.Contains(es.SkillId));
            var skillScore = (decimal)matchedSkillsCount / Math.Max(1, requiredSkillIds.Count) * 50; // Max 50 points
            var ratingScore = expert.RatingAvg * 2; // Max 10 points (if rating is 5) - Wait, rating is 0-5, let's say max 20 points
            var completionScore = Math.Min( expert.CompletedProjects, 20); // Max 20 points

            var totalScore = skillScore + (expert.RatingAvg * 4) + (Math.Min(expert.CompletedProjects, 10)); 

            var rec = new RecommendationResult
            {
                JobId = jobId,
                ExpertId = expert.UserId,
                SkillScore = skillScore,
                RatingScore = expert.RatingAvg * 4,
                CompletionScore = Math.Min(expert.CompletedProjects, 10),
                TotalScore = totalScore,
                Explanation = $"Expert matches {matchedSkillsCount} out of {requiredSkillIds.Count} required skills. Average rating is {expert.RatingAvg}."
            };

            recommendations.Add(rec);
        }

        _dbContext.RecommendationResults.AddRange(recommendations.OrderByDescending(r => r.TotalScore).Take(5));
        await _dbContext.SaveChangesAsync();

        return await GetRecommendationsAsync(clientId, jobId);
    }

    public async Task<List<Response.RecommendationResponse>> GetRecommendationsAsync(Guid clientId, Guid jobId)
    {
        var job = await _dbContext.JobPosts.AnyAsync(j => j.Id == jobId && j.ClientId == clientId);
        if (!job) throw new NotFoundException("Job not found or access denied.");

        var recs = await _dbContext.RecommendationResults
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
                RatingScore = r.RatingScore,
                CompletionScore = r.CompletionScore,
                Explanation = r.Explanation,
                RatingAvg = r.Expert.ExpertProfile.RatingAvg,
                CompletedProjects = r.Expert.ExpertProfile.CompletedProjects
            })
            .ToListAsync();

        return recs;
    }
}
