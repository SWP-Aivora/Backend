using Aivora.Repositories.Data;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.IdentityService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aivora.Services.AdminService;

public class AdminService : IAdminService
{
    private readonly AivoraDbContext _dbContext;
    private readonly ILogger<AdminService> _logger;

    public AdminService(AivoraDbContext dbContext, ILogger<AdminService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IdentityService.Response.UserResponse> SuspendUserAsync(Guid adminId, Guid userId, string reason)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null) throw new NotFoundException("User not found.");

        if (user.Role == UserRole.ADMIN) throw new ValidationException("Cannot suspend another admin.");
        if (user.Status == UserStatus.SUSPENDED) throw new ValidationException("User is already suspended.");

        user.Status = UserStatus.SUSPENDED;
        // Optionally store the reason in a log or field if we add one later

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Admin {AdminId} suspended user {UserId}. Reason: {Reason}", adminId, userId, reason);

        return MapToResponse(user);
    }

    public async Task<IdentityService.Response.UserResponse> UnsuspendUserAsync(Guid adminId, Guid userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null) throw new NotFoundException("User not found.");

        if (user.Status != UserStatus.SUSPENDED) throw new ValidationException("User is not suspended.");

        user.Status = UserStatus.ACTIVE;
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Admin {AdminId} unsuspended user {UserId}", adminId, userId);

        return MapToResponse(user);
    }

    public async Task<Aivora.Services.Base.Response.PageResult<IdentityService.Response.UserResponse>> GetUsersAsync(Aivora.Services.Base.Request.PageRequest pageRequest, string? search = null)
    {
        var query = _dbContext.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => u.Email.Contains(search) || u.FullName.Contains(search));
        }

        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((pageRequest.PageIndex - 1) * pageRequest.PageSize)
            .Take(pageRequest.PageSize)
            .ToListAsync();

        return new Aivora.Services.Base.Response.PageResult<IdentityService.Response.UserResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalItems = totalItems,
            PageIndex = pageRequest.PageIndex,
            PageSize = pageRequest.PageSize
        };
    }

    public async Task<Response.DashboardStatsResponse> GetDashboardStatsAsync()
    {
        return new Response.DashboardStatsResponse
        {
            TotalUsers = await _dbContext.Users.CountAsync(),
            TotalClients = await _dbContext.Users.CountAsync(u => u.Role == UserRole.CLIENT),
            TotalExperts = await _dbContext.Users.CountAsync(u => u.Role == UserRole.EXPERT),
            TotalJobs = await _dbContext.JobPosts.CountAsync(),
            ActiveProjects = await _dbContext.Projects.CountAsync(p => p.Status == ProjectStatus.ACTIVE),
            OpenDisputes = await _dbContext.Disputes.CountAsync(d => d.Status == DisputeStatus.OPEN),
            TotalEscrowAmount = await _dbContext.Wallets.SumAsync(w => w.HeldBalance)
        };
    }

    private static IdentityService.Response.UserResponse MapToResponse(Aivora.Repositories.Entities.User u)
    {
        return new IdentityService.Response.UserResponse
        {
            Id = u.Id,
            Email = u.Email,
            FullName = u.FullName,
            AvatarUrl = u.AvatarUrl,
            Phone = u.Phone,
            Role = u.Role.ToString(),
            Status = u.Status.ToString(),
            LastLoginAt = u.LastLoginAt
        };
    }
}
