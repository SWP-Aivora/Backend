using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.ProfileService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;

    public Service(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Response.ClientProfileResponse> GetClientProfileAsync(Guid userId)
    {
        var profile = await _dbContext.ClientProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) throw new NotFoundException("Client profile not found.");

        return new Response.ClientProfileResponse
        {
            UserId = profile.UserId,
            CompanyName = profile.CompanyName,
            Industry = profile.Industry,
            CompanySize = profile.CompanySize,
            Website = profile.Website,
            Description = profile.Description,
            Rating = profile.Rating,
            TotalReviews = profile.TotalReviews
        };
    }

    public async Task<Response.ClientProfileResponse> UpdateClientProfileAsync(Guid userId, Request.UpdateClientProfileRequest request)
    {
        var profile = await _dbContext.ClientProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) throw new NotFoundException("Client profile not found.");

        profile.CompanyName = request.CompanyName ?? profile.CompanyName;
        profile.Industry = request.Industry ?? profile.Industry;
        profile.CompanySize = request.CompanySize ?? profile.CompanySize;
        profile.Website = request.Website ?? profile.Website;
        profile.Description = request.Description ?? profile.Description;

        await _dbContext.SaveChangesAsync();

        return new Response.ClientProfileResponse
        {
            UserId = profile.UserId,
            CompanyName = profile.CompanyName,
            Industry = profile.Industry,
            CompanySize = profile.CompanySize,
            Website = profile.Website,
            Description = profile.Description,
            Rating = profile.Rating,
            TotalReviews = profile.TotalReviews
        };
    }

    public async Task<Response.ExpertProfileResponse> GetExpertProfileAsync(Guid userId)
    {
        var profile = await _dbContext.ExpertProfiles
            .Include(p => p.User)
            .Include(p => p.ExpertSkills)
                .ThenInclude(es => es.Skill)
            .FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) throw new NotFoundException("Expert profile not found.");
        if (profile.User == null) throw new NotFoundException("User associated with expert profile not found.");

        var counts = await GetCompletedProjectCountsAsync(new[] { profile.UserId });
        return MapToExpertProfileResponse(profile, counts.GetValueOrDefault(profile.UserId));
    }

    private static Response.ExpertProfileResponse MapToExpertProfileResponse(ExpertProfile profile, int completedProjects)
    {
        return new Response.ExpertProfileResponse
        {
            UserId = profile.UserId,
            FullName = profile.User.FullName,
            AvatarUrl = profile.User.AvatarUrl,
            Title = profile.Title,
            Bio = profile.Bio,
            HourlyRate = profile.HourlyRate,
            ExperienceYears = profile.ExperienceYears,
            AvailabilityStatus = profile.AvailabilityStatus,
            Rating = profile.Rating,
            TotalReviews = profile.TotalReviews,
            CompletedProjects = completedProjects,
            SuccessRate = profile.SuccessRate,
            Skills = profile.ExpertSkills.Select(es => new Response.ExpertSkillResponse
            {
                SkillId = es.SkillId,
                SkillName = es.Skill.Name,
                ProficiencyLevel = (int)es.Level
            }).ToList()
        };
    }

    // Real-time completed-project count per expert (Project.ExpertId == ExpertProfile.UserId),
    // batched to avoid N+1 across list endpoints. ExpertProfile.CompletedProjects itself is left
    // untouched — RecommendationService still reads it as a dispute-rate denominator.
    private async Task<Dictionary<Guid, int>> GetCompletedProjectCountsAsync(IReadOnlyCollection<Guid> expertUserIds)
    {
        return await _dbContext.Projects.AsNoTracking()
            .Where(p => expertUserIds.Contains(p.ExpertId) && p.Status == ProjectStatus.COMPLETED)
            .GroupBy(p => p.ExpertId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }

    public async Task<Response.ExpertProfileResponse> UpdateExpertProfileAsync(Guid userId, Request.UpdateExpertProfileRequest request)
    {
        var profile = await _dbContext.ExpertProfiles
            .Include(p => p.User)
            .Include(p => p.ExpertSkills)
                .ThenInclude(es => es.Skill)
            .FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) throw new NotFoundException("Expert profile not found.");
        if (profile.User == null) throw new NotFoundException("User associated with expert profile not found.");

        var hasPendingUpdate = await _dbContext.ExpertProfileUpdates
            .AnyAsync(u => u.ExpertProfileId == profile.Id && u.Status == Aivora.Repositories.Enums.ProfileUpdateStatus.PENDING);
        if (hasPendingUpdate) throw new ValidationException("A pending profile update already exists. Please wait for admin review.");

        var pendingUpdate = new ExpertProfileUpdate
        {
            ExpertProfileId = profile.Id,
            Title = request.Title ?? profile.Title,
            Bio = request.Bio ?? profile.Bio,
            HourlyRate = request.HourlyRate ?? profile.HourlyRate,
            ExperienceYears = request.ExperienceYears ?? profile.ExperienceYears,
            Status = Aivora.Repositories.Enums.ProfileUpdateStatus.PENDING
        };

        _dbContext.ExpertProfileUpdates.Add(pendingUpdate);

        // AvailabilityStatus can still be updated immediately as it doesn't require admin review
        if (request.AvailabilityStatus.HasValue)
        {
            profile.AvailabilityStatus = request.AvailabilityStatus.Value;
        }

        await _dbContext.SaveChangesAsync();

        var updateCounts = await GetCompletedProjectCountsAsync(new[] { profile.UserId });
        return MapToExpertProfileResponse(profile, updateCounts.GetValueOrDefault(profile.UserId));
    }

    public async Task<IdentityService.Response.UserResponse> UpdateUserAsync(Guid userId, Request.UpdateUserRequest request)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null) throw new NotFoundException("User not found.");

        if (request.FullName != null) user.FullName = request.FullName;
        if (request.AvatarUrl != null) user.AvatarUrl = request.AvatarUrl;
        if (request.Phone != null) user.Phone = request.Phone;

        await _dbContext.SaveChangesAsync();

        return new IdentityService.Response.UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            AvatarUrl = user.AvatarUrl,
            Phone = user.Phone,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            LastLoginAt = user.LastLoginAt
        };
    }

    public async Task<Response.ExpertProfileResponse> GetPublicExpertProfileAsync(Guid expertId)
    {
        var profile = await _dbContext.ExpertProfiles
            .Include(p => p.User)
            .Include(p => p.ExpertSkills)
                .ThenInclude(es => es.Skill)
            .FirstOrDefaultAsync(p => p.Id == expertId);

        if (profile == null)
        {
            profile = await _dbContext.ExpertProfiles
                .Include(p => p.User)
                .Include(p => p.ExpertSkills)
                    .ThenInclude(es => es.Skill)
                .FirstOrDefaultAsync(p => p.UserId == expertId);
        }

        if (profile == null) throw new NotFoundException("Expert profile not found.");
        if (profile.User == null) throw new NotFoundException("User associated with expert profile not found.");

        var publicCounts = await GetCompletedProjectCountsAsync(new[] { profile.UserId });
        return MapToExpertProfileResponse(profile, publicCounts.GetValueOrDefault(profile.UserId));
    }

    public async Task<List<Response.ExpertProfileResponse>> GetFeaturedExpertsAsync(int count)
    {
        var profiles = await _dbContext.ExpertProfiles
            .Include(p => p.User)
            .Include(p => p.ExpertSkills)
                .ThenInclude(es => es.Skill)
            .OrderByDescending(p => p.Rating)
            .ThenByDescending(p => p.SuccessRate)
            .Take(count)
            .ToListAsync();

        var featuredCounts = await GetCompletedProjectCountsAsync(profiles.Select(p => p.UserId).ToList());
        return profiles.Select(p => MapToExpertProfileResponse(p, featuredCounts.GetValueOrDefault(p.UserId))).ToList();
    }

    public async Task<Response.PaginatedExpertListResponse> SearchExpertsAsync(Request.SearchExpertsRequest request)
    {
        var query = _dbContext.ExpertProfiles
            .Include(p => p.User)
            .Include(p => p.ExpertSkills)
                .ThenInclude(es => es.Skill)
                        .AsQueryable();

        // Apply keyword search
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.ToLower();
            query = query.Where(p =>
                p.User.FullName.ToLower().Contains(keyword) ||
                p.Title.ToLower().Contains(keyword) ||
                p.Bio.ToLower().Contains(keyword) ||
                p.ExpertSkills.Any(es => es.Skill.Name.ToLower().Contains(keyword)));
        }

        // Apply category filter
        if (request.CategoryId.HasValue)
        {
            query = query.Where(p =>
                p.ExpertSkills.Any(es => es.Skill.CategoryId == request.CategoryId.Value));
        }

        // Get total count
        var totalCount = await query.CountAsync();

        // Apply pagination
        var experts = await query
            .OrderByDescending(p => p.Rating)
            .ThenByDescending(p => p.SuccessRate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        // Transform response with standardized skills
        var searchCounts = await GetCompletedProjectCountsAsync(experts.Select(p => p.UserId).ToList());
        var expertResponses = experts.Select(p => MapToExpertProfileResponse(p, searchCounts.GetValueOrDefault(p.UserId))).ToList();

        return new Response.PaginatedExpertListResponse
        {
            Experts = expertResponses,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
        };
    }

    public async Task<Response.PaginatedCompletedProjectListResponse> GetPublicCompletedProjectsAsync(Guid expertId, int page = 1, int pageSize = 10)
    {
        var expertProfile = await _dbContext.ExpertProfiles
            .FirstOrDefaultAsync(p => p.Id == expertId || p.UserId == expertId);

        if (expertProfile == null)
            throw new NotFoundException("Expert profile not found.");

        var expertUserId = expertProfile.UserId;

        var query = _dbContext.Projects.AsNoTracking()
            .Where(p => p.ExpertId == expertUserId && p.Status == ProjectStatus.COMPLETED);

        var totalCount = await query.CountAsync();

        var projects = await query
            .OrderByDescending(p => p.CompletedAt ?? p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var projectIds = projects.Select(p => p.Id).ToList();
        var reviews = await _dbContext.Reviews
            .Where(r => projectIds.Contains(r.ProjectId))
            .ToListAsync();

        var jobIds = projects.Select(p => p.JobId).Distinct().ToList();
        var jobs = await _dbContext.JobPosts
            .Include(j => j.Category)
            .Where(j => jobIds.Contains(j.Id))
            .ToListAsync();

        var clientIds = projects.Select(p => p.ClientId).Distinct().ToList();
        var clients = await _dbContext.Users
            .Where(u => clientIds.Contains(u.Id))
            .ToListAsync();

        var projectSummaries = projects.Select(p =>
        {
            var review = reviews.FirstOrDefault(r => r.ProjectId == p.Id);
            var job = jobs.FirstOrDefault(j => j.Id == p.JobId);
            var client = clients.FirstOrDefault(c => c.Id == p.ClientId);

            return new Response.CompletedProjectSummaryResponse
            {
                ProjectId = p.Id,
                Title = p.Title,
                Summary = p.Description,
                CategoryName = job?.Category?.Name,
                CompletedAt = p.CompletedAt,
                EndDate = p.EndDate,
                Rating = review?.Rating,
                ReviewComment = review?.Comment,
                TotalBudget = p.TotalBudget,
                Currency = string.IsNullOrWhiteSpace(p.Currency) ? "AICOIN" : p.Currency,
                ClientDisplayName = client?.FullName ?? "Client",
                ClientAvatarUrl = client?.AvatarUrl
            };
        }).ToList();

        return new Response.PaginatedCompletedProjectListResponse
        {
            Projects = projectSummaries,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        };
    }
}
