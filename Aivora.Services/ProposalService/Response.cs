using Aivora.Repositories.Enums;

namespace Aivora.Services.ProposalService;

public class Response
{
    public class ProposalResponse
    {
        public Guid Id { get; set; }
        public Guid JobId { get; set; }
        public string JobTitle { get; set; } = null!;
        public Guid ClientId { get; set; }
        public string ClientName { get; set; } = null!;
        public Guid ExpertId { get; set; }
        public string ExpertName { get; set; } = null!;
        public string CoverLetter { get; set; } = null!;
        public decimal ProposedBudget { get; set; }
        public int? ProposedTimelineDays { get; set; }
        public string Currency { get; set; } = null!;
        public ProposalStatus Status { get; set; }
        public DateTimeOffset SubmittedAt { get; set; }
        public List<ProposalMilestoneResponse> Milestones { get; set; } = new();
    }

    public class ProposalMilestoneResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public int DueDays { get; set; }
        public string? AcceptanceCriteria { get; set; }
        public int OrderIndex { get; set; }
    }
}
