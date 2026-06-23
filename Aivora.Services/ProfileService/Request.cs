using Aivora.Repositories.Enums;

namespace Aivora.Services.ProfileService;

public class Request
{
    public class UpdateClientProfileRequest
    {
        public string? CompanyName { get; set; }
        public string? Industry { get; set; }
        public string? CompanySize { get; set; }
        public string? Website { get; set; }
        public string? Description { get; set; }
    }

    public class UpdateExpertProfileRequest
    {
        public string? Title { get; set; }
        public string? Bio { get; set; }
        public decimal? HourlyRate { get; set; }
        public int ExperienceYears { get; set; }
        public AvailabilityStatus AvailabilityStatus { get; set; }
    }

    public class UpdateUserRequest
    {
        public string? FullName { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Phone { get; set; }
    }

    public class SearchExpertsRequest
    {
        public string? Keyword { get; set; }
        public Guid? CategoryId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
