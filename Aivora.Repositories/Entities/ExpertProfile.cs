using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class ExpertProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public string? Title { get; set; }
    public string? Bio { get; set; }
    public decimal? HourlyRate { get; set; }
    public int ExperienceYears { get; set; }
    public AvailabilityStatus AvailabilityStatus { get; set; } = AvailabilityStatus.AVAILABLE;
    public decimal RatingAvg { get; set; }
    public int CompletedProjects { get; set; }
    public decimal SuccessRate { get; set; }
    public int? ResponseTimeMinutes { get; set; }

    // Navigation Properties
    public virtual User User { get; set; } = null!;
    public virtual ICollection<ExpertSkill> ExpertSkills { get; set; } = new List<ExpertSkill>();
}
