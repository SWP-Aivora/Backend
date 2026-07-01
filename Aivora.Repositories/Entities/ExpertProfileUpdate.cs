using Aivora.Repositories.Abstractions;
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class ExpertProfileUpdate : AuditableBaseEntity
{
    public Guid ExpertProfileId { get; set; }
    public string? Title { get; set; }
    public string? Bio { get; set; }
    public decimal? HourlyRate { get; set; }
    public int? ExperienceYears { get; set; }

    public ProfileUpdateStatus Status { get; set; } = ProfileUpdateStatus.PENDING;
    public Guid? AdminId { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    // Navigation Properties
    public virtual ExpertProfile ExpertProfile { get; set; } = null!;
    public virtual User? Admin { get; set; }
}
