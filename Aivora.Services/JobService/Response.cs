using Aivora.Repositories.Enums;
using Aivora.Services.Base;

namespace Aivora.Services.JobService;

public class Response
{
    public class JobResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string OriginalDescription { get; set; } = null!;
        public string? FinalDescription { get; set; }
        public string? BusinessDomain { get; set; }
        public string? ExpectedOutcome { get; set; }
        public Guid ClientId { get; set; }
        public string ClientName { get; set; } = null!;
        public Guid? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public BudgetType BudgetType { get; set; }
        public decimal? BudgetMin { get; set; }
        public decimal? BudgetMax { get; set; }
        public string Currency { get; set; } = null!;
        public int? TimelineDays { get; set; }
        public DateOnly? Deadline { get; set; }
        public SkillLevel? ExperienceLevel { get; set; }
        public JobStatus Status { get; set; }
        public JobVisibility Visibility { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public List<SkillInfo> Skills { get; set; } = new();
        public List<JobMilestoneResponse> Milestones { get; set; } = new();
    }

    public class JobMilestoneResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public int OrderIndex { get; set; }
    }

    public class SkillInfo
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
    }
}
