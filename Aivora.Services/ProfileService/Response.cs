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
        public string? Title { get; set; }
        public string? Bio { get; set; }
        public decimal? HourlyRate { get; set; }
        public int ExperienceYears { get; set; }
        public AvailabilityStatus AvailabilityStatus { get; set; }
        public decimal Rating { get; set; }
        public int TotalReviews { get; set; }
        public int CompletedProjects { get; set; }
        public decimal SuccessRate { get; set; }
    }
}
