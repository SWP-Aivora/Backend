using Aivora.Repositories.Enums;

namespace Aivora.Services.ProjectService;

public class Response
{
    public class ProjectResponse
    {
        public Guid Id { get; set; }
        public Guid? JobId { get; set; }
        public Guid? AcceptedProposalId { get; set; }
        public Guid? ServiceRequestId { get; set; }
        public Guid ClientId { get; set; }
        public string ClientName { get; set; } = null!;
        public Guid ExpertId { get; set; }
        public string ExpertName { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public decimal TotalBudget { get; set; }
        public string Currency { get; set; } = null!;
        public ProjectStatus Status { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public List<MilestoneInfo> Milestones { get; set; } = new();
    }

    public class MilestoneInfo
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = null!;
        public MilestoneStatus Status { get; set; }
        public int OrderIndex { get; set; }
        public DateOnly? DueDate { get; set; }
    }
}
