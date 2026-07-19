using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.Extensions;
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
            .IncludeSkills()
            .FirstOrDefaultAsync(j => j.Id == jobId && j.ClientId == clientId);

        if (job == null) throw new NotFoundException("Job not found or access denied.");
        if (job.Status != JobStatus.OPEN) throw new ValidationException("Job must be OPEN to generate recommendations.");

        var existing = await _dbContext.RecommendationResults.Where(r => r.JobId == jobId).ToListAsync();
        _dbContext.RecommendationResults.RemoveRange(existing);

        var requiredSkills = job.JobSkills
            .Select(js => new RequiredSkill(js.SkillId, js.Skill.Name))
            .DistinctBy(x => x.SkillId)
            .ToList();

        var activeExpertsQuery = _dbContext.ExpertProfiles
            .Where(e => e.User.Role == UserRole.EXPERT && e.User.Status == UserStatus.ACTIVE);

        if (requiredSkills.Count > 0)
        {
            var requiredSkillIds = requiredSkills.Select(rs => rs.SkillId).ToList();
            activeExpertsQuery = activeExpertsQuery
                .Where(e => e.ExpertSkills.Any(es => requiredSkillIds.Contains(es.SkillId)))
                .OrderByDescending(e => e.ExpertSkills.Count(es => requiredSkillIds.Contains(es.SkillId)))
                .ThenByDescending(e => e.Rating);
        }
        else
        {
            activeExpertsQuery = activeExpertsQuery.OrderByDescending(e => e.Rating);
        }

        // Limit the candidate pool to the top 50 experts at database level to prevent memory issues
        activeExpertsQuery = activeExpertsQuery.Take(50);

        var expertIds = await activeExpertsQuery.Select(e => e.UserId).ToListAsync();

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

        var experts = await _dbContext.ExpertProfiles
            .Include(e => e.User)
            .Include(e => e.ExpertSkills).ThenInclude(es => es.Skill)
            .Where(e => expertIds.Contains(e.UserId))
            .ToListAsync();

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
        var score = RecommendationScorer.Score(job, requiredSkills, expert, disputeCount, totalMilestoneCount, overdueMilestoneCount, _options);

        return new RecommendationResult
        {
            JobId = job.Id,
            ExpertId = expert.UserId,
            SkillScore = score.SkillScore,
            PortfolioScore = score.PortfolioScore,
            RatingScore = score.RatingScore,
            BudgetScore = score.BudgetScore,
            AvailabilityScore = score.AvailabilityScore,
            CompletionScore = score.CompletionScore,
            DisputeRate = score.DisputeRate,
            DisputePenalty = score.DisputePenalty,
            OverdueRate = score.OverdueRate,
            OverduePenalty = score.OverduePenalty,
            TotalScore = score.TotalScore,
            Explanation = score.Explanation
        };
    }
}
