using Aivora.Repositories.Enums;

namespace Aivora.Services.ProposalService;

public class Request
{
    public class CreateProposalRequest
    {
        public Guid JobId { get; set; }
        public string CoverLetter { get; set; } = null!;
        public decimal ProposedBudget { get; set; }
        public int? ProposedTimelineDays { get; set; }
        public List<CreateProposalMilestoneRequest> Milestones { get; set; } = new();
    }

    public class CreateProposalMilestoneRequest
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public int DueDays { get; set; }
        public string? AcceptanceCriteria { get; set; }
        public int OrderIndex { get; set; }
    }
    public class UpdateProposalRequest
    {
        public string CoverLetter { get; set; } = null!;
        public decimal ProposedBudget { get; set; }
        public int? ProposedTimelineDays { get; set; }
        public List<UpdateProposalMilestoneRequest> Milestones { get; set; } = new();
    }

    public class UpdateProposalMilestoneRequest
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public int DueDays { get; set; }
        public string? AcceptanceCriteria { get; set; }
        public int OrderIndex { get; set; }
    }

}
