using Aivora.Repositories.Enums;

namespace Aivora.Services.ProfileService;

public class Response
{
    public class ClientProfileResponse
    {
        public Guid UserId { get; set; }
        public string? CompanyName { get; set; }
        public string? Industry { get; set; }
        public string? CompanySize { get; set; }
        public string? Website { get; set; }
        public string? Description { get; set; }
        public decimal Rating { get; set; }
        public int TotalReviews { get; set; }
    }

    public class ExpertProfileResponse
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public string? Title { get; set; }
        public string? Bio { get; set; }
        public decimal? HourlyRate { get; set; }
        public int ExperienceYears { get; set; }
        public AvailabilityStatus AvailabilityStatus { get; set; }
        public decimal Rating { get; set; }
        public int TotalReviews { get; set; }
        public int CompletedProjects { get; set; }
        public decimal SuccessRate { get; set; }
        public List<ExpertSkillResponse> Skills { get; set; } = null!;
    }

    public class ExpertSkillResponse
    {
        public Guid SkillId { get; set; }
        public string SkillName { get; set; } = null!;
        public int ProficiencyLevel { get; set; }
    }

    public class PaginatedExpertListResponse
    {
        public List<ExpertProfileResponse> Experts { get; set; } = null!;
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class CompletedProjectSummaryResponse
    {
        public Guid ProjectId { get; set; }
        public string Title { get; set; } = null!;
        public string? Summary { get; set; }
        public string? CategoryName { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public DateOnly? EndDate { get; set; }
        public int? Rating { get; set; }
        public string? ReviewComment { get; set; }
        public decimal? TotalBudget { get; set; }
        public string Currency { get; set; } = "AICOIN";
        public string ClientDisplayName { get; set; } = null!;
        public string? ClientAvatarUrl { get; set; }
    }

    public class PaginatedCompletedProjectListResponse
    {
        public List<CompletedProjectSummaryResponse> Projects { get; set; } = null!;
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}

