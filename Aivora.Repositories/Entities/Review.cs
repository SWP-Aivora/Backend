using Aivora.Repositories.Abstractions;
namespace Aivora.Repositories.Entities;

public class Review : AuditableBaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid ReviewerId { get; set; }
    public Guid RevieweeId { get; set; }
    public int Rating { get; set; } // 1-5
    public string? Comment { get; set; }
    public int? CommunicationRating { get; set; }
    public int? QualityRating { get; set; }
    public int? DeadlineRating { get; set; }
    public int? RequirementClarityRating { get; set; }

    // Navigation Properties
    public virtual Project Project { get; set; } = null!;
    public virtual User Reviewer { get; set; } = null!;
    public virtual User Reviewee { get; set; } = null!;
}

