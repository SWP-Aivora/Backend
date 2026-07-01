using Aivora.Repositories.Data;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.IdentityService;
using Aivora.Services.NotificationService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aivora.Services.AdminService;

public class AdminService : IAdminService
{
    private readonly AivoraDbContext _dbContext;
    private readonly ILogger<AdminService> _logger;
    private readonly NotificationService.IService _notificationService;

    public AdminService(AivoraDbContext dbContext, ILogger<AdminService> logger, NotificationService.IService notificationService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<IdentityService.Response.UserResponse> SuspendUserAsync(Guid adminId, Guid userId, string reason)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null) throw new NotFoundException("User not found.");

        if (user.Role == UserRole.ADMIN) throw new ValidationException("Cannot suspend another admin.");
        if (user.Status == UserStatus.SUSPENDED) throw new ValidationException("User is already suspended.");

        user.Status = UserStatus.SUSPENDED;

        await _dbContext.SaveChangesAsync();

        // Send notification to the suspended user
        try
        {
            await _notificationService.SendNotificationAsync(
                userId,
                "Your account has been suspended",
                $"Your account has been suspended by an administrator. Reason: {reason}",
                "ACCOUNT",
                null
            );
        }
        catch
        {
            // Notification failure should not block the main business flow
        }

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

        // Send notification to the unsuspended user
        try
        {
            await _notificationService.SendNotificationAsync(
                userId,
                "Your account has been unsuspended",
                "Your account has been unsuspended by an administrator. You can continue using Aivora.",
                "ACCOUNT",
                null
            );
        }
        catch
        {
            // Notification failure should not block the main business flow
        }

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

    public async Task<Aivora.Services.Base.Response.PageResult<Response.ExpertReviewResponse>> GetExpertReviewsAsync(Aivora.Services.Base.Request.PageRequest pageRequest, string? search = null)
    {
        var query = _dbContext.Reviews
            .Include(r => r.Reviewer)
            .Include(r => r.Reviewee)
            .Include(r => r.Project)
            .Where(r => r.Reviewee.Role == UserRole.EXPERT);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(r =>
                r.Reviewee.FullName.Contains(search) ||
                r.Reviewer.FullName.Contains(search) ||
                r.Project.Title.Contains(search));
        }

        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageRequest.PageIndex - 1) * pageRequest.PageSize)
            .Take(pageRequest.PageSize)
            .ToListAsync();

        return new Aivora.Services.Base.Response.PageResult<Response.ExpertReviewResponse>
        {
            Items = items.Select(r => new Response.ExpertReviewResponse
            {
                Id = r.Id,
                ProjectId = r.ProjectId,
                ProjectTitle = r.Project.Title,
                ReviewerId = r.ReviewerId,
                ReviewerName = r.Reviewer.FullName,
                RevieweeId = r.RevieweeId,
                RevieweeName = r.Reviewee.FullName,
                Rating = r.Rating,
                Comment = r.Comment,
                CommunicationRating = r.CommunicationRating,
                QualityRating = r.QualityRating,
                DeadlineRating = r.DeadlineRating,
                CreatedAt = r.CreatedAt
            }).ToList(),
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

    public async Task<Response.ExpertProfileUpdateResponse> ReviewExpertProfileUpdateAsync(Guid adminId, Guid updateId, Request.ReviewExpertProfileUpdateRequest request)
    {
        var update = await _dbContext.ExpertProfileUpdates
            .Include(u => u.ExpertProfile)
            .FirstOrDefaultAsync(u => u.Id == updateId);

        if (update == null) throw new NotFoundException("Profile update not found.");
        if (update.Status != ProfileUpdateStatus.PENDING) throw new ValidationException("Only pending updates can be reviewed.");

        update.AdminId = adminId;
        update.ReviewedAt = DateTimeOffset.UtcNow;

        if (request.IsApproved)
        {
            update.Status = ProfileUpdateStatus.APPROVED;

            // Apply changes to ExpertProfile
            if (update.Title != null) update.ExpertProfile.Title = update.Title;
            if (update.Bio != null) update.ExpertProfile.Bio = update.Bio;
            if (update.HourlyRate != null) update.ExpertProfile.HourlyRate = update.HourlyRate;
            if (update.ExperienceYears != null) update.ExpertProfile.ExperienceYears = update.ExperienceYears.Value;

            await _dbContext.SaveChangesAsync();

            // Send notification to expert
            try
            {
                await _notificationService.SendNotificationAsync(
                    update.ExpertProfile.UserId,
                    "Profile Update Approved",
                    "Your profile update request has been approved and applied.",
                    "ACCOUNT",
                    "/profile"
                );
            }
            catch { }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.RejectionReason))
                throw new ValidationException("Rejection reason is required when rejecting an update.");

            update.Status = ProfileUpdateStatus.REJECTED;
            update.RejectionReason = request.RejectionReason;

            await _dbContext.SaveChangesAsync();

            // Send notification to expert
            try
            {
                await _notificationService.SendNotificationAsync(
                    update.ExpertProfile.UserId,
                    "Profile Update Rejected",
                    $"Your profile update request was rejected. Reason: {request.RejectionReason}",
                    "ACCOUNT",
                    "/profile"
                );
            }
            catch { }
        }

        _logger.LogInformation("Admin {AdminId} reviewed profile update {UpdateId}. Approved: {IsApproved}", adminId, updateId, request.IsApproved);

        return new Response.ExpertProfileUpdateResponse
        {
            Id = update.Id,
            ExpertProfileId = update.ExpertProfileId,
            Title = update.Title,
            Bio = update.Bio,
            HourlyRate = update.HourlyRate,
            ExperienceYears = update.ExperienceYears,
            Status = update.Status.ToString(),
            RejectionReason = update.RejectionReason,
            CreatedAt = update.CreatedAt
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
