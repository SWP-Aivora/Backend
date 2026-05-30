using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
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
            Description = profile.Description
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
            Description = profile.Description
        };
    }

    public async Task<Response.ExpertProfileResponse> GetExpertProfileAsync(Guid userId)
    {
        var profile = await _dbContext.ExpertProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) throw new NotFoundException("Expert profile not found.");

        return new Response.ExpertProfileResponse
        {
            UserId = profile.UserId,
            Title = profile.Title,
            Bio = profile.Bio,
            HourlyRate = profile.HourlyRate,
            ExperienceYears = profile.ExperienceYears,
            AvailabilityStatus = profile.AvailabilityStatus,
            RatingAvg = profile.RatingAvg,
            CompletedProjects = profile.CompletedProjects,
            SuccessRate = profile.SuccessRate
        };
    }

    public async Task<Response.ExpertProfileResponse> UpdateExpertProfileAsync(Guid userId, Request.UpdateExpertProfileRequest request)
    {
        var profile = await _dbContext.ExpertProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
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
            Title = profile.Title,
            Bio = profile.Bio,
            HourlyRate = profile.HourlyRate,
            ExperienceYears = profile.ExperienceYears,
            AvailabilityStatus = profile.AvailabilityStatus,
            RatingAvg = profile.RatingAvg,
            CompletedProjects = profile.CompletedProjects,
            SuccessRate = profile.SuccessRate
        };
    }
}
