using Aivora.Services.Base;
using Aivora.Services.IdentityService;

namespace Aivora.Services.AdminService;

public interface IAdminService
{
    Task<IdentityService.Response.UserResponse> SuspendUserAsync(Guid adminId, Guid userId, string reason);
    Task<IdentityService.Response.UserResponse> UnsuspendUserAsync(Guid adminId, Guid userId);
    Task<Aivora.Services.Base.Response.PageResult<IdentityService.Response.UserResponse>> GetUsersAsync(Aivora.Services.Base.Request.PageRequest pageRequest, string? search = null);
    Task<Response.DashboardStatsResponse> GetDashboardStatsAsync();
    Task<Aivora.Services.Base.Response.PageResult<Response.ExpertReviewResponse>> GetExpertReviewsAsync(Aivora.Services.Base.Request.PageRequest pageRequest, string? search = null);
    Task<Aivora.Services.Base.Response.PageResult<Response.ExpertProfileUpdateResponse>> GetExpertProfileUpdatesAsync(Aivora.Services.Base.Request.PageRequest pageRequest, string? status = null);
    Task<Response.ExpertProfileUpdateResponse> ReviewExpertProfileUpdateAsync(Guid adminId, Guid updateId, Request.ReviewExpertProfileUpdateRequest request);
}

public class Response
{
    public class DashboardStatsResponse
    {
        public int TotalUsers { get; set; }
        public int TotalClients { get; set; }
        public int TotalExperts { get; set; }
        public int TotalJobs { get; set; }
        public int ActiveProjects { get; set; }
        public int OpenDisputes { get; set; }
        public decimal TotalEscrowAmount { get; set; }
    }

    public class ExpertReviewResponse
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string ProjectTitle { get; set; } = null!;
        public Guid ReviewerId { get; set; }
        public string ReviewerName { get; set; } = null!;
        public Guid RevieweeId { get; set; }
        public string RevieweeName { get; set; } = null!;
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public int? CommunicationRating { get; set; }
        public int? QualityRating { get; set; }
        public int? DeadlineRating { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class ExpertProfileUpdateResponse
    {
        public Guid Id { get; set; }
        public Guid ExpertProfileId { get; set; }
        public string? Title { get; set; }
        public string? Bio { get; set; }
        public decimal? HourlyRate { get; set; }
        public int? ExperienceYears { get; set; }
        public string Status { get; set; } = null!;
        public string? RejectionReason { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}

public class Request
{
    public class SuspendUserRequest
    {
        public string Reason { get; set; } = null!;
    }

    public class ReviewExpertProfileUpdateRequest
    {
        public bool IsApproved { get; set; }
        public string? RejectionReason { get; set; }
    }
}
