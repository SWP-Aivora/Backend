using Aivora.Repositories.Abstractions;
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class JobPost : AuditableBaseEntity
{
    public Guid ClientId { get; set; }
    public Guid? CategoryId { get; set; }
    public string Title { get; set; } = null!;
    public string OriginalDescription { get; set; } = null!;
    public string? FinalDescription { get; set; }
    public string? BusinessDomain { get; set; }
    public string? ExpectedOutcome { get; set; }
    public BudgetType BudgetType { get; set; } = BudgetType.FIXED;
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public string Currency { get; set; } = "AICOIN";
    public int? TimelineDays { get; set; }
    public DateOnly? Deadline { get; set; }
    public SkillLevel? ExperienceLevel { get; set; }
    public JobStatus Status { get; set; } = JobStatus.DRAFT;
    public JobVisibility Visibility { get; set; } = JobVisibility.PUBLIC;
    public DateTimeOffset? PublishedAt { get; set; }

    // Navigation Properties
    public virtual User Client { get; set; } = null!;
    public virtual Category? Category { get; set; }
    public virtual ICollection<JobSkill> JobSkills { get; set; } = new List<JobSkill>();
    public virtual ICollection<JobPostMilestone> Milestones { get; set; } = new List<JobPostMilestone>();
    public virtual ICollection<Proposal> Proposals { get; set; } = new List<Proposal>();
    public virtual ICollection<RecommendationResult> RecommendationResults { get; set; } = new List<RecommendationResult>();
}

