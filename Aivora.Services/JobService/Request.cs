using Aivora.Repositories.Enums;

namespace Aivora.Services.JobService;

public class Request
{
    public class CreateJobRequest
    {
        public string Title { get; set; } = null!;
        public string OriginalDescription { get; set; } = null!;
        public string? FinalDescription { get; set; }
        public string? BusinessDomain { get; set; }
        public string? ExpectedOutcome { get; set; }
        public Guid CategoryId { get; set; }
        public BudgetType BudgetType { get; set; }
        public decimal? BudgetMin { get; set; }
        public decimal? BudgetMax { get; set; }
        public string? Currency { get; set; }
        public int? TimelineDays { get; set; }
        public DateOnly? Deadline { get; set; }
        public SkillLevel? ExperienceLevel { get; set; }
        public JobVisibility Visibility { get; set; }
        public List<Guid> SkillIds { get; set; } = new();
        public List<CreateJobMilestoneRequest> Milestones { get; set; } = new();
    }

    public class CreateJobMilestoneRequest
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public int DueDays { get; set; }
        public string? AcceptanceCriteria { get; set; }
        public int OrderIndex { get; set; }
    }

    public class UpdateJobRequest
    {
        public string? Title { get; set; }
        public string? FinalDescription { get; set; }
        public string? BusinessDomain { get; set; }
        public string? ExpectedOutcome { get; set; }
        public Guid? CategoryId { get; set; }
        public BudgetType? BudgetType { get; set; }
        public decimal? BudgetMin { get; set; }
        public decimal? BudgetMax { get; set; }
        public string? Currency { get; set; }
        public int? TimelineDays { get; set; }
        public DateOnly? Deadline { get; set; }
        public SkillLevel? ExperienceLevel { get; set; }
        public JobVisibility? Visibility { get; set; }
        public List<Guid>? SkillIds { get; set; }
    }
}
