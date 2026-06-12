using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.ProfileService;

public class ProfileApplicationService : IService
{
    private readonly AivoraDbContext _dbContext;

    public ProfileApplicationService(AivoraDbContext dbContext)
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

        profile.CompanyName = request.CompanyName;
        profile.Industry = request.Industry;
        profile.CompanySize = request.CompanySize;
        profile.Website = request.Website;
        profile.Description = request.Description;

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
            .FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) throw new NotFoundException("Expert profile not found.");

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
            CompletedProjects = profile.CompletedProjects,
            SuccessRate = profile.SuccessRate
        };
    }

    public async Task<Response.ExpertProfileResponse> UpdateExpertProfileAsync(Guid userId, Request.UpdateExpertProfileRequest request)
    {
        var profile = await _dbContext.ExpertProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) throw new NotFoundException("Expert profile not found.");

        profile.Title = request.Title;
        profile.Bio = request.Bio;
        profile.HourlyRate = request.HourlyRate;
        profile.ExperienceYears = request.ExperienceYears;
        profile.AvailabilityStatus = request.AvailabilityStatus;

        await _dbContext.SaveChangesAsync();

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
            CompletedProjects = profile.CompletedProjects,
            SuccessRate = profile.SuccessRate
        };
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
            .FirstOrDefaultAsync(p => p.Id == expertId);

        if (profile == null)
        {
            profile = await _dbContext.ExpertProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == expertId);
        }

        if (profile == null) throw new NotFoundException("Expert profile not found.");

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
            CompletedProjects = profile.CompletedProjects,
            SuccessRate = profile.SuccessRate
        };
    }

    public async Task<List<Response.ExpertProfileResponse>> GetFeaturedExpertsAsync(int count)
    {
        var profiles = await _dbContext.ExpertProfiles
            .Include(p => p.User)
            .OrderByDescending(p => p.Rating)
            .ThenByDescending(p => p.SuccessRate)
            .Take(count)
            .ToListAsync();

        return profiles.Select(p => new Response.ExpertProfileResponse
        {
            UserId = p.UserId,
            FullName = p.User.FullName,
            AvatarUrl = p.User.AvatarUrl,
            Title = p.Title,
            Bio = p.Bio,
            HourlyRate = p.HourlyRate,
            ExperienceYears = p.ExperienceYears,
            AvailabilityStatus = p.AvailabilityStatus,
            Rating = p.Rating,
            TotalReviews = p.TotalReviews,
            CompletedProjects = p.CompletedProjects,
            SuccessRate = p.SuccessRate
        }).ToList();
    }
}
